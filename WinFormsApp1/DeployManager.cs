using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DeploymentHelper
{
    public class DeployManager
    {
        private const int MAX_BACKUP_COUNT = 5;

        public async Task DeployAsync(
        IEnumerable<(string Name, string SharePath)> servers,
        IEnumerable<string> files,
        Action<string> log)
        {
            foreach (var server in servers)
            {
                await Task.Run(() =>
                    DeploySingle(server, files, log));
            }
        }

        private void DeploySingle(
            (string Name, string SharePath) server,
            IEnumerable<string> files,
            Action<string> log)
        {
            string root = server.SharePath;
            string current = Path.Combine(root, "current");
            string backupRoot = Path.Combine(root, "backup");

            try
            {
                // 1️⃣ Klasör yapısını garanti et
                Directory.CreateDirectory(root);
                Directory.CreateDirectory(current);
                Directory.CreateDirectory(backupRoot);

                // 2️⃣ DEPLOY ÖNCESİ BACKUP (snapshot)
                string backupPath = CreateBackup(current, backupRoot, log);

                // 3️⃣ DOSYALARI CURRENT ÜZERİNE EKLE / GÜNCELLE
                foreach (var file in files)
                {
                    if (!File.Exists(file))
                    {
                        log($"❌ {server.Name}: Kaynak dosya bulunamadı");
                        continue;
                    }

                    string dest = Path.Combine(current, Path.GetFileName(file));
                    File.Copy(file, dest, true); // overwrite = true

                    log($"✔ {server.Name}: {Path.GetFileName(file)} kopyalandı");
                }

                log($"✔ {server.Name}: Deploy başarılı");
            }
            catch (Exception ex)
            {
                log($"❌ {server.Name}: Hata oluştu → rollback");
                Rollback(root, log);
                log(ex.Message);
            }
        }


        // ================= BACKUP =================
        private string CreateBackup(string current, string backupRoot, Action<string> log)
        {
            if (!Directory.Exists(current) ||
                !Directory.EnumerateFileSystemEntries(current).Any())
            {
                log("ℹ Backup alınmadı: current boş");
                return string.Empty;
            }

            Directory.CreateDirectory(backupRoot);

            // 🔢 BACKUP LİMİTİ (MAX 5)
            const int maxBackupCount = 5;

            var existingBackups = Directory
                .GetDirectories(backupRoot)
                .OrderBy(d => d)
                .ToList();

            // ❌ FAZLA BACKUP VARSA EN ESKİYİ SİL
            while (existingBackups.Count >= maxBackupCount)
            {
                string oldest = existingBackups.First();
                Directory.Delete(oldest, true);
                log($"🗑 Eski backup silindi: {Path.GetFileName(oldest)}");
                existingBackups.RemoveAt(0);
            }

            // 📦 YENİ BACKUP OLUŞTUR
            string backupPath = Path.Combine(
                backupRoot,
                DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));

            Directory.CreateDirectory(backupPath);

            foreach (var file in Directory.GetFiles(current))
            {
                File.Copy(
                    file,
                    Path.Combine(backupPath, Path.GetFileName(file)),
                    true);
            }

            log($"📦 Backup alındı: {Path.GetFileName(backupPath)}");
            return backupPath;
        }



        private void CleanupOldBackups(string backupRoot)
        {
            if (!Directory.Exists(backupRoot))
                return;

            var oldBackups = Directory
                .GetDirectories(backupRoot)
                .OrderByDescending(d => d)
                .Skip(MAX_BACKUP_COUNT);

            foreach (var dir in oldBackups)
            {
                Directory.Delete(dir, true);
            }
        }

        // ================= ROLLBACK =================
        public void Rollback(string root, Action<string> log)
        {
            string current = Path.Combine(root, "current");
            string backupRoot = Path.Combine(root, "backup");

            if (!Directory.Exists(backupRoot))
            {
                log("↩ Rollback yok: backup klasörü bulunamadı");
                return;
            }

            var lastBackup = Directory
                .GetDirectories(backupRoot)
                .OrderByDescending(d => d)
                .FirstOrDefault();

            if (lastBackup == null)
            {
                log("↩ Rollback yok: backup bulunamadı");
                return;
            }

            try
            {
                if (Directory.Exists(current))
                    Directory.Delete(current, true);

                Directory.Move(lastBackup, current);

                log($"↩ Rollback tamamlandı. Aktif sürüm: {Path.GetFileName(lastBackup)}");
            }
            catch (Exception ex)
            {
                log("❌ Rollback başarısız: " + ex.Message);
            }
        }


        // ================= HELPERS =================
        private void EnsureDeployStructure(string root)
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "current"));
            Directory.CreateDirectory(Path.Combine(root, "temp"));
            Directory.CreateDirectory(Path.Combine(root, "backup"));
        }
    }
}
