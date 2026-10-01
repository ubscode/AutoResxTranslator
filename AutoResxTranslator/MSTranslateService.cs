using AutoResxTranslator.Definitions;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace AutoResxTranslator
{
	/// <summary>
	/// Translation service using Microsoft Cogntive service.
	/// 
	/// ref: https://azure.microsoft.com/en-in/services/cognitive-services/translator-text-api/
	/// </summary>
	public class MsTranslateService
	{
		private const string MsCognitiveServicesApiUrl = "https://api.cognitive.microsofttranslator.com";

		public static async Task<ResultHolder<string>> TranslateAsync(string text,
			string fromLanguage,
			string toLanguage,
			string subscriptionKey,
			string region,
			CancellationToken cancellationToken = default(CancellationToken))
		{
			var sw = Stopwatch.StartNew();

			if (fromLanguage.Equals("auto") || fromLanguage.Equals(""))
			{
				fromLanguage = null;
			}

			var route = "/translate?api-version=3.0&to=" + toLanguage;
			if (!string.IsNullOrEmpty(fromLanguage))
			{
				route += "&from=" + fromLanguage;
			}

			try
			{
				AppLog.Info($"MS request start | from={fromLanguage ?? "auto"} to={toLanguage} chars={text?.Length ?? 0}");
				var body = new object[] { new { Text = text } };
				var requestBody = JsonConvert.SerializeObject(body);

				using (var client = new HttpClient())
				using (var request = new HttpRequestMessage())
				{
					client.Timeout = TimeSpan.FromSeconds(60);

					// Build the request.
					request.Method = HttpMethod.Post;
					request.RequestUri = new Uri(MsCognitiveServicesApiUrl + route);
					request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
					request.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
					request.Headers.Add("Ocp-Apim-Subscription-Region", region);
					request.Version = new Version("1.1");

					// Send the request and get response.
					var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
					AppLog.Info($"MS response | status={(int)response.StatusCode} {response.ReasonPhrase} | elapsedMs={sw.ElapsedMilliseconds}");

					if (response.StatusCode == System.Net.HttpStatusCode.OK)
					{
						// Read response as a string.
						var resultFromMs = await response.Content.ReadAsStringAsync();

						var deserializedOutput = JsonConvert.DeserializeObject<MsTranslationApi.TranslationResult[]>(resultFromMs);

						// Iterate over the deserialized results.
						foreach (var output in deserializedOutput)
						{
							// Iterate over the results, return the first result
							foreach (var t in output.Translations)
							{
								AppLog.Info($"MS translation success | elapsedMs={sw.ElapsedMilliseconds}");
								return new ResultHolder<string>(true, t.Text);
							}
						}
					}
					else
					{
						AppLog.Warn($"MS translation failed by status | elapsedMs={sw.ElapsedMilliseconds}");
						return new ResultHolder<string>(false, "Translation failed! Exception: " + response.ReasonPhrase);
					}
				}

				AppLog.Warn($"MS translation returned no results | elapsedMs={sw.ElapsedMilliseconds}");
				return new ResultHolder<string>(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception e)
			{
				AppLog.Error($"MS translation exception | elapsedMs={sw.ElapsedMilliseconds}", e);
				return new ResultHolder<string>(false, "Translation failed! Exception: " + e.Message);
			}
		}
	}
}
