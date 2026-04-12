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
    builder.Services.AddSingleton<BrandingService>();
    builder.Services.AddSingleton<LicenseService>();

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
        try {
            db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS SyncLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Date TEXT NOT NULL,
                BalanzaIp TEXT,
                PluCode INTEGER NOT NULL,
                ProductName TEXT,
                Status TEXT,
                ErrorMessage TEXT,
                BatchId TEXT
            );");
        } catch { }
        try {
            db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS ImportLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Date TEXT NOT NULL,
                FileName TEXT,
                TotalRecords INTEGER NOT NULL,
                ProcessedRecords INTEGER NOT NULL,
                ErrorCount INTEGER NOT NULL,
                details TEXT
            );");
        } catch { }
        try {
            db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS AppSettings (
                Key TEXT PRIMARY KEY,
                Value TEXT
            );");
            // Insertar default si no existe
            db.Database.ExecuteSqlRaw("INSERT OR IGNORE INTO AppSettings (Key, Value) VALUES ('ImportSeparator', ';');");
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

    app.MapPost("/api/shutdown", () => {
        // Ejecutar apagado de forma diferida para permitir que el cliente reciba el OK
        Task.Run(async () => {
            await Task.Delay(500);
            Environment.Exit(0);
        });
        return Results.Ok();
    });

    app.Run();

} catch (Exception ex) {
    File.WriteAllText("fatal_error.txt", ex.ToString());
    throw;
}
