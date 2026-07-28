using MajakServer.Repositories.MySQL;
using MajakServer.Repositories.MySQL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MajakServer.Tests;

public class MySqlLogMappingTests
{
    [Fact]
    public void BillingItemMaster_UsesNeutralTableName()
    {
        var options = new DbContextOptionsBuilder<GameDataContext>()
            .UseMySql(
                "Server=localhost;Database=test;User=test;Password=test;",
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        using var context = new GameDataContext(options);

        var entity = context.Model.FindEntityType(typeof(BillingItemMasterEntity));

        Assert.NotNull(entity);
        Assert.Equal("billing_item_master", entity.GetTableName());
    }

    [Fact]
    public void ItemPurchaseLog_UsesMigratedTableAndColumns()
    {
        var options = new DbContextOptionsBuilder<LogDataContext>()
            .UseMySql(
                "Server=localhost;Database=test;User=test;Password=test;",
                new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        using var context = new LogDataContext(options);

        var entity = context.Model.FindEntityType(typeof(ItemPurchaseLogEntity));
        Assert.NotNull(entity);
        Assert.Equal("item_purchase_log", entity.GetTableName());

        var table = StoreObjectIdentifier.Table("item_purchase_log", null);
        Assert.Equal("item_purchase_id", entity.FindProperty(nameof(ItemPurchaseLogEntity.ItemPurchaseId))!.GetColumnName(table));
        Assert.Equal("purchased_at", entity.FindProperty(nameof(ItemPurchaseLogEntity.PurchasedAt))!.GetColumnName(table));
        Assert.Equal("member_no", entity.FindProperty(nameof(ItemPurchaseLogEntity.MemberNo))!.GetColumnName(table));
        Assert.Equal("item_code", entity.FindProperty(nameof(ItemPurchaseLogEntity.ItemCode))!.GetColumnName(table));
        Assert.Equal("purchase_channel", entity.FindProperty(nameof(ItemPurchaseLogEntity.PurchaseChannel))!.GetColumnName(table));
    }
}