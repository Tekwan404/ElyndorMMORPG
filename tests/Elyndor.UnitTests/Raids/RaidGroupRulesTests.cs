using Elyndor.Core.Raids;
using Elyndor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.UnitTests.Raids;

public sealed class RaidGroupRulesTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RaidAllowsTwentyMembersAndRejectsTwentyFirst()
    {
        RaidGroup raid = RaidGroup.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            RaidGroup.DefaultMaximumMembers,
            CreatedAt);

        for (var index = 0; index < RaidGroup.DefaultMaximumMembers - 1; index++)
            raid.AddMember(Guid.NewGuid(), CreatedAt.AddMinutes(index + 1));

        Assert.Equal(RaidGroup.DefaultMaximumMembers, raid.Members.Count);
        Assert.Throws<InvalidOperationException>(() =>
            raid.AddMember(Guid.NewGuid(), CreatedAt.AddMinutes(30)));
    }

    [Fact]
    public void RaidDoesNotExposeSubgroupState()
    {
        Assert.DoesNotContain(
            typeof(RaidGroup).GetProperties(),
            property => property.Name.Contains("Subgroup", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RaidMembershipHasUniqueCharacterConstraint()
    {
        DbContextOptions<GameDbContext> options = new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql("Host=localhost;Database=elyndor_raid_model_test")
            .Options;
        using var dbContext = new GameDbContext(options);

        var memberEntity = dbContext.Model.FindEntityType(typeof(RaidMember));

        Assert.NotNull(memberEntity);
        Assert.Contains(memberEntity!.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Count == 1
            && index.Properties[0].Name == nameof(RaidMember.CharacterId));
    }

    [Fact]
    public void RaidPersistenceIncludesInvitesAndReadyChecks()
    {
        DbContextOptions<GameDbContext> options = new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql("Host=localhost;Database=elyndor_raid_model_test")
            .Options;
        using var dbContext = new GameDbContext(options);

        Assert.NotNull(dbContext.Model.FindEntityType(typeof(RaidInvite)));
        Assert.NotNull(dbContext.Model.FindEntityType(typeof(RaidReadyCheck)));
    }
}
