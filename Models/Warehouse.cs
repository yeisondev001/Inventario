using System.Text.Json.Serialization;

namespace InventarioApi.Models;

public class Warehouse
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Location { get; set; }

    [JsonIgnore]                
    public List<InventoryMovement> Movements { get; set; } = new();
}
