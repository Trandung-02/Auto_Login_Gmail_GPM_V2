using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

ConfigureConsoleUtf8();

const string GmailInboxUrl = "https://mail.google.com/mail/u/0/";
const int DefaultSecretColumnIndex = 2; // cột 3 trong Account.txt (0-based): secret TOTP / app

var root = FindProjectRoot();
var accountPath = Path.Combine(root, "Data", "Account.txt");
var cdpUrl = ResolveCdpUrl(root);
var columnIndex = DefaultSecretColumnIndex;
int? cdpColumnIndex = null; // 0-based; cột CDP/port trong Account (nhiều profile GPM)
var autoFindCdp = string.Equals(Environment.GetEnvironmentVariable("FILL2FA_AUTO_FIND_CDP"), "1", StringComparison.OrdinalIgnoreCase);
var cdpScanMin = 9222;
var cdpScanMax = 9300;
int[]? cdpRotatePortsFromArgs = null;
var autoGpmCdp = OperatingSystem.IsWindows();
int? gpmProfileColumnIndex = null;
int? onlyLine1Based = null;

for (var i = 0; i < args.Length; i++)
{
    if (args[i] is "--account" && i + 1 < args.Length)
        accountPath = Path.GetFullPath(args[++i]);
    else if (args[i] is "--cdp" && i + 1 < args.Length)
        cdpUrl = args[++i];
    else if (args[i] is "--column" && i + 1 < args.Length && int.TryParse(args[++i], out var c) && c >= 1)
        columnIndex = c - 1;
    else if (args[i] is "--cdp-column" && i + 1 < args.Length && int.TryParse(args[++i], out var cc) && cc >= 1)
        cdpColumnIndex = cc - 1;
    else if (args[i] is "--gpm-profile-column" && i + 1 < args.Length && int.TryParse(args[++i], out var gpc) && gpc >= 1)
        gpmProfileColumnIndex = gpc - 1;
    else if (args[i] is "--no-auto-gpm-cdp")
        autoGpmCdp = false;
    else if (args[i] is "--auto-gpm-cdp")
        autoGpmCdp = true;
    else if (args[i] is "--cdp-ports" && i + 1 < args.Length)
    {
        cdpRotatePortsFromArgs = ParseCdpPortsList(args[++i]);
        if (cdpRotatePortsFromArgs is null || cdpRotatePortsFromArgs.Length == 0)
            Console.WriteLine("[Fill2faLive] Canh bao: --cdp-ports khong hop le, bo qua.");
    }
    else if (args[i] is "--only-line" && i + 1 < args.Length && int.TryParse(args[++i], out var onlyLn) && onlyLn >= 1)
        onlyLine1Based = onlyLn;
    else if (args[i] is "--auto-find-cdp")
        autoFindCdp = true;
    else if (args[i] is "--cdp-scan-range" && i + 1 < args.Length)
    {
        if (TryParseCdpScanRange(args[i + 1], out var smin, out var smax))
        {
            cdpScanMin = smin;
            cdpScanMax = smax;
            i++;
        }
    }
    else if (args[i] is "-h" or "--help")
    {
        PrintHelp(accountPath, cdpUrl, columnIndex, cdpColumnIndex, autoFindCdp, cdpScanMin, cdpScanMax, cdpRotatePortsFromArgs, autoGpmCdp, onlyLine1Based);
        return 0;
    }
}

var cdpRotatePorts = (cdpRotatePortsFromArgs is { Length: > 0 })
    ? cdpRotatePortsFromArgs
    : ReadCdpPortsFile(root);

if (!File.Exists(accountPath))
{
    Console.Error.WriteLine($"Không thấy file: {accountPath}");
    return 1;
}

var lines = File.ReadAllLines(accountPath)
    .Select(l => l.Trim())
    .Where(l => l.Length > 0 && !l.StartsWith('#'))
    .ToList();

if (lines.Count == 0)
{
    Console.WriteLine("Account.txt không có dòng hợp lệ.");
    return 0;
}

if (onlyLine1Based is int ol && (ol < 1 || ol > lines.Count))
{
    Console.Error.WriteLine($"[Fill2faLive] --only-line {ol} không hợp lệ (Account.txt có {lines.Count} dòng sau khi bỏ comment/rỗng).");
    return 1;
}

