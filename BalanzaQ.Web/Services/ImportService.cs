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

    public async Task<int> ImportarDatosAsync(Stream fileStream)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BalanzaDbContext>();

        using var reader = new StreamReader(fileStream);
        string content = await reader.ReadToEndAsync();
        var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        int importados = 0;

        foreach (var linea in lines)
        {
            if (string.IsNullOrWhiteSpace(linea)) continue;
            var parts = linea.Split(';');
            if (parts.Length < 14) continue;

            if (int.TryParse(parts[0], out int pluId))
            {
                var existingPlu = await context.PluItems.FirstOrDefaultAsync(p => p.PluCode == pluId);
                bool isNew = existingPlu == null;
                
                var plu = existingPlu ?? new PluItem { PluCode = pluId };

                plu.ShortName = parts[2];
                plu.Name = parts[3];
                // Intentar parseo seguro a C# Culture Invariant o especifico. El precio "14099.00"
                if(decimal.TryParse(parts[6], NumberStyles.Any, CultureInfo.InvariantCulture, out decimal precio))
                {
                    plu.Price = precio;
                }

                if (int.TryParse(parts[4], out int rawt)) plu.RawType = rawt;
                if (int.TryParse(parts[5], out int grupo)) plu.Group = grupo;
                if (int.TryParse(parts[13], out int seccion)) plu.Section = seccion;
                
                plu.ItemType = parts[12]?.Trim(); // 'P' o 'N'
                if(int.TryParse(parts[14], out int vidaUtil)) plu.ShelfLife = vidaUtil;

                if (isNew)
                {
                    context.PluItems.Add(plu);
                }
                importados++;
            }
        }

        await context.SaveChangesAsync();
        return importados;
    }
}
