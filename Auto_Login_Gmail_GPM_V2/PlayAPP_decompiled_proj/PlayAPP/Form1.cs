using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Playwright;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PlayAPP;

public class Form1 : Form
{
	private IPlaywright _playwright;

	private List<IBrowser> _browsers = new List<IBrowser>();

	private bool _running = false;

	private const int BrowserWidth = 400;

	private const int BrowserHeight = 600;

	/// <summary>Tối đa số Chrome chạy song song mỗi đợt (sau mỗi đợt đóng hết rồi mở lô tiếp theo).</summary>
	private const int MaxConcurrentBrowsers = 10;

	private new const int Margin = 20;

	private int BrowserCount = 1;

	private List<ProxyInfo> _proxyList = new List<ProxyInfo>();

	private List<noidung> _noidung = new List<noidung>();

	private int _runningThreads = 0;

	private int _totalLoaded = 0;

	private int added = 0;

	private readonly object _lockCount = new object();

	private static readonly Random _rand = new Random();

	private int m_Rowindex = 0;

	private static readonly HttpClient client = new HttpClient();

	private int _startRow = 0;

	private int _endRow = -1;

	private static SemaphoreSlim _clipLock = new SemaphoreSlim(1, 1);

	private static readonly object LoginSuccessLogSync = new object();

	private List<string> _profileIds = new List<string>();

	/// <summary>Nhóm GPM vừa load (mới tạo nhất), để log / thông báo.</summary>
	private string _gpmLoadedGroupSummary = "";

	private int _currentProfileIndex = -1;

	private IContainer components = null;

	private Panel sidebar;

	private Panel topbar;

	private Label lbl_status;

	private Button btn_start;

	private Button btn_stop;

	private CheckBox cb_sudungproxy;

	private Label lbl_gpm_group;

	private ComboBox cb_gpm_group;

	private string _savedGpmGroupId;

	private CheckBox cb_hide;

	private TextBox txt_username;

	private Label label1;

	private Label labelUser;

	private DataGridView dataGridView1;

	private Label label2;

	private TextBox txt_luong;

	private ContextMenuStrip contextMenuStrip1;

	private ToolStripMenuItem toolStripMenuItem1;

	private Label label3;

	private TextBox txt_so_account_log;

	private ToolStripMenuItem copySelectToolStripMenuItem;

	private ToolStripMenuItem deleteToolStripMenuItem;

	private ToolStripMenuItem deleteAllToolStripMenuItem;

	private ToolStripMenuItem xuatCookieToolStripMenuItem;

	private ToolStripMenuItem proxyToolStripMenuItem;

	private ToolStripMenuItem tieudeToolStripMenuItem;

	private ToolStripMenuItem noidungToolStripMenuItem;

	private ToolStripMenuItem sciptToolStripMenuItem;

	private CheckBox cb_changeinfo;

	private ToolStripMenuItem copy2FAToolStripMenuItem;

	private CheckBox cb_taoform;

	private ToolStripMenuItem linkMainToolStripMenuItem;

	private DataGridViewTextBoxColumn STT;

	private DataGridViewTextBoxColumn UID;

	private DataGridViewTextBoxColumn PASS;

	private DataGridViewTextBoxColumn MA2FA;

	private DataGridViewTextBoxColumn MAIL2;

	private DataGridViewTextBoxColumn STATUS;

	private DataGridViewTextBoxColumn PROXY;

	private DataGridViewTextBoxColumn LINK_DOC;

	private DataGridViewTextBoxColumn LINK_SCRIPT;

	private DataGridViewTextBoxColumn LINK_SHEET;

	private DataGridViewTextBoxColumn LINK_MAIN;

	private ToolStripMenuItem acoountToolStripMenuItem;

	private CheckBox cb_offchrome;

	public Form1()
	{
		InitializeComponent();
	}

	private async void btnStart_Click(object sender, EventArgs e)
	{
		bool Runauto = true;
		UpdateLink(dataGridView1);
		if (!Runauto || _running)
		{
			return;
		}
		if (dataGridView1.SelectedRows.Count > 0)
		{
			List<int> selected = (from DataGridViewRow r in dataGridView1.SelectedRows
				select r.Index into i
				orderby i
				select i).ToList();
			_startRow = selected.First();
			_endRow = selected.Last();
		}
		else
		{
			_startRow = 0;
			_endRow = dataGridView1.Rows.Count - 1;
		}
		if (!int.TryParse(txt_luong.Text, out var luongRaw) || luongRaw < 1)
		{
			MessageBox.Show("Vui lòng nhập số luồng hợp lệ (≥ 1)!");
			return;
		}
		int luong = Math.Min(luongRaw, MaxConcurrentBrowsers);
		int soAccountWanted = 0;
		if (int.TryParse(txt_so_account_log.Text?.Trim(), out var parsedSo) && parsedSo > 0)
		{
			soAccountWanted = parsedSo;
		}
		List<(string uid, string pass, string ma2fa, string mail2, int rowIndex)> accountQueue = BuildAccountProcessingQueue(soAccountWanted);
		if (accountQueue.Count == 0)
		{
			MessageBox.Show("Không có account nào (UID trống) trong phạm vi hàng đã chọn.");
			return;
		}
		string logGroupId = GetGpmGroupIdForLoginLog();
		HashSet<string> alreadyOkForGroup = LoadEmailsLoggedForGpmGroup(logGroupId);
		List<(string uid, string pass, string ma2fa, string mail2, int rowIndex)> pending = new List<(string, string, string, string, int)>();
		foreach (var acc in accountQueue)
		{
			if (alreadyOkForGroup.Contains((acc.uid ?? "").Trim()))
			{
				SetText(acc.rowIndex, "STATUS", "Bỏ qua — đã login (log nhóm này)");
				continue;
			}
			pending.Add(acc);
		}
		accountQueue = pending;
		if (accountQueue.Count == 0)
		{
			MessageBox.Show("Tất cả account trong hàng đợi đã được ghi thành công trước đó cho nhóm GPM hiện tại (xem Data/login_success.log).");
			return;
		}
		if (cb_sudungproxy.Checked)
		{
			LoadProxiesFromFile();
			if (_proxyList.Count == 0)
			{
				MessageBox.Show("Không có proxy nào trong file!");
				return;
			}
			foreach (var acc in accountQueue)
			{
				if (acc.rowIndex >= _proxyList.Count)
				{
					MessageBox.Show($"Thiếu proxy cho hàng {acc.rowIndex + 1}: cần ít nhất {acc.rowIndex + 1} dòng trong Data/proxy.txt (chỉ có {_proxyList.Count} dòng).");
					return;
				}
			}
			if (cb_gpm_group.Items.Count == 0 || GetSelectedGpmGroupId() == null)
			{
				MessageBox.Show("Bật Sử dụng Proxy: cần chọn nhóm GPM. Nếu danh sách trống, bật GPM Login (API 19995) rồi tích lại \"Sử dụng Proxy\".");
				return;
			}
		}
		LoadNoiDung();
		_running = true;
		Interlocked.Exchange(ref _currentProfileIndex, -1);
		try
		{
			_playwright = await Playwright.CreateAsync();
			await RunBatchedLoginAsync(accountQueue, luong);
		}
		finally
		{
			foreach (IBrowser browser in _browsers)
			{
				try
				{
					await browser.CloseAsync();
				}
				catch
				{
				}
			}
			_browsers.Clear();
			_playwright?.Dispose();
			_playwright = null;
			_running = false;
		}
	}

	private async void btnStop_Click(object sender, EventArgs e)
	{
		_running = false;
		foreach (IBrowser browser in _browsers)
		{
			await browser.CloseAsync();
		}
		_browsers.Clear();
		_playwright?.Dispose();
		_playwright = null;
	}

	private string GetSelectedGpmGroupId()
	{
		if (cb_gpm_group?.SelectedItem is GpmGroupListItem item)
		{
			return item.Id;
		}
		return null;
	}

	/// <summary>Id nhóm dùng cho log / bỏ qua: khi không dùng proxy, mọi lần chạy chung một khóa.</summary>
	private string GetGpmGroupIdForLoginLog()
	{
		if (cb_sudungproxy.Checked)
		{
			return GetSelectedGpmGroupId() ?? "";
		}
		return "_no_proxy_group_";
	}

