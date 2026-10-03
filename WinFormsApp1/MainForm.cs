using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Forms;
using Npgsql;
using System.Configuration;


namespace DeploymentHelper
{
    public partial class MainForm : Form
    {
        // UI
        private ListView serverList = null!;
        private ImageList statusImages = null!;
        private TextBox txtFiles = null!;
        private Panel progressContainer = null!;
        private Panel progressFill = null!; private RichTextBox activeLogBox = null!;
        private RichTextBox historyLogBox = null!;

        // Core
        private readonly DeployManager deployManager = new();
        private readonly Logger logger = new("logs.txt");

        // Data
        private readonly List<ServerItem> servers = new();
        private List<string> selectedFiles = new();

        private readonly string connectionString;
        private Label lblProgressPercent = null!;




        public MainForm()
        {
            connectionString =
                Environment.GetEnvironmentVariable("DEPLOYMENT_DB_CONNECTION") ?? ConfigurationManager
                    .ConnectionStrings["PostgresDb"]
                    ?.ConnectionString
                ?? throw new Exception("Connection string bulunamadı");

            InitializeUI();
            LoadServers();
            _ = PingAllServersAsync();
        }


        private void InitializeUI()
        {
            Text = "Deployment Helper";
            Size = new Size(1050, 720);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            // ================= LEFT PANEL =================
            var leftPanel = new Panel
            {
                Location = new Point(15, 15),
                Size = new Size(420, 360),
                BackColor = Color.White
            };
            Controls.Add(leftPanel);

            leftPanel.Controls.Add(new Label
            {
                Text = "Sunucular",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(10, 10)
            });

            statusImages = new ImageList { ImageSize = new Size(16, 16) };
            statusImages.Images.Add("red", CreateStatusBitmap(Color.Red));
            statusImages.Images.Add("green", CreateStatusBitmap(Color.Green));

            serverList = new ListView
            {
                Location = new Point(10, 35),
                Size = new Size(400, 170),
                View = View.List,
                CheckBoxes = true,
                SmallImageList = statusImages
            };
            leftPanel.Controls.Add(serverList);

            leftPanel.Controls.Add(CreateSideButton("Server Ekle", Color.Green, 215, BtnAddServer));
            leftPanel.Controls.Add(CreateSideButton("Server Güncelle", Color.Orange, 250, BtnUpdateServer));
            leftPanel.Controls.Add(CreateSideButton("Server Sil", Color.DarkRed, 285, BtnDeleteServer));

            // 🔴 ROLLBACK BUTONU (GRİ)
            leftPanel.Controls.Add(CreateSideButton(
                "Rollback (Geri Al)",
                Color.Gray,
                320,
                BtnRollback));

            // ================= RIGHT PANEL =================
            var rightPanel = new Panel
            {
                Location = new Point(455, 15),
                Size = new Size(560, 360),
                BackColor = Color.White
            };
            Controls.Add(rightPanel);

            rightPanel.Controls.Add(new Label
            {
                Text = "Dosya",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(10, 10)
            });

            txtFiles = new TextBox
            {
                Location = new Point(10, 40),
                Size = new Size(420, 25),
                ReadOnly = true
            };
            rightPanel.Controls.Add(txtFiles);

            var btnSelectFiles = new Button
            {
                Text = "Dosya Seç",
                Location = new Point(440, 40),
                Size = new Size(100, 25)
            };
            btnSelectFiles.Click += SelectFiles;
            rightPanel.Controls.Add(btnSelectFiles);

            var btnCopy = new Button
            {
                Text = "Deploy",
                Location = new Point(10, 80),
                Size = new Size(530, 45),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnCopy.FlatAppearance.BorderSize = 0;
            btnCopy.Click += async (_, _) => await StartDeployAsync();
            rightPanel.Controls.Add(btnCopy);

            rightPanel.Controls.Add(new Label
            {
                Location = new Point(10, 135),
                ForeColor = Color.Gray,
                AutoSize = true
            });

            // ================= CUSTOM PROGRESS BAR =================
            progressContainer = new Panel
            {
                Location = new Point(15, 390),
                Size = new Size(1000, 24),
                BackColor = Color.LightGray,
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(progressContainer);

            progressFill = new Panel
            {
                Dock = DockStyle.Left,          // 🔥 KRİTİK
                Width = 0,                      // başlangıçta boş
                BackColor = Color.Green
            };
            progressContainer.Controls.Add(progressFill);

            lblProgressPercent = new Label
            {
                Dock = DockStyle.Fill,          // container’ın içinde
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Text = "%0"
            };
            progressContainer.Controls.Add(lblProgressPercent);
            lblProgressPercent.BringToFront();








            // ================= LOG TABS =================
            InitializeLogs();
        }



        private void BtnRollback(object? sender, EventArgs e)
        {
            var selected = serverList.CheckedItems
                .Cast<ListViewItem>()
                .Select(i => (ServerItem)i.Tag!)
                .ToList();

            if (!selected.Any())
            {
                MessageBox.Show("Rollback için sunucu seçmelisin.");
                return;
            }

            foreach (var s in selected)
            {
                deployManager.Rollback(s.SharePath, Log);
            }

            Log("↩ Manuel rollback tamamlandı");
        }

        private static Bitmap CreateStatusBitmap(Color color)
        {
            var bmp = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bmp);
            using var b = new SolidBrush(color);
            g.FillEllipse(b, 2, 2, 12, 12);
            return bmp;
        }

        // ================= DB =================
        private void LoadServers()
        {
            servers.Clear();
            serverList.Items.Clear();

            using var conn = new NpgsqlConnection(connectionString);
            conn.Open();

            using var cmd = new NpgsqlCommand(
                "SELECT id, name, ip, sharepath FROM servers ORDER BY id", conn);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                servers.Add(new ServerItem
                {
                    Id = r.GetInt32(0),
                    Name = r.GetString(1),
                    Ip = r.GetString(2),
                    SharePath = r.GetString(3),
                    IsOnline = false
                });
            }

            RefreshServerList();
        }

