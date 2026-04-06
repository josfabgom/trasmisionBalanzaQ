using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using BalanzaQ.Web.Data;
using BalanzaQ.Web.Models;
using Microsoft.Extensions.Configuration;

namespace Debugger
{
    class Program
    {
        static void Main(string[] args)
        {
            try {
                var optionsBuilder = new DbContextOptionsBuilder<BalanzaDbContext>();
                optionsBuilder.UseSqlite("Data Source=balanzas.db");

                using var db = new BalanzaDbContext(optionsBuilder.Options);
                var plu = db.PluItems.FirstOrDefault(p => p.PluCode == 22);

                if (plu != null) {
                    Console.WriteLine($"PLU: {plu.PluCode}");
                    Console.WriteLine($"Name: {plu.Name}");
                    Console.WriteLine($"Price: {plu.Price}");
                    Console.WriteLine($"Section: {plu.Section}");
                    Console.WriteLine($"LabelFormat: {plu.LabelFormat}");
                } else {
                    Console.WriteLine("PLU 22 NOT FOUND");
                }
            } catch (Exception ex) {
                Console.WriteLine($"ERROR: {ex.Message}");
            }
        }
    }
}