	private static string LoginSuccessLogPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "login_success.log");

	/// <summary>
	/// Playwright <c>SetFilesAsync("header.jpg")</c> dùng đường dẫn tương đối theo <see cref="Environment.CurrentDirectory"/>,
	/// không phải thư mục exe — dễ upload nhầm ảnh cũ hoặc file trống khi chạy từ shortcut / IDE.
	/// Thứ tự: cạnh exe → Data\ → CWD.
	/// </summary>
	private static string ResolveBundledImagePath(string fileName)
	{
		if (string.IsNullOrWhiteSpace(fileName))
		{
			throw new ArgumentException("fileName rỗng", nameof(fileName));
		}
		fileName = Path.GetFileName(fileName);
		string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
		string[] candidates = new string[3]
		{
			Path.Combine(baseDir, fileName),
			Path.Combine(baseDir, "Data", fileName),
			Path.Combine(Directory.GetCurrentDirectory(), fileName)
		};
		foreach (string path in candidates)
		{
			try
			{
				if (File.Exists(path))
				{
					return Path.GetFullPath(path);
				}
			}
			catch
			{
			}
		}
		return Path.GetFullPath(Path.Combine(baseDir, fileName));
	}

	private static HashSet<string> LoadEmailsLoggedForGpmGroup(string gpmGroupId)
	{
		HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			if (string.IsNullOrEmpty(gpmGroupId) || !File.Exists(LoginSuccessLogPath))
			{
				return set;
			}
			foreach (string raw in File.ReadAllLines(LoginSuccessLogPath, Encoding.UTF8))
			{
				string line = raw.Trim();
				if (line.Length == 0 || line[0] == '#')
				{
					continue;
				}
				int tab1 = line.IndexOf('\t');
				if (tab1 < 0)
				{
					continue;
				}
				int tab2 = line.IndexOf('\t', tab1 + 1);
				if (tab2 < 0)
				{
					continue;
				}
				string email = line.Substring(0, tab1).Trim();
				string gid = line.Substring(tab1 + 1, tab2 - tab1 - 1).Trim();
				if (string.Equals(gid, gpmGroupId, StringComparison.Ordinal) && !string.IsNullOrEmpty(email))
				{
					set.Add(email);
				}
			}
		}
		catch
		{
		}
		return set;
	}

	private static void AppendLoginSuccessLine(string email, string gpmGroupId, string proxyRaw)
	{
		try
		{
			string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
			Directory.CreateDirectory(dataDir);
			string e = (email ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
			string g = (gpmGroupId ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
			string p = (proxyRaw ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
			if (string.IsNullOrEmpty(e))
			{
				return;
			}
			string line = e + "\t" + g + "\t" + p;
			lock (LoginSuccessLogSync)
			{
				if (!File.Exists(LoginSuccessLogPath))
				{
					File.WriteAllText(LoginSuccessLogPath, "# account[TAB]gpm_group_id[TAB]proxy_raw — bỏ qua UID trùng khi chạy lại cùng nhóm GPM" + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				}
				File.AppendAllText(LoginSuccessLogPath, line + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			}
		}
		catch
		{
		}
	}

	private void UpdateGpmGroupControlsVisible()
	{
		bool on = cb_sudungproxy.Checked;
		lbl_gpm_group.Visible = on;
		cb_gpm_group.Visible = on;
	}

	private async Task RefreshGpmGroupComboAsync()
	{
		try
		{
			using HttpClient client = new HttpClient();
			string groupsJson = await client.GetStringAsync("http://127.0.0.1:19995/api/v3/groups");
			JObject groupsRoot = JObject.Parse(groupsJson);
			if (groupsRoot["success"]?.Value<bool>() != true)
			{
				MessageBox.Show("GPM danh sách nhóm: " + (groupsRoot["message"]?.ToString() ?? groupsJson));
				return;
			}
			JArray groupArr = groupsRoot["data"] as JArray;
			if (groupArr == null || groupArr.Count == 0)
			{
				MessageBox.Show("GPM không có nhóm nào (API /api/v3/groups).");
				return;
			}
			cb_gpm_group.Items.Clear();
			foreach (JToken g in groupArr)
			{
				string id = g["id"]?.ToString();
				if (string.IsNullOrEmpty(id))
				{
					continue;
				}
				string name = g["name"]?.Value<string>() ?? "";
				cb_gpm_group.Items.Add(new GpmGroupListItem(id, name));
			}
			if (!string.IsNullOrEmpty(_savedGpmGroupId))
			{
				foreach (GpmGroupListItem it in cb_gpm_group.Items)
				{
					if (it.Id == _savedGpmGroupId)
					{
						cb_gpm_group.SelectedItem = it;
						break;
					}
				}
			}
			if (cb_gpm_group.SelectedIndex < 0 && cb_gpm_group.Items.Count > 0)
			{
				cb_gpm_group.SelectedIndex = 0;
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Không tải được danh sách nhóm GPM (http://127.0.0.1:19995).\n" + ex.Message);
		}
	}

	private async void cb_sudungproxy_CheckedChanged(object sender, EventArgs e)
	{
		UpdateGpmGroupControlsVisible();
		if (cb_sudungproxy.Checked)
		{
			await RefreshGpmGroupComboAsync();
		}
	}

	private async Task LoadProfiles()
	{
		HttpClient client = new HttpClient();
		string url;
		if (cb_sudungproxy.Checked)
		{
			string groupIdStr = GetSelectedGpmGroupId();
			if (string.IsNullOrEmpty(groupIdStr))
			{
				throw new Exception("Bật Sử dụng Proxy: cần chọn nhóm GPM.");
			}
			string groupName = (cb_gpm_group.SelectedItem as GpmGroupListItem)?.Name ?? "";
			_gpmLoadedGroupSummary = string.IsNullOrEmpty(groupName) ? "id=" + groupIdStr : $"{groupName} (id={groupIdStr})";
			url = "http://127.0.0.1:19995/api/v3/profiles?group_id=" + Uri.EscapeDataString(groupIdStr) + "&sort=2&per_page=500";
		}
		else
		{
			_gpmLoadedGroupSummary = "tất cả nhóm";
			url = "http://127.0.0.1:19995/api/v3/profiles?sort=2&per_page=500";
		}
		dynamic json = JsonConvert.DeserializeObject(await client.GetStringAsync(url));
		_profileIds.Clear();
		foreach (dynamic p in json.data)
		{
			_profileIds.Add((string)p.id);
		}
		if (_profileIds.Count == 0)
		{
			throw new Exception("Không có profile nào trong GPM" + (cb_sudungproxy.Checked ? " (nhóm: " + _gpmLoadedGroupSummary + ")" : "") + ".");
		}
		Console.WriteLine($"Loaded {_profileIds.Count} profiles — {_gpmLoadedGroupSummary}");
	}

	private static int GetLastFilledAccountRowIndex(DataGridView grid)
	{
		int last = -1;
		for (int r = 0; r < grid.Rows.Count; r++)
		{
			if (grid.Rows[r].IsNewRow)
			{
				break;
			}
			string uid = grid.Rows[r].Cells["UID"].Value?.ToString();
			if (!string.IsNullOrWhiteSpace(uid))
			{
				last = r;
			}
		}
		return last;
	}

	private async Task ApplyProxiesToGpmProfilesAsync(HttpClient client)
	{
		int lastAcc = GetLastFilledAccountRowIndex(dataGridView1);
		if (lastAcc < 0)
		{
			return;
		}
		int n = Math.Min(lastAcc + 1, Math.Min(_proxyList.Count, _profileIds.Count));
		for (int i = 0; i < n; i++)
		{
			string profileId = _profileIds[i];
			string raw = _proxyList[i].RawLineForGpm;
			if (string.IsNullOrWhiteSpace(raw))
			{
				continue;
			}
			string body = JsonConvert.SerializeObject(new { raw_proxy = raw.Trim() });
			using StringContent content = new StringContent(body, Encoding.UTF8, "application/json");
			using HttpResponseMessage resp = await client.PostAsync("http://127.0.0.1:19995/api/v3/profiles/update/" + profileId, content);
			string respText = await resp.Content.ReadAsStringAsync();
			if (!resp.IsSuccessStatusCode)
			{
				throw new Exception($"GPM cập nhật proxy profile #{i + 1} ({profileId}): HTTP {(int)resp.StatusCode} — {respText}");
			}
			JObject jo = JObject.Parse(respText);
			if (jo["success"]?.Value<bool>() != true)
			{
				string msg = jo["message"]?.ToString() ?? respText;
				throw new Exception($"GPM cập nhật proxy profile #{i + 1}: {msg}");
			}
		}
		Console.WriteLine($"Đã gán raw_proxy cho {n} profile GPM trong nhóm {_gpmLoadedGroupSummary} (tên A-Z; dòng 1 = profile đầu danh sách).");
	}

	private string GetNextProfileId()
	{
		int num = Interlocked.Increment(ref _currentProfileIndex);
		num %= _profileIds.Count;
		return _profileIds[num];
	}

	/// <summary>Lấy account theo thứ tự hàng từ _startRow đến _endRow; nếu maxAccounts &gt; 0 chỉ lấy tối đa N dòng có UID.</summary>
	private List<(string uid, string pass, string ma2fa, string mail2, int rowIndex)> BuildAccountProcessingQueue(int maxAccounts)
	{
		List<(string, string, string, string, int)> list = new List<(string, string, string, string, int)>();
		int lastRow = ((_endRow >= _startRow) ? _endRow : _startRow);
		for (int r = _startRow; r <= lastRow; r++)
		{
			DataGridViewRow row = dataGridView1.Rows[r];
			if (row.IsNewRow)
			{
				continue;
			}
			string uid = row.Cells["UID"].Value?.ToString();
			if (string.IsNullOrWhiteSpace(uid))
			{
				continue;
			}
			string pass = row.Cells["PASS"].Value?.ToString();
			string ma2fa = row.Cells["MA2FA"].Value?.ToString();
			string mail2 = row.Cells["MAIL2"].Value?.ToString();
			list.Add((uid, pass, ma2fa, mail2, r));
			if (maxAccounts > 0 && list.Count >= maxAccounts)
			{
				break;
			}
		}
		return list;
	}

	private async Task RunBatchedLoginAsync(List<(string uid, string pass, string ma2fa, string mail2, int rowIndex)> accountQueue, int luongPerBatch)
	{
		if (!_running || accountQueue.Count == 0)
		{
			return;
		}
		await LoadProfiles();
		HttpClient client = new HttpClient();
		if (cb_sudungproxy.Checked)
		{
			int maxRow = accountQueue.Max(a => a.rowIndex);
			if (maxRow >= _profileIds.Count)
			{
				MessageBox.Show($"Không đủ profile GPM: hàng account lớn nhất là {maxRow + 1}, cần ít nhất {maxRow + 1} profile trong nhóm, hiện có {_profileIds.Count}.\r\nNhóm đang dùng: {_gpmLoadedGroupSummary}\r\nĐặt tên profile A-Z để thứ tự khớp Account.txt (API sort=2).");
				return;
			}
			try
			{
				await ApplyProxiesToGpmProfilesAsync(client);
			}
			catch (Exception ex)
			{
				MessageBox.Show("Lỗi đồng bộ proxy lên GPM:\n" + ex.Message);
				return;
			}
		}
		for (int offset = 0; offset < accountQueue.Count && _running; offset += luongPerBatch)
		{
			int n = Math.Min(luongPerBatch, accountQueue.Count - offset);
			List<(string uid, string pass, string ma2fa, string mail2, int rowIndex)> slice = accountQueue.GetRange(offset, n);
			await LaunchBrowserBatchAsync(client, slice);
			await RunBatchSliceAsync(slice);
			foreach (IBrowser browser in _browsers)
			{
				try
				{
					await browser.CloseAsync();
				}
				catch
				{
				}
			}
			_browsers.Clear();
		}
	}

	/// <summary>GPM mặc định đôi khi mở cửa sổ ngoài vùng nhìn thấy; gắn win_pos/win_size để luôn thấy khi không bật ẩn.</summary>
	private string BuildGpmProfileStartUrl(string profileId, int slotIndexInBatch)
	{
		string path = "http://127.0.0.1:19995/api/v3/profiles/start/" + Uri.EscapeDataString(profileId);
		if (cb_hide.Checked)
		{
			return path + "?win_scale=0.15&win_pos=-2800,-2800&win_size=" + BrowserWidth + "," + BrowserHeight;
		}
		int col = slotIndexInBatch % 5;
		int row = slotIndexInBatch / 5;
		int x = 32 + col * (BrowserWidth + Margin);
		int y = 32 + row * (BrowserHeight + Margin);
		return path + "?win_scale=1&win_pos=" + x + "," + y + "&win_size=" + BrowserWidth + "," + BrowserHeight;
	}

	private async Task LaunchBrowserBatchAsync(HttpClient client, List<(string uid, string pass, string ma2fa, string mail2, int rowIndex)> slice)
	{
		for (int bi = 0; bi < slice.Count; bi++)
		{
			var item = slice[bi];
			if (!_running)
			{
				break;
			}
			string profileId = cb_sudungproxy.Checked ? _profileIds[item.rowIndex] : GetNextProfileId();
			string startUrl = BuildGpmProfileStartUrl(profileId, bi);
			dynamic json = JsonConvert.DeserializeObject(await client.GetStringAsync(startUrl));
			string debugAddress = json.data.remote_debugging_address;
			string versionJson = await client.GetStringAsync("http://" + debugAddress + "/json/version");
			Console.WriteLine("VERSION JSON:");
			Console.WriteLine(versionJson);
			dynamic version = JsonConvert.DeserializeObject(versionJson);
			string wsEndpoint = version.webSocketDebuggerUrl;
			Console.WriteLine("WS ENDPOINT: " + wsEndpoint);
			IBrowser browser = await _playwright.Chromium.ConnectOverCDPAsync(wsEndpoint);
			_browsers.Add(browser);
			Console.WriteLine("Opened profile: " + profileId);
		}
	}

	private async Task RunBatchSliceAsync(List<(string uid, string pass, string ma2fa, string mail2, int rowIndex)> slice)
	{
		if (slice.Count == 0 || _browsers.Count != slice.Count)
		{
			return;
		}
		SemaphoreSlim semaphore = new SemaphoreSlim(slice.Count);
		List<Task> tasks = new List<Task>();
		for (int i = 0; i < slice.Count; i++)
		{
			if (!_running)
			{
				break;
			}
			await semaphore.WaitAsync();
			var account = slice[i];
			IBrowser browser = _browsers[i];
			Task task = ProcessAccount(semaphore, browser, (account.uid, account.pass, account.ma2fa, account.mail2), account.rowIndex);
			tasks.Add(task);
		}
		await Task.WhenAll(tasks);
	}

	private async Task ProcessAccount(SemaphoreSlim semaphore, IBrowser browser, (string uid, string pass, string ma2fa, string mail2) account, int currentRowIndex)
	{
		Interlocked.Increment(ref _runningThreads);
		UpdateStatus();
		try
		{
			await RunOneCycle(browser, account.uid, account.pass, account.ma2fa, account.mail2, currentRowIndex);
		}
		catch (Exception)
		{
		}
		finally
		{
			Interlocked.Decrement(ref _runningThreads);
			UpdateStatus();
			semaphore.Release();
		}
	}

	private async Task RunOneCycle(IBrowser browser, string email, string password, string cookie, string filename, int rowIndex)
	{
		IBrowserContext context = null;
		try
		{
			Invoke(delegate
			{
				dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.DarkRed;
			});
			ProxyInfo proxyInfo = (cb_sudungproxy.Checked ? GetProxyForAccountRow(rowIndex) : null);
			if (cb_sudungproxy.Checked && proxyInfo != null)
			{
				SetText(msg: (proxyInfo.Username == null) ? proxyInfo.Server : $"{proxyInfo.Server}:{proxyInfo.Username}:{proxyInfo.Password}", index: rowIndex, colName: "PROXY");
			}
			context = browser.Contexts.First();
			IPage page = ((context.Pages.Count <= 0) ? (await context.NewPageAsync()) : context.Pages.First());
			IPage page2 = page;
			await context.GrantPermissionsAsync(new string[2] { "clipboard-read", "clipboard-write" });
			await context.AddInitScriptAsync("Object.defineProperty(navigator, 'webdriver', {get: () => undefined});");
			await context.AddInitScriptAsync($"\r\n                    window.__AUTO_ID = '{rowIndex + 1}';\r\n\r\n                    function forceTitle() {{\r\n                        document.title = '#' + window.__AUTO_ID;\r\n                    }}\r\n\r\n                    setInterval(forceTitle, 1000);\r\n                ");
			await context.AddCookiesAsync(new Microsoft.Playwright.Cookie[1]
			{
				new Microsoft.Playwright.Cookie
				{
					Name = "PREF",
					Value = "hl=en",
					Domain = ".google.com",
					Path = "/"
				}
			});
			await page2.GotoAsync("https://accounts.google.com/", new PageGotoOptions
			{
				WaitUntil = WaitUntilState.NetworkIdle
			});
			string content = await page2.ContentAsync();
			if (content.Contains("ERR_PROXY_CONNECTION_FAILED") || content.Contains("ERR_NAME_NOT_RESOLVED") || content.Contains("ERR_INTERNET_DISCONNECTED") || content.Contains("ERR_CONNECTION_TIMED_OUT") || content.Contains("ERR_CONNECTION_REFUSED") || content.Contains("ERR_NETWORK_CHANGED") || content.Contains("ERR_SSL_PROTOCOL_ERROR") || content.Contains("ERR_ADDRESS_UNREACHABLE") || content.Contains("ERR_TUNNEL_CONNECTION_FAILED") || content.Contains("ERR_CONNECTION_RESET") || content.Contains("ERR_BAD_SSL_CLIENT_AUTH_CERT") || content.Contains("ERR_QUIC_PROTOCOL_ERROR") || content.Contains("ERR_EMPTY_RESPONSE") || content.Contains("ERR_SSL_VERSION_OR_CIPHER_MISMATCH"))
			{
				SetText(rowIndex, "STATUS", "PROXY RỚT MẠNG");
				return;
			}
			if (await hamcheckpass(rowIndex, context, page2, email, password, cookie, filename))
			{
				SetText(rowIndex, "STATUS", "Xong");
				AppendLoginSuccessLine(email, GetGpmGroupIdForLoginLog(), proxyInfo?.RawLineForGpm ?? "");
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Console.WriteLine("Error " + email + ": " + ex2.Message);
		}
		finally
		{
			if (context != null && cb_offchrome.Checked)
			{
				await context.CloseAsync();
			}
		}
	}

	public async Task<string> Get2FAToken(string secret)
	{
		return await Task.FromResult(GenerateTotp(secret));
	}

	private static string GenerateTotp(string base32Secret, int digits = 6, int stepSeconds = 30)
	{
		byte[] key = Base32Decode(base32Secret);
		long counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / stepSeconds;
		byte[] msg = new byte[8];
		for (int i = 7; i >= 0; i--)
		{
			msg[i] = (byte)(counter & 0xFF);
			counter >>= 8;
		}
		using HMACSHA1 hmac = new HMACSHA1(key);
		byte[] hash = hmac.ComputeHash(msg);
		int offset = hash[hash.Length - 1] & 0xF;
		int binary = ((hash[offset] & 0x7F) << 24) | ((hash[offset + 1] & 0xFF) << 16) | ((hash[offset + 2] & 0xFF) << 8) | (hash[offset + 3] & 0xFF);
		int mod = (int)Math.Pow(10.0, digits);
		int otp = binary % mod;
		return otp.ToString(new string('0', digits));
	}

	private static byte[] Base32Decode(string input)
	{
		if (string.IsNullOrWhiteSpace(input))
		{
			throw new ArgumentException("Secret rỗng.");
		}
		string s = Regex.Replace(input.Trim().ToUpperInvariant(), "\\s+", "");
		s = s.TrimEnd('=');
		const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
		int buffer = 0;
		int bitsLeft = 0;
		List<byte> output = new List<byte>(s.Length * 5 / 8);
		foreach (char ch in s)
		{
			int val = alphabet.IndexOf(ch);
			if (val < 0)
			{
				throw new FormatException("Secret Base32 không hợp lệ: ký tự '" + ch + "'.");
			}
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

	public async Task<bool> hamcheckpass(int vitri, IBrowserContext context, IPage page, string email, string password, string ma2fa, string mail2)
	{
		try
		{
			string token = "";
			try
			{
				string urlpage = page.Url;
				if (urlpage.Contains("myaccount"))
				{
					SetText(vitri, "STATUS", "Đã Login");
				}
				else
				{
					SetText(vitri, "STATUS", "STEP 1: Nhập email");
					await page.WaitForSelectorAsync("input[type='email']", new PageWaitForSelectorOptions
					{
						Timeout = 15000f
					});
					await page.FillAsync("input[type='email']", email);
					SetText(vitri, "STATUS", "STEP 1: Submit email");
					await page.ClickAsync("#identifierNext");
					SetText(vitri, "STATUS", "STEP 2: Nhập password");
					await page.WaitForSelectorAsync("input[type='password']", new PageWaitForSelectorOptions
					{
						Timeout = 15000f
					});
					await page.FillAsync("input[type='password']", password);
					SetText(vitri, "STATUS", "STEP 2: Submit password");
					await page.ClickAsync("#passwordNext");
					SetText(vitri, "STATUS", "STEP 3: Lấy mã 2FA");
					token = await Get2FAToken(ma2fa);
					await page.WaitForSelectorAsync("input[name='totpPin']", new PageWaitForSelectorOptions
					{
						Timeout = 15000f
					});
					await page.FillAsync("input[name='totpPin']", token);
					SetText(vitri, "STATUS", "STEP 3: Submit 2FA");
					await page.ClickAsync("#totpNext");
					await Task.Delay(5000);
					await page.GotoAsync("https://myaccount.google.com/language");
				}
				await page.EvaluateAsync("async () => {\r\n                        const html = document.documentElement.innerHTML;\r\n\r\n                        // regex bắt cả ' và \"\r\n                        const match = html.match(/(['\"])(APv[^'\"]+)\\1/);\r\n                        const at = match ? match[2] : null;\r\n\r\n                        if (!at) {\r\n                            console.log('❌ Không tìm thấy AT');\r\n                            return 'NO_AT';\r\n                        }\r\n\r\n                        console.log('✅ AT:', at);\r\n\r\n                        const res = await fetch('/_/language_update?hl=en&soc-app=1&soc-platform=1&soc-device=1', {\r\n                            method: 'POST',\r\n                            headers: {\r\n                                'content-type': 'application/x-www-form-urlencoded'\r\n                            },\r\n                            body: 'f.req=%5B%5B%22en%22%5D%5D&at=' + encodeURIComponent(at)\r\n                        });\r\n\r\n                        const text = await res.text();\r\n                        console.log(text);\r\n                        return 'OK';\r\n                    }");
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				SetText(vitri, "STATUS", "Lỗi Login " + ex2.Message);
				return false;
			}
			SetText(vitri, "STATUS", "Đăng nhập xong — mở Fill2faLive (đúng dòng " + (vitri + 1) + ")...");
			TryLaunchFill2faLive(vitri);
			if (cb_changeinfo.Checked)
			{
				SetText(vitri, "STATUS", "STEP 4: Mở trang Personal Info");
				await page.GotoAsync("https://myaccount.google.com/personal-info");
				await Task.Delay(5000);
				try
				{
					SetText(vitri, "STATUS", "STEP 5: Click Change Avatar");
					await page.GetByLabel("Change profile photo").ClickAsync();
					SetText(vitri, "STATUS", "STEP 5: Chờ iframe avatar");
					await Task.Delay(5000);
					await page.WaitForSelectorAsync("iframe[src*='profile-picture']", new PageWaitForSelectorOptions
					{
						Timeout = 30000f
					});
					IFrameLocator frame = page.FrameLocator("iframe[src*='profile-picture']");
					SetText(vitri, "STATUS", "STEP 5: Chờ nút Upload");
					ILocator uploadBtn = frame.GetByRole(AriaRole.Button, new FrameLocatorGetByRoleOptions
					{
						Name = "Upload from Device"
					});
					await uploadBtn.WaitForAsync(new LocatorWaitForOptions
					{
						State = WaitForSelectorState.Visible,
						Timeout = 30000f
					});
					SetText(vitri, "STATUS", "STEP 5: Upload avatar");
					string avatarPath = ResolveBundledImagePath("avatar.jpg");
					if (!File.Exists(avatarPath))
					{
						throw new FileNotFoundException("Đặt avatar.jpg cạnh PlayAPP.exe hoặc trong thư mục Data.", avatarPath);
					}
					Task<IFileChooser> chooserTask = page.WaitForFileChooserAsync();
					await uploadBtn.ClickAsync();
					await (await chooserTask).SetFilesAsync(avatarPath);
					await Task.Delay(5000);
					SetText(vitri, "STATUS", "STEP 5: Click Next");
					ILocator nextBtn = frame.GetByRole(AriaRole.Button, new FrameLocatorGetByRoleOptions
					{
						Name = "Next"
					});
					await nextBtn.WaitForAsync(new LocatorWaitForOptions
					{
						State = WaitForSelectorState.Visible,
						Timeout = 30000f
					});
					await nextBtn.ClickAsync();
					SetText(vitri, "STATUS", "STEP 5: Save avatar");
					ILocator saveBtn = frame.GetByRole(AriaRole.Button, new FrameLocatorGetByRoleOptions
					{
						Name = "Save as profile picture"
					});
					await saveBtn.WaitForAsync(new LocatorWaitForOptions
					{
						State = WaitForSelectorState.Visible,
						Timeout = 30000f
					});
					await saveBtn.ClickAsync();
					await Task.Delay(7000);
				}
				catch (Exception ex)
				{
					Exception ex3 = ex;
					SetText(vitri, "STATUS", "Lỗi đổi Avatar " + ex3.Message);
				}
			}
			if (cb_taoform.Checked)
			{
				SetText(vitri, "STATUS", "[Form] Bước 1/3: Điều hướng tới trang tạo Google Form...");
				await Task.Delay(3000);
				await page.GotoAsync("https://docs.google.com/forms/u/0/create?usp=forms_home&ths=true");
				await Task.Delay(3000);
				SetText(vitri, "STATUS", "[Form] Đóng popup quyền truy cập Forms (nếu có)...");
				await DismissGoogleFormsAccessControlDialogIfPresentAsync(page);
				await Task.Delay(500);
				if (_noidung.Count == 0)
				{
					MessageBox.Show("Không có nội dung");
					return false;
				}
				noidung nd = _noidung[0];
				string url1 = "";
				string formLink = "";
				try
				{
					await DismissGoogleFormsAccessControlDialogIfPresentAsync(page, waitBeforeCheck: false);
					SetText(vitri, "STATUS", "[Form] Điền tiêu đề form (chữ thuần, không HTML)...");
					ILocator formTitle = page.Locator("div[jsname='yrriRe'][contenteditable='true']").Or(page.Locator("div[role='textbox'][aria-label='Form title'][contenteditable='true']")).Or(page.Locator("div[aria-label='Form title'][contenteditable='true']")).First;
					ILocator desc = page.Locator("div[aria-label='Form description']").First;
					string titlePlain = ToPlainTextForGoogleForm(nd.tieude);
					await formTitle.ClickAsync();
					await page.WaitForTimeoutAsync(250f);
					if (!await TrySetGoogleFormEditablePlainAsync(page, "title", titlePlain))
					{
						SetText(vitri, "STATUS", "[Form] Tiêu đề: fallback clipboard (text/plain)...");
						await _clipLock.WaitAsync();
						try
						{
							await page.EvaluateAsync("() => {\r\n  const el = document.querySelector('div[jsname=\"yrriRe\"][contenteditable=\"true\"]')\r\n    || document.querySelector('div[role=\"textbox\"][aria-label=\"Form title\"][contenteditable=\"true\"]')\r\n    || document.querySelector('div[aria-label=\"Form title\"][contenteditable=\"true\"]');\r\n  if (!el) return;\r\n  el.focus();\r\n  const range = document.createRange();\r\n  range.selectNodeContents(el);\r\n  range.deleteContents();\r\n  el.dispatchEvent(new InputEvent('input', { bubbles: true }));\r\n}");
							await page.Keyboard.PressAsync("Control+A");
							await page.Keyboard.PressAsync("Backspace");
							await ClipboardWritePlainAsync(page, titlePlain);
							await page.WaitForTimeoutAsync(400f);
							await formTitle.ClickAsync();
							await page.Keyboard.PressAsync("Control+V");
							await page.WaitForTimeoutAsync(500f);
						}
						finally
						{
							_clipLock.Release();
						}
					}
					await page.EvaluateAsync("() => {\r\n  const el = document.querySelector('div[jsname=\"yrriRe\"][contenteditable=\"true\"]')\r\n    || document.querySelector('div[role=\"textbox\"][aria-label=\"Form title\"][contenteditable=\"true\"]')\r\n    || document.querySelector('div[aria-label=\"Form title\"][contenteditable=\"true\"]');\r\n  if (!el) {\r\n    return;\r\n  }\r\n  const strip = /^[\\s\\r\\n]*Untitled form[\\s\\r\\n]*/i;\r\n  const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT, null);\r\n  const textNodes = [];\r\n  let n;\r\n  while ((n = walker.nextNode())) {\r\n    textNodes.push(n);\r\n  }\r\n  for (let i = 0; i < textNodes.length; i++) {\r\n    const t = textNodes[i];\r\n    if (t.textContent && /Untitled form/i.test(t.textContent)) {\r\n      t.textContent = t.textContent.replace(strip, '');\r\n    }\r\n  }\r\n  while (el.firstChild && el.firstChild.nodeType === 3 && el.firstChild.textContent.trim() === '') {\r\n    el.removeChild(el.firstChild);\r\n  }\r\n  el.dispatchEvent(new InputEvent('input', { bubbles: true }));\r\n}");
					await page.WaitForTimeoutAsync(250f);
					SetText(vitri, "STATUS", "[Form] Điền mô tả form (chữ thuần, không HTML)...");
					string descPlain = ToPlainTextForGoogleForm(nd.noidungchinh);
					await desc.ClickAsync();
					await page.WaitForTimeoutAsync(500f);
					if (!await TrySetGoogleFormEditablePlainAsync(page, "description", descPlain))
					{
						SetText(vitri, "STATUS", "[Form] Mô tả: fallback clipboard (text/plain)...");
						await _clipLock.WaitAsync();
						try
						{
							await page.Keyboard.PressAsync("Control+A");
							await page.Keyboard.PressAsync("Delete");
							await ClipboardWritePlainAsync(page, descPlain);
							await page.WaitForTimeoutAsync(600f);
							await page.Keyboard.PressAsync("Control+V");
							await page.WaitForTimeoutAsync(2000f);
						}
						finally
						{
							_clipLock.Release();
						}
					}
					else
					{
						await page.WaitForTimeoutAsync(600f);
					}
					await page.WaitForTimeoutAsync(1000f);
					SetText(vitri, "STATUS", "[Form] Xóa câu hỏi mặc định (Question 1)...");
					ILocator desc2 = page.Locator("div[aria-label='Question']").First;
					await desc2.ClickAsync();
					ILocator desc3 = page.Locator("div[aria-label='Delete question']").First;
					await desc3.ClickAsync();
					try
					{
						await page.WaitForTimeoutAsync(1000f);
						SetText(vitri, "STATUS", "[Form] Theme: mở Customize Theme...");
						await page.Locator("[aria-label='Customize Theme']").ClickAsync();
						await page.WaitForTimeoutAsync(1000f);
						SetText(vitri, "STATUS", "[Form] Theme: chọn ảnh header (Upload → Browse)...");
						string headerPath = ResolveBundledImagePath("header.jpg");
						if (!File.Exists(headerPath))
						{
							SetText(vitri, "STATUS", "[Form] Không thấy header.jpg — đặt file cạnh PlayAPP.exe hoặc Data\\");
							throw new FileNotFoundException("Đặt header.jpg (ảnh header đúng màu #509beb) cạnh PlayAPP.exe hoặc trong thư mục Data.", headerPath);
						}
						SetText(vitri, "STATUS", "[Form] Theme: upload file " + headerPath);
						await page.Locator("[aria-label='Choose image for header']").ClickAsync();
						await page.WaitForTimeoutAsync(1000f);
						await page.WaitForSelectorAsync("iframe[src*='picker']");
						IFrameLocator frame2 = page.FrameLocator("iframe[src*='picker']");
						await frame2.GetByRole(AriaRole.Tab, new FrameLocatorGetByRoleOptions
						{
							Name = "Upload"
						}).WaitForAsync(new LocatorWaitForOptions
						{
							Timeout = 15000f
						});
						await frame2.GetByRole(AriaRole.Tab, new FrameLocatorGetByRoleOptions
						{
							Name = "Upload"
						}).ClickAsync();
						await (await page.RunAndWaitForFileChooserAsync(async delegate
						{
							await frame2.GetByText("Browse").ClickAsync();
						})).SetFilesAsync(headerPath);
						await Task.Delay(3000);
						await frame2.GetByRole(AriaRole.Button, new FrameLocatorGetByRoleOptions
						{
							Name = "Done"
						}).WaitForAsync(new LocatorWaitForOptions
						{
							Timeout = 20000f
						});
						await frame2.GetByRole(AriaRole.Button, new FrameLocatorGetByRoleOptions
						{
							Name = "Done"
						}).ClickAsync();
						await Task.Delay(5000);
						SetText(vitri, "STATUS", "[Form] Theme: ảnh header đã áp dụng (Done)");
						try
						{
							SetText(vitri, "STATUS", "[Form] Theme: dialog hoặc sidebar — chọn #509beb...");
							await page.WaitForTimeoutAsync(2000f);
							bool colorApplied = false;
							LocatorFilterOptions has509 = new LocatorFilterOptions
							{
								Has = page.Locator("div.UBrD9d[data-color='#509beb']")
							};
							ILocator themeDialog = page.Locator("div[role='dialog'][aria-label='Theme']");
							try
							{
								await themeDialog.First.WaitForAsync(new LocatorWaitForOptions
								{
									State = WaitForSelectorState.Visible,
									Timeout = 18000f
								});
								ILocator colorSwatch = themeDialog.Locator("div.UBrD9d[data-color='#509beb']").First;
								await colorSwatch.WaitForAsync(new LocatorWaitForOptions
								{
									State = WaitForSelectorState.Visible,
									Timeout = 45000f
								});
								await colorSwatch.ScrollIntoViewIfNeededAsync();
								await page.WaitForTimeoutAsync(200f);
								await colorSwatch.ClickAsync(new LocatorClickOptions
								{
									Timeout = 30000f,
									Force = true
								});
								await page.WaitForTimeoutAsync(400f);
								ILocator applyInTheme = themeDialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions
								{
									Name = "Apply"
								});
								if (await applyInTheme.CountAsync() > 0)
								{
									await applyInTheme.Last.ClickAsync(new LocatorClickOptions
									{
										Timeout = 30000f,
										Force = true
									});
								}
								else
								{
									ILocator spanApply = themeDialog.Locator("span.snByac").Filter(new LocatorFilterOptions
									{
										HasTextString = "Apply"
									});
									if (await spanApply.CountAsync() > 0)
									{
										await spanApply.Last.ClickAsync(new LocatorClickOptions
										{
											Timeout = 30000f,
											Force = true
										});
									}
								}
								colorApplied = true;
							}
							catch
							{
							}
							if (!colorApplied)
							{
								ILocator[] themeSidebars = new ILocator[3]
								{
									page.Locator("div[role='complementary'][aria-roledescription='sidebar']").Filter(has509).First,
									page.Locator("div.lOsMle.kiQbk.cvymMe").Filter(has509).First,
									page.Locator("div.lOsMle.cvymMe").Filter(has509).First
								};
								foreach (ILocator themePanel in themeSidebars)
								{
									try
									{
										await themePanel.WaitForAsync(new LocatorWaitForOptions
										{
											State = WaitForSelectorState.Visible,
											Timeout = 12000f
										});
										ILocator colorItem = themePanel.Locator("div.UBrD9d[role='listitem'][data-color='#509beb'][data-label='#509beb']").First;
										if (await colorItem.CountAsync() == 0)
										{
											colorItem = themePanel.Locator("div.UBrD9d[role='listitem'][data-color='#509beb']").First;
										}
										if (await colorItem.CountAsync() == 0)
										{
											colorItem = themePanel.Locator("div.UBrD9d[data-color='#509beb'][data-label='#509beb']").First;
										}
										if (await colorItem.CountAsync() == 0)
										{
											colorItem = themePanel.Locator("div.UBrD9d[data-color='#509beb']").First;
										}
										if (await colorItem.CountAsync() == 0)
										{
											colorItem = themePanel.GetByRole(AriaRole.Listitem, new LocatorGetByRoleOptions
											{
												Name = "#509beb",
												Exact = true
											}).First;
										}
										await colorItem.WaitForAsync(new LocatorWaitForOptions
										{
											State = WaitForSelectorState.Visible,
											Timeout = 30000f
										});
										await colorItem.ScrollIntoViewIfNeededAsync();
										await page.WaitForTimeoutAsync(250f);
										await colorItem.ClickAsync(new LocatorClickOptions
										{
											Timeout = 30000f,
											Force = true
										});
										await page.WaitForTimeoutAsync(500f);
										ILocator applyInPanel = themePanel.Locator("span.snByac").Filter(new LocatorFilterOptions
										{
											HasTextString = "Apply"
										});
										if (await applyInPanel.CountAsync() > 0)
										{
											await applyInPanel.Last.ClickAsync(new LocatorClickOptions
											{
												Timeout = 30000f,
												Force = true
											});
										}
										else
										{
											ILocator applyBtnSb = themePanel.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions
											{
												Name = "Apply"
											});
											if (await applyBtnSb.CountAsync() > 0)
											{
												await applyBtnSb.Last.ClickAsync(new LocatorClickOptions
												{
													Timeout = 30000f,
													Force = true
												});
											}
										}
										colorApplied = true;
										break;
									}
									catch
									{
									}
								}
							}
							if (!colorApplied)
							{
								bool jsPick = await page.EvaluateAsync<bool>("() => {\r\n  const pick = (root) => {\r\n    if (!root) return null;\r\n    return root.querySelector('div.UBrD9d[role=\"listitem\"][data-color=\"#509beb\"][data-label=\"#509beb\"]')\r\n      || root.querySelector('div.UBrD9d[data-color=\"#509beb\"][data-label=\"#509beb\"]')\r\n      || root.querySelector('div.UBrD9d[data-color=\"#509beb\"]');\r\n  };\r\n  const dlg = document.querySelector('div[role=\"dialog\"][aria-label=\"Theme\"]');\r\n  let el = pick(dlg);\r\n  if (el) {\r\n    el.scrollIntoView({ block: 'center', inline: 'center' });\r\n    el.click();\r\n    return true;\r\n  }\r\n  const sideSels = ['div[role=\"complementary\"][aria-roledescription=\"sidebar\"]', 'div.lOsMle.kiQbk.cvymMe', 'div.lOsMle.cvymMe'];\r\n  for (let s = 0; s < sideSels.length; s++) {\r\n    const nodes = document.querySelectorAll(sideSels[s]);\r\n    for (let i = 0; i < nodes.length; i++) {\r\n      el = pick(nodes[i]);\r\n      if (el) {\r\n        el.scrollIntoView({ block: 'center', inline: 'center' });\r\n        el.click();\r\n        return true;\r\n      }\r\n    }\r\n  }\r\n  el = document.querySelector('div.UBrD9d[data-color=\"#509beb\"]');\r\n  if (!el) return false;\r\n  el.scrollIntoView({ block: 'center', inline: 'center' });\r\n  el.click();\r\n  return true;\r\n}");
								if (jsPick)
								{
									try
									{
										if (await themeDialog.CountAsync() > 0)
										{
											bool vis = false;
											try
											{
												vis = await themeDialog.First.IsVisibleAsync();
											}
											catch
											{
											}
											if (vis)
											{
												ILocator dlgApply = themeDialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions
												{
													Name = "Apply"
												});
												if (await dlgApply.CountAsync() > 0)
												{
													await dlgApply.Last.ClickAsync(new LocatorClickOptions
													{
														Timeout = 30000f,
														Force = true
													});
												}
												else
												{
													ILocator sp = themeDialog.Locator("span.snByac").Filter(new LocatorFilterOptions
													{
														HasTextString = "Apply"
													});
													if (await sp.CountAsync() > 0)
													{
														await sp.Last.ClickAsync(new LocatorClickOptions
														{
															Timeout = 30000f,
															Force = true
														});
													}
												}
											}
										}
										colorApplied = true;
									}
									catch
									{
										colorApplied = true;
									}
								}
							}
							if (!colorApplied)
							{
								throw new Exception("Không tìm thấy ô màu #509beb (dialog Theme hoặc sidebar lOsMle/kiQbk).");
							}
							await page.WaitForTimeoutAsync(1000f);
							SetText(vitri, "STATUS", "[Form] Theme: đã Apply màu / hoàn tất tùy chỉnh");
						}
						catch (Exception exTheme)
						{
							SetText(vitri, "STATUS", "[Form] Lỗi theme (màu sau header): " + exTheme.Message);
						}
					}
					catch (Exception ex)
					{
						Exception ex5 = ex;
						SetText(vitri, "STATUS", "[Form] Lỗi ảnh header / picker: " + ex5.Message);
					}
					SetText(vitri, "STATUS", "[Form] Publish: mở hộp thoại Publish form...");
					await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions
					{
						Name = "Publish"
					}).First.ClickAsync();
					ILocator dialog = page.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions
					{
						Name = "Publish form"
					});
					await dialog.WaitForAsync(new LocatorWaitForOptions
					{
						Timeout = 10000f
					});
					await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions
					{
						Name = "Publish"
					}).ClickAsync();
					SetText(vitri, "STATUS", "[Form] Publish: xác nhận trong dialog → copy link responder");
					await page.GetByLabel("Click to copy responder link").ClickAsync();
					await page.WaitForTimeoutAsync(500f);
					formLink = await page.EvaluateAsync<string>("() => navigator.clipboard.readText()");
					url1 = page.Url;
					SetText(vitri, "LINK_DOC", url1);
					SetText(vitri, "STATUS", "[Form] Xong: đã lưu LINK_DOC + link phản hồi (clipboard)");
				}
				catch (Exception ex6)
				{
					SetText(vitri, "STATUS", "[Form] Lỗi tạo / chỉnh / publish Form: " + ex6.Message);
				}
				try
				{
					SetText(vitri, "STATUS", "[Sheet] Bước 2/3: Mở Google Sheets (tạo file mới)...");
					await Task.Delay(5000);
					await page.GotoAsync("https://docs.google.com/spreadsheets/u/0/create?usp=sheets_home&ths=true", new PageGotoOptions
					{
						WaitUntil = WaitUntilState.DOMContentLoaded,
						Timeout = 120000f
					});
					await Task.Delay(4500);
					SetText(vitri, "STATUS", "[Sheet] Chờ tab sheet (docs-sheet-tab-name)...");
					await page.WaitForSelectorAsync(".docs-sheet-tab-name", new PageWaitForSelectorOptions
					{
						Timeout = 120000f
					});
					SetText(vitri, "STATUS", "[Sheet] Đổi tên tab thành Sheet1 → Enter...");
					await page.DblClickAsync(".docs-sheet-tab-name");
					string newSheetName = "Sheet1";
					await page.Keyboard.TypeAsync(newSheetName);
					await page.Keyboard.PressAsync("Enter");
					string url3 = page.Url;
					SetText(vitri, "LINK_SHEET", url3);
					SetText(vitri, "STATUS", "[Sheet] Đã lưu LINK_SHEET → mở script.new (tab mới)...");
					IPage scriptPage = await context.NewPageAsync();
					await scriptPage.GotoAsync("https://script.new", new PageGotoOptions
					{
						WaitUntil = WaitUntilState.DOMContentLoaded,
						Timeout = 80000f
					});
					await Task.Delay(5000);
					try
					{
						await scriptPage.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
						{
							Timeout = 90000f
						});
					}
					catch
					{
					}
					await Task.Delay(2000);
					await scriptPage.SetViewportSizeAsync(1920, 1080);
					SetText(vitri, "STATUS", "[Script] Chờ editor Monaco (.view-lines)...");
					await scriptPage.WaitForSelectorAsync(".view-lines", new PageWaitForSelectorOptions
					{
						Timeout = 180000f
					});
					SetText(vitri, "STATUS", "[Script] Xóa code mặc định, chuẩn bị dán codescript...");
					await scriptPage.ClickAsync(".view-lines");
					await scriptPage.Keyboard.PressAsync("Control+A");
					await scriptPage.Keyboard.PressAsync("Delete");
					string formUrlNorm = formLink ?? "";
					if (!string.IsNullOrEmpty(formUrlNorm))
					{
						int indexVf = formUrlNorm.IndexOf("viewform");
						if (indexVf != -1)
						{
							formUrlNorm = formUrlNorm.Substring(0, indexVf + "viewform".Length);
						}
					}
					string sheetUrlNorm = url3 ?? "";
					string newCode = nd.codescript ?? "";
					newCode = newCode.Replace("[LINK_FORM]", formUrlNorm);
					newCode = newCode.Replace("[LINK_SHEET]", sheetUrlNorm);
					if (!string.IsNullOrEmpty(formUrlNorm))
					{
						newCode = newCode.Replace("123456", formUrlNorm);
					}
					SetText(vitri, "STATUS", "[Script] Dán mã: thay [LINK_FORM] / [LINK_SHEET] / 123456...");
					await scriptPage.EvaluateAsync("(code) => {\r\n                        let editor = window.monaco?.editor?.getModels?.()[0];\r\n                        if (editor) {\r\n                            editor.setValue(code); // \ud83d\udd25 cách chuẩn nhất\r\n                        }\r\n                         }", newCode);
					await Task.Delay(5000);
					IElementHandle closeBtn = await scriptPage.QuerySelectorAsync("button[aria-label='close']");
					if (closeBtn != null)
					{
						try
						{
							await closeBtn.ClickAsync();
							await Task.Delay(500);
						}
						catch
						{
						}
					}
					SetText(vitri, "STATUS", "[Script] Bước 3/3: Services → Add a service → Drive API v2...");
					await scriptPage.WaitForSelectorAsync("[aria-label='Add a service']", new PageWaitForSelectorOptions
					{
						Timeout = 60000f
					});
					await scriptPage.ClickAsync("[aria-label='Add a service']");
					await Task.Delay(2500);
					await scriptPage.WaitForSelectorAsync("text=Add a service", new PageWaitForSelectorOptions
					{
						Timeout = 45000f
					});
					await scriptPage.Locator("text=Drive API").First.ClickAsync();
					await scriptPage.ClickAsync("text=Version");
					await scriptPage.ClickAsync("text=v2");
					await scriptPage.GetByRole(AriaRole.Button, new PageGetByRoleOptions
					{
						Name = "Add"
					}).ClickAsync();
					await Task.Delay(2500);
					string url4 = scriptPage.Url;
					SetText(vitri, "LINK_SCRIPT", url4);
					SetText(vitri, "STATUS", "[Script] Đã lưu LINK_SCRIPT → Run function (lần 1)...");
					await scriptPage.Locator("button[aria-label='Run the selected function']").ClickAsync();
					await page.WaitForTimeoutAsync(10000f);
					SetText(vitri, "STATUS", "[Script] Run function (lần 2)...");
					await scriptPage.Locator("button[aria-label='Run the selected function']").ClickAsync();
					await page.WaitForTimeoutAsync(10000f);
					try
					{
						SetText(vitri, "STATUS", "[Script] OAuth: chờ Review permissions → mở tab xác thực...");
						await scriptPage.WaitForSelectorAsync("text=Review permissions", new PageWaitForSelectorOptions
						{
							Timeout = 45000f
						});
						await scriptPage.GetByRole(AriaRole.Button, new PageGetByRoleOptions
						{
							Name = "Review permissions"
						}).ClickAsync();
						IPage authPage = await context.WaitForPageAsync();
						await authPage.WaitForLoadStateAsync();
						try
						{
							await authPage.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
							{
								Timeout = 60000f
							});
						}
						catch
						{
						}
						await Task.Delay(2500);
						string oldToken = token;
						string newToken = oldToken;
						while (newToken == oldToken)
						{
							await Task.Delay(1000);
							newToken = await Get2FAToken(ma2fa);
						}
						token = newToken;
						await authPage.WaitForSelectorAsync("input[name='totpPin']", new PageWaitForSelectorOptions
						{
							Timeout = 15000f
						});
						await authPage.FillAsync("input[name='totpPin']", token);
						await authPage.ClickAsync("#totpNext");
						await page.WaitForTimeoutAsync(9000f);
						ILocator advanced = authPage.Locator("a:has-text('Advanced')");
						if (await advanced.CountAsync() > 0)
						{
							await advanced.ClickAsync();
							await page.WaitForTimeoutAsync(2000f);
						}
						ILocator gotouniti = authPage.Locator("a:has-text('Go to Untitled project (unsafe)')");
						if (await gotouniti.CountAsync() > 0)
						{
							await gotouniti.ClickAsync();
							await page.WaitForTimeoutAsync(3500f);
						}
						else
						{
							await page.WaitForTimeoutAsync(2000f);
						}
						await authPage.GetByRole(AriaRole.Button, new PageGetByRoleOptions
						{
							Name = "Continue"
						}).ClickAsync();
						await page.WaitForTimeoutAsync(4500f);
						try
						{
							await authPage.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
							{
								Timeout = 30000f
							});
						}
						catch
						{
						}
						await Task.Delay(2000);
						ILocator consentCheckbox = authPage.Locator("input[type='checkbox'][jsname='YPqjbf']").Or(authPage.GetByRole(AriaRole.Checkbox)).First;
						await consentCheckbox.WaitForAsync(new LocatorWaitForOptions
						{
							State = WaitForSelectorState.Visible,
							Timeout = 45000f
						});
						await Task.Delay(1500);
						await consentCheckbox.CheckAsync(new LocatorCheckOptions
						{
							Timeout = 20000f
						});
						await page.WaitForTimeoutAsync(4500f);
						await authPage.GetByRole(AriaRole.Button, new PageGetByRoleOptions
						{
							Name = "Continue"
						}).ClickAsync();
						await page.WaitForTimeoutAsync(3000f);
						SetText(vitri, "STATUS", "[Script] OAuth: cấp quyền / Continue / checkbox → xong");
					}
					catch (Exception ex7)
					{
						SetText(vitri, "STATUS", "[Script] Lỗi OAuth / cấp quyền Run: " + ex7.Message);
					}
				}
				catch (Exception ex8)
				{
					SetText(vitri, "STATUS", "[Sheet/Script] Lỗi (Sheets, editor, API hoặc Run): " + ex8.Message);
				}
				if (!string.IsNullOrWhiteSpace(url1) && Uri.TryCreate(url1, UriKind.Absolute, out Uri _))
				{
					try
					{
						SetText(vitri, "STATUS", "[Form] Mở lại LINK_DOC trong tab mới (DOMContentLoaded)...");
						await (await context.NewPageAsync()).GotoAsync(url1, new PageGotoOptions
						{
							WaitUntil = WaitUntilState.DOMContentLoaded,
							Timeout = 60000f
						});
					}
					catch (Exception exNav)
					{
						SetText(vitri, "STATUS", "[Form] Không mở lại LINK_DOC: " + exNav.Message);
					}
				}
				SetText(vitri, "STATUS", "[Form+Sheet+Script] Hoàn tất pipeline — sẵn sàng DONE");
			}
			else
			{
				try
				{
					SetText(vitri, "STATUS", "[Link] Mở tab đã lưu: LINK_DOC / LINK_SCRIPT / LINK_SHEET (nếu có)...");
					string url5 = dataGridView1?.Rows[vitri]?.Cells["LINK_DOC"]?.Value?.ToString();
					string url6 = dataGridView1?.Rows[vitri]?.Cells["LINK_SCRIPT"]?.Value?.ToString();
					string url7 = dataGridView1?.Rows[vitri]?.Cells["LINK_SHEET"]?.Value?.ToString();
					if (!string.IsNullOrEmpty(url5))
					{
						SetText(vitri, "STATUS", "[Link] Đang mở LINK_DOC...");
						await (await context.NewPageAsync()).GotoAsync(url5);
					}
					if (!string.IsNullOrEmpty(url6))
					{
						SetText(vitri, "STATUS", "[Link] Đang mở LINK_SCRIPT...");
						await (await context.NewPageAsync()).GotoAsync(url6);
					}
					if (!string.IsNullOrEmpty(url7))
					{
						SetText(vitri, "STATUS", "[Link] Đang mở LINK_SHEET...");
						await (await context.NewPageAsync()).GotoAsync(url7);
					}
					SetText(vitri, "STATUS", "[Link] Đã mở xong các URL có trong lưới");
				}
				catch (Exception ex)
				{
					Exception ex9 = ex;
					SetText(vitri, "STATUS", "[Link] Lỗi khi mở URL đã lưu: " + ex9.Message);
				}
			}
			SetText(vitri, "STATUS", "[Xong] DONE");
			return true;
		}
		catch (Exception ex)
		{
			Exception ex10 = ex;
			SetText(vitri, "STATUS", "ERROR " + ex10.Message);
			Console.WriteLine("Login error tổng: " + ex10.Message);
			return false;
		}
	}

	public void UpdateLink(DataGridView dataGridView_0)
	{
		try
		{
			if (!Directory.Exists("Data"))
			{
				Directory.CreateDirectory("Data");
			}
			string[] array = File.ReadAllLines("Data/linkmain.txt");
			if (array.Length != 0 && dataGridView_0.Rows.Count != 0)
			{
				for (int i = 0; i < dataGridView_0.Rows.Count; i++)
				{
					int num = i % array.Length;
					dataGridView_0.Rows[i].Cells["LINK_MAIN"].Value = array[num];
				}
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Lỗi khi update link: " + ex.Message);
		}
	}

	public void SetAccount(DataGridView dataGridView_0)
	{
		try
		{
			dataGridView_0.AllowUserToAddRows = false;
			if (!Directory.Exists("Data"))
			{
				Directory.CreateDirectory("Data");
			}
			using FileStream stream = new FileStream("Data/Account.txt", FileMode.Open, FileAccess.Read);
			using StreamReader streamReader = new StreamReader(stream);
			int num = 1;
			string text;
			while ((text = streamReader.ReadLine()) != null)
			{
				string[] array = text.Split('|', '\t', ' ');
				if (array.Length >= 3)
				{
					string text2 = array[0];
					string text3 = array[1];
					string text4 = array[2];
					string text5 = array[3];
					string text6 = array[4];
					string text7 = array[5];
					string text8 = array[6];
					dataGridView_0.Rows.Add(num++, text2, text3, text4, text5, "", "", text6, text7, text8);
				}
			}
		}
		catch
		{
		}
	}

	public void SetText(int index, string colName, string msg, int maxLines = 10)
	{
		try
		{
			if (index < 0 || index >= dataGridView1.Rows.Count)
			{
				return;
			}
			dataGridView1.Invoke(delegate
			{
				DataGridViewCell dataGridViewCell = dataGridView1.Rows[index].Cells[colName];
				List<string> list = new List<string>();
				if (!string.IsNullOrEmpty(dataGridViewCell.ToolTipText))
				{
					list.AddRange(dataGridViewCell.ToolTipText.Split(new string[1] { Environment.NewLine }, StringSplitOptions.None));
				}
				list.Add(msg);
				if (list.Count > maxLines)
				{
					list = list.Skip(list.Count - maxLines).ToList();
				}
				dataGridViewCell.ToolTipText = string.Join(Environment.NewLine, list);
				dataGridViewCell.Value = msg;
			});
		}
		catch
		{
		}
	}

	private List<(string uid, string pass, string ma2fa, string mail2)> GetAccountsFromGrid()
	{
		List<(string, string, string, string)> list = new List<(string, string, string, string)>();
		for (int i = _startRow; i <= _endRow; i++)
		{
			DataGridViewRow dataGridViewRow = dataGridView1.Rows[i];
			if (!dataGridViewRow.IsNewRow)
			{
				string item = dataGridViewRow.Cells["UID"].Value?.ToString();
				string item2 = dataGridViewRow.Cells["PASS"].Value?.ToString();
				string item3 = dataGridViewRow.Cells["MA2FA"].Value?.ToString();
				string item4 = dataGridViewRow.Cells["MAIL2"].Value?.ToString();
				list.Add((item, item2, item3, item4));
			}
		}
		return list;
	}

	private string GetRandomUserAgent()
	{
		string[] array = new string[3] { "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/119.0.0.0 Safari/537.36", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/118.0.0.0 Safari/537.36" };
		return array[new Random().Next(array.Length)];
	}

	private void LoadProxiesFromFile()
	{
		_proxyList.Clear();
		string baseDir = AppDomain.CurrentDomain.BaseDirectory;
		string fullPath = Path.GetFullPath(Path.Combine(baseDir, "Data", "proxy.txt"));
		if (!File.Exists(fullPath))
		{
			string fallback = Path.GetFullPath(Path.Combine(baseDir, "data", "proxy.txt"));
			if (File.Exists(fallback))
			{
				fullPath = fallback;
			}
			else
			{
				MessageBox.Show("Không tìm thấy file:\n" + fullPath);
				return;
			}
		}
		string[] array = File.ReadAllLines(fullPath);
		string[] array2 = array;
		foreach (string text in array2)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				continue;
			}
			string trimmed = text.Trim();
			string[] array3 = trimmed.Split(':');
			if (array3.Length >= 2)
			{
				string text2 = array3[0];
				string text3 = array3[1];
				string username = null;
				string password = null;
				if (array3.Length >= 4)
				{
					username = array3[2];
					password = array3[3];
				}
				_proxyList.Add(new ProxyInfo
				{
					Server = "http://" + text2 + ":" + text3,
					Username = username,
					Password = password,
					RawLineForGpm = trimmed
				});
			}
		}
	}

	private static bool LooksLikeHtml(string s)
	{
		if (string.IsNullOrWhiteSpace(s))
		{
			return false;
		}
		return Regex.IsMatch(s, @"</?[a-z][a-z0-9]*\b", RegexOptions.IgnoreCase);
	}

	private static string HtmlToPlain(string html)
	{
		if (string.IsNullOrEmpty(html))
		{
			return "";
		}
		string t = Regex.Replace(html, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
		t = Regex.Replace(t, "</p\\s*>", "\n", RegexOptions.IgnoreCase);
		t = Regex.Replace(t, "</div\\s*>", "\n", RegexOptions.IgnoreCase);
		t = Regex.Replace(t, "(?i)</li\\s*>", "\n");
		t = Regex.Replace(t, "(?i)<li\\b[^>]*>", "• ");
		t = Regex.Replace(t, "(?i)<a\\b[^>]*>", "");
		t = Regex.Replace(t, "(?i)</a\\s*>", "");
		t = Regex.Replace(t, "<[^>]+>", "");
		return WebUtility.HtmlDecode(t).Replace("\r\n", "\n").Trim();
	}

	/// <summary>Giảm xuống dòng thừa trước khi dán vào Google Forms (viewform hay bị “cách dòng” xấu).</summary>
	private static string NormalizeNewlinesForFormPaste(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		s = s.Replace("\r\n", "\n").Replace('\r', '\n');
		s = Regex.Replace(s, "\n{3,}", "\n\n");
		return s.Trim();
	}

	/// <summary>Google Forms không nhận rich text từ ngoài — luôn dùng chữ thuần; HTML trong file được gỡ tag.</summary>
	private static string ToPlainTextForGoogleForm(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "";
		}
		string t = LooksLikeHtml(s) ? HtmlToPlain(s) : s;
		return NormalizeNewlinesForFormPaste(t);
	}

	private static async Task<bool> TrySetGoogleFormEditablePlainAsync(IPage page, string formField, string plain)
	{
		plain ??= "";
		try
		{
			const string script = @"({ plain, formField }) => {
  let el = null;
  if (formField === 'title') {
    el = document.querySelector('div[jsname=""yrriRe""][contenteditable=""true""]')
      || document.querySelector('div[role=""textbox""][aria-label=""Form title""][contenteditable=""true""]')
      || document.querySelector('div[aria-label=""Form title""][contenteditable=""true""]');
  } else {
    const wrap = document.querySelector('div[aria-label=""Form description""]');
    if (!wrap) return 'missing';
    if (wrap.matches('[contenteditable=""true""]')) el = wrap;
    else el = wrap.querySelector('[contenteditable=""true""]') || wrap;
  }
  if (!el) return 'missing';
  const ce = el.isContentEditable || el.getAttribute('contenteditable') === 'true';
  if (!ce) return 'not_editable';
  el.focus();
  el.innerText = plain;
  const opts = { bubbles: true, cancelable: true, inputType: 'insertFromPaste', data: null };
  el.dispatchEvent(new InputEvent('beforeinput', opts));
  el.dispatchEvent(new InputEvent('input', opts));
  el.dispatchEvent(new Event('input', { bubbles: true }));
  return 'ok';
}";
			string status = await page.EvaluateAsync<string>(script, new
			{
				plain,
				formField
			});
			return status == "ok";
		}
		catch
		{
			return false;
		}
	}

	private static async Task ClipboardWritePlainAsync(IPage page, string plain)
	{
		if (string.IsNullOrEmpty(plain))
		{
			await page.EvaluateAsync("() => navigator.clipboard.writeText('')");
			return;
		}
		await page.EvaluateAsync("(text) => navigator.clipboard.writeText(text)", plain);
	}

	private static async Task<bool> TryClickGoogleFormsGotItInAllFramesDeepAsync(IPage page, string label)
	{
		const string script = @"(label) => {
		const want = (label || '').trim();
		function norm(t) { return (t || '').replace(/\s+/g, ' ').trim(); }
		function textMatches(btn) {
			if (!btn) return false;
			const t = norm(btn.textContent);
			return t === want || t.indexOf(want) >= 0;
		}
		function fireClick(btn) {
			if (!btn) return;
			const o = { bubbles: true, cancelable: true, view: window };
			try { btn.dispatchEvent(new PointerEvent('pointerdown', o)); } catch (e0) {}
			btn.dispatchEvent(new MouseEvent('mousedown', o));
			try { btn.dispatchEvent(new PointerEvent('pointerup', o)); } catch (e1) {}
			btn.dispatchEvent(new MouseEvent('mouseup', o));
			btn.dispatchEvent(new MouseEvent('click', o));
			if (typeof btn.click === 'function') btn.click();
		}
		function querySelectorAllDeep(root, sel) {
			const out = [];
			function visit(node) {
				if (!node) return;
				try {
					if (node.querySelectorAll) {
						node.querySelectorAll(sel).forEach(function(el) { out.push(el); });
					}
				} catch (e) {}
				if (node.shadowRoot) visit(node.shadowRoot);
				const ch = node.children;
				if (ch) {
					for (let i = 0; i < ch.length; i++) visit(ch[i]);
				}
			}
			visit(root);
			return out;
		}
		const root = document.documentElement;
		if (!root) return false;
		const dialogs = querySelectorAllDeep(root, '[role=""alertdialog""]');
		const targetBtns = [];
		for (let d = 0; d < dialogs.length; d++) {
			const inner = querySelectorAllDeep(dialogs[d], 'div[role=""button""][jsname=""LgbsSe""]');
			for (let i = 0; i < inner.length; i++) {
				if (textMatches(inner[i])) targetBtns.push(inner[i]);
			}
		}
		if (!targetBtns.length) {
			const all = querySelectorAllDeep(root, 'div[role=""button""][jsname=""LgbsSe""]');
			for (let j = 0; j < all.length; j++) {
				if (textMatches(all[j])) targetBtns.push(all[j]);
			}
		}
		if (!targetBtns.length) return false;
		let chosen = null;
		for (let k = 0; k < targetBtns.length; k++) {
			if (targetBtns[k].classList && targetBtns[k].classList.contains('M9Bg4d')) {
				chosen = targetBtns[k];
				break;
			}
		}
		if (!chosen) chosen = targetBtns[targetBtns.length - 1];
		fireClick(chosen);
		return true;
	}";
		foreach (IFrame frame in page.Frames)
		{
			try
			{
				if (await frame.EvaluateAsync<bool>(script, label))
				{
					return true;
				}
			}
			catch
			{
			}
		}
		return false;
	}

	private static async Task<bool> TryDismissGoogleFormsPdYghbOverlayMainPageAsync(IPage page)
	{
		LocatorClickOptions opt = new LocatorClickOptions
		{
			Timeout = 15000f,
			Force = true
		};
		try
		{
			ILocator dlg = page.Locator("div[role='alertdialog'][data-position='pdYghb']");
			if (await dlg.CountAsync() == 0)
			{
				return false;
			}
			ILocator m9 = dlg.Locator("div[role='button'][jsname='LgbsSe'].M9Bg4d");
			if (await m9.CountAsync() > 0)
			{
				await m9.Last.ClickAsync(opt);
				return true;
			}
			ILocator ebs = dlg.Locator("div[role='button'][jsname='LgbsSe'][data-id='EBS5u']");
			if (await ebs.CountAsync() > 0)
			{
				await ebs.Last.ClickAsync(opt);
				return true;
			}
			ILocator lgbs = dlg.Locator("div[role='button'][jsname='LgbsSe']");
			if (await lgbs.CountAsync() > 0)
			{
				await lgbs.Last.ClickAsync(opt);
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	private static async Task<bool> TryClickGotItInSingleFrameAsync(IFrame frame, string label)
	{
		try
		{
			ILocator dlg = frame.Locator("[role='alertdialog'][data-position='pdYghb']");
			if (await dlg.CountAsync() == 0)
			{
				dlg = frame.Locator("[role='alertdialog']");
			}
			if (await dlg.CountAsync() == 0)
			{
				return false;
			}
			ILocator m9 = dlg.Locator("div[role='button'][jsname='LgbsSe'].M9Bg4d");
			if (await m9.CountAsync() > 0)
			{
				await m9.Last.ClickAsync(new LocatorClickOptions
				{
					Timeout = 12000f,
					Force = true
				});
				return true;
			}
			ILocator buttons = dlg.Locator("div[role='button'][jsname='LgbsSe']").Filter(new LocatorFilterOptions
			{
				HasTextString = label
			});
			if (await buttons.CountAsync() > 0)
			{
				await buttons.Last.ClickAsync(new LocatorClickOptions
				{
					Timeout = 12000f,
					Force = true
				});
				return true;
			}
			ILocator spans = dlg.Locator("span.snByac", new LocatorLocatorOptions
			{
				HasTextString = label
			});
			if (await spans.CountAsync() > 0)
			{
				await spans.Last.ClickAsync(new LocatorClickOptions
				{
					Timeout = 12000f,
					Force = true
				});
				return true;
			}
			ILocator byRole = dlg.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions
			{
				Name = label
			});
			if (await byRole.CountAsync() > 0)
			{
				await byRole.Last.ClickAsync(new LocatorClickOptions
				{
					Timeout = 12000f,
					Force = true
				});
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	private static async Task<bool> PageOrAnyFrameHasAlertDialogAsync(IPage page)
	{
		try
		{
			if (await page.Locator("[role='alertdialog']").CountAsync() > 0)
			{
				return true;
			}
		}
		catch
		{
		}
		foreach (IFrame frame in page.Frames)
		{
			try
			{
				if (await frame.Locator("[role='alertdialog']").CountAsync() > 0)
				{
					return true;
				}
			}
			catch
			{
			}
		}
		return false;
	}

	private static async Task<bool> TryClickGoogleFormsGotItPlaywrightInAllFramesAsync(IPage page, string label)
	{
		if (await TryClickGotItInSingleFrameAsync(page.MainFrame, label))
		{
			return true;
		}
		foreach (IFrame frame in page.Frames)
		{
			if (frame == page.MainFrame)
			{
				continue;
			}
			if (await TryClickGotItInSingleFrameAsync(frame, label))
			{
				return true;
			}
		}
		return false;
	}

	private static async Task<bool> TryFocusGotItAndPressEnterAsync(IPage page)
	{
		if (!await PageOrAnyFrameHasAlertDialogAsync(page))
		{
			return false;
		}
		try
		{
			ILocator pd = page.Locator("div[role='alertdialog'][data-position='pdYghb']");
			if (await pd.CountAsync() > 0)
			{
				ILocator m9p = pd.Locator("div[role='button'][jsname='LgbsSe'].M9Bg4d");
				if (await m9p.CountAsync() > 0)
				{
					await m9p.Last.FocusAsync();
				}
				else
				{
					ILocator anyp = pd.Locator("div[role='button'][jsname='LgbsSe']");
					if (await anyp.CountAsync() > 0)
					{
						await anyp.Last.FocusAsync();
					}
				}
				await page.Keyboard.PressAsync("Enter");
				await page.WaitForTimeoutAsync(800f);
				if (!await PageOrAnyFrameHasAlertDialogAsync(page))
				{
					return true;
				}
			}
		}
		catch
		{
		}
		foreach (IFrame frame in page.Frames)
		{
			try
			{
				ILocator dlg = frame.Locator("[role='alertdialog']");
				if (await dlg.CountAsync() == 0)
				{
					continue;
				}
				ILocator m9 = dlg.Locator("div[role='button'][jsname='LgbsSe'].M9Bg4d");
				if (await m9.CountAsync() > 0)
				{
					await m9.Last.FocusAsync();
				}
				else
				{
					ILocator anyBtn = dlg.Locator("div[role='button'][jsname='LgbsSe']");
					if (await anyBtn.CountAsync() == 0)
					{
						continue;
					}
					await anyBtn.Last.FocusAsync();
				}
				await page.Keyboard.PressAsync("Enter");
				await page.WaitForTimeoutAsync(800f);
				return !await PageOrAnyFrameHasAlertDialogAsync(page);
			}
			catch
			{
			}
		}
		return false;
	}

	private static async Task<bool> TryDismissGoogleFormsAlertViaLocatorsAsync(ILocator dialog, IPage page, string[] labels)
	{
		foreach (string name in labels)
		{
			try
			{
				ILocator lgbs = dialog.Locator("div[role='button'][jsname='LgbsSe']").Filter(new LocatorFilterOptions
				{
					HasTextString = name
				});
				int n = await lgbs.CountAsync();
				if (n > 0)
				{
					await lgbs.Last.ClickAsync(new LocatorClickOptions
					{
						Timeout = 5000f,
						Force = true
					});
					return true;
				}
			}
			catch
			{
			}
			if (name == "Got it")
			{
				try
				{
					ILocator globalLgbs = page.Locator("div[role='button'][jsname='LgbsSe'][data-id='EBS5u']");
					if (await globalLgbs.CountAsync() > 0)
					{
						await globalLgbs.Last.ClickAsync(new LocatorClickOptions
						{
							Timeout = 5000f,
							Force = true
						});
						return true;
					}
				}
				catch
				{
				}
			}
			try
			{
				await dialog.Locator("span.snByac", new LocatorLocatorOptions
				{
					HasTextString = name
				}).First.ClickAsync(new LocatorClickOptions
				{
					Timeout = 5000f,
					Force = true
				});
				return true;
			}
			catch
			{
			}
			try
			{
				await dialog.GetByText(name, new LocatorGetByTextOptions
				{
					Exact = true
				}).First.ClickAsync(new LocatorClickOptions
				{
					Timeout = 5000f,
					Force = true
				});
				return true;
			}
			catch
			{
			}
			try
			{
				await dialog.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions
				{
					Name = name
				}).First.ClickAsync(new LocatorClickOptions
				{
					Timeout = 5000f,
					Force = true
				});
				return true;
			}
			catch
			{
			}
		}
		return false;
	}

	private static async Task DismissGoogleFormsAccessControlDialogIfPresentAsync(IPage page, bool waitBeforeCheck = true)
	{
		if (waitBeforeCheck)
		{
			await page.WaitForTimeoutAsync(600f);
		}
		string[] buttonNames = new string[4] { "Got it", "Đã hiểu", "Tôi hiểu", "OK" };
		for (int round = 0; round < 3; round++)
		{
			if (!await PageOrAnyFrameHasAlertDialogAsync(page))
			{
				return;
			}
			bool clicked = false;
			string[] array = buttonNames;
			clicked = await TryDismissGoogleFormsPdYghbOverlayMainPageAsync(page);
			if (!clicked)
			{
				foreach (string name in array)
				{
					if (await TryClickGoogleFormsGotItInAllFramesDeepAsync(page, name))
					{
						clicked = true;
						break;
					}
				}
			}
			if (!clicked)
			{
				foreach (string name2 in array)
				{
					if (await TryClickGoogleFormsGotItPlaywrightInAllFramesAsync(page, name2))
					{
						clicked = true;
						break;
					}
				}
			}
			if (!clicked)
			{
				clicked = await TryFocusGotItAndPressEnterAsync(page);
			}
			if (!clicked)
			{
				ILocator dialogs = page.Locator("[role='alertdialog']");
				if (await dialogs.CountAsync() > 0)
				{
					clicked = await TryDismissGoogleFormsAlertViaLocatorsAsync(dialogs.First, page, array);
				}
			}
			if (!clicked)
			{
				return;
			}
			await page.WaitForTimeoutAsync(900f);
		}
	}

	private void LoadNoiDung()
	{
		try
		{
			_noidung.Clear();
			string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
			string path = Path.Combine(baseDirectory, "data", "tieude.txt");
			string path2 = Path.Combine(baseDirectory, "data", "noidung.txt");
			string path3 = Path.Combine(baseDirectory, "data", "codesc.txt");
			if (!File.Exists(path) || !File.Exists(path2) || !File.Exists(path3))
			{
				MessageBox.Show("Thiếu file dữ liệu trong thư mục data");
				return;
			}
			string noidungchinh = File.ReadAllText(path2);
			string codescript = File.ReadAllText(path3);
			foreach (string raw in File.ReadAllLines(path))
			{
				string tieude = raw.Trim();
				if (tieude.Length == 0)
				{
					continue;
				}
				_noidung.Add(new noidung
				{
					tieude = tieude,
					noidungchinh = noidungchinh,
					codescript = codescript
				});
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Lỗi load nội dung: " + ex.Message);
		}
	}

	/// <summary>Proxy dòng (rowIndex+1) khớp Account.txt / lưới; dòng proxy thừa bỏ qua.</summary>
	private ProxyInfo GetProxyForAccountRow(int rowIndex)
	{
		if (_proxyList.Count == 0 || rowIndex < 0)
		{
			return null;
		}
		if (rowIndex >= _proxyList.Count)
		{
			return null;
		}
		return _proxyList[rowIndex];
	}

	private void mainPanel_Paint(object sender, PaintEventArgs e)
	{
	}

	private void toolStripMenuItem1_Click(object sender, EventArgs e)
	{
		HashSet<string> hashSet = new HashSet<string>();
		foreach (DataGridViewRow item in (IEnumerable)dataGridView1.Rows)
		{
			if (item.Cells["UID"].Value != null)
			{
				string text = item.Cells["UID"].Value.ToString();
				if (!string.IsNullOrWhiteSpace(text))
				{
					hashSet.Add(text);
				}
			}
		}
		DataObject dataObject = (DataObject)Clipboard.GetDataObject();
		if (dataObject == null || !dataObject.GetDataPresent(DataFormats.Text))
		{
			MessageBox.Show("Clipboard không có dữ liệu.");
			return;
		}
		string input = dataObject.GetData(DataFormats.Text).ToString().TrimEnd('\r', '\n');
		string[] array = Regex.Split(input, "\r\n");
		Random random = new Random();
		int num = 0;
		string[] array2 = array;
		foreach (string text2 in array2)
		{
			try
			{
				if (!string.IsNullOrWhiteSpace(text2))
				{
					string[] array3 = text2.Split(new char[3] { '|', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
					string text3 = "";
					string text4 = "";
					string text5 = "";
					string text6 = "";
					text3 = array3[0];
					text4 = ((array3.Length > 1) ? array3[1] : "");
					text5 = ((array3.Length > 2) ? array3[2] : "");
					text6 = ((array3.Length > 3) ? array3[3] : "");
					int num2 = dataGridView1.Rows.Add();
					dataGridView1.Rows[num2].Cells["UID"].Value = text3;
					dataGridView1.Rows[num2].Cells["PASS"].Value = text4;
					dataGridView1.Rows[num2].Cells["MAIL2"].Value = text5;
					dataGridView1.Rows[num2].Cells["MA2FA"].Value = text6;
					dataGridView1.Rows[num2].Cells["STT"].Value = num2 + 1;
					hashSet.Add(text3);
				}
			}
			catch
			{
			}
		}
		for (int j = 0; j < dataGridView1.Rows.Count; j++)
		{
			if (!dataGridView1.Rows[j].IsNewRow)
			{
				dataGridView1.Rows[j].Cells["STT"].Value = j + 1;
			}
		}
		if (num > 0)
		{
			MessageBox.Show($"Đã bỏ qua {num} dòng lỗi.");
		}
	}

	private void copySelectToolStripMenuItem_Click(object sender, EventArgs e)
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (DataGridViewRow selectedRow in dataGridView1.SelectedRows)
			{
				if (!selectedRow.IsNewRow)
				{
					string value = method_35(selectedRow.Index, "UID");
					string value2 = method_35(selectedRow.Index, "PASS");
					string value3 = method_35(selectedRow.Index, "COOKIE");
					StringBuilder stringBuilder2 = stringBuilder;
					StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(2, 3, stringBuilder2);
					handler.AppendFormatted(value);
					handler.AppendLiteral("|");
					handler.AppendFormatted(value2);
					handler.AppendLiteral("|");
					handler.AppendFormatted(value3);
					stringBuilder2.AppendLine(ref handler);
				}
			}
			Clipboard.SetText(stringBuilder.ToString());
		}
		catch (Exception ex)
		{
			MessageBox.Show("Đã xảy ra lỗi: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private string method_35(int int_0, string string_7)
	{
		try
		{
			return dataGridView1.Rows[int_0].Cells[string_7].Value.ToString();
		}
		catch
		{
			return "";
		}
	}

	private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
	{
		try
		{
			DialogResult dialogResult = MessageBox.Show("Xóa DATA Đã Chọn", "CẢNH BÁO", MessageBoxButtons.YesNo);
			if (dialogResult != DialogResult.Yes)
			{
				return;
			}
			foreach (object selectedRow in dataGridView1.SelectedRows)
			{
				DataGridViewRow dataGridViewRow = (DataGridViewRow)selectedRow;
				dataGridView1.Rows.RemoveAt(dataGridViewRow.Index);
			}
		}
		catch
		{
		}
	}

	private void deleteAllToolStripMenuItem_Click(object sender, EventArgs e)
	{
		try
		{
			DialogResult dialogResult = MessageBox.Show("Xóa Tất Cả DATA", "CẢNH BÁO", MessageBoxButtons.YesNo);
			if (dialogResult == DialogResult.Yes)
			{
				dataGridView1.Rows.Clear();
			}
		}
		catch
		{
		}
	}

	private async void Form1_FormClosed(object sender, FormClosedEventArgs e)
	{
	}

	private async void Form1_Load(object sender, EventArgs e)
	{
		LoadSettings();
		SetAccount(dataGridView1);
		UpdateGpmGroupControlsVisible();
		if (cb_sudungproxy.Checked)
		{
			await RefreshGpmGroupComboAsync();
		}
	}

	private void Form1_Shown(object sender, EventArgs e)
	{
		WindowState = FormWindowState.Normal;
		Visible = true;
		Activate();
		BringToFront();
		TopMost = true;
		BeginInvoke(new Action(() => TopMost = false));
	}

	private void UpdateStatus()
	{
		if (lbl_status.InvokeRequired)
		{
			lbl_status.Invoke(UpdateStatus);
			return;
		}
		lbl_status.Text = $"Đang chạy: {_runningThreads}";
	}

	private void SaveAccount()
	{
		if (!Directory.Exists("Data"))
		{
			Directory.CreateDirectory("Data");
		}
		List<string> list = new List<string>(dataGridView1.Rows.Count);
		foreach (DataGridViewRow item in (IEnumerable)dataGridView1.Rows)
		{
			if (!item.IsNewRow)
			{
				string value = item.Cells["UID"]?.Value?.ToString() ?? "";
				string value2 = item.Cells["PASS"]?.Value?.ToString() ?? "";
				string value3 = item.Cells["MA2FA"]?.Value?.ToString() ?? "";
				string value4 = item.Cells["MAIL2"]?.Value?.ToString() ?? "";
				string value5 = item.Cells["LINK_DOC"]?.Value?.ToString() ?? "";
				string value6 = item.Cells["LINK_SCRIPT"]?.Value?.ToString() ?? "";
				string value7 = item.Cells["LINK_SHEET"]?.Value?.ToString() ?? "";
				list.Add($"{value}|{value2}|{value3}|{value4}|{value5}|{value6}|{value7}");
			}
		}
		File.WriteAllLines("Data/Account.txt", list);
	}

	private void LoadSettings()
	{
		try
		{
			if (!File.Exists("Data/Setting.txt"))
			{
				return;
			}
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			string[] array = File.ReadAllLines("Data/Setting.txt");
			foreach (string text in array)
			{
				if (!string.IsNullOrWhiteSpace(text))
				{
					string[] array2 = text.Split(new char[1] { '=' }, 2);
					if (array2.Length == 2)
					{
						dictionary[array2[0]] = array2[1];
					}
				}
			}
			if (dictionary.ContainsKey("so_account_log"))
			{
				txt_so_account_log.Text = dictionary["so_account_log"];
			}
			if (dictionary.ContainsKey("username"))
			{
				txt_username.Text = dictionary["username"];
			}
			if (dictionary.ContainsKey("luong"))
			{
				txt_luong.Text = dictionary["luong"];
			}
			if (dictionary.ContainsKey("sudungproxy") && bool.TryParse(dictionary["sudungproxy"], out var result))
			{
				cb_sudungproxy.CheckedChanged -= cb_sudungproxy_CheckedChanged;
				try
				{
					cb_sudungproxy.Checked = result;
				}
				finally
				{
					cb_sudungproxy.CheckedChanged += cb_sudungproxy_CheckedChanged;
				}
			}
			if (dictionary.ContainsKey("gpm_proxy_group_id"))
			{
				_savedGpmGroupId = dictionary["gpm_proxy_group_id"]?.Trim();
			}
			if (dictionary.ContainsKey("changeinfo") && bool.TryParse(dictionary["changeinfo"], out var result2))
			{
				cb_changeinfo.Checked = result2;
			}
			if (dictionary.ContainsKey("taoform") && bool.TryParse(dictionary["taoform"], out var result3))
			{
				cb_taoform.Checked = result3;
			}
			if (dictionary.ContainsKey("hide") && bool.TryParse(dictionary["hide"], out var result4))
			{
				cb_hide.Checked = result4;
			}
		}
		catch (Exception)
		{
		}
	}

	private void SaveSettings()
	{
		if (!Directory.Exists("Data"))
		{
			Directory.CreateDirectory("Data");
		}
		List<string> contents = new List<string>
		{
			"so_account_log=" + txt_so_account_log.Text,
			"username=" + txt_username.Text,
			"luong=" + txt_luong.Text,
			$"sudungproxy={cb_sudungproxy.Checked}",
			$"gpm_proxy_group_id={GetSelectedGpmGroupId() ?? ""}",
			$"hide={cb_hide.Checked}"
		};
		File.WriteAllLines("Data/Setting.txt", contents);
	}

	private static string EscapeProcessArg(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "\"\"";
		}
		if (s.IndexOfAny(new char[3] { ' ', '\t', '"' }) < 0)
		{
			return s;
		}
		return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
	}

	private void ShowFill2faLiveWarning(string message)
	{
		try
		{
			if (InvokeRequired)
			{
				Invoke(new Action(() => MessageBox.Show(message, "Fill2faLive", MessageBoxButtons.OK, MessageBoxIcon.Warning)));
			}
			else
			{
				MessageBox.Show(message, "Fill2faLive", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
		}
		catch
		{
		}
	}

	/// <param name="gridRow0Based">Hàng lưới = dòng tương ứng trong Data/Account.txt (0-based); truyền cho Fill2faLive --only-line.</param>
	private void TryLaunchFill2faLive(int gridRow0Based)
	{
		string root = Application.StartupPath.TrimEnd(Path.DirectorySeparatorChar);
		string releaseExe = Path.Combine(root, "Fill2faLive", "bin", "Release", "net8.0", "Fill2faLive.exe");
		string debugExe = Path.Combine(root, "Fill2faLive", "bin", "Debug", "net8.0", "Fill2faLive.exe");
		string csproj = Path.Combine(root, "Fill2faLive", "Fill2faLive.csproj");
		List<string> args = new List<string>();
		if (Environment.GetEnvironmentVariable("FILL2FA_NO_AUTO_FIND_CDP") != "1")
		{
			args.Add("--auto-find-cdp");
		}
		args.Add("--only-line");
		args.Add((gridRow0Based + 1).ToString());
		string argLine = string.Join(" ", args.ConvertAll(EscapeProcessArg));
		try
		{
			if (File.Exists(releaseExe))
			{
				StartFill2faLiveProcess(releaseExe, argLine, root);
				return;
			}
			if (File.Exists(debugExe))
			{
				StartFill2faLiveProcess(debugExe, argLine, root);
				return;
			}
			if (File.Exists(csproj))
			{
				ProcessStartInfo psi = new ProcessStartInfo
				{
					FileName = "dotnet",
					Arguments = "run --project " + EscapeProcessArg(csproj) + " --configuration Release -- " + argLine,
					WorkingDirectory = root,
					UseShellExecute = false,
					CreateNoWindow = false
				};
				psi.Environment["NODE_OPTIONS"] = "--no-deprecation";
				Process.Start(psi);
				return;
			}
			ShowFill2faLiveWarning("Không tìm thấy Fill2faLive (build Release/Debug hoặc thư mục Fill2faLive).");
		}
		catch (Exception ex)
		{
			ShowFill2faLiveWarning("Không chạy được Fill2faLive: " + ex.Message);
		}
	}

	private static void StartFill2faLiveProcess(string exePath, string arguments, string workingDirectory)
	{
		ProcessStartInfo psi = new ProcessStartInfo
		{
			FileName = exePath,
			Arguments = arguments,
			WorkingDirectory = workingDirectory,
			UseShellExecute = false,
			CreateNoWindow = false
		};
		psi.Environment["NODE_OPTIONS"] = "--no-deprecation";
		Process.Start(psi);
	}

	private void Form1_FormClosing(object sender, FormClosingEventArgs e)
	{
		try
		{
			Validate();
			SaveAccount();
			SaveSettings();
		}
		catch
		{
		}
	}

	public async Task<bool> DownloadFileFromFolder(string m_filename, string user)
	{
		string inputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "datafile", "input");
		string usedDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "datafile", "used");
		Directory.CreateDirectory(inputDir);
		Directory.CreateDirectory(usedDir);
		try
		{
			string[] files = Directory.GetFiles(inputDir);
			new WebClient();
			if (files.Length == 0)
			{
				return false;
			}
			string sourceFile = files[0];
			string fileName = Path.GetFileName(sourceFile);
			string usedFile = Path.Combine(usedDir, fileName);
			if (File.Exists(m_filename))
			{
				File.Delete(m_filename);
			}
			File.Copy(sourceFile, m_filename, overwrite: true);
			File.Move(sourceFile, usedFile);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private void xuatCookieToolStripMenuItem_Click(object sender, EventArgs e)
	{
		try
		{
			List<string> list = new List<string>();
			foreach (DataGridViewRow item in (IEnumerable)dataGridView1.Rows)
			{
				if (!item.IsNewRow)
				{
					string value = method_35(item.Index, "UID");
					string value2 = method_35(item.Index, "PASS");
					string value3 = method_35(item.Index, "COOKIE");
					if (!string.IsNullOrWhiteSpace(value))
					{
						list.Add($"{value}|{value2}|{value3}");
					}
				}
			}
			if (list.Count == 0)
			{
				MessageBox.Show("Không có dữ liệu để xuất!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			string text = Path.Combine(Application.StartupPath, "Cookie");
			if (!Directory.Exists(text))
			{
				Directory.CreateDirectory(text);
			}
			int num = 300;
			int num2 = (int)Math.Ceiling((double)list.Count / (double)num);
			string value4 = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
			for (int i = 0; i < num2; i++)
			{
				List<string> contents = list.Skip(i * num).Take(num).ToList();
				string path = $"Cookie_{value4}_{i + 1}.txt";
				string path2 = Path.Combine(text, path);
				File.WriteAllLines(path2, contents);
			}
			DialogResult dialogResult = MessageBox.Show($"Xuất thành công!\nTổng dòng: {list.Count}\nTổng file: {num2}\n\nMở thư mục?", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			if (dialogResult == DialogResult.OK)
			{
				Process.Start("explorer.exe", text);
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Đã xảy ra lỗi:\n" + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private void button1_Click(object sender, EventArgs e)
	{
		string arguments = "Data/proxy.txt";
		try
		{
			Process.Start("notepad.exe", arguments);
		}
		catch (Exception ex)
		{
			Console.WriteLine("The file could not be opened:");
			Console.WriteLine(ex.Message);
		}
	}

	private void proxyToolStripMenuItem_Click(object sender, EventArgs e)
	{
		string arguments = "Data/proxy.txt";
		try
		{
			Process.Start("notepad.exe", arguments);
		}
		catch (Exception ex)
		{
			Console.WriteLine("The file could not be opened:");
			Console.WriteLine(ex.Message);
		}
	}

	private void tieudeToolStripMenuItem_Click(object sender, EventArgs e)
	{
		string arguments = "data/tieude.txt";
		try
		{
			Process.Start("notepad.exe", arguments);
		}
		catch (Exception ex)
		{
			Console.WriteLine("The file could not be opened:");
			Console.WriteLine(ex.Message);
		}
	}

	private void noidungToolStripMenuItem_Click(object sender, EventArgs e)
	{
		string arguments = "data/noidung.txt";
		try
		{
			Process.Start("notepad.exe", arguments);
		}
		catch (Exception ex)
		{
			Console.WriteLine("The file could not be opened:");
			Console.WriteLine(ex.Message);
		}
	}

	private void sciptToolStripMenuItem_Click(object sender, EventArgs e)
	{
		string arguments = "data/codesc.txt";
		try
		{
			Process.Start("notepad.exe", arguments);
		}
		catch (Exception ex)
		{
			Console.WriteLine("The file could not be opened:");
			Console.WriteLine(ex.Message);
		}
	}

	private async void copy2FAToolStripMenuItem_Click(object sender, EventArgs e)
	{
		try
		{
			if (dataGridView1.SelectedRows.Count == 0)
			{
				MessageBox.Show("Vui lòng chọn 1 dòng!");
				return;
			}
			DataGridViewRow row = dataGridView1.SelectedRows[0];
			string ma2fa = row.Cells["MA2FA"].Value?.ToString();
			if (string.IsNullOrWhiteSpace(ma2fa))
			{
				MessageBox.Show("Không có mã 2FA!");
				return;
			}
			string token = await Get2FAToken(ma2fa);
			if (string.IsNullOrWhiteSpace(token))
			{
				MessageBox.Show("Không lấy được token!");
				return;
			}
			Clipboard.SetText(token);
			MessageBox.Show("Đã copy 2fa: " + token, "Thành công");
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			MessageBox.Show("Lỗi: " + ex2.Message);
		}
	}

	private void linkMainToolStripMenuItem_Click(object sender, EventArgs e)
	{
		string arguments = "data/linkmain.txt";
		try
		{
			Process.Start("notepad.exe", arguments);
		}
		catch (Exception ex)
		{
			Console.WriteLine("The file could not be opened:");
			Console.WriteLine(ex.Message);
		}
	}

	private void acoountToolStripMenuItem_Click(object sender, EventArgs e)
	{
		string arguments = "data/account.txt";
		try
		{
			Process.Start("notepad.exe", arguments);
		}
		catch (Exception ex)
		{
			Console.WriteLine("The file could not be opened:");
			Console.WriteLine(ex.Message);
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		this.components = new System.ComponentModel.Container();
		System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle = new System.Windows.Forms.DataGridViewCellStyle();
		System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
		System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
		System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PlayAPP.Form1));
		this.sidebar = new System.Windows.Forms.Panel();
		this.cb_taoform = new System.Windows.Forms.CheckBox();
		this.cb_changeinfo = new System.Windows.Forms.CheckBox();
		this.label3 = new System.Windows.Forms.Label();
		this.txt_so_account_log = new System.Windows.Forms.TextBox();
		this.lbl_status = new System.Windows.Forms.Label();
		this.label2 = new System.Windows.Forms.Label();
		this.txt_luong = new System.Windows.Forms.TextBox();
		this.label1 = new System.Windows.Forms.Label();
		this.txt_username = new System.Windows.Forms.TextBox();
		this.cb_sudungproxy = new System.Windows.Forms.CheckBox();
		this.lbl_gpm_group = new System.Windows.Forms.Label();
		this.cb_gpm_group = new System.Windows.Forms.ComboBox();
		this.cb_hide = new System.Windows.Forms.CheckBox();
		this.topbar = new System.Windows.Forms.Panel();
		this.btn_start = new System.Windows.Forms.Button();
		this.btn_stop = new System.Windows.Forms.Button();
		this.dataGridView1 = new System.Windows.Forms.DataGridView();
		this.STT = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.UID = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.PASS = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.MA2FA = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.MAIL2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.STATUS = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.PROXY = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.LINK_DOC = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.LINK_SCRIPT = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.LINK_SHEET = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.LINK_MAIN = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
		this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
		this.acoountToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.copySelectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.deleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.deleteAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.proxyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.xuatCookieToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.tieudeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.noidungToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.sciptToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.linkMainToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.copy2FAToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.cb_offchrome = new System.Windows.Forms.CheckBox();
		this.sidebar.SuspendLayout();
		this.topbar.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
		this.contextMenuStrip1.SuspendLayout();
		base.SuspendLayout();
		this.sidebar.BackColor = System.Drawing.Color.FromArgb(24, 24, 24);
		this.sidebar.Controls.Add(this.cb_offchrome);
		this.sidebar.Controls.Add(this.cb_taoform);
		this.sidebar.Controls.Add(this.cb_changeinfo);
		this.sidebar.Controls.Add(this.label3);
		this.sidebar.Controls.Add(this.txt_so_account_log);
		this.sidebar.Controls.Add(this.lbl_status);
		this.sidebar.Controls.Add(this.label2);
		this.sidebar.Controls.Add(this.txt_luong);
		this.sidebar.Controls.Add(this.label1);
		this.sidebar.Controls.Add(this.txt_username);
		this.sidebar.Controls.Add(this.cb_sudungproxy);
		this.sidebar.Controls.Add(this.lbl_gpm_group);
		this.sidebar.Controls.Add(this.cb_gpm_group);
		this.sidebar.Controls.Add(this.cb_hide);
		this.sidebar.Dock = System.Windows.Forms.DockStyle.Left;
		this.sidebar.Location = new System.Drawing.Point(0, 60);
		this.sidebar.Name = "sidebar";
		this.sidebar.Size = new System.Drawing.Size(250, 601);
		this.sidebar.TabIndex = 0;
		this.cb_taoform.AutoSize = true;
		this.cb_taoform.ForeColor = System.Drawing.Color.White;
		this.cb_taoform.Location = new System.Drawing.Point(20, 341);
		this.cb_taoform.Name = "cb_taoform";
		this.cb_taoform.TabIndex = 10;
		this.cb_taoform.Text = "Tạo Form + Ggs + Script";
		this.cb_changeinfo.ForeColor = System.Drawing.Color.White;
		this.cb_changeinfo.Location = new System.Drawing.Point(20, 316);
		this.cb_changeinfo.Name = "cb_changeinfo";
		this.cb_changeinfo.Size = new System.Drawing.Size(104, 24);
		this.cb_changeinfo.TabIndex = 9;
		this.cb_changeinfo.Text = "Đổi Info Gmail";
		this.label3.ForeColor = System.Drawing.Color.Silver;
		this.label3.Location = new System.Drawing.Point(20, 49);
		this.label3.Name = "label3";
		this.label3.Size = new System.Drawing.Size(210, 23);
		this.label3.TabIndex = 8;
		this.label3.Text = "Số Account cần Log (0=tất cả)";
		this.txt_so_account_log.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
		this.txt_so_account_log.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.txt_so_account_log.ForeColor = System.Drawing.Color.White;
		this.txt_so_account_log.Location = new System.Drawing.Point(20, 76);
		this.txt_so_account_log.Name = "txt_so_account_log";
		this.txt_so_account_log.Size = new System.Drawing.Size(207, 23);
		this.txt_so_account_log.TabIndex = 7;
		this.lbl_status.AutoSize = true;
		this.lbl_status.Font = new System.Drawing.Font("Segoe UI", 11.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lbl_status.ForeColor = System.Drawing.Color.Lime;
		this.lbl_status.Location = new System.Drawing.Point(20, 16);
		this.lbl_status.Name = "lbl_status";
		this.lbl_status.Size = new System.Drawing.Size(97, 20);
		this.lbl_status.TabIndex = 2;
		this.lbl_status.Text = "Status: Ready";
		this.label2.ForeColor = System.Drawing.Color.Silver;
		this.label2.Location = new System.Drawing.Point(19, 164);
		this.label2.Name = "label2";
		this.label2.Size = new System.Drawing.Size(69, 23);
		this.label2.TabIndex = 6;
		this.label2.Text = "Luồng";
		this.txt_luong.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
		this.txt_luong.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.txt_luong.ForeColor = System.Drawing.Color.White;
		this.txt_luong.Location = new System.Drawing.Point(17, 189);
		this.txt_luong.Name = "txt_luong";
		this.txt_luong.Size = new System.Drawing.Size(213, 23);
		this.txt_luong.TabIndex = 5;
		this.txt_luong.Text = "5";
		this.label1.ForeColor = System.Drawing.Color.Silver;
		this.label1.Location = new System.Drawing.Point(20, 109);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(69, 23);
		this.label1.TabIndex = 1;
		this.label1.Text = "Username";
		this.txt_username.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
		this.txt_username.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.txt_username.ForeColor = System.Drawing.Color.White;
		this.txt_username.Location = new System.Drawing.Point(20, 134);
		this.txt_username.Name = "txt_username";
		this.txt_username.Size = new System.Drawing.Size(210, 23);
		this.txt_username.TabIndex = 2;
		this.txt_username.Text = "admin";
		this.cb_sudungproxy.ForeColor = System.Drawing.Color.White;
		this.cb_sudungproxy.Location = new System.Drawing.Point(20, 214);
		this.cb_sudungproxy.Name = "cb_sudungproxy";
		this.cb_sudungproxy.Size = new System.Drawing.Size(130, 24);
		this.cb_sudungproxy.TabIndex = 3;
		this.cb_sudungproxy.Text = "Sử dụng Proxy";
		this.cb_sudungproxy.CheckedChanged += new System.EventHandler(cb_sudungproxy_CheckedChanged);
		this.lbl_gpm_group.ForeColor = System.Drawing.Color.Silver;
		this.lbl_gpm_group.Location = new System.Drawing.Point(20, 238);
		this.lbl_gpm_group.Name = "lbl_gpm_group";
		this.lbl_gpm_group.Size = new System.Drawing.Size(210, 18);
		this.lbl_gpm_group.TabIndex = 12;
		this.lbl_gpm_group.Text = "Nhóm GPM (khi dùng proxy)";
		this.cb_gpm_group.BackColor = System.Drawing.Color.FromArgb(40, 40, 40);
		this.cb_gpm_group.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.cb_gpm_group.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.cb_gpm_group.ForeColor = System.Drawing.Color.White;
		this.cb_gpm_group.Location = new System.Drawing.Point(20, 258);
		this.cb_gpm_group.Name = "cb_gpm_group";
		this.cb_gpm_group.Size = new System.Drawing.Size(210, 23);
		this.cb_gpm_group.TabIndex = 13;
		this.lbl_gpm_group.Visible = false;
		this.cb_gpm_group.Visible = false;
		this.cb_hide.ForeColor = System.Drawing.Color.White;
		this.cb_hide.Location = new System.Drawing.Point(20, 288);
		this.cb_hide.Name = "cb_hide";
		this.cb_hide.Size = new System.Drawing.Size(104, 24);
		this.cb_hide.TabIndex = 4;
		this.cb_hide.Text = "Ẩn trình duyệt";
		this.topbar.BackColor = System.Drawing.Color.FromArgb(37, 37, 38);
		this.topbar.Controls.Add(this.btn_start);
		this.topbar.Controls.Add(this.btn_stop);
		this.topbar.Dock = System.Windows.Forms.DockStyle.Top;
		this.topbar.Location = new System.Drawing.Point(0, 0);
		this.topbar.Name = "topbar";
		this.topbar.Size = new System.Drawing.Size(1184, 60);
		this.topbar.TabIndex = 1;
		this.btn_start.BackColor = System.Drawing.Color.FromArgb(0, 120, 215);
		this.btn_start.FlatAppearance.BorderSize = 0;
		this.btn_start.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.btn_start.ForeColor = System.Drawing.Color.White;
		this.btn_start.Location = new System.Drawing.Point(20, 12);
		this.btn_start.Name = "btn_start";
		this.btn_start.Size = new System.Drawing.Size(100, 35);
		this.btn_start.TabIndex = 0;
		this.btn_start.Text = "▶ START";
		this.btn_start.UseVisualStyleBackColor = false;
		this.btn_start.Click += new System.EventHandler(btnStart_Click);
		this.btn_stop.BackColor = System.Drawing.Color.FromArgb(180, 50, 50);
		this.btn_stop.FlatAppearance.BorderSize = 0;
		this.btn_stop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.btn_stop.ForeColor = System.Drawing.Color.White;
		this.btn_stop.Location = new System.Drawing.Point(130, 12);
		this.btn_stop.Name = "btn_stop";
		this.btn_stop.Size = new System.Drawing.Size(100, 35);
		this.btn_stop.TabIndex = 1;
		this.btn_stop.Text = "■ STOP";
		this.btn_stop.UseVisualStyleBackColor = false;
		this.btn_stop.Click += new System.EventHandler(btnStop_Click);
		this.dataGridView1.BackgroundColor = System.Drawing.Color.FromArgb(30, 30, 30);
		this.dataGridView1.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
		dataGridViewCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
		dataGridViewCellStyle.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
		dataGridViewCellStyle.Font = new System.Drawing.Font("Segoe UI", 11.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		dataGridViewCellStyle.ForeColor = System.Drawing.SystemColors.ButtonHighlight;
		dataGridViewCellStyle.SelectionBackColor = System.Drawing.SystemColors.Highlight;
		dataGridViewCellStyle.SelectionForeColor = System.Drawing.SystemColors.ControlLight;
		dataGridViewCellStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
		this.dataGridView1.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle;
		this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
		this.dataGridView1.Columns.AddRange(this.STT, this.UID, this.PASS, this.MA2FA, this.MAIL2, this.STATUS, this.PROXY, this.LINK_DOC, this.LINK_SCRIPT, this.LINK_SHEET, this.LINK_MAIN);
		this.dataGridView1.ContextMenuStrip = this.contextMenuStrip1;
		dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
		dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
		dataGridViewCellStyle2.Font = new System.Drawing.Font("Segoe UI", 11.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ButtonHighlight;
		dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.ControlLight;
		dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.ActiveCaptionText;
		dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
		this.dataGridView1.DefaultCellStyle = dataGridViewCellStyle2;
		this.dataGridView1.EnableHeadersVisualStyles = false;
		this.dataGridView1.Location = new System.Drawing.Point(252, 60);
		this.dataGridView1.Name = "dataGridView1";
		this.dataGridView1.RowHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
		dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
		dataGridViewCellStyle3.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
		dataGridViewCellStyle3.Font = new System.Drawing.Font("Segoe UI", 11.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.ButtonHighlight;
		dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
		dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
		dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
		this.dataGridView1.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
		this.dataGridView1.Size = new System.Drawing.Size(932, 601);
		this.dataGridView1.TabIndex = 2;
		this.STT.FillWeight = 70f;
		this.STT.HeaderText = "#";
		this.STT.Name = "STT";
		this.STT.Width = 70;
		this.UID.FillWeight = 120f;
		this.UID.HeaderText = "UID";
		this.UID.Name = "UID";
		this.UID.Width = 120;
		this.PASS.FillWeight = 120f;
		this.PASS.HeaderText = "PASS";
		this.PASS.Name = "PASS";
		this.PASS.Width = 120;
		this.MA2FA.HeaderText = "2FA";
		this.MA2FA.Name = "MA2FA";
		this.MAIL2.FillWeight = 150f;
		this.MAIL2.HeaderText = "Mail Backup";
		this.MAIL2.Name = "MAIL2";
		this.MAIL2.Width = 150;
		this.STATUS.FillWeight = 260f;
		this.STATUS.HeaderText = "STATUS (log từng bước)";
		this.STATUS.Name = "STATUS";
		this.STATUS.MinimumWidth = 220;
		this.STATUS.Width = 320;
		this.PROXY.HeaderText = "PROXY";
		this.PROXY.Name = "PROXY";
		this.LINK_DOC.FillWeight = 200f;
		this.LINK_DOC.HeaderText = "Link Doc";
		this.LINK_DOC.Name = "LINK_DOC";
		this.LINK_DOC.Width = 200;
		this.LINK_SCRIPT.FillWeight = 200f;
		this.LINK_SCRIPT.HeaderText = "Link Script";
		this.LINK_SCRIPT.Name = "LINK_SCRIPT";
		this.LINK_SCRIPT.Width = 200;
		this.LINK_SHEET.FillWeight = 200f;
		this.LINK_SHEET.HeaderText = "Link Sheet";
		this.LINK_SHEET.Name = "LINK_SHEET";
		this.LINK_SHEET.Width = 200;
		this.LINK_MAIN.FillWeight = 200f;
		this.LINK_MAIN.HeaderText = "Link Main";
		this.LINK_MAIN.Name = "LINK_MAIN";
		this.LINK_MAIN.Width = 200;
		this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[12]
		{
			this.toolStripMenuItem1, this.acoountToolStripMenuItem, this.copySelectToolStripMenuItem, this.deleteToolStripMenuItem, this.deleteAllToolStripMenuItem, this.proxyToolStripMenuItem, this.xuatCookieToolStripMenuItem, this.tieudeToolStripMenuItem, this.noidungToolStripMenuItem, this.sciptToolStripMenuItem,
			this.linkMainToolStripMenuItem, this.copy2FAToolStripMenuItem
		});
		this.contextMenuStrip1.Name = "contextMenuStrip1";
		this.contextMenuStrip1.Size = new System.Drawing.Size(156, 268);
		this.toolStripMenuItem1.Name = "toolStripMenuItem1";
		this.toolStripMenuItem1.Size = new System.Drawing.Size(155, 22);
		this.toolStripMenuItem1.Text = "Add Mail";
		this.toolStripMenuItem1.Click += new System.EventHandler(toolStripMenuItem1_Click);
		this.acoountToolStripMenuItem.Name = "acoountToolStripMenuItem";
		this.acoountToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
		this.acoountToolStripMenuItem.Text = "Account";
		this.acoountToolStripMenuItem.Click += new System.EventHandler(acoountToolStripMenuItem_Click);
		this.copySelectToolStripMenuItem.Name = "copySelectToolStripMenuItem";
		this.copySelectToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
		this.copySelectToolStripMenuItem.Text = "Copy";
		this.copySelectToolStripMenuItem.Click += new System.EventHandler(copySelectToolStripMenuItem_Click);
		this.deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
		this.deleteToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
		this.deleteToolStripMenuItem.Text = "Delete";
		this.deleteToolStripMenuItem.Click += new System.EventHandler(deleteToolStripMenuItem_Click);
		this.deleteAllToolStripMenuItem.Name = "deleteAllToolStripMenuItem";
		this.deleteAllToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
		this.deleteAllToolStripMenuItem.Text = "Delete All";
		this.deleteAllToolStripMenuItem.Click += new System.EventHandler(deleteAllToolStripMenuItem_Click);
		this.proxyToolStripMenuItem.Name = "proxyToolStripMenuItem";
		this.proxyToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
		this.proxyToolStripMenuItem.Text = "Proxy";
		this.proxyToolStripMenuItem.Click += new System.EventHandler(proxyToolStripMenuItem_Click);
		this.xuatCookieToolStripMenuItem.Name = "xuatCookieToolStripMenuItem";
		this.xuatCookieToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
		this.xuatCookieToolStripMenuItem.Text = "Xuất All Cookie";
		this.xuatCookieToolStripMenuItem.Click += new System.EventHandler(xuatCookieToolStripMenuItem_Click);
		this.tieudeToolStripMenuItem.Name = "tieudeToolStripMenuItem";
		this.tieudeToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
		this.tieudeToolStripMenuItem.Text = "Tiêu Đề";
		this.tieudeToolStripMenuItem.Click += new System.EventHandler(tieudeToolStripMenuItem_Click);
		this.noidungToolStripMenuItem.Name = "noidungToolStripMenuItem";
		this.noidungToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
		this.noidungToolStripMenuItem.Text = "Nội Dung";
		this.noidungToolStripMenuItem.Click += new System.EventHandler(noidungToolStripMenuItem_Click);
		this.sciptToolStripMenuItem.Name = "sciptToolStripMenuItem";
		this.sciptToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
		this.sciptToolStripMenuItem.Text = "Code Script";
		this.sciptToolStripMenuItem.Click += new System.EventHandler(sciptToolStripMenuItem_Click);
		this.linkMainToolStripMenuItem.Name = "linkMainToolStripMenuItem";
		this.linkMainToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
		this.linkMainToolStripMenuItem.Text = "Link Main";
		this.linkMainToolStripMenuItem.Click += new System.EventHandler(linkMainToolStripMenuItem_Click);
		this.copy2FAToolStripMenuItem.Name = "copy2FAToolStripMenuItem";
		this.copy2FAToolStripMenuItem.Size = new System.Drawing.Size(155, 22);
		this.copy2FAToolStripMenuItem.Text = "Copy 2FA";
		this.copy2FAToolStripMenuItem.Click += new System.EventHandler(copy2FAToolStripMenuItem_Click);
		this.cb_offchrome.ForeColor = System.Drawing.Color.White;
		this.cb_offchrome.Location = new System.Drawing.Point(20, 366);
		this.cb_offchrome.Name = "cb_offchrome";
		this.cb_offchrome.Size = new System.Drawing.Size(104, 24);
		this.cb_offchrome.TabIndex = 11;
		this.cb_offchrome.Text = "Tắt Chrome";
		this.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
		base.ClientSize = new System.Drawing.Size(1184, 661);
		base.Controls.Add(this.dataGridView1);
		base.Controls.Add(this.sidebar);
		base.Controls.Add(this.topbar);
		this.Font = new System.Drawing.Font("Segoe UI", 9f);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
		base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
		base.MaximizeBox = false;
		base.Name = "Form1";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "Auto Login";
		base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(Form1_FormClosing);
		base.FormClosed += new System.Windows.Forms.FormClosedEventHandler(Form1_FormClosed);
		base.Load += new System.EventHandler(Form1_Load);
		base.Shown += new System.EventHandler(Form1_Shown);
		this.sidebar.ResumeLayout(false);
		this.sidebar.PerformLayout();
		this.topbar.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
		this.contextMenuStrip1.ResumeLayout(false);
		base.ResumeLayout(false);
	}

	private sealed class GpmGroupListItem
	{
		public GpmGroupListItem(string id, string name)
		{
			Id = id;
			Name = name ?? "";
		}

		public string Id { get; }

		public string Name { get; }

		public override string ToString()
		{
			return string.IsNullOrEmpty(Name) ? "id=" + Id : Name + " (id=" + Id + ")";
		}
	}
}
