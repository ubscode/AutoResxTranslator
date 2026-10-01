using AutoResxTranslator.Definitions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;

namespace AutoResxTranslator
{
	/// <summary>
	/// Translation service using Microsoft Cogntive service.
	/// 
	/// ref: https://azure.microsoft.com/en-in/services/cognitive-services/translator-text-api/
	/// </summary>
	public class DeepLTranslateService
	{
		private const string DeepLFreeCognitiveServicesApiUrl = "https://api-free.deepl.com";
		private const string DeepLProCognitiveServicesApiUrl = "https://api.deepl.com";

		private readonly struct TextTranslateResult
		{
			/// <summary>Initializes a new instance of <see cref="TextTranslateResult" />, used for JSON deserialization.</summary>
			[JsonConstructor]
			public TextTranslateResult(TextResult[] translations)
			{
				Translations = translations;
			}

			/// <summary>Array of <see cref="TextResult" /> objects holding text translation results.</summary>
			public TextResult[] Translations { get; }
			public sealed class TextResult
			{
				/// <summary>Initializes a new instance of <see cref="TextResult" />.</summary>
				/// <param name="text">Translated text.</param>
				/// <param name="detectedSourceLanguageCode">The detected language code of the input text.</param>
				/// <remarks>
				///   The constructor for this class (and all other Model classes) should not be used by library users. Ideally it
				///   would be marked <see langword="internal" />, but needs to be <see langword="public" /> for JSON deserialization.
				///   In future this function may have backwards-incompatible changes.
				/// </remarks>
				[JsonConstructor]
				public TextResult(string text, string detectedSourceLanguageCode)
				{
					Text = text;
					DetectedSourceLanguageCode = detectedSourceLanguageCode;
				}

				/// <summary>The translated text.</summary>
				public string Text { get; }

				/// <summary>The language code of the source text detected by DeepL.</summary>
				[JsonProperty("detected_source_language")]
				public string DetectedSourceLanguageCode { get; }

				/// <summary>Returns the translated text.</summary>
				/// <returns>The translated text.</returns>
				public override string ToString() => Text;
			}
		}

		public static async Task<ResultHolder<string>> TranslateAsync(string text,
			string fromLanguage,
			string toLanguage,
			string subscriptionKey,
			string region,
			CancellationToken cancellationToken = default(CancellationToken))
		{
			var sw = Stopwatch.StartNew();
			if (fromLanguage.Equals("auto"))
			{
				fromLanguage = "";
			}

			var route = "/v2/translate";

			try
			{
				AppLog.Info($"DeepL request start | from={fromLanguage ?? "auto"} to={toLanguage} chars={text?.Length ?? 0} regionType={region}");
				using (var client = new HttpClient())
				using (var request = new HttpRequestMessage())
				{
					client.Timeout = TimeSpan.FromSeconds(60);
					client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
					client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
					// Build the request.

					var data = new Dictionary<string, string>
					{
						{ "auth_key", subscriptionKey },
						{ "text", text },
						{ "target_lang", toLanguage.ToUpper() }
					};
					
					if (!string.IsNullOrEmpty(fromLanguage))
						data.Add("source_lang", fromLanguage.ToUpper());
					
					var response = await client.PostAsync(
						(region == "0" ? DeepLFreeCognitiveServicesApiUrl : DeepLProCognitiveServicesApiUrl) + route,
							new FormUrlEncodedContent(data), cancellationToken);
					AppLog.Info($"DeepL response | status={(int)response.StatusCode} {response.ReasonPhrase} | elapsedMs={sw.ElapsedMilliseconds}");
					response.EnsureSuccessStatusCode();

					if (response.StatusCode == HttpStatusCode.OK)
					{
						// Read response as a string.
						var resultFromDeepL = await response.Content.ReadAsStringAsync();
						var deserializedOutput = JsonConvert.DeserializeObject<TextTranslateResult>(resultFromDeepL);

						// Iterate over the results, return the first result
						foreach (var t in deserializedOutput.Translations)
						{
							AppLog.Info($"DeepL translation success | elapsedMs={sw.ElapsedMilliseconds}");
							return new ResultHolder<string>(true, t.Text);
						}
					}
					else
					{
						AppLog.Warn($"DeepL translation failed by status | elapsedMs={sw.ElapsedMilliseconds}");
						return new ResultHolder<string>(false, "Translation failed! Reason: " + response.ReasonPhrase);
					}
				}

				AppLog.Warn($"DeepL translation returned no results | elapsedMs={sw.ElapsedMilliseconds}");
				return new ResultHolder<string>(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception e)
			{
				AppLog.Error($"DeepL translation exception | elapsedMs={sw.ElapsedMilliseconds}", e);
				return new ResultHolder<string>(false, "Translation failed! Exception: " + e.Message);
			}
		}
	}
}