if (onlyLine1Based is not null)
    Console.WriteLine($"[Fill2faLive] Chỉ xử lý dòng {onlyLine1Based} (PlayAPP: đúng account vừa login; không quét hết file).");

var gpmProfilesRoot = GpmChromeDiscovery.ResolveGpmProfilesRoot(root);
IReadOnlyList<GpmChromeRuntime> gpmRuntimes = Array.Empty<GpmChromeRuntime>();
if (autoGpmCdp && gpmProfilesRoot is not null)
{
    gpmRuntimes = GpmChromeDiscovery.EnumerateGpmChromeRuntimes(gpmProfilesRoot);
    if (gpmRuntimes.Count > 0)
    {
        Console.WriteLine($"[Fill2faLive] GPM tu dong (WMI): {gpmRuntimes.Count} Chrome dang chay duoi {gpmProfilesRoot}");
        foreach (var g in gpmRuntimes)
            Console.WriteLine($"         profile \"{g.ProfileKey}\" -> {g.CdpHttpUrl} (PID start order)");
    }
    else
        Console.WriteLine($"[Fill2faLive] GPM: khong thay Chrome nao (--user-data-dir trong {gpmProfilesRoot}). Hay mo profile GPM truoc.");
}

using var playwright = await Playwright.CreateAsync();

var usePerLineCdp = (cdpRotatePorts is { Length: > 0 }) || cdpColumnIndex is not null || gpmRuntimes.Count > 0;

// Mot CDP chung: chi khi khong co cot CDP, khong co CdpPorts.txt, khong co GPM WMI.
if (!usePerLineCdp)
{
    if (!await IsCdpReachableAsync(cdpUrl, millisecondsTimeout: 2000))
    {
        if (autoFindCdp)
        {
            var host = CdpHostFromUrl(cdpUrl);
            Console.WriteLine($"[Fill2faLive] Không mở được {cdpUrl}, đang quét CDP {host} cổng {cdpScanMin}-{cdpScanMax}...");
            var found = await FindWorkingCdpUrlAsync(playwright, host, cdpScanMin, cdpScanMax);
            if (found is null)
            {
                Console.WriteLine("[Fill2faLive] Không thấy trong dải trên; đang quét mọi cổng TCP LISTEN trên loopback (Chrome /json/version)...");
                using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(600) };
                found = await FindCdpAmongListeningPortsAsync(playwright, http, host);
            }
            if (found is null)
            {
                Console.Error.WriteLine("Không tìm thấy Chrome DevTools (CDP) trên máy bạn.");
                Console.Error.WriteLine("GPM có thể không bật remote debugging: kiểm tra trong profile có thêm đúng --remote-debugging-port=... hay không.");
                Console.Error.WriteLine("Mở Chrome GPM -> gõ chrome://version -> xem dòng \"Command Line\" có chứa remote-debugging-port không.");
                PrintCdpTroubleshootingEnglish(cdpUrl, null);
                return 1;
            }
            cdpUrl = found;
            Console.WriteLine($"[Fill2faLive] Tìm thấy CDP: {cdpUrl}");
        }
        else
        {
            Console.Error.WriteLine($"Không có tiến trình nào lắng nghe CDP tại {cdpUrl} (kiểm tra TCP thất bại).");
            Console.Error.WriteLine("Gợi ý: chạy kèm --auto-find-cdp (hoặc set FILL2FA_AUTO_FIND_CDP=1) để quét cổng 9222-9250.");
            PrintCdpTroubleshootingEnglish(cdpUrl, null);
            return 1;
        }
    }
}
IBrowser? browser = null;
string? attachedCdp = null;

