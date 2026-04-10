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
    }

    public async Task<(string Message, string HexPayload)> SyncBalanzaAsync(Balanza balanza, List<PluItem> items, bool enviarABalanza = true, Action<int, int>? onProgress = null, string batchId = "")
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

            // 1. Leer Plantilla y Extraer Partes
            byte[] templateBytes;
            using (var fs = new FileStream(templatePath, FileMode.Open, FileAccess.Read))
            using (var sr = new StreamReader(fs, Encoding.ASCII))
            {
                string rawHex = await sr.ReadToEndAsync();
                // Limpieza total: eliminar todo lo que no sea hexadecimal (espacios, saltos de línea, etc)
                string cleanHex = System.Text.RegularExpressions.Regex.Replace(rawHex, @"[^a-fA-F0-9]", "");
                
                if (cleanHex.EndsWith("E2")) cleanHex = cleanHex.Substring(0, cleanHex.Length - 2);
                templateBytes = Convert.FromHexString(cleanHex);
            }

            int numNameStart = FindSequence(templateBytes, new byte[] { 0x03, 0x07 });
            if (numNameStart == -1) return ("ERROR: Patrón 03 07 no encontrado.", "");

            int templateNameLen = templateBytes[numNameStart + 2];
            byte[] afterName = new byte[templateBytes.Length - (numNameStart + 3 + templateNameLen)];
            Array.Copy(templateBytes, numNameStart + 3 + templateNameLen, afterName, 0, afterName.Length);

            // 2. Transmisión INDIVIDUAL (v3.4.4 para máxima fiabilidad)
            StringBuilder finalLogAll = new StringBuilder();
            string resultPath = Path.Combine(digiFolder, "RESULT");
            string f37Path = Path.Combine(digiFolder, $"SM{balanza.IpAddress}F37.DAT");
            string batPath = Path.Combine(digiFolder, "run_sync.bat");

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                    // Data Setup
                    Array.Copy(IntToBcdArray(item.PluCode, 4), 0, record, 0, 4);
                    record[5] = isPesable ? (byte)0x41 : (byte)0x39;
                    record[6] = isPesable ? (byte)0x7C : (byte)0x7D;
                    Array.Copy(IntToBcdArray((int)Math.Round(item.Price * 10), 4), 0, record, 11, 4);
                    Array.Copy(IntToBcdArray(item.ShelfLife, 2), 0, record, 24, 2);
                    Array.Copy(IntToBcdArray(item.Section, 2), 0, record, 26, 2);
                    Array.Copy(IntToBcdArray(isPesable ? 0 : 1, 2), 0, record, 28, 2);

                    // Barcode (v3.3.7 pattern)
                    string fullBarcodeStr = ("000" + item.PluCode.ToString() + "1111111111").Substring(0, 12);
                    byte[] barcodeBytes = new byte[6];
                    for (int j = 0; j < 6; j++) barcodeBytes[j] = Convert.ToByte(fullBarcodeStr.Substring(j * 2, 2), 16);
                    Array.Copy(barcodeBytes, 0, record, 18, 6);
                    record[15] = (byte)17; 

                    // Name & Marker
                    record[numNameStart] = (byte)0x01; 
                    string nameToUse = (item.Name ?? "").PadRight(templateNameLen, ' ').Substring(0, templateNameLen);
                    byte[] nameBytes = Encoding.ASCII.GetBytes(nameToUse);
                    record[numNameStart + 2] = (byte)templateNameLen;

                    StringBuilder rowHex = new StringBuilder();
                    rowHex.Append(Convert.ToHexString(record, 0, numNameStart + 3)); 
                    rowHex.Append(Convert.ToHexString(nameBytes));                  

                    byte[] localAfterName = (byte[])afterName.Clone();
                    for (int k = 0; k < localAfterName.Length - 12; k++)
                    {
                        if (localAfterName[k] == 0xFF && localAfterName[k + 1] == 0x09)
                        {
                            localAfterName[k + 9] = 0x01; 
                            localAfterName[k + 10] = 0x01; 
                        }
                    }
                    rowHex.Append(Convert.ToHexString(localAfterName));             
                    
                    // Transmitir individualmente
                    if (File.Exists(f37Path)) try { File.Delete(f37Path); } catch {}
                    if (File.Exists(resultPath)) try { File.Delete(resultPath); } catch {}
                    
                    await File.WriteAllTextAsync(f37Path, rowHex.ToString().ToUpper() + "\n", Encoding.ASCII);

                    if (enviarABalanza)
                    {
                        string batContent = $"@echo off\r\ncd /d \"%~dp0\"\r\ndigiwtcp.exe WR 37 {balanza.IpAddress}\r\nexit";
                        await File.WriteAllTextAsync(batPath, batContent);

                        using (Process proc = Process.Start("cmd.exe", $"/c \"{batPath}\""))
                        {
                            await proc.WaitForExitAsync();
                        }

                        string resultLine = await ReadResultWithRetry(resultPath);
                        string resCode = resultLine; 
                        int lastColon = resultLine.LastIndexOf(':');
                        if (lastColon >= 0) resCode = resultLine.Substring(lastColon + 1).Trim();

                        bool success = (resCode == "0");
                        finalLogAll.AppendLine($"PLU {item.PluCode}: {resultLine}");

                        // Auditoría Item por Item
                        item.LastSyncDate = DateTime.Now;
                        item.LastSyncStatus = success ? "Exitoso" : "Fallo";
                        item.LastSyncError = GetDigiErrorMessage(resCode);
                        item.IsSyncronized = success;

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
                }

                onProgress?.Invoke(i + 1, items.Count);
            }

            return ("Sincronización Individual Finalizada.", finalLogAll.ToString());
        }
        catch (Exception ex)
        {
            return ($"ERROR: {ex.Message}", "");
        }
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
        for (int i = 0; i < 15; i++)
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
