using System;
using System.Security.Cryptography;
using System.Text;

public static class DeviceHelper
{
    public static string GetUniqueDeviceId()
    {
        string machineName = Environment.MachineName;
        string userName = Environment.UserName;
        string osVersion = Environment.OSVersion.ToString();

        string rawId = $"{machineName}-{userName}-{osVersion}";

        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawId));

        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
}
