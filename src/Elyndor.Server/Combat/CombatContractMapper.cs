using Elyndor.Contracts.Combat;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;
using Elyndor.Infrastructure.Combat;

namespace Elyndor.Server.Combat;

internal static class CombatContractMapper
{
    public static CombatUpdateResponse ToResponse(
        CombatOperationResult result,
        GameContentPackage fallbackContent)
    {
        GameContentPackage content =
            result.ContentSnapshot?.Package ?? fallbackContent;
        return new CombatUpdateResponse(
        result.Succeeded,
        result.ErrorCode,
        result.Snapshot is null ? null : ToResponse(result.Snapshot, content),
        result.Events.Select(ToResponse).ToArray(),
        result.Reward?.Progression is null
            ? null
            : new CombatRewardResponse(
                result.Reward.XpEarned,
                result.Reward.GoldEarned,
                result.Reward.Progression.LeveledUp,
                result.Reward.Progression.PreviousLevel,
                result.Reward.Progression.CurrentLevel,
                result.Reward.Items.Select(item => new CombatRewardItemResponse(
                    item.ItemId,
                    item.Name,
                    item.Type.ToString(),
                    item.Rarity.ToString(),
                    item.Quantity)).ToArray(),
                result.Reward.CompletedContractIds,
                result.Reward.LootRolls?.Select(roll => new CombatLootRollResponse(
                    roll.LootRollId,
                    roll.ItemId,
                    roll.Name,
                    roll.Rarity.ToString(),
                    roll.Quantity,
                    roll.EndsAtUtc,
                    roll.EligibleCharacterIds,
                    roll.CanNeed)).ToArray()));
    }

    private static CombatSnapshotResponse ToResponse(
        CombatSessionSnapshot snapshot,
        GameContentPackage content)
    {
        CombatActorResponse selected = ToResponse(snapshot.Enemy, content);
        CombatActorResponse[] enemies = (snapshot.Enemies ?? new[] { snapshot.Enemy })
            .Select(enemy => ToResponse(enemy, content))
            .ToArray();
        return new CombatSnapshotResponse(
            snapshot.SessionId,
            snapshot.Sequence,
            snapshot.Status.ToString(),
            snapshot.ServerTimeUtc,
            ToResponse(snapshot.Player, content),
            selected,
            snapshot.ContentVersion,
            snapshot.BalanceVersion,
            enemies,
            snapshot.SelectedTargetActorId ?? snapshot.Enemy.ActorId,
            snapshot.Companion is null ? null : ToResponse(snapshot.Companion, content),
            snapshot.PlayerContribution is null
                ? null
                : new CombatContributionResponse(
                    snapshot.PlayerContribution.CharacterId,
                    snapshot.PlayerContribution.QualifyingActions,
                    snapshot.PlayerContribution.DamageDealt,
                    snapshot.PlayerContribution.EffectiveHealing,
                    snapshot.PlayerContribution.SupportContribution,
                    snapshot.PlayerContribution.TankingContribution,
                    snapshot.PlayerContribution.JoinedAtUtc,
                    snapshot.PlayerContribution.FledAtUtc,
                    snapshot.PlayerContribution.DiedAtUtc),
            snapshot.Players?.Select(player => ToResponse(player, content)).ToArray(),
            snapshot.ParticipantRoster?.Select(participant => new CombatParticipantResponse(
                participant.AccountId,
                participant.CharacterId,
                participant.ActorId,
                participant.Status.ToString(),
                participant.RosteredAtUtc,
                participant.JoinedAtUtc,
                participant.FledAtUtc,
                participant.DiedAtUtc)).ToArray(),
            snapshot.PlayerContributionEligible,
            snapshot.ParticipantContributions?.Select(item =>
                new CombatParticipantContributionResponse(
                    new CombatContributionResponse(
                        item.Snapshot.CharacterId,
                        item.Snapshot.QualifyingActions,
                        item.Snapshot.DamageDealt,
                        item.Snapshot.EffectiveHealing,
                        item.Snapshot.SupportContribution,
                        item.Snapshot.TankingContribution,
                        item.Snapshot.JoinedAtUtc,
                        item.Snapshot.FledAtUtc,
                        item.Snapshot.DiedAtUtc),
                    item.IsEligible,
                    item.Reason,
                    item.ContributionScore)).ToArray());
    }

    private static CombatActorResponse ToResponse(
        CombatActorSnapshot actor,
        GameContentPackage content)
    {
        MonsterDefinition? monster = actor.Kind == CombatActorKind.Monster
            ? content.Monsters?.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, actor.DefinitionId, StringComparison.Ordinal))
            : null;

        return new CombatActorResponse(
            actor.ActorId,
            actor.Kind.ToString(),
            actor.DefinitionId,
            actor.Name,
            actor.Hp,
            actor.MaxHp,
            actor.ResourceType,
            actor.Resource,
            actor.MaxResource,
            actor.AutoAttackEnabled,
            actor.Cooldowns,
            actor.KnownAbilityIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            actor.Abilities.Select(ability =>
            {
                var definition = (content.Abilities ?? []).SingleOrDefault(candidate =>
                    string.Equals(candidate.Id, ability.Id, StringComparison.Ordinal));
                return new CombatAbilityResponse(
                    ability.Id,
                    definition?.DisplayName ?? ability.Id,
                    definition?.Description ?? string.Empty,
                    definition?.IconId,
                    ability.ResourceCost,
                    ability.Cooldown.TotalSeconds);
            }).ToArray(),
            actor.Effects.Select(effect => new CombatEffectResponse(
                effect.Id, effect.Stacks, effect.ExpiresAtUtc)).ToArray(),
            monster?.Level ?? 1,
            monster?.ArtId,
            actor.ActiveCast is null
                ? null
                : new CombatCastResponse(
                    actor.ActiveCast.AbilityId,
                    actor.ActiveCast.StartedAtUtc,
                    actor.ActiveCast.ResolvesAtUtc),
            actor.ConsumableCooldowns,
            actor.AutoAttackIntervalSeconds,
            actor.NextAutoAttackAtUtc);
    }

    private static CombatEventResponse ToResponse(CombatEvent combatEvent) => new(
        combatEvent.Sequence,
        combatEvent.Type.ToString(),
        combatEvent.ActorId,
        combatEvent.SourceActorId,
        combatEvent.TargetActorId,
        combatEvent.DefinitionId,
        combatEvent.Amount,
        combatEvent.OccurredAtUtc,
        combatEvent.AmountBeforeShields,
        combatEvent.WeaponHand?.ToString(),
        combatEvent.WeaponDefinitionId);
}
