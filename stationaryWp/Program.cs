using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

class Program
{
    private const string UserProfilePath = @"C:\Users\mehme\AppData\Local\Google\Chrome\User Data\WAppProfile_PdfDownloader";
    private const string TempDownloadPath = @"C:\WhatsAppDownloads\Temp";
    private const string FinalOrganizedPath = @"C:\WhatsAppDownloads\Organized";

    public static async Task Main(string[] args)
    {
        Console.WriteLine("--- WhatsApp PDF Otomatik İndirici Başlatılıyor ---");

        Directory.CreateDirectory(TempDownloadPath);
        Directory.CreateDirectory(FinalOrganizedPath);

        var chromeOptions = new ChromeOptions();
        chromeOptions.AddArgument("--start-maximized");
        chromeOptions.AddArgument($"user-data-dir={UserProfilePath}");
        chromeOptions.AddUserProfilePreference("download.prompt_for_download", false);
        chromeOptions.AddUserProfilePreference("download.default_directory", TempDownloadPath);
        chromeOptions.AddUserProfilePreference("plugins.always_open_pdf_externally", true);

        IWebDriver driver = null;
        var processedIdsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "processed_ids.txt");

        try
        {
            driver = new ChromeDriver(chromeOptions);

            var whatsAppHandler = new WhatsAppHandler(driver, TempDownloadPath, FinalOrganizedPath, processedIdsFile);

            driver.Navigate().GoToUrl("https://web.whatsapp.com/");
            Console.WriteLine("Lütfen WhatsApp Web'in yüklenmesini bekleyin.");
            Console.WriteLine("İLK ÇALIŞTIRMA: Telefonunuzla QR kodunu okutarak giriş yapın.");
            Console.WriteLine("SONRAKİ ÇALIŞTIRMALAR: Oturumunuz otomatik olarak açılacaktır.");

            var wait = new WebDriverWait(driver, TimeSpan.FromMinutes(2));
            await Task.Run(() => wait.Until(d => d.FindElement(By.CssSelector("div[role='textbox'][contenteditable='true']"))));

            Console.WriteLine("WhatsApp Web başarıyla yüklendi. Sohbetler dinleniyor...");

            while (true)
            {
                Console.WriteLine($"\n[{DateTime.Now:HH:mm:ss}] Yeni mesajlar ve PDF'ler kontrol ediliyor...");

                await whatsAppHandler.ProcessOnlyNewestUnreadChatAsync();

                // Aktif sohbetteki yeni PDF'leri de kontrol et
                await whatsAppHandler.ProcessPdfsInActiveChatAsync();

                await Task.Delay(TimeSpan.FromSeconds(5));  // Döngü bekleme süresi kısaltıldı
            }
        }
        catch (WebDriverTimeoutException)
        {
            Logger.LogError("HATA: WhatsApp Web 2 dakika içinde yüklenemedi. İnternet bağlantınızı kontrol edin veya QR kodu tekrar okutun.");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Beklenmedik bir hata oluştu: {ex.Message}");
        }
        finally
        {
            driver?.Quit();
            Logger.LogInfo("Tarayıcı kapatıldı. Program sonlandırıldı.");
        }
    }
}