        private void RefreshServerList()
        {
            serverList.Items.Clear();
            foreach (var s in servers)
            {
                var item = new ListViewItem($"{s.Name} ({s.SharePath})")
                {
                    ImageKey = s.IsOnline ? "green" : "red",
                    Tag = s
                };
                serverList.Items.Add(item);
            }
        }

        // ================= PING =================
        private async Task PingAllServersAsync()
        {
            // 🔒 Listeyi kopyala (thread-safe)
            var serverSnapshot = servers.ToList();

            await Task.Run(() =>
            {
                foreach (var s in serverSnapshot)
                {
                    try
                    {
                        using var ping = new Ping();
                        s.IsOnline = ping.Send(s.Ip, 700).Status == IPStatus.Success;
                    }
                    catch
                    {
                        s.IsOnline = false;
                    }
                }
            });

            // UI thread
            if (!IsDisposed)
                Invoke(new Action(RefreshServerList));
        }


        // ================= DEPLOY =================
        private async Task StartDeployAsync()
        {
            var selectedServers = serverList.CheckedItems
                .Cast<ListViewItem>()
                .Select(i => (ServerItem)i.Tag!)
                .Where(s => s.IsOnline)
                .ToList();

            if (!selectedFiles.Any() || !selectedServers.Any())
            {
                MessageBox.Show("Dosya ve çevrimiçi sunucu seçmelisin.");
                return;
            }

            int totalOperations = selectedServers.Count * selectedFiles.Count;
            int completedOperations = 0;

            int successCount = 0;
            int failCount = 0;

            // 🔵 PROGRESS BAŞLANGIÇ
            Invoke(new Action(() =>
            {
                progressFill.Width = 0;
                lblProgressPercent.Text = "%0";
            }));

            foreach (var s in selectedServers)
            {
                bool serverFailed = false;

                Log($"🚀 Deploy başlatıldı: {s.Name}");

                await deployManager.DeployAsync(
                    new[] { (s.Name, s.SharePath) },
                    selectedFiles,
                    msg =>
                    {
                        Log(msg);

                        if (msg.Contains("kopyalandı"))
                        {
                            Invoke(new Action(() =>
                            {
                                completedOperations++;

                                int percent = Math.Min(
                                    100,
                                    (int)((completedOperations * 100.0) / totalOperations)
                                );

                                int newWidth = (int)(
                                    progressContainer.Width * (percent / 100.0)
                                );

                                int fillWidth = (int)(progressContainer.Width * (percent / 100.0));
                                progressFill.Width = fillWidth;
                                lblProgressPercent.Text = $"%{percent}";
                            }));
                        }

                        if (msg.Contains("❌"))
                        {
                            serverFailed = true;
                        }
                    });

                if (serverFailed)
                    failCount++;
                else
                    successCount++;
            }

            // 🔵 %100 GARANTİ
            Invoke(new Action(() =>
            {
                progressFill.Width = progressContainer.Width;
                lblProgressPercent.Text = "%100";
            }));

            // 📊 ÖZET POPUP
            MessageBox.Show(
                $"Deploy Özeti\n\n" +
                $"Başarılı Sunucu: {successCount}\n" +
                $"Başarısız Sunucu: {failCount}\n" +
                $"Toplam Dosya: {selectedFiles.Count}\n" +
                $"Toplam Sunucu: {selectedServers.Count}",
                "Deploy Tamamlandı",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            Log("✅ Tüm işlemler tamamlandı.");
        }




        // ================= CRUD =================
        private void BtnAddServer(object? s, EventArgs e)
        {
            using var f = new AddServerForm();
            if (f.ShowDialog() == DialogResult.OK)
            {
                LoadServers();
                _ = PingAllServersAsync();
            }
        }

        private void BtnUpdateServer(object? s, EventArgs e)
        {
            if (serverList.CheckedItems.Count != 1)
            {
                MessageBox.Show("Sadece 1 sunucu seç.");
                return;
            }

            var sv = (ServerItem)serverList.CheckedItems[0].Tag!;
            using var f = new AddServerForm(sv.Id, sv.Name, sv.Ip, sv.SharePath);
            if (f.ShowDialog() == DialogResult.OK)
            {
                LoadServers();
                _ = PingAllServersAsync();
            }
        }

        private void BtnDeleteServer(object? sender, EventArgs e)
        {
            if (serverList.CheckedItems.Count == 0)
            {
                MessageBox.Show("En az 1 sunucu seçmelisin.");
                return;
            }

            if (MessageBox.Show(
                $"{serverList.CheckedItems.Count} sunucu silinsin mi?",
                "Onay",
                MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;

            using var conn = new NpgsqlConnection(connectionString);
            conn.Open();

            using var tran = conn.BeginTransaction();

            try
            {
                foreach (ListViewItem item in serverList.CheckedItems)
                {
                    var sv = (ServerItem)item.Tag!;

                    using var cmd = new NpgsqlCommand(
                        "DELETE FROM servers WHERE id=@id",
                        conn, tran);

                    cmd.Parameters.AddWithValue("@id", sv.Id);
                    cmd.ExecuteNonQuery();
                }

                tran.Commit();
            }
            catch
            {
                tran.Rollback();
                MessageBox.Show("Silme sırasında hata oluştu.");
                return;
            }

            LoadServers();
            _ = PingAllServersAsync();
        }


        // ================= HELPERS =================
        private void SelectFiles(object? s, EventArgs e)
        {
            using var ofd = new OpenFileDialog { Multiselect = true };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                selectedFiles = ofd.FileNames.ToList();
                txtFiles.Text = string.Join(" ; ",
                    selectedFiles.Select(Path.GetFileName));
            }
        }
        private Button CreateSideButton(string text, Color color, int y, EventHandler handler)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(10, y),
                Size = new Size(400, 30),
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };

            btn.FlatAppearance.BorderSize = 0;
            btn.Click += handler;
            return btn;
        }


