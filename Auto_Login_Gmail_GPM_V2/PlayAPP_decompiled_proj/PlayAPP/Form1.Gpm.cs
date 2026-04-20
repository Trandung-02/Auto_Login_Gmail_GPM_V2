using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Playwright;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PlayAPP;

public partial class Form1
{
	private const int SidebarPadX = 12;

	private const int SidebarOptsRowGap = 28;

	private void UpdateGpmGroupControlsVisible()
	{
		lbl_gpm_group.Visible = true;
		cb_gpm_group.Visible = true;
		int baseY = cb_gpm_group.Bottom + 10;
		cb_changeinfo.Location = new Point(SidebarPadX, baseY);
		cb_tao_form.Location = new Point(SidebarPadX, baseY + SidebarOptsRowGap);
		cb_tao_sheet_script.Location = new Point(SidebarPadX, baseY + SidebarOptsRowGap * 2);
		cb_offchrome.Location = new Point(SidebarPadX, baseY + SidebarOptsRowGap * 3);
	}

	private async Task RefreshGpmGroupComboAsync()
	{
		try
		{
			using HttpClient client = new HttpClient();
			string groupsJson = await client.GetStringAsync(GpmApi.GroupsEndpoint);
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
			MessageBox.Show("Không tải được danh sách nhóm GPM (" + GpmApi.BaseUrl + ").\n" + ex.Message);
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

	private async Task LoadProfiles(HttpClient client)
	{
		if (client == null)
		{
			throw new ArgumentNullException(nameof(client));
		}
		string groupIdStr = GetSelectedGpmGroupId();
		if (string.IsNullOrEmpty(groupIdStr))
		{
			throw new Exception("Chưa chọn nhóm GPM. Chọn nhóm có profile (thứ tự A–Z trong GPM khớp hàng 1…N trên lưới).");
		}
		string groupName = (cb_gpm_group.SelectedItem as GpmGroupListItem)?.Name ?? "";
		_gpmLoadedGroupSummary = string.IsNullOrEmpty(groupName) ? "id=" + groupIdStr : $"{groupName} (id={groupIdStr})";
		string url = GpmApi.ProfilesListUrl(groupIdStr);
		string jsonText = await client.GetStringAsync(url);
		JObject root = JObject.Parse(jsonText);
		JToken succTok = root["success"];
		if (succTok != null && succTok.Type == JTokenType.Boolean && !succTok.Value<bool>())
		{
			throw new Exception("GPM danh sách profile: " + (root["message"]?.ToString() ?? jsonText));
		}
		_profileIds.Clear();
		JArray dataArr = root["data"] as JArray ?? new JArray();
		foreach (JToken p in dataArr)
		{
			string id = p["id"]?.ToString();
			if (string.IsNullOrEmpty(id))
			{
				continue;
			}
			_profileIds.Add(id);
		}
		if (_profileIds.Count == 0)
		{
			throw new Exception("Không có profile nào trong nhóm GPM: " + _gpmLoadedGroupSummary + ".");
		}
		Console.WriteLine($"Loaded {_profileIds.Count} profiles — {_gpmLoadedGroupSummary}");
	}

	private static bool TrySplitHostPortFromPlaywrightServer(string server, out string host, out string port)
	{
		host = null;
		port = null;
		if (string.IsNullOrWhiteSpace(server))
		{
			return false;
		}
		string s = server.Trim();
		if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
		{
			s = s.Substring(7);
		}
		else if (s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
		{
			s = s.Substring(8);
		}
		int colon = s.LastIndexOf(':');
		if (colon <= 0 || colon >= s.Length - 1)
		{
			return false;
		}
		host = s.Substring(0, colon);
		port = s.Substring(colon + 1);
		return true;
	}

	/// <summary>Thu thập toàn bộ text DOM kể cả shadow (chrome://version WebUI — Command Line nằm trong shadow).</summary>
	private const string JsCollectDocumentTextThroughShadow = @"() => {
  const chunks = [];
  function walk(node) {
    if (!node) return;
    if (node.nodeType === 3) {
      const t = node.nodeValue;
      if (t) chunks.push(t);
      return;
    }
    if (node.nodeType === 1 && node.shadowRoot) {
      walk(node.shadowRoot);
    }
    const ch = node.childNodes;
    if (ch) for (let i = 0; i < ch.length; i++) walk(ch[i]);
  }
  walk(document.documentElement);
  return chunks.join(' ');
}";

	/// <summary>GPM-Browser / Chromium: toàn bộ Command Line nằm trong <c>td#command_line</c> (ảnh HTML bạn gửi).</summary>
	private const string JsGetCommandLineTdText = @"() => {
  const el = document.querySelector('#command_line');
  if (!el) return '';
  return (el.innerText || el.textContent || '').trim();
}";

	private static bool TryHostPortFromProxyServerFlagValue(string flagVal, out string host, out string port)
	{
		host = null;
		port = null;
		flagVal = (flagVal ?? "").Trim().Trim('"').Trim('\'');
		if (string.IsNullOrEmpty(flagVal))
		{
			return false;
		}
		int at = flagVal.LastIndexOf('@');
		if (at >= 0 && at < flagVal.Length - 1)
		{
			flagVal = flagVal.Substring(at + 1);
		}
		string prefixed = flagVal.Contains("://", StringComparison.Ordinal) ? flagVal : ("http://" + flagVal);
		return TrySplitHostPortFromPlaywrightServer(prefixed, out host, out port);
	}

	private static bool TryFindProxyServerFlagArgument(string text, out string argValue)
	{
		argValue = null;
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		Match[] ordered = new[]
		{
			Regex.Match(text, @"--proxy-server\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
			Regex.Match(text, @"--proxy-server\s*=\s*'([^']+)'", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
			Regex.Match(text, @"--proxy-server\s*=\s*(\S+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
		};
		foreach (Match m in ordered)
		{
			if (m.Success && !string.IsNullOrWhiteSpace(m.Groups[1].Value))
			{
				argValue = m.Groups[1].Value.Trim();
				return true;
			}
		}
		return false;
	}

	private static bool TryExtractProxyServerHostPortFromCommandLine(string text, out string host, out string port)
	{
		host = null;
		port = null;
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		if (!TryFindProxyServerFlagArgument(text, out string arg))
		{
			return false;
		}
		return TryHostPortFromProxyServerFlagValue(arg, out host, out port);
	}

	private const string GpmProxyMismatchStatus = "Không tìm thấy proxy tương ứng bên GPM";

	/// <summary>Đọc <c>td#command_line</c> trên <c>chrome://version</c> và so <c>--proxy-server</c> với PROXY lưới. Luôn gọi khi mở profile (tránh bỏ qua khi parse PROXY lỗi nhưng ô vẫn có chữ).</summary>
	private async Task<bool> TryVerifyChromeVersionProxyMatchesGridAsync(IBrowser browser, int rowIndex, string uidForLog)
	{
		string rawCell = (GetProxyRawForRunRow(rowIndex) ?? "").Trim();
		ProxyInfo want = GetProxyForAccountRowOnUi(rowIndex);
		if (!string.IsNullOrEmpty(rawCell) && want == null)
		{
			AppendAutomationLog("WARN", rowIndex, uidForLog, "Cột PROXY có dữ liệu nhưng không đúng định dạng host:port / host:port:user:pass — không so được với GPM (sửa ô PROXY hoặc xóa trống).");
			return false;
		}
		if (want == null)
		{
			return true;
		}
		if (!TrySplitHostPortFromPlaywrightServer(want.Server, out string expHost, out string expPort))
		{
			AppendAutomationLog("WARN", rowIndex, uidForLog, "Không parse được PROXY lưới để so với chrome://version.");
			return false;
		}
		if (browser.Contexts.Count == 0)
		{
			AppendAutomationLog("WARN", rowIndex, uidForLog, "CDP: không có browser context để mở chrome://version.");
			return false;
		}
		IBrowserContext ctx = browser.Contexts[0];
		IPage page = await ctx.NewPageAsync();
		try
		{
			await page.GotoAsync("chrome://version", new PageGotoOptions
			{
				WaitUntil = WaitUntilState.Load,
				Timeout = 60000f
			});
			string cmdTd = await page.EvaluateAsync<string>(JsGetCommandLineTdText);
			string shadowWalk = await page.EvaluateAsync<string>(JsCollectDocumentTextThroughShadow);
			string body = await page.ContentAsync();
			string innerLegacy = await page.EvaluateAsync<string>("() => document.body ? (document.body.innerText || '') : ''");
			string combined = (cmdTd ?? "") + "\n" + (shadowWalk ?? "") + "\n" + (innerLegacy ?? "") + "\n" + (body ?? "");
			string actHost;
			string actPort;
			if (!TryExtractProxyServerHostPortFromCommandLine(cmdTd, out actHost, out actPort) && !TryExtractProxyServerHostPortFromCommandLine(combined, out actHost, out actPort))
			{
				string snippet = (cmdTd ?? "").Length > 0
					? (cmdTd.Length > 320 ? cmdTd.Substring(0, 317) + "..." : cmdTd).Replace('\r', ' ').Replace('\n', ' ')
					: (combined.Length > 0 ? combined.Substring(0, Math.Min(200, combined.Length)).Replace('\r', ' ').Replace('\n', ' ') : "(rỗng)");
				AppendAutomationLog("WARN", rowIndex, uidForLog, "chrome://version: không trích được --proxy-server (#command_line len=" + (cmdTd ?? "").Length + "). Snippet: " + snippet);
				return false;
			}
			bool ok = string.Equals(expHost, actHost, StringComparison.OrdinalIgnoreCase) && string.Equals(expPort, actPort, StringComparison.Ordinal);
			if (!ok)
			{
				AppendAutomationLog("WARN", rowIndex, uidForLog, "PROXY lưới " + expHost + ":" + expPort + " khác --proxy-server trên chrome://version (#command_line: " + actHost + ":" + actPort + ").");
			}
			return ok;
		}
		finally
		{
			try
			{
				await page.CloseAsync();
			}
			catch
			{
			}
		}
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

	private int GetLastFilledAccountRowIndexOnUi()
	{
		try
		{
			if (!IsHandleCreated)
			{
				return GetLastFilledAccountRowIndex(dataGridView1);
			}
			if (!InvokeRequired)
			{
				return GetLastFilledAccountRowIndex(dataGridView1);
			}
			int r = -1;
			Invoke(new Action(() => { r = GetLastFilledAccountRowIndex(dataGridView1); }));
			return r;
		}
		catch
		{
			return GetLastFilledAccountRowIndex(dataGridView1);
		}
	}

	private async Task ApplyProxiesToGpmProfilesAsync(HttpClient client)
	{
		int lastAcc = GetLastFilledAccountRowIndexOnUi();
		if (lastAcc < 0)
		{
			return;
		}
		int n = Math.Min(lastAcc + 1, _profileIds.Count);
		for (int i = 0; i < n; i++)
		{
			_batchToken.ThrowIfCancellationRequested();
			string profileId = _profileIds[i];
			string raw = GetProxyRawForRunRow(i);
			if (string.IsNullOrWhiteSpace(raw))
			{
				continue;
			}
			string body = JsonConvert.SerializeObject(new { raw_proxy = raw.Trim() });
			using StringContent content = new StringContent(body, Encoding.UTF8, "application/json");
			using HttpResponseMessage resp = await client.PostAsync(GpmApi.ProfileUpdateUrl(profileId), content);
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

	private const string GpmNoteTaiKhoanDaChet = "Tài khoản đã chết";

	/// <summary>Ghi chú GPM khi mở Gmail bị chuyển sang trang Restrictions (không coi tài khoản chết).</summary>
	private const string GpmNoteGoogleMyAccountRestrictions = "Google: myaccount/restrictions";

	private const string GpmNoteLogMailMoiRecaptcha = "Log mail mới: reCAPTCHA/Verify";

	private const string GpmNoteLogMailMoiAccountDisabled = "Log mail mới: Account disabled";

	/// <summary>Id profile GPM cho hàng lưới: snapshot chết → dict mở CDP → nhóm theo hàng.</summary>
	private string ResolveGpmProfileIdForRow(int rowIndex, string preferredProfileIdFromDeadSnapshot)
	{
		try
		{
			string profileId = (preferredProfileIdFromDeadSnapshot ?? "").Trim();
			if (string.IsNullOrEmpty(profileId) && _gpmProfileIdOpenedForRow.TryGetValue(rowIndex, out string opened))
			{
				profileId = (opened ?? "").Trim();
			}
			if (string.IsNullOrEmpty(profileId) && rowIndex >= 0 && rowIndex < _profileIds.Count)
			{
				profileId = (_profileIds[rowIndex] ?? "").Trim();
			}
			return string.IsNullOrEmpty(profileId) ? null : profileId;
		}
		catch
		{
			return null;
		}
	}

	/// <summary>POST cập nhật trường <c>note</c> (Ghi chú trong GPM).</summary>
	/// <param name="okLogSuffix">Nối vào log INFO khi thành công (vd. ngữ cảnh xóa profile thất bại).</param>
	private async Task TryGpmSetProfileNoteAsync(string profileId, int rowIndex, string note, string okLogSuffix = null)
	{
		if (string.IsNullOrWhiteSpace(profileId))
		{
			return;
		}
		string n = (note ?? "").Trim();
		if (string.IsNullOrEmpty(n))
		{
			return;
		}
		try
		{
			using HttpClient http = new HttpClient();
			http.Timeout = TimeSpan.FromSeconds(35.0);
			string body = JsonConvert.SerializeObject(new { note = n });
			using StringContent content = new StringContent(body, Encoding.UTF8, "application/json");
			using HttpResponseMessage resp = await http.PostAsync(GpmApi.ProfileUpdateUrl(profileId.Trim()), content);
			string respText = await resp.Content.ReadAsStringAsync();
			bool ok = resp.IsSuccessStatusCode;
			try
			{
				JObject jo = JObject.Parse(respText);
				if (jo["success"] != null && jo["success"].Type == JTokenType.Boolean)
				{
					ok = jo["success"].Value<bool>();
				}
			}
			catch
			{
			}
			string respPreview = (respText ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
			if (respPreview.Length > 200)
			{
				respPreview = respPreview.Substring(0, 200) + "…";
			}
			if (ok)
			{
				string suffix = string.IsNullOrEmpty(okLogSuffix) ? "" : okLogSuffix;
				AppendAutomationLog("INFO", rowIndex, null, "GPM: đã ghi chú profile id=" + profileId + " — \"" + n + "\"" + suffix + " | API trả: " + respPreview);
			}
			else
			{
				AppendAutomationLog("WARN", rowIndex, null, "GPM: không ghi chú được profile id=" + profileId + " HTTP " + (int)resp.StatusCode + " " + respPreview);
			}
		}
		catch (Exception ex)
		{
			AppendAutomationLog("WARN", rowIndex, null, "GPM: lỗi khi POST ghi chú profile: " + ex.Message);
		}
	}

	/// <summary>POST cập nhật <c>note</c> khi không xóa được profile (tài khoản chết).</summary>
	private Task TryGpmSetProfileNoteDeadAccountAsync(string profileId, int rowIndex)
	{
		return TryGpmSetProfileNoteAsync(profileId, rowIndex, GpmNoteTaiKhoanDaChet, " (xóa profile không thành công)");
	}

	/// <summary>Ghi id profile GPM cần xóa khi hàng bị coi chết (ưu tiên profile đã mở CDP cho hàng đó).</summary>
	private void RememberGpmProfileIdForDeadAccountRow(int vitri)
	{
		try
		{
			string pid = ResolveGpmProfileIdForRow(vitri, null);
			if (!string.IsNullOrEmpty(pid))
			{
				_deadAccountGpmProfileIdByRow[vitri] = pid;
			}
		}
		catch
		{
		}
	}

	/// <summary>GPM mặc định đôi khi mở cửa sổ ngoài vùng nhìn thấy; gắn win_pos/win_size theo lưới vừa màn hình.</summary>
	private static void ComputeBrowserTileLayout(int countInBatch, out int cols, out int winW, out int winH, out int gapPx)
	{
		gapPx = 10;
		if (countInBatch <= 0)
		{
			cols = 1;
			winW = 400;
			winH = 600;
			return;
		}
		Rectangle wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1040);
		const int edge = 8;
		const int minW = 280;
		const int minH = 360;
		int availW = Math.Max(640, wa.Width - 2 * edge);
		int availH = Math.Max(400, wa.Height - 2 * edge);
		cols = 1;
		winW = minW;
		winH = minH;
		for (int tryCols = Math.Min(countInBatch, 6); tryCols >= 1; tryCols--)
		{
			int tryRows = (int)Math.Ceiling((double)countInBatch / tryCols);
			int w = (availW - gapPx * (tryCols + 1)) / tryCols;
			int h = (availH - gapPx * (tryRows + 1)) / tryRows;
			if (w >= minW && h >= minH)
			{
				cols = tryCols;
				winW = w;
				winH = h;
				return;
			}
		}
		cols = Math.Min(countInBatch, 5);
		int rows2 = (int)Math.Ceiling((double)countInBatch / cols);
		winW = Math.Max(240, (availW - gapPx * (cols + 1)) / cols);
		winH = Math.Max(300, (availH - gapPx * (rows2 + 1)) / rows2);
	}

	private string BuildGpmProfileStartUrl(string profileId, int slotIndexInBatch, int cols, int winW, int winH, int gapPx)
	{
		Rectangle wa = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1040);
		string path = GpmApi.ProfileStartUrl(profileId);
		int col = slotIndexInBatch % cols;
		int row = slotIndexInBatch / cols;
		int x = wa.Left + 8 + col * (winW + gapPx);
		int y = wa.Top + 8 + row * (winH + gapPx);
		return path + "?win_scale=1&win_pos=" + x + "," + y + "&win_size=" + winW + "," + winH;
	}

	/// <summary>Đóng profile qua GPM API (GET /api/v3/profiles/close/{id}) — cần để cửa sổ Chrome thật sự tắt khi chỉ ConnectOverCDP.</summary>
	private async Task TryGpmCloseProfileByRowAsync(int rowIndex)
	{
		try
		{
			string profileId = ResolveGpmProfileIdForRow(rowIndex, null);
			if (string.IsNullOrEmpty(profileId))
			{
				return;
			}
			using HttpClient http = new HttpClient();
			http.Timeout = TimeSpan.FromSeconds(25.0);
			string url = GpmApi.ProfileCloseUrl(profileId);
			await http.GetAsync(url);
		}
		catch
		{
		}
	}

	/// <summary>Xóa profile GPM (tài khoản chết). Nếu API xóa không thành công → POST <c>note</c> = «Tài khoản đã chết» (Ghi chú trong GPM).</summary>
	private async Task TryGpmDeleteProfileByRowAsync(int rowIndex, string preferredProfileIdFromDeadSnapshot = null)
	{
		string profileId = ResolveGpmProfileIdForRow(rowIndex, preferredProfileIdFromDeadSnapshot);
		if (string.IsNullOrEmpty(profileId))
		{
			AppendAutomationLog("WARN", rowIndex, null, "GPM: không xóa/ghi chú profile (tài khoản chết) — không xác định được profile id (mở CDP / nhóm).");
			return;
		}
		bool deletedOk = false;
		try
		{
			using HttpClient http = new HttpClient();
			http.Timeout = TimeSpan.FromSeconds(60.0);
			string url = GpmApi.ProfileDeleteUrl(profileId);
			using HttpResponseMessage resp = await http.GetAsync(url);
			string body = await resp.Content.ReadAsStringAsync();
			bool ok = resp.IsSuccessStatusCode;
			try
			{
				JToken root = JToken.Parse(body);
				JToken succ = root["success"];
				if (succ != null && succ.Type == JTokenType.Boolean)
				{
					ok = (bool)succ;
				}
			}
			catch
			{
			}
			deletedOk = ok;
			if (ok)
			{
				AppendAutomationLog("INFO", rowIndex, null, "GPM: đã xóa profile (tài khoản chết) id=" + profileId);
			}
			else
			{
				AppendAutomationLog("WARN", rowIndex, null, "GPM: xóa profile thất bại id=" + profileId + " HTTP " + (int)resp.StatusCode + " " + (body ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim() + " — sẽ thử ghi chú \"" + GpmNoteTaiKhoanDaChet + "\".");
			}
		}
		catch (Exception ex)
		{
			AppendAutomationLog("WARN", rowIndex, null, "GPM: lỗi khi xóa profile id=" + profileId + ": " + ex.Message + " — sẽ thử ghi chú \"" + GpmNoteTaiKhoanDaChet + "\".");
		}
		if (!deletedOk)
		{
			await TryGpmSetProfileNoteDeadAccountAsync(profileId, rowIndex);
		}
	}

	private async Task<List<(string uid, string pass, string ma2fa, string mail2, int rowIndex)>> LaunchBrowserBatchAsync(HttpClient client, List<(string uid, string pass, string ma2fa, string mail2, int rowIndex)> slice)
	{
		ChromeWindowNativeHelper.MinimizeAllChromeMainWindowsIfOverThreshold(ChromeWindowNativeHelper.MinimizeWhenChromeWindowCountExceeds);
		ComputeBrowserTileLayout(slice.Count, out int tileCols, out int tileW, out int tileH, out int tileGap);
		_gpmProfileIdOpenedForRow.Clear();
		_deadAccountGpmProfileIdByRow.Clear();
		List<(string uid, string pass, string ma2fa, string mail2, int rowIndex)> okSlice = new List<(string uid, string pass, string ma2fa, string mail2, int rowIndex)>();
		for (int bi = 0; bi < slice.Count; bi++)
		{
			var item = slice[bi];
			if (!_running || _batchToken.IsCancellationRequested)
			{
				break;
			}
			if (item.rowIndex < 0 || item.rowIndex >= _profileIds.Count)
			{
				throw new Exception($"Hàng account {item.rowIndex + 1} không có profile tương ứng trong nhóm (nhóm có {_profileIds.Count} profile).");
			}
			string profileId = _profileIds[item.rowIndex];
			_gpmProfileIdOpenedForRow[item.rowIndex] = profileId;
			string startUrl = BuildGpmProfileStartUrl(profileId, bi, tileCols, tileW, tileH, tileGap);
			dynamic json = JsonConvert.DeserializeObject(await client.GetStringAsync(startUrl));
			string debugAddress = json.data.remote_debugging_address;
			string versionJson = await client.GetStringAsync("http://" + debugAddress + "/json/version");
			Console.WriteLine("VERSION JSON:");
			Console.WriteLine(versionJson);
			dynamic version = JsonConvert.DeserializeObject(versionJson);
			string wsEndpoint = version.webSocketDebuggerUrl;
			Console.WriteLine("WS ENDPOINT: " + wsEndpoint);
			IBrowser browser;
			try
			{
				browser = await _playwright.Chromium.ConnectOverCDPAsync(wsEndpoint);
			}
			catch (Exception ex)
			{
				AppendAutomationLog("ERROR", item.rowIndex, item.uid, "ConnectOverCDP: " + ex.Message);
				SetText(item.rowIndex, "STATUS", "Lỗi mở GPM/CDP");
				_gpmProfileIdOpenedForRow.TryRemove(item.rowIndex, out _);
				try
				{
					await TryGpmCloseProfileByRowAsync(item.rowIndex);
				}
				catch
				{
				}
				continue;
			}
			try
			{
				if (!await TryVerifyChromeVersionProxyMatchesGridAsync(browser, item.rowIndex, item.uid))
				{
					try
					{
						await browser.CloseAsync();
					}
					catch
					{
					}
					_gpmProfileIdOpenedForRow.TryRemove(item.rowIndex, out _);
					try
					{
						await TryGpmCloseProfileByRowAsync(item.rowIndex);
					}
					catch
					{
					}
					SetText(item.rowIndex, "STATUS", GpmProxyMismatchStatus);
					AppendAutomationLog("WARN", item.rowIndex, item.uid, "Đã đóng profile: chrome://version không khớp PROXY lưới.");
					continue;
				}
				_browsers.Add(browser);
				okSlice.Add(item);
				Console.WriteLine("Opened profile: " + profileId);
			}
			catch (Exception ex)
			{
				try
				{
					await browser.CloseAsync();
				}
				catch
				{
				}
				_gpmProfileIdOpenedForRow.TryRemove(item.rowIndex, out _);
				try
				{
					await TryGpmCloseProfileByRowAsync(item.rowIndex);
				}
				catch
				{
				}
				AppendAutomationLog("ERROR", item.rowIndex, item.uid, "Sau CDP (chrome://version): " + ex.Message);
				SetText(item.rowIndex, "STATUS", "Lỗi kiểm tra proxy GPM");
				continue;
			}
		}
		return okSlice;
	}
}
