using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;
using AutoResxTranslator.Definitions;

/* 
 * AutoResxTranslator
 * by Salar Khalilzadeh
 * 
 * https://github.com/salarcode/AutoResxTranslator/
 * Mozilla Public License v2
 */

namespace AutoResxTranslator
{
	public partial class frmMain : Form
	{
		public frmMain()
		{
			InitializeComponent();
			AppLog.Info("frmMain initialized.");
		}

		private readonly Dictionary<string, string> _languages =
			new Dictionary<string, string>
			{
				{"auto", "(Detect)"},
				{"af", "Afrikaans"},
				{"sq", "Albanian"},
				{"ar", "Arabic"},
				{"hy", "Armenian"},
				{"az", "Azerbaijani"},
				{"eu", "Basque"},
				{"be", "Belarusian"},
				{"bn", "Bengali"},
				{"bg", "Bulgarian"},
				{"ca", "Catalan"},
				{"zh-CN", "Chinese (Simplified)"},
				{"zh-TW", "Chinese (Traditional)"},
				{"hr", "Croatian"},
				{"cs", "Czech"},
				{"da", "Danish"},
				{"nl", "Dutch"},
				{"en", "English"},
				{"eo", "Esperanto"},
				{"et", "Estonian"},
				{"tl", "Filipino"},
				{"fi", "Finnish"},
				{"fr", "French"},
				{"gl", "Galician"},
				{"ka", "Georgian"},
				{"de", "German"},
				{"el", "Greek"},
				{"gu", "Gujarati"},
				{"ht", "Haitian Creole"},
				{"iw", "Hebrew"},
				{"hi", "Hindi"},
				{"hu", "Hungarian"},
				{"is", "Icelandic"},
				{"id", "Indonesian"},
				{"ga", "Irish"},
				{"it", "Italian"},
				{"ja", "Japanese"},
				{"kn", "Kannada"},
				{"km", "Khmer"},
				{"ko", "Korean"},
				{"lo", "Lao"},
				{"la", "Latin"},
				{"lv", "Latvian"},
				{"lt", "Lithuanian"},
				{"mk", "Macedonian"},
				{"ms", "Malay"},
				{"mt", "Maltese"},
				{"no", "Norwegian"},
				{"fa", "Persian"},
				{"pl", "Polish"},
				{"pt-PT", "Portuguese - Portugal"},
				{"pt-BR", "Portuguese - Brazil"},
				{"ro", "Romanian"},
				{"ru", "Russian"},
				{"sr", "Serbian"},
				{"sk", "Slovak"},
				{"sl", "Slovenian"},
				{"es", "Spanish"},
				{"sw", "Swahili"},
				{"sv", "Swedish"},
				{"ta", "Tamil"},
				{"te", "Telugu"},
				{"th", "Thai"},
				{"tr", "Turkish"},
				{"uk", "Ukrainian"},
				{"ur", "Urdu"},
				{"vi", "Vietnamese"},
				{"cy", "Welsh"},
				{"yi", "Yiddish"}
			};
		private bool _translateSettingsChanged;
		private DateTime _lastProgressLogUtc = DateTime.MinValue;

		private static string SafeText(string value, int maxLength = 160)
		{
			if (string.IsNullOrEmpty(value))
				return string.Empty;

			var cleaned = value.Replace("\r", " ").Replace("\n", " ");
			if (cleaned.Length <= maxLength)
				return cleaned;

			return cleaned.Substring(0, maxLength) + "...";
		}

		ServiceTypeEnum ServiceType
		{
			get
			{
				if (rbtnGoogleTranslateService.Checked)
					return ServiceTypeEnum.Google;
				if (rbtnDeepLTranslateService.Checked)
					return ServiceTypeEnum.DeepL;
				return ServiceTypeEnum.Microsoft;
			}
		}

		void FillComboBoxes()
		{
			AppLog.Info("FillComboBoxes start.");
			cmbSrc.DisplayMember = "Value";
			cmbSrc.ValueMember = "Key";

			cmbDesc.DisplayMember = "Value";
			cmbDesc.ValueMember = "Key";
			lstResxLanguages.Items.Clear();

			foreach (var k in _languages)
			{
				cmbSrc.Items.Add(k);
				if (k.Key == "auto")
					continue;
				cmbDesc.Items.Add(k);
				cmbSourceResxLng.Items.Add(k);
				lstResxLanguages.Items.Add(k.Key, k.Value, -1);
			}
			cmbSrc.SelectedIndex = 0;
			cmbDesc.Text = "English";
			AppLog.Info($"FillComboBoxes done. Languages loaded={lstResxLanguages.Items.Count}");
		}

		void SetResult(string result)
		{
			AppLog.Info($"SetResult called. textLength={result?.Length ?? 0} preview='{SafeText(result)}'");
			if (this.InvokeRequired)
			{
				this.BeginInvoke(new Action<string>(SetResult), result);
				return;
			}
			else
			{
				txtDesc.Text = result;
			}
		}

