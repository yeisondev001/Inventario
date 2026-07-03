using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using InventarioApi.Models;

namespace InventarioApi.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // Precisión para dinero/cantidades
        mb.Entity<Product>().Property(p => p.PurchasePrice).HasColumnType("decimal(18,2)");
        mb.Entity<Product>().Property(p => p.UnitPrice).HasColumnType("decimal(18,2)");
        mb.Entity<InventoryMovement>().Property(m => m.Quantity).HasColumnType("decimal(18,2)");

        // Relaciones Tenant
        mb.Entity<AppUser>()
          .HasOne(u => u.Tenant)
          .WithMany(t => t.Users)
          .HasForeignKey(u => u.TenantId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Category>()
          .HasOne(c => c.Tenant)
          .WithMany(t => t.Categories)
          .HasForeignKey(c => c.TenantId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Warehouse>()
          .HasOne(w => w.Tenant)
          .WithMany(t => t.Warehouses)
          .HasForeignKey(w => w.TenantId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Product>()
          .HasOne(p => p.Tenant)
          .WithMany(t => t.Products)
          .HasForeignKey(p => p.TenantId)
          .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<InventoryMovement>()
          .HasOne(m => m.Tenant)
          .WithMany()
          .HasForeignKey(m => m.TenantId)
          .OnDelete(DeleteBehavior.Restrict);

        // SKU único POR TENANT (no global)
        mb.Entity<Product>().HasIndex(p => new { p.TenantId, p.SKU }).IsUnique();

        // Relaciones existentes
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
    }
}