using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Text;
using System.Web;
using System.Threading;
using System.Globalization;
using Newtonsoft.Json.Linq;

/* 
 * AutoResxTranslator
 * by Salar Khalilzadeh
 * 
 * https://github.com/salarcode/AutoResxTranslator/
 * Mozilla Public License v2
 */
namespace AutoResxTranslator
{
	public class GTranslateService
	{
		private const string RequestUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:91.0) Gecko/20100101 Firefox/91.0";
		private const string RequestGoogleTranslatorUrl = "https://translate.googleapis.com/translate_a/single?client=gtx&sl={0}&tl={1}&hl=en&dt=t&dt=bd&dj=1&source=icon&tk=467103.467103&q={2}";


		public delegate void TranslateCallBack(bool succeed, string result);
		public static void TranslateAsync(
			string text,
			string sourceLng,
			string destLng,
			string textTranslatorUrlKey,
			TranslateCallBack callBack)
		{
			var request = CreateWebRequest(text, sourceLng, destLng, textTranslatorUrlKey);
			request.BeginGetResponse(
				TranslateRequestCallBack,
				new KeyValuePair<WebRequest, TranslateCallBack>(request, callBack));
		}

		public static bool Translate(
			string text,
			string sourceLng,
			string destLng,
			string textTranslatorUrlKey,
			out string result,
			CancellationToken cancellationToken = default(CancellationToken))
		{
			TimeSpan? retryAfter;
			return Translate(text, sourceLng, destLng, textTranslatorUrlKey, out result, out retryAfter, cancellationToken);
		}

		public static bool Translate(
			string text,
			string sourceLng,
			string destLng,
			string textTranslatorUrlKey,
			out string result,
			out TimeSpan? retryAfter,
			CancellationToken cancellationToken = default(CancellationToken))
		{
			retryAfter = null;
			var sw = Stopwatch.StartNew();
			AppLog.Info($"Google request start | from={sourceLng} to={destLng} chars={text?.Length ?? 0}");
			var request = CreateWebRequest(text, sourceLng, destLng, textTranslatorUrlKey);
			try
			{
				using (cancellationToken.Register(() => request.Abort()))
				using (var response = (HttpWebResponse)request.GetResponse())
				{
					if (response.StatusCode != HttpStatusCode.OK)
					{
						result = "Response is failed with code: " + response.StatusCode;
						return false;
					}

					using (var stream = response.GetResponseStream())
					{
						var succeed = ReadGoogleTranslatedResult(stream, out var output);
						result = output;
						AppLog.Info($"Google request done | success={succeed} | elapsedMs={sw.ElapsedMilliseconds}");
						return succeed;
					}
				}
			}
			catch (WebException ex) when (ex.Response is HttpWebResponse response && (int)response.StatusCode == 429)
			{
				retryAfter = ParseRetryAfter(response.Headers[HttpResponseHeader.RetryAfter]);
				AppLog.Warn($"Google rate limit | elapsedMs={sw.ElapsedMilliseconds} retryAfter={retryAfter}");
				result = ex.Message;
				return false;
			}
			catch (Exception ex)
			{
				AppLog.Error($"Google request exception | elapsedMs={sw.ElapsedMilliseconds}", ex);
				result = ex.Message;
				return false;
			}
		}

		private static TimeSpan ParseRetryAfter(string value)
		{
			if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0)
				return TimeSpan.FromSeconds(seconds);
			if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
				DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
				return date > DateTimeOffset.UtcNow ? date - DateTimeOffset.UtcNow : TimeSpan.Zero;
			return TimeSpan.Zero;
		}

		static WebRequest CreateWebRequest(
			string text,
			string lngSourceCode,
			string lngDestinationCode,
			string textTranslatorUrlKey)
		{
			text = HttpUtility.UrlEncode(text);

			var url = string.Format(RequestGoogleTranslatorUrl, lngSourceCode, lngDestinationCode, text);


			var create = (HttpWebRequest)WebRequest.Create(url);
			create.UserAgent = RequestUserAgent;
			create.Timeout = 50 * 1000;
			return create;
		}

		private static void TranslateRequestCallBack(IAsyncResult ar)
		{
			var pair = (KeyValuePair<WebRequest, TranslateCallBack>)ar.AsyncState;
			var request = pair.Key;
			var callback = pair.Value;
			HttpWebResponse response = null;
			try
			{
				response = (HttpWebResponse)request.EndGetResponse(ar);
				if (response.StatusCode != HttpStatusCode.OK)
				{
					callback(false, "Response is failed with code: " + response.StatusCode);
					return;
				}

				using (var stream = response.GetResponseStream())
				{
					string output;
					var succeed = ReadGoogleTranslatedResult(stream, out output);

					callback(succeed, output);
				}
			}
			catch (Exception ex)
			{
				callback(false, "Request failed.\r\n" + ex.Message);
			}
			finally
			{
				response?.Close();
			}
		}

		/// <summary>
		///  the main trick :)
		/// </summary>
		static bool ReadGoogleTranslatedResult(Stream rawdata, out string result)
		{
			string text;
			using (var reader = new StreamReader(rawdata, Encoding.UTF8))
			{
				text = reader.ReadToEnd();
			}

			try
			{
				var obj = JToken.Parse(text);
				var segments = obj is JObject ? obj["sentences"] : obj[0];
				var final = new StringBuilder();
				foreach (var segment in segments)
					final.Append((string)(segment is JObject ? segment["trans"] : segment[0]));
				result = final.ToString();
				if (result.Length == 0) return false;
				return true;
			}
			catch (Exception ex)
			{
				result = ex.Message;
				return false;
			}
		}

	}
}
