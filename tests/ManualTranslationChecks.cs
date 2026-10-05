using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AutoResxTranslator;
using AutoResxTranslator.Definitions;
using Newtonsoft.Json;

internal static class ManualTranslationChecks
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "LabmanChecks-" + Guid.NewGuid().ToString("N"));
    private static readonly string[] TestLanguages = { "en", "it", "ca", "tr" };
    private static string DirectoryPath;
    private static string SourcePath;
    private static int Calls;
    private static readonly List<string> Errors = new List<string>();

    private sealed class ProgressLog : IProgress<string>
    {
        public void Report(string message) { if (message.StartsWith("ERROR")) Errors.Add(message); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Write(string path, Dictionary<string, string> values)
    {
        File.WriteAllText(path, JsonConvert.SerializeObject(values, Formatting.Indented), new UTF8Encoding(false));
    }

    private static string PathFor(string language) { return Path.Combine(DirectoryPath, language + ".json"); }
    private static string HistoryFor(string language) { return Path.Combine(DirectoryPath, ".translation-history", language + ".json"); }
    private static string Translate(string text) { return text.Replace("Hola", "Hello").Replace("Cuenta", "Account").Replace("Pantalla", "Screen").Replace("Texto", "Text"); }

    private static Task<ResultHolder<string>> Success(string text, string language, CancellationToken token)
    {
        Calls++;
        return Task.FromResult(new ResultHolder<string>(true, Translate(text)));
    }

    private static string Run(Func<string, string, CancellationToken, Task<ResultHolder<string>>> translate = null,
        int limit = 6000, CancellationToken token = default(CancellationToken), string[] languages = null)
    {
        Errors.Clear();
        return ManualTranslator.RunAsync(SourcePath, languages ?? TestLanguages, translate ?? Success, limit,
            ManualTranslator.DefaultGlossary, new ProgressLog(), token).GetAwaiter().GetResult();
    }

    private static void IncrementalAndFailures()
    {
        var markdown = "# Hola {#cuenta}\r\n\r\nTexto con **Cuenta** y _Pantalla_.\r\n\r\n"
            + "1. [Cuenta](./usage/login.md \"cuenta\")\r\n2. ![Pantalla](/img/manual/inicio-sesion.png)\r\n\r\n"
            + "| Cuenta | Pantalla |\r\n| :--- | ---: |\r\n| Hola | Texto |\r\n\r\n"
            + ":::note\r\nHola {year}, UBS Labman Integration, HMI, APL, Print Current Selection, PRINT, Log in.\r\n:::\r\n\r\n"
            + "[Cuenta](https://example.test/page_(1)) y [Pantalla][Cuenta].\r\n[Cuenta]: /img/manual/file.png\r\n\r\n"
            + "```text\r\nHola PRINT\r\n```\r\n`Hola HMI`\r\n";
        var source = new Dictionary<string, string>
        {
            { "nav.manual", "Hola ç ı İ ş ğ \"comillas\"" },
            { "docs.usage.login.content", markdown },
            { "translation.pending", "Cuenta" },
            { "date", "2026-10-05T12:00:00Z" }
        };
        Write(SourcePath, source);
        foreach (var language in TestLanguages)
            Write(PathFor(language), new Dictionary<string, string> { { "translation.pending", "Pretranslated" }, { "retired", "Delete me" } });
        var untouchedSource = File.ReadAllBytes(SourcePath);
        Run();
        Check(Calls == 16, "Initial empty/missing/historyless entries must all translate.");
        foreach (var language in TestLanguages)
        {
            var values = ManualTranslator.ReadCatalog(PathFor(language));
            Check(values.Keys.SequenceEqual(source.Keys), "Destination keys/order must follow Spanish.");
            Check(values["nav.manual"] == Translate(source["nav.manual"]), "Unicode/quotes did not round-trip.");
            var expected = Translate(markdown).Replace("```text\r\nHello PRINT", "```text\r\nHola PRINT").Replace("`Hello HMI`", "`Hola HMI`");
            expected = expected.Replace("[Screen][Account]", "[Screen][Cuenta]").Replace("[Account]: /img/manual/file.png", "[Cuenta]: /img/manual/file.png");
            Check(values["docs.usage.login.content"] == expected, "Markdown structure, destinations, glossary or whitespace changed.\n" + values["docs.usage.login.content"]);
            Check(values["date"] == source["date"], "Date-shaped strings must stay strings.");
            Check(ManualTranslator.ReadCatalog(HistoryFor(language))["nav.manual"] == ManualTranslator.SourceHash(source["nav.manual"]), "Wrong source hash.");
        }
        Check(File.ReadAllBytes(SourcePath).SequenceEqual(untouchedSource), "Translator changed es.json.");
        var before = Calls;
        Run();
        Check(Calls == before, "Unchanged second run sent translation requests.");
        source["nav.manual"] += " ";
        Write(SourcePath, source);
        Run();
        Check(Calls == before + 4, "Whitespace-only source change must update all four languages.");
        source.Remove("date");
        source.Add("new", "Hola nuevo");
        Write(SourcePath, source);
        before = Calls;
        Run();
        Check(Calls == before + 4, "New key must translate in every language.");
        foreach (var language in TestLanguages)
            Check(!ManualTranslator.ReadCatalog(HistoryFor(language)).ContainsKey("date"), "Deleted keys survived in history.");

        var previousHash = ManualTranslator.SourceHash(source["nav.manual"]);
        source["nav.manual"] += " cambiado";
        source.Add("new-failure", "Hola fallo");
        Write(SourcePath, source);
        Run((text, language, token) => Task.FromResult(new ResultHolder<string>(false, "simulated failure")));
        Check(Errors.Count == 8, "Errors were not reported for each failed key/language.");
        foreach (var language in TestLanguages)
        {
            var values = ManualTranslator.ReadCatalog(PathFor(language));
            var history = ManualTranslator.ReadCatalog(HistoryFor(language));
            Check(values["nav.manual"] == "" && values["new-failure"] == "", "Failed changed/new values must stay empty.");
            Check(values["new"] == "Hello nuevo", "Unchanged translation was lost on failure.");
            Check(history["nav.manual"] == previousHash && !history.ContainsKey("new-failure"), "A failed translation recorded success.");
        }
        before = Calls;
        Run();
        Check(Calls == before + 8, "Failed values were not retried.");
        before = Calls;
        Run();
        Check(Calls == before, "Successful retry was not remembered.");
        source["nav.manual"] += " cancelado";
        source["new"] += " cancelado";
        Write(SourcePath, source);
        using (var cancellation = new CancellationTokenSource())
        {
            var requests = 0;
            try
            {
                Run((text, language, token) =>
                {
                    if (++requests == 2) cancellation.Cancel();
                    token.ThrowIfCancellationRequested();
                    return Success(text, language, token);
                }, token: cancellation.Token);
                throw new Exception("Cancellation was ignored.");
            }
            catch (OperationCanceledException) { }
        }
        foreach (var language in new[] { "it", "ca", "tr" })
            Check(ManualTranslator.ReadCatalog(PathFor(language))["nav.manual"] == "", "Cancellation left stale translations in later languages.");
        Check(ManualTranslator.ReadCatalog(PathFor("en"))["nav.manual"].Length > 0, "Cancellation lost a completed result.");
        Run();
        Check(!Directory.GetFiles(DirectoryPath, "*.tmp", SearchOption.AllDirectories).Any(), "Atomic writes left temporary files.");
    }

    private static void ValidationAndProtection()
    {
        var sourceBytes = File.ReadAllBytes(SourcePath);
        var targetBytes = File.ReadAllBytes(PathFor("tr"));
        var enBytes = File.ReadAllBytes(PathFor("en"));
        foreach (var invalid in new[] { "[]", "{\"x\":1}", "{\"x\":null}", "{\"x\":{}}", "{\"\":\"a\"}", "{\"x\":\"\"}", "{\"x\":\"a\",\"x\":\"b\"}", "{/*comment*/\"x\":\"a\"}", "{\"x\":\"a\"", "{} {}", "{}", "{\"x\":\"a\",}", "{'x':'a'}", "{x:\"a\"}" })
        {
            File.WriteAllText(SourcePath, invalid);
            try { Run(); throw new Exception("Accepted invalid input: " + invalid); }
            catch (InvalidDataException) { }
            Check(File.ReadAllBytes(PathFor("en")).SequenceEqual(enBytes), "Invalid input caused a partial write.");
        }
        File.WriteAllBytes(SourcePath, sourceBytes);
        File.WriteAllText(PathFor("tr"), "{\"x\":false}");
        try { Run(); throw new Exception("Accepted invalid destination."); } catch (InvalidDataException) { }
        Check(File.ReadAllBytes(PathFor("en")).SequenceEqual(enBytes), "Later invalid destination caused earlier writes.");
        File.WriteAllBytes(PathFor("tr"), targetBytes);
        File.WriteAllBytes(SourcePath, new byte[] { 0x7b, 0x22, 0x78, 0x22, 0x3a, 0x22, 0xff, 0x22, 0x7d });
        try { Run(); throw new Exception("Accepted invalid UTF-8."); } catch (DecoderFallbackException) { }
        File.WriteAllBytes(SourcePath, sourceBytes);
        var source = ManualTranslator.ReadCatalog(SourcePath);
        source.Add("protected-failure", "# Hola\n\n[Cuenta](./login.md) {year}\n:::warning\nTexto\n:::\n");
        Write(SourcePath, source);
        Run((text, language, token) => Task.FromResult(new ResultHolder<string>(true, text.Replace("ZXQLABMAN", "BROKEN"))));
        foreach (var language in TestLanguages)
            Check(ManualTranslator.ReadCatalog(PathFor(language))["protected-failure"] == "", "Corrupted Markdown was saved.");
        Run((text, language, token) => Task.FromResult(new ResultHolder<string>(true, text + "\n```explanation```")));
        Check(Errors.Count == 4, "Unexpected Markdown from provider was accepted.");
        Run();
    }

    private static void Chunking()
    {
        DirectoryPath = Path.Combine(Root, "chunks", "localization");
        Directory.CreateDirectory(DirectoryPath);
        SourcePath = Path.Combine(DirectoryPath, "es.json");
        var text = string.Join("\n\n", Enumerable.Repeat("Hola " + new string('a', 60), 15)) + "\n";
        Write(SourcePath, new Dictionary<string, string> { { "article", text } });
        Calls = 0;
        Run(limit: 400);
        Check(Calls > 4, "Large articles were not split into safe blocks.");
        Check(ManualTranslator.ReadCatalog(PathFor("en"))["article"] == Translate(text), "Split article was not reconstructed exactly.");
        foreach (var indivisible in new[]
        {
            "| Hola | Cuenta |\n| --- | --- |\n" + string.Concat(Enumerable.Repeat("| Hola | " + new string('a', 80) + " |\n", 10)),
            string.Join("\n\n", Enumerable.Repeat("1. Hola " + new string('a', 80), 10)),
            ":::tip\n" + text + ":::\n"
        })
        {
            Write(SourcePath, new Dictionary<string, string> { { "article", indivisible } });
            var requests = 0;
            Run((value, language, token) => { requests++; return Success(value, language, token); }, 400);
            Check(requests == 0 && Errors.Count == 4, "An oversized table/list/admonition was split or accepted.");
            Check(ManualTranslator.ReadCatalog(PathFor("en"))["article"] == "", "Oversized failure saved partial text.");
        }
    }

    private static void GoogleResponses()
    {
        var parse = typeof(GTranslateService).GetMethod("ReadGoogleTranslatedResult", BindingFlags.NonPublic | BindingFlags.Static);
        foreach (var response in new[] { "{\"sentences\":[{\"trans\":\"Hello \"},{\"trans\":\"world\"}]}", "[[[\"Hello \",\"Hola\"],[\"world\",\"mundo\"]]]" })
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(response)))
            {
                var args = new object[] { stream, null };
                Check((bool)parse.Invoke(null, args) && (string)args[1] == "Hello world", "Google response format not supported.");
            }
        }
    }

    private static void SelectedLanguages()
    {
        DirectoryPath = Path.Combine(Root, "selected", "localization");
        Directory.CreateDirectory(DirectoryPath);
        SourcePath = Path.Combine(DirectoryPath, "es.json");
        var source = new Dictionary<string, string> { { "existing", "Hola" } };
        Write(SourcePath, source);
        File.WriteAllText(PathFor("en"), "Unselected catalog must not be read or rewritten.");
        Directory.CreateDirectory(Path.GetDirectoryName(HistoryFor("en")));
        File.WriteAllText(HistoryFor("en"), "Unselected history must not be read or rewritten.");
        var untouchedCatalog = File.ReadAllBytes(PathFor("en"));
        var untouchedHistory = File.ReadAllBytes(HistoryFor("en"));
        var selected = new[] { "fr", "de", "fr" };
        var requested = new List<string>();
        Run((text, language, token) => { requested.Add(language); return Success(text, language, token); }, languages: selected);
        Check(requested.SequenceEqual(new[] { "fr", "de" }), "Requests must use only selected languages, without duplicates.");
        Check(File.Exists(PathFor("de")) && File.Exists(HistoryFor("de")), "Missing selected catalog/history was not created.");
        var before = Calls;
        Run(languages: selected);
        Check(Calls == before, "Selected translations did not reuse their history.");
        source.Add("new-one", "Hola uno");
        source.Add("new-two", "Hola dos");
        Write(SourcePath, source);
        Run(languages: selected);
        Check(Calls == before + 4, "Two new keys must translate only in the two selected languages.");
        foreach (var invalid in new[] { new string[0], new[] { "es" }, new[] { "ES" }, new[] { "../en" }, new[] { "auto" }, new[] { "en.json" }, new[] { "" }, new string[] { null } })
        {
            try { Run(languages: invalid); throw new Exception("Accepted unsafe/empty language selection."); }
            catch (InvalidDataException) { }
        }
        Check(File.ReadAllBytes(PathFor("en")).SequenceEqual(untouchedCatalog), "Unselected catalog was modified.");
        Check(File.ReadAllBytes(HistoryFor("en")).SequenceEqual(untouchedHistory), "Unselected history was modified.");
        Check(!File.Exists(PathFor("it")), "A fixed, unselected language was created.");
        Check(ManualTranslator.ReadCatalog(SourcePath).SequenceEqual(source), "Spanish was modified by language selection.");
    }

    private static void LanguageTable()
    {
        var first = Path.Combine(Root, "detection", "first", "localization");
        var second = Path.Combine(Root, "detection", "second", "localization");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);
        foreach (var name in new[] { "es.json", "en.json", "FR.JSON", "pt-BR.json", "metadata.json" })
            File.WriteAllText(Path.Combine(first, name), "{}");
        File.WriteAllText(Path.Combine(second, "es.json"), "{}");
        File.WriteAllText(Path.Combine(second, "ja.json"), "{}");
        using (var form = new frmMain())
        {
            Check(form.Controls.OfType<TabControl>().Single().TabPages["tabManual"] != null, "The manual tab was not added to the form.");
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var list = (ListView)typeof(frmMain).GetField("_manualLanguages", flags).GetValue(form);
            var allLanguages = (Dictionary<string, string>)typeof(frmMain).GetField("_languages", flags).GetValue(form);
            Check(list.CheckBoxes && list.View == View.Details, "Languages must appear in a table with checkboxes.");
            Check(list.Items.Cast<ListViewItem>().Select(item => item.Name).SequenceEqual(allLanguages.Keys.Where(k => k != "es" && k != "auto")), "The table must reuse all RESX destination languages.");
            var load = typeof(frmMain).GetMethod("LoadManualSource", flags);
            load.Invoke(form, new object[] { Path.Combine(first, "es.json") });
            Check(list.CheckedItems.Count == 3 && list.Items["en"].Checked && list.Items["fr"].Checked && list.Items["pt-BR"].Checked,
                "Existing JSONs (including regional/case variants) were not automatically checked.");
            list.Items["de"].Checked = true;
            load.Invoke(form, new object[] { Path.Combine(second, "es.json") });
            Check(list.CheckedItems.Count == 1 && list.Items["ja"].Checked, "Loading a different folder did not reset selections.");
            File.WriteAllText(Path.Combine(second, "ca.json"), "{}");
            load.Invoke(form, new object[] { Path.Combine(second, "es.json") });
            Check(list.CheckedItems.Count == 2 && list.Items["ca"].Checked, "Reloading the same source did not discover new catalogs.");
        }
    }

    [STAThread]
    private static int Main()
    {
        try
        {
            DirectoryPath = Path.Combine(Root, "localization");
            Directory.CreateDirectory(DirectoryPath);
            SourcePath = Path.Combine(DirectoryPath, "es.json");
            Check(ManualTranslator.SourceHash("á\n") == "f09fd4166be5b2bfdc2375508d168401d78a9fe665a6da33d17efc6cdd40196a", "Source hash did not use exact UTF-8 bytes.");
            IncrementalAndFailures();
            ValidationAndProtection();
            Chunking();
            GoogleResponses();
            SelectedLanguages();
            LanguageTable();
            Console.WriteLine("Passed: incremental translation, failure/retry, cancellation, JSON/UTF-8 validation, Markdown/glossary, safe chunking, Google formats, selected languages and automatic JSON detection.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
}
