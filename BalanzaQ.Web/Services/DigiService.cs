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
            if(items == null || !items.Any()) return ("No hay articulos para enviar.", "");

            string templatePath = Path.Combine(_baseDir, "TEMPLATE.DAT");
            string digiFolder = Path.Combine(_baseDir, "Digi");
            string digiExe = Path.Combine(digiFolder, "digiwtcp.exe");

            if (!File.Exists(templatePath))
            {
                // Fallback attempt to get one existing file
                string existingFile = Directory.GetFiles(_baseDir, "SM*F37.DAT").FirstOrDefault();
                if (existingFile != null)
                {
                    File.Copy(existingFile, templatePath);
                }
                else
                {
                    return ("ERROR: Plantilla TEMPLATE.DAT no encontrada en el directorio raíz.", "");
                }
            }

            // 1. Read Template bytes
            byte[] templateBytes;
            using (var fs = new FileStream(templatePath, FileMode.Open, FileAccess.Read))
            using (var sr = new StreamReader(fs, Encoding.ASCII))
            {
                string hex = sr.ReadToEnd().Trim();
                if (hex.EndsWith("E2")) hex = hex.Substring(0, hex.Length - 2);
                templateBytes = Convert.FromHexString(hex);
            }

            // Encuentra el separador (03 07)
            int numNameStart = FindSequence(templateBytes, new byte[] { 0x03, 0x07 });
            if (numNameStart == -1) return ("ERROR: Plantilla inválida, patrón 03 07 no encontrado.", "");

            int nameLen = templateBytes[numNameStart + 2];
            byte[] beforeName = new byte[numNameStart + 3];
            Array.Copy(templateBytes, 0, beforeName, 0, beforeName.Length);

            byte[] afterName = new byte[templateBytes.Length - (numNameStart + 3 + nameLen)];
            Array.Copy(templateBytes, numNameStart + 3 + nameLen, afterName, 0, afterName.Length);

            // 2. Construir payload en LOTES para evitar límite de buffer de 64KB en la balanza
            int batchSize = 100;
            StringBuilder finalHexAll = new StringBuilder();

            for (int chunkStart = 0; chunkStart < items.Count; chunkStart += batchSize)
            {
                var batchItems = items.Skip(chunkStart).Take(batchSize).ToList();
                var paylList = new List<byte>();

                foreach (var item in batchItems)
                {
                    // Volver a lógica dinámica para no romper patrones 03 07
                    byte[] recordHeader = new byte[numNameStart + 3];
                    Array.Copy(templateBytes, 0, recordHeader, 0, recordHeader.Length);

                    // PLU BCD (Bytes 0-3)
                    int pluCode = item.PluCode;
                    byte[] pluBcd = IntToBcdArray(pluCode, 4);
                    Array.Copy(pluBcd, 0, recordHeader, 0, 4);

                    // CONTROL BYTE (Byte 5): 3D para Pesado (P), 41 para Unitario (N)
                    bool isPesable = item.ItemType == "P";
                    recordHeader[5] = isPesable ? (byte)0x3D : (byte)0x41;

                    // BYTE 16: 09 para Pesado (P), 05 para Unitario (N)
                    recordHeader[16] = isPesable ? (byte)0x09 : (byte)0x05;

                    // PRECIO BCD (Bytes 11-14)
                    int priceScaled = (int)Math.Round(item.Price * 10);
                    byte[] priceBcd = IntToBcdArray(priceScaled, 4);
                    Array.Copy(priceBcd, 0, recordHeader, 11, 4);

                    // ITEM CODE (Bytes 19-23)
                    string strCode = item.PluCode.ToString();
                    if (strCode.Length % 2 == 0) strCode = "0" + strCode;
                    strCode = strCode.PadRight(10, '1');
                    byte[] codeBcd = new byte[5];
                    for (int i = 0; i < 5; i++) codeBcd[i] = Convert.ToByte(strCode.Substring(i * 2, 2), 16);
                    Array.Copy(codeBcd, 0, recordHeader, 19, 5);

                    // SECCIÓN / FORMATO (Bytes 24-25 y 26-27)
                    byte[] sectionBcd = IntToBcdArray(item.Section, 2);
                    Array.Copy(sectionBcd, 0, recordHeader, 24, 2);
                    Array.Copy(sectionBcd, 0, recordHeader, 26, 2);

                    // NOMBRE (Byte dinámico)
                    string nameToUse = string.IsNullOrWhiteSpace(item.ShortName) ? item.Name : item.ShortName;
                    byte[] nameBytes = Encoding.ASCII.GetBytes(nameToUse);
                    if (nameBytes.Length > 28) nameBytes = nameBytes.Take(28).ToArray();
                    
                    recordHeader[recordHeader.Length - 1] = (byte)nameBytes.Length;

                    paylList.AddRange(recordHeader);
                    paylList.AddRange(nameBytes);
                    paylList.AddRange(afterName);
                }

                string finalHex = Convert.ToHexString(paylList.ToArray());
                finalHexAll.Append(finalHex);

                if (chunkStart + batchSize >= items.Count)
                {
                    // Es el último lote, añadir terminador de archivo E2 si no existe
                    finalHexAll.Append("E2");
                }

                if (!enviarABalanza)
                {
                    // Solo guardamos un consolidado local y salimos (para modo demo/archivoDAT)
                    string dDebugFile = Path.Combine(_baseDir, $"SM{balanza.IpAddress}F37_DEBUG.DAT");
                    File.WriteAllText(dDebugFile, finalHexAll.ToString(), Encoding.ASCII);
                    continue; // Skip balanza transmit if requested
                }

                // 3. Escribir archivo F37 del Lote en directorio local
                string destFileName = $"SM{balanza.IpAddress}F37.DAT";
                string datFileRoot = Path.Combine(_baseDir, destFileName);
                string datFileDigi = Path.Combine(digiFolder, destFileName);

                File.WriteAllText(datFileRoot, finalHex, Encoding.ASCII);
                File.WriteAllText(datFileDigi, finalHex, Encoding.ASCII);

                // 4. Invocar digiwtcp.exe para escribir
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

                // Validar RESULT
                string resultPath = Path.Combine(digiFolder, "RESULT");
                bool success = false;
                string resCode = "ERROR_RESULT_MISSING";

                if (File.Exists(resultPath))
                {
                    resCode = File.ReadAllText(resultPath).Trim();
                    success = (resCode == "0");
                }

                // ACTUALIZAR ESTADO DE CADA ITEM DEL LOTE
                foreach(var item in batchItems)
                {
                    item.LastSyncDate = DateTime.Now;
                    if (success)
                    {
                        item.LastSyncStatus = "Sincronizado";
                        item.LastSyncError = null;
                        item.IsSyncronized = true;
                    }
                    else
                    {
                        item.LastSyncStatus = "Error";
                        item.LastSyncError = resCode;
                        item.IsSyncronized = false;
                    }
                }

                if (!success)
                {
                    return ($"Error de balanza en lote {chunkStart}: Código {resCode}", finalHexAll.ToString());
                }
            }

            if (!enviarABalanza) return ("Generado, no enviado.", finalHexAll.ToString());

            return ("Exito", finalHexAll.ToString());
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
