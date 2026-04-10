using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace PlayAPP;

/// <summary>
/// Điều khiển cửa sổ Chrome trên Windows (GPM / Chrome thường dùng lớp Chrome_WidgetWin_1).
/// </summary>
internal static class ChromeWindowNativeHelper
{
	public const int MinimizeWhenChromeWindowCountExceeds = 20;

	private const int SwMaximize = 3;
	private const int SwMinimize = 6;

	private const string ChromeWidgetClass = "Chrome_WidgetWin_1";

	private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

	[DllImport("user32.dll")]
	private static extern bool IsWindowVisible(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

	private const uint GwOwner = 4;

	[StructLayout(LayoutKind.Sequential)]
	private struct Rect
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

	private static bool IsChromeMainFrame(IntPtr hWnd)
	{
		if (hWnd == IntPtr.Zero || !IsWindowVisible(hWnd))
		{
			return false;
		}
		if (GetWindow(hWnd, GwOwner) != IntPtr.Zero)
		{
			return false;
		}
		var cls = new StringBuilder(256);
		if (GetClassName(hWnd, cls, cls.Capacity) <= 0)
		{
			return false;
		}
		if (!string.Equals(cls.ToString(), ChromeWidgetClass, StringComparison.Ordinal))
		{
			return false;
		}
		if (!GetWindowRect(hWnd, out Rect r))
		{
			return false;
		}
		int h = r.Bottom - r.Top;
		int w = r.Right - r.Left;
		return w >= 320 && h >= 240;
	}

	private static void CollectChromeMainWindows(List<IntPtr> buffer)
	{
		buffer.Clear();
		EnumWindows((hWnd, _) =>
		{
			if (IsChromeMainFrame(hWnd))
			{
				buffer.Add(hWnd);
			}
			return true;
		}, IntPtr.Zero);
	}

	public static int CountChromeMainWindows()
	{
		var list = new List<IntPtr>(64);
		CollectChromeMainWindows(list);
		return list.Count;
	}

	/// <summary>
	/// Nếu có hơn <paramref name="threshold"/> cửa sổ Chrome chính, thu nhỏ tất cả cửa sổ đó.
	/// </summary>
	public static void MinimizeAllChromeMainWindowsIfOverThreshold(int threshold = MinimizeWhenChromeWindowCountExceeds)
	{
		var list = new List<IntPtr>(128);
		CollectChromeMainWindows(list);
		if (list.Count <= threshold)
		{
			return;
		}
		for (int i = 0; i < list.Count; i++)
		{
			try
			{
				ShowWindow(list[i], SwMinimize);
			}
			catch
			{
			}
		}
	}

	private static bool TitleMatchesAccountIndex(string title, int oneBasedIndex)
	{
		if (string.IsNullOrEmpty(title) || oneBasedIndex < 1)
		{
			return false;
		}
		string p = "#" + oneBasedIndex;
		if (!title.StartsWith(p, StringComparison.Ordinal))
		{
			return false;
		}
		if (title.Length == p.Length)
		{
			return true;
		}
		char c = title[p.Length];
		return c == ' ' || c == '-' || c == '\u2013' || c == '\u2014';
	}

	/// <summary>
	/// Phóng to cửa sổ Chrome có tiêu đề bắt đầu bằng #&lt;oneBasedIndex&gt; (khớp init script __AUTO_ID trong Form1).
	/// </summary>
	public static bool TryMaximizeChromeWindowByAccountTitle(int oneBasedIndex)
	{
		IntPtr found = IntPtr.Zero;
		EnumWindows((hWnd, _) =>
		{
			if (!IsChromeMainFrame(hWnd))
			{
				return true;
			}
			var sb = new StringBuilder(512);
			if (GetWindowText(hWnd, sb, sb.Capacity) <= 0)
			{
				return true;
			}
			if (TitleMatchesAccountIndex(sb.ToString(), oneBasedIndex))
			{
				found = hWnd;
				return false;
			}
			return true;
		}, IntPtr.Zero);
		if (found == IntPtr.Zero)
		{
			return false;
		}
		try
		{
			ShowWindow(found, SwMaximize);
			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// Phóng to cửa sổ đang foreground nếu là Chrome chính (sau BringToFrontAsync).
	/// </summary>
	public static bool TryMaximizeForegroundIfChromeMain()
	{
		IntPtr fg = GetForegroundWindow();
		if (fg == IntPtr.Zero || !IsChromeMainFrame(fg))
		{
			return false;
		}
		try
		{
			ShowWindow(fg, SwMaximize);
			return true;
		}
		catch
		{
			return false;
		}
	}
}
