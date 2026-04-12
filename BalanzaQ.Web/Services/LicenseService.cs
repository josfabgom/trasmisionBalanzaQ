using BalanzaQ.Web.Security;

namespace BalanzaQ.Web.Services;

public class LicenseService
{
    private bool? _isValid;
    private readonly string _licensePath;

    public LicenseService()
    {
        _licensePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.lic");
    }

    public DateTime? ExpiryDate { get; private set; }

    public bool IsLicensed()
    {
        if (_isValid.HasValue) return _isValid.Value;

        if (!File.Exists(_licensePath))
        {
            _isValid = false;
            return false;
        }

        try
        {
            string encryptedContent = File.ReadAllText(_licensePath);
            string? decryptedData = SecurityUtils.DecryptLicense(encryptedContent);
            
            if (string.IsNullOrEmpty(decryptedData) || !decryptedData.Contains("|"))
            {
                _isValid = false;
                return false;
            }

            var parts = decryptedData.Split('|');
            string decryptedUID = parts[0];
            string dateStr = parts[1];

            string currentMachineUID = SecurityUtils.GetMachineFingerprint();

            if (decryptedUID != currentMachineUID)
            {
                _isValid = false;
                return false;
            }

            // Validar fecha de expiración
            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime expiryDate))
            {
                ExpiryDate = expiryDate;
                _isValid = DateTime.Now.Date <= expiryDate.Date;
            }
            else
            {
                _isValid = false;
            }
        }
        catch
        {
            _isValid = false;
        }

        return _isValid.Value;
    }

    public string GetMachineUID()
    {
        return SecurityUtils.GetMachineFingerprint();
    }
}
