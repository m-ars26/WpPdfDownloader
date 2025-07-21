using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Text.RegularExpressions;
using OpenQA.Selenium.Support.UI;
using System.Diagnostics;
// Ana program sınıfı
public class Program
{
    // --- UYGULAMA AYARLARI ---
    // Chrome profilinizin kaydedileceği yer. Bu sayede her seferinde QR kod okutmanız gerekmez.
    // "KULLANICI_ADINIZ" yazan yeri kendi Windows kullanıcı adınızla değiştirin.
    private const string UserProfilePath = @"C:\Users\mehme\AppData\Local\Google\Chrome\User Data\WAppProfile_PdfDownloader";

    // Dosyaların ilk olarak indirileceği geçici klasör.
    private const string TempDownloadPath = @"C:\WhatsAppDownloads\Temp";

    // Dosyaların gönderen kişiye göre düzenleneceği nihai klasör.
    private const string FinalOrganizedPath = @"C:\WhatsAppDownloads\Organized";

    // İşlenen PDF'lerin tekrar tekrar indirilmesini önlemek için bir liste.
    // Basit bir örnek için, mesajın metnini veya bir özelliğini saklayabiliriz.
    // Bu, program her çalıştığında sıfırlanır. Daha kalıcı bir çözüm için bu listeyi bir dosyaya yazıp okuyabilirsiniz.
    private static readonly HashSet<string> _processedMessageIds = new HashSet<string>();


    public static async Task Main(string[] args)
    {
        Console.WriteLine("--- WhatsApp PDF Otomatik İndirici Başlatılıyor ---");

        // Gerekli klasörlerin var olduğundan emin ol
        Directory.CreateDirectory(TempDownloadPath);
        Directory.CreateDirectory(FinalOrganizedPath);

        // Chrome ayarlarını yapılandırma
        var chromeOptions = new ChromeOptions();

        // Tarayıcıyı tam ekran başlatmak genellikle elementlerin daha stabil bulunmasına yardımcı olur.
        chromeOptions.AddArgument("--start-maximized");

        // Oturumu kaydetmek için profil yolu. İlk çalıştırmada bu klasör oluşturulur.
        chromeOptions.AddArgument($"user-data-dir={UserProfilePath}");

        // "Farklı Kaydet" penceresini sormadan dosyaları otomatik indir.
        chromeOptions.AddUserProfilePreference("download.prompt_for_download", false);
        chromeOptions.AddUserProfilePreference("download.default_directory", TempDownloadPath);
        chromeOptions.AddUserProfilePreference("plugins.always_open_pdf_externally", true);

        IWebDriver driver = null;

        try
        {
            // ChromeDriver'ı ayarlarımızla başlat
            driver = new ChromeDriver(chromeOptions);
            var whatsAppHandler = new WhatsAppHandler(driver, TempDownloadPath, FinalOrganizedPath, _processedMessageIds);

            // WhatsApp Web'e git
            driver.Navigate().GoToUrl("https://web.whatsapp.com/");
            Console.WriteLine("Lütfen WhatsApp Web'in yüklenmesini bekleyin.");
            Console.WriteLine("İLK ÇALIŞTIRMA: Telefonunuzla QR kodunu okutarak giriş yapın.");
            Console.WriteLine("SONRAKİ ÇALIŞTIRMALAR: Oturumunuz otomatik olarak açılacaktır.");

            // Ana sohbet listesi yüklenene kadar bekle (örneğin 2 dakika kadar)
            // Bu seçici, WhatsApp'ın arama çubuğudur ve genellikle stabil bir elementtir.
            var wait = new WebDriverWait(driver, TimeSpan.FromMinutes(2));
            wait.Until(d => d.FindElement(By.CssSelector("div[role='textbox'][contenteditable='true']")));

            Console.WriteLine("WhatsApp Web başarıyla yüklendi. Sohbetler dinleniyor...");

            // Ana döngü: Belirli aralıklarla yeni PDF'leri kontrol et
            while (true)
            {
                Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] Yeni mesajlar ve PDF'ler kontrol ediliyor...");
                whatsAppHandler.CheckForUnreadChatsAndProcessPdfs();

                // Kontrol döngüsü arasında bekleme süresi
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
        }
        catch (WebDriverTimeoutException)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("HATA: WhatsApp Web 2 dakika içinde yüklenemedi. İnternet bağlantınızı kontrol edin veya QR kodu tekrar okutun.");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Beklenmedik bir hata oluştu: {ex.Message}");
            Console.ResetColor();
        }
        finally
        {
            // Program kapanırken tarayıcıyı düzgünce kapat
            driver?.Quit();
            Console.WriteLine("Tarayıcı kapatıldı. Program sonlandırıldı.");
        }
    }
}

