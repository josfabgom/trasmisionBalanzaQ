using BalanzaQ.Web.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

var optionsBuilder = new DbContextOptionsBuilder<BalanzaDbContext>();
optionsBuilder.UseSqlite("Data Source=balanzas.db");

using var db = new BalanzaDbContext(optionsBuilder.Options);
var item = db.PluItems.FirstOrDefault(p => p.PluCode == 20683);

if (item != null)
{
    Console.WriteLine($"--- DATOS PLU 20683 ---");
    Console.WriteLine($"Nombre: {item.ShortName}");
    Console.WriteLine($"Viene de data.dat: P;45;01;");
    Console.WriteLine($"Formato en DB: {item.BarcodeFormat}");
    Console.WriteLine($"Flag en DB: {item.BarcodeFlag}");
    Console.WriteLine($"-----------------------");
}
else
{
    Console.WriteLine("No se encontró el PLU 20683.");
}
