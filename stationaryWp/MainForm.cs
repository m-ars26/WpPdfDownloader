using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

public partial class MainForm : Form
{
    private Button startButton;
    private Button stopButton;
    private TextBox logTextBox;
    private Label statusLabel;
    private Label deviceIdLabel;
    private ProgressBar progressBar;
    private GroupBox controlsGroupBox;
    private GroupBox statusGroupBox;
    private GroupBox logGroupBox;
    private Label contactLabel;
    
    private bool isRunning = false;
    private CancellationTokenSource? cancellationTokenSource;
    private IWebDriver? driver;
    
    private const string UserProfilePath = @"C:\Users\mehme\AppData\Local\Google\Chrome\User Data\WAppProfile_PdfDownloader";
    private const string TempDownloadPath = @"C:\WhatsAppDownloads\Temp";
    private const string FinalOrganizedPath = @"C:\WhatsAppDownloads\Organized";

    public MainForm()
    {
        InitializeComponent();
        InitializeDirectories();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        
        this.Size = new Size(800, 600);
        this.Text = "WhatsApp PDF Otomatik İndirici";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;

         // Contact Label (sağ üst köşede)
        contactLabel = new Label();
        contactLabel.Text = "Bize ulaşın:\nAlp Arslan\n0545 946 87 97";
        contactLabel.Size = new Size(130, 60);
        contactLabel.Location = new Point(630, 35);
        contactLabel.Font = new Font("Arial", 8, FontStyle.Regular);
        contactLabel.ForeColor = Color.Gray;
        contactLabel.TextAlign = ContentAlignment.TopCenter;
        contactLabel.BorderStyle = BorderStyle.FixedSingle;
        contactLabel.BackColor = Color.LightYellow;
        contactLabel.Cursor = Cursors.Hand;
        contactLabel.Click += ContactLabel_Click;
        this.Controls.Add(contactLabel);

        // Controls GroupBox
        controlsGroupBox = new GroupBox();
        controlsGroupBox.Text = "Kontroller";
        controlsGroupBox.Size = new Size(760, 80);
        controlsGroupBox.Location = new Point(20, 20);
        this.Controls.Add(controlsGroupBox);

        // Start Button
        startButton = new Button();
        startButton.Text = "Başlat";
        startButton.Size = new Size(100, 35);
        startButton.Location = new Point(20, 25);
        startButton.BackColor = Color.LightGreen;
        startButton.FlatStyle = FlatStyle.Flat;
        startButton.Click += StartButton_Click;
        controlsGroupBox.Controls.Add(startButton);

        // Stop Button
        stopButton = new Button();
        stopButton.Text = "Durdur";
        stopButton.Size = new Size(100, 35);
        stopButton.Location = new Point(140, 25);
        stopButton.BackColor = Color.LightCoral;
        stopButton.FlatStyle = FlatStyle.Flat;
        stopButton.Enabled = false;
        stopButton.Click += StopButton_Click;
        controlsGroupBox.Controls.Add(stopButton);

        // Progress Bar
        progressBar = new ProgressBar();
        progressBar.Size = new Size(200, 25);
        progressBar.Location = new Point(260, 30);
        progressBar.Style = ProgressBarStyle.Marquee;
        progressBar.MarqueeAnimationSpeed = 0;
        controlsGroupBox.Controls.Add(progressBar);

        // Status GroupBox
        statusGroupBox = new GroupBox();
        statusGroupBox.Text = "Durum Bilgileri";
        statusGroupBox.Size = new Size(760, 120);
        statusGroupBox.Location = new Point(20, 110);
        this.Controls.Add(statusGroupBox);

        // Status Label
        statusLabel = new Label();
        statusLabel.Text = "Hazır";
        statusLabel.Size = new Size(720, 25);
        statusLabel.Location = new Point(20, 30);
        statusLabel.Font = new Font("Arial", 10, FontStyle.Bold);
        statusLabel.ForeColor = Color.DarkBlue;
        statusGroupBox.Controls.Add(statusLabel);

        // Device ID Label
        deviceIdLabel = new Label();
        deviceIdLabel.Text = "Cihaz ID yükleniyor...";
        deviceIdLabel.Size = new Size(720, 50);
        deviceIdLabel.Location = new Point(20, 60);
        deviceIdLabel.Font = new Font("Arial", 9);
        deviceIdLabel.ForeColor = Color.Gray;
        statusGroupBox.Controls.Add(deviceIdLabel);

        // Log GroupBox
        logGroupBox = new GroupBox();
        logGroupBox.Text = "Log Mesajları";
        logGroupBox.Size = new Size(760, 310);
        logGroupBox.Location = new Point(20, 240);
        this.Controls.Add(logGroupBox);

        // Log TextBox
        logTextBox = new TextBox();
        logTextBox.Multiline = true;
        logTextBox.ScrollBars = ScrollBars.Vertical;
        logTextBox.ReadOnly = true;
        logTextBox.Size = new Size(720, 270);
        logTextBox.Location = new Point(20, 25);
        logTextBox.Font = new Font("Consolas", 9);
        logTextBox.BackColor = Color.Black;
        logTextBox.ForeColor = Color.LightGreen;
        logGroupBox.Controls.Add(logTextBox);


        // Form events
        this.Load += MainForm_Load;
        this.FormClosing += MainForm_FormClosing;
        
        this.ResumeLayout(false);
    }

