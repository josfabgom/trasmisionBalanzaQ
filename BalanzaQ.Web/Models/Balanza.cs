namespace BalanzaQ.Web.Models;

public class Balanza
{
    public int Id { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string LastSyncStatus { get; set; } = "Nunca sincronizado";
    public DateTime? LastSyncDate { get; set; }
}
