using System.Text.Json.Serialization;

namespace InventarioApi.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;

    [JsonIgnore]                   
    public List<Product> Products { get; set; } = new();
}
