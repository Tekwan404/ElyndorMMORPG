using System.Data.Common;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Items;
using Elyndor.Core.Monsters;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Items;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Progression;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elyndor.IntegrationTests.Combat;

public sealed partial class CombatDurabilityServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReservationIsRefundedAfterCommitEvenWhenConnectionIsCancelledOrCommitThrows(bool throwAfterCommit)
    {
        var (ownerAccount, ownerCharacter) = await CreateCharacterAsync("PotionOwner");
        var (memberAccount, memberCharacter) = await CreateCharacterAsync("PotionMember");
        GameContentSnapshot content = GameContentSnapshot.Create(await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json")));
        CombatSession session = PotionSession(ownerCharacter, memberAccount, memberCharacter, content);
        await using (GameDbContext setup = postgres.CreateDbContext())
        {
            setup.ActiveCombatSessions.Add(new ActiveCombatSession(session.SessionId, memberCharacter,
                Now, content.Package.ContentVersion, content.Package.BalanceVersion));
            setup.CharacterItems.Add(new CharacterItem(Guid.CreateVersion7(), memberCharacter,
                "SMALL_HEALING_POTION", 2, Now));
            await setup.SaveChangesAsync();
        }

        using CancellationTokenSource disconnected = new();
        AfterCommitInterceptor interceptor = new(() =>
        {
            if (throwAfterCommit) throw new IOException("Simulated lost commit acknowledgment");
            session.Cancel(Now);
            disconnected.Cancel();
        });
        await using GameDbContext db = new(new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql(postgres.ConnectionString).AddInterceptors(interceptor).Options);
        using CombatSessionRegistry registry = PotionRegistry();
        Assert.True(registry.TryAdd(ownerAccount, ownerCharacter, session, content,
            [new(memberAccount, memberCharacter)]));
        CombatApplicationService application = PotionApplication(registry, db, content);

        if (throwAfterCommit)
            await Assert.ThrowsAsync<IOException>(() => application.UseConsumableAsync(memberAccount,
                session.SessionId, "refund", "SMALL_HEALING_POTION", disconnected.Token));
        else
        {
            CombatOperationResult result = await application.UseConsumableAsync(memberAccount,
                session.SessionId, "refund", "SMALL_HEALING_POTION", disconnected.Token);
            Assert.False(result.Succeeded);
            Assert.Equal(CombatErrorCodes.Ended, result.ErrorCode);
        }

        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal(2, await verify.CharacterItems.Where(item => item.CharacterId == memberCharacter
            && item.ItemDefinitionId == "SMALL_HEALING_POTION").SumAsync(item => item.Quantity));
        Assert.Empty(await verify.CombatConsumableUses.ToArrayAsync());
    }

    [Fact]
    public async Task MissingPotionReturnsRequestingMembersSnapshot()
    {
        var (ownerAccount, ownerCharacter) = await CreateCharacterAsync("EmptyOwner");
        var (memberAccount, memberCharacter) = await CreateCharacterAsync("EmptyMember");
        GameContentSnapshot content = GameContentSnapshot.Create(await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json")));
        CombatSession session = PotionSession(ownerCharacter, memberAccount, memberCharacter, content);
        await using GameDbContext db = postgres.CreateDbContext();
        db.ActiveCombatSessions.Add(new ActiveCombatSession(session.SessionId, memberCharacter,
            Now, content.Package.ContentVersion, content.Package.BalanceVersion));
        await db.SaveChangesAsync();
        using CombatSessionRegistry registry = PotionRegistry();
        Assert.True(registry.TryAdd(ownerAccount, ownerCharacter, session, content,
            [new(memberAccount, memberCharacter)]));
        CombatOperationResult result = await PotionApplication(registry, db, content).UseConsumableAsync(
            memberAccount, session.SessionId, "missing", "SMALL_HEALING_POTION", CancellationToken.None);
        Assert.Equal(CombatErrorCodes.CommandRejected, result.ErrorCode);
        Assert.Equal(memberCharacter, result.Snapshot!.Player.ActorId);
        Assert.Empty(await db.CombatConsumableUses.ToArrayAsync());
    }

    [Fact]
    public async Task PotionReplayDoesNotDebitAgainAndReceiptCannotBeUsedOrRefundedByAnotherMember()
    {
        var (ownerAccount, ownerCharacter) = await CreateCharacterAsync("ReplayOwner");
        var (memberAccount, memberCharacter) = await CreateCharacterAsync("ReplayMember");
        GameContentSnapshot content = GameContentSnapshot.Create(await GameContentPackageLoader.LoadAsync(
            Path.GetFullPath("content/package.json")));
        CombatSession session = PotionSession(ownerCharacter, memberAccount, memberCharacter, content);
        await using GameDbContext db = postgres.CreateDbContext();
        foreach (Guid characterId in new[] { ownerCharacter, memberCharacter })
        {
            db.ActiveCombatSessions.Add(new ActiveCombatSession(session.SessionId, characterId,
                Now, content.Package.ContentVersion, content.Package.BalanceVersion));
            db.CharacterItems.Add(new CharacterItem(Guid.CreateVersion7(), characterId, "SMALL_HEALING_POTION", 2, Now));
        }
        await db.SaveChangesAsync();
        using CombatSessionRegistry registry = PotionRegistry();
        Assert.True(registry.TryAdd(ownerAccount, ownerCharacter, session, content, [new(memberAccount, memberCharacter)]));
        CombatApplicationService application = PotionApplication(registry, db, content);
        CombatOperationResult first = await application.UseConsumableAsync(memberAccount, session.SessionId,
            "same-potion", "SMALL_HEALING_POTION", CancellationToken.None);
        Assert.True(first.Succeeded, first.ErrorCode);
        CombatOperationResult replay = await application.UseConsumableAsync(memberAccount, session.SessionId,
            "same-potion", "SMALL_HEALING_POTION", CancellationToken.None);
        Assert.True(replay.Succeeded, replay.ErrorCode);
        Assert.Equal(first.Snapshot!.Player.Hp, replay.Snapshot!.Player.Hp);
        Assert.Empty(replay.Events);
        Assert.Equal(CombatErrorCodes.DuplicateCommand, (await application.UseConsumableAsync(ownerAccount,
            session.SessionId, "same-potion", "SMALL_HEALING_POTION", CancellationToken.None)).ErrorCode);
        CombatDurabilityService durability = new(db, NullLogger<CombatDurabilityService>.Instance);
        Assert.Equal(InventoryErrorCodes.MutationConflict, await durability.ReserveConsumableAsync(ownerAccount,
            session.SessionId, "same-potion", "SMALL_HEALING_POTION", content, Now, CancellationToken.None));
        await durability.RollbackConsumableAsync(session.SessionId, "same-potion", ownerCharacter, CancellationToken.None);
        Assert.Single(await db.CombatConsumableUses.AsNoTracking().ToArrayAsync());
        Assert.Equal(1, await db.CharacterItems.Where(item => item.CharacterId == memberCharacter).SumAsync(item => item.Quantity));
        Assert.Equal(2, await db.CharacterItems.Where(item => item.CharacterId == ownerCharacter).SumAsync(item => item.Quantity));
    }

    private static CombatApplicationService PotionApplication(
        CombatSessionRegistry registry, GameDbContext db, GameContentSnapshot content)
    {
        StaticContentSnapshotProvider provider = new(content.Package);
        return new(null!, registry, null!, provider,
            new InventoryEquipmentService(db, provider, new PotionTimeProvider()),
            new CharacterOperationGuard(registry),
            new CombatDurabilityService(db, NullLogger<CombatDurabilityService>.Instance));
    }

    private static CombatSession PotionSession(Guid owner, Guid memberAccount, Guid member, GameContentSnapshot content)
    {
        static CombatParticipantDefinition Player(Guid id) => new(
            new CombatActorState(id, 1000, 100, 100, 0, CombatStats.Default),
            CombatActorKind.Player, "WARRIOR", "Player", "RAGE", new(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(), CanAutoAttack: false);
        CombatParticipantDefinition enemy = new(
            new CombatActorState(Guid.CreateVersion7(), 10000, 10000, 0, 0, CombatStats.Default),
            CombatActorKind.Monster, "TEST_ENEMY", "Enemy", "NONE", new(TimeSpan.FromHours(1), 0, 0, 0),
            new HashSet<string>(), CanAutoAttack: false);
        return new(Guid.CreateVersion7(), Player(owner), enemy, new Dictionary<string, AbilityDefinition>(),
            new MonsterAiProfile("PASSIVE", []), ResolvedTalentModifiers.Empty, new SeededGameRandom(1), Now,
            contentVersion: content.Package.ContentVersion, balanceVersion: content.Package.BalanceVersion,
            additionalPlayers: [new(memberAccount, Player(member), ResolvedTalentModifiers.Empty)]);
    }

    private static CombatSessionRegistry PotionRegistry() => new(new PotionTimeProvider(),
        new PotionPublisher(), new PotionFinalizer(), NullLogger<CombatSessionRegistry>.Instance);

    private sealed class AfterCommitInterceptor(Action callback) : DbTransactionInterceptor
    {
        private bool _fired;
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (!_fired) { _fired = true; callback(); }
            return Task.CompletedTask;
        }
    }

    private sealed class PotionTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => new IdleTimer();
        private sealed class IdleTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
    private sealed class PotionPublisher : ICombatUpdatePublisher
    {
        public Task PublishAsync(Guid accountId, CombatOperationResult update, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class PotionFinalizer : ICombatSessionFinalizer
    {
        public Task<CombatRewardApplicationResult?> FinalizeAsync(Guid characterId, CombatSessionSnapshot snapshot,
            CancellationToken cancellationToken) => Task.FromResult<CombatRewardApplicationResult?>(null);
    }
}
