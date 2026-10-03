# Deployment Helper · Sunucu Dağıtım Yönetimi

**C# ve Windows Forms ile geliştirilen staj projesi.** Seçilen dosyaları birden fazla sunucunun paylaşım klasörüne aktarır; dağıtım öncesinde yedek oluşturur ve önceki dosyalara dönüş imkânı sunar.

> English: A Windows Forms deployment helper with PostgreSQL-backed server management, ping checks, file distribution, backup rotation and rollback.

## Neler yapar?

- PostgreSQL'de sunucu adı, IP ve paylaşım yolu kaydeder; ekleme, güncelleme ve silme sunar.
- Sunucuların erişilebilirliğini ping ile kontrol eder.
- Seçilen dosyaları işaretlenen çevrimiçi sunucuların `current` klasörüne kopyalar.
- Mevcut dosyaların zaman damgalı yedeğini alır; en fazla 5 yedek saklar.
- Dağıtım hatasında veya kullanıcı isteğiyle rollback işlemi uygular.
- İlerleme yüzdesi, işlem özeti ve geçmiş günlüklerini gösterir.

## Teknolojiler ve yapı

**C# · .NET 8 · Windows Forms · PostgreSQL · Npgsql**

| Dosya | Sorumluluğu |
| --- | --- |
| `WinFormsApp1/MainForm.cs` | Sunucu listesi, ping, dağıtım akışı ve günlük arayüzü |
| `WinFormsApp1/AddServerForm.cs` | Parametreli SQL ile sunucu kayıt işlemleri |
| `WinFormsApp1/DeployManager.cs` | Dosya kopyalama, yedekleme ve geri dönüş |
| `WinFormsApp1/Logger.cs` | Dosyaya günlük yazma |
| `database/schema.sql` | Örnek veritabanı tablosu |

## Kurulum

Windows, .NET 8 veya daha yeni SDK ve PostgreSQL gerekir.

1. PostgreSQL'de `ServerList` adında boş bir veritabanı oluşturun.
2. Bu veritabanında [database/schema.sql](database/schema.sql) dosyasını çalıştırın.
3. PowerShell'de kendi yerel bağlantı bilgilerinizi tanımlayın:

```powershell
$env:DEPLOYMENT_DB_CONNECTION='Host=localhost;Port=5432;Database=ServerList;Username=postgres;Password=YEREL_SIFRENIZ'
dotnet run --project WinFormsApp1/DeploymentHelper.csproj
```

Alternatif olarak `WinFormsApp1.sln` dosyasını Visual Studio'da açın. Ortam değişkeni yoksa `App.config` içindeki `PostgresDb` kullanılır; `CHANGE_ME` değeri yerel parolanızla değiştirilmelidir.

## Demo senaryosu

1. Deneme için ayrı bir paylaşım klasörü hazırlayın ve Windows hesabınıza yazma yetkisi verin.
2. Uygulamaya bu paylaşım için bir sunucu kaydı ekleyin.
3. Örnek bir metin dosyası seçip çevrimiçi sunucuya dağıtın.
4. Aynı dosyayı değiştirip yeniden dağıtın; `backup` altında önceki dosyayı inceleyin.
5. **Rollback** ile önceki dosya kümesine dönün.

```text
paylasim/
├── current/                   # Dağıtılan dosyalar
└── backup/YYYY-MM-DD_HH-mm-ss/ # Önceki dosyalar
```

## Kapsam

Bu araç ağ paylaşımına dosya kopyalar; servis yeniden başlatma, SSH ve paket sürümü yönetimi içermez. Yedekleme yalnızca `current` klasöründeki üst düzey dosyaları kapsar. Ping sonucu paylaşım yetkisini garanti etmez. Rollback `current` içeriğini yedekle değiştirir; demo için ayrı bir test paylaşımı kullanın.

**Geliştirici:** [Beraterd](https://github.com/Beraterd)

`dotnet build --configuration Release` ile derlendi (0 hata, 0 uyarı). PostgreSQL ve gerçek ağ paylaşımıyla dağıtım kontrolü yapılmadı.