try
{
    if (lines.Count > 1 && cdpColumnIndex is null && (cdpRotatePorts is null || cdpRotatePorts.Length < 2) && gpmRuntimes.Count == 0)
    {
        Console.WriteLine("[Fill2faLive] Canh bao: nhieu dong Account nhung chi mot CDP (ChromeCdp.txt / auto-find).");
        Console.WriteLine("            Moi profile GPM can 1 tab 2fa rieng -> tao Data\\CdpPorts.txt (cong phan cach dau phay, thu tu trung thu tu Account)");
        Console.WriteLine("            hoac: Fill2faLive.exe --cdp-ports 55055,58331  hoac cot CDP trong Account + --cdp-column N\n");
    }
    else if (cdpRotatePorts is { Length: > 0 })
    {
        Console.WriteLine($"[Fill2faLive] CDP xoay vong theo dong: {string.Join(", ", cdpRotatePorts)} (dong 1 -> cong dau, dong 2 -> cong tiep...)\n");
    }
    else if (gpmRuntimes.Count > 0 && lines.Count > gpmRuntimes.Count && gpmProfileColumnIndex is null && cdpColumnIndex is null && (cdpRotatePorts is null || cdpRotatePorts.Length < lines.Count))
    {
        Console.WriteLine("[Fill2faLive] Canh bao: nhieu dong Account hon so Chrome GPM dang chay; CDP se lap lai theo thu tu WMI.");
        Console.WriteLine("            Mo du profile GPM, hoac --gpm-profile-column N + ten thu muc profile trong cot do, hoac Data\\CdpPorts.txt.\n");
    }

    Console.WriteLine(
        cdpColumnIndex is not null
            ? $"Xử lý {lines.Count} tài khoản (cột secret = {columnIndex + 1}, CDP theo cột {cdpColumnIndex + 1}).\n"
            : cdpRotatePorts is { Length: > 0 }
                ? $"Xử lý {lines.Count} tài khoản (cột secret = {columnIndex + 1}, CDP theo CdpPorts / --cdp-ports).\n"
                : gpmRuntimes.Count > 0
                    ? $"Xử lý {lines.Count} tài khoản (cột secret = {columnIndex + 1}, CDP tu dong GPM / WMI, {gpmRuntimes.Count} Chrome).\n"
                    : $"Xử lý {lines.Count} tài khoản (cột secret = {columnIndex + 1}, một CDP chung).\n");

    for (var i = 0; i < lines.Count; i++)
    {
        if (onlyLine1Based is int wantLine && wantLine != i + 1)
            continue;

        var parts = lines[i].Split('|');
        if (columnIndex >= parts.Length)
        {
            Console.WriteLine($"[{i + 1}] Bỏ qua: không đủ cột (cần cột {columnIndex + 1}).");
            continue;
        }

        var email = parts.Length > 0 ? parts[0] : "(không email)";
        var secretRaw = parts[columnIndex].Trim();
        if (string.IsNullOrEmpty(secretRaw))
        {
            Console.WriteLine($"[{i + 1}] {email} - bỏ qua: secret trống.");
            continue;
        }

        var lineCdp = ResolveCdpForLine(parts, i, cdpColumnIndex, cdpUrl, cdpRotatePorts, gpmRuntimes, gpmProfileColumnIndex);
        if (attachedCdp != lineCdp)
        {
            if (browser is not null)
                await browser.CloseAsync();

            if (!await IsCdpReachableAsync(lineCdp, millisecondsTimeout: 2000))
            {
                Console.WriteLine($"[{i + 1}] {email} - bỏ qua: không mở được CDP {lineCdp} (Chrome profile tương ứng có đang mở với đúng port không?).");
                attachedCdp = null;
                browser = null;
                continue;
            }

            try
            {
                browser = await playwright.Chromium.ConnectOverCDPAsync(lineCdp);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{i + 1}] {email} - lỗi CDP {lineCdp}: {ex.Message}");
                if (browser is not null)
                {
                    try { await browser.CloseAsync(); } catch { /* ignore */ }
                }
                attachedCdp = null;
                browser = null;
                continue;
            }

            attachedCdp = lineCdp;
            for (var ci = 0; ci < browser.Contexts.Count; ci++)
            {
                var ctx = browser.Contexts[ci];
                Console.WriteLine($"[Fill2faLive] CDP {lineCdp} context #{ci}: {OpenTabCount(ctx)} tab (total {ctx.Pages.Count})");
            }

            var context = PickBrowserContext(browser) ?? await browser.NewContextAsync();
            // Không mở 2fa.live nữa. Nếu cần, vẫn có thể mở Gmail để người dùng dễ thao tác.
            var gmailTab = await OpenUrlInNewTabAsync(context, GmailInboxUrl, "mail.google.com");
            Console.WriteLine($"[Fill2faLive] Tab Gmail: {await gmailTab.EvaluateAsync<string>("() => location.href")}");
            await gmailTab.BringToFrontAsync();
        }

        var secret = Regex.Replace(secretRaw, @"\s+", "").ToUpperInvariant();
        try
        {
            var code = GenerateTotp(secret);
            Console.WriteLine($"[{i + 1}] {email} => {code}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{i + 1}] {email} - lỗi tạo TOTP: {ex.Message}");
        }
    }
}
finally
{
    if (browser is not null)
        await browser.CloseAsync();
}

