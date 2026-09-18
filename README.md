# BytePeeX 🚀
**Modern, Ultra Hızlı ve Akıllı Disk & Klasör Boyut Analiz Aracı**

[![Version](https://img.shields.io/badge/version-0.4.0-blue.svg)](https://github.com/0zMert/BytePeeX/releases)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011%20(x64)-0078d7.svg)](https://github.com/0zMert/BytePeeX)
[![Framework](https://img.shields.io/badge/.NET-9.0%20WPF-512bd4.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Language](https://img.shields.io/badge/language-TR%20%7C%20EN-orange.svg)](#-çoklu-dil-desteği-tr--en)

BytePeeX, Windows işletim sisteminde depolama alanınızı saniyeler içinde analiz eden, hangi dosya ve dizinlerin ne kadar yer kapladığını interaktif modern grafiklerle görselleştiren ve gereksiz alan israfını anında tespit etmenizi sağlayan yüksek performanslı bir masaüstü aracıdır.

---

## ✨ Öne Çıkan Özellikler

### 🏎️ Yüksek Hızlı Tarama Motoru
- **Doğrudan Win32 Entegrasyonu**: Yüksek seviyeli dosya API'lerinin getirdiği gecikmeleri ortadan kaldıran düşük seviyeli `FindFirstFileExW` / `FindNextFileW` mimarisiyle yüzbinlerce dosyayı saniyeler içinde tarar.
- **Asenkron & Çok İş Parçacıklı**: Kullanıcı arayüzünü kilitlemeyen tamamen asenkron arka plan işleme altyapısı.
- **Anlık İlerleme Takibi**: Taranan dosya/klasör adetleri, geçen süre ve anlık hız gerçek zamanlı olarak durum çubuğunda gösterilir.

### 🛡️ Gelişmiş Güvenlik ve İzin Mimarisi
- **Varsayılan Standart Kullanıcı Modu**: Uygulama açılışta gereksiz UAC (Kullanıcı Hesabı Denetimi) uyarısı vermeden standart kullanıcı haklarıyla başlar.
- **Tek Tıkla Yönetici Modu**: Korunan sistem alanlarını taramak istediğinizde, araç çubuğundaki **"🛡️ Yönetici Moduna Geç"** butonuyla uygulamayı tek tıkla yetkilendirerek yeniden başlatabilirsiniz.
- **SeBackupPrivilege & Yedekleme Semantiği**: Windows çekirdek seviyesi `FILE_FLAG_BACKUP_SEMANTICS` desteği sayesinde `System Volume Information`, gölge kopyalar ve kilitli sistem dizinleri erişim engeline takılmadan taranabilir.

### 📊 Zengin Görselleştirme ve Analiz Modülleri
- **Treemap (Blok Harita)**: Klasör ve dosyaların diskteki ağırlığını orantılı dikdörtgen bloklar halinde gösterir. 
  - *Dinamik (Dengeli) Ölçekleme*: Çok büyük klasörlerin tüm haritayı kaplamasını önleyerek küçük ve orta boy dizinlerin rahatça okunmasını sağlar.
  - *Sistem Klasörü Filtresi*: Dilerseniz `Windows`, `$Recycle.Bin` gibi sistem bloklarını haritadan gizleyebilirsiniz.
- **Sunburst (Güneş Grafiği)**: Katman katman dairesel halkalarla klasör hiyerarşisinde derinlemesine görsel gezinti sunar.
- **Donut Grafiği**: Diskteki alanın dosya uzantılarına (`.mp4`, `.zip`, `.dll`, `.iso` vb.) göre yüzdesel dağılımını kategorize eder.
- **Depolama Isı Haritası (Storage Heatmap)**: Boyutlarına göre dinamik renklendirilen kartlar (>10GB Kırmızı, 1-10GB Turuncu, >250MB Sarı, <250MB Mavi) üzerinden hızlı keşif ve inceleme olanağı sağlar.
- **Fluent Dosya Gezgini (Fluent Explorer)**: Mevcut dizinin kapasite dağılım çubuğu, tıklanabilir breadcrumb (içerik haritası) ve modern veri tablosu.
- **En Büyük & Büyük Dosyalar Listesi**: Diskin en büyük alan tüketen dosyalarını tek listede sıralar.
- **Yüklü Uygulamalar Analizörü**: Windows Kayıt Defteri'ni (Registry) tarayarak sistemde kurulu 32-bit ve 64-bit uygulamaların kapladığı alanları, yayımcılarını ve kurulum tarihlerini raporlar.

### 🗑️ Güvenli Dosya Yönetimi
- **Geri Dönüşüm Kutusu Entegrasyonu**: Listeden seçilen dosya ve klasörler sağ tık menüsü veya <kbd>Del</kbd> tuşuyla onay alınarak doğrudan Windows Geri Dönüşüm Kutusu'na taşınır.
- **Canlı Boyut Güncellemesi**: Silinen öğelerin boyutları anında üst dizinlerden ve grafiklerden düşülür; tüm diski yeniden taramaya gerek kalmaz.
- **Hızlı Erişim**: Dosya Gezgini'nde açma ve tam dosya yolunu panoya kopyalama kısayolları.

### 🧭 Ergonomik Navigasyon ve Kontroller
- **Akıllı Ağaç Hiyerarşisi**: Seviye seviye açıp kapatma (`+1 Seviye Genişlet`, `-1 Seviye Daralt`, `Tümünü Genişlet/Daralt`).
- **Anlık Filtreleme & Arama**: Taranan on binlerce öğe arasında anında arama yapabilen arama çubuğu.
- **Canlı Sürücü Takibi**: Yerel sürücülerin doluluk durumları her 5 saniyede bir arka planda kontrol edilerek sol panelde güncel tutulur.
- **Özel Dizin Seçici**: Masaüstü, İndirilenler, Belgeler ve Kullanıcı Profili gibi sık kullanılan konumlara tek tıkla erişim sunan özel koyu temalı seçim penceresi.

### 🌐 Çoklu Dil Desteği (TR / EN)
- **Türkçe ve İngilizce**: Tüm arayüz, durum mesajları, araç ipuçları ve başlangıç ekranı eksiksiz olarak iki dilde sunulur.
- **Anında Değiştirme**: Üst paneldeki bayrak simgesinden veya Ayarlar menüsünden uygulamayı yeniden başlatmaya gerek kalmadan canlı dil değişimi yapılabilir.
- **Otomatik Dil Algılama**: İlk açılışta Windows işletim sistemi diline göre otomatik seçim yapar ve tercihi kaydeder.

---

## 📦 İndirme & Sürümler

[Releases](https://github.com/0zMert/BytePeeX/releases) sayfasından ihtiyacınıza uygun paketi indirebilirsiniz:

| Paket | Dosya Boyutu | Açıklama | Hedef Kitle |
| :--- | :---: | :--- | :--- |
| **`BytePeeX-v0.4-win-x64-Standalone.zip`** | **~59 MB** | .NET 9 çalışma zamanını ve tüm bağımlılıkları içinde barındıran taşınabilir (portable) tek `.exe`. Hiçbir kurulum gerektirmez. | **Önerilen** (Her sistemde doğrudan çalışır) |
| **`BytePeeX-v0.4-win-x64.zip`** | **~1.2 MB** | Yalnızca uygulama çekirdeğini içeren ultra hafif tek `.exe`. | Sisteminde **.NET 9 Desktop Runtime** kurulu olanlar |

> ⬇️ *Hafif sürümü kullanmak isteyenler için resmi çalışma zamanı: [Microsoft .NET 9.0 Desktop Runtime (x64) İndir](https://aka.ms/dotnet/9.0/windowsdesktop-runtime-win-x64.exe)*

---

## 🚀 Hızlı Başlangıç

1. [Releases](https://github.com/0zMert/BytePeeX/releases) bölümünden en son sürüm `.zip` arşivini indirin.
2. Arşiv içindeki **`BytePeeX.exe`** dosyasını istediğiniz bir klasöre çıkartın.
3. Çift tıklayarak uygulamayı başlatın.
4. Taramak istediğiniz sürücüyü veya **"Dizin Seç"** butonuyla istediğiniz klasörü belirleyin ve **"Tara"** butonuna basın.

---

## 🛡️ Windows SmartScreen Uyarısı Hakkında

BytePeeX, bağımsız bir açık kaynak topluluk projesidir. Yüksek maliyetli ticari kod imzalama sertifikaları kullanılmadığı için Windows ilk açılışta *"Windows kişisel bilgisayarınızı korudu"* (SmartScreen) uyarısı verebilir.

Uygulamayı çalıştırmak için:
- **Pencere Üzerinden:** Ekranda beliren **"Ek bilgi"** *(More info)* bağlantısına tıklayın, ardından açılan **"Yine de çalıştır"** *(Run anyway)* butonunu seçin.
- **Dosya Özelliklerinden:** İndirdiğiniz `.exe` dosyasına sağ tıklayıp **Özellikler (Properties) ➔ En alttaki "Engellemeyi Kaldır" (Unblock)** kutucuğunu işaretleyip **Tamam**'a basın.

> 🔒 *BytePeeX tamamen açık kaynaklıdır; hiçbir telemetri, veri toplama veya arka plan ağ etkinliği içermez. Tüm kaynak kodları bu depoda şeffaf biçimde incelenebilir.*

---

## 🛠️ Kaynak Koddan Derleme (Build & Test)

Projeyi yerel ortamınızda kaynak koddan derlemek için [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)'nın kurulu olması gerekir:

```powershell
# Depoyu klonlayın
git clone https://github.com/0zMert/BytePeeX.git
cd BytePeeX

# Çözümü Release modunda derleyin
dotnet build BytePeeX.sln -c Release

# Birim testlerini çalıştırın
dotnet test BytePeeX.sln

# Hafif sürüm olarak yayınlayın (~1.2 MB)
dotnet publish BytePeeX.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/win-x64

# Bağımsız tak-çalıştır sürüm olarak yayınlayın (~59 MB)
dotnet publish BytePeeX.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64-standalone
```

---

## 📄 Lisans

Bu proje [MIT Lisansı](LICENSE) kapsamında lisanslanmıştır. Dilediğiniz gibi kullanabilir, geliştirebilir ve katkıda bulunabilirsiniz.