public class WhatsAppHandler
{
    private readonly IWebDriver _driver;
    private readonly string _tempDownloadPath;
    private readonly string _finalOrganizedPath;
    private readonly HashSet<string> _processedMessageIds;

    public WhatsAppHandler(IWebDriver driver, string tempDownloadPath, string finalOrganizedPath, HashSet<string> processedMessageIds)
    {
        _driver = driver;
        _tempDownloadPath = tempDownloadPath;
        _finalOrganizedPath = finalOrganizedPath;
        _processedMessageIds = processedMessageIds;
    }

    public void CheckForUnreadChatsAndProcessPdfs()
    {
        // Okunmamış mesajı olan sohbetleri bul (yeşil bildirim topu olanlar)
        // Bu seçici, okunmamış mesaj sayısını gösteren yeşil daireyi hedefler.
        var unreadMarkers = _driver.FindElements(By.XPath(
            "//span[contains(@aria-label, 'okunmamış mesaj') or contains(@aria-label, 'unread message')]"));

        if (unreadMarkers.Count == 0)
        {
            Console.WriteLine("Okunmamış yeni mesaj bulunamadı.");
            return;
        }

        Console.WriteLine($"Bulunan okunmamış sohbet sayısı: {unreadMarkers.Count}");

        // Element listesi DOM'da değişebileceği için döngüden önce listeyi kopyalıyoruz.
        var chatsToProcess = new List<IWebElement>();
        foreach (var marker in unreadMarkers)
        {
            try
            {
                // Bildirim ikonundan yola çıkarak tüm sohbet kutusunu (tıklanabilir alanı) bul
                var chatContainer = marker.FindElement(By.XPath("./ancestor::div[@role='listitem']"));
                chatsToProcess.Add(chatContainer);
            }
            catch (Exception) { /* Element DOM'dan kaybolmuş olabilir, önemseme */ }
        }

        foreach (var chat in chatsToProcess)
        {
            try
            {
                chat.Click(); // Sohbete tıkla
                Thread.Sleep(2000); // Mesajların yüklenmesi için kısa bir bekleme

                Console.WriteLine("Aktif sohbetteki PDF'ler taranıyor...");
                ProcessPdfsInActiveChat();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sohbete tıklanırken bir hata oluştu: {ex.Message}");
            }
        }
    }

    private void ProcessPdfsInActiveChat()
    {
        try
        {
            IReadOnlyCollection<IWebElement> pdfMessages = new List<IWebElement>();

            try
            {
                var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));
                pdfMessages = wait.Until(driver =>
                {
                    var found = driver.FindElements(By.XPath("//span[contains(text(), '.pdf')]"));
                    return found.Count > 0 ? found : null;
                });
            }
            catch (WebDriverTimeoutException)
            {
                Console.WriteLine("Bu sohbette PDF bulunamadı.");
                return;
            }

            if (pdfMessages == null || pdfMessages.Count == 0)
            {
                Console.WriteLine("Bu sohbette PDF bulunamadı.");
                return;
            }

            Console.WriteLine($"Bu sohbette {pdfMessages.Count} adet PDF bulundu. İşleniyor...");

