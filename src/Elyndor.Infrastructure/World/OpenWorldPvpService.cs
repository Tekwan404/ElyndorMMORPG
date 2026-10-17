using Elyndor.Core.Afk;
using Elyndor.Core.Pvp;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Dungeons;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Characters;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Persistence;
using Elyndor.Infrastructure.Pvp;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.Infrastructure.World;

public sealed record OpenWorldPlayer(Guid CharacterId, string Name, string RaceId, string ClassId,
    string GenderId, string? SkinId, int Level, bool InCombat);

public sealed record OpenWorldPlayersResult(bool Available, IReadOnlyList<OpenWorldPlayer> Players);

public sealed class OpenWorldPvpService(
    GameDbContext db, BootstrapService bootstrap, OpenWorldPresence presence,
    CharacterOperationGuard guard, CombatSessionRegistry registry, CombatSessionFactory factory,
    CombatDurabilityService durability, IContentSnapshotProvider contentProvider,
    ArenaMatchRuntime arena, ArenaTestRegistry arenaTests)
{
    public async Task<OpenWorldPlayersResult> GetPlayersAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var self = await bootstrap.GetAsync(accountId, cancellationToken);
        if (!Eligible(self)) return new(false, []);
        presence.Observe(accountId);
        Guid[] online = presence.ActiveAccounts().Concat(registry.OpenWorldAccounts()).Distinct().ToArray();
        var candidates = await (from character in db.Characters.AsNoTracking()
                                join location in db.CharacterLocations.AsNoTracking() on character.Id equals location.CharacterId
                                where character.AccountId != accountId && online.Contains(character.AccountId)
                                    && location.LocationId == OpenWorldPvpRules.LocationId && character.Level >= OpenWorldPvpRules.MinimumLevel
                                orderby character.Name, character.Id
                                select new
                                {
                                    character.Id,
                                    character.AccountId,
                                    character.Name,
                                    character.RaceId,
                                    character.ClassId,
                                    character.GenderId,
                                    character.ActiveSkinId,
                                    character.Level
                                })
            .Take(OpenWorldPvpRules.MaximumVisiblePlayers).ToArrayAsync(cancellationToken);
        return new(true, candidates.Select(player => new OpenWorldPlayer(player.Id, player.Name, player.RaceId,
            player.ClassId, player.GenderId, player.ActiveSkinId, player.Level,
            registry.IsOpenWorldCombat(player.AccountId))).ToArray());
    }

    public async Task<CombatOperationResult> EnterAsync(Guid accountId, Guid targetCharacterId,
        bool attackPlayer, CancellationToken cancellationToken)
    {
        var targetAccount = await db.Characters.AsNoTracking().Where(character => character.Id == targetCharacterId)
            .Select(character => (Guid?)character.AccountId).SingleOrDefaultAsync(cancellationToken);
        if (targetAccount is null || targetAccount == accountId)
            return CombatOperationResult.Failure("world_pvp_target_unavailable");

        using var admission = await guard.AcquireManyAsync([accountId, targetAccount.Value], cancellationToken);
        registry.ClearFinished(accountId);
        registry.ClearFinished(targetAccount.Value);
        if (registry.HasActiveCombat(accountId) || arena.IsInMatch(accountId) || arenaTests.Get(accountId) is not null
            || arena.IsInMatch(targetAccount.Value) || arenaTests.Get(targetAccount.Value) is not null)
            return CombatOperationResult.Failure(CombatErrorCodes.AlreadyActive);

        var content = contentProvider.GetCurrent();
        var self = await bootstrap.GetAsync(accountId, content, cancellationToken, checkpoint: true);
        var target = await bootstrap.GetAsync(targetAccount.Value, content, cancellationToken, checkpoint: true);
        if (!Eligible(self) || !Eligible(target) || self.Character!.Id == targetCharacterId
            || !presence.IsPresent(targetAccount.Value) && !registry.IsOpenWorldCombat(targetAccount.Value))
            return CombatOperationResult.Failure("world_pvp_target_unavailable");
        if (attackPlayer && self.Character!.RaceId == target.Character!.RaceId)
            return CombatOperationResult.Failure("world_pvp_friendly_target");
        Guid[] characters = [self.Character!.Id, targetCharacterId];
        if (await db.DungeonRuns.AnyAsync(run => run.State == DungeonRunState.Active
                && run.Members.Any(member => characters.Contains(member.CharacterId)
                    && member.State == DungeonRunMemberState.Active), cancellationToken))
            return CombatOperationResult.Failure("world_pvp_target_unavailable");
        presence.Observe(accountId);

        if (registry.IsOpenWorldCombat(targetAccount.Value))
            return await registry.JoinOpenWorldAsync(targetAccount.Value, accountId, self.Character.Id,
                async (pinned, token) => await factory.CreateArenaPlayerAsync(
                    await bootstrap.GetAsync(accountId, pinned, token, checkpoint: true), pinned, token),
                durability, attackPlayer ? targetCharacterId : null, cancellationToken);
        if (!attackPlayer || registry.HasActiveCombat(targetAccount.Value))
            return CombatOperationResult.Failure("world_pvp_target_unavailable");

        var attacker = await factory.CreateArenaPlayerAsync(self, content, cancellationToken);
        var defender = await factory.CreateArenaPlayerAsync(target, content, cancellationToken);
        var session = factory.CreateOpenWorldDuel(attacker, defender, content);
        bool admitted = false;
        try
        {
            foreach (var character in characters)
                if (!(await durability.BeginParticipantAsync(character, session.Snapshot(character), cancellationToken)).Succeeded)
                    return CombatOperationResult.Failure(CombatErrorCodes.AlreadyActive);
            if (!registry.TryAdd(accountId, attacker.Participant.Actor.ActorId, session, content,
                    [new(targetAccount.Value, targetCharacterId)], OpenWorldPvpRules.LocationId))
                return CombatOperationResult.Failure(CombatErrorCodes.AlreadyActive);
            admitted = true;
            await registry.PublishCurrentAsync(accountId, CancellationToken.None);
            return await registry.ResumeAsync(accountId, 0, cancellationToken);
        }
        finally
        {
            if (!admitted) await durability.CompleteAsync(session.SessionId, CancellationToken.None);
        }
    }

    private static bool Eligible(BootstrapSnapshot snapshot) => snapshot.Character is { Vitals.CurrentHp: > 0 } character
        && snapshot.World is { Travel: null } world
        && OpenWorldPvpRules.CanParticipate(world.CurrentLocation.Id, character.Level)
        && snapshot.AfkFarm?.Status != AfkFarmStatus.Active;
}
