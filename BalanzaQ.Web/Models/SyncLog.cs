using System.ComponentModel.DataAnnotations;

namespace BalanzaQ.Web.Models;

public class SyncLog
{
    public int Id { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    
    [MaxLength(50)]
    public string BalanzaIp { get; set; } = string.Empty;
    
    public int PluCode { get; set; }
    
    [MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;
    
    [MaxLength(20)]
    public string Status { get; set; } = string.Empty; // Exitoso, Fallo
    
    public string? ErrorMessage { get; set; }
    
    [MaxLength(50)]
    public string BatchId { get; set; } = string.Empty; // Guid para agrupar el envío
}
