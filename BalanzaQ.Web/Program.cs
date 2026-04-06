using BalanzaQ.Web.Components;
using BalanzaQ.Web.Data;
using BalanzaQ.Web.Services;
using Microsoft.EntityFrameworkCore;

try {
    var builder = WebApplication.CreateBuilder(args);

    // Add services to the container.
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddDbContext<BalanzaDbContext>(options =>
        options.UseSqlite("Data Source=balanzas.db"));

    builder.Services.AddScoped<DigiService>();
    builder.Services.AddScoped<ImportService>();

    var app = builder.Build();

    // Ensure Database is created
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<BalanzaDbContext>();
        db.Database.EnsureCreated();
        
        // Parches manuales para añadir columnas si no existen
        try {
            db.Database.ExecuteSqlRaw("ALTER TABLE PluItems ADD COLUMN RawType INTEGER NOT NULL DEFAULT 0;");
        } catch { }

        try {
            db.Database.ExecuteSqlRaw("ALTER TABLE PluItems ADD COLUMN LastSyncStatus TEXT;");
            db.Database.ExecuteSqlRaw("ALTER TABLE PluItems ADD COLUMN LastSyncError TEXT;");
            db.Database.ExecuteSqlRaw("ALTER TABLE PluItems ADD COLUMN LastSyncDate TEXT;");
        } catch { }
        try {
            db.Database.ExecuteSqlRaw("ALTER TABLE PluItems ADD COLUMN BarcodeFormat INTEGER NOT NULL DEFAULT 0;");
            db.Database.ExecuteSqlRaw("ALTER TABLE PluItems ADD COLUMN LabelFormat INTEGER NOT NULL DEFAULT 0;");
        } catch { }
    }

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseAntiforgery();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    app.Run();
} catch (Exception ex) {
    File.WriteAllText("fatal_error.txt", ex.ToString());
    throw;
}