        private void InitializeLogs()
        {
            var tabs = new TabControl
            {
                Location = new Point(15, 420),
                Size = new Size(1000, 260)  
            };

            activeLogBox = CreateLogBox();
            historyLogBox = CreateLogBox();

            var tabActive = new TabPage("Aktif Loglar");
            tabActive.Controls.Add(activeLogBox);

            var tabHistory = new TabPage("Geçmiş Loglar");
            tabHistory.Controls.Add(historyLogBox);

            tabs.TabPages.Add(tabActive);
            tabs.TabPages.Add(tabHistory);

            // 🔴 BURASI ÖNEMLİ
            tabs.SelectedIndexChanged += (_, _) =>
            {
                if (tabs.SelectedTab == tabHistory)
                {
                    LoadHistoryLogs();
                }
            };

            Controls.Add(tabs);
        }

        private void LoadHistoryLogs()
        {
            try
            {
                if (File.Exists("logs.txt"))
                {
                    historyLogBox.Text = File.ReadAllText("logs.txt");
                }
                else
                {
                    historyLogBox.Text = "Henüz log yok.";
                }
            }
            catch (Exception ex)
            {
                historyLogBox.Text = "Loglar okunamadı: " + ex.Message;
            }
        }

        private void Log(string msg)
        {
            var line = $"{DateTime.Now:HH:mm:ss} - {msg}";

            if (activeLogBox.InvokeRequired)
            {
                activeLogBox.Invoke(new Action(() =>
                {
                    activeLogBox.AppendText(line + Environment.NewLine);
                }));
            }
            else
            {
                activeLogBox.AppendText(line + Environment.NewLine);
            }

            logger.Log(msg);
        }


        private static RichTextBox CreateLogBox()
            => new() { Dock = DockStyle.Fill, ReadOnly = true, Font = new Font("Consolas", 9) };
    }

    public class ServerItem
    {
        public int Id;
        public string Name = "";
        public string Ip = "";
        public string SharePath = "";
        public bool IsOnline;
    }
}
