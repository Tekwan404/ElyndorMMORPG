using Elyndor.Contracts.Combat;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;
using Elyndor.Infrastructure.Combat;

namespace Elyndor.Server.Combat;

internal static class CombatContractMapper
{
    private const string CombatRegenDefinitionId = "COMBAT_REGEN";

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
        result.Events
            .Where(ShouldPublishRealtimeEvent)
            .Select(ToResponse)
            .ToArray(),
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

    private static bool ShouldPublishRealtimeEvent(CombatEvent combatEvent) =>
        combatEvent.Type != CombatEventType.ResourceChanged
        || !string.Equals(
            combatEvent.DefinitionId,
            CombatRegenDefinitionId,
            StringComparison.Ordinal);

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
                AbilityDefinition? definition = (content.Abilities ?? []).SingleOrDefault(candidate =>
                    string.Equals(candidate.Id, ability.Id, StringComparison.Ordinal));
                return new CombatAbilityResponse(
                    ability.Id,
                    definition?.DisplayName ?? ability.Id,
                    ResolveAbilityDescription(
                        definition,
                        ability.ResourceCost,
                        ability.Cooldown,
                        ability.TargetType),
                    definition?.IconId,
                    ability.ResourceCost,
                    ability.Cooldown.TotalSeconds,
                    definition?.Actions?.Any(action => action.IsUnblockable) == true,
                    ability.TargetType.ToString());
            }).ToArray(),
            actor.Effects.Select(effect => new CombatEffectResponse(
                effect.Id,
                effect.Stacks,
                effect.ExpiresAtUtc,
                effect.DisplayName,
                effect.Description,
                effect.IconId)).ToArray(),
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
            actor.NextAutoAttackAtUtc,
            actor.CurrentAggroTargetActorId);
    }

    private static string ResolveAbilityDescription(
        AbilityDefinition? definition,
        decimal resourceCost,
        TimeSpan cooldown,
        AbilityTargetType targetType)
    {
        if (!string.IsNullOrWhiteSpace(definition?.Description))
            return definition.Description.Trim();

        List<string> parts = [];
        foreach (AbilityActionDefinition action in definition?.Actions ?? [])
        {
            string? part = action.Type switch
            {
                AbilityActionType.Damage => "Наносит урон цели.",
                AbilityActionType.Healing => "Восстанавливает здоровье.",
                AbilityActionType.ApplyEffect => "Накладывает боевой эффект.",
                AbilityActionType.ResourceChange => action.Amount >= 0
                    ? "Восстанавливает ресурс."
                    : "Уменьшает ресурс цели.",
                AbilityActionType.Dispel => "Снимает эффект.",
                AbilityActionType.Taunt => "Заставляет противника атаковать персонажа.",
                AbilityActionType.Interrupt => "Прерывает применение способности противника.",
                AbilityActionType.AddThreat => "Повышает угрозу.",
                AbilityActionType.DropThreatPercent => "Снижает угрозу.",
                AbilityActionType.ClearThreat => "Сбрасывает угрозу.",
                AbilityActionType.Fixate => "Фиксирует внимание противника на выбранной цели.",
                AbilityActionType.TemporaryUntargetable => "Временно делает цель недоступной для атак.",
                _ => null
            };
            if (part is not null && !parts.Contains(part, StringComparer.Ordinal))
                parts.Add(part);
        }

        if (parts.Count == 0)
        {
            parts.Add(targetType switch
            {
                AbilityTargetType.Self => "Применяется к персонажу.",
                AbilityTargetType.SingleAlly => "Применяется к выбранному союзнику.",
                AbilityTargetType.SingleEnemy => "Применяется к выбранному противнику.",
                AbilityTargetType.AllEnemiesInCombat => "Воздействует на всех противников в бою.",
                AbilityTargetType.NEnemiesInCombat => "Воздействует на несколько противников в бою.",
                AbilityTargetType.SelfAndPartyMembersInCombat => "Воздействует на персонажа и союзников в бою.",
                AbilityTargetType.ActiveCompanion => "Применяется к активному спутнику.",
                AbilityTargetType.Owner => "Применяется к владельцу.",
                _ => "Боевая способность."
            });
        }

        if (resourceCost > 0)
            parts.Add($"Стоимость: {decimal.Round(resourceCost, 0)} ед. ресурса.");
        if (cooldown > TimeSpan.Zero)
        {
            decimal seconds = decimal.Round((decimal)cooldown.TotalSeconds, 1);
            parts.Add($"Восстановление: {seconds:0.#} сек.");
        }

        return string.Join(" ", parts);
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
        combatEvent.WeaponDefinitionId,
        combatEvent.RawDamage,
        combatEvent.DamageAfterMitigation,
        combatEvent.DamageBeforeBlock,
        combatEvent.IsUnblockable);
}
