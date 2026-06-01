using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlayAPP;

public partial class Form1
{
	/// <summary>Tên thư mục mẹ (trong Data\) chứa các thư mục con PDF cần upload lên Google Drive.</summary>
	private const string DriveUploadParentFolderName = "DriveUpload";

	/// <summary>Số thư mục con cần có trong thư mục mẹ. Mỗi thư mục con chứa 1 file PDF.</summary>
	private const int DriveUploadSubFolderCount = 12;

	/// <summary>Tổng số tab Google Drive mở để upload: 11 thư mục đầu × 2 tab + thư mục cuối × 3 tab = 25.</summary>
	private const int DriveUploadTotalTabs = 25;

	/// <summary>Đường dẫn thư mục mẹ: ...\Data\DriveUpload (không tạo, chỉ tính đường dẫn).</summary>
	private static string GetDriveUploadParentDir()
	{
		string baseDir = AppDomain.CurrentDomain.BaseDirectory;
		return Path.Combine(baseDir, "Data", DriveUploadParentFolderName);
	}

	/// <summary>Đường dẫn thư mục con thứ index (1-based hiển thị, tên "01".."12").</summary>
	private static string GetDriveUploadSubDir(string parentDir, int folderIndexZeroBased)
	{
		string name = (folderIndexZeroBased + 1).ToString("D2");
		return Path.Combine(parentDir, name);
	}

	/// <summary>Tạo (nếu chưa có) thư mục mẹ + 12 thư mục con trong Data\. An toàn khi gọi nhiều lần.</summary>
	private string EnsureDriveUploadFoldersExist()
	{
		string parentDir = GetDriveUploadParentDir();
		Directory.CreateDirectory(parentDir);
		for (int i = 0; i < DriveUploadSubFolderCount; i++)
		{
			Directory.CreateDirectory(GetDriveUploadSubDir(parentDir, i));
		}
		return parentDir;
	}

