# Folderize Yayınlama (Publish) Kılavuzu 🚀

Bu doküman, **Folderize** uygulamasının yeni sürümlerinin nasıl derlenip yayınlanacağını (Release / Publish adımlarını) adım adım açıklamaktadır.

---

## 📋 Sürüm Bilgisi (v0.4.0)

| Özellik | Detay |
|---|---|
| **Sürüm** | `0.4.0` (v0.4) |
| **Hedef Platform** | Windows 10 / Windows 11 (64-bit - `win-x64`) |
| **Platform** | .NET 9.0 (WPF) |
| **Çıkış Tarihi** | 17 Eylül 2026 |

---

## 🛠️ Yayınlama Komutları (Build & Publish)

Folderize iki farklı dağıtım modeliyle yayınlanır:

### 1. Hafif Sürüm (Framework-Dependent Single File)
* Bilgisayarında .NET 9 Desktop Runtime yüklü olan kullanıcılar içindir.
* Dosya boyutu yalnızca **~1.7 MB** civarındadır.

```powershell
dotnet publish Folderize.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -p:PublishSingleFile=true `
  -o publish/win-x64
```

### 2. Bağımsız Sürüm (Self-Contained Standalone)
* Bilgisayarında .NET yüklü olmayan tüm Windows kullanıcıları için tak-çalıştır sürümdür.
* .NET çalışma zamanını kendi içine gömer ve tek bir `.exe` üretir.

```powershell
dotnet publish Folderize.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:EnableCompressionInSingleFile=true `
  -o publish/win-x64-standalone
```

---

## 📦 Zip Paketlerini Oluşturma

Yayınlanan çıktıları GitHub Releases için `.zip` formatında paketlemek için PowerShell komutları:

```powershell
# Hafif paketi zip yap
Compress-Archive -Path "publish/win-x64/Folderize.exe" -DestinationPath "publish/Folderize-v0.4-win-x64.zip" -Force

# Bağımsız paketi zip yap
Compress-Archive -Path "publish/win-x64-standalone/Folderize.exe" -DestinationPath "publish/Folderize-v0.4-win-x64-Standalone.zip" -Force
```

---

## 🏷️ GitHub Sürüm Notları Şablonu (Release Notes)

GitHub üzerinde yeni release oluştururken kullanabileceğiniz sürüm notu taslağı:

```markdown
# Folderize v0.4 🚀

### ✨ Yenilikler ve İyileştirmeler:
- 🗑️ **Geri Dönüşüm Kutusu Entegrasyonu**: Ağaç tablosundaki dosyaları sağ tık menüsünden veya Del tuşuyla onay alarak güvenle Windows Geri Dönüşüm Kutusu'na gönderme.
- 🛡️ **Gelişmiş Çekirdek Tarayıcı (SeBackupPrivilege)**: Win32 `FILE_FLAG_BACKUP_SEMANTICS` çekirdek seviyesi erişim ile `System Volume Information` ve korumalı tüm sistem klasörlerini eksiksiz okuma.
- ⏱️ **5 Saniyede Bir Canlı Sürücü Takibi**: Sol alttaki yerel sürücü doluluk kartlarının 5 saniyede bir milisaniyelik API ile otomatik senkronizasyonu.
- 🎨 **Karanlık Tema İyileştirmeleri**: Sağ tık menüsündeki beyaz sütun görsel bozukluğunun giderilmesi ve seçili satırlarda yüksek kontrastlı lacivert vurgu.
- 🛡️ **Otomatik Yönetici Yetkisi**: Projeye `highestAvailable` uygulama manifesti ve standart modda açıldığında tek tıkla yöneticiye geçiren buton eklendi.

### 📦 İndirme Seçenekleri:
- **`Folderize-v0.4-win-x64-Standalone.zip`** (Önerilen): .NET kurulumu gerektirmez, doğrudan çalışır.
- **`Folderize-v0.4-win-x64.zip`**: .NET 9 Desktop Runtime kurulu sistemler için ultra hafif paket.
```

---

## ⚠️ Önemli Kontroller
1. Yayınlamadan önce tüm testlerin geçtiğinden emin olun: `dotnet test Tests/Tests.csproj`
2. `Folderize.exe` uygulamasının arka planda açık olmadığından emin olun (dosya kilitlenme hatası almamak için).
