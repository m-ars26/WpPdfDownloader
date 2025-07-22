using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

public class WhatsAppHandler
{
    private readonly IWebDriver _driver;
    private readonly string _tempDownloadPath;
    private readonly string _finalOrganizedPath;
    private HashSet<string> _processedMessageIds;
    private readonly string _processedIdsFilePath;

    public WhatsAppHandler(IWebDriver driver, string tempDownloadPath, string finalOrganizedPath, string processedIdsFilePath)
    {
        _driver = driver;
        _tempDownloadPath = tempDownloadPath;
        _finalOrganizedPath = finalOrganizedPath;
        _processedIdsFilePath = processedIdsFilePath;

        _processedMessageIds = LoadProcessedIds();
    }

    private HashSet<string> LoadProcessedIds()
    {
        if (File.Exists(_processedIdsFilePath))
            return new HashSet<string>(File.ReadAllLines(_processedIdsFilePath));
        return new HashSet<string>();
    }

    private void SaveProcessedIds()
    {
        File.WriteAllLines(_processedIdsFilePath, _processedMessageIds);
    }

    // Sadece en yeni unread sohbeti açar ve onun PDF'lerini indirir
    public async Task ProcessOnlyNewestUnreadChatAsync()
    {
        var unreadMarkers = _driver.FindElements(By.XPath(
            "//span[contains(@aria-label, 'okunmamış mesaj') or contains(@aria-label, 'unread message')]"));

        if (unreadMarkers.Count == 0)
        {
            Logger.LogInfo("Okunmamış yeni mesaj bulunamadı.");
            return;
        }

        var newestMarker = unreadMarkers.Last();

        IWebElement chatContainer;
        try
        {
            chatContainer = newestMarker.FindElement(By.XPath("./ancestor::div[@role='listitem']"));
        }
        catch
        {
            Logger.LogWarning("Unread marker'ın sohbet konteyneri bulunamadı.");
            return;
        }

        try
        {
            chatContainer.Click();
            await Task.Delay(2000); // Yüklenme için kısaltıldı

            Logger.LogInfo("Yeni mesajın olduğu sohbette PDF'ler taranıyor...");
            await ProcessPdfsInActiveChatAsync();
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Sohbete tıklanırken hata: {ex.Message}");
        }
    }

