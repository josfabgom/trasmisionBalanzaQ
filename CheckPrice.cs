using System;
using Microsoft.EntityFrameworkCore;
using BalanzaQ.Web.Data;
using BalanzaQ.Web.Models;
using System.Linq;

class Program {
    static void Main() {
        var options = new DbContextOptionsBuilder<BalanzaDbContext>().UseSqlite("Data Source=BalanzaQ.Web\\balanzas.db").Options;
        using var db = new BalanzaDbContext(options);
        var item = db.PluItems.FirstOrDefault(p => p.PluCode == 4921);
        if (item != null) Console.WriteLine($"Price of 4921 is: {item.Price}");
    }
}
