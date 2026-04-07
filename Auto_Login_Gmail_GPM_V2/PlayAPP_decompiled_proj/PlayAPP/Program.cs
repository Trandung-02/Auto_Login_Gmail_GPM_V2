using System;
using System.IO;
using System.Windows.Forms;

namespace PlayAPP;

internal static class Program
{
	[STAThread]
	private static void Main()
	{
		try
		{
			Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
		}
		catch
		{
		}
		ApplicationConfiguration.Initialize();
		Application.Run(new Form1());
	}
}
