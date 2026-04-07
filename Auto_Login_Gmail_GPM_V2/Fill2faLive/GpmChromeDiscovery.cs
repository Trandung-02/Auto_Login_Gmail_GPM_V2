using System.Management;
using System.Text.RegularExpressions;

internal readonly record struct GpmChromeRuntime(string ProfileKey, int Port, DateTime StartedUtc)
{
    public string CdpHttpUrl => $"http://127.0.0.1:{Port}";
}

internal static class GpmChromeDiscovery
{
    internal static bool IsPathUnderRoot(string rootFull, string pathFull)
    {
        rootFull = Path.GetFullPath(rootFull).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        pathFull = Path.GetFullPath(pathFull).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (pathFull.Equals(rootFull, StringComparison.OrdinalIgnoreCase))
            return true;
        return pathFull.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
               || pathFull.StartsWith(rootFull + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    internal static string? ResolveGpmProfilesRoot(string projectRoot)
    {
        var file = Path.Combine(projectRoot, "Data", "GpmProfilesRoot.txt");
        if (File.Exists(file))
        {
            foreach (var line in File.ReadAllLines(file))
            {
                var t = line.Trim();
                if (t.Length == 0 || t.StartsWith('#'))
                    continue;
                var p = Path.GetFullPath(t);
                if (Directory.Exists(p))
                    return p;
            }
        }

        var env = Environment.GetEnvironmentVariable("GPM_PROFILES_ROOT");
        if (!string.IsNullOrWhiteSpace(env))
        {
            var p = Path.GetFullPath(env.Trim());
            if (Directory.Exists(p))
                return p;
        }

        const string defaultWin = @"D:\Login_GPM";
        if (Directory.Exists(defaultWin))
            return Path.GetFullPath(defaultWin);
        return null;
    }

    internal static IReadOnlyList<GpmChromeRuntime> EnumerateGpmChromeRuntimes(string profilesRoot)
    {
        if (!OperatingSystem.IsWindows())
            return Array.Empty<GpmChromeRuntime>();

        var rootNorm = Path.GetFullPath(profilesRoot);
        var best = new Dictionary<string, (int Port, DateTime Started)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            const string query = "SELECT CommandLine, CreationDate FROM Win32_Process WHERE Name = 'chrome.exe'";
            using var searcher = new ManagementObjectSearcher(query);
            foreach (ManagementObject o in searcher.Get())
            {
                var cmd = o["CommandLine"] as string;
                if (string.IsNullOrEmpty(cmd) || cmd.IndexOf("remote-debugging-port", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                var portM = Regex.Match(cmd, @"--remote-debugging-port=(\d+)", RegexOptions.IgnoreCase);
                if (!portM.Success || !int.TryParse(portM.Groups[1].Value, out var port) || port is < 1 or > 65535)
                    continue;
                var m = Regex.Match(cmd, @"--user-data-dir=""([^""]+)""");
                if (!m.Success)
                    m = Regex.Match(cmd, @"--user-data-dir=([^\s""]+)");
                if (!m.Success)
                    continue;
                var userData = Path.GetFullPath(m.Groups[1].Value.Trim('"'));
                if (!IsPathUnderRoot(rootNorm, userData))
                    continue;
                var key = Path.GetFileName(userData.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(key))
                    continue;
                DateTime started;
                try
                {
                    var cds = o["CreationDate"] as string;
                    started = string.IsNullOrEmpty(cds) ? DateTime.MaxValue : ManagementDateTimeConverter.ToDateTime(cds);
                }
                catch
                {
                    started = DateTime.MaxValue;
                }

                if (!best.TryGetValue(key, out var prev) || started < prev.Started)
                    best[key] = (port, started);
            }
        }
        catch
        {
            /* WMI denied or unavailable */
        }

        return best
            .Select(kv => new GpmChromeRuntime(kv.Key, kv.Value.Port, kv.Value.Started))
            .OrderBy(x => x.StartedUtc)
            .ThenBy(x => x.ProfileKey, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
