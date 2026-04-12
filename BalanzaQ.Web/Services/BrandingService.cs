using BalanzaQ.Web.Data;
using Microsoft.EntityFrameworkCore;
using BalanzaQ.Web.Models;

namespace BalanzaQ.Web.Services;

public class BrandingService
{
    private readonly IServiceScopeFactory _scopeFactory;
    
    public string BusinessName { get; private set; } = "BalanzaQ";
    public string BusinessAddress { get; private set; } = "";
    public string BusinessEmail { get; private set; } = "";
    public string BusinessPhone { get; private set; } = "";
    public string BusinessLogoBase64 { get; private set; } = "";

    public event Action? OnBrandingChanged;

    public BrandingService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        // La carga inicial se disparará de forma asíncrona
        Task.Run(() => RefreshAsync());
    }

    public async Task RefreshAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BalanzaDbContext>();

        var settings = await dbContext.AppSettings.ToListAsync();

        BusinessName = settings.FirstOrDefault(s => s.Key == "BusinessName")?.Value ?? "BalanzaQ";
        BusinessAddress = settings.FirstOrDefault(s => s.Key == "BusinessAddress")?.Value ?? "";
        BusinessEmail = settings.FirstOrDefault(s => s.Key == "BusinessEmail")?.Value ?? "";
        BusinessPhone = settings.FirstOrDefault(s => s.Key == "BusinessPhone")?.Value ?? "";
        BusinessLogoBase64 = settings.FirstOrDefault(s => s.Key == "BusinessLogo")?.Value ?? "";

        OnBrandingChanged?.Invoke();
    }
}
