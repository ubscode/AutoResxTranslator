using System;
using System.Windows.Forms;

/* 
 * AutoResxTranslator
 * by Salar Khalilzadeh
 * 
 * https://github.com/salarcode/AutoResxTranslator/
 * Mozilla Public License v2
 */
namespace AutoResxTranslator
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
			{
				AppLog.Error("Unhandled domain exception",
					args.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception object."));
			};
			Application.ThreadException += (sender, args) =>
			{
				AppLog.Error("Unhandled UI thread exception", args.Exception);
			};

			AppLog.Info("Application startup.");
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			AppLog.Info("Starting frmMain.");
			Application.Run(new frmMain());
			AppLog.Info("Application shutdown.");
		}
	}
}