	/// <summary>Tìm 1 file PDF trong thư mục con (lấy file đầu theo tên). Trả null nếu thư mục thiếu / không có PDF.</summary>
	private static string FindSinglePdfInSubFolder(string subDir)
	{
		try
		{
			if (string.IsNullOrEmpty(subDir) || !Directory.Exists(subDir))
			{
				return null;
			}
			string[] pdfs = Directory.GetFiles(subDir, "*.pdf", SearchOption.TopDirectoryOnly);
			if (pdfs == null || pdfs.Length == 0)
			{
				return null;
			}
			Array.Sort(pdfs, StringComparer.OrdinalIgnoreCase);
			return pdfs[0];
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Sinh kế hoạch phân bổ tab: trả về danh sách <paramref name="folderIndex"/> theo từng tab.
	/// 11 thư mục đầu (0..10) mỗi thư mục 2 tab, thư mục cuối (11) 3 tab → đúng 25 phần tử.
	/// Hàm thuần (không I/O) để dễ kiểm thử và tránh lệch tổng.
	/// </summary>
	/// <summary>Số tab upload kế hoạch: lấy từ ô «số mail cần log» (0 = tối đa 25).</summary>
	private int GetDriveUploadPlannedTabCount()
	{
		int limit = 0;
		try
		{
			int.TryParse(txt_so_account_log?.Text?.Trim(), out limit);
		}
		catch
		{
		}
		if (limit <= 0)
		{
			return DriveUploadTotalTabs;
		}
		return Math.Min(limit, DriveUploadTotalTabs);
	}

	private static List<int> BuildDriveUploadTabFolderMap(int subFolderCount, int totalTabs)
	{
		List<int> map = new List<int>(Math.Max(0, totalTabs));
		if (subFolderCount <= 0 || totalTabs <= 0)
		{
			return map;
		}
		// Ít tab hơn số thư mục: mỗi thư mục 01..N một tab (tránh trùng folder khi user chỉ log N mail).
		if (totalTabs <= subFolderCount)
		{
			for (int i = 0; i < totalTabs; i++)
			{
				map.Add(i);
			}
			return map;
		}
		// Thư mục cuối nhận phần dư để tổng luôn = totalTabs (mặc định: 11×2 + 1×3 = 25).
		int basePerFolder = totalTabs / subFolderCount;          // 25 / 12 = 2
		int remainder = totalTabs - basePerFolder * subFolderCount; // 25 - 24 = 1 → dồn vào thư mục cuối
		for (int folderIndex = 0; folderIndex < subFolderCount; folderIndex++)
		{
			int tabsForThisFolder = basePerFolder;
			if (folderIndex == subFolderCount - 1)
			{
				tabsForThisFolder += remainder;
			}
			for (int t = 0; t < tabsForThisFolder && map.Count < totalTabs; t++)
			{
				map.Add(folderIndex);
			}
		}
		// Phòng trường hợp totalTabs > sức chứa: lấp nốt bằng thư mục cuối.
		while (map.Count < totalTabs)
		{
			map.Add(subFolderCount - 1);
		}
		return map;
	}

	/// <summary>
	/// Mỗi hàng lưới (mail) = đúng 1 tab Drive + 1 PDF: slot thứ <paramref name="rowIndex"/> trong kế hoạch N tab.
	/// Vd. N=25: hàng 0→tab 0 (folder 01), hàng 1→tab 1 (folder 01 lần 2), hàng 2→tab 2 (folder 02)…
	/// Không gom mọi tab cùng folder vào một hàng (tránh mở/upload 2 lần cho cùng 1 mail).
	/// </summary>
	private static List<(int folderIndex, string pdfPath)> BuildUploadSlotsForAccountRow(
		int rowIndex,
		int plannedTabCount,
		Dictionary<int, string> pdfByFolder)
	{
		List<(int folderIndex, string pdfPath)> slots = new List<(int, string)>();
		if (plannedTabCount <= 0 || rowIndex < 0 || pdfByFolder == null || rowIndex >= plannedTabCount)
		{
			return slots;
		}
		List<int> tabFolderMap = BuildDriveUploadTabFolderMap(DriveUploadSubFolderCount, plannedTabCount);
		int folderIndex = tabFolderMap[rowIndex];
		if (!pdfByFolder.TryGetValue(folderIndex, out string pdf) || string.IsNullOrEmpty(pdf) || !File.Exists(pdf))
		{
			return slots;
		}
		slots.Add((folderIndex, pdf));
		return slots;
	}

	/// <summary>
	/// Mở tab Google Drive; nếu <paramref name="uploadPdf"/> thì upload PDF theo phân bổ 25 tab.
	/// Khi không upload: chỉ mở 1 tab My Drive (hành vi cũ).
	/// </summary>
	private async Task RunDriveOpenAndUploadAsync(IBrowserContext context, int vitri, string email, bool uploadPdf)
	{
		if (context == null)
		{
			return;
		}
		if (!uploadPdf)
		{
			await OpenSingleDriveMyDriveTabAsync(context, vitri, email);
			return;
		}
		string parentDir;
		try
		{
			parentDir = EnsureDriveUploadFoldersExist();
		}
		catch (Exception ex)
		{
			SetText(vitri, "STATUS", "Drive: không tạo được thư mục upload — bỏ qua");
			AppendAutomationLog("WARN", vitri, email, "Drive upload: không tạo được thư mục " + GetDriveUploadParentDir() + " — " + ex.Message);
			await OpenSingleDriveMyDriveTabAsync(context, vitri, email);
			return;
		}

		int plannedTabCount = GetDriveUploadPlannedTabCount();

		// Cache PDF theo thư mục để không quét đĩa lặp lại.
		Dictionary<int, string> pdfByFolder = new Dictionary<int, string>();
		for (int i = 0; i < DriveUploadSubFolderCount; i++)
		{
			pdfByFolder[i] = FindSinglePdfInSubFolder(GetDriveUploadSubDir(parentDir, i));
		}

		// Chỉ upload tab thuộc hàng này (theo số mail log + phân bổ folder).
		List<(int folderIndex, string pdfPath)> uploadSlots = BuildUploadSlotsForAccountRow(vitri, plannedTabCount, pdfByFolder);

		if (uploadSlots.Count == 0)
		{
			bool rowOutsidePlan = vitri >= plannedTabCount;
			string folderForRow = GetDriveUploadSubDir(parentDir, Math.Min(Math.Max(vitri, 0), DriveUploadSubFolderCount - 1));
			bool missingPdfForRow = vitri >= 0 && vitri < DriveUploadSubFolderCount
				&& (!pdfByFolder.TryGetValue(vitri, out string pdfRow) || string.IsNullOrEmpty(pdfRow));
			if (rowOutsidePlan)
			{
				AppendAutomationLog("INFO", vitri, email,
					$"Drive upload: hàng {vitri + 1} ngoài kế hoạch {plannedTabCount} mail/tab — bỏ qua upload (chỉ mở My Drive).");
				SetText(vitri, "STATUS", $"Drive: hàng {vitri + 1} ngoài {plannedTabCount} mail — bỏ qua upload");
			}
			else if (missingPdfForRow)
			{
				AppendAutomationLog("WARN", vitri, email,
					$"Drive upload: thiếu PDF trong thư mục '{folderForRow}' (hàng {vitri + 1} ↔ thư mục {(vitri + 1):D2}).");
				SetText(vitri, "STATUS", $"Drive: thiếu PDF thư mục {(vitri + 1):D2}");
			}
			else
			{
				AppendAutomationLog("WARN", vitri, email,
					$"Drive upload: không có tab upload cho hàng {vitri + 1} (kế hoạch {plannedTabCount} tab).");
				SetText(vitri, "STATUS", "Drive: không có tab upload cho hàng này");
			}
			await OpenSingleDriveMyDriveTabAsync(context, vitri, email);
			return;
		}

		int missingFolders = pdfByFolder.Count(kv => string.IsNullOrEmpty(kv.Value));
		if (missingFolders > 0)
		{
			AppendAutomationLog("WARN", vitri, email,
				$"Drive upload: {missingFolders}/{DriveUploadSubFolderCount} thư mục con chưa có PDF (hàng {vitri + 1} upload {uploadSlots.Count} tab).");
		}

		SetText(vitri, "STATUS", $"Drive: hàng {vitri + 1} — mở {uploadSlots.Count} tab (kế hoạch {plannedTabCount} mail)...");

		// Bước 1: mở toàn bộ tab Drive (giữ lại tab nào mở thành công).
		List<(IPage page, int folderIndex, string pdfPath)> openedTabs = new List<(IPage, int, string)>();
		for (int i = 0; i < uploadSlots.Count; i++)
		{
			if (!_running || _batchToken.IsCancellationRequested)
			{
				break;
			}
			(int folderIndex, string pdfPath) slot = uploadSlots[i];
			try
			{
				IPage drivePage = await context.NewPageAsync();
				await RunStepWithReloadRetryAsync(drivePage, vitri, $"Mở Drive tab {i + 1}/{uploadSlots.Count}", async delegate
				{
					await drivePage.GotoAsync(GoogleUrlEn("https://drive.google.com/drive/my-drive"), PwGotoGoogleDomLoaded());
					await drivePage.BringToFrontAsync();
				});
				openedTabs.Add((drivePage, slot.folderIndex, slot.pdfPath));
			}
			catch (Exception ex)
			{
				AppendAutomationLog("WARN", vitri, email, $"Drive: không mở được tab {i + 1} — {ex.GetType().Name}: {ex.Message}");
			}
		}

		if (openedTabs.Count == 0)
		{
			SetText(vitri, "STATUS", "Drive: không mở được tab nào");
			return;
		}

		// Bước 2: upload → share (Anyone with the link) → copy link → mở tab link. Chỉ báo OK khi cả 3 bước xong.
		int uploadOkCount = 0;
		int shareOkCount = 0;
		for (int i = 0; i < openedTabs.Count; i++)
		{
			if (!_running || _batchToken.IsCancellationRequested)
			{
				break;
			}
			(IPage page, int folderIndex, string pdfPath) tab = openedTabs[i];
			string fileName = Path.GetFileName(tab.pdfPath);
			int tabNo = i + 1;
			SetText(vitri, "STATUS", $"Drive: upload tab {tabNo}/{openedTabs.Count} (thư mục {tab.folderIndex + 1:D2}) — {fileName}");
			bool uploadOk = await TryUploadPdfToDriveTabAsync(tab.page, tab.pdfPath, vitri, tabNo, email);
			if (!uploadOk)
			{
				continue;
			}
			uploadOkCount++;
			SetText(vitri, "STATUS", $"Drive: share + copy link tab {tabNo}/{openedTabs.Count} — {fileName}");
			bool shareOk = await TryShareDriveFileAndOpenLinkAsync(context, tab.page, tab.pdfPath, vitri, tabNo, email);
			if (shareOk)
			{
				shareOkCount++;
			}
		}

		if (shareOkCount == openedTabs.Count && shareOkCount > 0)
		{
			SetText(vitri, "STATUS", $"Drive: upload+share OK {shareOkCount}/{openedTabs.Count} tab (hàng {vitri + 1})");
			AppendAutomationLog("INFO", vitri, email, $"Drive: hàng {vitri + 1} — upload+share+link OK {shareOkCount}/{openedTabs.Count} tab.");
		}
		else if (uploadOkCount > 0 && shareOkCount == 0)
		{
			SetText(vitri, "STATUS", $"Drive: upload OK nhưng share/link thất bại 0/{uploadOkCount} — xem automation.log");
			AppendAutomationLog("WARN", vitri, email, $"Drive: hàng {vitri + 1} — upload {uploadOkCount} tab nhưng share/link 0 tab.");
		}
		else if (shareOkCount == 0 && uploadOkCount == 0)
		{
			SetText(vitri, "STATUS", $"Drive: thất bại 0/{openedTabs.Count} tab — xem automation.log");
			AppendAutomationLog("WARN", vitri, email, $"Drive: hàng {vitri + 1} — 0/{openedTabs.Count} tab upload được.");
		}
		else
		{
			SetText(vitri, "STATUS", $"Drive: upload {uploadOkCount}/{openedTabs.Count}, share+link {shareOkCount}/{openedTabs.Count} (hàng {vitri + 1})");
			AppendAutomationLog("WARN", vitri, email, $"Drive: hàng {vitri + 1} — upload {uploadOkCount}, share+link {shareOkCount} / {openedTabs.Count} tab.");
		}
	}

	/// <summary>Mở 1 tab My Drive (hành vi cũ) — dùng làm fallback khi chưa có file để upload.</summary>
	private async Task OpenSingleDriveMyDriveTabAsync(IBrowserContext context, int vitri, string email)
	{
		try
		{
			IPage drivePage = await context.NewPageAsync();
			await RunStepWithReloadRetryAsync(drivePage, vitri, "Mở Google Drive My Drive", async delegate
			{
				await drivePage.GotoAsync(GoogleUrlEn("https://drive.google.com/drive/my-drive"), PwGotoGoogleDomLoaded());
				await drivePage.BringToFrontAsync();
			});
			try
			{
				await drivePage.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
				{
					Timeout = 45000f
				});
			}
			catch
			{
			}
		}
		catch (Exception ex)
		{
			AppendAutomationLog("WARN", vitri, email, "Drive: mở My Drive thất bại — " + ex.Message);
		}
	}

	/// <summary>
	/// Upload 1 PDF: gửi file (nhiều cách) rồi <b>chỉ báo OK khi xác minh</b> file đã lên My Drive.
	/// Trước đây luôn return true dù chưa upload — đã sửa.
	/// </summary>
	private async Task<bool> TryUploadPdfToDriveTabAsync(IPage page, string pdfPath, int vitri, int tabNumber, string email)
	{
		if (page == null || string.IsNullOrEmpty(pdfPath) || !File.Exists(pdfPath))
		{
			AppendAutomationLog("WARN", vitri, email, $"Drive upload tab {tabNumber}: file không tồn tại — {pdfPath}");
			return false;
		}
		string absolutePdfPath = Path.GetFullPath(pdfPath);
		string fileName = Path.GetFileName(absolutePdfPath);
		try
		{
			if (!await EnsureDriveMyDriveReadyAsync(page, vitri, tabNumber, email))
			{
				return false;
			}
			bool sent = await TrySendFileToDriveAsync(page, absolutePdfPath, vitri, tabNumber, email);
			if (!sent)
			{
				AppendAutomationLog("WARN", vitri, email, $"Drive upload tab {tabNumber}: không gửi được file '{fileName}' (New/File upload/input file).");
				return false;
			}
			bool verified = await VerifyDriveUploadSucceededAsync(page, fileName, vitri, email, tabNumber);
			if (!verified)
			{
				AppendAutomationLog("WARN", vitri, email, $"Drive upload tab {tabNumber}: đã thử gửi '{fileName}' nhưng không xác nhận được trên My Drive (không coi là thành công).");
				return false;
			}
			AppendAutomationLog("INFO", vitri, email, $"Drive upload tab {tabNumber}: xác nhận OK — '{fileName}' đã có trên Drive.");
			return true;
		}
		catch (Exception ex)
		{
			AppendAutomationLog("WARN", vitri, email, $"Drive upload tab {tabNumber}: {ex.GetType().Name} — {ex.Message}");
			return false;
		}
	}

	/// <summary>Chờ My Drive sẵn sàng (nút New, không kẹt trang trắng).</summary>
	private async Task<bool> EnsureDriveMyDriveReadyAsync(IPage page, int vitri, int tabNumber, string email)
	{
		try
		{
			await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions { Timeout = 45000f });
		}
		catch
		{
		}
		try
		{
			await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 35000f });
		}
		catch
		{
		}
		for (int attempt = 0; attempt < 3; attempt++)
		{
			ILocator newButton = await ResolveDriveNewButtonAsync(page);
			if (newButton != null)
			{
				return true;
			}
			AppendAutomationLog("WARN", vitri, email, $"Drive upload tab {tabNumber}: chờ nút New (lần {attempt + 1}/3)...");
			try
			{
				await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 60000f });
			}
			catch
			{
			}
			await DelayBatchAsync(2000);
		}
		AppendAutomationLog("WARN", vitri, email, $"Drive upload tab {tabNumber}: My Drive chưa sẵn sàng (không thấy nút New).");
		return false;
	}

	/// <summary>Thử gửi file một lần: file chooser (ưu tiên); chỉ fallback input khi chưa gửi được (tránh upload trùng).</summary>
	private async Task<bool> TrySendFileToDriveAsync(IPage page, string absolutePdfPath, int vitri, int tabNumber, string email)
	{
		bool sentViaChooser = await TryDriveUploadViaNewMenuFileChooserAsync(page, absolutePdfPath, vitri, tabNumber, email);
		if (sentViaChooser)
		{
			return true;
		}
		if (await TryDriveUploadViaHiddenFileInputAsync(page, absolutePdfPath, clickNewMenuFirst: false))
		{
			AppendAutomationLog("INFO", vitri, email, $"Drive upload tab {tabNumber}: gửi file qua input[type=file] (dự phòng).");
			return true;
		}
		return false;
	}

	/// <summary>New → File upload, dùng RunAndWaitForFileChooser (tránh race WaitForFileChooser + Click).</summary>
	private async Task<bool> TryDriveUploadViaNewMenuFileChooserAsync(IPage page, string absolutePdfPath, int vitri, int tabNumber, string email)
	{
		try
		{
			ILocator newButton = await ResolveDriveNewButtonAsync(page);
			if (newButton == null)
			{
				return false;
			}
			await newButton.ClickAsync(new LocatorClickOptions { Timeout = 20000f });
			await DelayBatchAsync(800);
			ILocator fileUploadItem = await ResolveDriveFileUploadMenuItemAsync(page);
			if (fileUploadItem == null)
			{
				try
				{
					await page.Keyboard.PressAsync("Escape");
				}
				catch
				{
				}
				return false;
			}
			bool filesSet = false;
			try
			{
				IFileChooser chooser = await page.RunAndWaitForFileChooserAsync(async delegate
				{
					await fileUploadItem.ClickAsync(new LocatorClickOptions { Timeout = 20000f });
				}, new PageRunAndWaitForFileChooserOptions { Timeout = 45000f });
				await chooser.SetFilesAsync(absolutePdfPath);
				filesSet = true;
			}
			catch (Exception exSet)
			{
				// SetFiles có thể đã chạy xong rồi mới ném lỗi (timeout đóng dialog) — không fallback để tránh upload 2 lần.
				if (!filesSet)
				{
					AppendAutomationLog("WARN", vitri, email, $"Drive upload tab {tabNumber}: file chooser — {exSet.GetType().Name}: {exSet.Message}");
					try
					{
						await page.Keyboard.PressAsync("Escape");
					}
					catch
					{
					}
					return false;
				}
			}
			AppendAutomationLog("INFO", vitri, email, $"Drive upload tab {tabNumber}: đã SetFiles qua file chooser.");
			return true;
		}
		catch (Exception ex)
		{
			AppendAutomationLog("WARN", vitri, email, $"Drive upload tab {tabNumber}: file chooser — {ex.GetType().Name}: {ex.Message}");
			return false;
		}
	}

	/// <summary>Gán file trực tiếp vào input[type=file] (Drive thường có input ẩn).</summary>
	private static async Task<bool> TryDriveUploadViaHiddenFileInputAsync(IPage page, string absolutePdfPath, bool clickNewMenuFirst)
	{
		try
		{
			if (clickNewMenuFirst)
			{
				ILocator newButton = await ResolveDriveNewButtonAsync(page);
				if (newButton != null)
				{
					await newButton.ClickAsync(new LocatorClickOptions { Timeout = 15000f });
					await Task.Delay(600);
				}
			}
			ILocator inputs = page.Locator("input[type='file']");
			int count = await inputs.CountAsync();
			for (int i = count - 1; i >= 0; i--)
			{
				try
				{
					await inputs.Nth(i).SetInputFilesAsync(absolutePdfPath);
					return true;
				}
				catch
				{
				}
			}
		}
		catch
		{
		}
		return false;
	}

	/// <summary>Chờ tối đa ~90s: file xuất hiện trên lưới / toast complete, và không có thông báo failed.</summary>
	private async Task<bool> VerifyDriveUploadSucceededAsync(IPage page, string fileName, int vitri, string email, int tabNumber)
	{
		string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
		for (int tick = 0; tick < 45; tick++)
		{
			if (await DriveUploadFailedMessageVisibleAsync(page))
			{
				return false;
			}
			if (await DriveFileListedInMyDriveAsync(page, fileName, nameNoExt))
			{
				return true;
			}
			if (await DriveUploadCompleteToastVisibleAsync(page))
			{
				await DelayBatchAsync(1500);
				if (await DriveFileListedInMyDriveAsync(page, fileName, nameNoExt))
				{
					return true;
				}
			}
			await DelayBatchAsync(2000);
		}
		return false;
	}

	private static async Task<bool> DriveUploadFailedMessageVisibleAsync(IPage page)
	{
		string[] failSelectors = new[]
		{
			"text=/upload failed/i",
			"text=/couldn't upload/i",
			"text=/could not upload/i",
			"text=/failed to upload/i"
		};
		foreach (string sel in failSelectors)
		{
			try
			{
				if (await page.Locator(sel).CountAsync() > 0)
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

	private static async Task<bool> DriveUploadCompleteToastVisibleAsync(IPage page)
	{
		string[] doneSelectors = new[]
		{
			"text=/upload\\s+complete/i",
			"text=/uploads?\\s+complete/i",
			"text=/uploaded\\s+1/i",
			"text=/đã\\s+tải.*lên/i"
		};
		foreach (string sel in doneSelectors)
		{
			try
			{
				if (await page.Locator(sel).First.IsVisibleAsync())
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

	private static async Task<bool> DriveFileListedInMyDriveAsync(IPage page, string fileName, string nameNoExt)
	{
		try
		{
			ILocator byRow = page.GetByRole(AriaRole.Row, new PageGetByRoleOptions { Name = fileName });
			if (await byRow.CountAsync() > 0)
			{
				return await byRow.Last.IsVisibleAsync();
			}
		}
		catch
		{
		}
		try
		{
			ILocator gridRow = page.Locator($"div[role='row'][aria-label*='{EscapeForCssAttr(fileName)}']");
			if (await gridRow.CountAsync() > 0)
			{
				return await gridRow.Last.IsVisibleAsync();
			}
		}
		catch
		{
		}
		if (!string.Equals(nameNoExt, fileName, StringComparison.Ordinal))
		{
			try
			{
				ILocator byName = page.Locator($"div[role='gridcell']:has-text(\"{EscapeForHasText(nameNoExt)}\")");
				if (await byName.CountAsync() > 0)
				{
					return await byName.Last.IsVisibleAsync();
				}
			}
			catch
			{
			}
		}
		return false;
	}

	/// <summary>Tìm nút "New" của Google Drive với nhiều selector dự phòng.</summary>
	private static async Task<ILocator> ResolveDriveNewButtonAsync(IPage page)
	{
		string[] selectors = new[]
		{
			"div[guidedhelpid='new_menu_button']",
			"button[guidedhelpid='new_menu_button']",
			"[aria-label='New']",
			"button:has-text('New')"
		};
		foreach (string sel in selectors)
		{
			try
			{
				ILocator loc = page.Locator(sel);
				if (await loc.CountAsync() > 0)
				{
					ILocator first = loc.First;
					await first.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000f });
					return first;
				}
			}
			catch
			{
			}
		}
		try
		{
			ILocator byRole = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "New" });
			if (await byRole.CountAsync() > 0)
			{
				ILocator first = byRole.First;
				await first.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000f });
				return first;
			}
		}
		catch
		{
		}
		return null;
	}

	/// <summary>Tìm mục "File upload" trong menu New với nhiều selector dự phòng.</summary>
	private static async Task<ILocator> ResolveDriveFileUploadMenuItemAsync(IPage page)
	{
		string[] menuNames = new[] { "File upload", "Upload files", "Upload file" };
		foreach (string menuName in menuNames)
		{
			try
			{
				ILocator byRole = page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions { Name = menuName });
				if (await byRole.CountAsync() > 0)
				{
					ILocator first = byRole.First;
					await first.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000f });
					return first;
				}
			}
			catch
			{
			}
		}
		string[] selectors = new[]
		{
			"div[role='menuitem']:has-text('File upload')",
			"div[role='menuitem']:has-text('File Upload')",
			"[role='menuitem']:has-text('File upload')",
			"[role='menuitem']:has-text('Upload files')"
		};
		foreach (string sel in selectors)
		{
			try
			{
				ILocator loc = page.Locator(sel);
				if (await loc.CountAsync() > 0)
				{
					ILocator first = loc.First;
					await first.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 15000f });
					return first;
				}
			}
			catch
			{
			}
		}
		return null;
	}

	/// <summary>
	/// Upload xong → chọn file trên lưới → Share → Anyone with the link → lấy link → mở tab mới.
	/// Chỉ return true khi xác nhận được link Drive hợp lệ và tab mới mở được.
	/// </summary>
	private async Task<bool> TryShareDriveFileAndOpenLinkAsync(IBrowserContext context, IPage page, string pdfPath, int vitri, int tabNumber, string email)
	{
		if (context == null || page == null || string.IsNullOrEmpty(pdfPath))
		{
			return false;
		}
		string fileName = Path.GetFileName(pdfPath);
		try
		{
			await DelayBatchAsync(1000);
			ILocator fileRow = await ResolveUploadedDriveFileRowAsync(page, fileName);
			if (fileRow == null)
			{
				AppendAutomationLog("WARN", vitri, email, $"Drive share tab {tabNumber}: không thấy hàng file '{fileName}' trên lưới My Drive.");
				return false;
			}
			// Panel Share là iframe drivesharing → mọi thao tác phải chạy trong frame này.
			IFrame shareFrame = await OpenDriveShareDialogForFileRowAsync(page, fileRow, vitri, tabNumber, email);
			if (shareFrame == null)
			{
				AppendAutomationLog("WARN", vitri, email, $"Drive share tab {tabNumber}: không mở được hộp thoại Share (iframe).");
				return false;
			}
			if (!await SetDriveGeneralAccessAnyoneWithLinkAsync(shareFrame, vitri, tabNumber, email))
			{
				AppendAutomationLog("WARN", vitri, email, $"Drive share tab {tabNumber}: không đặt được General access = Anyone with the link.");
				await TryCloseDriveShareDialogAsync(shareFrame);
				return false;
			}
			await DelayBatchAsync(800);
			string link = await ObtainDriveShareLinkAsync(shareFrame, vitri, tabNumber, email);
			if (!IsValidDriveShareLink(link))
			{
				AppendAutomationLog("WARN", vitri, email, $"Drive share tab {tabNumber}: không lấy được link chia sẻ hợp lệ.");
				await TryCloseDriveShareDialogAsync(shareFrame);
				return false;
			}
			AppendAutomationLog("INFO", vitri, email, $"Drive share tab {tabNumber}: link = {link}");
			await TryCloseDriveShareDialogAsync(shareFrame);
			if (!await TryOpenDriveShareLinkInNewTabAsync(context, link, vitri, tabNumber, email))
			{
				AppendAutomationLog("WARN", vitri, email, $"Drive share tab {tabNumber}: có link nhưng mở tab thất bại.");
				return false;
			}
			SetText(vitri, "STATUS", $"Drive: share+link OK tab {tabNumber}");
			return true;
		}
		catch (Exception ex)
		{
			AppendAutomationLog("WARN", vitri, email, $"Drive share tab {tabNumber}: {ex.GetType().Name} — {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// Tìm iframe của panel Share (DriveShareDialogUi tại /drivesharing/driveshare).
	/// Trả null nếu không thấy trong thời gian chờ.
	/// </summary>
	private static async Task<IFrame> ResolveDriveShareFrameAsync(IPage page, float timeoutMs)
	{
		DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
		while (true)
		{
			foreach (IFrame f in page.Frames)
			{
				try
				{
					string u = f.Url ?? "";
					if (u.Contains("drivesharing", StringComparison.OrdinalIgnoreCase)
						|| u.Contains("/driveshare", StringComparison.OrdinalIgnoreCase))
					{
						return f;
					}
				}
				catch
				{
				}
			}
			if (DateTime.UtcNow >= deadline)
			{
				break;
			}
			await Task.Delay(300);
		}
		return null;
	}

	/// <summary>Panel Share Drive (heading Share + General access) trong iframe drivesharing.</summary>
	private static ILocator GetDriveSharePanelLocator(IFrame frame)
	{
		return frame.Locator("div.asdCEb").Filter(new LocatorFilterOptions { HasTextString = "General access" });
	}

	private static ILocator GetDriveGeneralAccessRootLocator(IFrame frame)
	{
		return frame.Locator("div.u5mDnb").First;
	}

	private static bool IsValidDriveShareLink(string url)
	{
		if (string.IsNullOrWhiteSpace(url))
		{
			return false;
		}
		string u = url.Trim();
		if (!u.StartsWith("http", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		return u.Contains("drive.google.com", StringComparison.OrdinalIgnoreCase)
			|| u.Contains("docs.google.com", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>Tìm hàng file trên lưới My Drive (không dùng toast — toast không mở Share đúng).</summary>
	private async Task<ILocator> ResolveUploadedDriveFileRowAsync(IPage page, string fileName)
	{
		string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
		List<Func<ILocator>> candidateFactories = new List<Func<ILocator>>();
		try { candidateFactories.Add(() => page.GetByRole(AriaRole.Row, new PageGetByRoleOptions { Name = fileName })); } catch { }
		try { candidateFactories.Add(() => page.Locator($"div[role='row'][aria-label*='{EscapeForCssAttr(fileName)}']")); } catch { }
		try { candidateFactories.Add(() => page.Locator($"div[role='row']:has-text(\"{EscapeForHasText(fileName)}\")")); } catch { }
		if (!string.Equals(nameNoExt, fileName, StringComparison.Ordinal))
		{
			try { candidateFactories.Add(() => page.GetByRole(AriaRole.Row, new PageGetByRoleOptions { Name = nameNoExt })); } catch { }
		}
		foreach (Func<ILocator> factory in candidateFactories)
		{
			try
			{
				ILocator loc = factory();
				int count = await loc.CountAsync();
				if (count > 0)
				{
					ILocator pick = count > 1 ? loc.Last : loc.First;
					await pick.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 12000f });
					return pick;
				}
			}
			catch
			{
			}
		}
		return null;
	}

	private static string EscapeForCssAttr(string s)
	{
		return (s ?? "").Replace("'", "\\'");
	}

	private static string EscapeForHasText(string s)
	{
		return (s ?? "").Replace("\"", "\\\"");
	}

	/// <summary>Chọn file → Ctrl+Alt+A mở panel Share (ưu tiên); dự phòng chuột phải / toolbar. Trả iframe Share hoặc null.</summary>
	private async Task<IFrame> OpenDriveShareDialogForFileRowAsync(IPage page, ILocator fileRow, int vitri, int tabNumber, string email)
	{
		try
		{
			await fileRow.ScrollIntoViewIfNeededAsync();
		}
		catch
		{
		}
		// Cách 1 (ưu tiên): chọn file rồi nhấn Ctrl+Alt+A để mở Share.
		try
		{
			await fileRow.ClickAsync(new LocatorClickOptions { Timeout = 15000f });
			await DelayBatchAsync(500);
			await page.Keyboard.PressAsync("Control+Alt+a");
			IFrame f = await WaitForDriveShareFrameReadyAsync(page, 15000f);
			if (f != null)
			{
				await TryDismissDriveShareGotItCalloutAsync(f);
				return f;
			}
		}
		catch (Exception ex)
		{
			AppendAutomationLog("WARN", vitri, email, $"Drive share tab {tabNumber}: Ctrl+Alt+A → Share — {ex.Message}");
		}
		// Cách 2: chuột phải → Share.
		try
		{
			await fileRow.ClickAsync(new LocatorClickOptions { Button = MouseButton.Right, Timeout = 15000f });
			await DelayBatchAsync(700);
			ILocator shareMenu = page.GetByRole(AriaRole.Menuitem, new PageGetByRoleOptions { Name = "Share" });
			if (await shareMenu.CountAsync() == 0)
			{
				shareMenu = page.Locator("div[role='menuitem']:has-text('Share'), li[role='menuitem']:has-text('Share')");
			}
			if (await shareMenu.CountAsync() > 0)
			{
				await shareMenu.First.ClickAsync(new LocatorClickOptions { Timeout = 15000f });
				IFrame f = await WaitForDriveShareFrameReadyAsync(page, 15000f);
				if (f != null)
				{
					await TryDismissDriveShareGotItCalloutAsync(f);
					return f;
				}
			}
		}
		catch (Exception ex)
		{
			AppendAutomationLog("WARN", vitri, email, $"Drive share tab {tabNumber}: chuột phải → Share — {ex.Message}");
		}
		// Cách 3: chọn file → nút Share toolbar.
		try
		{
			await fileRow.ClickAsync(new LocatorClickOptions { Timeout = 10000f });
			await DelayBatchAsync(500);
			ILocator shareToolbar = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Share" });
			if (await shareToolbar.CountAsync() > 0)
			{
				await shareToolbar.First.ClickAsync(new LocatorClickOptions { Timeout = 15000f });
				IFrame f = await WaitForDriveShareFrameReadyAsync(page, 12000f);
				if (f != null)
				{
					await TryDismissDriveShareGotItCalloutAsync(f);
					return f;
				}
			}
		}
		catch
		{
		}
		return null;
	}

	/// <summary>Đóng callout «Got it» nếu che panel Share (trong iframe).</summary>
	private static async Task TryDismissDriveShareGotItCalloutAsync(IFrame frame)
	{
		try
		{
			ILocator gotIt = frame.Locator("button[jsname='plIjzf'] span[jsname='V67aGc']:has-text('Got it')");
			if (await gotIt.CountAsync() == 0)
			{
				gotIt = frame.GetByRole(AriaRole.Button, new FrameGetByRoleOptions { Name = "Got it" });
			}
			if (await gotIt.CountAsync() > 0)
			{
				await gotIt.First.ClickAsync(new LocatorClickOptions { Timeout = 5000f });
				await Task.Delay(400);
			}
		}
		catch
		{
		}
	}

	/// <summary>Chờ iframe Share xuất hiện và panel bên trong đã render (General access / nút dropdown).</summary>
	private async Task<IFrame> WaitForDriveShareFrameReadyAsync(IPage page, float timeoutMs)
	{
		IFrame frame = await ResolveDriveShareFrameAsync(page, timeoutMs);
		if (frame == null)
		{
			return null;
		}
		ILocator[] waitTargets = new[]
		{
			frame.Locator("div[jsname='nfek']"),
			frame.Locator("button[jsname='dLruDf']"),
			frame.Locator("h3.quTB6").Filter(new LocatorFilterOptions { HasTextString = "General access" }),
			GetDriveSharePanelLocator(frame)
		};
		foreach (ILocator loc in waitTargets)
		{
			try
			{
				await loc.First.WaitForAsync(new LocatorWaitForOptions
				{
					State = WaitForSelectorState.Visible,
					Timeout = timeoutMs
				});
				return frame;
			}
			catch
			{
			}
		}
		// Iframe có nhưng chưa nhận diện được element quen thuộc — vẫn trả về để thử thao tác.
		return frame;
	}

	/// <summary>
	/// General access: bấm dropdown (Restricted / Anyone with the link) → chọn span[jsname=K4r5Ff] «Anyone with the link».
	/// Nút dropdown General access nằm trong div[jsname=nfek], aria-label kết thúc bằng «change general access».
	/// </summary>
	private async Task<bool> SetDriveGeneralAccessAnyoneWithLinkAsync(IFrame frame, int vitri, int tabNumber, string email)
	{
		try
		{
			ILocator gaRoot = GetDriveGeneralAccessRootLocator(frame);
			await gaRoot.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 12000f });
			if (await DriveGeneralAccessShowsAnyoneWithLinkAsync(frame))
			{
				return true;
			}
			ILocator accessDropdownBtn = await ResolveDriveGeneralAccessDropdownButtonAsync(frame);
			if (accessDropdownBtn == null)
			{
				AppendAutomationLog("WARN", vitri, email, $"Drive share tab {tabNumber}: không thấy nút dropdown General access (Restricted).");
				return false;
			}
			await accessDropdownBtn.ScrollIntoViewIfNeededAsync();
			await accessDropdownBtn.ClickAsync(new LocatorClickOptions { Timeout = 12000f });
			await DelayBatchAsync(800);
			if (!await ClickDriveAnyoneWithLinkMenuItemAsync(frame))
			{
				// Thử click lại dropdown 1 lần nữa (menu có thể đóng nhanh / chưa kịp mở).
				try
				{
					await accessDropdownBtn.ClickAsync(new LocatorClickOptions { Timeout = 8000f });
					await DelayBatchAsync(800);
				}
				catch
				{
				}
				if (!await ClickDriveAnyoneWithLinkMenuItemAsync(frame))
				{
					AppendAutomationLog("WARN", vitri, email, $"Drive share tab {tabNumber}: không chọn được «Anyone with the link» trong menu.");
					return false;
				}
			}
			await DelayBatchAsync(1200);
			return await DriveGeneralAccessShowsAnyoneWithLinkAsync(frame);
		}
		catch (Exception ex)
		{
			AppendAutomationLog("WARN", vitri, email, $"Drive share tab {tabNumber}: General access — {ex.Message}");
			return false;
		}
	}

	/// <summary>Nút mở dropdown General access (không nhầm với nút vai trò Viewer/Owner).</summary>
	private static async Task<ILocator> ResolveDriveGeneralAccessDropdownButtonAsync(IFrame frame)
	{
		string[] selectors = new[]
		{
			"div[jsname='nfek'] button[jsname='dLruDf']",
			"button[jsname='dLruDf'][aria-label*='general access' i]",
			"button[aria-label*='change general access' i]"
		};
		foreach (string sel in selectors)
		{
			try
			{
				ILocator loc = frame.Locator(sel);
				if (await loc.CountAsync() > 0)
				{
					ILocator first = loc.First;
					await first.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000f });
					return first;
				}
			}
			catch
			{
			}
		}
		// Dự phòng: nút trong vùng General access có span hiện trạng thái Restricted/Anyone with the link.
		try
		{
			ILocator gaRoot = GetDriveGeneralAccessRootLocator(frame);
			ILocator btnByLabel = gaRoot.Locator("button[jsname='dLruDf']").Filter(new LocatorFilterOptions
			{
				HasTextRegex = new Regex("Restricted|Anyone with the link", RegexOptions.IgnoreCase)
			});
			if (await btnByLabel.CountAsync() > 0)
			{
				ILocator first = btnByLabel.First;
				await first.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 8000f });
				return first;
			}
		}
		catch
		{
		}
		return null;
	}

	/// <summary>Đã chọn «Anyone with the link» trên nút dropdown General access (không phải trong menu đang mở).</summary>
	private static async Task<bool> DriveGeneralAccessShowsAnyoneWithLinkAsync(IFrame frame)
	{
		try
		{
			ILocator btn = await ResolveDriveGeneralAccessDropdownButtonAsync(frame);
			if (btn != null)
			{
				string text = (await btn.InnerTextAsync() ?? "").Trim();
				if (text.Contains("Anyone with the link", StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
		}
		catch
		{
		}
		try
		{
			ILocator hint = GetDriveGeneralAccessRootLocator(frame).Locator(".RJS6zb");
			string sub = (await hint.InnerTextAsync() ?? "").Trim();
			if (sub.Contains("Anyone on the internet with the link", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	/// <summary>
	/// Trong menu «Change link type»: click vào li[role=menuitemradio] chứa «Anyone with the link»
	/// (click cả LI, không chỉ span con vì span không nhận sự kiện click).
	/// </summary>
	private static async Task<bool> ClickDriveAnyoneWithLinkMenuItemAsync(IFrame frame)
	{
		// 1) Click trực tiếp LI menuitemradio chứa text.
		try
		{
			ILocator radioItem = frame.Locator("li[role='menuitemradio']").Filter(new LocatorFilterOptions { HasTextString = "Anyone with the link" });
			if (await radioItem.CountAsync() > 0)
			{
				ILocator item = radioItem.First;
				await item.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 8000f });
				await item.ClickAsync(new LocatorClickOptions { Timeout = 10000f });
				return true;
			}
		}
		catch
		{
		}
		// 2) Tìm span text rồi leo lên LI cha để click.
		try
		{
			ILocator span = frame.Locator("span[jsname='K4r5Ff']").Filter(new LocatorFilterOptions { HasTextString = "Anyone with the link" });
			if (await span.CountAsync() > 0)
			{
				ILocator li = span.First.Locator("xpath=ancestor::li[1]");
				if (await li.CountAsync() > 0)
				{
					await li.First.ClickAsync(new LocatorClickOptions { Timeout = 10000f });
					return true;
				}
				await span.First.ClickAsync(new LocatorClickOptions { Timeout = 10000f });
				return true;
			}
		}
		catch
		{
		}
		// 3) Dự phòng bàn phím: menu radio, mũi tên xuống + Enter.
		try
		{
			ILocator menu = frame.Locator("ul[jsname='rymPhb'][aria-label='Change link type'], ul[aria-label='Change link type']");
			if (await menu.CountAsync() > 0)
			{
				await frame.Page.Keyboard.PressAsync("ArrowDown");
				await Task.Delay(200);
				await frame.Page.Keyboard.PressAsync("Enter");
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	/// <summary>Đọc link từ ô trong dialog trước, rồi Copy link + clipboard.</summary>
	private async Task<string> ObtainDriveShareLinkAsync(IFrame frame, int vitri, int tabNumber, string email)
	{
		string fromInput = await TryReadShareLinkFromDialogAsync(frame);
		if (IsValidDriveShareLink(fromInput))
		{
			return fromInput;
		}
		string fromCopy = await ClickCopyLinkAndReadAsync(frame, vitri, tabNumber, email);
		if (IsValidDriveShareLink(fromCopy))
		{
			return fromCopy;
		}
		return "";
	}

	private static async Task<string> TryReadShareLinkFromDialogAsync(IFrame frame)
	{
		try
		{
			ILocator dialog = GetDriveSharePanelLocator(frame);
			if (await dialog.CountAsync() == 0)
			{
				dialog = frame.Locator("div.asdCEb").First;
			}
			else
			{
				dialog = dialog.First;
			}
			ILocator inputs = dialog.Locator("input[type='text'], input[readonly], textarea");
			int n = await inputs.CountAsync();
			for (int i = 0; i < n; i++)
			{
				try
				{
					string val = await inputs.Nth(i).InputValueAsync();
					if (IsValidDriveShareLink(val))
					{
						return val.Trim();
					}
				}
				catch
				{
				}
			}
			string fromJs = await frame.EvaluateAsync<string>(@"() => {
				const panels = document.querySelectorAll('.asdCEb, div[role=""dialog""]');
				for (const d of panels) {
					if (!d.innerText || d.innerText.indexOf('General access') < 0) continue;
					const inputs = d.querySelectorAll('input, textarea');
					for (const inp of inputs) {
						const v = (inp.value || '').trim();
						if (v.indexOf('drive.google.com') >= 0 || v.indexOf('docs.google.com') >= 0) return v;
					}
				}
				return '';
			}");
			if (IsValidDriveShareLink(fromJs))
			{
				return fromJs.Trim();
			}
		}
		catch
		{
		}
		return "";
	}

	/// <summary>Bấm nút Copy link (jsname=lmPqlf / span AeBiU-vQzf8d) rồi đọc clipboard.</summary>
	private async Task<string> ClickCopyLinkAndReadAsync(IFrame frame, int vitri, int tabNumber, string email)
	{
		try
		{
			ILocator copyBtn = frame.Locator("button[jsname='lmPqlf']");
			if (await copyBtn.CountAsync() == 0)
			{
				copyBtn = frame.Locator("span.AeBiU-vQzf8d:has-text('Copy link')").Locator("xpath=ancestor::button[1]");
			}
			if (await copyBtn.CountAsync() == 0)
			{
				copyBtn = frame.GetByRole(AriaRole.Button, new FrameGetByRoleOptions { Name = "Copy link" });
			}
			if (await copyBtn.CountAsync() > 0)
			{
				await copyBtn.First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 12000f });
				await copyBtn.First.ClickAsync(new LocatorClickOptions { Timeout = 12000f });
				await DelayBatchAsync(1000);
			}
			else
			{
				AppendAutomationLog("WARN", vitri, email, $"Drive share tab {tabNumber}: không thấy nút Copy link (jsname=lmPqlf).");
				return "";
			}
			for (int attempt = 0; attempt < 10; attempt++)
			{
				try
				{
					string fromClip = await frame.EvaluateAsync<string>("() => navigator.clipboard.readText().catch(() => '')");
					if (IsValidDriveShareLink(fromClip))
					{
						return fromClip.Trim();
					}
				}
				catch
				{
				}
				await DelayBatchAsync(400);
			}
		}
		catch (Exception ex)
		{
			AppendAutomationLog("WARN", vitri, email, $"Drive share tab {tabNumber}: Copy link — {ex.Message}");
		}
		return "";
	}

	private async Task<bool> TryOpenDriveShareLinkInNewTabAsync(IBrowserContext context, string link, int vitri, int tabNumber, string email)
	{
		try
		{
			IPage linkPage = await context.NewPageAsync();
			await RunStepWithReloadRetryAsync(linkPage, vitri, $"Mở link chia sẻ tab {tabNumber}", async delegate
			{
				await linkPage.GotoAsync(link, PwGotoGoogleDomLoaded());
				await linkPage.BringToFrontAsync();
			});
			try
			{
				await linkPage.WaitForURLAsync(url => url.Contains("drive.google.com", StringComparison.OrdinalIgnoreCase)
					|| url.Contains("docs.google.com", StringComparison.OrdinalIgnoreCase),
					new PageWaitForURLOptions { Timeout = 45000f });
			}
			catch
			{
				string u = linkPage.Url ?? "";
				if (!IsValidDriveShareLink(u) && !u.Contains("accounts.google.com", StringComparison.OrdinalIgnoreCase))
				{
					AppendAutomationLog("WARN", vitri, email, $"Drive share tab {tabNumber}: tab link URL không hợp lệ: {u}");
					return false;
				}
			}
			return true;
		}
		catch (Exception ex)
		{
			AppendAutomationLog("WARN", vitri, email, $"Drive share tab {tabNumber}: mở tab link — {ex.Message}");
			return false;
		}
	}

	/// <summary>Đóng panel Share (Done jsname=AHldd hoặc Escape).</summary>
	private async Task TryCloseDriveShareDialogAsync(IFrame frame)
	{
		try
		{
			ILocator doneBtn = frame.Locator("button[jsname='AHldd']");
			if (await doneBtn.CountAsync() == 0)
			{
				doneBtn = frame.GetByRole(AriaRole.Button, new FrameGetByRoleOptions { Name = "Done" });
			}
			if (await doneBtn.CountAsync() > 0)
			{
				await doneBtn.First.ClickAsync(new LocatorClickOptions { Timeout = 8000f });
				return;
			}
		}
		catch
		{
		}
		try
		{
			await frame.Page.Keyboard.PressAsync("Escape");
		}
		catch
		{
		}
	}
}
