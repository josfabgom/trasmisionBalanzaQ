namespace BalanzaQ.Web.Models;

public class LabelField
{
    public int FieldId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Font { get; set; }
}

public class LabelFormatInfo
{
    public string FileName { get; set; } = string.Empty;
    public int WidthLabel { get; set; } = 440; // Default 44mm (units in dots/0.1mm)
    public int HeightLabel { get; set; } = 550; // Default 55mm
    public List<LabelField> Fields { get; set; } = new();
}
