using System;
using System.Text;

public static class Logger
{
    private static bool _encodingSet = false;
    
    private static void EnsureEncodingSet()
    {
        if (_encodingSet) return;
        
        try
        {
            // Console encoding'i düzelt
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            
            // Windows için ek encoding ayarları
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                try
                {
                    // UTF-8 Code Page ayarla
                    var utf8 = new UTF8Encoding(false);
                    Console.OutputEncoding = utf8;
                }
                catch
                {
                    // Fallback: Turkish Windows encoding
                    Console.OutputEncoding = Encoding.GetEncoding(1254);
                }
            }
            
            _encodingSet = true;
        }
        catch (Exception ex)
        {
            // Encoding ayarlanamadıysa bile devam et
            Console.WriteLine($"Encoding ayarlanamadı: {ex.Message}");
        }
    }
    
    public static void LogInfo(string message)
    {
        EnsureEncodingSet();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"[INFO] {message}");
        Console.ResetColor();
    }

    public static void LogSuccess(string message)
    {
        EnsureEncodingSet();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[SUCCESS] {message}");
        Console.ResetColor();
    }

    public static void LogWarning(string message)
    {
        EnsureEncodingSet();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARNING] {message}");
        Console.ResetColor();
    }

    public static void LogError(string message)
    {
        EnsureEncodingSet();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {message}");
        Console.ResetColor();
    }
}