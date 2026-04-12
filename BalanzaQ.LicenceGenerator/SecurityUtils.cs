using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace BalanzaQ.LicenceGenerator;

public static class SecurityUtils
{
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("B4l4nz4Q_S3cur1ty_K3y_2024_04_!#"); // 32 bytes
    private static readonly byte[] IV = Encoding.UTF8.GetBytes("B4l4nz4Q_IV_2024"); // 16 bytes

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
                sw.Write($"{fingerprint}|{expiryDate:yyyy-MM-dd}");
            }
        }
        return Convert.ToBase64String(ms.ToArray());
    }
}
