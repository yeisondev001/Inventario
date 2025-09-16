namespace InventarioApi.Models;

public enum MovementType { In = 1, Out = 2 }

public class InventoryMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public MovementType Type { get; set; }     // In / Out
    public decimal Quantity { get; set; }      // usa decimal
    public DateTime MovementDate { get; set; } = DateTime.UtcNow;
    public string? Reference { get; set; }     // factura/OC/usuario, etc.
}
