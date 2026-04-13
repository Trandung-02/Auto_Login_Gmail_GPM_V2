using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Playwright;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PlayAPP;

public partial class Form1 : Form
{
	private IPlaywright _playwright;

	private List<IBrowser> _browsers = new List<IBrowser>();

	private bool _running = false;

	/// <summary>Tối đa số Chrome chạy song song mỗi đợt (sau mỗi đợt đóng hết rồi mở lô tiếp theo).</summary>
	private const int MaxConcurrentBrowsers = 10;

	private static readonly int[] AllowedLuongValues = new int[3] { 2, 5, 10 };

	private int BrowserCount = 1;

	private List<noidung> _noidung = new List<noidung>();

	/// <summary>Hàng bắt đầu hàng đợi chạy (0-based) khi không chọn nhiều dòng; cập nhật khi click/chọn một dòng trên lưới.</summary>
	private int _runQueueStartRowIndex;

	private int _runningThreads = 0;

	private int _batchOk = 0;

	private int _batchFail = 0;

	private int _lastBatchOk = 0;

	private int _lastBatchFail = 0;

	private ToolTip _uiToolTip;

	private int _totalLoaded = 0;

	private int added = 0;

	private readonly object _lockCount = new object();

	private static readonly Random _rand = new Random();

	private int m_Rowindex = 0;

	private static HttpClient CreateSharedHttpClient()
	{
		HttpClient c = new HttpClient();
		c.Timeout = TimeSpan.FromSeconds(120.0);
		return c;
	}

	private static readonly HttpClient client = CreateSharedHttpClient();

	private int _startRow = 0;

	private int _endRow = -1;

	private static SemaphoreSlim _clipLock = new SemaphoreSlim(1, 1);

	private static readonly object LoginSuccessLogSync = new object();

	private static readonly object AutomationLogSync = new object();

	private static readonly object DeadRecaptchaVerifyLogSync = new object();

	private static readonly object DeadGoogleRestrictionsLogSync = new object();

	private static readonly object DeadGoogleAccountDisabledLogSync = new object();

	private const long AutomationLogMaxBytesBeforeRotate = 5242880L;

