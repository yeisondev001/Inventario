namespace InventarioApi.Models;

public enum MovementType { In = 1, Out = 2 }

public class InventoryMovement
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public MovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public DateTime MovementDate { get; set; } = DateTime.UtcNow;
    public string? Reference { get; set; }
}