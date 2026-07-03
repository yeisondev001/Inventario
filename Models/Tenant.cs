using System.Text.Json.Serialization;

namespace InventarioApi.Models;

public class Tenant
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Slug { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool Active { get; set; } = true;

    [JsonIgnore]
    public List<AppUser> Users { get; set; } = new();

    [JsonIgnore]
    public List<Category> Categories { get; set; } = new();

    [JsonIgnore]
    public List<Warehouse> Warehouses { get; set; } = new();

    [JsonIgnore]
    public List<Product> Products { get; set; } = new();
}