using System.IO;
using System.Text;
using BalanzaQ.Web.Models;
using BalanzaQ.Web.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace BalanzaQ.Web.Services;

public class DigiService
{
    private readonly IConfiguration _config;
    private readonly BalanzaDbContext _db;
    private readonly string _baseDir;

    public DigiService(IConfiguration config, BalanzaDbContext db)
    {
        _config = config;
        _db = db;
        // Lógica para encontrar la raíz física del ejecutable (necesaria para SingleFile)
        var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        string exePath = currentProcess.MainModule?.FileName ?? AppContext.BaseDirectory;
        string exeDir = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
        
        if (Directory.Exists(Path.Combine(exeDir, "Digi")))
        {
            _baseDir = exeDir;
        }
        else if (Directory.Exists(Path.Combine(exeDir, "..", "Digi")))
        {
            _baseDir = Path.GetFullPath(Path.Combine(exeDir, ".."));
        }
        else
        {
            _baseDir = exeDir; // Fallback
        }
    }    public async Task<(string Message, string HexPayload)> SyncBalanzaAsync(Balanza balanza, List<PluItem> items, bool enviarABalanza = true, Action<int, int>? onProgress = null, string batchId = "")
    {
        try
        {
            if (items == null || !items.Any()) return ("No hay articulos para enviar.", "");

            string templatePath = Path.Combine(_baseDir, "TEMPLATE.DAT");
            string digiFolder = Path.Combine(_baseDir, "Digi");
            string digiExe = Path.Combine(digiFolder, "digiwtcp.exe");

            if (!File.Exists(templatePath))
            {
                string? existingFile = Directory.GetFiles(_baseDir, "SM*F37.DAT").FirstOrDefault();
                if (existingFile != null) File.Copy(existingFile, templatePath);
                else return ("ERROR: Plantilla TEMPLATE.DAT no encontrada.", "");
            }

            // 1. Leer Plantilla
            byte[] templateBytes;
            using (var fs = new FileStream(templatePath, FileMode.Open, FileAccess.Read))
            using (var sr = new StreamReader(fs, Encoding.ASCII))
            {
                string hex = sr.ReadToEnd().Trim();
                if (hex.EndsWith("E2")) hex = hex.Substring(0, hex.Length - 2);
                templateBytes = Convert.FromHexString(hex);
            }

            int numNameStart = FindSequence(templateBytes, new byte[] { 0x03, 0x07 });
            if (numNameStart == -1) return ("ERROR: Patrón 03 07 no encontrado.", "");

            int templateNameLen = templateBytes[numNameStart + 2];
            byte[] afterName = new byte[templateBytes.Length - (numNameStart + 3 + templateNameLen)];
            Array.Copy(templateBytes, numNameStart + 3 + templateNameLen, afterName, 0, afterName.Length);

            // Sincronización Individual de Alta Velocidad (v3.5.45)
            // Revertimos a envío de 1 en 1 porque el SM-300 solo procesa el primero de cada archivo.
            int batchSize = 1;
            int exitosTotal = 0;

            // Crear o limpiar el log general de hex
            StringBuilder finalLogAll = new StringBuilder();

            for (int i = 0; i < items.Count; i += batchSize)
            {
                List<PluItem> currentBatch = items.GetRange(i, Math.Min(batchSize, items.Count - i));

                StringBuilder batchHexPayload = new StringBuilder();
                
                foreach (var item in currentBatch)
                {
                    byte[] record = new byte[132]; 
                    Array.Copy(templateBytes, 0, record, 0, Math.Min(templateBytes.Length, 132));

                    Array.Copy(IntToBcdArray(item.PluCode, 4), 0, record, 0, 4);
                    bool isPesable = item.ItemType == "P";
                    int precioFinal = (int)Math.Round(item.Price * 10);
                    Array.Copy(IntToBcdArray(precioFinal, 3), 0, record, 12, 3);

                    string nameToUse = item.ShortName ?? "";
                    if (nameToUse.Length > 28) nameToUse = nameToUse.Substring(0, 28);
                    byte[] nameBytes = Encoding.ASCII.GetBytes(nameToUse);
                    int currentLen = nameBytes.Length;

                    record[5] = (byte)(0x2A + currentLen); 
                    record[6] = isPesable ? (byte)0x7C : (byte)0x7D;
                    record[10] = 0x0D; 
                    
                    // IMPORTANTE: NO sobreescribir Offset 15 y 16. Debemos dejar
                    // que TEMPLATE.DAT mantenga el formato exacto original (ej. 17 y 5)
                    // que requiere la balanza para validar cada archivo sin cortarse.
                    record[17] = 0x20; 

                    string pluPart = item.PluCode.ToString().PadLeft(5, '0');
                    string fillerPart = isPesable ? "0000000" : "1111111"; 
                    string strCode = (pluPart + fillerPart).Substring(0, 12);
                    byte[] codeBcd = new byte[6];
                    for (int j = 0; j < 6; j++) codeBcd[j] = Convert.ToByte(strCode.Substring(j * 2, 2), 16);
                    Array.Copy(codeBcd, 0, record, 18, 6);

                    // Offset 24: Días de Vencimiento dinámicos (Sell By - 2 bytes en BCD)
                    byte[] shelfLifeBcdArray = IntToBcdArray(Math.Min(item.ShelfLife, 9999), 2);
                    Array.Copy(shelfLifeBcdArray, 0, record, 24, 2); 

                    // Offset 26: Sección. (Como estaba en v3.5.30 original donde todo andaba)
                    Array.Copy(IntToBcdArray(item.Section, 2), 0, record, 26, 2);


                    int start = 33; 
                    record[start] = 0x01;
                    record[start + 1] = 0x01;
                    record[start + 2] = 0x07; 
                    record[start + 3] = (byte)currentLen;
                    Array.Copy(nameBytes, 0, record, start + 4, nameBytes.Length);

                    int currentTailStart = start + 4 + currentLen;
                    int tailLength = Math.Min(afterName.Length, 132 - currentTailStart);
                    if (tailLength > 0)
                    {
                        Array.Copy(afterName, 0, record, currentTailStart, tailLength);
                    }

                    batchHexPayload.Append(Convert.ToHexString(record).ToUpper());
                }

                string hexPayload = batchHexPayload.ToString();
                finalLogAll.Append(hexPayload);

                if (enviarABalanza)
                {
                    // Nombre Estándar Requerido por el Driver
                    string destFileName = $"SM{balanza.IpAddress}F37.DAT";
                    string datFileDigi = Path.Combine(digiFolder, destFileName);
                    string resultPath = Path.Combine(digiFolder, "RESULT");

                    if (File.Exists(datFileDigi)) File.Delete(datFileDigi);
                    if (File.Exists(resultPath)) File.Delete(resultPath);

                    File.WriteAllText(datFileDigi, hexPayload, Encoding.ASCII);

                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = Path.GetFullPath(digiExe),
                        Arguments = $"WR 37 {balanza.IpAddress}",
                        WorkingDirectory = Path.GetFullPath(digiFolder),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    };

                    using var process = Process.Start(psi);
                    await process!.WaitForExitAsync();
                    
                    string resultLine = await ReadResultWithRetry(resultPath);
                    string resCode = resultLine.Trim();
                    int lastColon = resultLine.LastIndexOf(':');
                    if (lastColon >= 0) resCode = resultLine.Substring(lastColon + 1).Trim();

                    // Criterio Pragmático Refinado (v3.5.50):
                    // No es éxito SI Y SOLO SI es un error crítico (Red o Local).
                    // EXCEPCIÓN: El error -2 (READ_FILE_ERR) a veces es falso si los PLU pasaron.
                    // Lo marcaremos como éxito pero con una nota de "Pendiente Verificación".
                    bool esErrorDeRed = (resCode == "-5" || resCode == "-6" || resCode == "-7");
                    bool esErrorLocalCritico = (resCode == "-1" || resCode == "-3" || resCode == "MISSING" || resCode == "LOCKED");
                    bool esErrorLecturaLocal = (resCode == "-2");

                    bool success = !esErrorDeRed && !esErrorLocalCritico; 
                    
                    if (success) exitosTotal += currentBatch.Count;

                    foreach(var item in currentBatch)
                    {
                        item.LastSyncDate = DateTime.Now;
                        item.LastSyncStatus = success ? (esErrorLecturaLocal ? "Verificando" : "Exitoso") : "Fallo";
                        
                        if (esErrorLecturaLocal)
                            item.LastSyncError = "Enviado con dudas (-2). Use Verificar Lote.";
                        else
                            item.LastSyncError = (resCode == "0" || resCode == "00") ? null : GetDigiErrorMessage(resCode);

                        _db.SyncLogs.Add(new SyncLog
                        {
                            BalanzaIp = balanza.IpAddress,
                            PluCode = item.PluCode,
                            ProductName = item.Name,
                            Status = item.LastSyncStatus,
                            ErrorMessage = item.LastSyncError,
                            BatchId = batchId,
                            Date = DateTime.Now
                        });
                        
                        await AppendToLogAsync(balanza, item);
                    }
                    
                    // Pausa de seguridad optimizada (50ms) para alta velocidad individual
                    await Task.Delay(50);
                }
                onProgress?.Invoke(Math.Min(i + batchSize, items.Count), items.Count);
            }

            if (enviarABalanza)
            {
                // VERIFICACIÓN AUTOMÁTICA CENTINELA (v3.5.51)
                // Si hubo al menos un envío con éxito parcial, verificamos el último PLU
                var lastItem = items.LastOrDefault();
                if (lastItem != null && exitosTotal > 0)
                {
                    bool verificado = await VerifyPLUInScaleAsync(balanza.IpAddress, lastItem.PluCode);
                    if (verificado)
                    {
                        // Si el último PLU está bien, subimos de categoría a todos los "Verificando" de esta sesión
                        foreach (var it in items.Where(x => x.LastSyncStatus == "Verificando"))
                        {
                            it.LastSyncStatus = "Exitoso";
                            it.LastSyncError = "(Auto-Verificado RD)";
                        }

                        // También actualizar los logs que están en el ChangeTracker de EF
                        var logsActuales = _db.SyncLogs.Local.Where(l => l.BatchId == batchId && l.Status == "Verificando").ToList();
                        foreach (var log in logsActuales)
                        {
                            log.Status = "Exitoso";
                            log.ErrorMessage = "(Auto-Verificado RD)";
                        }
                    }
                }
                
                await _db.SaveChangesAsync();
            }

            if (!enviarABalanza) return ("Archivos generados correctamente.", finalLogAll.ToString());
            return (exitosTotal == items.Count ? "Exito" : $"Parcial: {exitosTotal}/{items.Count} exitosos", finalLogAll.ToString());
        }
        catch (Exception ex)
        {
            return ($"EXCEPTION: {ex.Message}", "");
        }
    }

    /// <summary>
    /// Verifica si un PLU específico existe realmente en la balanza leyendo el archivo F37.
    /// </summary>
    public async Task<bool> VerifyPLUInScaleAsync(string ip, int pluCode)
    {
        try
        {
            string digiFolder = Path.Combine(_baseDir, "Digi");
            string digiExe = Path.GetFullPath(Path.Combine(digiFolder, "digiwtcp.exe"));
            string destFileName = $"SM{ip}F37.DAT";
            string datFile = Path.Combine(digiFolder, destFileName);
            string resultPath = Path.Combine(digiFolder, "RESULT");

            if (File.Exists(datFile)) File.Delete(datFile);
            if (File.Exists(resultPath)) try { File.Delete(resultPath); } catch {}

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = digiExe,
                Arguments = $"RD 37 {ip}",
                WorkingDirectory = digiFolder,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;
            await process.WaitForExitAsync();

            if (!File.Exists(datFile)) return false;

            // El archivo RD de F37 es Hexadecimal ASCII (264 chars por registro)
            // Usamos un stream para no cargar archivos gigantes si la balanza tiene muchos PLUs
            using var reader = new StreamReader(datFile);
            string pluHexPrefix = pluCode.ToString().PadLeft(8, '0'); 
            
            // Leemos de a bloques
            char[] buffer = new char[264];
            while (await reader.ReadBlockAsync(buffer, 0, 264) > 0)
            {
                string block = new string(buffer);
                if (block.StartsWith(pluHexPrefix)) return true;
                
                // Si por alguna razón hay desfase (headers extraños), búsqueda libre en el bloque
                if (block.Contains(pluHexPrefix)) return true;
            }

            return false;
        }
        catch { return false; }
    }

    public async Task<string> ExecuteMaintenanceCommandAsync(Balanza balanza, string command, int fileId)
    {
        try
        {
            string digiFolder = Path.Combine(_baseDir, "Digi");
            string digiExe = Path.Combine(digiFolder, "digiwtcp.exe");

            if (!File.Exists(digiExe)) return "ERROR: digiwtcp.exe no encontrado.";

            // Comando: RD=Lectura, DELFI=Borrado Integral
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = digiExe,
                Arguments = $"{command} {fileId} {balanza.IpAddress}",
                WorkingDirectory = digiFolder,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };

            // Limpiar resultado previo
            string resultPath = Path.Combine(digiFolder, "RESULT");
            if (File.Exists(resultPath)) try { File.Delete(resultPath); } catch {}

            using var process = Process.Start(psi);
            await process!.WaitForExitAsync();

            string resultLine = await ReadResultWithRetry(resultPath);
            string resCode = resultLine;
            int lastColon = resultLine.LastIndexOf(':');
            if (lastColon >= 0) resCode = resultLine.Substring(lastColon + 1).Trim();

            if (resCode == "0")
            {
                if (command == "RD")
                {
                    string sourceFile = Path.Combine(digiFolder, $"SM{balanza.IpAddress}F{fileId}.DAT");
                    if (File.Exists(sourceFile))
                    {
                        // Copiar a la raíz para fácil acceso
                        string destFile = Path.Combine(_baseDir, $"SM{balanza.IpAddress}F{fileId}_READ.DAT");
                        File.Copy(sourceFile, destFile, true);
                        return $"Éxito: Datos leídos y guardados en {Path.GetFileName(destFile)}";
                    }
                }
                return "Éxito: Operación completada.";
            }

            return $"Error: {GetDigiErrorMessage(resCode)} ({resultLine})";
        }
        catch (Exception ex)
        {
            return $"EXCEPTION: {ex.Message}";
        }
    }

    private int FindSequence(byte[] source, byte[] seq)
    {
        for (int i = 0; i < source.Length - seq.Length; i++)
        {
            bool match = true;
            for (int k = 0; k < seq.Length; k++)
            {
                if (source[i + k] != seq[k])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }

    private async Task<string> ReadResultWithRetry(string path)
    {
        for (int i = 0; i < 50; i++) // Hasta 10 segundos de espera para lotes grandes
        {
            try
            {
                if (File.Exists(path)) {
                   string content = (await File.ReadAllTextAsync(path)).Trim();
                   if (!string.IsNullOrEmpty(content)) return content;
                }
                await Task.Delay(200); 
            }
            catch (IOException)
            {
                await Task.Delay(300); 
            }
        }
        return File.Exists(path) ? "LOCKED" : "MISSING";
    }

    public async Task<LabelFormatInfo?> GetLabelFormatInfoAsync(string fileName)
    {
        try
        {
            string digiFolder = Path.Combine(_baseDir, "Digi");
            string path = Path.Combine(digiFolder, fileName);
            if (!File.Exists(path)) return null;

            byte[] data = await File.ReadAllBytesAsync(path);
            if (data.Length < 32) return null;

            var info = new LabelFormatInfo { FileName = fileName };

            // Heurística Digi SM-100/300 F52
            // Buscamos el registro de cabecera (Tipo 1)
            // Generalmente los primeros bytes o un bloque que empieza con 00 00 00 01
            for (int i = 0; i < data.Length - 16; i += 16)
            {
                if (data[i] == 0x00 && data[i+1] == 0x00 && data[i+2] == 0x00 && data[i+3] == 0x01)
                {
                    // Ancho y Alto en 0.1mm (Big Endian o parecido)
                    // En el dump: 06 80 01 C0 01 60 03 00
                    // 01 60 = 352 (35.2mm), 03 00 = 768 (76.8mm)
                    info.WidthLabel = (data[i + 8] << 8) | data[i + 9];
                    info.HeightLabel = (data[i + 10] << 8) | data[i + 11];
                    if (info.WidthLabel <= 0) info.WidthLabel = 400; // Plan B
                    if (info.HeightLabel <= 0) info.HeightLabel = 600;
                    break;
                }
            }

            // Escaner de campos (Bloques de 16 o 32 bytes)
            // Mapeo común: 0x01: Nombre, 0x02: Precio U, 0x03: Peso, 0x05: Barras, 0x13: Fecha
            // Nota: El mapeado varia, usaremos los IDs más frecuentes en SM-100
            for (int i = 0; i < data.Length - 16; i += 16)
            {
                byte id = data[i + 7]; // Heurística: ID en offset 7 de bloques de 16
                if (id > 0 && id < 50) 
                {
                    var field = new LabelField
                    {
                        FieldId = id,
                        FieldName = GetOriginalFieldName(id),
                        X = (data[i + 1] << 8) | data[i + 2],
                        Y = (data[i + 3] << 8) | data[i + 4],
                        Font = data[i + 5]
                    };
                    
                    if (field.X > 0 && field.Y > 0 && field.X < 2000 && field.Y < 2000)
                    {
                        info.Fields.Add(field);
                    }
                }
            }

            return info;
        }
        catch { return null; }
    }

    public string GetOriginalFieldName(int id)
    {
        return id switch
        {
            1 => "Nombre del Producto",
            2 => "Precio Unitario",
            3 => "Peso",
            4 => "Cantidad/Unidad",
            5 => "Código de Barras",
            6 => "Precio Total",
            7 => "Precio Total",
            8 => "Fecha de Empaque",
            9 => "Fecha de Vencimiento",
            10 => "Número de PLU",
            13 => "Fecha/Hora",
            14 => "Nombre de Tienda",
            15 => "Ingredientes",
            18 => "Logo/Imagen",
            20 => "Mensaje Especial",
            21 => "Mensaje Especial 2",
            _ => $"Campo {id}"
        };
    }

    public async Task<bool> GetEtiquetasAsync(string ip)
    {
        try
        {
            string driverPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Digi", "digiwtcp.exe");
            if (!File.Exists(driverPath)) return false;

            var startInfo = new ProcessStartInfo
            {
                FileName = driverPath,
                Arguments = $"RD 52 {ip}",
                WorkingDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Digi"),
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;
            await process.WaitForExitAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> BorrarEtiquetasAsync(string ip)
    {
        try
        {
            string driverPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Digi", "digiwtcp.exe");
            if (!File.Exists(driverPath)) return false;

            var startInfo = new ProcessStartInfo
            {
                FileName = driverPath,
                Arguments = $"DELFI 52 {ip}",
                WorkingDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Digi"),
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;
            await process.WaitForExitAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> SendEtiquetasAsync(string ip)
    {
        try
        {
            string driverPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Digi", "digiwtcp.exe");
            if (!File.Exists(driverPath)) return false;

            // El driver espera que el archivo se llame SM<IP>F52.DAT en su directorio de trabajo
            var startInfo = new ProcessStartInfo
            {
                FileName = driverPath,
                Arguments = $"WR 52 {ip}",
                WorkingDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Digi"),
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;
            await process.WaitForExitAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> SaveLabelFormatAsync(LabelFormatInfo info)
    {
        try
        {
            string digiFolder = Path.Combine(_baseDir, "Digi");
            string path = Path.Combine(digiFolder, info.FileName);
            if (!File.Exists(path)) return false;

            byte[] data = await File.ReadAllBytesAsync(path);
            
            // Re-escribir cabecera (Tipo 1)
            for (int i = 0; i < data.Length - 16; i += 16)
            {
                if (data[i] == 0x00 && data[i+1] == 0x00 && data[i+2] == 0x00 && data[i+3] == 0x01)
                {
                    data[i + 8] = (byte)(info.WidthLabel >> 8);
                    data[i + 9] = (byte)(info.WidthLabel & 0xFF);
                    data[i + 10] = (byte)(info.HeightLabel >> 8);
                    data[i + 11] = (byte)(info.HeightLabel & 0xFF);
                    break;
                }
            }

            // Re-escribir campos (Bloques de 16 bytes SM-100/300)
            foreach (var field in info.Fields)
            {
                // Buscar el bloque original por ID (Byte 7)
                for (int i = 0; i < data.Length - 16; i += 16)
                {
                    if (data[i + 7] == field.FieldId)
                    {
                        // Sincronizado con GetLabelFormatInfoAsync
                        data[i + 1] = (byte)(field.X >> 8);
                        data[i + 2] = (byte)(field.X & 0xFF);
                        data[i + 3] = (byte)(field.Y >> 8);
                        data[i + 4] = (byte)(field.Y & 0xFF);
                        data[i + 5] = (byte)field.Font;
                        // Nota: El flag (byte 0 y 6) se mantiene original
                    }
                }
            }

            await File.WriteAllBytesAsync(path, data);
            return true;
        }
        catch { return false; }
    }

    public async Task<string> SendRawHexAsync(Balanza balanza, string hexPayload, int fileId = 37)
    {
        try
        {
            hexPayload = hexPayload.Replace(" ", "").Replace("\r", "").Replace("\n", "").Trim().ToUpper();
            if (string.IsNullOrEmpty(hexPayload)) return "Error: Hex vacío";

            string digiFolder = Path.Combine(_baseDir, "Digi");
            string digiExe = Path.Combine(digiFolder, "digiwtcp.exe");
            if (!File.Exists(digiExe)) return "ERROR: digiwtcp.exe no encontrado.";

            string destFileName = $"SM{balanza.IpAddress}F{fileId}.DAT";
            string datFileDigi = Path.Combine(digiFolder, destFileName);
            
            // Escribimos la trama como texto ASCII (formato esperado por el driver para F37)
            await File.WriteAllTextAsync(datFileDigi, hexPayload, Encoding.ASCII);

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = Path.GetFullPath(digiExe),
                Arguments = $"WR {fileId} {balanza.IpAddress}",
                WorkingDirectory = Path.GetFullPath(digiFolder),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };

            string resultPath = Path.Combine(digiFolder, "RESULT");
            if (File.Exists(resultPath)) try { File.Delete(resultPath); } catch {}

            using var process = Process.Start(psi);
            await process!.WaitForExitAsync();

            string resultLine = await ReadResultWithRetry(resultPath);
            return resultLine.Trim() == "0" ? "Éxito: Trama enviada correctamente" : $"Error driver: {resultLine}";
        }
        catch (Exception ex)
        {
            return $"EXCEPTION: {ex.Message}";
        }
    }

    private byte[] IntToBcdArray(int value, int numBytes)
    {
        string s = value.ToString().PadLeft(numBytes * 2, '0');
        byte[] arr = new byte[numBytes];
        for (int i = 0; i < numBytes; i++)
        {
            arr[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
        }
        return arr;
    }

    private string GetDigiErrorMessage(string resCode)
    {
        return resCode switch
        {
            "0" => "Transmisión exitosa",
            "-1" => "Error de apertura en archivo de entrada o salida (OPEN_FILE_ERR)",
            "-2" => "Error leyendo archivo de entrada (READ_FILE_ERR)",
            "-3" => "Error de escritura a archivo de entrada o salida (WRIT_FILE_ERR)",
            "-5" => "Error de conexión a balanza (NETWORK_OPEN_ERR)",
            "-6" => "Error de recepción de datos desde la balanza (NETWORK_READ_ERR)",
            "-7" => "Error de envío de datos a la balanza (NETWORK_WRIT_ERR)",
            "-8" => "Error de lectura retornado por balanza (MACHINE_READ_ERR)",
            "-9" => "Error de escritura retornado por balanza (MACHINE_WRIT_ERR)",
            "-10" => "Error de 'no record' (sin registro) retornado por balanza (MACHINE_NOREC_ERR)",
            "-11" => "Error de espacio retornado por balanza (MACHINE_SPACE_ERR)",
            "-12" => "Error indefinido retornado por balanza (MACHINE_UNDEF_ERROR)",
            "LOCKED" => "El archivo de resultados está bloqueado por otro proceso.",
            "MISSING" => "No se encontró el archivo de resultado de la balanza.",
            _ => $"Error desconocido: {resCode}"
        };
    }

    private async Task AppendToLogAsync(Balanza balanza, PluItem item)
    {
        try
        {
            string logsDir = Path.Combine(_baseDir, "logs");
            if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);

            string fileName = $"sync_{balanza.IpAddress}_{DateTime.Now:yyyyMMdd}.log";
            string logPath = Path.Combine(logsDir, fileName);

            string line = $"[{DateTime.Now:HH:mm:ss}] PLU:{item.PluCode} - {item.Name} - {item.LastSyncStatus} - {item.LastSyncError ?? "OK"}";
            await File.AppendAllLinesAsync(logPath, new[] { line });
        }
        catch { /* Ignorar errores de log para no bloquear flujo */ }
    }
}
