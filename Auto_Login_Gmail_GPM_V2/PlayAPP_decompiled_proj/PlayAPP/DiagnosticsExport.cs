using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace PlayAPP;

/// <summary>Gói ZIP chẩn đoán: log + screenshot gần đây — không gồm Account.txt / proxy.txt.</summary>
internal static class DiagnosticsExport
{
	private static readonly string[] LogNames =
	{
		"automation.log",
		"login_success.log",
		"dead_recaptcha_verify.log",
		"dead_google_restrictions.log"
	};

	private const int MaxScreenshots = 15;

	public static bool TryCreateZip(string applicationBaseDirectory, string zipFilePath, out string errorMessage)
	{
		errorMessage = null;
		try
		{
			string dataDir = Path.Combine(applicationBaseDirectory, "Data");
			string shotDir = Path.Combine(dataDir, "screenshots");
			if (File.Exists(zipFilePath))
			{
				File.Delete(zipFilePath);
			}
			using (ZipArchive zip = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
			{
				void AddFileIfExists(string fullPath, string entryName)
				{
					if (!File.Exists(fullPath))
					{
						return;
					}
					zip.CreateEntryFromFile(fullPath, entryName, CompressionLevel.Optimal);
				}
				foreach (string logName in LogNames)
				{
					AddFileIfExists(Path.Combine(dataDir, logName), "Data/" + logName);
				}
				AddFileIfExists(Path.Combine(dataDir, "Setting.txt"), "Data/Setting.txt");
				if (Directory.Exists(shotDir))
				{
					string[] files = Directory.GetFiles(shotDir, "*.png", SearchOption.TopDirectoryOnly);
					Array.Sort(files, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
					int n = 0;
					foreach (string f in files)
					{
						if (n >= MaxScreenshots)
						{
							break;
						}
						zip.CreateEntryFromFile(f, "Data/screenshots/" + Path.GetFileName(f), CompressionLevel.Optimal);
						n++;
					}
				}
				string readme = "PlayAPP — gói chẩn đoán\r\n"
					+ "Thời gian: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n"
					+ "Gồm: các file log (nếu có), Setting.txt, tối đa " + MaxScreenshots + " ảnh PNG mới nhất trong Data/screenshots.\r\n"
					+ "KHÔNG gồm: Account.txt, proxy.txt (thông tin nhạy cảm).\r\n"
					+ "Khi gửi hỗ trợ, kiểm tra lại ZIP trước khi đính kèm.\r\n";
				ZipArchiveEntry entry = zip.CreateEntry("readme_diagnostics.txt");
				using (Stream s = entry.Open())
				{
					byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(readme);
					s.Write(bytes, 0, bytes.Length);
				}
			}
			return true;
		}
		catch (Exception ex)
		{
			errorMessage = ex.Message;
			return false;
		}
	}
}