	private static readonly UTF8Encoding Utf8NoBomEnc = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	private static readonly Channel<string> AutomationLogChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
	{
		SingleReader = true,
		AllowSynchronousContinuations = false
	});

	private static Task _automationLogPumpTask;

	private static readonly object AutomationLogPumpInitSync = new object();

	private CancellationTokenSource _batchCts;

	private CancellationToken _batchToken;

	private DateTime _batchStartedUtc;

	private int _batchTotalPlanned;

	/// <summary>Lát chờ Playwright (ms) giữa các lần kiểm tra hủy batch.</summary>
	private int _waitSliceMs = 250;

	/// <summary>Sau mỗi lần bấm Run trong editor Apps Script (chờ execution log).</summary>
	private int _scriptRunPauseMs = 10000;

	/// <summary>Hàng nào cần đóng Chrome ngay cả khi tắt "Đóng Chrome sau mỗi account" (tài khoản chết: Account disabled, reCAPTCHA/Verify, myaccount/restrictions). Sau finally còn gọi GPM xóa/ghi chú profile.</summary>
	private readonly ConcurrentDictionary<int, bool> _forceCloseChromeAfterCycle = new ConcurrentDictionary<int, bool>();

	/// <summary>Profile GPM vừa mở theo chỉ số hàng lưới trong batch hiện tại (để gọi API đóng profile).</summary>
	private readonly ConcurrentDictionary<int, string> _gpmProfileIdOpenedForRow = new ConcurrentDictionary<int, string>();

	/// <summary>Khi coi tài khoản chết: lưu id profile GPM tại thời điểm phát hiện (đảm bảo xóa đúng profile đã mở CDP, không nhầm chỉ số _profileIds).</summary>
	private readonly ConcurrentDictionary<int, string> _deadAccountGpmProfileIdByRow = new ConcurrentDictionary<int, string>();

	private List<string> _profileIds = new List<string>();

	/// <summary>Nhóm GPM vừa load (mới tạo nhất), để log / thông báo.</summary>
	private string _gpmLoadedGroupSummary = "";

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

	// "Ẩn trình duyệt" removed

	private DataGridView dataGridView1;

	private Label label2;

	private ComboBox cb_luong;

	private ContextMenuStrip contextMenuStrip1;

	private ToolStripMenuItem toolStripMenuItem1;

	private Label label3;

	private TextBox txt_so_account_log;

	private ToolStripMenuItem copySelectToolStripMenuItem;

	private ToolStripMenuItem deleteToolStripMenuItem;

	private ToolStripMenuItem deleteAllToolStripMenuItem;

	private ToolStripMenuItem xuatCookieToolStripMenuItem;

	private ToolStripMenuItem tieudeToolStripMenuItem;

	private ToolStripMenuItem noidungToolStripMenuItem;

	private ToolStripMenuItem sciptToolStripMenuItem;

	private CheckBox cb_changeinfo;

	private ToolStripMenuItem copy2FAToolStripMenuItem;

	private ToolStripMenuItem xuatCsvLuoiToolStripMenuItem;

	private CheckBox cb_tao_form;

	private CheckBox cb_tao_sheet_script;

	private DataGridViewTextBoxColumn STT;

	private DataGridViewTextBoxColumn UID;

	private DataGridViewTextBoxColumn PASS;

	private DataGridViewTextBoxColumn MA2FA;

	private DataGridViewTextBoxColumn MAIL2;

	private DataGridViewTextBoxColumn STATUS;

	private DataGridViewTextBoxColumn PROXY;

	private ToolStripMenuItem acoountToolStripMenuItem;

	private ToolStripMenuItem menuMoFile;

	private Label lbl_app_tagline;

	private CheckBox cb_offchrome;

	private Button btn_open_data_folder;

	private Button btn_export_diagnostics;

	public Form1()
	{
		InitializeComponent();
		try
		{
			typeof(Control).InvokeMember("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty, null, this, new object[1] { true });
		}
		catch
		{
		}
	}

	private async void btnStart_Click(object sender, EventArgs e)
	{
		if (_running)
		{
			return;
		}
		int lastGridRow = GetLastGridDataRowIndex();
		if (lastGridRow < 0)
		{
			MessageBox.Show("Lưới account trống.");
			return;
		}
		if (dataGridView1.SelectedRows.Count > 1)
		{
			List<int> selected = (from DataGridViewRow r in dataGridView1.SelectedRows
				select r.Index into i
				orderby i
				select i).ToList();
			_startRow = selected.First();
			_endRow = selected.Last();
		}
		else if (dataGridView1.SelectedRows.Count == 1)
		{
			_startRow = dataGridView1.SelectedRows[0].Index;
			if (_startRow < 0 || dataGridView1.Rows[_startRow].IsNewRow)
			{
				_startRow = Math.Min(Math.Max(0, _runQueueStartRowIndex), lastGridRow);
			}
			_endRow = lastGridRow;
		}
		else
		{
			_startRow = Math.Min(Math.Max(0, _runQueueStartRowIndex), lastGridRow);
			_endRow = lastGridRow;
		}
		_startRow = Math.Min(Math.Max(0, _startRow), lastGridRow);
		_endRow = Math.Min(Math.Max(0, _endRow), lastGridRow);
		if (_endRow < _startRow)
		{
			int t = _startRow;
			_startRow = _endRow;
			_endRow = t;
		}
		int luong = GetSelectedLuong();
		if (luong < 1)
		{
			MessageBox.Show("Vui lòng chọn số luồng (2, 5 hoặc 10).");
			return;
		}
		luong = Math.Min(luong, MaxConcurrentBrowsers);
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
		LoadLoginSuccessEmailSets(logGroupId, out HashSet<string> alreadyOkForGroup, out HashSet<string> alreadyOkOtherGroups);
		List<(string uid, string pass, string ma2fa, string mail2, int rowIndex)> pending = new List<(string, string, string, string, int)>();
		foreach (var acc in accountQueue)
		{
			string uidTrim = (acc.uid ?? "").Trim();
			if (alreadyOkForGroup.Contains(uidTrim))
			{
				SetText(acc.rowIndex, "STATUS", "Bỏ qua — đã login (log nhóm này)");
				continue;
			}
			if (alreadyOkOtherGroups.Contains(uidTrim))
			{
				SetText(acc.rowIndex, "STATUS", "Bỏ qua — tài khoản trùng với nhóm khác trước đó (login_success.log)");
				AppendAutomationLog("INFO", acc.rowIndex, acc.uid, "Bỏ qua: UID đã ghi thành công cho nhóm GPM khác trong Data/login_success.log.");
				continue;
			}
			pending.Add(acc);
		}
		accountQueue = pending;
		if (accountQueue.Count == 0)
		{
			MessageBox.Show("Tất cả account trong hàng đợi đã bị bỏ qua: đã login cùng nhóm GPM hiện tại hoặc đã thành công ở nhóm khác (xem Data/login_success.log).");
			return;
		}
		HashSet<string> deadEmails = LoadEmailsMarkedDeadInAnyDeadLog();
		if (deadEmails.Count > 0)
		{
			int beforeDeadSkip = accountQueue.Count;
			List<(string uid, string pass, string ma2fa, string mail2, int rowIndex)> notDead = new List<(string, string, string, string, int)>();
			foreach (var acc in accountQueue)
			{
				string uidTrim = (acc.uid ?? "").Trim();
				if (deadEmails.Contains(uidTrim))
				{
					SetText(acc.rowIndex, "STATUS", "Bỏ qua — tài khoản chết (Data/dead_*.log)");
					AppendAutomationLog("INFO", acc.rowIndex, acc.uid, "Bỏ qua: UID nằm trong log tài khoản chết (reCAPTCHA / restrictions / Account disabled).");
					continue;
				}
				notDead.Add(acc);
			}
			accountQueue = notDead;
			int skippedDead = beforeDeadSkip - accountQueue.Count;
			if (skippedDead > 0)
			{
				AppendAutomationLog("INFO", null, null, "Đã bỏ qua " + skippedDead + " account (đã ghi trong Data/dead_recaptcha_verify.log, dead_google_restrictions.log hoặc dead_google_account_disabled.log).");
			}
		}
		if (accountQueue.Count == 0)
		{
			MessageBox.Show("Sau khi bỏ qua account đã login và account trong log tài khoản chết, không còn hàng nào để chạy.\r\nXóa dòng tương ứng trong các file Data/dead_*.log nếu muốn thử lại.");
			return;
		}
		if (cb_gpm_group.Items.Count == 0 || GetSelectedGpmGroupId() == null)
		{
			MessageBox.Show("Cần chọn nhóm GPM (profile trong nhóm khớp thứ tự hàng 1…N trên lưới). Mở GPM Login (API 19995); nếu combo trống, khởi động lại form hoặc bật/tắt \"Proxy…\" để tải lại danh sách nhóm.");
			return;
		}
		if (cb_sudungproxy.Checked)
		{
			foreach (var acc in accountQueue)
			{
				if (GetProxyForAccountRowOnUi(acc.rowIndex) == null)
				{
					MessageBox.Show($"Hàng {acc.rowIndex + 1}: cột PROXY trống hoặc sai định dạng (host:port hoặc host:port:user:pass). Nhập proxy trên lưới hoặc trong Data\\Account.txt (cột thứ 5).");
					return;
				}
			}
		}
		if (!await TryPingGpmApiAsync().ConfigureAwait(true))
		{
			MessageBox.Show("Không kết nối được GPM Login API tại http://127.0.0.1:19995 .\nMở GPM Login / GPM Browser rồi thử lại.", "GPM không phản hồi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			AppendAutomationLog("WARN", null, null, "Không chạy batch: GPM API 19995 không phản hồi (kiểm tra trong 5 giây).");
			return;
		}
		Interlocked.Exchange(ref _batchOk, 0);
		Interlocked.Exchange(ref _batchFail, 0);
		LoadNoiDung();
		AppendAutomationLog("INFO", null, null, "Bắt đầu batch: " + accountQueue.Count + " account, luồng " + luong + ", bắt buộc PROXY mỗi hàng=" + cb_sudungproxy.Checked + ", nhóm GPM log=\"" + GetGpmGroupIdForLoginLog() + "\".");
		_batchCts?.Dispose();
		_batchCts = new CancellationTokenSource();
		_batchToken = _batchCts.Token;
		_batchStartedUtc = DateTime.UtcNow;
		_batchTotalPlanned = accountQueue.Count;
		_running = true;
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
			_batchToken = default;
			_batchCts?.Dispose();
			_batchCts = null;
			_running = false;
			_lastBatchOk = Volatile.Read(ref _batchOk);
			_lastBatchFail = Volatile.Read(ref _batchFail);
			AppendAutomationLog("INFO", null, null, "Kết thúc batch: OK=" + _lastBatchOk + " Fail=" + _lastBatchFail + " (Playwright đã đóng).");
			UpdateStatus();
		}
	}

	private void btn_open_data_folder_Click(object sender, EventArgs e)
	{
		try
		{
			string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
			Directory.CreateDirectory(dir);
			Process.Start(new ProcessStartInfo
			{
				FileName = dir,
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			MessageBox.Show("Không mở được thư mục Data:\n" + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private void btn_export_diagnostics_Click(object sender, EventArgs e)
	{
		try
		{
			using SaveFileDialog dlg = new SaveFileDialog
			{
				Filter = "ZIP (*.zip)|*.zip",
				FileName = "PlayAPP_diagnostics_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".zip",
				OverwritePrompt = true
			};
			if (dlg.ShowDialog() != DialogResult.OK)
			{
				return;
			}
			string baseDir = AppDomain.CurrentDomain.BaseDirectory;
			if (!DiagnosticsExport.TryCreateZip(baseDir, dlg.FileName, out string err))
			{
				MessageBox.Show("Không tạo được ZIP:\n" + err, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				return;
			}
			AppendAutomationLog("INFO", null, null, "Xuất gói chẩn đoán ZIP: " + dlg.FileName);
			MessageBox.Show("Đã lưu gói chẩn đoán (log + screenshot gần đây, không gồm Account/proxy):\n" + dlg.FileName, "Chẩn đoán", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Lỗi xuất ZIP:\n" + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private async void btnStop_Click(object sender, EventArgs e)
	{
		AppendAutomationLog("INFO", null, null, "Người dùng bấm Dừng — đang đóng trình duyệt.");
		try
		{
			_batchCts?.Cancel();
		}
		catch
		{
		}
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

	/// <summary>Id nhóm GPM đang chọn — dùng cho login_success và bỏ qua trùng nhóm.</summary>
	private string GetGpmGroupIdForLoginLog()
	{
		return GetSelectedGpmGroupId() ?? "_no_proxy_group_";
	}

	/// <summary>Kiểm tra GPM Login API trước khi batch (tránh mở Playwright khi GPM tắt).</summary>
	private static async Task<bool> TryPingGpmApiAsync()
	{
		try
		{
			using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
			using HttpResponseMessage resp = await client.GetAsync(GpmApi.GroupsEndpoint, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
			return resp.IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}

	private static string LoginSuccessLogPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "login_success.log");

	private static string AutomationLogPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "automation.log");

	private static string DeadRecaptchaVerifyLogPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "dead_recaptcha_verify.log");

	private static string DeadGoogleRestrictionsLogPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "dead_google_restrictions.log");

	private static string DeadGoogleAccountDisabledLogPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "dead_google_account_disabled.log");

	private static string GetAppVersionLabel()
	{
		try
		{
			System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
			System.Reflection.AssemblyInformationalVersionAttribute info = asm.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), inherit: false).OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault();
			if (info != null && !string.IsNullOrWhiteSpace(info.InformationalVersion))
			{
				string s = info.InformationalVersion.Trim();
				int plus = s.IndexOf('+');
				return plus > 0 ? s.Substring(0, plus) : s;
			}
			Version v = asm.GetName().Version;
			return v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "?";
		}
		catch
		{
			return "?";
		}
	}

	private static string MaskEmailForLog(string email)
	{
		string e = (email ?? "").Trim();
		if (e.Length == 0)
		{
			return "";
		}
		int at = e.IndexOf('@');
		if (at <= 0)
		{
			return e.Length <= 3 ? "***" : e.Substring(0, 2) + "***";
		}
		string local = e.Substring(0, at);
		string domain = e.Substring(at);
		if (local.Length <= 1)
		{
			return "*" + domain;
		}
		return local.Substring(0, 1) + "***" + domain;
	}

	private static void EnsureAutomationLogPump()
	{
		lock (AutomationLogPumpInitSync)
		{
			if (_automationLogPumpTask != null)
			{
				return;
			}
			_automationLogPumpTask = Task.Run(AutomationLogPumpAsync);
		}
	}

	private static async Task AutomationLogPumpAsync()
	{
		try
		{
			await foreach (string line in AutomationLogChannel.Reader.ReadAllAsync().ConfigureAwait(false))
			{
				WriteAutomationLogLineUnderLock(line);
			}
		}
		catch
		{
		}
	}

	private static void WriteAutomationLogLineUnderLock(string line)
	{
		lock (AutomationLogSync)
		{
			try
			{
				if (File.Exists(AutomationLogPath))
				{
					FileInfo fi = new FileInfo(AutomationLogPath);
					if (fi.Length > AutomationLogMaxBytesBeforeRotate)
					{
						string bak = AutomationLogPath + ".1.bak";
						if (File.Exists(bak))
						{
							File.Delete(bak);
						}
						File.Move(AutomationLogPath, bak);
					}
				}
			}
			catch
			{
			}
			if (!File.Exists(AutomationLogPath))
			{
				File.WriteAllText(AutomationLogPath, "# time[TAB]LEVEL[TAB]row[TAB]email_masked[TAB]message — UTF-8" + Environment.NewLine, Utf8NoBomEnc);
			}
			File.AppendAllText(AutomationLogPath, line + Environment.NewLine, Utf8NoBomEnc);
		}
	}

	private static void ShutdownAutomationLogWriter()
	{
		try
		{
			AutomationLogChannel.Writer.TryComplete();
			if (_automationLogPumpTask != null && !_automationLogPumpTask.Wait(2500))
			{
				string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "\tWARN\t-\t\tGhi log: pump chưa kịp flush hết trong 2,5s.";
				try
				{
					string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
					Directory.CreateDirectory(dataDir);
					WriteAutomationLogLineUnderLock(line);
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
	}

	private static void AppendAutomationLog(string level, int? rowIndex, string email, string message)
	{
		try
		{
			string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
			Directory.CreateDirectory(dataDir);
			string rowPart = rowIndex.HasValue ? "hàng " + (rowIndex.Value + 1) : "-";
			string who = string.IsNullOrEmpty(email) ? "" : MaskEmailForLog(email);
			string msg = (message ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
			if (msg.Length > 2000)
			{
				msg = msg.Substring(0, 1997) + "...";
			}
			string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "\t" + (level ?? "INFO") + "\t" + rowPart + "\t" + who + "\t" + msg;
			EnsureAutomationLogPump();
			if (!AutomationLogChannel.Writer.TryWrite(line))
			{
				WriteAutomationLogLineUnderLock(line);
			}
		}
		catch
		{
		}
	}

	private Task DelayBatchAsync(int millisecondsDelay)
	{
		if (_batchToken.CanBeCanceled)
		{
			return Task.Delay(millisecondsDelay, _batchToken);
		}
		return Task.Delay(millisecondsDelay);
	}

	private Task PageWaitCancellableAsync(IPage page, float totalMs)
	{
		if (page == null || totalMs <= 0f)
		{
			return Task.CompletedTask;
		}
		CancellationToken ct = _batchToken.CanBeCanceled ? _batchToken : CancellationToken.None;
		return PlaywrightWaitHelpers.PageWaitAsync(page, totalMs, ct, _waitSliceMs);
	}

	/// <summary>Chọn tab phù hợp khi có nhiều trang: ưu tiên accounts.google (signin/challenge), rồi myaccount, rồi Gmail, cuối cùng tab cuối hoặc preferred.</summary>
	private static IPage PickPageForFailureScreenshot(IPage preferred, IBrowserContext contextFallback)
	{
		IBrowserContext ctx = contextFallback;
		if (ctx == null && preferred != null)
		{
			try
			{
				ctx = preferred.Context;
			}
			catch
			{
				ctx = null;
			}
		}
		List<IPage> live = new List<IPage>();
		if (ctx != null)
		{
			try
			{
				foreach (IPage p in ctx.Pages)
				{
					if (p == null)
					{
						continue;
					}
					try
					{
						if (!p.IsClosed)
						{
							live.Add(p);
						}
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
		}
		if (live.Count == 0)
		{
			if (preferred == null)
			{
				return null;
			}
			try
			{
				return preferred.IsClosed ? null : preferred;
			}
			catch
			{
				return null;
			}
		}
		if (live.Count == 1)
		{
			return live[0];
		}
		foreach (IPage p in live)
		{
			string u = "";
			try
			{
				u = p.Url ?? "";
			}
			catch
			{
				continue;
			}
			if (u.IndexOf("accounts.google.com", StringComparison.OrdinalIgnoreCase) < 0)
			{
				continue;
			}
			if (u.IndexOf("/signin/", StringComparison.OrdinalIgnoreCase) >= 0 || u.IndexOf("ServiceLogin", StringComparison.OrdinalIgnoreCase) >= 0 || u.IndexOf("challenge", StringComparison.OrdinalIgnoreCase) >= 0 || u.IndexOf("oauth", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return p;
			}
		}
		foreach (IPage p in live)
		{
			string u = "";
			try
			{
				u = p.Url ?? "";
			}
			catch
			{
				continue;
			}
			if (u.IndexOf("accounts.google.com", StringComparison.OrdinalIgnoreCase) >= 0 || u.IndexOf("myaccount.google.com", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return p;
			}
		}
		foreach (IPage p in live)
		{
			string u = "";
			try
			{
				u = p.Url ?? "";
			}
			catch
			{
				continue;
			}
			if (u.IndexOf("mail.google.com", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return p;
			}
		}
		if (preferred != null)
		{
			foreach (IPage p in live)
			{
				if (ReferenceEquals(p, preferred))
				{
					return preferred;
				}
			}
		}
		return live[live.Count - 1];
	}

	private static async Task TryCaptureFailureScreenshotAsync(IPage preferredPage, int rowIndex, string reasonSlug, IBrowserContext contextFallback = null)
	{
		IPage page = PickPageForFailureScreenshot(preferredPage, contextFallback);
		if (page == null)
		{
			return;
		}
		try
		{
			try
			{
				await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions
				{
					Timeout = 3000f
				});
			}
			catch
			{
			}
			string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "screenshots");
			Directory.CreateDirectory(dir);
			string safe = new string((reasonSlug ?? "fail").Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)).ToArray());
			if (string.IsNullOrEmpty(safe))
			{
				safe = "fail";
			}
			string name = safe + "_hang" + (rowIndex + 1) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmssfff") + ".png";
			string full = Path.Combine(dir, name);
			string urlForFull = "";
			try
			{
				urlForFull = page.Url ?? "";
			}
			catch
			{
			}
			bool fullPage = urlForFull.IndexOf("mail.google.com", StringComparison.OrdinalIgnoreCase) < 0;
			await page.ScreenshotAsync(new PageScreenshotOptions
			{
				Path = full,
				FullPage = fullPage,
				Type = ScreenshotType.Png,
				Animations = ScreenshotAnimations.Disabled
			});
			AppendAutomationLog("INFO", rowIndex, null, "Screenshot lỗi: " + full);
		}
		catch (Exception ex)
		{
			AppendAutomationLog("DEBUG", rowIndex, null, "Không chụp screenshot: " + ex.Message);
		}
	}

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

	private static bool TryParseLoginSuccessLogDataLine(string raw, out string email, out string gpmGroupId)
	{
		email = null;
		gpmGroupId = null;
		string line = (raw ?? "").Trim();
		if (line.Length == 0 || line[0] == '#')
		{
			return false;
		}
		int tab1 = line.IndexOf('\t');
		if (tab1 < 0)
		{
			return false;
		}
		int tab2 = line.IndexOf('\t', tab1 + 1);
		if (tab2 < 0)
		{
			return false;
		}
		email = line.Substring(0, tab1).Trim();
		gpmGroupId = line.Substring(tab1 + 1, tab2 - tab1 - 1).Trim();
		return !string.IsNullOrEmpty(email);
	}

	/// <summary>Đọc Data/login_success.log: email đã OK cùng <paramref name="currentGroupId"/> và email đã OK ở nhóm GPM khác (gid khác).</summary>
	private static void LoadLoginSuccessEmailSets(string currentGroupId, out HashSet<string> sameGroupEmails, out HashSet<string> otherGroupEmails)
	{
		sameGroupEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		otherGroupEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			if (string.IsNullOrEmpty(currentGroupId) || !File.Exists(LoginSuccessLogPath))
			{
				return;
			}
			foreach (string raw in File.ReadAllLines(LoginSuccessLogPath, Encoding.UTF8))
			{
				if (!TryParseLoginSuccessLogDataLine(raw, out string email, out string gid))
				{
					continue;
				}
				if (string.IsNullOrEmpty(gid))
				{
					continue;
				}
				if (string.Equals(gid, currentGroupId, StringComparison.Ordinal))
				{
					sameGroupEmails.Add(email);
				}
				else
				{
					otherGroupEmails.Add(email);
				}
			}
		}
		catch
		{
		}
	}

	/// <summary>Email (cột 2 trong dòng time[TAB]email[TAB]reason) đã có trong bất kỳ log tài khoản chết nào — khi Bắt đầu sẽ bỏ qua.</summary>
	private static HashSet<string> LoadEmailsMarkedDeadInAnyDeadLog()
	{
		HashSet<string> set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		string[] paths = new string[3]
		{
			DeadRecaptchaVerifyLogPath,
			DeadGoogleRestrictionsLogPath,
			DeadGoogleAccountDisabledLogPath
		};
		for (int pi = 0; pi < paths.Length; pi++)
		{
			try
			{
				if (!File.Exists(paths[pi]))
				{
					continue;
				}
				foreach (string raw in File.ReadAllLines(paths[pi], Encoding.UTF8))
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
					string email = line.Substring(tab1 + 1, tab2 - tab1 - 1).Trim();
					if (!string.IsNullOrEmpty(email))
					{
						set.Add(email);
					}
				}
			}
			catch
			{
			}
		}
		return set;
	}

	/// <summary>Ghi email bị coi chết do màn Google reCAPTCHA / Verify (một dòng một lần phát hiện).</summary>
	private static void AppendDeadRecaptchaVerifyAccountLine(string email)
	{
		try
		{
			string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
			Directory.CreateDirectory(dataDir);
			string e = (email ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
			if (string.IsNullOrEmpty(e))
			{
				return;
			}
			string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "\t" + e + "\tGoogle reCAPTCHA/Verify — tài khoản chết (không tự động được)";
			lock (DeadRecaptchaVerifyLogSync)
			{
				if (!File.Exists(DeadRecaptchaVerifyLogPath))
				{
					File.WriteAllText(DeadRecaptchaVerifyLogPath, "# time[TAB]email[TAB]reason — UTF-8 (chỉ ghi khi phát hiện reCAPTCHA/Verify)" + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				}
				File.AppendAllText(DeadRecaptchaVerifyLogPath, line + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			}
		}
		catch
		{
		}
	}

	/// <summary>Ghi email bị redirect Gmail → myaccount.google.com/.../restrictions (coi mail chết).</summary>
	private static void AppendGoogleRestrictionsAccountDeadLine(string email)
	{
		try
		{
			string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
			Directory.CreateDirectory(dataDir);
			string e = (email ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
			if (string.IsNullOrEmpty(e))
			{
				return;
			}
			string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "\t" + e + "\tGmail redirect → myaccount/restrictions — tài khoản chết";
			lock (DeadGoogleRestrictionsLogSync)
			{
				if (!File.Exists(DeadGoogleRestrictionsLogPath))
				{
					File.WriteAllText(DeadGoogleRestrictionsLogPath, "# time[TAB]email[TAB]reason — UTF-8" + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				}
				File.AppendAllText(DeadGoogleRestrictionsLogPath, line + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			}
		}
		catch
		{
		}
	}

	/// <summary>Ghi email khi Google hiện màn «Account disabled» (tài khoản bị khóa).</summary>
	private static void AppendGoogleAccountDisabledDeadLine(string email)
	{
		try
		{
			string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
			Directory.CreateDirectory(dataDir);
			string e = (email ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
			if (string.IsNullOrEmpty(e))
			{
				return;
			}
			string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "\t" + e + "\tGoogle Account disabled — tài khoản chết";
			lock (DeadGoogleAccountDisabledLogSync)
			{
				if (!File.Exists(DeadGoogleAccountDisabledLogPath))
				{
					File.WriteAllText(DeadGoogleAccountDisabledLogPath, "# time[TAB]email[TAB]reason — UTF-8" + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				}
				File.AppendAllText(DeadGoogleAccountDisabledLogPath, line + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			}
		}
		catch
		{
		}
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
					File.WriteAllText(LoginSuccessLogPath, "# account[TAB]gpm_group_id[TAB]proxy_raw — bỏ qua UID trùng khi chạy lại cùng nhóm; bỏ qua nếu đã OK ở nhóm khác" + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				}
				File.AppendAllText(LoginSuccessLogPath, line + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			}
		}
		catch
		{
		}
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
		using HttpClient client = new HttpClient();
		await LoadProfiles(client);
		int maxRow = accountQueue.Max(a => a.rowIndex);
		if (maxRow >= _profileIds.Count)
		{
			MessageBox.Show($"Không đủ profile GPM trong nhóm đã chọn: hàng account lớn nhất là {maxRow + 1}, cần ít nhất {maxRow + 1} profile, hiện có {_profileIds.Count}.\r\nNhóm: {_gpmLoadedGroupSummary}\r\nĐặt tên profile A–Z trong GPM để thứ tự API khớp hàng trên lưới (sort=2).");
			return;
		}
		// Luôn gán raw_proxy từ cột PROXY lên profile GPM (theo thứ tự hàng); ô trống thì bỏ qua — không phụ thuộc checkbox.
		// Checkbox chỉ bắt buộc mỗi account trong hàng đợi phải có PROXY hợp lệ (btnStart).
		try
		{
			await ApplyProxiesToGpmProfilesAsync(client);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Lỗi đồng bộ proxy lên GPM:\n" + ex.Message);
			return;
		}
		for (int offset = 0; offset < accountQueue.Count && _running && !_batchToken.IsCancellationRequested; offset += luongPerBatch)
		{
			int n = Math.Min(luongPerBatch, accountQueue.Count - offset);
			List<(string uid, string pass, string ma2fa, string mail2, int rowIndex)> slice = accountQueue.GetRange(offset, n);
			List<(string uid, string pass, string ma2fa, string mail2, int rowIndex)> runSlice = await LaunchBrowserBatchAsync(client, slice);
			if (runSlice.Count == 0)
			{
				AppendAutomationLog("WARN", null, null, "Batch " + (offset + 1) + "–" + (offset + slice.Count) + ": không profile nào vượt kiểm tra chrome://version (proxy khớp lưới).");
				continue;
			}
			await RunBatchSliceAsync(runSlice);
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
			if (!_running || _batchToken.IsCancellationRequested)
			{
				break;
			}
			try
			{
				await semaphore.WaitAsync(_batchToken.CanBeCanceled ? _batchToken : CancellationToken.None);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			var account = slice[i];
			IBrowser browser = _browsers[i];
			Task task = ProcessAccount(semaphore, browser, (account.uid, account.pass, account.ma2fa, account.mail2), account.rowIndex);
			tasks.Add(task);
		}
		if (tasks.Count > 0)
		{
			await Task.WhenAll(tasks);
		}
	}

	private async Task ProcessAccount(SemaphoreSlim semaphore, IBrowser browser, (string uid, string pass, string ma2fa, string mail2) account, int currentRowIndex)
	{
		Interlocked.Increment(ref _runningThreads);
		UpdateStatus();
		try
		{
			await RunOneCycle(browser, account.uid, account.pass, account.ma2fa, account.mail2, currentRowIndex);
		}
		catch (OperationCanceledException)
		{
			AppendAutomationLog("INFO", currentRowIndex, account.uid, "Đã dừng theo yêu cầu (hủy tác vụ).");
		}
		catch (Exception ex)
		{
			AppendAutomationLog("ERROR", currentRowIndex, account.uid, "ProcessAccount: " + ex.GetType().Name + " — " + ex.Message);
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
		IPage page2 = null;
		try
		{
			Invoke(delegate
			{
				dataGridView1.Rows[rowIndex].DefaultCellStyle.BackColor = Color.DarkRed;
			});
			ProxyInfo proxyInfo = GetProxyForAccountRowOnUi(rowIndex);
			context = browser.Contexts.First();
			IPage page = ((context.Pages.Count <= 0) ? (await context.NewPageAsync()) : context.Pages.First());
			page2 = page;
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
				AppendAutomationLog("WARN", rowIndex, email, "Trang accounts.google báo lỗi mạng/proxy (Chrome error page).");
				Interlocked.Increment(ref _batchFail);
				UpdateStatus();
				return;
			}
			if (await hamcheckpass(rowIndex, context, page2, email, password, cookie, filename))
			{
				SetText(rowIndex, "STATUS", "Xong");
				AppendLoginSuccessLine(email, GetGpmGroupIdForLoginLog(), proxyInfo?.RawLineForGpm ?? "");
				AppendAutomationLog("INFO", rowIndex, email, "Hoàn tất chu trình (hamcheckpass OK).");
				Interlocked.Increment(ref _batchOk);
				UpdateStatus();
			}
			else
			{
				AppendAutomationLog("WARN", rowIndex, email, "hamcheckpass trả về false — kiểm tra cột STATUS và Data/screenshots.");
				try
				{
					await TryCaptureFailureScreenshotAsync(page2, rowIndex, "login_flow_fail", context);
				}
				catch
				{
				}
				Interlocked.Increment(ref _batchFail);
				UpdateStatus();
			}
		}
		catch (OperationCanceledException)
		{
			AppendAutomationLog("INFO", rowIndex, email, "Đã dừng theo yêu cầu (Dừng).");
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Console.WriteLine("Error " + email + ": " + ex2.Message);
			AppendAutomationLog("ERROR", rowIndex, email, "Exception: " + ex2.GetType().Name + " — " + ex2.Message);
			Interlocked.Increment(ref _batchFail);
			UpdateStatus();
			try
			{
				await TryCaptureFailureScreenshotAsync(page2, rowIndex, "exception", context);
			}
			catch
			{
			}
		}
		finally
		{
			bool forceCloseChrome = false;
			try
			{
				forceCloseChrome = _forceCloseChromeAfterCycle.TryRemove(rowIndex, out bool fc) && fc;
			}
			catch
			{
			}
			bool closeChromeAfterAccount = cb_offchrome.Checked || forceCloseChrome;
			try
			{
				if (context != null && !closeChromeAfterAccount)
				{
					IPage p = context.Pages.Count > 0 ? context.Pages[0] : null;
					if (p != null)
					{
						await TryMaximizeChromeAfterCycleAsync(p, rowIndex);
					}
				}
			}
			catch
			{
			}
			if (context != null && closeChromeAfterAccount)
			{
				try
				{
					await context.CloseAsync();
				}
				catch
				{
				}
			}
			if (closeChromeAfterAccount)
			{
				try
				{
					await TryGpmCloseProfileByRowAsync(rowIndex);
				}
				catch
				{
				}
				try
				{
					await browser.CloseAsync();
				}
				catch
				{
				}
			}
			if (forceCloseChrome)
			{
				try
				{
					await DelayBatchAsync(600);
				}
				catch (OperationCanceledException)
				{
				}
				catch
				{
				}
				string deadProfileSnap = null;
				try
				{
					_deadAccountGpmProfileIdByRow.TryGetValue(rowIndex, out deadProfileSnap);
				}
				catch
				{
				}
				try
				{
					await TryGpmDeleteProfileByRowAsync(rowIndex, deadProfileSnap);
				}
				catch
				{
				}
				finally
				{
					try
					{
						_deadAccountGpmProfileIdByRow.TryRemove(rowIndex, out _);
					}
					catch
					{
					}
				}
			}
		}
	}

	private static async Task TryMaximizeChromeAfterCycleAsync(IPage page, int rowIndex)
	{
		if (page == null)
		{
			return;
		}
		try
		{
			await page.BringToFrontAsync();
		}
		catch
		{
		}
		await Task.Delay(280);
		if (ChromeWindowNativeHelper.TryMaximizeChromeWindowByAccountTitle(rowIndex + 1))
		{
			return;
		}
		ChromeWindowNativeHelper.TryMaximizeForegroundIfChromeMain();
	}

	private sealed class GoogleSignInDeadAccountException : Exception
	{
		public GoogleSignInDeadAccountException()
			: base("Google: màn reCAPTCHA/Verify — coi tài khoản chết (không retry).")
		{
		}
	}

	/// <summary>Lấy segment đầu sau .../signin/challenge/ hoặc .../signin/v2/challenge/ (totp, recaptcha, pwd, ...).</summary>
	private static bool TryGetGoogleAccountsChallengeSegment(string url, out string segment)
	{
		segment = null;
		if (string.IsNullOrEmpty(url))
		{
			return false;
		}
		string[] markers = new string[2] { "/signin/v2/challenge/", "/signin/challenge/" };
		for (int m = 0; m < markers.Length; m++)
		{
			string mk = markers[m];
			int i = url.IndexOf(mk, StringComparison.OrdinalIgnoreCase);
			if (i < 0)
			{
				continue;
			}
			string tail = url.Substring(i + mk.Length);
			int cut = tail.IndexOfAny(new char[3] { '?', '&', '#' });
			if (cut >= 0)
			{
				tail = tail.Substring(0, cut);
			}
			int slash = tail.IndexOf('/');
			string seg = (slash >= 0 ? tail.Substring(0, slash) : tail).Trim();
			if (seg.Length > 0)
			{
				segment = seg;
				return true;
			}
		}
		return false;
	}

	/// <summary>Đang ở bước challenge hợp lệ (2FA, chọn cách verify, nhập lại MK, ...) — không coi là mail chết.</summary>
	private static bool GoogleAccountsUrlIsNonRecaptchaChallenge(string url)
	{
		if (string.IsNullOrEmpty(url) || url.IndexOf("accounts.google.com", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return false;
		}
		if (!TryGetGoogleAccountsChallengeSegment(url, out string seg))
		{
			return false;
		}
		return !seg.Equals("recaptcha", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>Chỉ coi mail chết khi thật sự kẹt màn reCAPTCHA (URL) hoặc banner quá nhiều lần thử — không dùng heuristics HTML chung (tránh dương tính giả ở 2FA).</summary>
	private static async Task<bool> PageShowsGoogleSignInRecaptchaDeadEndAsync(IPage page)
	{
		if (page == null)
		{
			return false;
		}
		try
		{
			string url = "";
			try
			{
				url = page.Url ?? "";
			}
			catch
			{
			}
			if (url.IndexOf("accounts.google.com", StringComparison.OrdinalIgnoreCase) < 0)
			{
				return false;
			}
			if (GoogleAccountsUrlIsNonRecaptchaChallenge(url))
			{
				return false;
			}
			if (url.IndexOf("challenge/recaptcha", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			string html = await page.ContentAsync();
			if (string.IsNullOrEmpty(html))
			{
				return false;
			}
			if (html.IndexOf("Too many failed attempts", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			return false;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>Màn "Sign in with Google" lỗi tạm: Something went wrong / Try again — thường gặp khi OAuth Apps Script; có thể retry.</summary>
	private static async Task<bool> PageShowsGoogleSignInSomethingWentWrongAsync(IPage page)
	{
		if (page == null)
		{
			return false;
		}
		try
		{
			string url = "";
			try
			{
				url = page.Url ?? "";
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(url) || url.IndexOf("google.com", StringComparison.OrdinalIgnoreCase) < 0)
			{
				return false;
			}
			string html = await page.ContentAsync();
			if (string.IsNullOrEmpty(html))
			{
				return false;
			}
			if (html.IndexOf("Something went wrong", StringComparison.OrdinalIgnoreCase) < 0)
			{
				return false;
			}
			if (html.IndexOf("Sign in with Google", StringComparison.OrdinalIgnoreCase) < 0 && url.IndexOf("accounts.google", StringComparison.OrdinalIgnoreCase) < 0)
			{
				return false;
			}
			return html.IndexOf("sorry, something went wrong there", StringComparison.OrdinalIgnoreCase) >= 0 || html.IndexOf("Try again", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>Màn đăng nhập Google «Account disabled» (bị khóa / unusual activity, nút Try to restore).</summary>
	private static async Task<bool> PageShowsGoogleAccountDisabledAsync(IPage page)
	{
		if (page == null)
		{
			return false;
		}
		try
		{
			string url = "";
			try
			{
				url = page.Url ?? "";
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(url) || url.IndexOf("accounts.google.com", StringComparison.OrdinalIgnoreCase) < 0)
			{
				return false;
			}
			string html = await page.ContentAsync();
			if (string.IsNullOrEmpty(html))
			{
				return false;
			}
			if (html.IndexOf("Account disabled", StringComparison.OrdinalIgnoreCase) < 0)
			{
				return false;
			}
			if (html.IndexOf("Try to restore", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			if (html.IndexOf("unusual activity", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			if (html.IndexOf("locked it to protect", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			if (html.IndexOf("Your Google Account", StringComparison.OrdinalIgnoreCase) >= 0 && html.IndexOf("disabled", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			return false;
		}
		catch
		{
			return false;
		}
	}

	private async Task<bool> TryAbortIfGoogleAccountDisabledAsync(IPage page, int vitri, string email)
	{
		if (!await PageShowsGoogleAccountDisabledAsync(page))
		{
			return false;
		}
		SetText(vitri, "STATUS", "Tài khoản chết — Google: Account disabled (tài khoản bị khóa)");
		AppendAutomationLog("WARN", vitri, email, "Dừng: màn Account disabled — coi tài khoản chết, đóng Chrome.");
		_forceCloseChromeAfterCycle[vitri] = true;
		RememberGpmProfileIdForDeadAccountRow(vitri);
		AppendGoogleAccountDisabledDeadLine(email);
		try
		{
			await TryCaptureFailureScreenshotAsync(page, vitri, "google_signin_account_disabled");
		}
		catch
		{
		}
		return true;
	}

	/// <summary>reCAPTCHA/Verify chết hoặc Account disabled — dừng luồng đăng nhập.</summary>
	private async Task<bool> TryAbortIfGoogleSignInDeadAccountBlockingAsync(IPage page, int vitri, string email)
	{
		if (await TryAbortIfGoogleAccountDisabledAsync(page, vitri, email))
		{
			return true;
		}
		if (await TryAbortIfGoogleSignInRecaptchaDeadEndAsync(page, vitri, email))
		{
			return true;
		}
		return false;
	}

	private async Task<bool> TryAbortIfGoogleSignInRecaptchaDeadEndAsync(IPage page, int vitri, string email)
	{
		if (!await PageShowsGoogleSignInRecaptchaDeadEndAsync(page))
		{
			return false;
		}
		SetText(vitri, "STATUS", "Tài khoản chết — Google: reCAPTCHA / Verify (không tự động được)");
		AppendAutomationLog("WARN", vitri, email, "Dừng: màn reCAPTCHA / Verify — coi tài khoản chết, đóng Chrome.");
		_forceCloseChromeAfterCycle[vitri] = true;
		RememberGpmProfileIdForDeadAccountRow(vitri);
		AppendDeadRecaptchaVerifyAccountLine(email);
		try
		{
			await TryCaptureFailureScreenshotAsync(page, vitri, "google_signin_recaptcha_dead");
		}
		catch
		{
		}
		return true;
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

	/// <summary>Heuristic: lỗi có vẻ do giới hạn / throttle (Google, HTTP 429, v.v.) → được F5 thêm 1 lần so với lỗi thường.</summary>
	private static bool LooksLikeRateLimitOrGoogleThrottle(Exception ex)
	{
		for (Exception e = ex; e != null; e = e.InnerException)
		{
			string m = (e.Message ?? "").ToLowerInvariant();
			if (m.Contains("429") || m.Contains("503") || m.Contains("502"))
			{
				return true;
			}
			if (m.Contains("limit") || m.Contains("quota") || m.Contains("rate limit") || m.Contains("too many requests"))
			{
				return true;
			}
			if (m.Contains("try again later") || m.Contains("temporarily unavailable") || m.Contains("service unavailable"))
			{
				return true;
			}
			if (m.Contains("unusual traffic") || m.Contains("captcha") || m.Contains("automated"))
			{
				return true;
			}
			if (m.Contains("giới hạn") || m.Contains("thử lại sau"))
			{
				return true;
			}
		}
		return false;
	}

	private static async Task<bool> PageContentLooksLikeGoogleLimitOnPageAsync(IPage targetPage)
	{
		try
		{
			string html = await targetPage.ContentAsync();
			if (string.IsNullOrEmpty(html))
			{
				return false;
			}
			string h = html.ToLowerInvariant();
			if (h.Contains("unusual traffic") || h.Contains("automated queries"))
			{
				return true;
			}
			if (h.Contains("sorry") && h.Contains("cannot") && h.Contains("sign"))
			{
				return true;
			}
			if (h.Contains("too many") && (h.Contains("sign") || h.Contains("verify") || h.Contains("attempt")))
			{
				return true;
			}
			if (h.Contains("try again later") && (h.Contains("google") || h.Contains("account")))
			{
				return true;
			}
			if (h.Contains("đã vượt quá") || h.Contains("thử lại sau") || h.Contains("quá nhiều"))
			{
				return true;
			}
			return false;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>Execution log Apps Script: "This project requires access to your Google Account to run…"</summary>
	private static async Task<bool> ScriptPageShowsExecutionLogAccountAccessWarningAsync(IPage scriptPage)
	{
		try
		{
			string html = await scriptPage.ContentAsync();
			if (string.IsNullOrEmpty(html))
			{
				return false;
			}
			if (html.IndexOf("requires access to your Google Account", StringComparison.OrdinalIgnoreCase) < 0)
			{
				return false;
			}
			if (html.IndexOf("Please try again and allow it", StringComparison.OrdinalIgnoreCase) < 0 && html.IndexOf("allow it this time", StringComparison.OrdinalIgnoreCase) < 0)
			{
				return false;
			}
			string url = scriptPage.Url ?? "";
			return url.IndexOf("script.google.com", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>Execution log Apps Script: "Attempted to execute ..., but it was deleted."</summary>
	private static async Task<bool> ScriptPageShowsExecutionLogDeletedFunctionErrorAsync(IPage scriptPage)
	{
		try
		{
			string html = await scriptPage.ContentAsync();
			if (string.IsNullOrEmpty(html))
			{
				return false;
			}
			if (html.IndexOf("Attempted to execute", StringComparison.OrdinalIgnoreCase) < 0)
			{
				return false;
			}
			if (html.IndexOf("but it was deleted", StringComparison.OrdinalIgnoreCase) < 0)
			{
				return false;
			}
			string url = scriptPage.Url ?? "";
			return url.IndexOf("script.google.com", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		catch
		{
			return false;
		}
	}

	private static async Task<bool> ScriptPageShowsAuthorizationRequiredDialogAsync(IPage scriptPage)
	{
		try
		{
			ILocator dlg = scriptPage.Locator("h2:has-text('Authorization required')").Or(scriptPage.Locator("span.UywwFc-vQzf8d:has-text('Review permissions')")).First;
			return await dlg.IsVisibleAsync();
		}
		catch
		{
			return false;
		}
	}

	private static async Task<bool> WaitForAccountAccessWarningAsync(IPage scriptPage, int timeoutMs = 8000, int pollMs = 400)
	{
		DateTime endAt = DateTime.UtcNow.AddMilliseconds(timeoutMs);
		while (DateTime.UtcNow < endAt)
		{
			if (await ScriptPageShowsExecutionLogAccountAccessWarningAsync(scriptPage))
			{
				return true;
			}
			await Task.Delay(pollMs);
		}
		return await ScriptPageShowsExecutionLogAccountAccessWarningAsync(scriptPage);
	}

	/// <summary>Khi Execution log báo thiếu quyền tài khoản: F5 (reload) trang editor rồi Run 2 lần; lặp tối đa maxAttempts.</summary>
	private async Task RunScriptEditorTwiceWithReloadOnAccountAccessWarningAsync(IPage scriptPage, int vitri, string email, bool reloadImmediately = false, int maxAttempts = 4)
	{
		int authDialogSeenConsecutive = 0;
		for (int attempt = 0; attempt < maxAttempts; attempt++)
		{
			if (attempt > 0 || reloadImmediately)
			{
				SetText(vitri, "STATUS", "[Script] Execution log: cần quyền Google Account — F5, chờ editor (" + (attempt + 1) + "/" + maxAttempts + ")...");
				AppendAutomationLog("WARN", vitri, email, "[Script] Reload (F5) editor sau cảnh báo Execution log — chạy lại Run.");
				await scriptPage.ReloadAsync(new PageReloadOptions
				{
					WaitUntil = WaitUntilState.DOMContentLoaded,
					Timeout = 120000f
				});
				await DelayBatchAsync(3500);
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
				await DelayBatchAsync(2000);
				try
				{
					await scriptPage.SetViewportSizeAsync(1920, 1080);
				}
				catch
				{
				}
				await scriptPage.WaitForSelectorAsync(".view-lines", new PageWaitForSelectorOptions
				{
					Timeout = 180000f
				});
				await DelayBatchAsync(1500);
			}
			SetText(vitri, "STATUS", "[Script] Editor sẵn sàng → Run function (lần 1)...");
			await ClickRunSelectedFunctionAsync(scriptPage, vitri, "lần 1");
			await WaitForRunCycleAsync(scriptPage, vitri, "lần 1");
			if (await ScriptPageShowsExecutionLogDeletedFunctionErrorAsync(scriptPage))
			{
				authDialogSeenConsecutive = 0;
				SetText(vitri, "STATUS", "[Script] Execution log báo '...but it was deleted' sau lần 1 → F5 và Run lại...");
				AppendAutomationLog("WARN", vitri, email, "[Script] Lỗi deleted function sau Run lần 1 — F5 + Run lại.");
				continue;
			}
			if (await ScriptPageShowsAuthorizationRequiredDialogAsync(scriptPage))
			{
				authDialogSeenConsecutive++;
				if (authDialogSeenConsecutive <= 1 && attempt + 1 < maxAttempts)
				{
					SetText(vitri, "STATUS", "[Script] Đã hiện Authorization required sau lần 1 → F5 + Run thêm 1 vòng trước khi vào OAuth...");
					AppendAutomationLog("WARN", vitri, email, "[Script] Authorization required sau Run lần 1 — chạy thêm 1 vòng F5 + Run.");
					continue;
				}
				SetText(vitri, "STATUS", "[Script] Authorization required vẫn còn sau khi đã F5 + Run thêm vòng → chuyển bước OAuth.");
				return;
			}
			authDialogSeenConsecutive = 0;
			bool stillNeedAccountAccessAfterRun1 = await WaitForAccountAccessWarningAsync(scriptPage);
			if (!stillNeedAccountAccessAfterRun1)
			{
				SetText(vitri, "STATUS", "[Script] Run function (lần 1) đã hoàn tất, không còn warning account access.");
				return;
			}
			SetText(vitri, "STATUS", "[Script] Run function (lần 1) còn warning account access → F5 và Run lại...");
			AppendAutomationLog("WARN", vitri, email, "[Script] Warning account access sau Run lần 1 — F5 + Run lại.");
			continue;
		}
		throw new InvalidOperationException("Apps Script: Execution log vẫn báo cần quyền Google Account sau " + maxAttempts + " lần (F5 + Run).");
	}

	private static async Task ClickRunSelectedFunctionAsync(IPage scriptPage, int vitri, string lan)
	{
		ILocator runBtn = scriptPage.Locator("button[aria-label='Run the selected function']").First;
		await runBtn.WaitForAsync(new LocatorWaitForOptions
		{
			State = WaitForSelectorState.Visible,
			Timeout = 45000f
		});
		DateTime waitEnabledUntil = DateTime.UtcNow.AddSeconds(20.0);
		while (DateTime.UtcNow < waitEnabledUntil)
		{
			try
			{
				if (!await runBtn.IsDisabledAsync())
				{
					break;
				}
			}
			catch
			{
			}
			await Task.Delay(200);
		}
		bool clicked = false;
		try
		{
			await runBtn.ClickAsync(new LocatorClickOptions
			{
				Timeout = 15000f
			});
			clicked = true;
		}
		catch
		{
		}
		if (!clicked)
		{
			try
			{
				await runBtn.ClickAsync(new LocatorClickOptions
				{
					Timeout = 15000f,
					Force = true
				});
				clicked = true;
			}
			catch
			{
			}
		}
		if (!clicked)
		{
			clicked = await scriptPage.EvaluateAsync<bool>("() => { const btn = document.querySelector(\"button[aria-label='Run the selected function']\"); if (!btn) return false; btn.click(); return true; }");
		}
		if (!clicked)
		{
			throw new InvalidOperationException("[Script] Không click được nút Run the selected function (" + lan + ").");
		}
		ILocator stopBtn = scriptPage.Locator("button[aria-label='Stop the execution']").First;
		bool runStarted = false;
		DateTime waitStartUntil = DateTime.UtcNow.AddSeconds(8.0);
		while (DateTime.UtcNow < waitStartUntil)
		{
			try
			{
				if (await runBtn.IsDisabledAsync())
				{
					runStarted = true;
					break;
				}
			}
			catch
			{
			}
			try
			{
				if (await stopBtn.IsVisibleAsync() && !await stopBtn.IsDisabledAsync())
				{
					runStarted = true;
					break;
				}
			}
			catch
			{
			}
			await Task.Delay(200);
		}
		if (!runStarted)
		{
			throw new InvalidOperationException("[Script] Đã click Run (" + lan + ") nhưng không thấy trạng thái chạy bắt đầu.");
		}
	}

	private async Task WaitForRunCycleAsync(IPage scriptPage, int vitri, string lan)
	{
		ILocator runBtn = scriptPage.Locator("button[aria-label='Run the selected function']").First;
		ILocator stopBtn = scriptPage.Locator("button[aria-label='Stop the execution']").First;
		ILocator oauthDialog = scriptPage.Locator("h2:has-text('Authorization required')").Or(scriptPage.Locator("span.UywwFc-vQzf8d:has-text('Review permissions')")).First;
		bool sawRunStarted = false;
		DateTime endAt = DateTime.UtcNow.AddSeconds(45.0);
		while (DateTime.UtcNow < endAt)
		{
			try
			{
				if (await oauthDialog.IsVisibleAsync())
				{
					return;
				}
			}
			catch
			{
			}
			try
			{
				bool runDisabled = await runBtn.IsDisabledAsync();
				if (runDisabled)
				{
					sawRunStarted = true;
				}
				else if (sawRunStarted)
				{
					bool stopVisible = false;
					bool stopDisabled = true;
					try
					{
						stopVisible = await stopBtn.IsVisibleAsync();
						stopDisabled = await stopBtn.IsDisabledAsync();
					}
					catch
					{
					}
					if (!stopVisible || stopDisabled)
					{
						return;
					}
				}
			}
			catch
			{
			}
			try
			{
				bool stopVisibleNow = await stopBtn.IsVisibleAsync();
				bool stopDisabledNow = true;
				try
				{
					stopDisabledNow = await stopBtn.IsDisabledAsync();
				}
				catch
				{
				}
				if (stopVisibleNow && !stopDisabledNow)
				{
					sawRunStarted = true;
				}
			}
			catch
			{
			}
			// Chỉ xét warning account access sau khi thấy run thật sự bắt đầu,
			// tránh ăn phải warning cũ còn lưu trong execution log rồi nhảy sang lần 2 quá sớm.
			if (sawRunStarted)
			{
				try
				{
					if (await ScriptPageShowsExecutionLogAccountAccessWarningAsync(scriptPage))
					{
						return;
					}
				}
				catch
				{
				}
			}
			await DelayBatchAsync(400);
		}
		// Fallback: giữ hành vi chờ pause cũ để không thoát quá sớm khi UI không phản hồi trạng thái nút.
		await PageWaitCancellableAsync(scriptPage, _scriptRunPauseMs);
	}

	/// <summary>Lỗi ở một bước: reload (F5), tùy chọn khôi phục; thử lại 1 lần. Nếu lỗi giống limit/throttle (message hoặc HTML trang) thì thử thêm 1 lần nữa (tối đa 3 lần chạy step).</summary>
	private async Task RunStepWithReloadRetryAsync(IPage targetPage, int vitri, string stepLabel, Func<Task> step, Func<Task> afterReloadAsync = null)
	{
		int maxAttempts = 2;
		for (int attempt = 0; ; attempt++)
		{
			try
			{
				await step();
				return;
			}
			catch (Exception ex)
			{
				if (ex is GoogleSignInDeadAccountException)
				{
					throw;
				}
				if (maxAttempts < 3)
				{
					if (LooksLikeRateLimitOrGoogleThrottle(ex))
					{
						maxAttempts = 3;
					}
					else
					{
						try
						{
							if (await PageContentLooksLikeGoogleLimitOnPageAsync(targetPage))
							{
								maxAttempts = 3;
							}
						}
						catch
						{
						}
					}
				}
				if (attempt + 1 >= maxAttempts)
				{
					throw;
				}
				try
				{
					bool limitish = maxAttempts >= 3;
					string limitNote = limitish ? " [limit/throttle → tối đa 3 lần chạy bước]" : "";
					SetText(vitri, "STATUS", stepLabel + " — lỗi lần " + (attempt + 1) + "/" + maxAttempts + limitNote + ", F5 rồi thử lại: " + ex.Message);
				}
				catch
				{
				}
				try
				{
					await targetPage.ReloadAsync(new PageReloadOptions
					{
						WaitUntil = WaitUntilState.DOMContentLoaded,
						Timeout = 90000f
					});
				}
				catch
				{
					try
					{
						await targetPage.Keyboard.PressAsync("F5");
						await targetPage.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions
						{
							Timeout = 90000f
						});
					}
					catch
					{
					}
				}
				try
				{
					int waitMs = LooksLikeRateLimitOrGoogleThrottle(ex) ? 5000 : 2000;
					await DelayBatchAsync(waitMs);
				}
				catch
				{
				}
				if (afterReloadAsync != null)
				{
					try
					{
						await afterReloadAsync();
					}
					catch
					{
					}
				}
			}
		}
	}

	/// <summary>Sau F5, nếu đang về màn email thì nhập lại email để tới được ô mật khẩu.</summary>
	private static async Task TryRecoverGoogleSignInPasswordAsync(IPage page, string email)
	{
		if (string.IsNullOrEmpty((email ?? "").Trim()))
		{
			return;
		}
		string em = email.Trim();
		try
		{
			ILocator pwd = page.Locator("input[type='password']");
			if (await pwd.CountAsync() > 0)
			{
				try
				{
					if (await pwd.First.IsVisibleAsync())
					{
						return;
					}
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
		try
		{
			ILocator emailBox = page.Locator("input[type='email']");
			if (await emailBox.CountAsync() == 0)
			{
				return;
			}
			await emailBox.First.FillAsync(em);
			await page.ClickAsync("#identifierNext");
			await page.WaitForSelectorAsync("input[type='password']", new PageWaitForSelectorOptions
			{
				Timeout = 25000f
			});
		}
		catch
		{
		}
	}

	private static bool UrlIsGoogleTotpChallengePage(string url)
	{
		return !string.IsNullOrEmpty(url) && url.IndexOf("challenge/totp", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static async Task<bool> PageShowsGoogleTotpWrongCodeAsync(IPage page)
	{
		try
		{
			ILocator invalidPin = page.Locator("input[name='totpPin'][aria-invalid='true']");
			if (await invalidPin.CountAsync() > 0)
			{
				try
				{
					if (await invalidPin.First.IsVisibleAsync())
					{
						return true;
					}
				}
				catch
				{
				}
			}
			ILocator alerts = page.Locator("[role='alert']");
			int ac = await alerts.CountAsync();
			for (int i = 0; i < ac && i < 6; i++)
			{
				try
				{
					string t = await alerts.Nth(i).InnerTextAsync();
					if (string.IsNullOrEmpty(t))
					{
						continue;
					}
					string tl = t.ToLowerInvariant();
					if ((tl.Contains("wrong") && tl.Contains("code")) || tl.Contains("incorrect code") || tl.Contains("didn't work") || tl.Contains("didn’t work"))
					{
						return true;
					}
					if (tl.Contains("mã") && (tl.Contains("không đúng") || tl.Contains("sai") || tl.Contains("chính xác")))
					{
						return true;
					}
				}
				catch
				{
				}
			}
			string html = await page.ContentAsync();
			if (string.IsNullOrEmpty(html))
			{
				return false;
			}
			if (html.IndexOf("Wrong code", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			if (html.IndexOf("Incorrect code", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			if (html.IndexOf("That code didn", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			if (html.IndexOf("Couldn't sign you in", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			if (html.IndexOf("Couldn\u2019t sign you in", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			if (html.IndexOf("Couldn’t sign you in", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
			if (html.IndexOf("mã không đúng", StringComparison.OrdinalIgnoreCase) >= 0 || html.IndexOf("Mã không chính xác", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	/// <summary>Sau bấm #totpNext: chỉ OK khi URL không còn màn challenge/totp; ngược lại phát hiện banner lỗi hoặc hết thời gian vẫn kẹt totp → false.</summary>
	private async Task<bool> WaitForGoogleTotpSubmitOutcomeSuccessAsync(IPage page, int vitri)
	{
		const int stepMs = 450;
		const int maxWaitMs = 32000;
		int elapsed = 0;
		while (elapsed < maxWaitMs)
		{
			string url = "";
			try
			{
				url = page.Url ?? "";
			}
			catch
			{
			}
			if (!UrlIsGoogleTotpChallengePage(url))
			{
				return true;
			}
			if (await PageShowsGoogleTotpWrongCodeAsync(page))
			{
				try
				{
					SetText(vitri, "STATUS", "STEP 3: Google báo mã 2FA sai / không hợp lệ");
				}
				catch
				{
				}
				return false;
			}
			await DelayBatchAsync(stepMs);
			elapsed += stepMs;
		}
		try
		{
			string u2 = page.Url ?? "";
			if (UrlIsGoogleTotpChallengePage(u2))
			{
				try
				{
					SetText(vitri, "STATUS", "STEP 3: Vẫn ở màn 2FA sau khi submit — coi là thất bại (mã sai hoặc chưa xử lý xong)");
				}
				catch
				{
				}
				return false;
			}
		}
		catch
		{
		}
		return true;
	}

	/// <summary>
	/// Sau 2FA (hoặc verify xong): Google có thể đưa speedbump passkey &quot;Sign in faster&quot;.
	/// Chờ tối đa vài giây rồi bấm &quot;Not now&quot; và chờ rời URL passkeyenrollment.
	/// </summary>
	private async Task TryDismissGooglePasskeyAfterVerifyAsync(IPage page, int vitri, string statusPrefix)
	{
		DateTime t0 = DateTime.Now;
		bool onPasskey = false;
		while ((DateTime.Now - t0).TotalSeconds < 22.0)
		{
			string u = "";
			try
			{
				u = page.Url ?? "";
			}
			catch
			{
			}
			if (!string.IsNullOrEmpty(u) && u.IndexOf("speedbump/passkeyenrollment", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				onPasskey = true;
				break;
			}
			try
			{
				ILocator h1 = page.Locator("h1#headingText, h1[jsname='r4nke']");
				if (await h1.CountAsync() > 0)
				{
					string tx = await h1.First.InnerTextAsync();
					if (!string.IsNullOrEmpty(tx) && tx.Trim().IndexOf("Sign in faster", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						onPasskey = true;
						break;
					}
				}
			}
			catch
			{
			}
			await DelayBatchAsync(400);
		}
		if (!onPasskey)
		{
			return;
		}
		try
		{
			SetText(vitri, "STATUS", statusPrefix + ": Gặp Sign in faster — bấm Not now...");
		}
		catch
		{
		}
		try
		{
			await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
		}
		catch
		{
		}
		ILocator notNowBtn = page.Locator("[data-secondary-action-label='Not now'] div[jsname='eBSUOb'] button[jsname='LgbsSe']");
		if (await notNowBtn.CountAsync() == 0)
		{
			notNowBtn = page.Locator("[data-secondary-action-label='Not now'] button[jsname='LgbsSe']:has(span[jsname='V67aGc']:has-text(\"Not now\"))");
		}
		if (await notNowBtn.CountAsync() == 0)
		{
			notNowBtn = page.Locator("[data-secondary-action-label='Not now'] button:has(span[jsname='V67aGc']:has-text(\"Not now\"))");
		}
		if (await notNowBtn.CountAsync() == 0)
		{
			notNowBtn = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions
			{
				Name = "Not now"
			});
		}
		try
		{
			await notNowBtn.First.WaitForAsync(new LocatorWaitForOptions
			{
				State = WaitForSelectorState.Visible,
				Timeout = 12000f
			});
		}
		catch
		{
		}
		bool clicked = false;
		try
		{
			if (await notNowBtn.CountAsync() > 0)
			{
				try
				{
					await notNowBtn.First.ScrollIntoViewIfNeededAsync();
				}
				catch
				{
				}
				await notNowBtn.First.ClickAsync(new LocatorClickOptions
				{
					Timeout = 15000f,
					Force = true
				});
				clicked = true;
			}
		}
		catch
		{
			try
			{
				var box3 = await notNowBtn.First.BoundingBoxAsync();
				if (box3 != null)
				{
					float cx3 = (float)(box3.X + box3.Width / 2.0);
					float cy3 = (float)(box3.Y + box3.Height / 2.0);
					await page.Mouse.ClickAsync(cx3, cy3);
					clicked = true;
				}
			}
			catch
			{
			}
		}
		if (!clicked)
		{
			try
			{
				clicked = await page.EvaluateAsync<bool>(
					@"() => {
					  const spans = Array.from(document.querySelectorAll('span[jsname=""V67aGc""]'));
					  const s = spans.find(x => (x.textContent || '').trim() === 'Not now');
					  if (!s) return false;
					  const btn = s.closest('button');
					  if (!btn) return false;
					  btn.click();
					  return true;
					}");
			}
			catch
			{
			}
		}
		try
		{
			await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
		}
		catch
		{
		}
		try
		{
			await page.WaitForURLAsync(new Regex("^(?!.*speedbump/passkeyenrollment).*$", RegexOptions.IgnoreCase), new PageWaitForURLOptions
			{
				Timeout = 15000f
			});
		}
		catch
		{
		}
		try
		{
			await DelayBatchAsync(800);
		}
		catch
		{
		}
		try
		{
			SetText(vitri, "STATUS", statusPrefix + ": Đã bấm Not now (Sign in faster)");
		}
		catch
		{
		}
	}

	private static bool UrlLooksLikeGoogleMyAccountRestrictions(string url)
	{
		if (string.IsNullOrEmpty(url))
		{
			return false;
		}
		if (url.IndexOf("myaccount.google.com", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return false;
		}
		return url.IndexOf("/restrictions", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	/// <summary>Sau khi mở tab Gmail: nếu redirect sang myaccount/restrictions thì coi mail chết (log + force đóng Chrome).</summary>
	private async Task<bool> TryAbortIfInboxRedirectedToGoogleRestrictionsAsync(IPage inbox, int vitri, string email)
	{
		if (inbox == null)
		{
			return false;
		}
		for (int i = 0; i < 45; i++)
		{
			string u = "";
			try
			{
				u = inbox.Url ?? "";
			}
			catch
			{
			}
			if (UrlLooksLikeGoogleMyAccountRestrictions(u))
			{
				SetText(vitri, "STATUS", "Tài khoản chết — Google Restrictions (myaccount…/restrictions)");
				AppendAutomationLog("WARN", vitri, email, "Dừng: mở Gmail chuyển sang myaccount.google.com/.../restrictions — coi tài khoản chết.");
				_forceCloseChromeAfterCycle[vitri] = true;
				RememberGpmProfileIdForDeadAccountRow(vitri);
				AppendGoogleRestrictionsAccountDeadLine(email);
				try
				{
					await TryCaptureFailureScreenshotAsync(inbox, vitri, "google_myaccount_restrictions");
				}
				catch
				{
				}
				return true;
			}
			if (u.IndexOf("mail.google.com", StringComparison.OrdinalIgnoreCase) >= 0 && !UrlLooksLikeGoogleMyAccountRestrictions(u))
			{
				return false;
			}
			await DelayBatchAsync(500);
		}
		try
		{
			string u2 = inbox.Url ?? "";
			if (UrlLooksLikeGoogleMyAccountRestrictions(u2))
			{
				SetText(vitri, "STATUS", "Tài khoản chết — Google Restrictions (myaccount…/restrictions)");
				AppendAutomationLog("WARN", vitri, email, "Dừng: mở Gmail chuyển sang myaccount.google.com/.../restrictions — coi tài khoản chết.");
				_forceCloseChromeAfterCycle[vitri] = true;
				RememberGpmProfileIdForDeadAccountRow(vitri);
				AppendGoogleRestrictionsAccountDeadLine(email);
				try
				{
					await TryCaptureFailureScreenshotAsync(inbox, vitri, "google_myaccount_restrictions");
				}
				catch
				{
				}
				return true;
			}
		}
		catch
		{
		}
		return false;
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
					await RunStepWithReloadRetryAsync(page, vitri, "STEP 1 (email)", async delegate
					{
						await page.WaitForSelectorAsync("input[type='email']", new PageWaitForSelectorOptions
						{
							Timeout = 15000f
						});
						await page.FillAsync("input[type='email']", email);
						SetText(vitri, "STATUS", "STEP 1: Submit email");
						await page.ClickAsync("#identifierNext");
					});
					SetText(vitri, "STATUS", "STEP 2: Nhập password");
					await RunStepWithReloadRetryAsync(page, vitri, "STEP 2 (password)", async delegate
					{
						DateTime deadline = DateTime.UtcNow.AddMilliseconds(15500.0);
						while (DateTime.UtcNow < deadline)
						{
							if (await TryAbortIfGoogleSignInDeadAccountBlockingAsync(page, vitri, email))
							{
								throw new GoogleSignInDeadAccountException();
							}
							ILocator passLoc = page.Locator("input[name='Passwd']");
							if (await passLoc.CountAsync() == 0)
							{
								passLoc = page.Locator("input[type='password']:not([name='hiddenPassword'])");
							}
							try
							{
								await passLoc.First.WaitForAsync(new LocatorWaitForOptions
								{
									State = WaitForSelectorState.Visible,
									Timeout = 500f
								});
								await passLoc.First.FillAsync(password);
								SetText(vitri, "STATUS", "STEP 2: Submit password");
								await page.ClickAsync("#passwordNext");
								return;
							}
							catch
							{
							}
							await DelayBatchAsync(400);
						}
						if (await TryAbortIfGoogleSignInDeadAccountBlockingAsync(page, vitri, email))
						{
							throw new GoogleSignInDeadAccountException();
						}
						throw new TimeoutException("Timeout 15000ms exceeded — không thấy ô mật khẩu hiển thị.");
					}, async delegate
					{
						await TryRecoverGoogleSignInPasswordAsync(page, email);
					});

					try
					{
						await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
					}
					catch
					{
					}
					try
					{
						await DelayBatchAsync(1000);
					}
					catch
					{
					}
					if (await TryAbortIfGoogleSignInDeadAccountBlockingAsync(page, vitri, email))
					{
						return false;
					}

					// STEP 2.5: Nếu Google hiện màn "Sign in faster" (passkey enrollment) thì bấm "Not now"
					// rồi mới xử lý các bước verify (2FA / recovery...) tiếp theo.
					try
					{
						string u0 = "";
						try { u0 = page.Url ?? ""; } catch { }
						if (!string.IsNullOrEmpty(u0) && u0.Contains("speedbump/passkeyenrollment"))
						{
							SetText(vitri, "STATUS", "STEP 2.5: Gặp passkey enrollment — bấm Not now...");
							try { await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded); } catch { }

							ILocator notNow = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Not now" });
							if (await notNow.CountAsync() == 0)
								notNow = page.Locator("button:has-text(\"Not now\")");
							if (await notNow.CountAsync() == 0)
								notNow = page.Locator("span[jsname='V67aGc']:has-text(\"Not now\")");
							if (await notNow.CountAsync() == 0)
								notNow = page.Locator("[data-secondary-action-label='Not now'] button, [data-secondary-action-label='Not now']");

							if (await notNow.CountAsync() > 0)
							{
								try { await notNow.First.ScrollIntoViewIfNeededAsync(); } catch { }
								try
								{
									await notNow.First.ClickAsync(new LocatorClickOptions { Timeout = 15000f, Force = true });
								}
								catch
								{
									try
									{
										var box2 = await notNow.First.BoundingBoxAsync();
										if (box2 != null)
										{
											float cx2 = (float)(box2.X + (box2.Width / 2.0));
											float cy2 = (float)(box2.Y + (box2.Height / 2.0));
											await page.Mouse.ClickAsync(cx2, cy2);
										}
									}
									catch
									{
									}
								}
								try { await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded); } catch { }
								await DelayBatchAsync(1500);
								try { SetText(vitri, "STATUS", "STEP 2.5: Đã bấm Not now"); } catch { }
							}
							else
							{
								try { SetText(vitri, "STATUS", "STEP 2.5 [LOG]: Không thấy nút Not now"); } catch { }
							}
						}
					}
					catch
					{
					}

					// STEP 3: 2FA hoặc chọn "Confirm your recovery email" nếu không có ô totpPin
					SetText(vitri, "STATUS", "STEP 3: Xử lý Verify (2FA / Recovery email)...");
					try
					{
						await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
					}
					catch
					{
					}

					if (await TryAbortIfGoogleSignInDeadAccountBlockingAsync(page, vitri, email))
					{
						return false;
					}

					bool bypassVerifyByPasskey = false;

					// STEP 3.0: Ưu tiên bỏ qua màn "Sign in faster" (passkey enrollment) nếu Google hiện ra ở đây.
					// Thực tế trang này thường chỉ xuất hiện sau khi submit password và URL sẽ đổi sau vài giây,
					// nên cần check + bấm "Not now" ngay đầu bước 3 (không dựa vào check tức thời ở STEP 2.5).
					try
					{
						DateTime waitPasskeyStart = DateTime.Now;
						bool dismissedPasskey = false;
						while ((DateTime.Now - waitPasskeyStart).TotalSeconds < 20.0)
						{
							string uPass = "";
							try { uPass = page.Url ?? ""; } catch { }
							// URL có thể khác nhau theo từng tài khoản (TL/ifkv/dsh...), nên chỉ cần match path passkeyenrollment là đủ.
							if (!string.IsNullOrEmpty(uPass) && (uPass.Contains("/v3/signin/speedbump/passkeyenrollment") || uPass.Contains("speedbump/passkeyenrollment")))
							{
								SetText(vitri, "STATUS", "STEP 3.0: Gặp Sign in faster — bấm Not now...");
								try { await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded); } catch { }

								// Click đúng nút "Not now" (button). Tránh bị nhầm với nút "Continue" (cũng jsname="LgbsSe").
								// Ưu tiên theo container có data-secondary-action-label="Not now", và div jsname="eBSUOb" (khối action phụ).
								ILocator notNowBtn = page.Locator("[data-secondary-action-label='Not now'] div[jsname='eBSUOb'] button[jsname='LgbsSe']");
								if (await notNowBtn.CountAsync() == 0)
									notNowBtn = page.Locator("[data-secondary-action-label='Not now'] button[jsname='LgbsSe']:has(span[jsname='V67aGc']:has-text(\"Not now\"))");
								if (await notNowBtn.CountAsync() == 0)
									notNowBtn = page.Locator("[data-secondary-action-label='Not now'] button:has(span[jsname='V67aGc']:has-text(\"Not now\"))");
								if (await notNowBtn.CountAsync() == 0)
									notNowBtn = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Not now" });
								// log nhanh để biết có tìm thấy đúng nút không
								try { SetText(vitri, "STATUS", "STEP 3.0 [LOG]: notNowBtn.count=" + (await notNowBtn.CountAsync())); } catch { }
								try
								{
									// đợi nút xuất hiện (tối đa 10s) để tránh check CountAsync quá sớm
									await notNowBtn.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000f });
								}
								catch
								{
								}

								// Click thẳng đúng locator đã tìm được. Tránh tự "biến hình" locator gây mất nút.
								bool clicked = false;
								try
								{
									if (await notNowBtn.CountAsync() > 0)
									{
										try { await notNowBtn.First.ScrollIntoViewIfNeededAsync(); } catch { }
										await notNowBtn.First.ClickAsync(new LocatorClickOptions { Timeout = 15000f, Force = true });
										clicked = true;
									}
								}
								catch
								{
									try
									{
										var box3 = await notNowBtn.First.BoundingBoxAsync();
										if (box3 != null)
										{
											float cx3 = (float)(box3.X + (box3.Width / 2.0));
											float cy3 = (float)(box3.Y + (box3.Height / 2.0));
											await page.Mouse.ClickAsync(cx3, cy3);
											clicked = true;
										}
									}
									catch
									{
									}
								}

								// Fallback JS: click đúng button chứa span "Not now"
								if (!clicked)
								{
									try
									{
										bool jsClicked = await page.EvaluateAsync<bool>(
											@"() => {
											  const spans = Array.from(document.querySelectorAll('span[jsname=""V67aGc""]'));
											  const s = spans.find(x => (x.textContent || '').trim() === 'Not now');
											  if (!s) return false;
											  const btn = s.closest('button');
											  if (!btn) return false;
											  btn.click();
											  return true;
											}");
										clicked = jsClicked;
									}
									catch { }
								}

								if (clicked)
								{
									dismissedPasskey = true;
									bypassVerifyByPasskey = true;
								}

								if (dismissedPasskey)
								{
									try { await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded); } catch { }
									await DelayBatchAsync(1500);
									try { SetText(vitri, "STATUS", "STEP 3.0: Đã bấm Not now"); } catch { }
									bypassVerifyByPasskey = true;
								}
								else
								{
									try { SetText(vitri, "STATUS", "STEP 3.0 [LOG]: Click Not now nhưng chưa thoát speedbump"); } catch { }
								}
								break;
							}

							// Không break sớm theo các selector chung chung (vd input[type=email]) vì có thể URL passkeyenrollment chưa kịp load.
							// Chỉ break khi chắc chắn đã qua bước verify khác (totp/challenge) hoặc đã vào myaccount/mail.
							try
							{
								bool hasVerifyUi = (await page.Locator("input[name='totpPin'], div[jsname='EBHGs'][data-action='selectchallenge'], #totpNext").CountAsync() > 0);
								if (hasVerifyUi)
									break;
							}
							catch { }
							try
							{
								string u2 = "";
								try { u2 = page.Url ?? ""; } catch { }
								if (!string.IsNullOrEmpty(u2) && (u2.Contains("myaccount.google.com") || u2.Contains("mail.google.com")))
									break;
							}
							catch { }

							await DelayBatchAsync(500);
						}
					}
					catch
					{
					}

					// Nếu đã dismiss passkey enrollment: bỏ qua hẳn 2FA/Recovery và chạy bước tiếp theo luôn.
					if (bypassVerifyByPasskey)
					{
						try { SetText(vitri, "STATUS", "STEP 3: Bỏ qua 2FA/Recovery (Sign in faster) — qua bước tiếp theo..."); } catch { }
						await DelayBatchAsync(1500);
						try
						{
							// cố gắng chờ thoát speedbump trước khi goto để tránh bị "kẹt" đúng URL passkeyenrollment
							await page.WaitForURLAsync(new Regex("^(?!.*speedbump/passkeyenrollment).*$"), new PageWaitForURLOptions { Timeout = 10000f });
						}
						catch
						{
						}
						try
						{
							await RunStepWithReloadRetryAsync(page, vitri, "Goto myaccount/language (sau passkey)", async delegate
							{
								await page.GotoAsync("https://myaccount.google.com/language");
							});
						}
						catch
						{
						}
						goto AFTER_VERIFY_STEP_3;
					}

					string ma2faTrim = (ma2fa ?? "").Trim();
					string recoveryEmailTrim = (mail2 ?? "").Trim();
					bool verifyDone = false;
					bool switchedToRecoveryEmail = false;
					bool clickedRecoveryChoice = false;

					// Log context để debug việc tìm/click challenge.
					try
					{
						string t = "";
						try { t = await page.TitleAsync(); } catch { }
						SetText(vitri, "STATUS", "STEP 3 [LOG]: url=" + page.Url + " | title=" + t);
					}
					catch
					{
					}

					ILocator totpInput = page.Locator("input[name='totpPin']");
					bool hasTotpInput = await totpInput.CountAsync() > 0;
					try { SetText(vitri, "STATUS", "STEP 3 [LOG]: hasTotpInput=" + hasTotpInput); } catch { }

					// Nếu cột 2FA trống: luôn ưu tiên "Confirm your recovery email" (challenge type 12)
					if (string.IsNullOrEmpty(ma2faTrim))
					{
						SetText(vitri, "STATUS", "STEP 3: Không có 2FA — chọn Recovery email");

						// Chờ UI verify render: hoặc ô totpPin, hoặc danh sách challenge (type 12) xuất hiện.
						try
						{
							await page.WaitForSelectorAsync("input[name='totpPin'], div[jsname='EBHGs'][data-action='selectchallenge']", new PageWaitForSelectorOptions
							{
								Timeout = 20000f
							});
						}
						catch
						{
						}

						// Nếu đang đứng ở màn totpPin thì bấm "Try another way" để về danh sách lựa chọn
						if (hasTotpInput)
						{
							ILocator tryAnotherWay = page.GetByRole(AriaRole.Link, new PageGetByRoleOptions
							{
								Name = "Try another way"
							});
							if (await tryAnotherWay.CountAsync() == 0)
							{
								tryAnotherWay = page.Locator("[data-action='tryAnotherWay'], a:has-text(\"Try another way\"), div[role='link']:has-text(\"Try another way\"), button:has-text(\"Try another way\")");
							}
							if (await tryAnotherWay.CountAsync() > 0)
							{
								try { await tryAnotherWay.First.ClickAsync(); } catch { }
								try { await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded); } catch { }
								try
								{
									await page.WaitForSelectorAsync("div[jsname='EBHGs'][data-action='selectchallenge']", new PageWaitForSelectorOptions
									{
										Timeout = 20000f
									});
								}
								catch
								{
								}
							}
						}

						// Màn "Verify it's you" / "Choose how you want to sign in"
						// Ưu tiên click đúng option challenge type 12: Confirm your recovery email
						ILocator recovery = page.Locator("div.VV3oRb[jsname='EBHGs'][data-action='selectchallenge'][data-challengetype='12']");
						ILocator recovery2 = page.Locator("div[jsname='EBHGs'][data-action='selectchallenge'][data-challengetype='12']");
						ILocator recovery3 = page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Confirm your recovery email" });
						ILocator recovery4 = page.Locator("div[jsname='EBHGs'][role='link']").Filter(new LocatorFilterOptions { HasTextString = "Confirm your recovery email" });

						int c1 = 0, c2 = 0, c3 = 0, c4 = 0;
						try { c1 = await recovery.CountAsync(); } catch { }
						try { c2 = await recovery2.CountAsync(); } catch { }
						try { c3 = await recovery3.CountAsync(); } catch { }
						try { c4 = await recovery4.CountAsync(); } catch { }
						SetText(vitri, "STATUS", "STEP 3 [LOG]: recovery counts => css1=" + c1 + " css2=" + c2 + " role=" + c3 + " textFilter=" + c4);

						if (c1 == 0) recovery = recovery2;
						if ((await recovery.CountAsync()) == 0) recovery = recovery3;
						if ((await recovery.CountAsync()) == 0) recovery = recovery4;
						if (await recovery.CountAsync() > 0)
						{
							SetText(vitri, "STATUS", "STEP 3: Chọn Confirm recovery email");
							try { await recovery.First.ScrollIntoViewIfNeededAsync(); } catch { }
							try
							{
								await recovery.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000f });
							}
							catch
							{
							}
							// Click "cứng": thử force click, nếu fail thì click theo tọa độ bounding box.
							try
							{
								await recovery.First.ClickAsync(new LocatorClickOptions { Timeout = 15000f, Force = true });
								clickedRecoveryChoice = true;
							}
							catch
							{
								try
								{
									var box = await recovery.First.BoundingBoxAsync();
									if (box != null)
									{
										float cx = (float)(box.X + (box.Width / 2.0));
										float cy = (float)(box.Y + (box.Height / 2.0));
										await page.Mouse.ClickAsync(cx, cy);
										clickedRecoveryChoice = true;
									}
								}
								catch
								{
								}
							}
							SetText(vitri, "STATUS", "STEP 3 [LOG]: clickedRecoveryChoice=" + clickedRecoveryChoice);
							switchedToRecoveryEmail = true;

							// Đợi trang/step chuyển sang màn nhập recovery email (để không nhảy sang bước khác)
							try
							{
								// input ở step recovery email thường là email input, có thể khác với identifier.
								ILocator recInput = page.Locator("input[type='email'], input[type='email'][name], input[autocomplete='email']");
								await recInput.First.WaitForAsync(new LocatorWaitForOptions
								{
									State = WaitForSelectorState.Visible,
									Timeout = 15000f
								});

								if (!string.IsNullOrEmpty(recoveryEmailTrim))
								{
									SetText(vitri, "STATUS", "STEP 3: Nhập recovery email");
									await recInput.First.FillAsync(recoveryEmailTrim);
									// Nút Next ở step này thường là "Next"
									ILocator nextBtn = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Next" });
									if (await nextBtn.CountAsync() == 0)
										nextBtn = page.Locator("button:has-text(\"Next\"), #knowledgePreregisteredEmailNext, #next");
									if (await nextBtn.CountAsync() > 0)
										await nextBtn.First.ClickAsync();
								}
							}
							catch
							{
								// ignore: Google UI thay đổi; ít nhất đã click option
							}
						}

						// Khi không có 2FA: chỉ hợp lệ nếu click được recovery choice (type 12).
						// Nếu không tìm/click được thì dừng lại để tránh nhảy sang STEP 4.
						if (!clickedRecoveryChoice)
						{
							try { SetText(vitri, "STATUS", "STEP 3 [LOG]: KHÔNG click được challengetype=12 => BỎ QUA, QUA BƯỚC TIẾP THEO"); } catch { }
							goto AFTER_VERIFY_STEP_3;
						}

						verifyDone = false; // đang ở flow recovery email, chưa verify xong
					}
					else if (hasTotpInput)
					{
						// Có secret 2FA và có ô totpPin: nhập 2FA bình thường
						SetText(vitri, "STATUS", "STEP 3: Nhập mã 2FA");
						await RunStepWithReloadRetryAsync(page, vitri, "STEP 3 (2FA TOTP)", async delegate
						{
							token = await Get2FAToken(ma2faTrim);
							await totpInput.First.WaitForAsync(new LocatorWaitForOptions
							{
								State = WaitForSelectorState.Visible,
								Timeout = 15000f
							});
							await totpInput.First.FillAsync(token);
							SetText(vitri, "STATUS", "STEP 3: Submit 2FA");
							await page.ClickAsync("#totpNext");
						});
						verifyDone = await WaitForGoogleTotpSubmitOutcomeSuccessAsync(page, vitri);
						if (!verifyDone)
						{
							AppendAutomationLog("WARN", vitri, email, "Dừng: mã 2FA không được chấp nhận hoặc vẫn kẹt màn TOTP.");
							SetText(vitri, "STATUS", "Lỗi: Mã 2FA không đúng hoặc Google chưa chấp nhận — kết thúc đăng nhập.");
							return false;
						}
					}
					else
					{
						// Có 2FA nhưng không thấy ô totpPin: có thể đang ở màn "2-Step Verification" / "Choose how you want to sign in"
						// (chưa có input) — cần bấm "Get a verification code from the Google Authenticator app" (data-challengetype="6").
						try
						{
							await page.WaitForSelectorAsync("input[name='totpPin'], div[jsname='EBHGs'][data-action='selectchallenge']", new PageWaitForSelectorOptions
							{
								Timeout = 20000f
							});
						}
						catch
						{
						}

						totpInput = page.Locator("input[name='totpPin']");
						if (await totpInput.CountAsync() == 0)
						{
							ILocator ga6 = page.Locator("div.VV3oRb[jsname='EBHGs'][data-action='selectchallenge'][data-challengetype='6']:not(.RDPZE)");
							if (await ga6.CountAsync() == 0)
							{
								ga6 = page.Locator("div[jsname='EBHGs'][data-action='selectchallenge'][data-challengetype='6']:not(.RDPZE)");
							}
							if (await ga6.CountAsync() > 0)
							{
								SetText(vitri, "STATUS", "STEP 3: Chọn Google Authenticator app (challenge type 6)...");
								try { await ga6.First.ScrollIntoViewIfNeededAsync(); } catch { }
								try
								{
									await ga6.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000f });
								}
								catch
								{
								}
								try
								{
									await ga6.First.ClickAsync(new LocatorClickOptions { Timeout = 15000f, Force = true });
								}
								catch
								{
									try
									{
										var boxGa = await ga6.First.BoundingBoxAsync();
										if (boxGa != null)
										{
											float cxGa = (float)(boxGa.X + boxGa.Width / 2.0);
											float cyGa = (float)(boxGa.Y + boxGa.Height / 2.0);
											await page.Mouse.ClickAsync(cxGa, cyGa);
										}
									}
									catch
									{
									}
								}
								try
								{
									await page.WaitForSelectorAsync("input[name='totpPin']", new PageWaitForSelectorOptions { Timeout = 20000f });
								}
								catch
								{
								}
							}
						}

						totpInput = page.Locator("input[name='totpPin']");
						if (await totpInput.CountAsync() > 0)
						{
							SetText(vitri, "STATUS", "STEP 3: Nhập mã 2FA");
							await RunStepWithReloadRetryAsync(page, vitri, "STEP 3 (2FA TOTP sau chọn Authenticator)", async delegate
							{
								token = await Get2FAToken(ma2faTrim);
								await totpInput.First.WaitForAsync(new LocatorWaitForOptions
								{
									State = WaitForSelectorState.Visible,
									Timeout = 15000f
								});
								await totpInput.First.FillAsync(token);
								SetText(vitri, "STATUS", "STEP 3: Submit 2FA");
								await page.ClickAsync("#totpNext");
							});
							verifyDone = await WaitForGoogleTotpSubmitOutcomeSuccessAsync(page, vitri);
							if (!verifyDone)
							{
								AppendAutomationLog("WARN", vitri, email, "Dừng: mã 2FA không được chấp nhận hoặc vẫn kẹt màn TOTP.");
								SetText(vitri, "STATUS", "Lỗi: Mã 2FA không đúng hoặc Google chưa chấp nhận — kết thúc đăng nhập.");
								return false;
							}
						}
						else
						{
							// Fallback: Confirm recovery email (challenge 12) nếu không mở được màn TOTP
							ILocator recovery = page.Locator("div.VV3oRb[jsname='EBHGs'][data-action='selectchallenge'][data-challengetype='12']");
							if (await recovery.CountAsync() == 0)
								recovery = page.Locator("div[jsname='EBHGs'][data-action='selectchallenge'][data-challengetype='12']");
							if (await recovery.CountAsync() == 0)
								recovery = page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Confirm your recovery email" });
							if (await recovery.CountAsync() == 0)
								recovery = page.Locator("div[jsname='EBHGs'][role='link']").Filter(new LocatorFilterOptions { HasTextString = "Confirm your recovery email" });
							if (await recovery.CountAsync() > 0)
							{
								SetText(vitri, "STATUS", "STEP 3: Chọn Confirm recovery email");
								try { await recovery.First.ScrollIntoViewIfNeededAsync(); } catch { }
								try
								{
									await recovery.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000f });
								}
								catch
								{
								}
								await recovery.First.ClickAsync(new LocatorClickOptions { Timeout = 15000f });
								switchedToRecoveryEmail = true;
							}
							else
							{
								try { SetText(vitri, "STATUS", "STEP 3 [LOG]: Có 2FA nhưng không mở được TOTP và không thấy challengetype=12 => BỎ QUA"); } catch { }
								goto AFTER_VERIFY_STEP_3;
							}
							verifyDone = false;
						}
					}

					// Chỉ điều hướng tiếp khi đã verify xong (2FA OK). Nếu đang ở flow recovery email thì dừng tại đó.
					if (verifyDone)
					{
						// Sau TOTP, Google thường chuyển sang speedbump passkey "Sign in faster" — phải Not now trước khi Goto.
						try
						{
							await TryDismissGooglePasskeyAfterVerifyAsync(page, vitri, "STEP 3.1");
						}
						catch
						{
						}
						await DelayBatchAsync(5000);
						await RunStepWithReloadRetryAsync(page, vitri, "Goto myaccount/language (sau 2FA)", async delegate
						{
							await page.GotoAsync("https://myaccount.google.com/language");
						});
					}
					else
					{
						// Nếu đang ở flow recovery email: chờ Google chuyển bước (có thể mất thời gian).
						try
						{
							SetText(vitri, "STATUS", "STEP 3 [LOG]: verifyDone=false (dừng tại màn verify/recovery). url=" + page.Url);
						}
						catch
						{
						}

						// Quan trọng: khi chưa verify xong thì KHÔNG được chạy các bước tiếp theo (mở inbox/đổi info...).
						// Nhưng nếu đã chọn recovery email thì phải CHỜ hoàn tất verify, không báo lỗi ngay.
						if (clickedRecoveryChoice || switchedToRecoveryEmail)
						{
							SetText(vitri, "STATUS", "STEP 3: Đã chọn Recovery email — chờ Google xử lý...");
							DateTime startWait = DateTime.Now;
							while ((DateTime.Now - startWait).TotalSeconds < 90.0)
							{
								string u = "";
								try { u = page.Url ?? ""; } catch { }
								if (!string.IsNullOrEmpty(u) && (u.Contains("myaccount.google.com") || u.Contains("mail.google.com") || u.Contains("/myaccount")))
								{
									verifyDone = true;
									break;
								}
								await DelayBatchAsync(1000);
							}

							if (verifyDone)
							{
								SetText(vitri, "STATUS", "STEP 3: Verify OK — tiếp tục...");
								try
								{
									await TryDismissGooglePasskeyAfterVerifyAsync(page, vitri, "STEP 3.1");
								}
								catch
								{
								}
								try
								{
									await RunStepWithReloadRetryAsync(page, vitri, "Goto myaccount/language (sau recovery)", async delegate
									{
										await page.GotoAsync("https://myaccount.google.com/language");
									});
								}
								catch
								{
								}
							}
							else
							{
								SetText(vitri, "STATUS", "STEP 3: Chờ verify quá lâu — BỎ QUA, QUA BƯỚC TIẾP THEO");
								goto AFTER_VERIFY_STEP_3;
							}
						}
						else
						{
							// Không phải recovery flow và cũng chưa verify: theo yêu cầu mới thì vẫn qua bước tiếp theo.
							try { SetText(vitri, "STATUS", "STEP 3 [LOG]: Chưa verify xong nhưng vẫn QUA BƯỚC TIẾP THEO"); } catch { }
							goto AFTER_VERIFY_STEP_3;
						}
					}
				}
				AFTER_VERIFY_STEP_3:
				// Chỉ đổi ngôn ngữ khi đã verify xong. Tránh trường hợp chưa click được challenge mà vẫn nhảy bước.
				if (page.Url.Contains("myaccount"))
				{
					await page.EvaluateAsync("async () => {\r\n                        const html = document.documentElement.innerHTML;\r\n\r\n                        // regex bắt cả ' và \"\r\n                        const match = html.match(/(['\"])(APv[^'\"]+)\\1/);\r\n                        const at = match ? match[2] : null;\r\n\r\n                        if (!at) {\r\n                            console.log('❌ Không tìm thấy AT');\r\n                            return 'NO_AT';\r\n                        }\r\n\r\n                        console.log('✅ AT:', at);\r\n\r\n                        const res = await fetch('/_/language_update?hl=en&soc-app=1&soc-platform=1&soc-device=1', {\r\n                            method: 'POST',\r\n                            headers: {\r\n                                'content-type': 'application/x-www-form-urlencoded'\r\n                            },\r\n                            body: 'f.req=%5B%5B%22en%22%5D%5D&at=' + encodeURIComponent(at)\r\n                        });\r\n\r\n                        const text = await res.text();\r\n                        console.log(text);\r\n                        return 'OK';\r\n                    }");
				}
			}
			catch (GoogleSignInDeadAccountException)
			{
				return false;
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				SetText(vitri, "STATUS", "Lỗi Login " + ex2.Message);
				return false;
			}
			SetText(vitri, "STATUS", "Đăng nhập xong — mở tab Gmail Inbox...");
			try
			{
				IPage inbox = await context.NewPageAsync();
				await RunStepWithReloadRetryAsync(inbox, vitri, "Mở Gmail Inbox", async delegate
				{
					await inbox.GotoAsync("https://mail.google.com/mail/u/0/", new PageGotoOptions
					{
						WaitUntil = WaitUntilState.DOMContentLoaded
					});
					await inbox.BringToFrontAsync();
				});
				try
				{
					await inbox.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
					{
						Timeout = 20000f
					});
				}
				catch
				{
				}
				if (await TryAbortIfInboxRedirectedToGoogleRestrictionsAsync(inbox, vitri, email))
				{
					return false;
				}
			}
			catch
			{
				// ignore: mở tab chỉ để tiện thao tác, không ảnh hưởng luồng chính
			}
			if (cb_changeinfo.Checked)
			{
				try
				{
					await RunStepWithReloadRetryAsync(page, vitri, "STEP 4–5 (đổi avatar)", async delegate
					{
						SetText(vitri, "STATUS", "STEP 4: Mở trang Personal Info");
						await page.GotoAsync("https://myaccount.google.com/personal-info");
						await DelayBatchAsync(5000);
						SetText(vitri, "STATUS", "STEP 5: Click Change Avatar");
						await page.GetByLabel("Change profile photo").ClickAsync();
						SetText(vitri, "STATUS", "STEP 5: Chờ iframe avatar");
						await DelayBatchAsync(5000);
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
						await DelayBatchAsync(5000);
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
						await DelayBatchAsync(7000);
					});
				}
				catch (Exception ex)
				{
					Exception ex3 = ex;
					SetText(vitri, "STATUS", "Lỗi đổi Avatar " + ex3.Message);
				}
			}
			bool wantTaoForm = cb_tao_form.Checked;
			bool wantTaoSheetScript = cb_tao_sheet_script.Checked;
			if (!wantTaoForm && !wantTaoSheetScript)
			{
				SetText(vitri, "STATUS", "[Link] Không bật ‘Tạo Form’ hoặc ‘Tạo Sheet + Script’ — bỏ qua pipeline.");
			}
			else
			{
				if (_noidung.Count == 0)
				{
					MessageBox.Show("Không có nội dung (Data: tieude.txt, noidung.txt, codesc.txt…).");
					return false;
				}
				noidung nd = _noidung[0];
				string formLink = "";
				if (wantTaoForm)
				{
					SetText(vitri, "STATUS", "[Form] Bước 1/3: Tab mới → tạo Google Form...");
					await DelayBatchAsync(3000);
					IPage formPage = await context.NewPageAsync();
					await RunStepWithReloadRetryAsync(formPage, vitri, "[Form] Mở trang tạo Form", async delegate
					{
						await formPage.GotoAsync("https://docs.google.com/forms/u/0/create?usp=forms_home&ths=true", new PageGotoOptions
						{
							WaitUntil = WaitUntilState.DOMContentLoaded,
							Timeout = 120000f
						});
					});
					await DelayBatchAsync(3000);
					SetText(vitri, "STATUS", "[Form] Đóng popup quyền truy cập Forms (nếu có)...");
					await DismissGoogleFormsAccessControlDialogIfPresentAsync(formPage);
					await DelayBatchAsync(500);
					try
					{
						await DismissGoogleFormsAccessControlDialogIfPresentAsync(formPage, waitBeforeCheck: false);
					SetText(vitri, "STATUS", "[Form] Điền tiêu đề form (chữ thuần, không HTML)...");
					ILocator formTitle = formPage.Locator("div[jsname='yrriRe'][contenteditable='true']").Or(formPage.Locator("div[role='textbox'][aria-label='Form title'][contenteditable='true']")).Or(formPage.Locator("div[aria-label='Form title'][contenteditable='true']")).First;
					ILocator desc = formPage.Locator("div[aria-label='Form description']").First;
					string titlePlain = ToPlainTextForGoogleForm(nd.tieude);
					await formTitle.ClickAsync();
					await PageWaitCancellableAsync(formPage, 250f);
					if (!await TrySetGoogleFormEditablePlainAsync(formPage, "title", titlePlain))
					{
						SetText(vitri, "STATUS", "[Form] Tiêu đề: fallback clipboard (text/plain)...");
						await _clipLock.WaitAsync();
						try
						{
							await formPage.EvaluateAsync("() => {\r\n  const el = document.querySelector('div[jsname=\"yrriRe\"][contenteditable=\"true\"]')\r\n    || document.querySelector('div[role=\"textbox\"][aria-label=\"Form title\"][contenteditable=\"true\"]')\r\n    || document.querySelector('div[aria-label=\"Form title\"][contenteditable=\"true\"]');\r\n  if (!el) return;\r\n  el.focus();\r\n  const range = document.createRange();\r\n  range.selectNodeContents(el);\r\n  range.deleteContents();\r\n  el.dispatchEvent(new InputEvent('input', { bubbles: true }));\r\n}");
							await formPage.Keyboard.PressAsync("Control+A");
							await formPage.Keyboard.PressAsync("Backspace");
							await ClipboardWritePlainAsync(formPage, titlePlain);
							await PageWaitCancellableAsync(formPage, 400f);
							await formTitle.ClickAsync();
							await formPage.Keyboard.PressAsync("Control+V");
							await PageWaitCancellableAsync(formPage, 500f);
						}
						finally
						{
							_clipLock.Release();
						}
					}
					await formPage.EvaluateAsync("() => {\r\n  const el = document.querySelector('div[jsname=\"yrriRe\"][contenteditable=\"true\"]')\r\n    || document.querySelector('div[role=\"textbox\"][aria-label=\"Form title\"][contenteditable=\"true\"]')\r\n    || document.querySelector('div[aria-label=\"Form title\"][contenteditable=\"true\"]');\r\n  if (!el) {\r\n    return;\r\n  }\r\n  const strip = /^[\\s\\r\\n]*Untitled form[\\s\\r\\n]*/i;\r\n  const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT, null);\r\n  const textNodes = [];\r\n  let n;\r\n  while ((n = walker.nextNode())) {\r\n    textNodes.push(n);\r\n  }\r\n  for (let i = 0; i < textNodes.length; i++) {\r\n    const t = textNodes[i];\r\n    if (t.textContent && /Untitled form/i.test(t.textContent)) {\r\n      t.textContent = t.textContent.replace(strip, '');\r\n    }\r\n  }\r\n  while (el.firstChild && el.firstChild.nodeType === 3 && el.firstChild.textContent.trim() === '') {\r\n    el.removeChild(el.firstChild);\r\n  }\r\n  el.dispatchEvent(new InputEvent('input', { bubbles: true }));\r\n}");
					await PageWaitCancellableAsync(formPage, 250f);
					SetText(vitri, "STATUS", "[Form] Điền mô tả form (chữ thuần, không HTML)...");
					string descPlain = ToPlainTextForGoogleForm(nd.noidungchinh);
					await desc.ClickAsync();
					await PageWaitCancellableAsync(formPage, 500f);
					if (!await TrySetGoogleFormEditablePlainAsync(formPage, "description", descPlain))
					{
						SetText(vitri, "STATUS", "[Form] Mô tả: fallback clipboard (text/plain)...");
						await _clipLock.WaitAsync();
						try
						{
							await formPage.Keyboard.PressAsync("Control+A");
							await formPage.Keyboard.PressAsync("Delete");
							await ClipboardWritePlainAsync(formPage, descPlain);
							await PageWaitCancellableAsync(formPage, 600f);
							await formPage.Keyboard.PressAsync("Control+V");
							await PageWaitCancellableAsync(formPage, 2000f);
						}
						finally
						{
							_clipLock.Release();
						}
					}
					else
					{
						await PageWaitCancellableAsync(formPage, 600f);
					}
					await PageWaitCancellableAsync(formPage, 1000f);
					SetText(vitri, "STATUS", "[Form] Xóa câu hỏi mặc định (Question 1)...");
					ILocator desc2 = formPage.Locator("div[aria-label='Question']").First;
					await desc2.ClickAsync();
					ILocator desc3 = formPage.Locator("div[aria-label='Delete question']").First;
					await desc3.ClickAsync();
					try
					{
						await PageWaitCancellableAsync(formPage, 1000f);
						SetText(vitri, "STATUS", "[Form] Theme: mở Customize Theme...");
						await formPage.Locator("[aria-label='Customize Theme']").ClickAsync();
						await PageWaitCancellableAsync(formPage, 1000f);
						SetText(vitri, "STATUS", "[Form] Theme: chọn ảnh header (Upload → Browse)...");
						string headerPath = ResolveBundledImagePath("header.jpg");
						if (!File.Exists(headerPath))
						{
							SetText(vitri, "STATUS", "[Form] Không thấy header.jpg — đặt file cạnh PlayAPP.exe hoặc Data\\");
							throw new FileNotFoundException("Đặt header.jpg (ảnh header gần màu #509beb / #4f9beb) cạnh PlayAPP.exe hoặc trong thư mục Data.", headerPath);
						}
						SetText(vitri, "STATUS", "[Form] Theme: upload file " + headerPath);
						await formPage.Locator("[aria-label='Choose image for header']").ClickAsync();
						await PageWaitCancellableAsync(formPage, 1000f);
						await formPage.WaitForSelectorAsync("iframe[src*='picker']");
						IFrameLocator frame2 = formPage.FrameLocator("iframe[src*='picker']");
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
						await (await formPage.RunAndWaitForFileChooserAsync(async delegate
						{
							await frame2.GetByText("Browse").ClickAsync();
						})).SetFilesAsync(headerPath);
						await DelayBatchAsync(2000);
						await ClickGoogleFormsHeaderPickerDoneButtonAsync(formPage, frame2);
						await DelayBatchAsync(2500);
						SetText(vitri, "STATUS", "[Form] Theme: ảnh header đã áp dụng (Done)");
						try
						{
							SetText(vitri, "STATUS", "[Form] Theme: dialog hoặc sidebar — chọn màu #509beb hoặc #4f9beb...");
							await PageWaitCancellableAsync(formPage, 2000f);
							bool colorApplied = false;
							LocatorFilterOptions hasThemeBlue = new LocatorFilterOptions
							{
								Has = formPage.Locator("div.UBrD9d[data-color='#509beb'], div.UBrD9d[data-color='#4f9beb']")
							};
							ILocator themeDialog = formPage.Locator("div[role='dialog'][aria-label='Theme']");
							try
							{
								await themeDialog.First.WaitForAsync(new LocatorWaitForOptions
								{
									State = WaitForSelectorState.Visible,
									Timeout = 18000f
								});
								ILocator colorSwatch = themeDialog.Locator("div.UBrD9d[data-color='#509beb'], div.UBrD9d[data-color='#4f9beb']").First;
								await colorSwatch.WaitForAsync(new LocatorWaitForOptions
								{
									State = WaitForSelectorState.Visible,
									Timeout = 45000f
								});
								await colorSwatch.ScrollIntoViewIfNeededAsync();
								await PageWaitCancellableAsync(formPage, 200f);
								await colorSwatch.ClickAsync(new LocatorClickOptions
								{
									Timeout = 30000f,
									Force = true
								});
								await PageWaitCancellableAsync(formPage, 400f);
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
									formPage.Locator("div[role='complementary'][aria-roledescription='sidebar']").Filter(hasThemeBlue).First,
									formPage.Locator("div.lOsMle.kiQbk.cvymMe").Filter(hasThemeBlue).First,
									formPage.Locator("div.lOsMle.cvymMe").Filter(hasThemeBlue).First
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
										ILocator colorItem = themePanel.Locator("div.UBrD9d[role='listitem'][data-color='#509beb'][data-label='#509beb'], div.UBrD9d[role='listitem'][data-color='#4f9beb'][data-label='#4f9beb']").First;
										if (await colorItem.CountAsync() == 0)
										{
											colorItem = themePanel.Locator("div.UBrD9d[role='listitem'][data-color='#509beb'], div.UBrD9d[role='listitem'][data-color='#4f9beb']").First;
										}
										if (await colorItem.CountAsync() == 0)
										{
											colorItem = themePanel.Locator("div.UBrD9d[data-color='#509beb'][data-label='#509beb'], div.UBrD9d[data-color='#4f9beb'][data-label='#4f9beb']").First;
										}
										if (await colorItem.CountAsync() == 0)
										{
											colorItem = themePanel.Locator("div.UBrD9d[data-color='#509beb'], div.UBrD9d[data-color='#4f9beb']").First;
										}
										if (await colorItem.CountAsync() == 0)
										{
											colorItem = themePanel.GetByRole(AriaRole.Listitem, new LocatorGetByRoleOptions
											{
												Name = "#509beb",
												Exact = true
											}).Or(themePanel.GetByRole(AriaRole.Listitem, new LocatorGetByRoleOptions
											{
												Name = "#4f9beb",
												Exact = true
											})).First;
										}
										await colorItem.WaitForAsync(new LocatorWaitForOptions
										{
											State = WaitForSelectorState.Visible,
											Timeout = 30000f
										});
										await colorItem.ScrollIntoViewIfNeededAsync();
										await PageWaitCancellableAsync(formPage, 250f);
										await colorItem.ClickAsync(new LocatorClickOptions
										{
											Timeout = 30000f,
											Force = true
										});
										await PageWaitCancellableAsync(formPage, 500f);
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
								bool jsPick = await formPage.EvaluateAsync<bool>("() => {\r\n  const colors = ['#509beb', '#4f9beb'];\r\n  const pick = (root) => {\r\n    if (!root) return null;\r\n    for (let j = 0; j < colors.length; j++) {\r\n      const c = colors[j];\r\n      let hit = root.querySelector('div.UBrD9d[role=\"listitem\"][data-color=\"' + c + '\"][data-label=\"' + c + '\"]')\r\n        || root.querySelector('div.UBrD9d[data-color=\"' + c + '\"][data-label=\"' + c + '\"]')\r\n        || root.querySelector('div.UBrD9d[data-color=\"' + c + '\"]');\r\n      if (hit) return hit;\r\n    }\r\n    return null;\r\n  };\r\n  const dlg = document.querySelector('div[role=\"dialog\"][aria-label=\"Theme\"]');\r\n  let el = pick(dlg);\r\n  if (el) {\r\n    el.scrollIntoView({ block: 'center', inline: 'center' });\r\n    el.click();\r\n    return true;\r\n  }\r\n  const sideSels = ['div[role=\"complementary\"][aria-roledescription=\"sidebar\"]', 'div.lOsMle.kiQbk.cvymMe', 'div.lOsMle.cvymMe'];\r\n  for (let s = 0; s < sideSels.length; s++) {\r\n    const nodes = document.querySelectorAll(sideSels[s]);\r\n    for (let i = 0; i < nodes.length; i++) {\r\n      el = pick(nodes[i]);\r\n      if (el) {\r\n        el.scrollIntoView({ block: 'center', inline: 'center' });\r\n        el.click();\r\n        return true;\r\n      }\r\n    }\r\n  }\r\n  el = document.querySelector('div.UBrD9d[data-color=\"#509beb\"], div.UBrD9d[data-color=\"#4f9beb\"]');\r\n  if (!el) return false;\r\n  el.scrollIntoView({ block: 'center', inline: 'center' });\r\n  el.click();\r\n  return true;\r\n}");
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
								throw new Exception("Không tìm thấy ô màu #509beb / #4f9beb (dialog Theme hoặc sidebar lOsMle/kiQbk).");
							}
							await PageWaitCancellableAsync(formPage, 1000f);
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
					await formPage.GetByRole(AriaRole.Button, new PageGetByRoleOptions
					{
						Name = "Publish"
					}).First.ClickAsync();
					ILocator dialog = formPage.GetByRole(AriaRole.Dialog, new PageGetByRoleOptions
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
					await formPage.GetByLabel("Click to copy responder link").ClickAsync();
					await PageWaitCancellableAsync(formPage, 500f);
					formLink = await formPage.EvaluateAsync<string>("() => navigator.clipboard.readText()");
					SetText(vitri, "STATUS", "[Form] Xong: link phản hồi đã copy (clipboard); URL editor trên tab hiện tại");
				}
					catch (Exception ex6)
					{
						SetText(vitri, "STATUS", "[Form] Lỗi tạo / chỉnh / publish Form: " + ex6.Message);
					}
				}
				bool sheetScriptFlowOk = !wantTaoSheetScript;
				if (wantTaoSheetScript)
				{
				try
				{
					SetText(vitri, "STATUS", wantTaoForm ? "[Sheet] Bước 2/3: Tab mới → Google Sheets (tạo file)..." : "[Sheet] Tạo Google Sheets (không tạo Form)…");
					await DelayBatchAsync(5000);
					IPage sheetPage = await context.NewPageAsync();
					await RunStepWithReloadRetryAsync(sheetPage, vitri, "[Sheet] Mở Sheets + chờ tab", async delegate
					{
						await sheetPage.GotoAsync("https://docs.google.com/spreadsheets/u/0/create?usp=sheets_home&ths=true", new PageGotoOptions
						{
							WaitUntil = WaitUntilState.DOMContentLoaded,
							Timeout = 120000f
						});
						await DelayBatchAsync(4500);
						SetText(vitri, "STATUS", "[Sheet] Chờ tab sheet (docs-sheet-tab-name)...");
						await sheetPage.WaitForSelectorAsync(".docs-sheet-tab-name", new PageWaitForSelectorOptions
						{
							Timeout = 120000f
						});
					});
					SetText(vitri, "STATUS", "[Sheet] Đổi tên tab thành Sheet1 → Enter...");
					await sheetPage.DblClickAsync(".docs-sheet-tab-name");
					string newSheetName = "Sheet1";
					await sheetPage.Keyboard.TypeAsync(newSheetName);
					await sheetPage.Keyboard.PressAsync("Enter");
					string url3 = sheetPage.Url;
					SetText(vitri, "STATUS", "[Sheet] Đã tạo sheet → mở script.new (tab mới)...");
					IPage scriptPage = await context.NewPageAsync();
					await RunStepWithReloadRetryAsync(scriptPage, vitri, "[Script] Mở script.new + chờ editor", async delegate
					{
						await scriptPage.GotoAsync("https://script.new", new PageGotoOptions
						{
							WaitUntil = WaitUntilState.DOMContentLoaded,
							Timeout = 80000f
						});
						await DelayBatchAsync(5000);
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
						await DelayBatchAsync(2000);
						await scriptPage.SetViewportSizeAsync(1920, 1080);
						SetText(vitri, "STATUS", "[Script] Chờ editor Monaco (.view-lines)...");
						await scriptPage.WaitForSelectorAsync(".view-lines", new PageWaitForSelectorOptions
						{
							Timeout = 180000f
						});
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
					await DelayBatchAsync(5000);
					IElementHandle closeBtn = await scriptPage.QuerySelectorAsync("button[aria-label='close']");
					if (closeBtn != null)
					{
						try
						{
							await closeBtn.ClickAsync();
							await DelayBatchAsync(500);
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
					await DelayBatchAsync(2500);
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
					await DelayBatchAsync(2500);
					try
					{
						await RunScriptEditorTwiceWithReloadOnAccountAccessWarningAsync(scriptPage, vitri, email, reloadImmediately: false, maxAttempts: 4);
					}
					catch (InvalidOperationException exAccLog)
					{
						SetText(vitri, "STATUS", "[Script] " + exAccLog.Message);
						throw;
					}
					try
					{
						SetText(vitri, "STATUS", "[Script] OAuth: chờ cấp quyền (Authorization required / Review permissions)...");
						bool needOauthFlow = false;
						for (int oauthWait = 0; oauthWait < 20; oauthWait++)
						{
							if (await ScriptPageShowsAuthorizationRequiredDialogAsync(scriptPage))
							{
								needOauthFlow = true;
								break;
							}
							await DelayBatchAsync(500);
						}
						if (!needOauthFlow)
						{
							SetText(vitri, "STATUS", "[Script] Không thấy dialog Authorization required sau khi Run → bỏ qua bước OAuth, chuyển kiểm tra Execution log.");
							goto AFTER_OAUTH_FLOW;
						}
						ILocator oauthReviewBtnStrict = scriptPage.Locator("div.uW2Fw-P5QLlc button[data-mdc-dialog-action='cCU94d']").First;
						ILocator oauthReviewTextStrict = scriptPage.Locator("div.uW2Fw-P5QLlc span[jsname='V67aGc'].UywwFc-vQzf8d").Filter(new LocatorFilterOptions
						{
							HasTextString = "Review permissions"
						}).First;
						ILocator oauthReviewBtn = oauthReviewBtnStrict.Or(scriptPage.GetByRole(AriaRole.Button, new PageGetByRoleOptions
						{
							Name = "Review permissions"
						})).First;
						try
						{
							await oauthReviewBtn.WaitForAsync(new LocatorWaitForOptions
							{
								State = WaitForSelectorState.Visible,
								Timeout = 60000f
							});
						}
						catch
						{
							ILocator authDlg = scriptPage.Locator("h2:has-text('Authorization required')").Or(scriptPage.Locator("span.UywwFc-vQzf8d:has-text('Review permissions')"));
							await authDlg.First.WaitForAsync(new LocatorWaitForOptions
							{
								State = WaitForSelectorState.Visible,
								Timeout = 30000f
							});
							await oauthReviewBtn.WaitForAsync(new LocatorWaitForOptions
							{
								State = WaitForSelectorState.Visible,
								Timeout = 20000f
							});
						}
						IPage authPage = null;
						const int maxOauthSomethingWrongAttempts = 3;
						for (int oauthSw = 0; oauthSw < maxOauthSomethingWrongAttempts; oauthSw++)
						{
							if (oauthSw > 0)
							{
								SetText(vitri, "STATUS", "[Script] OAuth: Google Something went wrong — đóng tab đăng nhập, thử lại (" + (oauthSw + 1) + "/" + maxOauthSomethingWrongAttempts + ")...");
								AppendAutomationLog("WARN", vitri, email, "[Script] OAuth retry sau màn Something went wrong.");
								try
								{
									if (authPage != null && !authPage.IsClosed)
									{
										await authPage.CloseAsync();
									}
								}
								catch
								{
								}
								try
								{
									await scriptPage.BringToFrontAsync();
								}
								catch
								{
								}
								await DelayBatchAsync(2500);
							}
							try
							{
								Task<IPage> waitNewPage = context.WaitForPageAsync(new BrowserContextWaitForPageOptions
								{
									Timeout = 45000f
								});
								bool clicked = false;
								try
								{
									await oauthReviewBtnStrict.ClickAsync(new LocatorClickOptions
									{
										Timeout = 12000f
									});
									clicked = true;
								}
								catch
								{
								}
								if (!clicked)
								{
									try
									{
										await oauthReviewBtn.ClickAsync(new LocatorClickOptions
										{
											Timeout = 12000f,
											Force = true
										});
										clicked = true;
									}
									catch
									{
									}
								}
								if (!clicked)
								{
									try
									{
										await oauthReviewTextStrict.ClickAsync(new LocatorClickOptions
										{
											Timeout = 12000f,
											Force = true
										});
										clicked = true;
									}
									catch
									{
									}
								}
								if (!clicked)
								{
									try
									{
										clicked = await scriptPage.EvaluateAsync<bool>("() => { const btn = document.querySelector(\"div.uW2Fw-P5QLlc button[data-mdc-dialog-action='cCU94d']\"); if (!btn) return false; btn.click(); return true; }");
									}
									catch
									{
									}
								}
								if (!clicked)
								{
									try
									{
										clicked = await scriptPage.EvaluateAsync<bool>("() => { const span = document.querySelector(\"div.uW2Fw-P5QLlc span.UywwFc-vQzf8d\"); if (!span) return false; const btn = span.closest('button'); if (!btn) return false; btn.click(); return true; }");
									}
									catch
									{
									}
								}
								if (!clicked)
								{
									throw new InvalidOperationException("Không click được nút Review permissions trong dialog Authorization required.");
								}
								authPage = await waitNewPage;
							}
							catch
							{
								authPage = context.Pages.LastOrDefault((IPage p) => p != scriptPage && !p.IsClosed && p.Url.Contains("accounts.google.com"));
								if (authPage == null)
								{
									authPage = context.Pages.LastOrDefault((IPage p) => p != scriptPage && !p.IsClosed);
								}
								if (authPage == null)
								{
									throw;
								}
							}
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
							await DelayBatchAsync(2500);
							if (await PageShowsGoogleSignInSomethingWentWrongAsync(authPage))
							{
								if (oauthSw >= maxOauthSomethingWrongAttempts - 1)
								{
									throw new InvalidOperationException("Google OAuth: Something went wrong (đã thử " + maxOauthSomethingWrongAttempts + " lần).");
								}
								continue;
							}
							string ma2faOAuthTrim = ma2fa?.Trim() ?? "";
							bool hasSecretForOAuth = ma2faOAuthTrim.Length > 0;
							if (hasSecretForOAuth)
							{
								string oldToken = token;
								string newToken = oldToken;
								while (newToken == oldToken)
								{
									await DelayBatchAsync(1000);
									newToken = await Get2FAToken(ma2faOAuthTrim);
								}
								token = newToken;
								try
								{
									await authPage.WaitForSelectorAsync("input[name='totpPin']", new PageWaitForSelectorOptions
									{
										Timeout = 25000f
									});
									await authPage.FillAsync("input[name='totpPin']", token);
									await authPage.ClickAsync("#totpNext");
									await PageWaitCancellableAsync(scriptPage, 9000f);
								}
								catch
								{
									SetText(vitri, "STATUS", "[Script] OAuth: có secret 2FA nhưng không thấy ô TOTP — tiếp tục bước Continue / cấp quyền");
									await DelayBatchAsync(2000);
								}
							}
							else
							{
								SetText(vitri, "STATUS", "[Script] OAuth: không có mã 2FA trong Account — bỏ qua TOTP, tiếp tục cấp quyền");
								try
								{
									ILocator totpMaybe = authPage.Locator("input[name='totpPin']");
									await totpMaybe.WaitForAsync(new LocatorWaitForOptions
									{
										State = WaitForSelectorState.Visible,
										Timeout = 10000f
									});
									SetText(vitri, "STATUS", "[Script] OAuth: Google vẫn hiện TOTP — thiếu secret trong Account, bỏ qua điền → tiếp tục");
								}
								catch
								{
								}
								await DelayBatchAsync(2000);
							}
							if (await PageShowsGoogleSignInSomethingWentWrongAsync(authPage))
							{
								if (oauthSw >= maxOauthSomethingWrongAttempts - 1)
								{
									throw new InvalidOperationException("Google OAuth: Something went wrong sau bước 2FA (đã thử " + maxOauthSomethingWrongAttempts + " lần).");
								}
								continue;
							}
							ILocator advanced = authPage.Locator("a:has-text('Advanced')");
							if (await advanced.CountAsync() > 0)
							{
								await advanced.ClickAsync();
								await PageWaitCancellableAsync(scriptPage, 2000f);
							}
							ILocator gotouniti = authPage.Locator("a:has-text('Go to Untitled project (unsafe)')");
							if (await gotouniti.CountAsync() > 0)
							{
								await gotouniti.ClickAsync();
								await PageWaitCancellableAsync(scriptPage, 3500f);
							}
							else
							{
								await PageWaitCancellableAsync(scriptPage, 2000f);
							}
							bool consentDone = false;
							for (int consentStep = 0; consentStep < 3 && !consentDone; consentStep++)
							{
								ILocator allCheckboxes = authPage.Locator("input[type='checkbox'][jsname='YPqjbf']").Or(authPage.GetByRole(AriaRole.Checkbox));
								int cbCount = 0;
								try
								{
									cbCount = await allCheckboxes.CountAsync();
								}
								catch
								{
								}
								if (cbCount > 0)
								{
									ILocator selectAllCb = authPage.GetByRole(AriaRole.Checkbox, new PageGetByRoleOptions
									{
										Name = "Select all"
									});
									try
									{
										if (await selectAllCb.CountAsync() > 0 && !await selectAllCb.First.IsCheckedAsync())
										{
											await selectAllCb.First.CheckAsync(new LocatorCheckOptions
											{
												Timeout = 15000f
											});
										}
									}
									catch
									{
										ILocator firstCb = allCheckboxes.First;
										try
										{
											await firstCb.CheckAsync(new LocatorCheckOptions
											{
												Timeout = 15000f
											});
										}
										catch
										{
										}
									}
									await PageWaitCancellableAsync(scriptPage, 1500f);
								}
								ILocator continueBtn = authPage.GetByRole(AriaRole.Button, new PageGetByRoleOptions
								{
									Name = "Continue"
								}).First;
								await continueBtn.WaitForAsync(new LocatorWaitForOptions
								{
									State = WaitForSelectorState.Visible,
									Timeout = 30000f
								});
								try
								{
									if (await continueBtn.IsDisabledAsync())
									{
										await DelayBatchAsync(1500);
									}
								}
								catch
								{
								}
								await continueBtn.ClickAsync(new LocatorClickOptions
								{
									Timeout = 20000f
								});
								await PageWaitCancellableAsync(scriptPage, 3500f);
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
								await DelayBatchAsync(1200);
								int remainingCb = 0;
								try
								{
									remainingCb = await allCheckboxes.CountAsync();
								}
								catch
								{
								}
								consentDone = remainingCb == 0 || authPage.Url.IndexOf("accounts.google.com", StringComparison.OrdinalIgnoreCase) < 0;
							}
							if (!consentDone)
							{
								throw new TimeoutException("Google OAuth consent: chưa hoàn tất chọn quyền + Continue.");
							}
							SetText(vitri, "STATUS", "[Script] OAuth: cấp quyền / Continue / checkbox → xong");
							break;
						}
						AFTER_OAUTH_FLOW:
						;
					}
					catch (Exception ex7)
					{
						throw new InvalidOperationException("[Script] Lỗi OAuth / cấp quyền Run: " + ex7.Message, ex7);
					}
					try
					{
						if (await ScriptPageShowsExecutionLogAccountAccessWarningAsync(scriptPage))
						{
							SetText(vitri, "STATUS", "[Script] Sau OAuth vẫn thấy cảnh báo Execution log — F5 và Run lại...");
							AppendAutomationLog("WARN", vitri, email, "[Script] Cảnh báo account access còn sau OAuth — F5 + Run.");
							await RunScriptEditorTwiceWithReloadOnAccountAccessWarningAsync(scriptPage, vitri, email, reloadImmediately: true, maxAttempts: 3);
							if (await ScriptPageShowsExecutionLogAccountAccessWarningAsync(scriptPage))
							{
								throw new InvalidOperationException("[Script] Execution log vẫn còn cảnh báo requires access to your Google Account sau khi đã OAuth + F5/Run lại.");
							}
						}
					}
					catch (InvalidOperationException exAcc2)
					{
						throw new InvalidOperationException("[Script] " + exAcc2.Message, exAcc2);
					}
					catch (Exception exAfterOauth)
					{
						throw new InvalidOperationException("[Script] Lỗi khi F5 sau OAuth (Execution log): " + exAfterOauth.Message, exAfterOauth);
					}
					sheetScriptFlowOk = true;
				}
				catch (Exception ex8)
				{
					sheetScriptFlowOk = false;
					SetText(vitri, "STATUS", "[Sheet/Script] Lỗi (Sheets, editor, API hoặc Run): " + ex8.Message);
				}
				}
				if (wantTaoSheetScript && !sheetScriptFlowOk)
				{
					SetText(vitri, "STATUS", "ERROR [Sheet/Script] Chưa hoàn tất do vẫn còn lỗi/warning ở bước Script/OAuth.");
					return false;
				}
				if (wantTaoForm && wantTaoSheetScript)
				{
					SetText(vitri, "STATUS", "[Form+Sheet+Script] Hoàn tất pipeline — sẵn sàng DONE");
				}
				else if (wantTaoForm)
				{
					SetText(vitri, "STATUS", "[Form] Hoàn tất — đã bỏ qua Sheet/Script (chỉ tạo Form).");
				}
				else if (wantTaoSheetScript)
				{
					SetText(vitri, "STATUS", "[Sheet+Script] Hoàn tất — không tạo Form ([LINK_FORM] để trống nếu chưa có link).");
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
				string[] array = text.Split(new[] { '|', '\t' }, StringSplitOptions.None);
				if (array.Length >= 3)
				{
					string text2 = array[0];
					string text3 = array[1];
					string text4 = array[2];
					string text5 = (array.Length > 3) ? array[3] : "";
					string text6 = (array.Length > 4) ? array[4] : "";
					dataGridView_0.Rows.Add(num++, text2, text3, text4, text5, "", text6);
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

	private static bool TryParseProxyRawLine(string trimmed, out ProxyInfo info)
	{
		info = null;
		if (string.IsNullOrWhiteSpace(trimmed))
		{
			return false;
		}
		trimmed = trimmed.Trim();
		string[] array3 = trimmed.Split(':');
		if (array3.Length < 2)
		{
			return false;
		}
		string text2 = array3[0];
		string text3 = array3[1];
		string username = null;
		string password = null;
		if (array3.Length >= 4)
		{
			username = array3[2];
			password = array3[3];
		}
		info = new ProxyInfo
		{
			Server = "http://" + text2 + ":" + text3,
			Username = username,
			Password = password,
			RawLineForGpm = trimmed
		};
		return true;
	}

	private int GetLastGridDataRowIndex()
	{
		if (dataGridView1.Rows.Count == 0)
		{
			return -1;
		}
		return dataGridView1.Rows.Count - 1;
	}

	private void DataGridView1_SelectionChanged(object sender, EventArgs e)
	{
		try
		{
			if (dataGridView1.CurrentRow == null || dataGridView1.CurrentRow.IsNewRow)
			{
				return;
			}
			int idx = dataGridView1.CurrentRow.Index;
			if (idx >= 0)
			{
				_runQueueStartRowIndex = idx;
			}
		}
		catch
		{
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
		function visible(el) {
			if (!el || !el.getBoundingClientRect) return false;
			const s = window.getComputedStyle(el);
			if (s.visibility === 'hidden' || s.display === 'none' || parseFloat(s.opacity || '1') === 0) return false;
			const r = el.getBoundingClientRect();
			return r.width > 1 && r.height > 1;
		}
		function textMatches(btn) {
			if (!btn) return false;
			const t = norm(btn.textContent);
			return t === want || t.indexOf(want) >= 0;
		}
		function fireClick(btn) {
			if (!btn) return;
			try { btn.scrollIntoView({ block: 'center', inline: 'center', behavior: 'auto' }); } catch (eScroll) {}
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
			if (targetBtns[k].classList && targetBtns[k].classList.contains('M9Bg4d') && visible(targetBtns[k])) {
				chosen = targetBtns[k];
				break;
			}
		}
		if (!chosen) {
			for (let k = 0; k < targetBtns.length; k++) {
				if (targetBtns[k].classList && targetBtns[k].classList.contains('M9Bg4d')) {
					chosen = targetBtns[k];
					break;
				}
			}
		}
		if (!chosen) {
			for (let k = targetBtns.length - 1; k >= 0; k--) {
				if (visible(targetBtns[k])) { chosen = targetBtns[k]; break; }
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

	private static async Task<bool> TryMouseClickLocatorCenterAsync(ILocator target, IPage page)
	{
		try
		{
			if (await target.CountAsync() == 0)
			{
				return false;
			}
			ILocator last = target.Last;
			await last.ScrollIntoViewIfNeededAsync();
			await PlaywrightWaitHelpers.PageWaitAsync(page, 120f);
			var box = await last.BoundingBoxAsync();
			if (box == null)
			{
				return false;
			}
			float cx = (float)(box.X + box.Width / 2.0);
			float cy = (float)(box.Y + box.Height / 2.0);
			await page.Mouse.ClickAsync(cx, cy);
			return true;
		}
		catch
		{
			return false;
		}
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
			ILocator dlgFirst = dlg.First;
			await dlgFirst.WaitForAsync(new LocatorWaitForOptions
			{
				State = WaitForSelectorState.Visible,
				Timeout = 5000f
			});
			ILocator footerGotIt = dlg.Locator("div.OE6hId div[role='button'][jsname='LgbsSe'].M9Bg4d");
			if (await footerGotIt.CountAsync() > 0)
			{
				try
				{
					await footerGotIt.Last.ScrollIntoViewIfNeededAsync();
					await footerGotIt.Last.ClickAsync(opt);
					return true;
				}
				catch
				{
					if (await TryMouseClickLocatorCenterAsync(footerGotIt, page))
					{
						return true;
					}
				}
			}
			ILocator m9 = dlg.Locator("div[role='button'][jsname='LgbsSe'].M9Bg4d");
			if (await m9.CountAsync() > 0)
			{
				try
				{
					await m9.Last.ScrollIntoViewIfNeededAsync();
					await m9.Last.ClickAsync(opt);
					return true;
				}
				catch
				{
					if (await TryMouseClickLocatorCenterAsync(m9, page))
					{
						return true;
					}
				}
			}
			ILocator ebs = dlg.Locator("div[role='button'][jsname='LgbsSe'][data-id='EBS5u']");
			if (await ebs.CountAsync() > 0)
			{
				try
				{
					await ebs.Last.ScrollIntoViewIfNeededAsync();
					await ebs.Last.ClickAsync(opt);
					return true;
				}
				catch
				{
					if (await TryMouseClickLocatorCenterAsync(ebs, page))
					{
						return true;
					}
				}
			}
			ILocator lgbs = dlg.Locator("div[role='button'][jsname='LgbsSe']");
			if (await lgbs.CountAsync() > 0)
			{
				try
				{
					await lgbs.Last.ScrollIntoViewIfNeededAsync();
					await lgbs.Last.ClickAsync(opt);
					return true;
				}
				catch
				{
					if (await TryMouseClickLocatorCenterAsync(lgbs, page))
					{
						return true;
					}
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private static async Task<bool> TryClickGotItInSingleFrameAsync(IFrame frame, IPage page, string label)
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
			ILocator footerM9 = dlg.Locator("div.OE6hId div[role='button'][jsname='LgbsSe'].M9Bg4d");
			if (await footerM9.CountAsync() > 0)
			{
				try
				{
					await footerM9.Last.ScrollIntoViewIfNeededAsync();
					await footerM9.Last.ClickAsync(new LocatorClickOptions
					{
						Timeout = 12000f,
						Force = true
					});
					return true;
				}
				catch
				{
					if (await TryMouseClickLocatorCenterAsync(footerM9, page))
					{
						return true;
					}
				}
			}
			ILocator m9 = dlg.Locator("div[role='button'][jsname='LgbsSe'].M9Bg4d");
			if (await m9.CountAsync() > 0)
			{
				try
				{
					await m9.Last.ScrollIntoViewIfNeededAsync();
					await m9.Last.ClickAsync(new LocatorClickOptions
					{
						Timeout = 12000f,
						Force = true
					});
					return true;
				}
				catch
				{
					if (await TryMouseClickLocatorCenterAsync(m9, page))
					{
						return true;
					}
				}
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
		if (await TryClickGotItInSingleFrameAsync(page.MainFrame, page, label))
		{
			return true;
		}
		foreach (IFrame frame in page.Frames)
		{
			if (frame == page.MainFrame)
			{
				continue;
			}
			if (await TryClickGotItInSingleFrameAsync(frame, page, label))
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
				await PlaywrightWaitHelpers.PageWaitAsync(page, 800f);
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
				await PlaywrightWaitHelpers.PageWaitAsync(page, 800f);
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

	/// <summary>Trong một document (iframe picker Google): tìm nút Done / KbvHGe và kích hoạt chuột đầy đủ — crop UI đôi khi không phản hồi Playwright Click thường.</summary>
	private static async Task<bool> TryJsClickPickerDoneInFrameDocumentAsync(IFrame frame)
	{
		if (frame == null)
		{
			return false;
		}
		try
		{
			return await frame.EvaluateAsync<bool>(@"() => {
				function visible(el) {
					if (!el) return false;
					const st = window.getComputedStyle(el);
					if (st.display === 'none' || st.visibility === 'hidden' || parseFloat(st.opacity || '1') === 0) return false;
					const r = el.getBoundingClientRect();
					return r.width > 2 && r.height > 2;
				}
				function scrollAndFire(el) {
					if (!el || !visible(el)) return false;
					try { el.scrollIntoView({ block: 'center', inline: 'center', behavior: 'instant' }); } catch (e0) {}
					const o = { bubbles: true, cancelable: true, view: window, composed: true };
					try {
						const r = el.getBoundingClientRect();
						const cx = Math.floor(r.left + r.width / 2);
						const cy = Math.floor(r.top + r.height / 2);
						el.dispatchEvent(new PointerEvent('pointerdown', Object.assign({ pointerId: 1, pointerType: 'mouse', clientX: cx, clientY: cy, isPrimary: true }, o)));
						el.dispatchEvent(new PointerEvent('pointerup', Object.assign({ pointerId: 1, pointerType: 'mouse', clientX: cx, clientY: cy, isPrimary: true }, o)));
						el.dispatchEvent(new MouseEvent('mousedown', Object.assign({ clientX: cx, clientY: cy }, o)));
						el.dispatchEvent(new MouseEvent('mouseup', Object.assign({ clientX: cx, clientY: cy }, o)));
						el.dispatchEvent(new MouseEvent('click', Object.assign({ clientX: cx, clientY: cy }, o)));
					} catch (e1) {}
					if (typeof el.click === 'function') {
						try { el.click(); } catch (e2) {}
					}
					return true;
				}
				const byJsname = document.querySelector('button[jsname=""KbvHGe""]')
					|| document.querySelector('[jsname=""KbvHGe""]')
					|| document.querySelector('div[jsname=""KbvHGe""]');
				if (byJsname && scrollAndFire(byJsname)) return true;
				const mat = document.querySelector('button.VfPpkd-LgbsSe, .VfPpkd-LgbsSe[role=""button""]');
				if (mat) {
					const t = (mat.innerText || mat.textContent || '').trim();
					if (/^done\.?$/i.test(t) && scrollAndFire(mat)) return true;
				}
				const candidates = document.querySelectorAll('button, [role=""button""], div[role=""button""]');
				for (const el of candidates) {
					const raw = (el.innerText || el.textContent || '').replace(/\s+/g, ' ').trim();
					if (!raw) continue;
					const u = raw.toUpperCase();
					if (u === 'DONE' || u === 'DONE.' || (u.startsWith('DONE') && raw.length <= 8)) {
						if (scrollAndFire(el)) return true;
					}
				}
				return false;
			}").ConfigureAwait(false);
		}
		catch
		{
			return false;
		}
	}

	/// <summary>Duyệt mọi frame của trang (picker thường là docs.google.com/picker, có thể lồng iframe).</summary>
	private static async Task<bool> TryJsClickPickerDoneInAnyFrameAsync(IPage formPage)
	{
		IFrame[] frames = formPage.Frames.ToArray();
		Array.Sort(frames, (a, b) =>
		{
			bool ap = a.Url != null && a.Url.IndexOf("picker", StringComparison.OrdinalIgnoreCase) >= 0;
			bool bp = b.Url != null && b.Url.IndexOf("picker", StringComparison.OrdinalIgnoreCase) >= 0;
			if (ap == bp)
			{
				return 0;
			}
			return ap ? -1 : 1;
		});
		foreach (IFrame f in frames)
		{
			if (await TryJsClickPickerDoneInFrameDocumentAsync(f).ConfigureAwait(false))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>Picker Google Forms "Select Header" sau khi upload: nút Done dùng jsname KbvHGe, aria-label thường là "Done." (có dấu chấm). Có thể nằm trong iframe picker hoặc dialog trên trang chính.</summary>
	private static async Task ClickGoogleFormsHeaderPickerDoneButtonAsync(IPage formPage, IFrameLocator pickerFrame)
	{
		ILocator headerDialogMain = formPage.Locator("div[role='dialog'][aria-label='Select Header']").Or(formPage.Locator("div[jsname='BleNNd'][role='dialog']"));
		ILocator doneInDialogMain = headerDialogMain.Locator("button[jsname='KbvHGe']");
		ILocator doneInFrameByJsname = pickerFrame.Locator("button[jsname='KbvHGe']").Or(pickerFrame.Locator("[jsname='KbvHGe']"));
		ILocator doneInFrameByRole = pickerFrame.GetByRole(AriaRole.Button, new FrameLocatorGetByRoleOptions
		{
			Name = "Done."
		}).Or(pickerFrame.GetByRole(AriaRole.Button, new FrameLocatorGetByRoleOptions
		{
			Name = "Done"
		})).Or(pickerFrame.GetByRole(AriaRole.Button, new FrameLocatorGetByRoleOptions
		{
			Name = "DONE"
		}));
		ILocator doneInMainByRole = headerDialogMain.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions
		{
			Name = "Done."
		}).Or(headerDialogMain.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions
		{
			Name = "Done"
		}));
		ILocator doneInFrameByText = pickerFrame.Locator("button").Filter(new LocatorFilterOptions
		{
			HasTextRegex = new Regex("^\\s*DONE\\.?\\s*$", RegexOptions.IgnoreCase)
		});
		ILocator[] tryOrder = new ILocator[5] { doneInFrameByJsname, doneInFrameByRole, doneInFrameByText, doneInDialogMain, doneInMainByRole };
		for (int i = 0; i < 50; i++)
		{
			try
			{
				ILocator loadingBar = headerDialogMain.Locator("[jsname='aZ2wEe'][data-active='true']");
				if (await loadingBar.CountAsync() > 0)
				{
					try
					{
						await loadingBar.First.WaitForAsync(new LocatorWaitForOptions
						{
							State = WaitForSelectorState.Hidden,
							Timeout = 4000f
						});
					}
					catch
					{
					}
				}
				ILocator loadingInFrame = pickerFrame.Locator("[jsname='aZ2wEe'][data-active='true']");
				if (await loadingInFrame.CountAsync() > 0)
				{
					try
					{
						await loadingInFrame.First.WaitForAsync(new LocatorWaitForOptions
						{
							State = WaitForSelectorState.Hidden,
							Timeout = 4000f
						});
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
			foreach (ILocator loc in tryOrder)
			{
				try
				{
					if (await loc.CountAsync() == 0)
					{
						continue;
					}
					ILocator first = loc.First;
					if (!await first.IsVisibleAsync())
					{
						continue;
					}
					await first.ScrollIntoViewIfNeededAsync();
					await PlaywrightWaitHelpers.PageWaitAsync(formPage, 200f);
					await first.ClickAsync(new LocatorClickOptions
					{
						Timeout = 10000f,
						Force = true
					});
					return;
				}
				catch
				{
				}
			}
			try
			{
				if (await TryJsClickPickerDoneInAnyFrameAsync(formPage).ConfigureAwait(false))
				{
					await PlaywrightWaitHelpers.PageWaitAsync(formPage, 500f);
					return;
				}
			}
			catch
			{
			}
			await Task.Delay(400);
		}
		try
		{
			bool jsOk = await formPage.EvaluateAsync<bool>("() => {\r\n  const dlg = document.querySelector('div[role=\"dialog\"][aria-label=\"Select Header\"]') || document.querySelector('div[jsname=\"BleNNd\"][role=\"dialog\"]');\r\n  if (!dlg) return false;\r\n  const btn = dlg.querySelector('button[jsname=\"KbvHGe\"]');\r\n  if (!btn) return false;\r\n  btn.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, view: window }));\r\n  return true;\r\n}");
			if (jsOk)
			{
				return;
			}
		}
		catch
		{
		}
		if (await TryJsClickPickerDoneInAnyFrameAsync(formPage).ConfigureAwait(false))
		{
			return;
		}
		throw new TimeoutException("Không bấm được nút Done (Select Header / crop ảnh).");
	}

	private static async Task DismissGoogleFormsAccessControlDialogIfPresentAsync(IPage page, bool waitBeforeCheck = true)
	{
		if (waitBeforeCheck)
		{
			await PlaywrightWaitHelpers.PageWaitAsync(page, 600f);
		}
		for (int i = 0; i < 14; i++)
		{
			if (await PageOrAnyFrameHasAlertDialogAsync(page))
			{
				break;
			}
			if (i == 13)
			{
				return;
			}
			await PlaywrightWaitHelpers.PageWaitAsync(page, 350f);
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
			await PlaywrightWaitHelpers.PageWaitAsync(page, 900f);
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

	/// <summary>Proxy cho hàng: lưới hoặc fallback <c>Data/Account.txt</c> cột 5 — cùng nguồn với đồng bộ GPM / chrome://version.</summary>
	private ProxyInfo GetProxyForAccountRow(int rowIndex)
	{
		if (rowIndex < 0 || rowIndex >= dataGridView1.Rows.Count)
		{
			return null;
		}
		DataGridViewRow row = dataGridView1.Rows[rowIndex];
		if (row.IsNewRow)
		{
			return null;
		}
		string raw = GetProxyRawForRunRow(rowIndex);
		if (string.IsNullOrEmpty(raw))
		{
			return null;
		}
		return TryParseProxyRawLine(raw, out ProxyInfo info) ? info : null;
	}

	/// <summary>Đọc PROXY sau các <c>await</c> có thể không còn trên UI thread — bắt buộc marshal qua <c>Invoke</c>.</summary>
	private ProxyInfo GetProxyForAccountRowOnUi(int rowIndex)
	{
		try
		{
			if (!IsHandleCreated)
			{
				return GetProxyForAccountRow(rowIndex);
			}
			if (!InvokeRequired)
			{
				return GetProxyForAccountRow(rowIndex);
			}
			ProxyInfo r = null;
			Invoke(new Action(() => { r = GetProxyForAccountRow(rowIndex); }));
			return r;
		}
		catch (ObjectDisposedException)
		{
			return null;
		}
		catch (InvalidOperationException)
		{
			return GetProxyForAccountRow(rowIndex);
		}
	}

	/// <summary>Chuỗi thô cột PROXY trên UI thread (dùng khi đồng bộ GPM).</summary>
	private string GetGridProxyRawCellOnUi(int rowIndex)
	{
		try
		{
			if (!IsHandleCreated)
			{
				return ReadGridProxyRawCell(rowIndex);
			}
			if (!InvokeRequired)
			{
				return ReadGridProxyRawCell(rowIndex);
			}
			string r = "";
			Invoke(new Action(() => { r = ReadGridProxyRawCell(rowIndex); }));
			return r ?? "";
		}
		catch
		{
			return ReadGridProxyRawCell(rowIndex);
		}
	}

	private string ReadGridProxyRawCell(int rowIndex)
	{
		if (rowIndex < 0 || rowIndex >= dataGridView1.Rows.Count)
		{
			return "";
		}
		DataGridViewRow row = dataGridView1.Rows[rowIndex];
		if (row.IsNewRow)
		{
			return "";
		}
		return row.Cells["PROXY"].Value?.ToString()?.Trim() ?? "";
	}

	/// <summary>Cùng quy tắc bỏ dòng / đếm hàng như <see cref="SetAccount"/> — cột 5 (index 4) là PROXY.</summary>
	private static string ReadProxyRawFromAccountFileForGridRow(int rowIndex)
	{
		if (rowIndex < 0 || !File.Exists("Data/Account.txt"))
		{
			return "";
		}
		try
		{
			int validIndex = 0;
			foreach (string raw in File.ReadLines("Data/Account.txt"))
			{
				string line = (raw ?? "").Trim();
				if (string.IsNullOrEmpty(line))
				{
					continue;
				}
				string[] array = line.Split(new[] { '|', '\t' }, StringSplitOptions.None);
				if (array.Length < 3)
				{
					continue;
				}
				if (validIndex == rowIndex)
				{
					return array.Length > 4 ? (array[4] ?? "").Trim() : "";
				}
				validIndex++;
			}
		}
		catch
		{
		}
		return "";
	}

	/// <summary>Chuỗi proxy dùng khi chạy: ưu tiên lưới; ô trống thì đọc <c>Data/Account.txt</c> (tránh chỉ sửa file mà không reload lưới).</summary>
	private string GetProxyRawForRunRow(int rowIndex)
	{
		string g = (GetGridProxyRawCellOnUi(rowIndex) ?? "").Trim();
		if (!string.IsNullOrEmpty(g))
		{
			return g;
		}
		return ReadProxyRawFromAccountFileForGridRow(rowIndex);
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
					string[] array3 = text2.Split(new char[2] { '|', '\t' });
					string text3 = array3.Length > 0 ? array3[0].Trim() : "";
					string text4 = array3.Length > 1 ? array3[1].Trim() : "";
					string text5 = array3.Length > 2 ? array3[2].Trim() : "";
					string text6 = array3.Length > 3 ? array3[3].Trim() : "";
					string text7 = array3.Length > 4 ? array3[4].Trim() : "";
					int num2 = dataGridView1.Rows.Add();
					dataGridView1.Rows[num2].Cells["UID"].Value = text3;
					dataGridView1.Rows[num2].Cells["PASS"].Value = text4;
					dataGridView1.Rows[num2].Cells["MA2FA"].Value = text5;
					dataGridView1.Rows[num2].Cells["MAIL2"].Value = text6;
					dataGridView1.Rows[num2].Cells["PROXY"].Value = text7;
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
					string value3 = method_35(selectedRow.Index, "MA2FA");
					string value4 = method_35(selectedRow.Index, "MAIL2");
					string value5 = method_35(selectedRow.Index, "PROXY");
					stringBuilder.AppendLine(string.Join("|", new string[5] { value, value2, value3, value4, value5 }));
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

	private void Form1_FormClosed(object sender, FormClosedEventArgs e)
	{
		try
		{
			_uiToolTip?.Dispose();
			_uiToolTip = null;
		}
		catch
		{
		}
	}

	private int GetSelectedLuong()
	{
		if (cb_luong?.SelectedItem == null)
		{
			return 5;
		}
		string s = cb_luong.SelectedItem.ToString()?.Trim() ?? "";
		if (int.TryParse(s, out int v) && Array.IndexOf(AllowedLuongValues, v) >= 0)
		{
			return v;
		}
		return 5;
	}

	private void RestoreLuongComboFromSavedValue(string raw)
	{
		if (cb_luong == null || cb_luong.Items.Count == 0)
		{
			return;
		}
		if (!int.TryParse((raw ?? "").Trim(), out int v))
		{
			cb_luong.SelectedItem = "5";
			return;
		}
		int best = AllowedLuongValues[0];
		int bestDist = int.MaxValue;
		foreach (int a in AllowedLuongValues)
		{
			int d = Math.Abs(a - v);
			if (d < bestDist)
			{
				bestDist = d;
				best = a;
			}
		}
		cb_luong.SelectedItem = best.ToString();
	}

	private async void Form1_Load(object sender, EventArgs e)
	{
		Text = "Auto Login — GPM | v" + GetAppVersionLabel();
		LoadSettings();
		SetAccount(dataGridView1);
		_runQueueStartRowIndex = 0;
		UpdateGpmGroupControlsVisible();
		lbl_status.AutoSize = false;
		lbl_status.Width = 260;
		lbl_status.Height = 56;
		UpdateStatus();
		await RefreshGpmGroupComboAsync();
		try
		{
			typeof(DataGridView).InvokeMember("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty, null, dataGridView1, new object[1] { true });
			typeof(Control).InvokeMember("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty, null, sidebar, new object[1] { true });
			typeof(Control).InvokeMember("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty, null, topbar, new object[1] { true });
		}
		catch
		{
		}
		sidebar.Paint -= Sidebar_Paint;
		sidebar.Paint += Sidebar_Paint;
		_uiToolTip?.Dispose();
		_uiToolTip = new ToolTip
		{
			AutoPopDelay = 10000,
			InitialDelay = 400,
			ReshowDelay = 200,
			ShowAlways = true
		};
		_uiToolTip.SetToolTip(btn_start, "Chạy đăng nhập cho các hàng đã chọn (hoặc cả lưới). Bỏ qua UID đã login thành công cho nhóm hiện tại hoặc đã thành công ở nhóm GPM khác (login_success.log), và trong Data/dead_*.log. Kiểm tra GPM API :19995 trước khi bắt đầu.");
		_uiToolTip.SetToolTip(btn_stop, "Dừng: đóng trình duyệt và hủy batch đang chờ.");
		_uiToolTip.SetToolTip(txt_so_account_log, "0 hoặc để trống = xử lý mọi dòng có UID trong phạm vi đã chọn.");
		_uiToolTip.SetToolTip(cb_luong, "Số Chrome chạy song song mỗi đợt (không vượt quá số profile GPM).");
		_uiToolTip.SetToolTip(cb_sudungproxy, "Khi tích: mỗi hàng trong hàng đợi phải có PROXY hợp lệ. App đẩy PROXY lên GPM rồi sau khi mở profile đọc chrome://version (Command Line, --proxy-server) so host:port với lưới; lệch thì cột STATUS ghi \"Không tìm thấy proxy tương ứng bên GPM\" và không chạy hàng đó. Định dạng: host:port hoặc host:port:user:pass.");
		_uiToolTip.SetToolTip(cb_gpm_group, "Luôn chọn nhóm GPM: profile A, B, C… trong nhóm khớp hàng 1, 2, 3… (dù tắt \"Proxy…\").");
		_uiToolTip.SetToolTip(cb_changeinfo, "Sau khi đăng nhập: mở myaccount và đổi ảnh đại diện (cần avatar.jpg).");
		_uiToolTip.SetToolTip(cb_tao_form, "Mở Google Forms, điền tiêu đề/mô tả, theme, publish và copy link phản hồi (cần tieude.txt, noidung.txt, header.jpg… trong Data\\).");
		_uiToolTip.SetToolTip(cb_tao_sheet_script, "Tạo Google Sheet mới, mở script.new, dán codesc.txt (thay [LINK_FORM] / [LINK_SHEET]), thêm Drive API và chạy OAuth/Run. Có thể bật một mình: khi không tạo Form, [LINK_FORM] để trống.");
		_uiToolTip.SetToolTip(cb_offchrome, "Sau mỗi account: đóng context Playwright + gọi GPM đóng profile + ngắt CDP (tắt cửa sổ Chrome thật; tiết kiệm RAM). Bỏ tick để giữ tab xem lại.");
	}

	private void Form1_Shown(object sender, EventArgs e)
	{
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
		int ok = Volatile.Read(ref _batchOk);
		int fail = Volatile.Read(ref _batchFail);
		if (_running)
		{
			lbl_status.ForeColor = System.Drawing.Color.FromArgb(130, 210, 255);
			int done = ok + fail;
			string progress = _batchTotalPlanned > 0 ? $" | {done}/{_batchTotalPlanned}" : "";
			string eta = "";
			if (_batchTotalPlanned > 0 && done > 0 && done < _batchTotalPlanned)
			{
				double elapsed = (DateTime.UtcNow - _batchStartedUtc).TotalSeconds;
				if (elapsed >= 4.0)
				{
					double rate = (double)done / elapsed;
					if (rate > 0.0001)
					{
						int remainSec = (int)Math.Ceiling((_batchTotalPlanned - done) / rate);
						eta = $" | ~{remainSec / 60}p{remainSec % 60:D2}s";
					}
				}
			}
			lbl_status.Text = $"Chạy: {_runningThreads} luồng | OK {ok} | Lỗi {fail}{progress}{eta}";
		}
		else if (_lastBatchOk > 0 || _lastBatchFail > 0)
		{
			lbl_status.ForeColor = System.Drawing.Color.FromArgb(170, 215, 175);
			lbl_status.Text = $"Sẵn sàng | Lần trước: OK {_lastBatchOk} — Lỗi {_lastBatchFail}";
		}
		else
		{
			lbl_status.ForeColor = System.Drawing.Color.FromArgb(155, 205, 160);
			lbl_status.Text = "Sẵn sàng";
		}
	}

	private void Sidebar_Paint(object sender, PaintEventArgs e)
	{
		using (Pen pen = new Pen(Color.FromArgb(52, 52, 58), 1f))
		{
			int x = sidebar.Width - 1;
			e.Graphics.DrawLine(pen, x, 0, x, sidebar.Height);
		}
	}

	private void Topbar_LayoutTagline()
	{
		if (topbar == null || lbl_app_tagline == null || btn_export_diagnostics == null)
		{
			return;
		}
		int x = btn_export_diagnostics.Right + 12;
		int w = Math.Max(120, topbar.ClientSize.Width - x - 16);
		int h = TextRenderer.MeasureText(lbl_app_tagline.Text, lbl_app_tagline.Font, new Size(w, int.MaxValue), TextFormatFlags.WordBreak).Height;
		h = Math.Max(20, Math.Min(h + 4, topbar.ClientSize.Height - 8));
		lbl_app_tagline.SetBounds(x, (topbar.ClientSize.Height - h) / 2, w, h);
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
				string value5 = item.Cells["PROXY"]?.Value?.ToString() ?? "";
				list.Add($"{value}|{value2}|{value3}|{value4}|{value5}");
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
				// username setting removed
			}
			if (dictionary.ContainsKey("luong"))
			{
				RestoreLuongComboFromSavedValue(dictionary["luong"]);
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
			bool loadedTaoForm = false;
			bool loadedTaoSheetScript = false;
			if (dictionary.ContainsKey("tao_form") && bool.TryParse(dictionary["tao_form"], out var tf))
			{
				cb_tao_form.Checked = tf;
				loadedTaoForm = true;
			}
			if (dictionary.ContainsKey("tao_sheet_script") && bool.TryParse(dictionary["tao_sheet_script"], out var tss))
			{
				cb_tao_sheet_script.Checked = tss;
				loadedTaoSheetScript = true;
			}
			if (!loadedTaoForm && !loadedTaoSheetScript && dictionary.ContainsKey("taoform") && bool.TryParse(dictionary["taoform"], out var legacyTao))
			{
				cb_tao_form.Checked = legacyTao;
				cb_tao_sheet_script.Checked = legacyTao;
			}
			if (dictionary.ContainsKey("offchrome") && bool.TryParse(dictionary["offchrome"], out var result4))
			{
				cb_offchrome.Checked = result4;
			}
			if (dictionary.ContainsKey("wait_slice_ms") && int.TryParse(dictionary["wait_slice_ms"], out int wsm) && wsm >= 50 && wsm <= 2000)
			{
				_waitSliceMs = wsm;
			}
			if (dictionary.ContainsKey("script_run_pause_ms") && int.TryParse(dictionary["script_run_pause_ms"], out int srpm) && srpm >= 1000 && srpm <= 120000)
			{
				_scriptRunPauseMs = srpm;
			}
			ApplySavedWindowPlacement(dictionary);
		}
		catch (Exception)
		{
		}
	}

	private void ApplySavedWindowPlacement(Dictionary<string, string> dictionary)
	{
		try
		{
			bool wantMax = dictionary.TryGetValue("form_maximized", out string fm) && bool.TryParse(fm, out bool mx) && mx;
			int x = 0;
			int y = 0;
			int w = 0;
			int h = 0;
			bool haveGeom = dictionary.TryGetValue("form_x", out string sx) && int.TryParse(sx, out x) && dictionary.TryGetValue("form_y", out string sy) && int.TryParse(sy, out y) && dictionary.TryGetValue("form_w", out string sw) && int.TryParse(sw, out w) && dictionary.TryGetValue("form_h", out string sh) && int.TryParse(sh, out h) && w >= MinimumSize.Width && h >= MinimumSize.Height;
			if (!haveGeom)
			{
				if (wantMax)
				{
					WindowState = FormWindowState.Maximized;
				}
				return;
			}
			Rectangle wa = Screen.GetWorkingArea(this);
			w = Math.Min(Math.Max(w, MinimumSize.Width), wa.Width);
			h = Math.Min(Math.Max(h, MinimumSize.Height), wa.Height);
			if (x + w < wa.Left + 40)
			{
				x = wa.Right - w - 40;
			}
			if (y + h < wa.Top + 40)
			{
				y = wa.Bottom - h - 40;
			}
			if (x > wa.Right - 40)
			{
				x = wa.Left + 20;
			}
			if (y > wa.Bottom - 40)
			{
				y = wa.Top + 20;
			}
			StartPosition = FormStartPosition.Manual;
			Bounds = new Rectangle(x, y, w, h);
			if (wantMax)
			{
				WindowState = FormWindowState.Maximized;
			}
		}
		catch
		{
		}
	}

	private void SaveSettings()
	{
		if (!Directory.Exists("Data"))
		{
			Directory.CreateDirectory("Data");
		}
		Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			if (File.Exists("Data/Setting.txt"))
			{
				foreach (string line in File.ReadAllLines("Data/Setting.txt"))
				{
					if (string.IsNullOrWhiteSpace(line))
					{
						continue;
					}
					string[] p = line.Split(new char[1] { '=' }, 2);
					if (p.Length == 2)
					{
						d[p[0].Trim()] = p[1].Trim();
					}
				}
			}
		}
		catch
		{
		}
		d["so_account_log"] = txt_so_account_log.Text;
		d["luong"] = GetSelectedLuong().ToString();
		d["sudungproxy"] = cb_sudungproxy.Checked.ToString();
		d["gpm_proxy_group_id"] = GetSelectedGpmGroupId() ?? "";
		d["changeinfo"] = cb_changeinfo.Checked.ToString();
		d["tao_form"] = cb_tao_form.Checked.ToString();
		d["tao_sheet_script"] = cb_tao_sheet_script.Checked.ToString();
		d["taoform"] = (cb_tao_form.Checked && cb_tao_sheet_script.Checked).ToString();
		d["offchrome"] = cb_offchrome.Checked.ToString();
		d["wait_slice_ms"] = _waitSliceMs.ToString();
		d["script_run_pause_ms"] = _scriptRunPauseMs.ToString();
		try
		{
			if (WindowState == FormWindowState.Normal)
			{
				d["form_maximized"] = "False";
				d["form_x"] = Location.X.ToString();
				d["form_y"] = Location.Y.ToString();
				d["form_w"] = Width.ToString();
				d["form_h"] = Height.ToString();
			}
			else if (WindowState == FormWindowState.Maximized)
			{
				d["form_maximized"] = "True";
				Rectangle rb = RestoreBounds;
				if (rb.Width > 0 && rb.Height > 0)
				{
					d["form_x"] = rb.X.ToString();
					d["form_y"] = rb.Y.ToString();
					d["form_w"] = rb.Width.ToString();
					d["form_h"] = rb.Height.ToString();
				}
			}
		}
		catch
		{
		}
		List<string> lines = new List<string>();
		foreach (KeyValuePair<string, string> kv in d.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
		{
			lines.Add(kv.Key + "=" + kv.Value);
		}
		File.WriteAllLines("Data/Setting.txt", lines);
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
			ShutdownAutomationLogWriter();
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

	private static string EscapeCsvField(string value)
	{
		string s = value ?? "";
		if (s.IndexOfAny(new char[4] { '"', ',', '\r', '\n' }) >= 0)
		{
			return "\"" + s.Replace("\"", "\"\"") + "\"";
		}
		return s;
	}

	private void xuatCsvLuoiToolStripMenuItem_Click(object sender, EventArgs e)
	{
		try
		{
			using SaveFileDialog dlg = new SaveFileDialog
			{
				Filter = "CSV (*.csv)|*.csv",
				FileName = "luoi_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv",
				OverwritePrompt = true
			};
			if (dlg.ShowDialog() != DialogResult.OK)
			{
				return;
			}
			StringBuilder sb = new StringBuilder();
			bool firstCol = true;
			foreach (DataGridViewColumn col in dataGridView1.Columns)
			{
				if (!col.Visible)
				{
					continue;
				}
				if (!firstCol)
				{
					sb.Append(',');
				}
				sb.Append(EscapeCsvField(col.HeaderText));
				firstCol = false;
			}
			sb.AppendLine();
			foreach (DataGridViewRow row in dataGridView1.Rows)
			{
				if (row.IsNewRow)
				{
					continue;
				}
				firstCol = true;
				foreach (DataGridViewColumn col in dataGridView1.Columns)
				{
					if (!col.Visible)
					{
						continue;
					}
					if (!firstCol)
					{
						sb.Append(',');
					}
					sb.Append(EscapeCsvField(row.Cells[col.Name]?.Value?.ToString() ?? ""));
					firstCol = false;
				}
				sb.AppendLine();
			}
			File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			AppendAutomationLog("INFO", null, null, "Xuất CSV lưới: " + dlg.FileName);
			MessageBox.Show("Đã lưu:\n" + dlg.FileName, "Xuất CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Lỗi xuất CSV:\n" + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Hand);
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
		this.cb_tao_sheet_script = new System.Windows.Forms.CheckBox();
		this.cb_tao_form = new System.Windows.Forms.CheckBox();
		this.cb_changeinfo = new System.Windows.Forms.CheckBox();
		this.label3 = new System.Windows.Forms.Label();
		this.txt_so_account_log = new System.Windows.Forms.TextBox();
		this.lbl_status = new System.Windows.Forms.Label();
		this.label2 = new System.Windows.Forms.Label();
		this.cb_luong = new System.Windows.Forms.ComboBox();
		this.cb_sudungproxy = new System.Windows.Forms.CheckBox();
		this.lbl_gpm_group = new System.Windows.Forms.Label();
		this.cb_gpm_group = new System.Windows.Forms.ComboBox();
		this.topbar = new System.Windows.Forms.Panel();
		this.lbl_app_tagline = new System.Windows.Forms.Label();
		this.btn_export_diagnostics = new System.Windows.Forms.Button();
		this.btn_open_data_folder = new System.Windows.Forms.Button();
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
		this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
		this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
		this.menuMoFile = new System.Windows.Forms.ToolStripMenuItem();
		this.acoountToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.copySelectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.deleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.deleteAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.xuatCookieToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.tieudeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.noidungToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.sciptToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.copy2FAToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.xuatCsvLuoiToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
		this.cb_offchrome = new System.Windows.Forms.CheckBox();
		this.sidebar.SuspendLayout();
		this.topbar.SuspendLayout();
		((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
		this.contextMenuStrip1.SuspendLayout();
		base.SuspendLayout();
		this.sidebar.BackColor = System.Drawing.Color.FromArgb(32, 32, 36);
		this.sidebar.Controls.Add(this.cb_offchrome);
		this.sidebar.Controls.Add(this.cb_tao_sheet_script);
		this.sidebar.Controls.Add(this.cb_tao_form);
		this.sidebar.Controls.Add(this.cb_changeinfo);
		this.sidebar.Controls.Add(this.label3);
		this.sidebar.Controls.Add(this.txt_so_account_log);
		this.sidebar.Controls.Add(this.lbl_status);
		this.sidebar.Controls.Add(this.label2);
		this.sidebar.Controls.Add(this.cb_luong);
		this.sidebar.Controls.Add(this.cb_sudungproxy);
		this.sidebar.Controls.Add(this.lbl_gpm_group);
		this.sidebar.Controls.Add(this.cb_gpm_group);
		this.sidebar.Dock = System.Windows.Forms.DockStyle.Left;
		this.sidebar.Name = "sidebar";
		this.sidebar.Size = new System.Drawing.Size(300, 601);
		this.sidebar.TabIndex = 0;
		this.cb_tao_form.AutoSize = false;
		this.cb_tao_form.Cursor = System.Windows.Forms.Cursors.Hand;
		this.cb_tao_form.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.cb_tao_form.ForeColor = System.Drawing.Color.FromArgb(228, 228, 232);
		this.cb_tao_form.Location = new System.Drawing.Point(16, 312);
		this.cb_tao_form.Name = "cb_tao_form";
		this.cb_tao_form.Size = new System.Drawing.Size(260, 24);
		this.cb_tao_form.TabIndex = 6;
		this.cb_tao_form.Text = "Tạo Form";
		this.cb_tao_sheet_script.AutoSize = false;
		this.cb_tao_sheet_script.Cursor = System.Windows.Forms.Cursors.Hand;
		this.cb_tao_sheet_script.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.cb_tao_sheet_script.ForeColor = System.Drawing.Color.FromArgb(228, 228, 232);
		this.cb_tao_sheet_script.Location = new System.Drawing.Point(16, 340);
		this.cb_tao_sheet_script.Name = "cb_tao_sheet_script";
		this.cb_tao_sheet_script.Size = new System.Drawing.Size(260, 24);
		this.cb_tao_sheet_script.TabIndex = 7;
		this.cb_tao_sheet_script.Text = "Tạo Sheet + Script";
		this.cb_changeinfo.AutoSize = false;
		this.cb_changeinfo.Cursor = System.Windows.Forms.Cursors.Hand;
		this.cb_changeinfo.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.cb_changeinfo.ForeColor = System.Drawing.Color.FromArgb(228, 228, 232);
		this.cb_changeinfo.Location = new System.Drawing.Point(16, 284);
		this.cb_changeinfo.Name = "cb_changeinfo";
		this.cb_changeinfo.Size = new System.Drawing.Size(260, 24);
		this.cb_changeinfo.TabIndex = 5;
		this.cb_changeinfo.Text = "Đổi ảnh / thông tin Gmail";
		this.label3.Font = new System.Drawing.Font("Segoe UI", 8.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.label3.ForeColor = System.Drawing.Color.FromArgb(150, 152, 162);
		this.label3.Location = new System.Drawing.Point(16, 80);
		this.label3.Name = "label3";
		this.label3.Size = new System.Drawing.Size(260, 20);
		this.label3.TabIndex = 10;
		this.label3.TabStop = false;
		this.label3.Text = "Giới hạn số account (0 = chạy tất cả)";
		this.txt_so_account_log.BackColor = System.Drawing.Color.FromArgb(48, 48, 54);
		this.txt_so_account_log.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
		this.txt_so_account_log.ForeColor = System.Drawing.Color.FromArgb(245, 245, 248);
		this.txt_so_account_log.Location = new System.Drawing.Point(16, 104);
		this.txt_so_account_log.Name = "txt_so_account_log";
		this.txt_so_account_log.Size = new System.Drawing.Size(260, 25);
		this.txt_so_account_log.TabIndex = 1;
		this.lbl_status.AutoSize = false;
		this.lbl_status.BackColor = System.Drawing.Color.FromArgb(42, 42, 48);
		this.lbl_status.Font = new System.Drawing.Font("Segoe UI", 9.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.lbl_status.ForeColor = System.Drawing.Color.FromArgb(155, 205, 160);
		this.lbl_status.Location = new System.Drawing.Point(16, 12);
		this.lbl_status.Name = "lbl_status";
		this.lbl_status.Padding = new System.Windows.Forms.Padding(10, 8, 10, 8);
		this.lbl_status.Size = new System.Drawing.Size(260, 56);
		this.lbl_status.TabIndex = 0;
		this.lbl_status.TabStop = false;
		this.lbl_status.Text = "Sẵn sàng";
		this.label2.Font = new System.Drawing.Font("Segoe UI", 8.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.label2.ForeColor = System.Drawing.Color.FromArgb(150, 152, 162);
		this.label2.Location = new System.Drawing.Point(16, 140);
		this.label2.Name = "label2";
		this.label2.Size = new System.Drawing.Size(260, 20);
		this.label2.TabIndex = 3;
		this.label2.TabStop = false;
		this.label2.Text = "Luồng song song (profile GPM)";
		this.cb_luong.BackColor = System.Drawing.Color.FromArgb(48, 48, 54);
		this.cb_luong.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.cb_luong.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.cb_luong.ForeColor = System.Drawing.Color.FromArgb(245, 245, 248);
		this.cb_luong.FormattingEnabled = true;
		this.cb_luong.Items.AddRange(new object[3] { "2", "5", "10" });
		this.cb_luong.Location = new System.Drawing.Point(16, 164);
		this.cb_luong.Name = "cb_luong";
		this.cb_luong.Size = new System.Drawing.Size(260, 25);
		this.cb_luong.TabIndex = 2;
		this.cb_luong.SelectedItem = "5";
		this.cb_sudungproxy.Cursor = System.Windows.Forms.Cursors.Hand;
		this.cb_sudungproxy.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.cb_sudungproxy.ForeColor = System.Drawing.Color.FromArgb(228, 228, 232);
		this.cb_sudungproxy.Location = new System.Drawing.Point(16, 196);
		this.cb_sudungproxy.Name = "cb_sudungproxy";
		this.cb_sudungproxy.Size = new System.Drawing.Size(260, 24);
		this.cb_sudungproxy.TabIndex = 3;
		this.cb_sudungproxy.Text = "Proxy từ cột PROXY (Account.txt)";
		this.cb_sudungproxy.CheckedChanged += new System.EventHandler(cb_sudungproxy_CheckedChanged);
		this.lbl_gpm_group.Font = new System.Drawing.Font("Segoe UI", 8.75f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.lbl_gpm_group.ForeColor = System.Drawing.Color.FromArgb(150, 152, 162);
		this.lbl_gpm_group.Location = new System.Drawing.Point(16, 226);
		this.lbl_gpm_group.Name = "lbl_gpm_group";
		this.lbl_gpm_group.Size = new System.Drawing.Size(260, 20);
		this.lbl_gpm_group.TabIndex = 20;
		this.lbl_gpm_group.TabStop = false;
		this.lbl_gpm_group.Text = "Nhóm profile GPM";
		this.cb_gpm_group.BackColor = System.Drawing.Color.FromArgb(48, 48, 54);
		this.cb_gpm_group.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.cb_gpm_group.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.cb_gpm_group.ForeColor = System.Drawing.Color.FromArgb(245, 245, 248);
		this.cb_gpm_group.Location = new System.Drawing.Point(16, 248);
		this.cb_gpm_group.Name = "cb_gpm_group";
		this.cb_gpm_group.Size = new System.Drawing.Size(260, 25);
		this.cb_gpm_group.TabIndex = 4;
		this.lbl_gpm_group.Visible = true;
		this.cb_gpm_group.Visible = true;
		this.topbar.BackColor = System.Drawing.Color.FromArgb(40, 40, 44);
		this.topbar.Controls.Add(this.lbl_app_tagline);
		this.topbar.Controls.Add(this.btn_export_diagnostics);
		this.topbar.Controls.Add(this.btn_open_data_folder);
		this.topbar.Controls.Add(this.btn_start);
		this.topbar.Controls.Add(this.btn_stop);
		this.topbar.Dock = System.Windows.Forms.DockStyle.Top;
		this.topbar.Name = "topbar";
		this.topbar.Size = new System.Drawing.Size(1184, 58);
		this.topbar.TabIndex = 1;
		this.lbl_app_tagline.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
		this.lbl_app_tagline.AutoSize = false;
		this.lbl_app_tagline.Font = new System.Drawing.Font("Segoe UI", 8.75f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.lbl_app_tagline.ForeColor = System.Drawing.Color.FromArgb(155, 157, 168);
		this.lbl_app_tagline.Location = new System.Drawing.Point(528, 11);
		this.lbl_app_tagline.Name = "lbl_app_tagline";
		this.lbl_app_tagline.Size = new System.Drawing.Size(644, 36);
		this.lbl_app_tagline.TabIndex = 30;
		this.lbl_app_tagline.TabStop = false;
		this.lbl_app_tagline.Text = "Tự động đăng nhập Gmail · GPM Login (API 19995) · Playwright";
		this.lbl_app_tagline.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
		this.btn_start.BackColor = System.Drawing.Color.FromArgb(0, 120, 212);
		this.btn_start.Cursor = System.Windows.Forms.Cursors.Hand;
		this.btn_start.FlatAppearance.BorderSize = 0;
		this.btn_start.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(0, 92, 168);
		this.btn_start.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(28, 151, 234);
		this.btn_start.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.btn_start.Font = new System.Drawing.Font("Segoe UI", 9.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.btn_start.ForeColor = System.Drawing.Color.White;
		this.btn_start.Location = new System.Drawing.Point(16, 11);
		this.btn_start.Name = "btn_start";
		this.btn_start.Size = new System.Drawing.Size(118, 36);
		this.btn_start.TabIndex = 0;
		this.btn_start.Text = "▶  Bắt đầu";
		this.btn_start.UseVisualStyleBackColor = false;
		this.btn_start.Click += new System.EventHandler(btnStart_Click);
		this.btn_stop.BackColor = System.Drawing.Color.FromArgb(168, 52, 56);
		this.btn_stop.Cursor = System.Windows.Forms.Cursors.Hand;
		this.btn_stop.FlatAppearance.BorderSize = 0;
		this.btn_stop.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(130, 40, 44);
		this.btn_stop.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(198, 72, 76);
		this.btn_stop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.btn_stop.Font = new System.Drawing.Font("Segoe UI", 9.25f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		this.btn_stop.ForeColor = System.Drawing.Color.White;
		this.btn_stop.Location = new System.Drawing.Point(142, 11);
		this.btn_stop.Name = "btn_stop";
		this.btn_stop.Size = new System.Drawing.Size(108, 36);
		this.btn_stop.TabIndex = 1;
		this.btn_stop.Text = "■  Dừng";
		this.btn_stop.UseVisualStyleBackColor = false;
		this.btn_stop.Click += new System.EventHandler(btnStop_Click);
		this.btn_open_data_folder.BackColor = System.Drawing.Color.FromArgb(58, 58, 64);
		this.btn_open_data_folder.Cursor = System.Windows.Forms.Cursors.Hand;
		this.btn_open_data_folder.FlatAppearance.BorderSize = 0;
		this.btn_open_data_folder.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(48, 48, 54);
		this.btn_open_data_folder.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(72, 72, 80);
		this.btn_open_data_folder.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.btn_open_data_folder.Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.btn_open_data_folder.ForeColor = System.Drawing.Color.FromArgb(232, 232, 236);
		this.btn_open_data_folder.Location = new System.Drawing.Point(258, 11);
		this.btn_open_data_folder.Name = "btn_open_data_folder";
		this.btn_open_data_folder.Size = new System.Drawing.Size(136, 36);
		this.btn_open_data_folder.TabIndex = 3;
		this.btn_open_data_folder.Text = "Thư mục Data";
		this.btn_open_data_folder.UseVisualStyleBackColor = false;
		this.btn_open_data_folder.Click += new System.EventHandler(btn_open_data_folder_Click);
		this.btn_export_diagnostics.BackColor = System.Drawing.Color.FromArgb(58, 58, 64);
		this.btn_export_diagnostics.Cursor = System.Windows.Forms.Cursors.Hand;
		this.btn_export_diagnostics.FlatAppearance.BorderSize = 0;
		this.btn_export_diagnostics.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(48, 48, 54);
		this.btn_export_diagnostics.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(72, 72, 80);
		this.btn_export_diagnostics.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.btn_export_diagnostics.Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.btn_export_diagnostics.ForeColor = System.Drawing.Color.FromArgb(232, 232, 236);
		this.btn_export_diagnostics.Location = new System.Drawing.Point(400, 11);
		this.btn_export_diagnostics.Name = "btn_export_diagnostics";
		this.btn_export_diagnostics.Size = new System.Drawing.Size(120, 36);
		this.btn_export_diagnostics.TabIndex = 4;
		this.btn_export_diagnostics.Text = "ZIP chẩn đoán";
		this.btn_export_diagnostics.UseVisualStyleBackColor = false;
		this.btn_export_diagnostics.Click += new System.EventHandler(btn_export_diagnostics_Click);
		this.dataGridView1.AllowUserToResizeRows = false;
		this.dataGridView1.BackgroundColor = System.Drawing.Color.FromArgb(24, 24, 28);
		this.dataGridView1.BorderStyle = System.Windows.Forms.BorderStyle.None;
		this.dataGridView1.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
		this.dataGridView1.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
		dataGridViewCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
		dataGridViewCellStyle.BackColor = System.Drawing.Color.FromArgb(46, 46, 52);
		dataGridViewCellStyle.Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 0);
		dataGridViewCellStyle.ForeColor = System.Drawing.Color.FromArgb(236, 236, 240);
		dataGridViewCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(46, 46, 52);
		dataGridViewCellStyle.SelectionForeColor = System.Drawing.Color.FromArgb(236, 236, 240);
		dataGridViewCellStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
		this.dataGridView1.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle;
		this.dataGridView1.ColumnHeadersHeight = 36;
		this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
		this.dataGridView1.Columns.AddRange(this.STT, this.UID, this.PASS, this.MA2FA, this.MAIL2, this.STATUS, this.PROXY);
		this.dataGridView1.ContextMenuStrip = this.contextMenuStrip1;
		dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
		dataGridViewCellStyle2.BackColor = System.Drawing.Color.FromArgb(24, 24, 28);
		dataGridViewCellStyle2.Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		dataGridViewCellStyle2.ForeColor = System.Drawing.Color.FromArgb(232, 232, 236);
		dataGridViewCellStyle2.SelectionBackColor = System.Drawing.Color.FromArgb(0, 120, 212);
		dataGridViewCellStyle2.SelectionForeColor = System.Drawing.Color.White;
		dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
		this.dataGridView1.DefaultCellStyle = dataGridViewCellStyle2;
		this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
		this.dataGridView1.EnableHeadersVisualStyles = false;
		this.dataGridView1.GridColor = System.Drawing.Color.FromArgb(52, 52, 58);
		this.dataGridView1.Name = "dataGridView1";
		this.dataGridView1.RowHeadersVisible = false;
		this.dataGridView1.RowHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
		dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
		dataGridViewCellStyle3.BackColor = System.Drawing.Color.FromArgb(24, 24, 28);
		dataGridViewCellStyle3.Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		dataGridViewCellStyle3.ForeColor = System.Drawing.Color.FromArgb(232, 232, 236);
		dataGridViewCellStyle3.SelectionBackColor = System.Drawing.Color.FromArgb(0, 120, 212);
		dataGridViewCellStyle3.SelectionForeColor = System.Drawing.Color.White;
		dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
		this.dataGridView1.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
		this.dataGridView1.RowTemplate.Height = 28;
		this.dataGridView1.SelectionChanged += new System.EventHandler(DataGridView1_SelectionChanged);
		this.dataGridView1.TabIndex = 2;
		System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyleAlt = new System.Windows.Forms.DataGridViewCellStyle(this.dataGridView1.DefaultCellStyle);
		dataGridViewCellStyleAlt.BackColor = System.Drawing.Color.FromArgb(32, 32, 38);
		this.dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyleAlt;
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
		this.STATUS.HeaderText = "Trạng thái";
		this.STATUS.Name = "STATUS";
		this.STATUS.MinimumWidth = 220;
		this.STATUS.Width = 320;
		this.PROXY.HeaderText = "PROXY";
		this.PROXY.Name = "PROXY";
		this.contextMenuStrip1.Name = "contextMenuStrip1";
		this.contextMenuStrip1.BackColor = System.Drawing.Color.FromArgb(42, 42, 48);
		this.contextMenuStrip1.ForeColor = System.Drawing.Color.FromArgb(242, 242, 246);
		this.contextMenuStrip1.Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);
		this.toolStripMenuItem1.Name = "toolStripMenuItem1";
		this.toolStripMenuItem1.Size = new System.Drawing.Size(220, 22);
		this.toolStripMenuItem1.Text = "Dán mail từ clipboard…";
		this.toolStripMenuItem1.Click += new System.EventHandler(toolStripMenuItem1_Click);
		this.acoountToolStripMenuItem.Name = "acoountToolStripMenuItem";
		this.acoountToolStripMenuItem.Size = new System.Drawing.Size(200, 22);
		this.acoountToolStripMenuItem.Text = "Account.txt";
		this.acoountToolStripMenuItem.Click += new System.EventHandler(acoountToolStripMenuItem_Click);
		this.copySelectToolStripMenuItem.Name = "copySelectToolStripMenuItem";
		this.copySelectToolStripMenuItem.Size = new System.Drawing.Size(220, 22);
		this.copySelectToolStripMenuItem.Text = "Sao chép dòng chọn";
		this.copySelectToolStripMenuItem.Click += new System.EventHandler(copySelectToolStripMenuItem_Click);
		this.deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
		this.deleteToolStripMenuItem.Size = new System.Drawing.Size(220, 22);
		this.deleteToolStripMenuItem.Text = "Xóa dòng";
		this.deleteToolStripMenuItem.Click += new System.EventHandler(deleteToolStripMenuItem_Click);
		this.deleteAllToolStripMenuItem.Name = "deleteAllToolStripMenuItem";
		this.deleteAllToolStripMenuItem.Size = new System.Drawing.Size(220, 22);
		this.deleteAllToolStripMenuItem.Text = "Xóa toàn bộ";
		this.deleteAllToolStripMenuItem.Click += new System.EventHandler(deleteAllToolStripMenuItem_Click);
		this.xuatCookieToolStripMenuItem.Name = "xuatCookieToolStripMenuItem";
		this.xuatCookieToolStripMenuItem.Size = new System.Drawing.Size(220, 22);
		this.xuatCookieToolStripMenuItem.Text = "Xuất cookie (file Cookie\\)";
		this.xuatCookieToolStripMenuItem.Click += new System.EventHandler(xuatCookieToolStripMenuItem_Click);
		this.tieudeToolStripMenuItem.Name = "tieudeToolStripMenuItem";
		this.tieudeToolStripMenuItem.Size = new System.Drawing.Size(200, 22);
		this.tieudeToolStripMenuItem.Text = "tieude.txt";
		this.tieudeToolStripMenuItem.Click += new System.EventHandler(tieudeToolStripMenuItem_Click);
		this.noidungToolStripMenuItem.Name = "noidungToolStripMenuItem";
		this.noidungToolStripMenuItem.Size = new System.Drawing.Size(200, 22);
		this.noidungToolStripMenuItem.Text = "noidung.txt";
		this.noidungToolStripMenuItem.Click += new System.EventHandler(noidungToolStripMenuItem_Click);
		this.sciptToolStripMenuItem.Name = "sciptToolStripMenuItem";
		this.sciptToolStripMenuItem.Size = new System.Drawing.Size(200, 22);
		this.sciptToolStripMenuItem.Text = "codesc.txt";
		this.sciptToolStripMenuItem.Click += new System.EventHandler(sciptToolStripMenuItem_Click);
		this.copy2FAToolStripMenuItem.Name = "copy2FAToolStripMenuItem";
		this.copy2FAToolStripMenuItem.Size = new System.Drawing.Size(220, 22);
		this.copy2FAToolStripMenuItem.Text = "Sao chép mã 2FA (hàng chọn)";
		this.copy2FAToolStripMenuItem.Click += new System.EventHandler(copy2FAToolStripMenuItem_Click);
		this.xuatCsvLuoiToolStripMenuItem.Name = "xuatCsvLuoiToolStripMenuItem";
		this.xuatCsvLuoiToolStripMenuItem.Size = new System.Drawing.Size(220, 22);
		this.xuatCsvLuoiToolStripMenuItem.Text = "Xuất CSV (toàn lưới)";
		this.xuatCsvLuoiToolStripMenuItem.Click += new System.EventHandler(xuatCsvLuoiToolStripMenuItem_Click);
		this.menuMoFile.Name = "menuMoFile";
		this.menuMoFile.Size = new System.Drawing.Size(220, 22);
		this.menuMoFile.Text = "Mở / chỉnh file Data\\";
		this.menuMoFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[]
		{
			this.acoountToolStripMenuItem,
			new System.Windows.Forms.ToolStripSeparator(),
			this.tieudeToolStripMenuItem,
			this.noidungToolStripMenuItem,
			this.sciptToolStripMenuItem
		});
		this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[]
		{
			this.toolStripMenuItem1,
			this.menuMoFile,
			new System.Windows.Forms.ToolStripSeparator(),
			this.copySelectToolStripMenuItem,
			this.copy2FAToolStripMenuItem,
			new System.Windows.Forms.ToolStripSeparator(),
			this.deleteToolStripMenuItem,
			this.deleteAllToolStripMenuItem,
			new System.Windows.Forms.ToolStripSeparator(),
			this.xuatCookieToolStripMenuItem,
			this.xuatCsvLuoiToolStripMenuItem
		});
		this.contextMenuStrip1.Size = new System.Drawing.Size(240, 198);
		this.topbar.Resize += delegate
		{
			Topbar_LayoutTagline();
		};
		this.cb_offchrome.AutoSize = false;
		this.cb_offchrome.Cursor = System.Windows.Forms.Cursors.Hand;
		this.cb_offchrome.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
		this.cb_offchrome.ForeColor = System.Drawing.Color.FromArgb(228, 228, 232);
		this.cb_offchrome.Location = new System.Drawing.Point(16, 368);
		this.cb_offchrome.Name = "cb_offchrome";
		this.cb_offchrome.Size = new System.Drawing.Size(260, 24);
		this.cb_offchrome.TabIndex = 8;
		this.cb_offchrome.Text = "Đóng Chrome sau mỗi account";
		this.BackColor = System.Drawing.Color.FromArgb(24, 24, 28);
		base.ClientSize = new System.Drawing.Size(1200, 700);
		base.Controls.Add(this.dataGridView1);
		base.Controls.Add(this.sidebar);
		base.Controls.Add(this.topbar);
		this.Font = new System.Drawing.Font("Segoe UI", 9.25f);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
		base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
		base.MaximizeBox = true;
		base.MinimumSize = new System.Drawing.Size(1000, 580);
		base.Name = "Form1";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "PlayAPP — Auto Login";
		base.FormClosing += new System.Windows.Forms.FormClosingEventHandler(Form1_FormClosing);
		base.FormClosed += new System.Windows.Forms.FormClosedEventHandler(Form1_FormClosed);
		base.Load += new System.EventHandler(Form1_Load);
		base.Shown += new System.EventHandler(Form1_Shown);
		this.sidebar.ResumeLayout(false);
		this.sidebar.PerformLayout();
		this.topbar.ResumeLayout(false);
		((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
		this.contextMenuStrip1.ResumeLayout(false);
		UpdateGpmGroupControlsVisible();
		Topbar_LayoutTagline();
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
