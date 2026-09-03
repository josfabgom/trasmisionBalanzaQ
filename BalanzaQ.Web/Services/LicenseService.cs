using BalanzaQ.Web.Security;
using System.IO;

namespace BalanzaQ.Web.Services;

public class LicenseService
{
    private readonly string _licensePath;
    private bool? _cachedIsValid;
    private DateTime? _cachedExpiryDate;
    private long _lastFileSize = -1;
    private DateTime _lastFileWriteTime = DateTime.MinValue;

    public LicenseService()
    {
        _licensePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.lic");
    }

    public DateTime? ExpiryDate
    {
        get
        {
            if (!_cachedIsValid.HasValue)
            {
                IsLicensed();
            }
            return _cachedExpiryDate;
        }
    }

    public bool IsLicensed()
    {
        try
        {
            if (!File.Exists(_licensePath))
            {
                _cachedIsValid = false;
                _cachedExpiryDate = null;
                return false;
            }

            var fileInfo = new FileInfo(_licensePath);
            if (_cachedIsValid.HasValue && fileInfo.Length == _lastFileSize && fileInfo.LastWriteTime == _lastFileWriteTime)
            {
                if (_cachedIsValid.Value && _cachedExpiryDate.HasValue)
                {
                    if (DateTime.Now > _cachedExpiryDate.Value)
                    {
                        _cachedIsValid = false;
                        return false;
                    }
                    return true;
                }
                return _cachedIsValid.Value;
            }

            _lastFileSize = fileInfo.Length;
            _lastFileWriteTime = fileInfo.LastWriteTime;

            string encryptedLicense = File.ReadAllText(_licensePath).Trim();
            string? decrypted = SecurityUtils.DecryptLicense(encryptedLicense);

            if (string.IsNullOrEmpty(decrypted))
            {
                _cachedIsValid = false;
                _cachedExpiryDate = null;
                return false;
            }

            var parts = decrypted.Split('|');
            if (parts.Length != 2)
            {
                _cachedIsValid = false;
                _cachedExpiryDate = null;
                return false;
            }

            string licenseFingerprint = parts[0];
            string expiryDateStr = parts[1];

            string currentFingerprint = GetMachineUID();

            if (licenseFingerprint != currentFingerprint)
            {
                _cachedIsValid = false;
                _cachedExpiryDate = null;
                return false;
            }

            if (DateTime.TryParse(expiryDateStr, out DateTime expiryDate))
            {
                _cachedExpiryDate = expiryDate;
                if (DateTime.Now > expiryDate)
                {
                    _cachedIsValid = false;
                    return false;
                }

                _cachedIsValid = true;
                return true;
            }

            _cachedIsValid = false;
            _cachedExpiryDate = null;
            return false;
        }
        catch
        {
            _cachedIsValid = false;
            _cachedExpiryDate = null;
            return false;
        }
    }

    public string GetMachineUID()
    {
        return SecurityUtils.GetMachineFingerprint();
    }
}
