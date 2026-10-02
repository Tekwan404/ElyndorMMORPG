using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Elyndor.UnitTests.Persistence;

public sealed class MigrationModelTests
{
    [Fact]
    public void GameDbContextHasNoPendingModelChanges()
    {
        DbContextOptions<GameDbContext> options = new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql("Host=localhost;Database=elyndor_model_check;Username=postgres;Password=postgres")
            .Options;

        using var db = new GameDbContext(options);
        IMigrationsAssembly migrations = db.GetService<IMigrationsAssembly>();
        IMigrationsModelDiffer differ = db.GetService<IMigrationsModelDiffer>();
        IDesignTimeModel designTimeModel = db.GetService<IDesignTimeModel>();

        Assert.NotNull(migrations.ModelSnapshot);

        IReadOnlyList<MigrationOperation> operations = differ.GetDifferences(
            migrations.ModelSnapshot!.Model.GetRelationalModel(),
            designTimeModel.Model.GetRelationalModel());

        Assert.True(
            operations.Count == 0,
            "Pending EF model operations:" + Environment.NewLine
            + string.Join(Environment.NewLine, operations.Select(Describe)));
    }

    private static string Describe(MigrationOperation operation)
    {
        Type type = operation.GetType();
        IEnumerable<string> details = type
            .GetProperties()
            .Where(property => property.GetIndexParameters().Length == 0
                && property.Name is "Name" or "Table" or "Schema" or "ColumnType"
                && property.GetValue(operation) is not null)
            .Select(property => $"{property.Name}={property.GetValue(operation)}");

        return $"{type.Name}: {string.Join(", ", details)}";
    }
}
