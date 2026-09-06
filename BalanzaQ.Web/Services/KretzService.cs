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

            // Leer configuración global de longitud de precio Kretz (5, 6 o 7)
            int kretzPriceDigits = 6; // Default to 6 (Report Nx LCD)
            var priceLenSetting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == "KretzPriceDigits");
            if (priceLenSetting != null && int.TryParse(priceLenSetting.Value, out int parsedPriceLen))
            {
                kretzPriceDigits = parsedPriceLen;
            }

            int index = 0;
            foreach (var item in items)
            {
                index++;
                
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

                // 1. Valor Fijo (7 dígitos): Siempre 0
                string valorFijo = "0000000";

                // 2. Precio (dinámico): multiplicado por 100 y rellenado con 0 a la izquierda
                long precioInt = (long)Math.Round(item.Price * 100);
                string precioStr = precioInt.ToString().PadLeft(kretzPriceDigits, '0');
                if (precioStr.Length > kretzPriceDigits) precioStr = precioStr.Substring(precioStr.Length - kretzPriceDigits); // Truncar si excede

                // 3. Precios alternativos/anteriores e impuestos/taras.
                // Precios ocupan 'kretzPriceDigits' cada uno. Son 2 extra: Alternativo y Anterior.
                // Impuestos y taras suman 22 ceros invariables: Imp1(6) + Imp2(6) + Tara1(5) + Tara2(5).
                int paddingLen = (2 * kretzPriceDigits) + 22;
                string paddingVacios = new string('0', paddingLen);
                
                // Código Etiqueta (2 dígitos)
                int labelFormat = item.LabelFormat > 0 ? (item.LabelFormat % 100) : 1;
                string codEtiqueta = labelFormat.ToString().PadLeft(2, '0');

                // Receta y Nutricional (8 dígitos) + Fecha envase (1 dígito) = 9
                string paddingExtra = new string('0', 9);

                // Vencimiento (3 dígitos)
                string vencimientoStr = Math.Min(item.ShelfLife, 999).ToString().PadLeft(3, '0');

                // Código Imagen (4 dígitos)
                string codImagen = "0000";

                string recordLine = $"C012005{pluNum}{group}{dept}{nameToUse}{itemCodeStr}{typeStr}{valorFijo}{precioStr}{paddingVacios}{codEtiqueta}{paddingExtra}{vencimientoStr}{codImagen}";
                infoBuilder.AppendLine(recordLine);
                
                onProgress?.Invoke(index, items.Count);
            }

            string infoContent = infoBuilder.ToString();
            string infoFilePath = Path.Combine(kretzFolder, "INFO.JDG");
            await File.WriteAllTextAsync(infoFilePath, infoContent, Encoding.ASCII);

            if (enviarABalanza)
            {
                string errorMessageGeneral = "(Archivos JDG listos)";
                bool hasErrors = false;

                // Disparar DataGate Automáticamente
                // Se asume que DataGate.exe está en la misma carpeta Jdate
                string dataGateExe = Path.Combine(kretzFolder, "DataGate.exe");
                if (File.Exists(dataGateExe))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = dataGateExe,
                        Arguments = "/nografico tx01", // /nografico para no abrir Java Swing, tx01 para forzar la IP/ID 01
                        WorkingDirectory = kretzFolder,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    try
                    {
                        using var process = Process.Start(psi);
                        if (process != null)
                        {
                            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                            try
                            {
                                await process.WaitForExitAsync(cts.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                process.Kill();
                                errorMessageGeneral = "Cancelado por tiempo de espera excedido (>60s).";
                                hasErrors = true;
                            }
                        }

                        // Leer archivo de log si existe
                        string logJdgPath = Path.Combine(kretzFolder, "LOG.JDG");
                        if (File.Exists(logJdgPath) && !hasErrors)
                        {
                            string logJdgContent = await File.ReadAllTextAsync(logJdgPath);
                            
                            if (logJdgContent.Contains("10")) { errorMessageGeneral = "Error 10: Checksum incorrecto recibido por equipo Kretz."; hasErrors = true; }
                            else if (logJdgContent.Contains("11")) { errorMessageGeneral = "Error 11: Modelo de datos (cantidad incorrecta de bytes)."; hasErrors = true; }
                            else if (logJdgContent.Contains("20")) { errorMessageGeneral = "Error 20: Registro Inexistente."; hasErrors = true; }
                            else if (logJdgContent.Contains("50")) { errorMessageGeneral = "Error 50: Capacidad Máxima Superada (Tabla completa)."; hasErrors = true; }
                            else if (logJdgContent.Contains("60")) { errorMessageGeneral = "Error 60: Falló la ejecución del comando en el equipo Kretz."; hasErrors = true; }
                            else if (logJdgContent.Contains("01") || string.IsNullOrWhiteSpace(logJdgContent)) 
                            {
                                errorMessageGeneral = "Transmisión Exitosa confirmada (DataGate/Kretz).";
                            }
                            else
                            {
                                errorMessageGeneral = $"DataGate Log: {logJdgContent.Substring(0, Math.Min(logJdgContent.Length, 80))}";
                                hasErrors = true;
                            }

                            try { File.Delete(logJdgPath); } catch { /* Ignorar si no se puede borrar */ }
                        }
                    }
                    catch { errorMessageGeneral = "(DataGate.exe falló o no pudo iniciarse)"; hasErrors = true; }
                }

                foreach (var item in items)
                {
                    item.LastSyncDate = DateTime.Now;
                    item.LastSyncStatus = hasErrors ? "Error" : "Exitoso";
                    item.LastSyncError = errorMessageGeneral;

                    _db.SyncLogs.Add(new SyncLog
                    {
                        BalanzaIp = balanza.IpAddress,
                        PluCode = item.PluCode,
                        ProductName = item.Name,
                        Status = hasErrors ? "Error" : "Exitoso",
                        ErrorMessage = errorMessageGeneral,
                        BatchId = batchId,
                        Date = DateTime.Now
                    });
                    
                    await AppendToLogAsync(balanza, item, errorMessageGeneral);
                }

                await _db.SaveChangesAsync();

                if (hasErrors) return (errorMessageGeneral, infoContent);
            }

            return ($"Archivos JDG enviados y confirmados", infoContent);
        }
        catch (Exception ex)
        {
            return ($"EXCEPTION: {ex.Message}", "");
        }
    }

    private async Task AppendToLogAsync(Balanza balanza, PluItem item, string message)
    {
        try
        {
            string logsDir = Path.Combine(_baseDir, "logs");
            if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);

            string fileName = $"sync_kretz_{balanza.IpAddress}_{DateTime.Now:yyyyMMdd}.log";
            string logPath = Path.Combine(logsDir, fileName);

            string line = $"[{DateTime.Now:HH:mm:ss}] PLU:{item.PluCode} - {item.Name} - {message}";
            await File.AppendAllLinesAsync(logPath, new[] { line });
        }
        catch { /* Ignorar */ }
    }
}
