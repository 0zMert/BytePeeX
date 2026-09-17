# Folderize 🚀
**Modern, Hızlı ve Akıllı Disk & Klasör Boyut Analiz Aracı**

Folderize, Windows bilgisayarınızdaki depolama alanını saniyeler içinde tarayan, hangi dosya ve klasörlerin ne kadar yer kapladığını modern ve interaktif grafiklerle görselleştiren açık kaynaklı bir masaüstü uygulamasıdır.

---

## ⚡ Sistem Gereksinimleri & Kurulum

Folderize ultra hafif boyutta (**yalnızca ~1.2 MB**) çalışacak şekilde optimize edilmiştir. Uygulamayı çalıştırabilmek için bilgisayarınızda **.NET 9 Masaüstü Çalışma Zamanı (Desktop Runtime)** bulunmalıdır.

| Gereksinim | Detay |
| :--- | :--- |
| **İşletim Sistemi** | Windows 10 / Windows 11 (64-bit) |
| **Gerekli Platform** | **.NET 9.0 Desktop Runtime (x64)** |
| **Doğrudan İndirme Linki** | ⬇️ **[Microsoft .NET 9 Desktop Runtime x64 İndir (Resmi Link)](https://aka.ms/dotnet/9.0/windowsdesktop-runtime-win-x64.exe)** |
| **Alternatif İndirme Sayfası** | 🔗 **[Microsoft .NET 9 İndirme Portalı](https://dotnet.microsoft.com/download/dotnet/9.0)** |

> 💡 *Not: Bilgisayarınızda .NET 9 zaten kuruluysa herhangi bir ek yükleme yapmadan doğrudan çalıştırabilirsiniz.*

---

## 📦 Hızlı Başlangıç

1. [Releases](https://github.com/0zMert/Foldarize/releases) sayfasından en güncel paketi indirin:
   - **`Folderize-v0.4-win-x64-Standalone.zip`**: Bilgisayarınızda .NET 9 yüklü olmasa bile doğrudan çalışır (Önerilen).
   - **`Folderize-v0.4-win-x64.zip`**: Sisteminde .NET 9 Desktop Runtime kurulu olanlar için ultra hafif sürüm.
2. Arşivi istediğiniz bir klasöre çıkartın.
3. **`Folderize.exe`** dosyasına çift tıklayarak uygulamayı başlatın.

---

## 🛡️ Windows SmartScreen Uyarısı Alırsanız

Uygulama açık kaynaklı bir topluluk projesidir. Yüksek maliyetli ticari imzalama sertifikaları kullanılmadığı için Windows ilk çalıştırmada mavi ekranda *"Windows kişisel bilgisayarınızı korudu"* (SmartScreen) uyarısı gösterebilir.

Uygulamayı başlatmak için:
- **Yöntem 1 (Mavi ekrandan):** Mavi penceredeki **"Ek bilgi"** *(More info)* linkine tıklayın, ardından sağ altta açılan **"Yine de çalıştır"** *(Run anyway)* butonuna basın.
- **Yöntem 2 (Dosya Özelliklerinden):** İndirdiğiniz `.zip` veya `.exe` dosyasına **sağ tıklayın ➔ Özellikler (Properties) ➔ En alttaki "Engellemeyi Kaldır" (Unblock) kutucuğunu işaretleyip Tamam** deyin.

> 🔒 *Folderize tamamen açık kaynaklıdır ve güvenlidir. Tüm kaynak kodları, kullanılan algoritmalar ve bileşenler bu GitHub deposu üzerinden şeffaf şekilde incelenebilir.*

---

## ✨ Temel Özellikler

- 🏎️ **Işık Hızında Disk Tarama**: Asenkron çoklu iş parçacıklı motoru ve Win32 FindFirstFileEx entegrasyonuyla yüzbinlerce dosyayı saniyeler içinde analiz eder.
- 🛡️ **Gelişmiş Çekirdek & Yedekleme Semantiği (SeBackupPrivilege)**: Win32 `FILE_FLAG_BACKUP_SEMANTICS` çekirdek seviyesi erişim sayesinde standart kullanıcıların erişemediği korumalı sistem klasörlerini (`System Volume Information`, gölge kopyalar vb.) eksiksiz tarar.
- 🗑️ **Güvenli Silme & Geri Dönüşüm Kutusu Entegrasyonu**: Ağaç tablosunda seçili tekli veya çoklu dosya/klasörleri sağ tık menüsü veya <kbd>Del</kbd> tuşuyla onay alarak doğrudan Windows Geri Dönüşüm Kutusu'na (Recycle Bin) taşır. Canlı ağaç boyutlarını, ebeveynleri ve grafikleri anında günceller.
- ⏱️ **5 Saniyede Bir Canlı Sürücü Takibi**: Sol alttaki yerel sürücü kartları arka planda her 5 saniyede bir milisaniyelik Win32 API ile gerçek boş/dolu alan durumunu kesintisiz olarak günceller.
- 📊 **3 Farklı İnteraktif Görselleştirme**:
  - **Treemap (Ağaç Haritası)**: Klasör ve dosyaların diskteki ağırlığını alan büyüklükleriyle gösterir.
  - **Sunburst (Güneş Işını / Katmanlı Halka)**: Klasör hiyerarşisini katman katman dairesel olarak keşfetmenizi sağlar.
  - **Donut Grafiği**: Dosya uzantılarına (.mp4, .zip, .dll vb.) göre disk kullanım dağılımını gösterir.
- 📂 **Fluent Dosya Gezgini**: Ağaç görünümü üzerinde akıllı seviye genişletme (katman katman veya tümünü açma), Explorer'da açma, yolu kopyalama ve dosya türüne göre hızlı filtreleme.
- 💾 **Sürücü & Sistem Görünümü**: Tüm yerel sürücülerin doluluk oranları, büyük dosyalar listesi ve kurulu uygulamalar analizi.
- 🎨 **Modern Windows 11 Teması**: Fluent Design ilkelerine uygun şık koyu tema (Dark Mode), yüksek kontrastlı seçim satırları ve akıcı animasyonlar.

---

## 🛠️ Geliştirici & Derleme (Build)

Projeyi kaynak koddan derlemek için:

```powershell
# Depoyu klonlayın
git clone https://github.com/0zMert/Foldarize.git
cd Foldarize

# Çözümü derleyin
dotnet build Folderize.sln

# Testleri çalıştırın
dotnet test Folderize.sln

# Hafif tek dosya olarak yayınlayın (.NET 9 yüklü sistemler için ~1.2 MB)
dotnet publish Folderize.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/framework-dependent

# Bağımsız tek dosya olarak yayınlayın (.NET 9 gerektirmeyen bağımsız paket)
dotnet publish Folderize.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish/standalone
```

---

## 📄 Lisans
Bu proje [MIT Lisansı](LICENSE) kapsamında geliştirilmektedir.