    private void InitializeDirectories()
    {
        try
        {
            Directory.CreateDirectory(TempDownloadPath);
            Directory.CreateDirectory(FinalOrganizedPath);
        }
        catch (Exception ex)
        {
            LogMessage($"Klasör oluşturma hatası: {ex.Message}", LogLevel.Error);
        }
    }

    private void ContactLabel_Click(object sender, EventArgs e)
    {
        try
        {
            // Telefon numarasını panoya kopyala
            Clipboard.SetText("05459468797");
            
            MessageBox.Show(
                "Telefon numarası panoya kopyalandı!\n\nAlp Arslan\n0545 946 87 97\n\nWhatsApp'tan yazabilirsiniz veya arama yapabilirsiniz.",
                "İletişim Bilgisi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "İletişim Bilgisi:\n\nAlp Arslan\n0545 946 87 97\n\nNumara kopyalanamadı: " + ex.Message,
                "İletişim",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
    private async void MainForm_Load(object sender, EventArgs e)
    {
        try
        {
            LogMessage("Uygulama başlatılıyor...", LogLevel.Info);
            
            // Device ID'yi yükle
            string deviceId = DeviceHelper.GetUniqueDeviceId();
            deviceIdLabel.Text = $"Cihaz ID: {deviceId}";
            
            LogMessage($"Cihaz ID: {deviceId}", LogLevel.Info);
            LogMessage("Firestore yetki kontrolü yapılıyor...", LogLevel.Info);
            
            // Yetki kontrolü
            var firestoreService = new FirestoreService();
            bool isAuthorized = await firestoreService.IsDeviceAuthorizedAsync(deviceId);
            
            if (isAuthorized)
            {
                LogMessage("Cihaz yetkilendirildi. Başlatmaya hazır.", LogLevel.Success);
                statusLabel.Text = "Yetkilendirildi - Başlatmaya Hazır";
                statusLabel.ForeColor = Color.Green;
                startButton.Enabled = true;
            }
            else
            {
                LogMessage("UYARI: Bu cihaz yetkilendirilmemiş!", LogLevel.Error);
                statusLabel.Text = "Yetkisiz Cihaz - İletişime Geçin";
                statusLabel.ForeColor = Color.Red;
                startButton.Enabled = false;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Başlangıç kontrolü hatası: {ex.Message}", LogLevel.Error);
            statusLabel.Text = "Hata - Başlatılamadı";
            statusLabel.ForeColor = Color.Red;
            startButton.Enabled = false;
        }
    }

    private async void StartButton_Click(object sender, EventArgs e)
    {
        if (!isRunning)
        {
            isRunning = true;
            startButton.Enabled = false;
            stopButton.Enabled = true;
            statusLabel.Text = "Çalışıyor...";
            statusLabel.ForeColor = Color.Orange;
            progressBar.MarqueeAnimationSpeed = 30;

            cancellationTokenSource = new CancellationTokenSource();
            
            try
            {
                await StartWhatsAppProcessAsync(cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                LogMessage("İşlem kullanıcı tarafından iptal edildi.", LogLevel.Warning);
            }
            catch (Exception ex)
            {
                LogMessage($"Beklenmedik hata: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                StopProcess();
            }
        }
    }

    private void StopButton_Click(object sender, EventArgs e)
    {
        cancellationTokenSource?.Cancel();
        StopProcess();
        LogMessage("İşlem kullanıcı tarafından durduruldu.", LogLevel.Warning);
    }

    private void StopProcess()
    {
        isRunning = false;
        startButton.Enabled = true;
        stopButton.Enabled = false;
        statusLabel.Text = "Durduruldu";
        statusLabel.ForeColor = Color.Red;
        progressBar.MarqueeAnimationSpeed = 0;
        
        try
        {
            driver?.Quit();
            driver = null;
        }
        catch (Exception ex)
        {
            LogMessage($"Chrome kapatma hatası: {ex.Message}", LogLevel.Warning);
        }
    }

    private async Task StartWhatsAppProcessAsync(CancellationToken cancellationToken)
    {
        LogMessage("WhatsApp PDF İndirici başlatılıyor...", LogLevel.Info);

        try
        {
            // Chrome seçenekleri
            var chromeOptions = new ChromeOptions();
            chromeOptions.AddArgument("--start-maximized");
            chromeOptions.AddArgument($"user-data-dir={UserProfilePath}");
            chromeOptions.AddUserProfilePreference("download.prompt_for_download", false);
            chromeOptions.AddUserProfilePreference("download.default_directory", TempDownloadPath);
            chromeOptions.AddUserProfilePreference("plugins.always_open_pdf_externally", true);

            LogMessage("Chrome tarayıcısı açılıyor...", LogLevel.Info);
            driver = new ChromeDriver(chromeOptions);

            var processedIdsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "processed_ids.txt");
            var whatsAppHandler = new WhatsAppHandler(driver, TempDownloadPath, FinalOrganizedPath, processedIdsFile);

            driver.Navigate().GoToUrl("https://web.whatsapp.com/");
            LogMessage("WhatsApp Web'e bağlanılıyor...", LogLevel.Info);
            LogMessage("İLK ÇALIŞTIRMA: Telefonunuzla QR kodunu okutun", LogLevel.Warning);
            LogMessage("SONRAKİ ÇALIŞTIRMALAR: Otomatik giriş yapılır", LogLevel.Info);

            // WhatsApp yüklenmesini bekle
            statusLabel.Text = "WhatsApp Web yükleniyor...";
            var wait = new WebDriverWait(driver, TimeSpan.FromMinutes(2));
            
            await Task.Run(() => {
                wait.Until(d => d.FindElement(By.CssSelector("div[role='textbox'][contenteditable='true']")));
            }, cancellationToken);

            LogMessage("WhatsApp Web başarıyla yüklendi!", LogLevel.Success);
            statusLabel.Text = "Aktif - PDF'ler Taranıyor";
            statusLabel.ForeColor = Color.Green;

            // Ana döngü
            while (!cancellationToken.IsCancellationRequested)
            {
                LogMessage($"[{DateTime.Now:HH:mm:ss}] Yeni mesajlar kontrol ediliyor...", LogLevel.Info);

                await whatsAppHandler.ProcessOnlyNewestUnreadChatAsync();
                await whatsAppHandler.ProcessPdfsInActiveChatAsync();

                await Task.Delay(5000, cancellationToken);
            }
        }
        catch (WebDriverTimeoutException)
        {
            LogMessage("HATA: WhatsApp Web yüklenemedi. İnternet bağlantısını kontrol edin.", LogLevel.Error);
            statusLabel.Text = "Bağlantı Hatası";
            statusLabel.ForeColor = Color.Red;
        }
        catch (OperationCanceledException)
        {
            // Bu normal bir durum, tekrar throw et
            throw;
        }
        catch (Exception ex)
        {
            LogMessage($"WhatsApp işlemi hatası: {ex.Message}", LogLevel.Error);
            statusLabel.Text = "İşlem Hatası";
            statusLabel.ForeColor = Color.Red;
        }
    }

    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    private void LogMessage(string message, LogLevel level)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<string, LogLevel>(LogMessage), message, level);
            return;
        }

        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string prefix = level switch
        {
            LogLevel.Info => "[INFO]",
            LogLevel.Success => "[SUCCESS]",
            LogLevel.Warning => "[WARNING]",
            LogLevel.Error => "[ERROR]",
            _ => "[LOG]"
        };

        string logLine = $"[{timestamp}] {prefix} {message}";
        logTextBox.AppendText(logLine + Environment.NewLine);
        logTextBox.ScrollToCaret();

        // Console'a da yazdır (eski sistem ile uyumluluk için)
        try
        {
            switch (level)
            {
                case LogLevel.Info:
                    Logger.LogInfo(message);
                    break;
                case LogLevel.Success:
                    Logger.LogSuccess(message);
                    break;
                case LogLevel.Warning:
                    Logger.LogWarning(message);
                    break;
                case LogLevel.Error:
                    Logger.LogError(message);
                    break;
            }
        }
        catch
        {
            // Logger sınıfı yoksa console'a yazdır
            Console.WriteLine(logLine);
        }
    }

    private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (isRunning)
        {
            var result = MessageBox.Show(
                "Program hala çalışıyor. Kapatmak istediğinizden emin misiniz?",
                "Uyarı",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.No)
            {
                e.Cancel = true;
                return;
            }

            cancellationTokenSource?.Cancel();
            StopProcess();
        }
    }

    // SetVisibleCore override'ı kaldırıldı - gereksiz ve sorunlara neden olabilir
}