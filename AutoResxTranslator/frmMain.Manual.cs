using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AutoResxTranslator.Definitions;

namespace AutoResxTranslator
{
    public partial class frmMain
    {
        private TextBox _manualSource;
        private TextBox _manualGlossary;
        private ListView _manualLanguages;
        private TextBox _manualLog;
        private Label _manualService;
        private Button _manualBrowse;
        private Button _manualStart;
        private Button _manualCancel;
        private CancellationTokenSource _manualCancellation;
        private bool _closeAfterManualCancellation;

        private void InitializeManualTab()
        {
            var tab = new TabPage("Manual Labman") { Name = "tabManual", Padding = new Padding(10) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            var files = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            files.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            files.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            _manualSource = new TextBox { Name = "txtManualSource", AccessibleName = "Fuente española", ReadOnly = true, Dock = DockStyle.Fill };
            _manualBrowse = new Button { Text = "Seleccionar…", Dock = DockStyle.Fill };
            _manualBrowse.Click += (sender, args) =>
            {
                using (var dialog = new OpenFileDialog { Filter = "Fuente española (es.json)|es.json", Title = "Seleccionar localization/es.json" })
                {
                    if (dialog.ShowDialog(this) == DialogResult.OK) LoadManualSource(dialog.FileName);
                }
            };
            files.Controls.Add(_manualSource, 0, 0);
            files.Controls.Add(_manualBrowse, 1, 0);
            layout.Controls.Add(new Label { Text = "Fuente española", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            layout.Controls.Add(files, 1, 0);
            var help = new Label { Text = "Genera los JSON de los idiomas marcados junto a es.json.\nSolo actualiza textos pendientes o cuyo español haya cambiado.", Dock = DockStyle.Fill };
            layout.Controls.Add(help, 0, 1);
            layout.SetColumnSpan(help, 2);
            layout.Controls.Add(new Label { Text = "Idiomas destino", Dock = DockStyle.Fill }, 0, 2);
            var languagesAndGlossary = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = new Padding(0) };
            languagesAndGlossary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            languagesAndGlossary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            languagesAndGlossary.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            languagesAndGlossary.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _manualLanguages = new ListView { Name = "lstManualLanguages", AccessibleName = "Idiomas de destino",
                CheckBoxes = true, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, Dock = DockStyle.Fill };
            _manualLanguages.Columns.Add("Código", 65);
            _manualLanguages.Columns.Add("Idioma", 160);
            foreach (var language in _languages.Where(p => p.Key != "auto" && p.Key != "es"))
            {
                var item = new ListViewItem(language.Key) { Name = language.Key };
                item.SubItems.Add(language.Value);
                _manualLanguages.Items.Add(item);
            }
            languagesAndGlossary.Controls.Add(_manualLanguages, 0, 0);
            languagesAndGlossary.SetRowSpan(_manualLanguages, 2);
            languagesAndGlossary.Controls.Add(new Label { Text = "Conservar términos (uno por línea)", Dock = DockStyle.Fill }, 1, 0);
            _manualGlossary = new TextBox { AccessibleName = "Términos que se conservarán", Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Text = ManualTranslator.DefaultGlossary };
            languagesAndGlossary.Controls.Add(_manualGlossary, 1, 1);
            layout.Controls.Add(languagesAndGlossary, 1, 2);
            _manualService = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            layout.Controls.Add(_manualService, 0, 3);
            layout.SetColumnSpan(_manualService, 2);
            _manualLog = new TextBox { Name = "txtManualLog", AccessibleName = "Progreso y errores de traducción", Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill,
                Text = "Selecciona la fuente y configura el proveedor en Translate Service.\r\nLos errores quedan pendientes para el siguiente intento." };
            layout.Controls.Add(_manualLog, 0, 4);
            layout.SetColumnSpan(_manualLog, 2);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            _manualStart = new Button { Text = "Traducir manual", Width = 135, Height = 28 };
            _manualCancel = new Button { Text = "Cancelar", Width = 95, Height = 28, Enabled = false };
            _manualStart.Click += TranslateManual_Click;
            _manualCancel.Click += (sender, args) => _manualCancellation?.Cancel();
            actions.Controls.Add(_manualStart);
            actions.Controls.Add(_manualCancel);
            layout.Controls.Add(actions, 0, 5);
            layout.SetColumnSpan(actions, 2);
            tab.Controls.Add(layout);
            tabMain.TabPages.Add(tab);
            tabMain.Selected += (sender, args) =>
            {
                if (args.TabPage == tab) _manualService.Text = "Proveedor: " + ServiceType + " · Fuente: español";
            };
        }

        private void LoadManualSource(string sourcePath)
        {
            _manualSource.Text = sourcePath;
            var directory = Path.GetDirectoryName(sourcePath);
            foreach (ListViewItem item in _manualLanguages.Items)
                item.Checked = File.Exists(Path.Combine(directory, item.Name + ".json"));
        }

        private async void TranslateManual_Click(object sender, EventArgs args)
        {
            if (_resxCancellation != null || _manualCancellation != null) return;
            if (!File.Exists(_manualSource.Text))
            {
                MessageBox.Show(this, "Selecciona localization/es.json del manual.", "Manual Labman", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var languages = _manualLanguages.CheckedItems.Cast<ListViewItem>().Select(item => item.Name).ToArray();
            if (languages.Length == 0)
            {
                MessageBox.Show(this, "Marca al menos un idioma de destino.", "Manual Labman", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var options = new TranslationOptions
            {
                ServiceType = ServiceType,
                MsSubscriptionKey = txtMsTranslationKey.Text,
                MsSubscriptionRegion = txtMsTranslationRegion.Text,
                DeepLSubscriptionKey = txtDeepLTranslationKey.Text,
                DeepLSubscriptionRegion = cmbDeeplApiType.SelectedIndex.ToString()
            };
            if ((options.ServiceType == ServiceTypeEnum.Microsoft && string.IsNullOrWhiteSpace(options.MsSubscriptionKey))
                || (options.ServiceType == ServiceTypeEnum.DeepL && string.IsNullOrWhiteSpace(options.DeepLSubscriptionKey)))
            {
                MessageBox.Show(this, "Configura la clave del proveedor en Translate Service.", "Manual Labman", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var sourcePath = _manualSource.Text;
            var glossary = _manualGlossary.Text;
            var cancellation = new CancellationTokenSource();
            _manualCancellation = cancellation;
            _manualStart.Enabled = _manualBrowse.Enabled = _manualGlossary.Enabled = _manualLanguages.Enabled = false;
            _manualCancel.Enabled = true;
            foreach (TabPage page in tabMain.TabPages) if (page.Name != "tabManual") page.Enabled = false;
            _manualLog.Clear();
            var progress = new Progress<string>(message =>
            {
                if (_manualCancellation == cancellation) _manualLog.AppendText(message + "\r\n");
            });
            try
            {
                var summary = await Task.Run(() => ManualTranslator.RunAsync(sourcePath, languages,
                    (text, language, token) => TranslateManualTextAsync(text, language, options, token),
                    options.ServiceType == ServiceTypeEnum.Google ? 6000 : 60000, glossary, progress, cancellation.Token));
                _manualLog.AppendText(summary + "\r\n");
                _manualLog.AppendText("En el repositorio del manual ejecuta translations:generate, translations:check, typecheck y build.\r\nPara una entrega completa ejecuta también translations:strict.\r\n");
            }
            catch (OperationCanceledException)
            {
                _manualLog.AppendText("Cancelado. Se han guardado los resultados completados; el resto queda pendiente.\r\n");
            }
            catch (Exception ex)
            {
                AppLog.Error("Manual Labman", ex);
                _manualLog.AppendText("ERROR: " + ex.Message + "\r\n");
            }
            finally
            {
                _manualCancellation = null;
                cancellation.Dispose();
                _manualStart.Enabled = _manualBrowse.Enabled = _manualGlossary.Enabled = _manualLanguages.Enabled = true;
                _manualCancel.Enabled = false;
                foreach (TabPage page in tabMain.TabPages) page.Enabled = true;
                if (_closeAfterManualCancellation) Close();
            }
        }

        private static async Task<ResultHolder<string>> TranslateManualTextAsync(string text, string language,
            TranslationOptions options, CancellationToken token)
        {
            if (options.ServiceType == ServiceTypeEnum.Microsoft)
                return await MsTranslateService.TranslateAsync(text, "es", language, options.MsSubscriptionKey, options.MsSubscriptionRegion, token).ConfigureAwait(false);
            if (options.ServiceType == ServiceTypeEnum.DeepL)
                return await DeepLTranslateService.TranslateAsync(text, "es", language, options.DeepLSubscriptionKey, options.DeepLSubscriptionRegion, token).ConfigureAwait(false);
            string result;
            var success = GTranslateService.Translate(text, "es", language, "", out result, token);
            token.ThrowIfCancellationRequested();
            return new ResultHolder<string>(success, result);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_manualCancellation != null)
            {
                _closeAfterManualCancellation = true;
                _manualCancellation.Cancel();
                e.Cancel = true;
            }
            base.OnFormClosing(e);
        }
    }
}
