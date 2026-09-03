using System.IO;
using System.Text;
using BalanzaQ.Web.Models;
using BalanzaQ.Web.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace BalanzaQ.Web.Services;

public class KretzService
{
    private readonly IConfiguration _config;
    private readonly BalanzaDbContext _db;
    private readonly string _baseDir;

    public KretzService(IConfiguration config, BalanzaDbContext db)
    {
        _config = config;
        _db = db;
        
        string startDir = AppContext.BaseDirectory;
        string? current = startDir;
        string foundRoot = startDir;
        
        while (current != null)
        {
            if (File.Exists(Path.Combine(current, "BalanzaQ.sln")))
            {
                foundRoot = current;
                break;
            }
            current = Path.GetDirectoryName(current);
        }
        _baseDir = foundRoot;
    }

    public async Task<(string Message, string HexPayload)> SyncBalanzaAsync(Balanza balanza, List<PluItem> items, bool enviarABalanza = true, Action<int, int>? onProgress = null, string batchId = "")
    {
        try
        {
            if (items == null || !items.Any()) return ("No hay articulos para enviar.", "");

            string kretzFolder = Path.Combine(_baseDir, "Kretz");
            if (!Directory.Exists(kretzFolder))
            {
                Directory.CreateDirectory(kretzFolder);
            }

            // Generar archivo CSV en formato Kretz iTegra
            // Delimitador: Semicolon ';'
            // Header obligatorio (o recomendado):
            // NUMERO DE PLU;CODIGO DE PLU;NOMBRE DE PLU;CODIGO DE DEPARTAMENTO;PRECIO;TIPO DE PLU;CODIGO DE ETIQUETA
            
            // Leer configuración global de longitud de código (4 o 5)
            int barcodeCodeLength = 5;
            var lenSetting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == "BarcodeItemCodeLength");
            if (lenSetting != null && int.TryParse(lenSetting.Value, out int parsedLen))
            {
                barcodeCodeLength = parsedLen;
            }

            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("NUMERO DE PLU;CODIGO DE PLU;NOMBRE DE PLU;CODIGO DE DEPARTAMENTO;PRECIO;TIPO DE PLU;CODIGO DE ETIQUETA");

            int index = 0;
            foreach (var item in items)
            {
                index++;
                // 1. NUMERO DE PLU (max 6 digitos, usamos PluCode)
                int numPlu = item.PluCode;
                
                // 2. CODIGO DE PLU (max 5 digitos para balanza standard, o usar PluCode)
                // Adaptamos el módulo a la longitud requerida (ej. 10000 para 4 dígitos, 100000 para 5 dígitos)
                int divisorLength = (int)Math.Pow(10, barcodeCodeLength);
                int codPlu = item.PluCode >= divisorLength ? item.PluCode % divisorLength : item.PluCode;
                
                // 3. NOMBRE DE PLU (max 26 caracteres en iTegra Kretz)
                string nameToUse = !string.IsNullOrWhiteSpace(item.ShortName) ? item.ShortName : item.Name;
                nameToUse = nameToUse.Replace(";", " ").Replace("\"", " ").Trim(); // Sanitizar punto y coma
                if (nameToUse.Length > 26) nameToUse = nameToUse.Substring(0, 26);
                
                // 4. CODIGO DE DEPARTAMENTO (max 3 digitos, usamos Section o Group o default 1)
                int dept = item.Section > 0 ? (item.Section % 1000) : (item.Group > 0 ? (item.Group % 1000) : 1);
                
                // 5. PRECIO (formato decimal ej. 123.45. Usamos Invariant para asegurar el punto decimal o coma)
                string precioStr = item.Price.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                
                // 6. TIPO DE PLU (N no pesable, P pesable)
                string tipoPlu = item.ItemType == "P" ? "P" : "N";
                
                // 7. CODIGO DE ETIQUETA (max 2 digitos, usamos LabelFormat o default 21)
                int labelFormat = item.LabelFormat > 0 ? (item.LabelFormat % 100) : 21;

                csvBuilder.AppendLine($"{numPlu};{codPlu};{nameToUse};{dept};{precioStr};{tipoPlu};{labelFormat}");
                
                onProgress?.Invoke(index, items.Count);
            }

            string csvContent = csvBuilder.ToString();

            // Guardar físicamente en la carpeta Kretz
            string destFileName = $"Kretz_PLU_{balanza.IpAddress}.csv";
            string csvFilePath = Path.Combine(kretzFolder, destFileName);

            if (File.Exists(csvFilePath))
            {
                File.Delete(csvFilePath);
            }
            await File.WriteAllTextAsync(csvFilePath, csvContent, Encoding.UTF8);

            // Loguear en base de datos para cada artículo sincronizado si es en modo real
            if (enviarABalanza)
            {
                foreach (var item in items)
                {
                    item.LastSyncDate = DateTime.Now;
                    item.LastSyncStatus = "Exitoso";
                    item.LastSyncError = "(iTegra CSV listo)";

                    _db.SyncLogs.Add(new SyncLog
                    {
                        BalanzaIp = balanza.IpAddress,
                        PluCode = item.PluCode,
                        ProductName = item.Name,
                        Status = "Exitoso",
                        ErrorMessage = "(iTegra CSV listo en carpeta Kretz)",
                        BatchId = batchId,
                        Date = DateTime.Now
                    });
                    
                    await AppendToLogAsync(balanza, item);
                }

                await _db.SaveChangesAsync();
            }

            return ($"Archivo CSV generado en Carpeta Kretz: {destFileName}", csvContent);
        }
        catch (Exception ex)
        {
            return ($"EXCEPTION: {ex.Message}", "");
        }
    }

    private async Task AppendToLogAsync(Balanza balanza, PluItem item)
    {
        try
        {
            string logsDir = Path.Combine(_baseDir, "logs");
            if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);

            string fileName = $"sync_kretz_{balanza.IpAddress}_{DateTime.Now:yyyyMMdd}.log";
            string logPath = Path.Combine(logsDir, fileName);

            string line = $"[{DateTime.Now:HH:mm:ss}] PLU:{item.PluCode} - {item.Name} - Exitoso - CSV Generado";
            await File.AppendAllLinesAsync(logPath, new[] { line });
        }
        catch { /* Ignorar */ }
    }
}
