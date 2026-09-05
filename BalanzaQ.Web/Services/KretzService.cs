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

            string kretzFolder = Path.Combine(_baseDir, "Jdate");
            if (!Directory.Exists(kretzFolder))
            {
                Directory.CreateDirectory(kretzFolder);
            }

            // Generar archivo COM.JDG
            string comContent = $"\"01\",\"C\",\"3\",\"TCP\",\"{balanza.IpAddress}\",\"1001\"";
            string comFilePath = Path.Combine(kretzFolder, "COM.JDG");
            await File.WriteAllTextAsync(comFilePath, comContent, Encoding.ASCII);

            // Generar archivo INFO.JDG
            var infoBuilder = new StringBuilder();
            infoBuilder.AppendLine("C0110702012012"); // Encabezado fijo de JDataGate

            // Leer configuración global de longitud de código (4 o 5)
            int barcodeCodeLength = 5;
            var lenSetting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == "BarcodeItemCodeLength");
            if (lenSetting != null && int.TryParse(lenSetting.Value, out int parsedLen))
            {
                barcodeCodeLength = parsedLen;
            }

            int index = 0;
            foreach (var item in items)
            {
                index++;
                
                // Formato de registro de artículo (142 caracteres por línea)
                // [0-3] Comando y Equipo: C01
                // [3-4] Acción: 2
                // [4-7] Formato/Dato extra: 005
                // [7-13] Número de PLU: 6 dígitos
                // [13-16] Sección/Grupo: 3 dígitos
                // [16-19] Departamento: 3 dígitos
                // [19-71] Nombre del Artículo: 52 caracteres (rellenados con espacios)
                // [71-76] Código de Artículo: 5 dígitos
                // [76-77] Tipo: P o N
                // [77-89] Precio: 12 dígitos
                // [89-142] Resto (Días de vencimiento, ceros extra)

                string pluNum = item.PluCode.ToString().PadLeft(6, '0');
                string group = (item.Group > 0 ? (item.Group % 1000) : 1).ToString().PadLeft(3, '0');
                string dept = (item.Section > 0 ? (item.Section % 1000) : 1).ToString().PadLeft(3, '0');

                string nameToUse = !string.IsNullOrWhiteSpace(item.ShortName) ? item.ShortName : item.Name;
                nameToUse = nameToUse.Replace(";", " ").Replace("\"", " ").Trim();
                if (nameToUse.Length > 52) nameToUse = nameToUse.Substring(0, 52);
                nameToUse = nameToUse.PadRight(52, ' ');

                int divisorLength = (int)Math.Pow(10, barcodeCodeLength);
                int codPlu = item.PluCode >= divisorLength ? item.PluCode % divisorLength : item.PluCode;
                string itemCodeStr = codPlu.ToString().PadLeft(5, '0');

                string typeStr = item.ItemType == "P" ? "P" : "N";

                // Precio: multiplicado por 100 para remover punto decimal, rellenado a 12 caracteres. (ej. 24.12 -> 000000002412)
                long precioInt = (long)Math.Round(item.Price * 100);
                string precioStr = precioInt.ToString().PadLeft(12, '0');

                // Vencimiento (ej. 15 días -> 0150000) en el bloque final
                string vencimientoStr = Math.Min(item.ShelfLife, 999).ToString().PadLeft(3, '0') + "0000";
                
                // padding final
                string paddingFinal = "00000000000000000000000000000000000010000000000" + vencimientoStr;
                if (paddingFinal.Length > 53) paddingFinal = paddingFinal.Substring(paddingFinal.Length - 53);

                string recordLine = $"C012005{pluNum}{group}{dept}{nameToUse}{itemCodeStr}{typeStr}{precioStr}{paddingFinal}";
                infoBuilder.AppendLine(recordLine);
                
                onProgress?.Invoke(index, items.Count);
            }

            string infoContent = infoBuilder.ToString();
            string infoFilePath = Path.Combine(kretzFolder, "INFO.JDG");
            await File.WriteAllTextAsync(infoFilePath, infoContent, Encoding.ASCII);

            if (enviarABalanza)
            {
                // Disparar JDataGate Automáticamente
                // Se asume que JDataGate.exe está en la misma carpeta Jdate, de lo contrario esto puede fallar pero los archivos quedarán listos para polling.
                string jDataGateExe = Path.Combine(kretzFolder, "JDataGate.exe");
                if (File.Exists(jDataGateExe))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = jDataGateExe,
                        WorkingDirectory = kretzFolder,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    try
                    {
                        using var process = Process.Start(psi);
                        // No esperamos a que termine para no bloquear, o si es rapido, await process.WaitForExitAsync();
                        // await process.WaitForExitAsync(new TimeSpan(0, 0, 10)); // max 10 seg
                    }
                    catch { /* ignorar errores de ejecución, los archivos JDG ya se crearon */ }
                }

                foreach (var item in items)
                {
                    item.LastSyncDate = DateTime.Now;
                    item.LastSyncStatus = "Exitoso";
                    item.LastSyncError = "(JDG Generado)";

                    _db.SyncLogs.Add(new SyncLog
                    {
                        BalanzaIp = balanza.IpAddress,
                        PluCode = item.PluCode,
                        ProductName = item.Name,
                        Status = "Exitoso",
                        ErrorMessage = "(Archivos JDG listos para JDataGate)",
                        BatchId = batchId,
                        Date = DateTime.Now
                    });
                    
                    await AppendToLogAsync(balanza, item);
                }

                await _db.SaveChangesAsync();
            }

            return ($"Archivos JDG generados en Carpeta Jdate y enviados a JDataGate", infoContent);
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

            string line = $"[{DateTime.Now:HH:mm:ss}] PLU:{item.PluCode} - {item.Name} - Exitoso - JDG Generado";
            await File.AppendAllLinesAsync(logPath, new[] { line });
        }
        catch { /* Ignorar */ }
    }
}
