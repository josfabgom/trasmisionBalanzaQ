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

            // 2. Construir Payload Unificado
            StringBuilder finalHexAll = new StringBuilder();

            foreach (var item in items)
            {
                paylList.Clear();
                // Lógica Dinámica con Mapeo de Campos REAL (Forense 153)
                byte[] recordHeader = new byte[numNameStart + 3];
                Array.Copy(templateBytes, 0, recordHeader, 0, recordHeader.Length);

                // 1. PLU BCD (Bytes 0-3)
                int pluCode = item.PluCode;
                byte[] pluBcd = IntToBcdArray(pluCode, 4);
                Array.Copy(pluBcd, 0, recordHeader, 0, 4);

                // 2. NOMBRE (Uso prioritario de Name para Header exacto)
                string nameToUse = item.Name;
                if (nameToUse.Length > 28) nameToUse = nameToUse.Substring(0, 28);
                byte[] nameBytes = Encoding.ASCII.GetBytes(nameToUse);
                int currentNameLen = nameBytes.Length;

                // 3. HEADER / LARGO (Byte 5): Fórmula Base + (Len * 2)
                bool isPesable = item.ItemType == "P";
                int headerBase = isPesable ? 15 : 35;
                recordHeader[5] = (byte)(headerBase + (currentNameLen * 2));

                // 4. PRECIO BCD (Bytes 11-14)
                int priceScaled = (int)Math.Round(item.Price * 10);
                byte[] priceBcd = IntToBcdArray(priceScaled, 4);
                Array.Copy(priceBcd, 0, recordHeader, 11, 4);

                // 5. CONTROL Y SECCIÓN (Byte 16-17)
                recordHeader[16] = isPesable ? (byte)0x09 : (byte)0x05;
                recordHeader[17] = (byte)item.Section; // Sección en index 17 según traza real

                // 6. ITEM CODE (Bytes 18-22) - Alineación exacta
                string strCode = item.PluCode.ToString();
                if (strCode.Length % 2 != 0) strCode = "0" + strCode; // Pad para ser par si es necesario
                strCode = strCode.PadRight(10, '1');
                byte[] codeBcd = new byte[5];
                for (int i = 0; i < 5; i++) codeBcd[i] = Convert.ToByte(strCode.Substring(i * 2, 2), 16);
                Array.Copy(codeBcd, 0, recordHeader, 18, 5);

                // 7. SECCIÓN / FORMATO (Bytes 24-27)
                byte[] sectionBcd = IntToBcdArray(item.Section, 2);
                Array.Copy(sectionBcd, 0, recordHeader, 24, 2);
                Array.Copy(sectionBcd, 0, recordHeader, 26, 2);

                // 8. LONGITUD NOMBRE (Byte final del header)
                recordHeader[recordHeader.Length - 1] = (byte)currentNameLen;

                finalHexAll.Append(Convert.ToHexString(recordHeader));
                finalHexAll.Append(Convert.ToHexString(nameBytes));
                finalHexAll.Append(Convert.ToHexString(afterName));
            }

            // 3. Escribir y enviar archivo único
            string destFileName = $"SM{balanza.IpAddress}F37.DAT";
            string datFileDigi = Path.Combine(_digiDir, destFileName);
            string hexPayload = finalHexAll.ToString().ToUpper();

            if (!enviarABalanza)
            {
                string dDebugFile = Path.Combine(_baseDir, $"SM{balanza.IpAddress}F37_DEBUG.DAT");
                File.WriteAllText(dDebugFile, hexPayload, Encoding.ASCII);
                return ("Modo manual: Archivo DAT generado.", dDebugFile);
            }

            // 3. Escribir a Balanza
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = digiExe,
                Arguments = $"WT 37 {destFileName} {balanza.IpAddress}",
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

            foreach (var item in items)
            {
                item.LastSyncDate = DateTime.Now;
                item.LastSyncStatus = success ? "Sincronizado" : "Error";
                item.LastSyncError = success ? null : resCode;
                item.IsSyncronized = success;
            }

            if (!success) return ($"Error de balanza: Código {resCode}", hexPayload);
            return ("Exito", hexPayload);
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
