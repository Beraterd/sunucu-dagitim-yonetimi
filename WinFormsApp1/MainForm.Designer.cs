using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeploymentHelper
{
    public partial class MainForm : Form
    {
        private readonly ConfigReader _configReader;
        private readonly DeployManager _deployManager;
        private readonly Logger _logger;
        private List<ServerInfo> _servers;
        private CheckedListBox _serverListBox;

        public MainForm()
        {
            InitializeComponent();
            _configReader = new ConfigReader();
            _deployManager = new DeployManager();
            _logger = new Logger("logs.txt");

            LoadServers();
        }

        private void InitializeComponent()
        {
            this.Text = "Deployment Helper - SMB Çoklu Kopyalama";
            this.Width = 900;
            this.Height = 600;

            var tabControl = new TabControl() { Dock = DockStyle.Fill };
            var tabDeploy = new TabPage("Kopyalama");
            var tabLogs = new TabPage("Geçmiş / Loglar");
            tabControl.TabPages.Add(tabDeploy);
            tabControl.TabPages.Add(tabLogs);

            // --- Kopyalama Sekmesi ---
            var lblServers = new Label() { Text = "Sunucular:", Left = 20, Top = 20 };
            _serverListBox = new CheckedListBox() { Left = 20, Top = 45, Width = 300, Height = 150 };

            var btnSelectFiles = new Button() { Text = "Dosya Seç", Left = 350, Top = 45 };
            var txtFiles = new TextBox() { Left = 350, Top = 80, Width = 400 };
            var btnSelectFolder = new Button() { Text = "Hedef Klasör Seç", Left = 350, Top = 120 };
            var txtTarget = new TextBox() { Left = 350, Top = 155, Width = 400 };
            var btnDeploy = new Button() { Text = "Kopyala", Left = 350, Top = 200, Width = 150 };

            var progressBox = new ListBox() { Left = 20, Top = 240, Width = 730, Height = 250 };

            btnSelectFiles.Click += (s, e) =>
            {
                using var ofd = new OpenFileDialog() { Multiselect = true };
                if (ofd.ShowDialog() == DialogResult.OK)
                    txtFiles.Text = string.Join(";", ofd.FileNames);
            };

            btnSelectFolder.Click += (s, e) =>
            {
                using var fbd = new FolderBrowserDialog();
                if (fbd.ShowDialog() == DialogResult.OK)
                    txtTarget.Text = fbd.SelectedPath;
            };

            btnDeploy.Click += async (s, e) =>
            {
                var selectedServers = _serverListBox.CheckedItems.Cast<ServerInfo>().ToList();
                var files = txtFiles.Text.Split(';', StringSplitOptions.RemoveEmptyEntries);
                var target = txtTarget.Text;

                progressBox.Items.Clear();
                if (!files.Any() || !selectedServers.Any())
                {
                    MessageBox.Show("Lütfen sunucu ve dosya seçiniz.");
                    return;
                }

                progressBox.Items.Add("Kopyalama başlatılıyor...");

                await _deployManager.DeployAsync(selectedServers, files, target, (msg) =>
                {
                    Invoke(new Action(() => progressBox.Items.Add(msg)));
                    _logger.Log(msg);
                });

                progressBox.Items.Add("Tüm işlemler tamamlandı!");
            };

            tabDeploy.Controls.AddRange(new Control[]
            {
                lblServers, _serverListBox, btnSelectFiles, txtFiles,
                btnSelectFolder, txtTarget, btnDeploy, progressBox
            });

            // --- Log Sekmesi ---
            var txtLog = new TextBox()
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                ReadOnly = true
            };
            tabLogs.Controls.Add(txtLog);

            tabLogs.Enter += (s, e) =>
            {
                if (File.Exists("logs.txt"))
                    txtLog.Text = File.ReadAllText("logs.txt");
                else
                    txtLog.Text = "Henüz log kaydı yok.";
            };

            this.Controls.Add(tabControl);
        }

        private void LoadServers()
        {
            _servers = _configReader.LoadServers();
            _serverListBox.Items.Clear();
            foreach (var server in _servers)
                _serverListBox.Items.Add(server);
        }
    }
}
