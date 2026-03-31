using System;
using System.IO;
using System.Text;
using System.Threading;

namespace AutoResxTranslator
{
	internal static class AppLog
	{
		private static readonly object Sync = new object();
		private static readonly string LogDirectory =
			Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"AutoResxTranslator",
				"logs");

		private static readonly string LogFilePath =
			Path.Combine(LogDirectory, $"AutoResxTranslator-{DateTime.Now:yyyyMMdd}.log");

		public static string CurrentLogFilePath => LogFilePath;

		public static void Info(string message)
		{
			Write("INF", message, null);
		}

		public static void Warn(string message)
		{
			Write("WRN", message, null);
		}

		public static void Error(string message, Exception ex = null)
		{
			Write("ERR", message, ex);
		}

		private static void Write(string level, string message, Exception ex)
		{
			try
			{
				Directory.CreateDirectory(LogDirectory);

				var line = new StringBuilder();
				line.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
				line.Append(" [");
				line.Append(level);
				line.Append("] [T");
				line.Append(Thread.CurrentThread.ManagedThreadId);
				line.Append("] ");
				line.Append(message ?? string.Empty);

				if (ex != null)
				{
					line.Append(" | ");
					line.Append(ex.GetType().FullName);
					line.Append(": ");
					line.Append(ex.Message);
					if (!string.IsNullOrWhiteSpace(ex.StackTrace))
					{
						line.Append(" | ");
						line.Append(ex.StackTrace.Replace(Environment.NewLine, " || "));
					}
				}

				line.AppendLine();

				lock (Sync)
				{
					File.AppendAllText(LogFilePath, line.ToString(), Encoding.UTF8);
				}
			}
			catch
			{
				// Never throw from logger.
			}
		}
	}
}
