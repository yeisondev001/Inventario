using System.Text.Json.Serialization;

namespace InventarioApi.Models;

public class Product
{
    public int Id { get; set; }
    public string SKU { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal UnitPrice { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    [JsonIgnore]                    
    public List<InventoryMovement> Movements { get; set; } = new();
}