		void IsBusy(bool isbusy)
		{
			AppLog.Info($"IsBusy({isbusy})");
			if (this.InvokeRequired)
			{
				this.BeginInvoke(new Action<bool>(IsBusy), isbusy);
				return;
			}
			else
			{
				tabMain.Enabled = !isbusy;
			}
		}


		string ReadLanguageName(string fileName)
		{
			try
			{
				fileName = Path.GetFileName(fileName);
				var match = Regex.Match(fileName, @".+\.(?<lng>.+)\.resx");
				var language = match.Groups["lng"].Value;
				var culture = new CultureInfo(language);
				return language;
			}
			catch (Exception ex)
			{
				AppLog.Warn($"ReadLanguageName fallback to 'en' for '{fileName}'. {ex.Message}");
				return "en";
			}
		}

		string ReadLanguageFilename(string fileName)
		{
			try
			{
				fileName = Path.GetFileName(fileName);
				var match = Regex.Match(fileName, @"(?<file>.+)\.(?<lng>.+)\.resx");
				var file = match.Groups["file"].Value;
				if (string.IsNullOrWhiteSpace(file))
				{
					file = Path.GetFileNameWithoutExtension(fileName);
				}
				return file;
			}
			catch (Exception ex)
			{
				AppLog.Warn($"ReadLanguageFilename fallback to 'res' for '{fileName}'. {ex.Message}");
				return "res";
			}
		}



