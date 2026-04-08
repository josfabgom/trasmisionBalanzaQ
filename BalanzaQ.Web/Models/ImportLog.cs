using System.ComponentModel.DataAnnotations;

namespace BalanzaQ.Web.Models;

public class ImportLog
{
    public int Id { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    
    [MaxLength(100)]
    public string FileName { get; set; } = string.Empty;
    
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public int ErrorCount { get; set; }
    
    public string? details { get; set; } // Resumen de lo que se importó o errores específicos
}
