using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BalanzaQ.Web.Services;

public class DbImporterService
{
    private readonly IConfiguration _config;
    
    public DbImporterService(IConfiguration config)
    {
        _config = config;
    }

    public async Task<List<string>> GetTablesAsync(string dbPath)
    {
        var tables = new List<string>();
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();
        
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name != '__EFMigrationsHistory';";
        
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }
        
        return tables;
    }

    public async Task<string> ImportTablesAsync(string sourceDbPath, string targetDbPath, List<string> tablesToImport)
    {
        try
        {
            // Crear backup de la BD destino
            string backupPath = targetDbPath + ".bak_" + System.DateTime.Now.ToString("yyyyMMddHHmmss");
            File.Copy(targetDbPath, backupPath, true);

            using var targetConn = new SqliteConnection($"Data Source={targetDbPath}");
            await targetConn.OpenAsync();
            
            // Attach source database
            using var attachCmd = targetConn.CreateCommand();
            attachCmd.CommandText = $"ATTACH DATABASE '{sourceDbPath.Replace("'", "''")}' AS SourceDb;";
            await attachCmd.ExecuteNonQueryAsync();

            int totalRowsImported = 0;

            foreach (var table in tablesToImport)
            {
                // Obtener columnas de origen
                var sourceCols = await GetColumnsAsync(targetConn, "SourceDb", table);
                // Obtener columnas de destino
                var targetCols = await GetColumnsAsync(targetConn, "main", table);
                
                // Interseccion
                var commonCols = sourceCols.Intersect(targetCols, System.StringComparer.OrdinalIgnoreCase).ToList();
                if (!commonCols.Any()) continue;
                
                var colsStr = string.Join(", ", commonCols.Select(c => $"\"{c}\""));
                
                // Empezar transaccion para cada tabla
                using var transaction = targetConn.BeginTransaction();
                
                // Vaciar tabla destino
                using var deleteCmd = targetConn.CreateCommand();
                deleteCmd.Transaction = transaction;
                deleteCmd.CommandText = $"DELETE FROM main.\"{table}\";";
                await deleteCmd.ExecuteNonQueryAsync();
                
                // Insertar datos
                using var insertCmd = targetConn.CreateCommand();
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = $"INSERT INTO main.\"{table}\" ({colsStr}) SELECT {colsStr} FROM SourceDb.\"{table}\";";
                var rows = await insertCmd.ExecuteNonQueryAsync();
                totalRowsImported += rows;
                
                transaction.Commit();
            }
            
            // Detach
            using var detachCmd = targetConn.CreateCommand();
            detachCmd.CommandText = "DETACH DATABASE SourceDb;";
            await detachCmd.ExecuteNonQueryAsync();

            return $"Importación exitosa: {totalRowsImported} registros importados. (Backup guardado en: {Path.GetFileName(backupPath)})";
        }
        catch (System.Exception ex)
        {
            return $"Error durante la importación: {ex.Message}";
        }
    }

    private async Task<List<string>> GetColumnsAsync(SqliteConnection connection, string dbSchema, string table)
    {
        var cols = new List<string>();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA {dbSchema}.table_info(\"{table}\");";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            cols.Add(reader.GetString(1)); // Indice 1 es el nombre de la columna
        }
        return cols;
    }
}
