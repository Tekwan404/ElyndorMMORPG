using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.Raids;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Raids;
using Elyndor.IntegrationTests.Postgres;

namespace Elyndor.IntegrationTests.Raids;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class RaidLifecycleServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 19, 4, 45, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task LeaderCanKickMember()
    {
        SeededCharacters seeded = await SeedCharactersAsync(2);
        await using GameDbContext context = postgres.CreateDbContext();
        RaidService service = new(context, new FixedTimeProvider(Now));
        await CreateAndJoinAsync(service, seeded, 1);

        RaidOperationResult kicked = await service.KickAsync(
            seeded.AccountIds[0],
            seeded.CharacterIds[1],
            CancellationToken.None);

        Assert.True(kicked.IsSuccess);
        Assert.Single(kicked.Snapshot!.Members);
        Assert.DoesNotContain(
            kicked.Snapshot.Members,
            member => member.CharacterId == seeded.CharacterIds[1]);
        Assert.Null(await service.GetAsync(seeded.AccountIds[1], CancellationToken.None));
    }

    [Fact]
    public async Task LeaderCanTransferLeadershipAndNewLeaderCanDisband()
    {
        SeededCharacters seeded = await SeedCharactersAsync(2);
        await using GameDbContext context = postgres.CreateDbContext();
        RaidService service = new(context, new FixedTimeProvider(Now));
        await CreateAndJoinAsync(service, seeded, 1);

        RaidOperationResult transferred = await service.TransferLeadershipAsync(
            seeded.AccountIds[0],
            seeded.CharacterIds[1],
            CancellationToken.None);
        RaidOperationResult disbanded = await service.DisbandAsync(
            seeded.AccountIds[1],
            CancellationToken.None);

        Assert.True(transferred.IsSuccess);
        Assert.Equal(seeded.CharacterIds[1], transferred.Snapshot!.LeaderCharacterId);
        Assert.Equal(
            RaidMemberRole.Member,
            transferred.Snapshot.Members.Single(member => member.CharacterId == seeded.CharacterIds[0]).Role);
        Assert.Equal(
            RaidMemberRole.Leader,
            transferred.Snapshot.Members.Single(member => member.CharacterId == seeded.CharacterIds[1]).Role);
        Assert.True(disbanded.IsSuccess);
        Assert.Equal(RaidState.Disbanded, disbanded.Snapshot!.State);
        Assert.Empty(disbanded.Snapshot.Members);
        Assert.Null(await service.GetAsync(seeded.AccountIds[0], CancellationToken.None));
        Assert.Null(await service.GetAsync(seeded.AccountIds[1], CancellationToken.None));
    }

    [Fact]
    public async Task DeclinedInviteCannotBeAccepted()
    {
        SeededCharacters seeded = await SeedCharactersAsync(2);
        await using GameDbContext context = postgres.CreateDbContext();
        RaidService service = new(context, new FixedTimeProvider(Now));
        Assert.True((await service.CreateAsync(
            seeded.AccountIds[0],
            Guid.NewGuid(),
            CancellationToken.None)).IsSuccess);
        RaidOperationResult invited = await service.InviteAsync(
            seeded.AccountIds[0],
            Guid.NewGuid(),
            seeded.CharacterIds[1],
            CancellationToken.None);

        RaidOperationResult declined = await service.DeclineInviteAsync(
            seeded.AccountIds[1],
            invited.Invite!.Id,
            CancellationToken.None);
        RaidOperationResult accepted = await service.AcceptInviteAsync(
            seeded.AccountIds[1],
            invited.Invite.Id,
            CancellationToken.None);

        Assert.True(declined.IsSuccess);
        Assert.Equal(RaidInviteStatus.Declined, declined.Invite!.Status);
        Assert.False(accepted.IsSuccess);
        Assert.Equal(RaidErrorCodes.InvalidState, accepted.ErrorCode);
    }

    [Fact]
    public async Task AcceptedInviteReplayReturnsCurrentRaidSnapshot()
    {
        SeededCharacters seeded = await SeedCharactersAsync(2);
        await using GameDbContext context = postgres.CreateDbContext();
        RaidService service = new(context, new FixedTimeProvider(Now));
        Assert.True((await service.CreateAsync(
            seeded.AccountIds[0],
            Guid.NewGuid(),
            CancellationToken.None)).IsSuccess);
        RaidOperationResult invited = await service.InviteAsync(
            seeded.AccountIds[0],
            Guid.NewGuid(),
            seeded.CharacterIds[1],
            CancellationToken.None);

        RaidOperationResult first = await service.AcceptInviteAsync(
            seeded.AccountIds[1],
            invited.Invite!.Id,
            CancellationToken.None);
        RaidOperationResult replay = await service.AcceptInviteAsync(
            seeded.AccountIds[1],
            invited.Invite.Id,
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Snapshot!.RaidId, replay.Snapshot!.RaidId);
        Assert.Equal(2, replay.Snapshot.Members.Count);
    }

    [Fact]
    public async Task AssistantCanBeginReadyCheckAndCapturedRosterCompletesIt()
    {
        SeededCharacters seeded = await SeedCharactersAsync(3);
        await using GameDbContext context = postgres.CreateDbContext();
        RaidService service = new(context, new FixedTimeProvider(Now));
        await CreateAndJoinAsync(service, seeded, 1, 2);
        Assert.True((await service.PromoteAssistantAsync(
            seeded.AccountIds[0],
            seeded.CharacterIds[1],
            CancellationToken.None)).IsSuccess);
        Guid readyCheckId = Guid.NewGuid();

        RaidOperationResult started = await service.BeginReadyCheckAsync(
            seeded.AccountIds[1],
            readyCheckId,
            CancellationToken.None);
        RaidOperationResult leaderReady = await service.SetReadyStateAsync(
            seeded.AccountIds[0],
            readyCheckId,
            RaidReadyState.Ready,
            CancellationToken.None);
        RaidOperationResult assistantReady = await service.SetReadyStateAsync(
            seeded.AccountIds[1],
            readyCheckId,
            RaidReadyState.Ready,
            CancellationToken.None);
        RaidOperationResult memberNotReady = await service.SetReadyStateAsync(
            seeded.AccountIds[2],
            readyCheckId,
            RaidReadyState.NotReady,
            CancellationToken.None);

        Assert.True(started.IsSuccess);
        Assert.Equal(RaidReadyCheckState.Open, started.Snapshot!.ReadyCheck!.State);
        Assert.True(leaderReady.IsSuccess);
        Assert.True(assistantReady.IsSuccess);
        Assert.True(memberNotReady.IsSuccess);
        Assert.Equal(RaidReadyCheckState.Completed, memberNotReady.Snapshot!.ReadyCheck!.State);
        Assert.Equal(
            RaidReadyState.NotReady,
            memberNotReady.Snapshot.Members.Single(member => member.CharacterId == seeded.CharacterIds[2]).ReadyState);
    }

    [Fact]
    public async Task OrdinaryMemberCannotBeginReadyCheck()
    {
        SeededCharacters seeded = await SeedCharactersAsync(2);
        await using GameDbContext context = postgres.CreateDbContext();
        RaidService service = new(context, new FixedTimeProvider(Now));
        await CreateAndJoinAsync(service, seeded, 1);

        RaidOperationResult result = await service.BeginReadyCheckAsync(
            seeded.AccountIds[1],
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(RaidErrorCodes.NotLeaderOrAssistant, result.ErrorCode);
    }

    private static async Task CreateAndJoinAsync(
        RaidService service,
        SeededCharacters seeded,
        params int[] memberIndexes)
    {
        Assert.True((await service.CreateAsync(
            seeded.AccountIds[0],
            Guid.NewGuid(),
            CancellationToken.None)).IsSuccess);
        foreach (int index in memberIndexes)
        {
            RaidOperationResult invited = await service.InviteAsync(
                seeded.AccountIds[0],
                Guid.NewGuid(),
                seeded.CharacterIds[index],
                CancellationToken.None);
            Assert.True(invited.IsSuccess);
            Assert.True((await service.AcceptInviteAsync(
                seeded.AccountIds[index],
                invited.Invite!.Id,
                CancellationToken.None)).IsSuccess);
        }
    }

    private async Task<SeededCharacters> SeedCharactersAsync(int count)
    {
        Guid[] accountIds = new Guid[count];
        Guid[] characterIds = new Guid[count];
        await using GameDbContext context = postgres.CreateDbContext();
        for (var index = 0; index < count; index++)
        {
            accountIds[index] = Guid.NewGuid();
            characterIds[index] = Guid.NewGuid();
            context.Accounts.Add(new Account(accountIds[index], 7000 + index, Now));
            context.Characters.Add(new Character(
                characterIds[index],
                accountIds[index],
                Guid.NewGuid(),
                $"RaidLife{index}",
                $"RAIDLIFE{index}",
                "HUMAN",
                index % 2 == 0 ? "MALE" : "FEMALE",
                index == 0 ? "WARRIOR" : "MAGE",
                Now));
        }
        await context.SaveChangesAsync();
        return new SeededCharacters(accountIds, characterIds);
    }

    private sealed record SeededCharacters(Guid[] AccountIds, Guid[] CharacterIds);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
