using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace BalanzaQ.Web.Security;

public static class SecurityUtils
{
    // Clave secreta para cifrado AES (Debe ser la misma en el generador)
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("B4l4nz4Q_S3cur1ty_K3y_2024_04_!#"); // 32 bytes
    private static readonly byte[] IV = Encoding.UTF8.GetBytes("B4l4nz4Q_IV_2024"); // 16 bytes

    public static string GetMachineFingerprint()
    {
        try
        {
            string motherboard = GetMotherboardSerial();
            string disk = GetDiskSerial();
            
            string rawId = $"{motherboard}|{disk}";
            return ComputeHash(rawId);
        }
        catch (Exception)
        {
            return "ERROR_RETRIEVING_HWID";
        }
    }

    private static string GetMotherboardSerial()
    {
        string serial = "MB-UNKNOWN";
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
            foreach (var obj in searcher.Get())
            {
                serial = obj["SerialNumber"]?.ToString()?.Trim() ?? "MB-UNKNOWN";
                break;
            }
        }
        catch { }
        return serial;
    }

    private static string GetDiskSerial()
    {
        string serial = "DISK-UNKNOWN";
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_PhysicalMedia");
            foreach (var obj in searcher.Get())
            {
                string s = obj["SerialNumber"]?.ToString()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(s))
                {
                    serial = s;
                    break;
                }
            }
        }
        catch { }
        return serial;
    }

    private static string ComputeHash(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input + "SALT_BalanzaQ_2024"));
        return Convert.ToHexString(bytes);
    }

    public static string EncryptLicense(string fingerprint, DateTime expiryDate)
    {
        using Aes aes = Aes.Create();
        aes.Key = Key;
        aes.IV = IV;

        ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

        using MemoryStream ms = new MemoryStream();
        using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            using (StreamWriter sw = new StreamWriter(cs))
            {
                // Formato: FINGERPRINT|YYYY-MM-DD
                sw.Write($"{fingerprint}|{expiryDate:yyyy-MM-dd}");
            }
        }
        return Convert.ToBase64String(ms.ToArray());
    }

    public static string? DecryptLicense(string cipherText)
    {
        try
        {
            using Aes aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;

            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            using MemoryStream ms = new MemoryStream(Convert.FromBase64String(cipherText));
            using CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using StreamReader sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }
        catch
        {
            return null;
        }
    }
}
