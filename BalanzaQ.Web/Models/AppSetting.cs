using System.ComponentModel.DataAnnotations;

namespace BalanzaQ.Web.Models;

public class AppSetting
{
    [Key]
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
