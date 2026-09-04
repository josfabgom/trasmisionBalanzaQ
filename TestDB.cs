using System;
using Microsoft.Data.Sqlite;
class Program {
    static void Main() {
        using (var connection = new SqliteConnection("Data Source=BalanzaQ.Web\\balanzas.db")) {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT PluCode, ItemType, ShortName FROM PluItems WHERE PluCode = 1051";
            using (var reader = command.ExecuteReader()) {
                while (reader.Read()) {
                    Console.WriteLine($"{reader.GetString(0)}, {reader.GetString(1)}, {reader.GetString(2)}");
                }
            }
        }
    }
}