		bool ValidateResxTranslate()
		{
			string errors = "";
			AppLog.Info("ValidateResxTranslate start.");

			if (!File.Exists(txtSourceResx.Text))
				errors += "Please select source ResX file.\n";
			if (cmbSourceResxLng.SelectedIndex == -1)
				errors += "Please select source ResX file's language.\n";
			if (!Directory.Exists(txtOutputDir.Text))
				errors += "Please select output directory.\n";

			bool anychecked = false;
			foreach (ListViewItem item in lstResxLanguages.Items)
			{
				if (item.Checked)
				{
					anychecked = true;
					break;
				}
			}
			if (!anychecked)
			{
				errors += "At least one output language should be selected.\n";
			}

			if (errors.Length > 0)
			{
				AppLog.Warn("ValidateResxTranslate failed: " + SafeText(errors, 500));
				MessageBox.Show(errors, "", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return false;
			}
			AppLog.Info("ValidateResxTranslate passed.");
			return true;
		}


		void TranslateResxFiles()
		{
			var srcLng = ((KeyValuePair<string, string>)cmbSourceResxLng.SelectedItem).Key;
			var destLanguages = new List<string>();
			foreach (ListViewItem item in lstResxLanguages.Items)
			{
				if (!item.Checked)
					continue;
				if (item.Name == srcLng)
					continue;
				destLanguages.Add(item.Name);
			}
			if (destLanguages.Count == 0)
			{
				AppLog.Warn("TranslateResxFiles aborted: destination language list is empty after filtering source language.");
				MessageBox.Show("The source and the destination languages can not be the same.", "", MessageBoxButtons.OK,
					MessageBoxIcon.Error);
				return;
			}
			bool translateFromKey = chkTranslateFromKey.Checked;

			var translationOptions = new TranslationOptions
			{
				ServiceType = ServiceType,
				MsSubscriptionKey = txtMsTranslationKey.Text,
				MsSubscriptionRegion = txtMsTranslationRegion.Text,
				DeepLSubscriptionKey = txtDeepLTranslationKey.Text,
				DeepLSubscriptionRegion = cmbDeeplApiType.SelectedIndex.ToString()
			};

			AppLog.Info(
				$"TranslateResxFiles start | service={translationOptions.ServiceType} source='{txtSourceResx.Text}' sourceLng={srcLng} " +
				$"destCount={destLanguages.Count} translateFromKey={translateFromKey} onlyNew={checkBoxTranslateOnlyNew.Checked} csv={chkCSVOutput.Checked} outputDir='{txtOutputDir.Text}'");
			AppLog.Info("Destination languages: " + string.Join(", ", destLanguages));

			IsBusy(true);
			new Action<string, string, TranslationOptions, List<string>, string, ResxProgressCallback, bool, bool, bool, string>(TranslateResxFilesAsync).BeginInvoke(
				txtSourceResx.Text,
				srcLng,
				translationOptions,
				destLanguages,
				txtOutputDir.Text,
				ResxWorkingProgress,
				translateFromKey,
				checkBoxTranslateOnlyNew.Checked,
				chkCSVOutput.Checked,
				txtCSVOutputDir.Text,
				(x) =>
				{
					AppLog.Info("TranslateResxFilesAsync delegate callback invoked. Setting IsBusy(false).");
					IsBusy(false);
				},
				null);
		}

		private delegate void ResxProgressCallback(int max, int pos, string status);

		async void TranslateResxFilesAsync(
			string sourceResx,
			string sourceLng,
			TranslationOptions translationOptions,
			List<string> desLanguages, string destDir,
			ResxProgressCallback progress,
			bool translateFromKey,
			bool translateOnlyNewKeys,
			bool generateCsv,
			string generateCsvDir)
		{
			var totalSw = Stopwatch.StartNew();
			int max = 0;
			int pos = 0;
			int trycount = 0;
			string status = "";
			bool hasErrors = false;
			AppLog.Info(
				$"TranslateResxFilesAsync start | source='{sourceResx}' sourceLng={sourceLng} destDir='{destDir}' " +
				$"destCount={desLanguages?.Count ?? 0} service={translationOptions.ServiceType} translateFromKey={translateFromKey} onlyNew={translateOnlyNewKeys} csv={generateCsv}");

			var sourceResxFilename = ReadLanguageFilename(sourceResx);
			var errorLogFilename = sourceResxFilename + ".errors.log";
			var errorLogFile = Path.Combine(destDir, errorLogFilename);

			try
			{
				foreach (var destLng in desLanguages)
				{
					var languageSw = Stopwatch.StartNew();
					var destFile = Path.Combine(destDir, sourceResxFilename + "." + destLng + ".resx");
					AppLog.Info($"Language start | target={destLng} destFile='{destFile}'");

					var doc = new XmlDocument();
					doc.Load(sourceResx);
					var dataList = ResxTranslator.ReadResxData(doc);
					max = dataList.Count;
					AppLog.Info($"Language loaded | target={destLng} totalKeys={max}");

					string[] csvOutputDataBuffer = null;
					if (generateCsv)
						csvOutputDataBuffer = new string[max];

					List<XmlNode> destinationDataList = null;
					var destTranslateOnlyNewKeys =
							translateOnlyNewKeys &&
							File.Exists(destFile);

					if (destTranslateOnlyNewKeys)
					{
						var destDoc = new XmlDocument();
						destDoc.Load(destFile);
						destinationDataList = ResxTranslator.ReadResxData(destDoc);
						AppLog.Info($"Language existing destination loaded | target={destLng} existingKeys={destinationDataList.Count}");
					}

					pos = 0;
					status = "Translating language: " + destLng;
					progress.BeginInvoke(max, pos, status, null, null);

					try
					{
						int destIndexCorrection = 0;
						foreach (var (node, index) in dataList.Select((n, i) => (n, i)))
						{
							var itemSw = Stopwatch.StartNew();
							status = "Translating language: " + destLng;
							pos += 1;
							progress.BeginInvoke(max, pos, status, null, null);
							var valueNode = ResxTranslator.GetDataValueNode(node);
							var keyNode = ResxTranslator.GetDataKeyName(node);
							AppLog.Info($"Key start | target={destLng} index={index + 1}/{max} key='{SafeText(keyNode, 120)}'");

							try
							{
								if (valueNode == null)
								{
									AppLog.Warn($"Key skipped (no value node) | target={destLng} index={index + 1} key='{SafeText(keyNode, 120)}'");
									continue;
								}

								var orgText = translateFromKey ? keyNode : valueNode.InnerText;

								if (destTranslateOnlyNewKeys)
								{
									int destIndex = index - destIndexCorrection;
									if (destIndex < 0 || destIndex >= destinationDataList.Count)
									{
										AppLog.Warn($"Key only-new index mismatch | target={destLng} index={index + 1} destIndex={destIndex} correction={destIndexCorrection}");
									}
									else
									{
										var destNode = ResxTranslator.GetDataKeyName(destinationDataList.ElementAt(destIndex));
										if (destNode == keyNode)
										{
											var destValueNode = ResxTranslator.GetDataValueNode(destinationDataList.ElementAt(destIndex));
											var existingDestinationValue = destValueNode?.InnerText;
											if (!string.IsNullOrWhiteSpace(existingDestinationValue))
											{
												valueNode.InnerText = existingDestinationValue;
												if (generateCsv)
													csvOutputDataBuffer[index] = keyNode + "," + valueNode.InnerText;

												AppLog.Info($"Key reused existing translation | target={destLng} index={index + 1} elapsedMs={itemSw.ElapsedMilliseconds}");
												continue;
											}

											AppLog.Info($"Key found with empty destination value; translating | target={destLng} index={index + 1}");
										}
										else
										{
											destIndexCorrection++;
										}
									}
								}

								if (string.IsNullOrWhiteSpace(orgText))
								{
									AppLog.Warn($"Key skipped (empty text) | target={destLng} index={index + 1} key='{SafeText(keyNode, 120)}'");
									continue;
								}

								if (translationOptions.ServiceType == ServiceTypeEnum.Google)
								{
									// There is no longer a key to validate
									// the key
									var textTranslatorUrlKey = "";

									string translated = string.Empty;
									bool success = false;
									trycount = 0;
									do
									{
										try
										{
											AppLog.Info($"Google call | target={destLng} index={index + 1} try={trycount + 1}");
											success = GTranslateService.Translate(orgText, sourceLng, destLng, textTranslatorUrlKey, out translated);
										}
										catch (Exception ex)
										{
											success = false;
											AppLog.Error($"Google call exception | target={destLng} index={index + 1} try={trycount + 1}", ex);
										}
										trycount++;

										if (!success)
										{
											status = "Translating language: " + destLng + " , key '" + keyNode + "' failed to translate in try " + trycount;
											progress.BeginInvoke(max, pos, status, null, null);
										}

									} while (success == false && trycount <= 2);

									if (success)
									{
										valueNode.InnerText = translated;
									}
									else
									{
										hasErrors = true;
										try
										{
											string message = "\r\nKey '" + keyNode + "' translation to language '" + destLng + "' failed.";
											File.AppendAllText(errorLogFile, message);
										}
										catch (Exception ex)
										{
											AppLog.Error("Failed writing google error log entry.", ex);
										}
									}
								}
								else if (translationOptions.ServiceType == ServiceTypeEnum.Microsoft)
								{
									AppLog.Info($"Microsoft call | target={destLng} index={index + 1} key='{SafeText(keyNode, 120)}' textLength={orgText.Length}");
									var translationResult = await MsTranslateService.TranslateAsync(orgText, sourceLng, destLng,
										translationOptions.MsSubscriptionKey, translationOptions.MsSubscriptionRegion);

									if (translationResult.Success)
									{
										valueNode.InnerText = translationResult.Result;
									}
									else
									{
										hasErrors = true;
										var key = ResxTranslator.GetDataKeyName(node);
										try
										{
											string message = "\r\nKey '" + key + "' translation to language '" + destLng + "' failed. ";
											if (!string.IsNullOrEmpty(translationResult.Result))
												message += " Error message: " + translationResult.Result;

											File.AppendAllText(errorLogFile, message);
										}
										catch (Exception ex)
										{
											AppLog.Error("Failed writing microsoft error log entry.", ex);
										}
									}
								}
								else if (translationOptions.ServiceType == ServiceTypeEnum.DeepL)
								{
									AppLog.Info($"DeepL call | target={destLng} index={index + 1} key='{SafeText(keyNode, 120)}' textLength={orgText.Length}");
									var translationResult = await DeepLTranslateService.TranslateAsync(orgText, sourceLng, destLng,
										translationOptions.DeepLSubscriptionKey, translationOptions.DeepLSubscriptionRegion);

									if (translationResult.Success)
									{
										valueNode.InnerText = translationResult.Result;
									}
									else
									{
										hasErrors = true;
										var key = ResxTranslator.GetDataKeyName(node);
										try
										{
											string message = "\r\nKey '" + key + "' translation to language '" + destLng + "' failed. ";
											if (!string.IsNullOrEmpty(translationResult.Result))
												message += " Error message: " + translationResult.Result;

											File.AppendAllText(errorLogFile, message);
										}
										catch (Exception ex)
										{
											AppLog.Error("Failed writing DeepL error log entry.", ex);
										}
									}
								}

								if (generateCsv)
									csvOutputDataBuffer[index] = keyNode + "," + valueNode.InnerText;

								AppLog.Info($"Key done | target={destLng} index={index + 1}/{max} key='{SafeText(keyNode, 120)}' elapsedMs={itemSw.ElapsedMilliseconds}");
							}
							catch (Exception ex)
							{
								hasErrors = true;
								AppLog.Error($"Unhandled key exception | target={destLng} index={index + 1} key='{SafeText(keyNode, 120)}'", ex);
								try
								{
									string message = "\r\nKey '" + keyNode + "' translation to language '" + destLng + "' failed by exception: " + ex.Message;
									File.AppendAllText(errorLogFile, message);
								}
								catch
								{
								}
							}
						}
					}
					finally
					{
						// now save the data!
						doc.Save(destFile);
						AppLog.Info($"Language file saved | target={destLng} file='{destFile}'");

						if (generateCsv)
						{
							if (!Directory.Exists(generateCsvDir))
								Directory.CreateDirectory(generateCsvDir);
							var csvFile = Path.Combine(generateCsvDir, sourceResxFilename + "." + destLng + ".resx.csv");

							File.WriteAllLines(csvFile, new string[] { "KEY,Value" }, Encoding.UTF8);
							File.AppendAllLines(csvFile, csvOutputDataBuffer, Encoding.UTF8);
							AppLog.Info($"Language CSV saved | target={destLng} file='{csvFile}'");
						}

						AppLog.Info($"Language done | target={destLng} elapsedMs={languageSw.ElapsedMilliseconds}");
					}
				}
			}
			catch (Exception ex)
			{
				hasErrors = true;
				status = "Translation failed by unhandled exception. Check log file.";
				AppLog.Error("TranslateResxFilesAsync unhandled exception.", ex);
			}

			if (hasErrors)
			{
				status = "Translation finished. Errors are logged in to '" + errorLogFilename + "'.";
			}
			else
			{
				status = "Translation finished.";
			}

			AppLog.Info($"TranslateResxFilesAsync done | hasErrors={hasErrors} elapsedMs={totalSw.ElapsedMilliseconds}");

			progress.BeginInvoke(max, pos, status, null, null);

		}

		void ResxWorkingProgress(int max, int pos, string status)
		{
			if ((DateTime.UtcNow - _lastProgressLogUtc).TotalSeconds >= 5 || pos == max)
			{
				_lastProgressLogUtc = DateTime.UtcNow;
				AppLog.Info($"Progress update | max={max} pos={pos} status='{SafeText(status, 220)}'");
			}

			if (this.InvokeRequired)
			{
				this.BeginInvoke(new ResxProgressCallback(ResxWorkingProgress), max, pos, status);
				return;
			}
			else
			{
				barResxProgress.Minimum = 0;
				barResxProgress.Maximum = max;
				barResxProgress.Value = pos;
				lblResxTranslateStatus.Text = $"Processing {max:00}/{pos:00}, " + status;
			}
		}


		void ImportExcel()
		{
			var sheetName = cmbExcelSheets.Text;
			var excelFile = txtExcelFile.Text;
			var resxFile = txtExcelResx.Text;
			var sheetKeyColumn = cmbExcelKey.Text;
			var sheetTranslation = cmbExcelTranslation.Text;
			var create = chkExcelCreateAbsent.Checked;
			AppLog.Info($"ImportExcel start | excel='{excelFile}' resx='{resxFile}' sheet='{sheetName}' keyCol='{sheetKeyColumn}' valueCol='{sheetTranslation}' create={create}");

			IsBusy(true);
			new Action<string, string, string, string, string, bool>(ImportExcel).BeginInvoke(
				excelFile,
				resxFile,
				sheetName,
				sheetKeyColumn,
				sheetTranslation,
				create,
				(x) => IsBusy(false),
				null);
		}

		private void ImportExcel(string excelFile, string resxFile, string sheetName, string sheetKeyColumn,
			string sheetTranslation, bool create)
		{
			var sw = Stopwatch.StartNew();
			var doc = new XmlDocument();
			doc.Load(resxFile);
			var dataList = ResxTranslator.ReadResxData(doc);

			var excelLanguages = ResxExcel.ReadExcelLanguage(excelFile, sheetName, sheetKeyColumn, sheetTranslation);
			foreach (var lngPair in excelLanguages)
			{
				var node = dataList.FirstOrDefault(x => ResxTranslator.GetDataKeyName(x) == lngPair.Key);
				if (node != null)
				{
					ResxTranslator.SetDataValue(doc, node, lngPair.Value);
				}
				else
				{
					ResxTranslator.AddLanguageNode(doc, lngPair.Key, lngPair.Value);
				}
			}
			doc.PreserveWhitespace = false;
			var writer = new XmlTextWriter(resxFile, Encoding.UTF8)
			{
				Formatting = Formatting.Indented
			};
			doc.Save(writer);
			writer.Close();
			AppLog.Info($"ImportExcel done | elapsedMs={sw.ElapsedMilliseconds}");
		}

		bool IsGoogleTranslatorLoaded()
		{
			if (webBrowser.Document != null &&
				(webBrowser.ReadyState == WebBrowserReadyState.Loaded ||
				 webBrowser.ReadyState == WebBrowserReadyState.Complete))
			{
				return true;
			}
			AppLog.Warn("Google translator browser not loaded.");
			return false;
		}

		private void frmMain_Load(object sender, EventArgs e)
		{
			AppLog.Info("frmMain_Load start.");
			FillComboBoxes();
			txtMsTranslationKey.Text = Properties.Settings.Default.MicrosoftTranslatorKey;
			txtMsTranslationRegion.Text = Properties.Settings.Default.MicrosoftTranslatorRegion;
			txtDeepLTranslationKey.Text = Properties.Settings.Default.DeepLTranslatorKey;
			cmbDeeplApiType.SelectedIndex = Properties.Settings.Default.DeepLTranslatorType;
			tabMain.TabPages.Remove(tabBrowser);
			AppLog.Info($"frmMain_Load done. Log file: '{AppLog.CurrentLogFilePath}'");
		}

		private async void btnTranslate_ClickAsync(object sender, EventArgs e)
		{
			AppLog.Info("btnTranslate_ClickAsync triggered.");

			if (cmbDesc.SelectedIndex == -1 || cmbSrc.SelectedIndex == -1)
			{
				AppLog.Warn("Manual translate blocked: source/destination language not selected.");
				MessageBox.Show("Please select source and destination languages correctly.", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			if (txtSrc.Text.Length == 0)
			{
				AppLog.Warn("Manual translate blocked: input text empty.");
				MessageBox.Show("The text body can not be empty.", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			var lngSrc = ((KeyValuePair<string, string>)cmbSrc.SelectedItem).Key;
			var lngDest = ((KeyValuePair<string, string>)cmbDesc.SelectedItem).Key;
			var text = txtSrc.Text;
			AppLog.Info($"Manual translate start | service={ServiceType} from={lngSrc} to={lngDest} chars={text.Length}");

			var translationOptions = new TranslationOptions
			{
				ServiceType = ServiceType,
				MsSubscriptionKey = txtMsTranslationKey.Text,
				MsSubscriptionRegion = txtMsTranslationRegion.Text,
				DeepLSubscriptionKey = txtDeepLTranslationKey.Text,
				DeepLSubscriptionRegion = cmbDeeplApiType.SelectedIndex.ToString()
			};


			IsBusy(true);

			if (ServiceType == ServiceTypeEnum.Google)
			{
				// There is no longer a key to validate
				var textTranslatorUrlKey = "";

				GTranslateService.TranslateAsync(
				text, lngSrc, lngDest, textTranslatorUrlKey,
				(success, result) =>
				{
					AppLog.Info($"Manual google callback | success={success} resultLength={result?.Length ?? 0}");
					SetResult(result);
					IsBusy(false);
				});
			}
			else if (ServiceType == ServiceTypeEnum.Microsoft)
			{
				var translationResult = await MsTranslateService.TranslateAsync(text, lngSrc, lngDest, txtMsTranslationKey.Text, txtMsTranslationRegion.Text);
				AppLog.Info($"Manual MS result | success={translationResult.Success} resultLength={translationResult.Result?.Length ?? 0}");

				if (translationResult.Success)
				{
					SetResult(translationResult.Result);
				}
				else
				{
					if (!string.IsNullOrEmpty(translationResult.Result))
						SetResult(translationResult.Result);
					else
						SetResult("Translation Failed!");
				}

				IsBusy(false);
			}
			else
			{
				var translationResult = await DeepLTranslateService.TranslateAsync(text, lngSrc, lngDest, txtDeepLTranslationKey.Text, cmbDeeplApiType.SelectedIndex.ToString());
				AppLog.Info($"Manual DeepL result | success={translationResult.Success} resultLength={translationResult.Result?.Length ?? 0}");

				if (translationResult.Success)
				{
					SetResult(translationResult.Result);
				}
				else
				{
					if (!string.IsNullOrEmpty(translationResult.Result))
						SetResult(translationResult.Result);
					else
						SetResult("Translation Failed!");
				}

				IsBusy(false);
			}
		}

		private void btnSelectResxSource_Click(object sender, EventArgs e)
		{
			AppLog.Info("btnSelectResxSource_Click triggered.");
			var dlg = new OpenFileDialog();
			dlg.Filter = "ResourceX File|*.resx";
			if (dlg.ShowDialog() == DialogResult.OK)
			{
				txtSourceResx.Text = dlg.FileName;
				AppLog.Info($"Source resx selected: '{txtSourceResx.Text}'");
				if (txtOutputDir.Text.Length == 0)
				{
					txtOutputDir.Text = Path.GetDirectoryName(txtSourceResx.Text);
					txtCSVOutputDir.Text = Path.GetDirectoryName(txtSourceResx.Text);
				}

				// reset selection
				foreach (ListViewItem item in lstResxLanguages.Items)
				{
					item.Checked = false;
				}

				// select based on what is in destination
				string[] languageFilesInDir = Directory.GetFiles(Path.GetDirectoryName(txtSourceResx.Text), "*.resx");
				AppLog.Info($"Detected {languageFilesInDir.Length} resx files in source directory.");

				foreach (var lngFile in languageFilesInDir)
				{
					var languageTag = ReadLanguageName(lngFile);
					if (string.IsNullOrWhiteSpace(languageTag))
						continue;

					var haskey = _languages.FirstOrDefault(x => x.Key.Equals(languageTag, StringComparison.InvariantCultureIgnoreCase));
					if (string.IsNullOrEmpty(haskey.Key))
					{
						AppLog.Warn($"Language tag '{languageTag}' from file '{lngFile}' not found in supported language list.");
						continue;
					}

					int index = lstResxLanguages.Items.IndexOfKey(haskey.Key);
					if (index >= 0)
						lstResxLanguages.Items[index].Checked = true;
					else
						AppLog.Warn($"Language '{haskey.Key}' not found in list view keys.");
				}

				var lng = ReadLanguageName(txtSourceResx.Text);
				var key = _languages.FirstOrDefault(x => string.Compare(x.Key, lng, StringComparison.InvariantCultureIgnoreCase) == 0);
				if (key.Key != null)
					cmbSourceResxLng.SelectedItem = key;
				else
				{
					lng = "en";
					key = _languages.FirstOrDefault(x => string.Compare(x.Key, lng, StringComparison.InvariantCultureIgnoreCase) == 0);
					if (key.Key != null)
						cmbSourceResxLng.SelectedItem = key;
				}
			}
		}

		private void btnSelectOutputDir_Click(object sender, EventArgs e)
		{
			AppLog.Info("btnSelectOutputDir_Click triggered.");
			var dlg = new FolderBrowserDialog();
			dlg.ShowNewFolderButton = true;
			// Remove dialog open directly without selected directory --> dlg.RootFolder = Environment.SpecialFolder.MyComputer;
			if (txtOutputDir.Text.Length > 0)
			{
				dlg.SelectedPath = txtOutputDir.Text;
			}
			if (dlg.ShowDialog() == DialogResult.OK)
			{
				txtOutputDir.Text = dlg.SelectedPath;
				AppLog.Info($"Output directory selected: '{txtOutputDir.Text}'");
			}
		}


		private void btnStartResxTranslate_Click(object sender, EventArgs e)
		{
			AppLog.Info("btnStartResxTranslate_Click triggered.");
			if (!ValidateResxTranslate())
				return;
			if (ServiceType == ServiceTypeEnum.Google && !IsGoogleTranslatorLoaded())
			{
				AppLog.Warn("Resx translate blocked: Google translator not loaded.");
				MessageBox.Show("Google Translator is not loaded.", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			TranslateResxFiles();
		}

		private void lnkAbout_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			using (var frm = new frmAbout())
			{
				frm.ShowDialog();
			}
		}

		private void btnOpenExcel_Click(object sender, EventArgs e)
		{
			AppLog.Info("btnOpenExcel_Click triggered.");
			if (!File.Exists(txtExcelFile.Text))
			{
				AppLog.Warn($"OpenExcel blocked: file not found '{txtExcelFile.Text}'");
				MessageBox.Show("Please select an excel file.", "Excel", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			var excel = ResxExcel.ReadExcel(txtExcelFile.Text);
			if (excel == null)
			{
				AppLog.Warn("OpenExcel failed: excel parser returned null.");
				MessageBox.Show("Failed to read excel file", "Excel", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			cmbExcelSheets.DataSource = excel.SheetNames;
			cmbExcelKey.DataSource = excel.SheetColumnsKey;
			cmbExcelTranslation.DataSource = excel.SheetColumnsTranslation;
			if (Array.IndexOf(excel.SheetColumnsKey, "Name") != -1)
			{
				cmbExcelKey.SelectedIndex = Array.IndexOf(excel.SheetColumnsKey, "Name");
			}
			btnImportExcel.Enabled = true;
			AppLog.Info("Excel loaded successfully and import enabled.");
		}

		private void btnSelectExcel_Click(object sender, EventArgs e)
		{
			AppLog.Info("btnSelectExcel_Click triggered.");
			var dlg = new OpenFileDialog
			{
				Filter = "Excel File|*.xls;xlsx"
			};
			if (dlg.ShowDialog() == DialogResult.OK)
			{
				txtExcelFile.Text = dlg.FileName;
				AppLog.Info($"Excel selected: '{txtExcelFile.Text}'");
			}
		}

		private void btnExcelResx_Click(object sender, EventArgs e)
		{
			AppLog.Info("btnExcelResx_Click triggered.");
			var dlg = new OpenFileDialog
			{
				Filter = "ResourceX File|*.resx"
			};
			if (dlg.ShowDialog() == DialogResult.OK)
			{
				txtExcelResx.Text = dlg.FileName;
				AppLog.Info($"Excel target resx selected: '{txtExcelResx.Text}'");
			}
		}

		private void cmbExcelSheets_SelectedIndexChanged(object sender, EventArgs e)
		{
			AppLog.Info($"cmbExcelSheets_SelectedIndexChanged | selected='{cmbExcelSheets.Text}'");
			if (string.IsNullOrWhiteSpace(txtExcelFile.Text))
				return;
			if (!File.Exists(txtExcelFile.Text))
			{
				AppLog.Warn($"Excel sheet change blocked: file not found '{txtExcelFile.Text}'");
				MessageBox.Show("Please select an excel file.", "Select Resx", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			if (cmbExcelSheets.SelectedIndex != -1)
			{
				var cols = ResxExcel.GetExcelSheetColumns(txtExcelFile.Text, cmbExcelSheets.Text);
				cmbExcelKey.DataSource = cols;
				var cols2 = ResxExcel.GetExcelSheetColumns(txtExcelFile.Text, cmbExcelSheets.Text);
				cmbExcelTranslation.DataSource = cols2;
				if (Array.IndexOf(cols, "Name") != -1)
				{
					cmbExcelKey.Text = "Name";
				}
			}
		}

		private void btnImportExcel_Click(object sender, EventArgs e)
		{
			AppLog.Info("btnImportExcel_Click triggered.");
			if (!File.Exists(txtExcelResx.Text))
			{
				AppLog.Warn($"ImportExcel blocked: resx file not found '{txtExcelResx.Text}'");
				MessageBox.Show("Please select ResX file.", "Select Resx", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			if (!File.Exists(txtExcelFile.Text))
			{
				AppLog.Warn($"ImportExcel blocked: excel file not found '{txtExcelFile.Text}'");
				MessageBox.Show("Please select an excel file.", "Select Resx", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			if (cmbExcelSheets.Items.Count == 0)
			{
				MessageBox.Show("Please open excel file and select key and translation columns.", "Open Excel", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}
			if (cmbExcelKey.SelectedIndex == -1 || cmbExcelSheets.SelectedIndex == -1 || cmbExcelTranslation.SelectedIndex == -1)
			{
				AppLog.Warn("ImportExcel blocked: columns/sheet not selected.");
				MessageBox.Show("Please select excel columns.", "Select Columns", MessageBoxButtons.OK, MessageBoxIcon.Error);
				return;
			}

			ImportExcel();
		}

		private void webBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
		{
			AppLog.Info($"webBrowser_DocumentCompleted: {e.Url}");
			var doc = webBrowser.Document;
			if (doc == null)
				return;
			foreach (HtmlElement imgElemt in doc.Images)
			{
				imgElemt.SetAttribute("src", "");
			}
		}

		private void RbtnMsTranslateService_CheckedChanged(object sender, EventArgs e)
		{
			txtMsTranslationKey.Enabled = rbtnMsTranslateService.Checked;
			txtMsTranslationRegion.Enabled = rbtnMsTranslateService.Checked;
			AppLog.Info($"RbtnMsTranslateService_CheckedChanged: enabled={rbtnMsTranslateService.Checked}");
		}
		private void rbtnDeepLTranslateService_CheckedChanged(object sender, EventArgs e)
		{
			txtDeepLTranslationKey.Enabled = rbtnDeepLTranslateService.Checked;
			cmbDeeplApiType.Enabled = rbtnDeepLTranslateService.Checked;
			AppLog.Info($"rbtnDeepLTranslateService_CheckedChanged: enabled={rbtnDeepLTranslateService.Checked} apiTypeIndex={cmbDeeplApiType.SelectedIndex}");
		}

		private void chkCSVOutput_CheckedChanged(object sender, EventArgs e)
		{
			txtCSVOutputDir.Enabled = btnSelectCSVOutputDir.Enabled = chkCSVOutput.Checked;
			AppLog.Info($"chkCSVOutput_CheckedChanged: checked={chkCSVOutput.Checked}");
		}

		private void btnSelectCSVOutputDir_Click(object sender, EventArgs e)
		{
			AppLog.Info("btnSelectCSVOutputDir_Click triggered.");
			var dlg = new FolderBrowserDialog
			{
				ShowNewFolderButton = true,
				Description = "Please select output directory for CSV files:"
			};
			if (txtCSVOutputDir.Text.Length > 0)
			{
				dlg.SelectedPath = txtCSVOutputDir.Text;
			}
			if (dlg.ShowDialog() == DialogResult.OK)
			{
				txtCSVOutputDir.Text = dlg.SelectedPath;
				AppLog.Info($"CSV output directory selected: '{txtCSVOutputDir.Text}'");
			}
		}

		private void tabMain_Selected(object sender, TabControlEventArgs e)
		{
			var s = (TabControl)sender;
			AppLog.Info($"tabMain_Selected: selectedTab='{s.SelectedTab?.Name}'");
			if (_translateSettingsChanged)
			{
				Properties.Settings.Default.MicrosoftTranslatorKey = txtMsTranslationKey.Text;
				Properties.Settings.Default.MicrosoftTranslatorRegion = txtMsTranslationRegion.Text;
				Properties.Settings.Default.DeepLTranslatorKey = txtDeepLTranslationKey.Text;
				Properties.Settings.Default.DeepLTranslatorType = (short)cmbDeeplApiType.SelectedIndex;
				Properties.Settings.Default.Save();
				AppLog.Info("Translation settings saved.");
				_translateSettingsChanged = false;
			}
			_translateSettingsChanged = s.SelectedTab.Name == "tabTranslateServices";
			AppLog.Info($"_translateSettingsChanged={_translateSettingsChanged}");
		}
	}
}
