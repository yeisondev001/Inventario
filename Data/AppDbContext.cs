using Microsoft.EntityFrameworkCore;
using InventarioApi.Models;

namespace InventarioApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Precisión para dinero/cantidades
        mb.Entity<Product>().Property(p => p.UnitPrice).HasColumnType("decimal(18,2)");
        mb.Entity<InventoryMovement>().Property(m => m.Quantity).HasColumnType("decimal(18,2)");

        // SKU único
        mb.Entity<Product>().HasIndex(p => p.SKU).IsUnique();

        // Relaciones
        mb.Entity<Product>()
          .HasOne(p => p.Category)
          .WithMany(c => c.Products)
          .HasForeignKey(p => p.CategoryId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<InventoryMovement>()
          .HasOne(m => m.Product)
          .WithMany(p => p.Movements)
          .HasForeignKey(m => m.ProductId);

        mb.Entity<InventoryMovement>()
          .HasOne(m => m.Warehouse)
          .WithMany(w => w.Movements)
          .HasForeignKey(m => m.WarehouseId);

        // Datos semilla mínimos
        mb.Entity<Category>().HasData(new Category { Id = 1, Name = "General" });
        mb.Entity<Warehouse>().HasData(new Warehouse { Id = 1, Name = "Principal", Location = "Matriz" });
        mb.Entity<Product>().HasData(new Product { Id = 1, SKU = "P-0001", Name = "Producto demo", CategoryId = 1, UnitPrice = 100 });
    }
}
