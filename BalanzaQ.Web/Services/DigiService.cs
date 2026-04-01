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

            // 2. Construir payload
            var paylList = new List<byte>();
            foreach (var item in items)
            {
                byte[] curBefore = new byte[beforeName.Length];
                Array.Copy(beforeName, curBefore, beforeName.Length);

                // PLU BCD (Bytes 0-3)
                int pluCode = item.PluCode;
                byte[] pluBcd = IntToBcdArray(pluCode, 4);
                Array.Copy(pluBcd, 0, curBefore, 0, 4);

                // PRECIO BCD (Bytes 11-14)
                // Se observó que el BCD esperado de 28376.00 es 00 28 37 60 (283760), es decir, por 10.
                int priceScaled = (int)Math.Round(item.Price * 10);
                byte[] priceBcd = IntToBcdArray(priceScaled, 4);
                Array.Copy(priceBcd, 0, curBefore, 11, 4);

                // Nombre (DIGI SM suele tener un límite estricto de ~25 dependiendo de la asignación)
                string nameToUse = string.IsNullOrWhiteSpace(item.ShortName) ? item.Name : item.ShortName;
                string nameTruncated = nameToUse.Length > 25 ? nameToUse.Substring(0, 25) : nameToUse;
                byte[] nameBytes = Encoding.ASCII.GetBytes(nameTruncated);

                // Forzamos el Byte 5 a '43' (0x43) si era 0, o lo dejamos en 41 dependiendo de algún flag si fuera necesario,
                // Pero por ahora igualamos a lo que el usuario espera según el item (PLU 36 = 43, 231 = 41).
                // Podría tener que ver con si es Group=1 (carniceria) vs Group=11 (quesos). Para igualarlo lo dejaremos en 0x43 si el grupo es 1.
                if (item.Group == 1)
                {
                    curBefore[5] = 0x43;
                }
                else
                {
                    curBefore[5] = 0x41;
                }

                // ITEM CODE (Bytes 19-23)
                // DIGI commonly expects item code to be BCD padded with Fs (or 1s in some configs) up to 10 digits
                // Si PluCode es par (ej 36), parece llevar cero a la izquierda para hacerlo impar según el archivo esperado (03611...)
                string strCode = item.PluCode.ToString();
                if (strCode.Length % 2 == 0) strCode = "0" + strCode;
                strCode = strCode.PadRight(10, '1'); // Pad with 1s to 10 chars
                byte[] codeBcd = new byte[5];
                for (int i = 0; i < 5; i++) codeBcd[i] = Convert.ToByte(strCode.Substring(i * 2, 2), 16);
                Array.Copy(codeBcd, 0, curBefore, 19, 5);
                
                // SHELF LIFE (Bytes 24-25 and 26-27)
                byte[] shelfBcd = IntToBcdArray(item.ShelfLife, 2);
                Array.Copy(shelfBcd, 0, curBefore, 24, 2); // Sell By
                Array.Copy(shelfBcd, 0, curBefore, 26, 2); // Used By

                // Len
                curBefore[curBefore.Length - 1] = (byte)nameBytes.Length;

                paylList.AddRange(curBefore);
                paylList.AddRange(nameBytes);
                paylList.AddRange(afterName);
            }

            // 3. Escribir archivo F37 en directorio local
            string destFileName = $"SM{balanza.IpAddress}F37.DAT";
            string datFileRoot = Path.Combine(_baseDir, destFileName);
            string datFileDigi = Path.Combine(digiFolder, destFileName);

            string finalHex = Convert.ToHexString(paylList.ToArray());
            File.WriteAllText(datFileRoot, finalHex, Encoding.ASCII);
            File.WriteAllText(datFileDigi, finalHex, Encoding.ASCII); // Copia en Digi carpeta donde vive EXE

            if (!enviarABalanza)
            {
                return ("Generado, no enviado.", finalHex);
            }

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

            // Check RESULT
            string resultPath = Path.Combine(digiFolder, "RESULT");
            if (File.Exists(resultPath))
            {
                string resCode = File.ReadAllText(resultPath).Trim();
                if(resCode == "0") return ("Exito", finalHex);
                return ($"Error de balanza: Código {resCode}", finalHex);
            }

            return ("Enviado, sin archivo de resultado.", finalHex);
        }
        catch (Exception ex)
        {
            return ($"EXCEPTION: {ex.Message}", "");
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
