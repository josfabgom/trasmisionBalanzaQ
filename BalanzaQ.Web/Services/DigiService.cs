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
                string existingFile = Directory.GetFiles(_baseDir, "SM*F37.DAT").FirstOrDefault();
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

                    // HEADER (5): Base(P=15, N=35) + (Len*2)
                    bool isPesable = item.ItemType == "P";
                    recordHeader[5] = (byte)((isPesable ? 15 : 35) + (currentLen * 2));

                    // PRECIO (11-14)
                    Array.Copy(IntToBcdArray((int)Math.Round(item.Price * 10), 4), 0, recordHeader, 11, 4);

                    // CONTROL Y SECCIÓN (16-17)
                    recordHeader[16] = isPesable ? (byte)0x09 : (byte)0x05;
                    recordHeader[17] = (byte)item.Section;

                    // ITEM CODE (18-22)
                    string strCode = item.PluCode.ToString();
                    if (strCode.Length % 2 != 0) strCode = "0" + strCode;
                    strCode = strCode.PadRight(10, '1');
                    byte[] codeBcd = new byte[5];
                    for (int j = 0; j < 5; j++) codeBcd[j] = Convert.ToByte(strCode.Substring(j * 2, 2), 16);
                    Array.Copy(codeBcd, 0, recordHeader, 18, 5);

                    // SECCIÓN/FORMATO (24-27)
                    byte[] secBcd = IntToBcdArray(item.Section, 2);
                    Array.Copy(secBcd, 0, recordHeader, 24, 2);
                    Array.Copy(secBcd, 0, recordHeader, 26, 2);

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

                string resultPath = Path.Combine(digiFolder, "RESULT");
                string resCode = File.Exists(resultPath) ? File.ReadAllText(resultPath).Trim() : "MISSING";
                bool success = (resCode == "0");

                foreach (var bItem in batchItems)
                {
                    bItem.LastSyncDate = DateTime.Now;
                    bItem.LastSyncStatus = success ? "Sincronizado" : "Error";
                    bItem.LastSyncError = success ? null : resCode;
                    bItem.IsSyncronized = success;
                }

                if (!success) return ($"Error de balanza en lote {i}: Código {resCode}", hexPayload);
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

            using var process = Process.Start(psi);
            await process!.WaitForExitAsync();

            string resultPath = Path.Combine(digiFolder, "RESULT");
            if (File.Exists(resultPath))
            {
                string resCode = File.ReadAllText(resultPath).Trim();
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
                return $"Error de balanza: Código {resCode}";
            }
            return "Error: No se recibió respuesta de digiwtcp.";
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
}
