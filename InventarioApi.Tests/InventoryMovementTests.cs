using InventarioApi.Models;
using Xunit;

namespace InventarioApi.Tests;

public class InventoryMovementTests
{
    [Fact]
    public void InventoryMovement_ShouldHaveDefaultMovementDate()
    {
        var movement = new InventoryMovement();
        Assert.NotEqual(default(DateTime), movement.MovementDate);
    }

    [Fact]
    public void MovementType_In_ShouldHaveValue1()
    {
        Assert.Equal(1, (int)MovementType.In);
    }

    [Fact]
    public void MovementType_Out_ShouldHaveValue2()
    {
        Assert.Equal(2, (int)MovementType.Out);
    }
}