            foreach (var pdfMessage in pdfMessages)
            {
                try
                {
                    string fileName = pdfMessage.Text ?? $"PDF_{DateTime.Now.Ticks}";
                    string messageId = fileName;

                    if (_processedMessageIds.Contains(messageId))
                        continue;

                    //var headerElement = _driver.FindElement(By.CssSelector("header span[data-testid='conversation-header-title']"));
                    //string senderName = SanitizeFolderName(headerElement.Text);

                    string senderName = "Bilinmiyor";

                    try
                    {
                        var headerElement = _driver.FindElement(By.XPath("//header//span[@dir='auto']"));
                        senderName = SanitizeFolderName(headerElement.Text);
                    }
                    catch (NoSuchElementException)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Uyarı: Gönderen adı alınamadı. Varsayılan isim kullanılacak.");
                        Console.ResetColor();
                    }

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"'{senderName}' kişisinden yeni PDF bulundu: {fileName}");
                    Console.ResetColor();
                    
                    // PDF mesajının yukarısındaki "indir" butonunu bulmaya çalış
                    IWebElement downloadButton = null;
                    try
                    {   
                        downloadButton = pdfMessage.FindElement(By.XPath(".//ancestor::div[@role='button']"));
                        downloadButton.Click();
                        Console.WriteLine("İndirme butonuna tıklandı.");
                    }
                    catch
                    {
                        // Eğer indir butonu yoksa doğrudan PDF ismine tıkla
                        pdfMessage.Click();
                        Console.WriteLine("PDF adına tıklandı.");
                    }

                    WaitForDownloadCompletion(fileName.Split('.')[0]);

                    MoveLatestFileToOrganizedFolder(senderName);
                    _processedMessageIds.Add(messageId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Bir PDF işlenirken hata oluştu: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PDF tarama genel hatası: {ex.Message}");
        }
    }
    private void MoveLatestFileToOrganizedFolder(string senderName)
    {
        // İndirmenin tamamlanması için bir süre bekle (dosya boyutuna göre ayarlanabilir)
        //Thread.Sleep(5000);

        var directoryInfo = new DirectoryInfo(_tempDownloadPath);

        // Son 1 dakika içinde oluşturulmuş dosyaları bul ve en yenisini al
        var recentFile = directoryInfo.GetFiles()
            .Where(f => f.LastWriteTime > DateTime.Now.AddMinutes(-1) && !f.Name.EndsWith(".crdownload"))
            .OrderByDescending(f => f.LastWriteTime)
            .FirstOrDefault();

        if (recentFile != null)
        {
            string personFolder = Path.Combine(_finalOrganizedPath, senderName);
            Directory.CreateDirectory(personFolder); // Klasör yoksa oluştur

            string destinationPath = Path.Combine(personFolder, recentFile.Name);

            // Eğer aynı isimde bir dosya varsa, ismini değiştir (örn: dosya(1).pdf)
            int count = 1;
            string fileNameOnly = Path.GetFileNameWithoutExtension(destinationPath);
            string extension = Path.GetExtension(destinationPath);
            while (File.Exists(destinationPath))
            {
                destinationPath = Path.Combine(personFolder, $"{fileNameOnly}({count++}){extension}");
            }

            File.Move(recentFile.FullName, destinationPath);
            Console.WriteLine($"Başarılı: '{recentFile.Name}' dosyası '{personFolder}' klasörüne taşındı.");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Uyarı: İndirilen dosya geçici klasörde bulunamadı. İndirme başarısız olmuş olabilir.");
            Console.ResetColor();
        }
    }

    // Klasör adlarında kullanılamayacak geçersiz karakterleri temizler.
    private string SanitizeFolderName(string name)
    {
        string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        var regex = new Regex(string.Format("[{0}]", Regex.Escape(invalidChars)));
        return regex.Replace(name, "");
    }
    private void WaitForDownloadCompletion(string expectedFileNameBase, int timeoutSeconds = 30)
{
    var watch = Stopwatch.StartNew();

    while (watch.Elapsed.TotalSeconds < timeoutSeconds)
    {
        var files = new DirectoryInfo(_tempDownloadPath).GetFiles();
        var matchingFile = files.FirstOrDefault(f =>
            f.Name.StartsWith(expectedFileNameBase) &&
            !f.Name.EndsWith(".crdownload"));

        if (matchingFile != null)
            return;

        Thread.Sleep(1000);
    }

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("Uyarı: Dosya indirimi zaman aşımına uğradı.");
    Console.ResetColor();
}
}

