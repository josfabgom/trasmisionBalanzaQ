using System.IO;
using System.Text;
using BalanzaQ.Web.Models;
using BalanzaQ.Web.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace BalanzaQ.Web.Services;

public class DigiService
{
    private readonly IConfiguration _config;
    private readonly string _baseDir;

    public DigiService(IConfiguration config)
    {
        _config = config;
        // Si el directorio actual tiene TEMPLATE.DAT, asume que es la raíz (portable)
        string currentDir = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(currentDir, "TEMPLATE.DAT")) || Directory.Exists(Path.Combine(currentDir, "Digi")))
        {
            _baseDir = currentDir;
        }
        else
        {
            _baseDir = Path.GetFullPath(Path.Combine(currentDir, "..")); // Modo desarrollo
        }
    }

    public async Task<(string Message, string HexPayload)> SyncBalanzaAsync(Balanza balanza, List<PluItem> items, bool enviarABalanza = true)
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
                string hex = sr.ReadToEnd().Trim();
                if (hex.EndsWith("E2")) hex = hex.Substring(0, hex.Length - 2);
                templateBytes = Convert.FromHexString(hex);
            }

            int numNameStart = FindSequence(templateBytes, new byte[] { 0x03, 0x07 });
            if (numNameStart == -1) return ("ERROR: Patrón 03 07 no encontrado.", "");

            int templateNameLen = templateBytes[numNameStart + 2];
            byte[] afterName = new byte[templateBytes.Length - (numNameStart + 3 + templateNameLen)];
            Array.Copy(templateBytes, numNameStart + 3 + templateNameLen, afterName, 0, afterName.Length);

            // 2. Transmisión en LOTES (Evita Error -3 por saturación)
            int batchSize = 100;
            StringBuilder finalLogAll = new StringBuilder();

            for (int i = 0; i < items.Count; i += batchSize)
            {
                var batchItems = items.Skip(i).Take(batchSize).ToList();
                StringBuilder batchHex = new StringBuilder();

                foreach (var item in batchItems)
                {
                    // Mapeo Forense Digi (V3)
                    byte[] recordHeader = new byte[numNameStart + 3];
                    Array.Copy(templateBytes, 0, recordHeader, 0, recordHeader.Length);

                    // PLU (0-3)
                    Array.Copy(IntToBcdArray(item.PluCode, 4), 0, recordHeader, 0, 4);

                    // NOMBRE (Para Header)
                    string nameToUse = item.Name;
                    if (nameToUse.Length > 28) nameToUse = nameToUse.Substring(0, 28);
                    byte[] nameBytes = Encoding.ASCII.GetBytes(nameToUse);
                    int currentLen = nameBytes.Length;

                    // HEADER (5): Base(42) + Len
                    bool isPesable = item.ItemType == "P";
                    recordHeader[5] = (byte)(0x2A + currentLen); 
                    recordHeader[6] = isPesable ? (byte)0x7C : (byte)0x7D;

                    // TIPO DE ARTÍCULO (Byte 10) - Estándar 0x0D para ambos
                    recordHeader[10] = 0x0D; 

                    // PRECIO (11-14) - 4 bytes BCD (ej: 5000 -> 00 05 00 00 o similar conforme a balanza)
                    Array.Copy(IntToBcdArray((int)Math.Round(item.Price * 10), 4), 0, recordHeader, 11, 4);

                    // TIPO Y SECCIÓN (16-17)
                    recordHeader[16] = (byte)item.Section; 
                    recordHeader[17] = (byte)(item.LabelFormat > 0 ? item.LabelFormat : 0x1E); 

                    // ITEM CODE (18-23) - 6 bytes BCD
                    string strCode = item.PluCode.ToString().PadLeft(5, '0').PadRight(12, '1');
                    byte[] codeBcd = new byte[6];
                    for (int j = 0; j < 6; j++) codeBcd[j] = Convert.ToByte(strCode.Substring(j * 2, 2), 16);
                    Array.Copy(codeBcd, 0, recordHeader, 18, 6);

                    // SECCIÓN (24-25) Y ETIQUETA (26-27)
                    Array.Copy(IntToBcdArray(item.Section, 2), 0, recordHeader, 24, 2);
                    Array.Copy(IntToBcdArray(item.LabelFormat > 0 ? item.LabelFormat : 30, 2), 0, recordHeader, 26, 2);

                    // LEN NOMBRE (Header final)
                    recordHeader[recordHeader.Length - 1] = (byte)currentLen;

                    batchHex.Append(Convert.ToHexString(recordHeader));
                    batchHex.Append(Convert.ToHexString(nameBytes));
                    batchHex.Append(Convert.ToHexString(afterName));
                }

                string hexPayload = batchHex.ToString().ToUpper();
                string destFileName = $"SM{balanza.IpAddress}F37.DAT";
                string datFileDigi = Path.Combine(digiFolder, destFileName);
                File.WriteAllText(datFileDigi, hexPayload, Encoding.ASCII);
                finalLogAll.Append(hexPayload);

                if (!enviarABalanza) continue;

                // Limpiar resultado previo para evitar colisiones
                string resultPath = Path.Combine(digiFolder, "RESULT");
                if (File.Exists(resultPath)) try { File.Delete(resultPath); } catch {}

                // 3. Escribir Lote a Balanza
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = digiExe,
                    Arguments = $"WR 37 {balanza.IpAddress}",
                    WorkingDirectory = digiFolder,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };

                using var process = Process.Start(psi);
                await process!.WaitForExitAsync();
                string resCode = await ReadResultWithRetry(resultPath);
                bool success = (resCode == "0");

                foreach (var bItem in batchItems)
                {
                    bItem.LastSyncDate = DateTime.Now;
                    bItem.LastSyncStatus = success ? "Sincronizado" : "Error";
                    bItem.LastSyncError = success ? null : resCode;
                    bItem.IsSyncronized = success;
                }

                if (!success) return ($"Error en lote {i}: {GetDigiErrorMessage(resCode)}", hexPayload);
            }

            return ("Exito", finalLogAll.ToString());
        }
        catch (Exception ex)
        {
            return ($"EXCEPTION: {ex.Message}", "");
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

            string resCode = await ReadResultWithRetry(resultPath);
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

            if (resCode == "MISSING" || resCode == "LOCKED") return "Error: No se recibió respuesta de la balanza o el archivo está bloqueado.";
            return $"Error: {GetDigiErrorMessage(resCode)}";
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
        for (int i = 0; i < 10; i++)
        {
            try
            {
                if (File.Exists(path)) return (await File.ReadAllTextAsync(path)).Trim();
                await Task.Delay(100); // Esperar a que el archivo aparezca
            }
            catch (IOException)
            {
                await Task.Delay(200); // Esperar si está bloqueado
            }
        }
        return File.Exists(path) ? "LOCKED" : "MISSING";
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
}