    // Aktif sohbetdeki yeni PDF'leri indirir, sadece indirme butonuna tıklar
    public async Task ProcessPdfsInActiveChatAsync()
{
    IReadOnlyCollection<IWebElement>? pdfMessages = null;

    try
    {
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));
        pdfMessages = await Task.Run(() => wait.Until(driver =>
        {
            var found = driver.FindElements(By.XPath("//span[contains(text(), '.pdf')]"));
            return found.Count > 0 ? found : null;
        }));
    }
    catch (WebDriverTimeoutException)
    {
        Logger.LogInfo("Bu sohbette PDF bulunamadı.");
        return;
    }

    if (pdfMessages == null || pdfMessages.Count == 0)
    {
        Logger.LogInfo("Bu sohbette PDF bulunamadı.");
        return;
    }

    Logger.LogInfo($"Bu sohbette {pdfMessages.Count} adet PDF bulundu. İşleniyor...");

    foreach (var pdfMessage in pdfMessages)
    {
        try
        {
            string fileName = pdfMessage.Text ?? $"PDF_{DateTime.Now.Ticks}";
            string messageId = GenerateMessageId(pdfMessage);

            if (_processedMessageIds.Contains(messageId))
                continue;

            string senderName = "Bilinmiyor";
            try
            {
                var headerElement = _driver.FindElement(By.XPath("//header//span[@dir='auto']"));
                senderName = SanitizeFolderName(headerElement.Text);
            }
            catch { }

            Logger.LogSuccess($"'{senderName}' kişisinden yeni PDF bulundu: {fileName}");

            try
            {
                // Yalnızca bu .pdf mesajına ait olan indirme butonunu bul
                var downloadButton = pdfMessage
                    .FindElement(By.XPath(".//ancestor::div[contains(@class, 'message-')]//span[contains(@data-icon, 'download')]"));

                if (downloadButton != null)
                {
                    downloadButton.Click();
                    Logger.LogInfo("PDF kutusu üzerindeki indirme butonuna tıklandı.");

                    await WaitUntilDownloadStartsAndCompletesAsync();
                    MoveLatestFileToOrganizedFolder(senderName);

                    _processedMessageIds.Add(messageId);
                    SaveProcessedIds();
                }
                else
                {
                    Logger.LogWarning("PDF kutusunda indirme butonu bulunamadı. İndirme atlandı.");
                }
            }
            catch (NoSuchElementException)
            {
                Logger.LogWarning("PDF mesajına ait indirme butonu bulunamadı.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"İndirme sırasında hata: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Bir PDF işlenirken hata oluştu: {ex.Message}");
        }
    }
}

    private string GenerateMessageId(IWebElement pdfMessage)
    {
        return (pdfMessage.Text + "_" + (pdfMessage.GetAttribute("data-id") ?? Guid.NewGuid().ToString())).GetHashCode().ToString();
    }

    private async Task WaitUntilDownloadStartsAndCompletesAsync(int timeoutSeconds = 20)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();

        FileInfo? downloadedFile = null;

        while (watch.Elapsed.TotalSeconds < timeoutSeconds)
        {
            downloadedFile = new DirectoryInfo(_tempDownloadPath)
                .GetFiles()
                .FirstOrDefault(f => !f.Name.EndsWith(".crdownload", StringComparison.OrdinalIgnoreCase) &&
                                     !f.Name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) &&
                                     !f.Name.EndsWith(".part", StringComparison.OrdinalIgnoreCase));

            if (downloadedFile != null)
                break;

            await Task.Delay(300);
        }

        if (downloadedFile == null)
        {
            Logger.LogWarning("İndirme hiç başlamadı veya dosya bulunamadı.");
            return;
        }

        Logger.LogInfo($"İndirilen dosya bulundu: {downloadedFile.Name}");

        bool IsFileLocked(FileInfo file)
        {
            try
            {
                using var stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return false;
            }
            catch (IOException)
            {
                return true;
            }
        }

        while (IsFileLocked(downloadedFile))
        {
            await Task.Delay(300);

            if (watch.Elapsed.TotalSeconds > timeoutSeconds)
            {
                Logger.LogWarning("Dosya kilit durumu zaman aşımına uğradı.");
                break;
            }
        }

        Logger.LogInfo("İndirme tamamlandı ve dosya serbest.");
    }

    private void MoveLatestFileToOrganizedFolder(string senderName)
    {
        var directoryInfo = new DirectoryInfo(_tempDownloadPath);

        var recentFile = directoryInfo.GetFiles()
            .Where(f => !f.Name.EndsWith(".crdownload", StringComparison.OrdinalIgnoreCase) &&
                        !f.Name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) &&
                        !f.Name.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.LastWriteTime)
            .FirstOrDefault();

        if (recentFile != null)
        {
            string personFolder = Path.Combine(_finalOrganizedPath, senderName);
            Directory.CreateDirectory(personFolder);

            string destinationPath = Path.Combine(personFolder, recentFile.Name);
            string fileNameOnly = Path.GetFileNameWithoutExtension(destinationPath);
            string extension = Path.GetExtension(destinationPath);

            int count = 1;
            while (File.Exists(destinationPath))
            {
                destinationPath = Path.Combine(personFolder, $"{fileNameOnly}({count++}){extension}");
            }

            File.Move(recentFile.FullName, destinationPath);
            Logger.LogSuccess($"'{recentFile.Name}' dosyası '{personFolder}' klasörüne taşındı.");
        }
        else
        {
            Logger.LogWarning("İndirilen dosya bulunamadı. İndirme başarısız olabilir.");
        }
    }

    private string SanitizeFolderName(string name)
    {
        string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        var regex = new Regex($"[{Regex.Escape(invalidChars)}]");
        return regex.Replace(name, "");
    }
}
