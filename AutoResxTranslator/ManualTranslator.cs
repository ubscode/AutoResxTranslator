using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AutoResxTranslator.Definitions;

namespace AutoResxTranslator
{
    public static class ManualTranslator
    {
        public const string DefaultGlossary = "Labman\r\nUBS Labman Integration\r\nHMI\r\nAPL\r\nPrint Current Selection\r\nPRINT\r\nLog in";

        public static string SourceHash(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }

        public static Dictionary<string, string> ReadCatalog(string path, bool source = false)
        {
            string json;
            using (var input = new StreamReader(path, new UTF8Encoding(true, true), false)) json = input.ReadToEnd();
            // Json.NET also accepts comments, single quotes and trailing commas; the catalog contract does not.
            const string jsonString = @"""(?:[^""\\\x00-\x1f]|\\(?:[""\\/bfnrt]|u[0-9a-fA-F]{4}))*""";
            var pair = jsonString + @"[ \t\r\n]*:[ \t\r\n]*" + jsonString;
            if (!Regex.IsMatch(json, @"\A[ \t\r\n]*\{[ \t\r\n]*(?:" + pair + @"(?:[ \t\r\n]*,[ \t\r\n]*" + pair + @")*)?[ \t\r\n]*\}[ \t\r\n]*\z", RegexOptions.None, TimeSpan.FromSeconds(5)))
                throw new InvalidDataException(path + ": se requiere JSON válido con claves y valores de tipo cadena.");
            using (var reader = new JsonTextReader(new StringReader(json)))
            {
                reader.DateParseHandling = DateParseHandling.None;
                var result = new Dictionary<string, string>(StringComparer.Ordinal);
                if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                    throw new InvalidDataException(path + ": se requiere un objeto JSON plano.");
                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType != JsonToken.PropertyName)
                        throw new InvalidDataException(path + ": clave JSON no válida.");
                    var key = (string)reader.Value;
                    if (string.IsNullOrWhiteSpace(key) || result.ContainsKey(key))
                        throw new InvalidDataException(path + ": clave vacía o duplicada: " + key);
                    if (!reader.Read() || reader.TokenType != JsonToken.String)
                        throw new InvalidDataException(path + ": el valor de '" + key + "' debe ser una cadena.");
                    var value = (string)reader.Value;
                    if (source && string.IsNullOrWhiteSpace(value))
                        throw new InvalidDataException(path + ": texto español vacío: " + key);
                    result.Add(key, value);
                }
                if (reader.TokenType != JsonToken.EndObject || reader.Read())
                    throw new InvalidDataException(path + ": JSON incompleto o contenido adicional.");
                if (source && result.Count == 0)
                    throw new InvalidDataException(path + ": el catálogo español está vacío.");
                return result;
            }
        }

        private static void SaveCatalog(string path, Dictionary<string, string> values)
        {
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                // Write and flush on the same volume before atomically replacing the destination.
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true))
                    {
                        writer.Write(JsonConvert.SerializeObject(values, Formatting.Indented));
                        writer.Write("\n");
                    }
                    stream.Flush(true);
                }
                if (File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public static async Task<string> RunAsync(string sourcePath, IEnumerable<string> destinationLanguages,
            Func<string, string, CancellationToken, Task<ResultHolder<string>>> translate,
            int maxRequestSize, string glossary, IProgress<string> progress, CancellationToken cancellationToken)
        {
            sourcePath = Path.GetFullPath(sourcePath);
            var directory = Path.GetDirectoryName(sourcePath);
            if (!string.Equals(Path.GetFileName(sourcePath), "es.json", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetFileName(directory), "localization", StringComparison.OrdinalIgnoreCase)
                || directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(p => new[] { ".generated", "build", ".docusaurus" }.Contains(p, StringComparer.OrdinalIgnoreCase)))
                throw new InvalidDataException("Selecciona localization/es.json del repositorio del manual.");
            if (maxRequestSize < 256) throw new ArgumentOutOfRangeException("maxRequestSize");
            if (destinationLanguages == null) throw new ArgumentNullException("destinationLanguages");
            var languages = destinationLanguages.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (languages.Length == 0 || languages.Any(language => string.IsNullOrWhiteSpace(language)
                || !Regex.IsMatch(language, @"\A[a-zA-Z]{2,3}(?:-[a-zA-Z0-9]{2,8})*\z")
                || string.Equals(language, "es", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Selecciona al menos un idioma de destino válido, distinto del español.");
            cancellationToken.ThrowIfCancellationRequested();
            using (var runLock = new FileStream(Path.Combine(directory, ".labman-translation.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
                var source = ReadCatalog(sourcePath, true);
                var hashes = source.ToDictionary(p => p.Key, p => SourceHash(p.Value), StringComparer.Ordinal);
                var historyDirectory = Path.Combine(directory, ".translation-history");
                var destinations = new Dictionary<string, Dictionary<string, string>>();
                var histories = new Dictionary<string, Dictionary<string, string>>();
                // Validate every selected input before changing any catalog.
                foreach (var language in languages)
                {
                    var destinationPath = Path.Combine(directory, language + ".json");
                    var historyPath = Path.Combine(historyDirectory, language + ".json");
                    var destination = File.Exists(destinationPath) ? ReadCatalog(destinationPath) : new Dictionary<string, string>();
                    var history = File.Exists(historyPath) ? ReadCatalog(historyPath) : new Dictionary<string, string>();
                    if (history.Values.Any(v => !Regex.IsMatch(v, "^[a-f0-9]{64}$")))
                        throw new InvalidDataException(historyPath + ": hash SHA-256 no válido.");
                    destinations[language] = source.ToDictionary(p => p.Key,
                        p => destination.ContainsKey(p.Key) ? destination[p.Key] : "", StringComparer.Ordinal);
                    histories[language] = source.Where(p => history.ContainsKey(p.Key))
                        .ToDictionary(p => p.Key, p => history[p.Key], StringComparer.Ordinal);
                }
                Directory.CreateDirectory(historyDirectory);
                var pending = 0;
                foreach (var language in languages)
                {
                    foreach (var key in source.Keys)
                    {
                        string previousHash;
                        if (string.IsNullOrWhiteSpace(destinations[language][key])
                            || !histories[language].TryGetValue(key, out previousHash) || previousHash != hashes[key])
                        {
                            destinations[language][key] = "";
                            pending++;
                        }
                    }
                    // Invalidate each selected stale language before requests, also on cancellation.
                    SaveCatalog(Path.Combine(directory, language + ".json"), destinations[language]);
                    SaveCatalog(Path.Combine(historyDirectory, language + ".json"), histories[language]);
                }
                progress?.Report(pending + " traducciones pendientes. Historial: localization/.translation-history/");
                var completed = 0;
                var failed = 0;
                var terms = (glossary ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim()).Where(t => t.Length > 0).Distinct().OrderByDescending(t => t.Length).ToArray();
                foreach (var language in languages)
                {
                    foreach (var entry in source)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (destinations[language][entry.Key].Length != 0) continue;
                        progress?.Report($"[{completed + failed + 1}/{pending}] {language} · {entry.Key}");
                        string translated;
                        try
                        {
                            translated = await TranslateValueAsync(entry.Value, language, translate,
                                maxRequestSize, terms, cancellationToken).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                        catch (Exception ex)
                        {
                            failed++;
                            AppLog.Error($"Manual Labman | {language} | {entry.Key}", ex);
                            progress?.Report($"ERROR {language} · {entry.Key}: {ex.Message}");
                            continue;
                        }
                        // Catalog first, confirmed source hash second. A crash between them safely retries.
                        destinations[language][entry.Key] = translated;
                        SaveCatalog(Path.Combine(directory, language + ".json"), destinations[language]);
                        histories[language][entry.Key] = hashes[entry.Key];
                        SaveCatalog(Path.Combine(historyDirectory, language + ".json"), histories[language]);
                        completed++;
                    }
                }
                return $"Finalizado: {completed} traducidas, {failed} pendientes por error, {source.Count * languages.Length - pending} conservadas.";
            }
        }

        private static async Task<string> TranslateValueAsync(string source, string language,
            Func<string, string, CancellationToken, Task<ResultHolder<string>>> translate,
            int limit, string[] terms, CancellationToken cancellationToken)
        {
            var whole = new ProtectedText(source, terms);
            var pieces = RequestSize(whole.Text) <= limit ? new[] { source } : SafeBlocks(source).ToArray();
            var output = new StringBuilder();
            // ponytail: use complete Markdown blocks; oversized indivisible blocks stay pending.
            // Add a Markdown parser/provider with larger limits if such blocks occur in the manual.
            var chunk = "";
            foreach (var piece in pieces)
            {
                if (chunk.Length > 0 && RequestSize(new ProtectedText(chunk + piece, terms).Text) > limit)
                {
                    output.Append(await TranslateChunkAsync(chunk, language, translate, limit, terms, cancellationToken).ConfigureAwait(false));
                    chunk = "";
                }
                chunk += piece;
            }
            output.Append(await TranslateChunkAsync(chunk, language, translate, limit, terms, cancellationToken).ConfigureAwait(false));
            return output.ToString();
        }

        private static int RequestSize(string value)
        {
            // Conservative for the existing Google GET transport and UTF-8 POST providers.
            return System.Web.HttpUtility.UrlEncode(value, Encoding.UTF8).Length;
        }

        private static async Task<string> TranslateChunkAsync(string source, string language,
            Func<string, string, CancellationToken, Task<ResultHolder<string>>> translate,
            int limit, string[] terms, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var protectedText = new ProtectedText(source, terms);
            if (!Regex.IsMatch(Regex.Replace(protectedText.Text, "ZXQLABMAN[0-9]+QXZ", ""), @"[\p{L}\p{N}]"))
                return source;
            if (RequestSize(protectedText.Text) > limit)
                throw new InvalidDataException("Una sección, tabla o lista supera el límite del proveedor. Usa un proveedor con mayor límite o reduce esa sección.");
            var result = await translate(protectedText.Text, language, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (result == null || !result.Success || string.IsNullOrWhiteSpace(result.Result))
                throw new InvalidDataException(result?.Result ?? "El proveedor no devolvió una traducción.");
            return protectedText.Restore(result.Result);
        }

        private static IEnumerable<string> SafeBlocks(string source)
        {
            var lines = Regex.Matches(source, @"[^\r\n]*(?:\r\n|\r|\n|$)").Cast<Match>().Where(m => m.Length > 0).Select(m => m.Value).ToArray();
            var block = new StringBuilder();
            char fence = '\0';
            var fenceLength = 0;
            var admonitions = 0;
            var list = false;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();
                if (fence == '\0' && admonitions == 0 && Regex.IsMatch(line, @"^#{1,6}\s") && block.Length > 0)
                {
                    yield return block.ToString();
                    block.Clear();
                    list = false;
                }
                block.Append(line);
                var fenceMatch = Regex.Match(line, @"^\s*(`{3,}|~{3,})");
                if (fenceMatch.Success)
                {
                    if (fence == '\0') { fence = fenceMatch.Value.Trim()[0]; fenceLength = fenceMatch.Value.Trim().Length; }
                    else if (trimmed[0] == fence && trimmed.Length >= fenceLength && trimmed.All(c => c == fence)) fence = '\0';
                }
                else if (fence == '\0')
                {
                    if (trimmed == ":::") admonitions = Math.Max(0, admonitions - 1);
                    else if (trimmed.StartsWith(":::")) admonitions++;
                    if (Regex.IsMatch(line, @"^\s*(?:[-+*]|\d+[.)])\s")) list = true;
                }
                if (trimmed.Length == 0 && fence == '\0' && admonitions == 0)
                {
                    var next = i + 1;
                    while (next < lines.Length && lines[next].Trim().Length == 0) next++;
                    if (list && next < lines.Length && Regex.IsMatch(lines[next], @"^(?:[ \t]+\S|\s*(?:[-+*]|\d+[.)])\s)")) continue;
                    yield return block.ToString();
                    block.Clear();
                    list = false;
                }
            }
            if (block.Length > 0) yield return block.ToString();
        }

        private sealed class ProtectedText
        {
            private readonly List<string> originals = new List<string>();
            public string Text { get; private set; }

            public ProtectedText(string source, string[] terms)
            {
                if (source.Contains("ZXQLABMAN"))
                    throw new InvalidDataException("El texto contiene un marcador reservado ZXQLABMAN.");
                var patterns = new List<string>
                {
                    @"^[ \t]*(?<fence>`{3,}|~{3,})[^\r\n]*\r?\n[\s\S]*?^[ \t]*\k<fence>[`~]*[ \t]*(?=\r?$)",
                    @"`+[^`\r\n]+`+",
                    @"(?<=\]\()(?:[^()\r\n]|\((?<depth>)|\)(?<-depth>))*(?(depth)(?!))(?=\))",
                    @"\]\[[^\]\r\n]*\]",
                    @"^[ \t]{0,3}\[[^\]\r\n]+\]:[^\r\n]*",
                    @"<[^>\r\n]+>",
                    @"https?://[^\s<>]+",
                    @"\{\{[^}\r\n]+\}\}|\{[^{}\r\n]+\}",
                    @"^[ \t]*\|?[ \t]*:?-{3,}:?[ \t]*(?:\|[ \t]*:?-{3,}:?[ \t]*)*\|?[ \t]*(?=\r?$)",
                    @"^[ \t]*(?:#{1,6}[ \t]+|(?:[-+*]|\d+[.)])[ \t]+|>+[ \t]*|:::(?:[a-z]+\b|(?=[ \t]*\r?$)))",
                    @"^[ \t]*(?:[-*_][ \t]*){3,}(?=\r?$)",
                    @"^[ \t]*[=-]+[ \t]*(?=\r?$)",
                    @"\r\n|\r|\n",
                    @"[\[\]()*_!|~`\\]+",
                    @"\A[ \t]+|[ \t]+\z"
                };
                if (terms.Length > 0) patterns.Add(@"(?<![\p{L}\p{N}])(?:" + string.Join("|", terms.Select(Regex.Escape)) + @")(?![\p{L}\p{N}])");
                Text = Regex.Replace(source, @"(?:[ \t]*(?:" + string.Join("|", patterns) + @")[ \t]*)+", m =>
                {
                    var token = "ZXQLABMAN" + originals.Count.ToString("D6") + "QXZ";
                    originals.Add(m.Value);
                    return " " + token + " ";
                }, RegexOptions.Multiline);
            }

            public string Restore(string translated)
            {
                translated = translated.Trim();
                var tokens = Regex.Matches(translated, @"ZXQLABMAN[0-9]+QXZ").Cast<Match>().Select(m => m.Value).ToArray();
                if (tokens.Length != originals.Count || tokens.Where((t, i) => t != "ZXQLABMAN" + i.ToString("D6") + "QXZ").Any()
                    || Regex.IsMatch(Regex.Replace(translated, @"ZXQLABMAN[0-9]+QXZ", ""), @"[\r\n\[\]{}<>*_~|`\\]|^\s*(?:#|:::)", RegexOptions.Multiline))
                    throw new InvalidDataException("El proveedor alteró el formato, un enlace o un marcador protegido.");
                var index = 0;
                var restored = Regex.Replace(translated, @"[ \t]*ZXQLABMAN[0-9]+QXZ[ \t]*", m =>
                {
                    var original = originals[index++];
                    // Keep a separator if the provider places a word after a protected product/control name.
                    if (original.Length > 0 && char.IsLetterOrDigit(original[original.Length - 1])
                        && m.Index + m.Length < translated.Length
                        && char.IsLetterOrDigit(translated[m.Index + m.Length])) original += " ";
                    return original;
                });
                if (string.IsNullOrWhiteSpace(restored)) throw new InvalidDataException("Traducción vacía.");
                return restored;
            }
        }
    }
}
