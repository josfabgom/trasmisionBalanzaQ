namespace BalanzaQ.Web.Models;

public class PluItem
{
    public int Id { get; set; }
    public int PluCode { get; set; }
    public string ShortName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Group { get; set; }
    public int Section { get; set; }
    public decimal Price { get; set; }
    public int LabelFormat { get; set; }
    public string ItemType { get; set; } = "P"; // P=Pesable, N=Unidad
    public int RawType { get; set; } // 1 for Weighted, other for Unit

    // Nueva trazabilidad por artículo
    public string? LastSyncStatus { get; set; }
    public string? LastSyncError { get; set; }
    public DateTime? LastSyncDate { get; set; }

    public int ShelfLife { get; set; }
    public int BarcodeFormat { get; set; } // Nuevo: permite elegir formato de barras
    public bool IsSyncronized { get; set; } = false;
}

