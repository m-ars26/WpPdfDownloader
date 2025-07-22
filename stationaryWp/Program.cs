using System;
using System.Windows.Forms;
using System.Threading.Tasks;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Encoding ayarları (console için)
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                Console.OutputEncoding = new System.Text.UTF8Encoding(false);
            }
        }
        catch { /* Ignore */ }

        // Windows Forms application ayarları
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // MainForm'u başlat
        Application.Run(new MainForm());
    }
}