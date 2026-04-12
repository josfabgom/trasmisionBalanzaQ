using System;
using System.IO;
using BalanzaQ.LicenceGenerator;

Console.WriteLine("========================================");
Console.WriteLine("Generador de Licencias BalanzaQ");
Console.WriteLine("v1.0.0");
Console.WriteLine("========================================");

Console.Write("\nIngrese el MACHINE UID del cliente: ");
string? uid = Console.ReadLine()?.Trim();

if (string.IsNullOrEmpty(uid))
{
    Console.WriteLine("ERROR: El UID no puede estar vacío.");
    return;
}

Console.Write("Ingrese la vigencia en meses (Enter para 12 meses): ");
string? inputMonths = Console.ReadLine()?.Trim();
int months = string.IsNullOrEmpty(inputMonths) ? 12 : int.Parse(inputMonths);

DateTime expiryDate = DateTime.Now.AddMonths(months);

try
{
    string encryptedLicense = SecurityUtils.EncryptLicense(uid, expiryDate);
    string fileName = "license.lic";
    
    File.WriteAllText(fileName, encryptedLicense);
    
    Console.WriteLine("\n----------------------------------------");
    Console.WriteLine("¡ÉXITO! Licencia generada correctamente.");
    Console.WriteLine($"Vigencia: {months} meses (Hasta: {expiryDate:dd/MM/yyyy})");
    Console.WriteLine($"Archivo creado: {Path.GetFullPath(fileName)}");
    Console.WriteLine("----------------------------------------");
    Console.WriteLine("\nEntregue este archivo al cliente para que lo coloque en la raíz de su aplicación.");
}
catch (Exception ex)
{
    Console.WriteLine($"\nERROR CRÍTICO: {ex.Message}");
}

Console.WriteLine("\nPresione cualquier tecla para salir...");
Console.ReadKey();
