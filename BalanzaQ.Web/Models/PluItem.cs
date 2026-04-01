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
    public int ShelfLife { get; set; }
    public bool IsSyncronized { get; set; } = false;
}
