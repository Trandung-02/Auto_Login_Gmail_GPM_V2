using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Playwright;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PlayAPP;

public partial class Form1
{
	private void UpdateGpmGroupControlsVisible()
	{
		bool on = cb_sudungproxy.Checked;
		lbl_gpm_group.Visible = on;
		cb_gpm_group.Visible = on;
		int y = on ? 258 : 218;
		cb_changeinfo.Location = new Point(14, y);
		cb_taoform.Location = new Point(14, y + 26);
		cb_offchrome.Location = new Point(14, y + 52);
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
			url = GpmApi.ProfilesListUrl(groupIdStr);
		}
		else
		{
			_gpmLoadedGroupSummary = "tất cả nhóm";
			url = GpmApi.ProfilesListUrl(null);
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
			_batchToken.ThrowIfCancellationRequested();
			string profileId = _profileIds[i];
			string raw = _proxyList[i].RawLineForGpm;
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

	private string GetNextProfileId()
	{
		int num = Interlocked.Increment(ref _currentProfileIndex);
		num %= _profileIds.Count;
		return _profileIds[num];
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
			string profileId = null;
			if (cb_sudungproxy.Checked && rowIndex >= 0 && rowIndex < _profileIds.Count)
			{
				profileId = (_profileIds[rowIndex] ?? "").Trim();
			}
			if (string.IsNullOrEmpty(profileId))
			{
				_gpmProfileIdOpenedForRow.TryGetValue(rowIndex, out string opened);
				profileId = (opened ?? "").Trim();
			}
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

	/// <summary>Xóa hẳn profile trên GPM (GET /api/v3/profiles/delete/{id}?mode=2 — DB + storage). Gọi sau khi đã đóng profile (tài khoản chết).</summary>
	private async Task TryGpmDeleteProfileByRowAsync(int rowIndex)
	{
		try
		{
			string profileId = null;
			if (cb_sudungproxy.Checked && rowIndex >= 0 && rowIndex < _profileIds.Count)
			{
				profileId = (_profileIds[rowIndex] ?? "").Trim();
			}
			if (string.IsNullOrEmpty(profileId))
			{
				_gpmProfileIdOpenedForRow.TryGetValue(rowIndex, out string opened);
				profileId = (opened ?? "").Trim();
			}
			if (string.IsNullOrEmpty(profileId))
			{
				return;
			}
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
			if (ok)
			{
				AppendAutomationLog("INFO", rowIndex, null, "GPM: đã xóa profile (tài khoản chết) id=" + profileId);
			}
			else
			{
				AppendAutomationLog("WARN", rowIndex, null, "GPM: xóa profile có thể thất bại id=" + profileId + " HTTP " + (int)resp.StatusCode + " " + (body ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim());
			}
		}
		catch (Exception ex)
		{
			AppendAutomationLog("WARN", rowIndex, null, "GPM: lỗi khi xóa profile: " + ex.Message);
		}
	}

	private async Task LaunchBrowserBatchAsync(HttpClient client, List<(string uid, string pass, string ma2fa, string mail2, int rowIndex)> slice)
	{
		ChromeWindowNativeHelper.MinimizeAllChromeMainWindowsIfOverThreshold(ChromeWindowNativeHelper.MinimizeWhenChromeWindowCountExceeds);
		ComputeBrowserTileLayout(slice.Count, out int tileCols, out int tileW, out int tileH, out int tileGap);
		_gpmProfileIdOpenedForRow.Clear();
		for (int bi = 0; bi < slice.Count; bi++)
		{
			var item = slice[bi];
			if (!_running || _batchToken.IsCancellationRequested)
			{
				break;
			}
			string profileId = cb_sudungproxy.Checked ? _profileIds[item.rowIndex] : GetNextProfileId();
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
			IBrowser browser = await _playwright.Chromium.ConnectOverCDPAsync(wsEndpoint);
			_browsers.Add(browser);
			Console.WriteLine("Opened profile: " + profileId);
		}
	}
}
