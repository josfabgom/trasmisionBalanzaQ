using BalanzaQ.Web.Models;
using BalanzaQ.Web.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BalanzaQ.Web.Services;

public class ImportService
{
    private readonly IServiceProvider _serviceProvider;

    public ImportService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<int> ImportarDatosAsync(Stream fileStream, Action<int, int>? onProgress = null, string fileName = "data.dat")
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BalanzaDbContext>();

        using var reader = new StreamReader(fileStream);
        string content = await reader.ReadToEndAsync();
        var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
                           .Where(l => !string.IsNullOrWhiteSpace(l))
                           .ToList();
        
        // Cargar separador desde configuración o usar default ';'
        string separator = (await context.AppSettings.FirstOrDefaultAsync(s => s.Key == "ImportSeparator"))?.Value ?? ";";
        if (string.IsNullOrEmpty(separator)) separator = ";";

        int total = lines.Count;
        int importados = 0;
        int errores = 0;

        for (int i = 0; i < total; i++)
        {
            try
            {
                var linea = lines[i];
                var parts = linea.Split(separator);
                if (parts.Length < 14) 
                {
                    errores++;
                    continue;
                }

                if (int.TryParse(parts[0], out int pluId))
                {
                    var existingPlu = await context.PluItems.FirstOrDefaultAsync(p => p.PluCode == pluId);
                    bool isNew = existingPlu == null;
                    
                    var plu = existingPlu ?? new PluItem { PluCode = pluId };

                    plu.ShortName = parts[2];
                    plu.Name = parts[3];
                    
                    if(decimal.TryParse(parts[6], NumberStyles.Any, CultureInfo.InvariantCulture, out decimal precio))
                    {
                        plu.Price = precio;
                    }

                    if (int.TryParse(parts[4], out int rawt)) plu.RawType = rawt;
                    if (int.TryParse(parts[5], out int grupo)) plu.Group = grupo;
                    if (int.TryParse(parts[13], out int vidaUtil)) plu.ShelfLife = vidaUtil;
                    if (int.TryParse(parts[14], out int seccion)) plu.Section = seccion;
                    
                    plu.ItemType = parts[12]?.Trim() ?? "P"; // 'P' o 'N'

                    if (isNew)
                    {
                        context.PluItems.Add(plu);
                    }
                    importados++;
                }
                else
                {
                    errores++;
                }
            }
            catch
            {
                errores++;
            }

            if (i % 50 == 0 || i == total - 1)
            {
                onProgress?.Invoke(i + 1, total);
            }
        }

        // Crear registro de auditoría
        context.ImportLogs.Add(new ImportLog
        {
            Date = DateTime.Now,
            FileName = fileName,
            TotalRecords = total,
            ProcessedRecords = importados,
            ErrorCount = errores,
            details = $"Importación finalizada. Total: {total}, Exitosos: {importados}, Errores: {errores}."
        });

        await context.SaveChangesAsync();
        return importados;
    }
}
