using System;
using System.Drawing;
using System.Windows.Forms;
using Npgsql;

namespace DeploymentHelper
{
    public class AddServerForm : Form
    {
        private TextBox txtName = null!;
        private TextBox txtIp = null!;
        private TextBox txtShare = null!;

        private readonly bool _isEditMode;
        private readonly int _serverId;

        private static string ConnectionString =>
            Environment.GetEnvironmentVariable("DEPLOYMENT_DB_CONNECTION")
            ?? System.Configuration.ConfigurationManager.ConnectionStrings["PostgresDb"]?.ConnectionString
            ?? throw new InvalidOperationException("PostgresDb bağlantısı yapılandırılmalı.");

        // ===== EKLE MODU =====
        public AddServerForm()
        {
            _isEditMode = false;
            InitializeUI();
        }

        // ===== GÜNCELLE MODU =====
        public AddServerForm(int id, string name, string ip, string sharePath)
        {
            _isEditMode = true;
            _serverId = id;

            InitializeUI();

            txtName.Text = name;
            txtIp.Text = ip;
            txtShare.Text = sharePath.Replace($@"\\{ip}\\", "");
        }

        private void InitializeUI()
        {
            Text = _isEditMode ? "Server Güncelle" : "Server Ekle";
            Size = new Size(420, 300);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            Controls.Add(CreateLabel("Server Adı", 20));
            txtName = CreateTextBox(45);
            Controls.Add(txtName);

            Controls.Add(CreateLabel("Server IP", 85));
            txtIp = CreateTextBox(110);
            Controls.Add(txtIp);

            Controls.Add(CreateLabel("Paylaşım Klasörü", 150));
            txtShare = CreateTextBox(175);
            Controls.Add(txtShare);

            var btnSave = new Button
            {
                Text = _isEditMode ? "Güncelle" : "Kaydet",
                Location = new Point(40, 220),
                Size = new Size(140, 30),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += SaveServer;
            Controls.Add(btnSave);

            var btnCancel = new Button
            {
                Text = "İptal",
                Location = new Point(220, 220),
                Size = new Size(140, 30)
            };
            btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;
            Controls.Add(btnCancel);
        }

        private void SaveServer(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text) ||
                string.IsNullOrWhiteSpace(txtIp.Text) ||
                string.IsNullOrWhiteSpace(txtShare.Text))
            {
                MessageBox.Show("Tüm alanları doldurmalısın.");
                return;
            }

            string sharePath = $@"\\{txtIp.Text.Trim()}\\{txtShare.Text.Trim()}";

            try
            {
                using var conn = new NpgsqlConnection(ConnectionString);
                conn.Open();

                NpgsqlCommand cmd;

                if (_isEditMode)
                {
                    cmd = new NpgsqlCommand(
                        "UPDATE servers SET name=@n, ip=@i, sharepath=@s WHERE id=@id",
                        conn);
                    cmd.Parameters.AddWithValue("@id", _serverId);
                }
                else
                {
                    cmd = new NpgsqlCommand(
                        "INSERT INTO servers (name, ip, sharepath) VALUES (@n, @i, @s)",
                        conn);
                }

                cmd.Parameters.AddWithValue("@n", txtName.Text.Trim());
                cmd.Parameters.AddWithValue("@i", txtIp.Text.Trim());
                cmd.Parameters.AddWithValue("@s", sharePath);

                cmd.ExecuteNonQuery();
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show("İşlem başarısız:\n" + ex.Message);
            }
        }

        private static Label CreateLabel(string text, int top)
            => new Label
            {
                Text = text,
                Location = new Point(20, top),
                AutoSize = true
            };

        private static TextBox CreateTextBox(int top)
            => new TextBox
            {
                Location = new Point(20, top),
                Size = new Size(360, 25)
            };
    }
}
