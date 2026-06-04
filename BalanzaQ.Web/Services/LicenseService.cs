using BalanzaQ.Web.Security;

namespace BalanzaQ.Web.Services;

public class LicenseService
{
    private bool? _isValid;
    private readonly string _licensePath;

    public LicenseService()
    {
        _licensePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license.lic");
        ExpiryDate = DateTime.Now.AddYears(10);
    }

    public DateTime? ExpiryDate { get; private set; }

    public bool IsLicensed()
    {
        return true;
    }

    public string GetMachineUID()
    {
        return SecurityUtils.GetMachineFingerprint();
    }
}
