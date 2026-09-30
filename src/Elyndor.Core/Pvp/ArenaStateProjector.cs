using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Sessions;

namespace Elyndor.Core.Pvp;

/// <summary>Builds the per-participant view (existing BattleScreen contract) of an arena session.</summary>
public static class ArenaStateProjector
{
    public static ArenaTestState Project(ArenaTestEntrant local, ArenaTestEntrant opponent,
        bool localIsFirst, ArenaCombatSession session, DateTimeOffset now, long afterSequence)
    {
        ArenaCombatSnapshot snapshot = session.Snapshot;
        CombatActorSnapshot player = ActorView(local, session);
        CombatActorSnapshot enemy = ActorView(opponent, session);
        CombatSessionStatus battleStatus = snapshot.Outcome switch
        {
            ArenaMatchOutcome.Active => CombatSessionStatus.Active,
            ArenaMatchOutcome.Draw or ArenaMatchOutcome.Cancelled => CombatSessionStatus.Cancelled,
            ArenaMatchOutcome.WinnerA when localIsFirst => CombatSessionStatus.Victory,
            ArenaMatchOutcome.WinnerB when !localIsFirst => CombatSessionStatus.Victory,
            _ => CombatSessionStatus.Defeat
        };
        var battle = new CombatSessionSnapshot(snapshot.MatchId, snapshot.Sequence,
            battleStatus, now, player, enemy, Enemies: [enemy],
            SelectedTargetActorId: enemy.ActorId);
        return new ArenaTestState(snapshot.Outcome == ArenaMatchOutcome.Active
                ? ArenaTestStatus.Active : ArenaTestStatus.Completed,
            snapshot.MatchId, local.Fighter.CharacterId, opponent.Fighter.CharacterId,
            opponent.Name,
            localIsFirst ? snapshot.ActorA.CurrentHp : snapshot.ActorB.CurrentHp,
            localIsFirst ? snapshot.ActorB.CurrentHp : snapshot.ActorA.CurrentHp,
            snapshot.Outcome, snapshot.Sequence, session.GetEventsAfter(afterSequence), battle);
    }

    private static CombatActorSnapshot ActorView(ArenaTestEntrant entrant, ArenaCombatSession session)
    {
        ArenaFighter fighter = entrant.Fighter;
        ArenaActorSnapshot actor = session.Snapshot.ActorA.ActorId == fighter.Actor.ActorId
            ? session.Snapshot.ActorA : session.Snapshot.ActorB;
        var cast = session.ActiveCastFor(fighter.AccountId);
        return new CombatActorSnapshot(actor.ActorId, CombatActorKind.Player,
            entrant.ClassId, entrant.Name, actor.CurrentHp, actor.MaxHp, entrant.ResourceType,
            actor.CurrentResource, actor.MaxResource, true, null,
            session.CooldownsFor(fighter.AccountId),
            new HashSet<string>(fighter.Abilities.Keys, StringComparer.Ordinal),
            fighter.Abilities.Values.Select(ability => new CombatAbilitySnapshot(ability.Id,
                ability.ResourceCost, ability.Cooldown, ability.TargetType)).ToArray(),
            fighter.Actor.ActiveEffects.Select(effect => new CombatEffectSnapshot(
                effect.Definition.Id, effect.Stacks, effect.ExpiresAtUtc,
                effect.Definition.DisplayName, effect.Definition.Description,
                effect.Definition.IconId)).ToArray(),
            cast is null ? null : new CombatCastSnapshot(cast.Ability.Id,
                cast.StartedAtUtc, cast.ResolvesAtUtc),
            GenderId: entrant.GenderId, SkinId: entrant.SkinId, Level: entrant.Level);
    }
}
