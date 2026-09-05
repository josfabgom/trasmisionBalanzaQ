using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

class Program {
    static void Main() {
        using (var conn = new SqliteConnection("Data Source=BalanzaQ.Web\\balanzas.db")) {
            conn.Open();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT PluCode, ItemType, Price, ShelfLife FROM PluItems WHERE PluCode IN (4051, 4921, 1371)";
            using (var reader = cmd.ExecuteReader()) {
                while (reader.Read()) {
                    Console.WriteLine($"PLU: {reader.GetInt32(0)}, Type: {reader.GetString(1)}, Price: {reader.GetDouble(2)}, ShelfLife: {reader.GetInt32(3)}");
                }
            }
        }
    }
}