Console.WriteLine("\nXong.");
return 0;

/// <summary>
/// GPM / Chrome CDP often exposes multiple contexts; the first may be empty.
/// Prefer the context that already has tabs (real profile session).
/// </summary>
static int OpenTabCount(IBrowserContext c) => c.Pages.Count(p => !p.IsClosed);

static IBrowserContext? PickBrowserContext(IBrowser browser)
{
    if (browser.Contexts.Count == 0)
        return null;
    var withPages = browser.Contexts.Where(c => OpenTabCount(c) > 0).ToList();
    if (withPages.Count > 0)
        return withPages.OrderByDescending(OpenTabCount).First();
    return browser.Contexts[0];
}

/// <summary>
/// CDP theo dong: cot CDP -&gt; CdpPorts xoay -&gt; cot ten profile GPM (WMI) -&gt; danh sach Chrome GPM (WMI, theo thoi gian tao process) -&gt; globalCdp.
/// </summary>
static string ResolveCdpForLine(
    string[] parts,
    int lineIndex0Based,
    int? cdpColumnIndex0Based,
    string globalCdp,
    int[]? rotatePorts,
    IReadOnlyList<GpmChromeRuntime> gpmRuntimes,
    int? gpmProfileColumn0Based)
{
    if (cdpColumnIndex0Based is int col && col < parts.Length)
    {
        var raw = parts[col].Trim();
        if (!string.IsNullOrEmpty(raw))
        {
            if (raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return raw;
            if (Regex.IsMatch(raw, @"^\d{2,5}$") && int.TryParse(raw, out var pcol) && pcol is >= 1 and <= 65535)
                return $"http://127.0.0.1:{pcol}";
        }
    }

    if (rotatePorts is { Length: > 0 })
        return $"http://127.0.0.1:{rotatePorts[lineIndex0Based % rotatePorts.Length]}";

    if (gpmProfileColumn0Based is int gcol && gcol < parts.Length && gpmRuntimes.Count > 0)
    {
        var name = parts[gcol].Trim();
        if (!string.IsNullOrEmpty(name))
        {
            foreach (var g in gpmRuntimes)
            {
                if (g.ProfileKey.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return g.CdpHttpUrl;
            }
        }
    }

    if (gpmRuntimes.Count > 0)
        return gpmRuntimes[lineIndex0Based % gpmRuntimes.Count].CdpHttpUrl;

    return globalCdp;
}

static int[]? ParseCdpPortsList(string s)
{
    var ports = s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Select(seg => int.TryParse(seg, out var n) && n is >= 1 and <= 65535 ? n : (int?)null)
        .Where(n => n.HasValue)
        .Select(n => n!.Value)
        .ToArray();
    return ports.Length == 0 ? null : ports;
}

static int[]? ReadCdpPortsFile(string root)
{
    var path = Path.Combine(root, "Data", "CdpPorts.txt");
    if (!File.Exists(path))
        return null;
    foreach (var line in File.ReadAllLines(path))
    {
        var t = line.Trim();
        if (t.Length == 0 || t.StartsWith('#'))
            continue;
        var arr = ParseCdpPortsList(t);
        return arr is { Length: > 0 } ? arr : null;
    }
    return null;
}

/// <summary>
/// Mở URL trong tab mới gắn session GPM: ưu tiên window.open từ tab đang có
/// (NewPageAsync qua CDP đôi khi mở tab ẩn / cửa sổ khác hoặc không lộ ra).
/// </summary>
static async Task<IPage> OpenUrlInNewTabAsync(IBrowserContext context, string url, string urlMustContain)
{
    var anchor = context.Pages.FirstOrDefault(p => !p.IsClosed);
    if (anchor != null)
    {
        try
        {
            var popup = await context.RunAndWaitForPageAsync(
                async () =>
                {
                    await anchor.EvaluateAsync(
                        "(u) => { window.open(u, '_blank', 'noopener,noreferrer'); }",
                        url);
                },
                new BrowserContextRunAndWaitForPageOptions { Timeout = 15000 });
            await popup.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            if (!popup.Url.Contains(urlMustContain, StringComparison.OrdinalIgnoreCase))
                await popup.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            return popup;
        }
        catch (TimeoutException)
        {
            Console.WriteLine("[Fill2faLive] window.open timeout (popup chặn?) - thử NewPage.");
        }
    }

    var page = await context.NewPageAsync();
    await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
    return page;
}

static void ConfigureConsoleUtf8()
{
    if (!OperatingSystem.IsWindows())
        return;
    try
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.InputEncoding = new UTF8Encoding(false);
    }
    catch
    {
        /* ignore */
    }
}

static void PrintCdpTroubleshootingEnglish(string cdpUrl, Exception? ex)
{
    var refused = ex?.Message.Contains("ECONNREFUSED", StringComparison.OrdinalIgnoreCase) == true
                  || ex?.InnerException?.Message.Contains("ECONNREFUSED", StringComparison.OrdinalIgnoreCase) == true;
    Console.Error.WriteLine();
    Console.Error.WriteLine("--- CDP help (English) ---");
    Console.Error.WriteLine($"Target: {cdpUrl}");
    if (refused || ex == null)
        Console.Error.WriteLine("ECONNREFUSED / no listener: Chrome is NOT exposing remote debugging on that port.");
    Console.Error.WriteLine("1) GPM Login: each profile needs its OWN port (--remote-debugging-port=...). Fill2faLive can auto-detect from WMI if profiles are under D:\\Login_GPM (or Data\\GpmProfilesRoot.txt / GPM_PROFILES_ROOT).");
    Console.Error.WriteLine("   Or set URL in Data\\ChromeCdp.txt; many profiles: Data\\CdpPorts.txt, --cdp-ports, or --cdp-column with port per Account line.");
    Console.Error.WriteLine("2) Start that GPM profile so the Chrome window is open.");
    Console.Error.WriteLine("3) Run PlayAPP; when done, close PlayAPP only - keep GPM Chrome running for Fill2faLive.");
    Console.Error.WriteLine("4) Or run Fill2faLive.exe manually while GPM Chrome is already open with CDP enabled.");
    Console.Error.WriteLine("5) In GPM Chrome open chrome://version - if Command Line has NO remote-debugging-port, GPM is not exposing CDP.");
    Console.Error.WriteLine("--------------------------");
}

static string CdpHostFromUrl(string cdpUrl)
{
    if (Uri.TryCreate(cdpUrl, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
        return uri.Host;
    return "127.0.0.1";
}

static bool TryParseCdpScanRange(string s, out int minPort, out int maxPort)
{
    minPort = 9222;
    maxPort = 9300;
    var parts = s.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length == 1 && int.TryParse(parts[0], out var single))
    {
        if (single is < 1 or > 65535)
            return false;
        minPort = maxPort = single;
        return true;
    }
    if (parts.Length >= 2 && int.TryParse(parts[0], out var a) && int.TryParse(parts[1], out var b))
    {
        minPort = Math.Min(a, b);
        maxPort = Math.Max(a, b);
        return minPort is >= 1 and <= 65535 && maxPort is >= 1 and <= 65535 && minPort <= maxPort;
    }
    return false;
}

static async Task<string?> FindWorkingCdpUrlAsync(IPlaywright playwright, string host, int minPort, int maxPort)
{
    for (var p = minPort; p <= maxPort; p++)
    {
        var url = $"http://{host}:{p}";
        if (!await IsCdpReachableAsync(url, 400))
            continue;
        IBrowser? b = null;
        try
        {
            b = await playwright.Chromium.ConnectOverCDPAsync(url);
            await b.CloseAsync();
            return url;
        }
        catch
        {
            if (b is not null)
            {
                try { await b.CloseAsync(); }
                catch { /* ignore */ }
            }
        }
    }
    return null;
}

static async Task<bool> LooksLikeChromeDevtoolsJsonAsync(HttpClient http, string httpRoot)
{
    try
    {
        var uri = $"{httpRoot.TrimEnd('/')}/json/version";
        var body = await http.GetStringAsync(uri);
        if (body.Contains("webSocketDebuggerUrl", StringComparison.OrdinalIgnoreCase))
            return true;
        return body.Contains("Browser", StringComparison.Ordinal)
               && (body.Contains("Chrome", StringComparison.OrdinalIgnoreCase)
                   || body.Contains("Chromium", StringComparison.OrdinalIgnoreCase));
    }
    catch
    {
        return false;
    }
}

static string LoopbackProbeHost(string hostFromCdp)
{
    if (string.IsNullOrEmpty(hostFromCdp) || hostFromCdp is "localhost" or "127.0.0.1" or "::1")
        return "127.0.0.1";
    return hostFromCdp;
}

/// <summary>
/// GPM đôi khi mở debug trên cổng ngẫu nhiên; liệt kê LISTEN trên loopback rồi gọi /json/version.
/// </summary>
static async Task<string?> FindCdpAmongListeningPortsAsync(IPlaywright playwright, HttpClient http, string hostFromCdp)
{
    var probeHost = LoopbackProbeHost(hostFromCdp);
    if (!IPAddress.TryParse(probeHost, out var probeIp) || (!IPAddress.IsLoopback(probeIp) && probeHost != "127.0.0.1"))
        return null;

    var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
    var ports = new HashSet<int>();
    foreach (var ep in listeners)
    {
        if (IPAddress.IsLoopback(ep.Address))
            ports.Add(ep.Port);
    }

    var ordered = ports
        .Where(static p => p >= 1024)
        .OrderBy(static p => p is >= 9200 and <= 65535 ? 0 : 1)
        .ThenBy(static p => p)
        .Take(400)
        .ToList();

    foreach (var port in ordered)
    {
        var url = $"http://{probeHost}:{port}";
        if (!await IsCdpReachableAsync(url, 200))
            continue;
        if (!await LooksLikeChromeDevtoolsJsonAsync(http, url))
            continue;
        IBrowser? b = null;
        try
        {
            b = await playwright.Chromium.ConnectOverCDPAsync(url);
            await b.CloseAsync();
            return url;
        }
        catch
        {
            if (b is not null)
            {
                try { await b.CloseAsync(); }
                catch { /* ignore */ }
            }
        }
    }
    return null;
}

static async Task<bool> IsCdpReachableAsync(string cdpUrl, int millisecondsTimeout)
{
    try
    {
        if (!Uri.TryCreate(cdpUrl, UriKind.Absolute, out var uri))
            return false;
        var host = string.IsNullOrEmpty(uri.Host) ? "127.0.0.1" : uri.Host;
        var port = uri.Port;
        using var client = new TcpClient();
        using var cts = new CancellationTokenSource(millisecondsTimeout);
        await client.ConnectAsync(host, port, cts.Token);
        return true;
    }
    catch
    {
        return false;
    }
}

static string FindProjectRoot()
{
    var dir = AppContext.BaseDirectory;
    for (var i = 0; i < 6; i++)
    {
        var data = Path.Combine(dir, "Data", "Account.txt");
        if (File.Exists(data))
            return dir;
        var parent = Directory.GetParent(dir);
        if (parent == null)
            break;
        dir = parent.FullName;
    }
    return Directory.GetCurrentDirectory();
}

static string ResolveCdpUrl(string root)
{
    var file = Path.Combine(root, "Data", "ChromeCdp.txt");
    if (File.Exists(file))
    {
        foreach (var line in File.ReadAllLines(file))
        {
            var t = line.Trim();
            if (t.Length == 0 || t.StartsWith('#'))
                continue;
            if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return t;
        }
    }

    var env = Environment.GetEnvironmentVariable("CHROME_CDP_URL");
    if (!string.IsNullOrWhiteSpace(env))
        return env.Trim();
    return "http://127.0.0.1:9222";
}

static void PrintHelp(string accountPath, string cdpUrl, int columnIndex, int? cdpColumnIndex, bool autoFindCdp, int scanMin, int scanMax, int[]? cdpPortsArg, bool autoGpmCdp, int? onlyLine1Based)
{
    Console.WriteLine("""
        Fill2faLive - Tạo mã TOTP nội bộ theo từng dòng Account (không mở 2fa.live). Có thể mở tab Gmail (mail/u/0/) để tiện thao tác.

        --only-line N   Chi xu ly dong thu N (1-based) trong Account.txt — PlayAPP dung khi 1 Chrome / nhieu account
                        de khong nhay secret ve dong cuoi file.

        GPM (Windows): tu dong doc --remote-debugging-port tu chrome.exe (WMI) neu --user-data-dir nam duoi
          Data\GpmProfilesRoot.txt (1 dong duong dan), hoac bien GPM_PROFILES_ROOT, hoac D:\Login_GPM neu co.
          Thu tu dong Account ~ thu tu Chrome theo thoi gian khoi dong process. Tat: --no-auto-gpm-cdp.
          An dinh theo ten thu muc profile: cot trong Account + --gpm-profile-column N.

        Nhieu profile / nhieu Chrome (thu cong):
          --cdp-ports 55055,58331   Dong 1 -> cong 55055, dong 2 -> 58331, ... (lap lai neu nhieu dong hon cong)
          hoac file Data\CdpPorts.txt mot dong: 55055,58331
          hoac cot port trong Account.txt + --cdp-column N

        Mot Chrome: Data\ChromeCdp.txt + --auto-find-cdp (SauPlayAPP them san)

        Tham so: --account, --cdp, --column, --cdp-column, --cdp-ports, --auto-find-cdp, --cdp-scan-range,
                 --auto-gpm-cdp, --no-auto-gpm-cdp, --gpm-profile-column, --only-line
        """);
    var portsHint = cdpPortsArg is { Length: > 0 } ? string.Join(",", cdpPortsArg) : "(file CdpPorts.txt neu co)";
    Console.WriteLine($"account={accountPath}, cdp={cdpUrl}, column={columnIndex + 1}, cdp-column={(cdpColumnIndex is null ? "off" : (cdpColumnIndex + 1).ToString())}, cdp-ports-arg={portsHint}, auto-find={autoFindCdp}, scan={scanMin}-{scanMax}, auto-gpm={autoGpmCdp}, only-line={(onlyLine1Based is null ? "off" : onlyLine1Based.ToString())}");
}

static string GenerateTotp(string base32Secret, int digits = 6, int stepSeconds = 30)
{
    var key = Base32Decode(base32Secret);
    var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / stepSeconds;
    Span<byte> msg = stackalloc byte[8];
    for (var i = 7; i >= 0; i--)
    {
        msg[i] = (byte)(counter & 0xFF);
        counter >>= 8;
    }

    using var hmac = new HMACSHA1(key);
    var hash = hmac.ComputeHash(msg.ToArray());
    var offset = hash[^1] & 0x0F;
    var binary =
        ((hash[offset] & 0x7f) << 24) |
        ((hash[offset + 1] & 0xff) << 16) |
        ((hash[offset + 2] & 0xff) << 8) |
        (hash[offset + 3] & 0xff);

    var mod = (int)Math.Pow(10, digits);
    var otp = binary % mod;
    return otp.ToString(new string('0', digits));
}

static byte[] Base32Decode(string input)
{
    if (string.IsNullOrWhiteSpace(input))
        throw new ArgumentException("Secret rỗng.");

    var s = Regex.Replace(input.Trim().ToUpperInvariant(), @"\s+", "");
    s = s.TrimEnd('=');

    const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    var buffer = 0;
    var bitsLeft = 0;
    var output = new List<byte>(s.Length * 5 / 8);

    foreach (var ch in s)
    {
        var val = alphabet.IndexOf(ch);
        if (val < 0)
            throw new FormatException($"Secret Base32 không hợp lệ: ký tự '{ch}'.");

        buffer = (buffer << 5) | val;
        bitsLeft += 5;

        if (bitsLeft >= 8)
        {
            bitsLeft -= 8;
            output.Add((byte)((buffer >> bitsLeft) & 0xFF));
        }
    }

    return output.ToArray();
}
