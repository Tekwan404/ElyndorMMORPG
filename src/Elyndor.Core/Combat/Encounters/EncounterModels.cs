namespace Elyndor.Core.Combat.Encounters;

public enum EncounterTriggerType
{
    CombatStart,
    HpAtOrBelow,
    ElapsedTime,
    AddDeath,
    ResourceAtOrBelow,
    ResourceAtOrAbove,
    EffectExpired,
    CastInterrupted,
    PhaseStart
}

public enum EncounterActionType
{
    Summon,
    ApplyEffect,
    ChangeAbilitySet,
    Shield,
    Vulnerability,
    ResourceChange,
    SetPhase
}

public static class EncounterTargetSelectors
{
    public const string Boss = "BOSS";
    public const string CurrentTarget = "CURRENT_TARGET";
    public const string RandomPartyMember = "RANDOM_PARTY_MEMBER";
    public const string AllPartyMembers = "ALL_PARTY_MEMBERS";
    public const string LowestHpPartyMember = "LOWEST_HP_PARTY_MEMBER";

    private static readonly HashSet<string> Supported = new(StringComparer.Ordinal)
    {
        Boss,
        CurrentTarget,
        RandomPartyMember,
        AllPartyMembers,
        LowestHpPartyMember
    };

    public static bool IsSupported(string? selector) =>
        string.IsNullOrWhiteSpace(selector) || Supported.Contains(selector);
}

public sealed record SummonDefinition(
    string MonsterId,
    int Count,
    int MaxActive = 0,
    TimeSpan? Lifetime = null,
    bool LinkToCaster = false,
    bool DespawnOnBossDeath = true,
    bool NoReward = true);

public sealed record EncounterTriggerDefinition(
    EncounterTriggerType Type,
    decimal Threshold = 0,
    TimeSpan? Elapsed = null,
    string? DefinitionId = null,
    string? PhaseId = null,
    bool Once = true);

public sealed record EncounterActionDefinition(
    EncounterActionType Type,
    SummonDefinition? Summon = null,
    string? EffectId = null,
    IReadOnlyList<string>? AbilityIds = null,
    decimal Magnitude = 0,
    TimeSpan? Duration = null,
    decimal ResourceAmount = 0,
    string? PhaseId = null,
    string? TargetSelector = null,
    TimeSpan? Delay = null);

public sealed record EncounterPhaseDefinition(
    string Id,
    EncounterTriggerDefinition Trigger,
    IReadOnlyList<EncounterActionDefinition> Actions,
    IReadOnlyList<string>? AbilityIds = null,
    string? DisplayName = null);

public sealed record EncounterDefinition(
    string Id,
    string MonsterId,
    IReadOnlyList<EncounterPhaseDefinition> Phases,
    int Version = 1,
    string? DisplayName = null);

public sealed record EncounterRuntimeSnapshot(
    decimal CurrentHp,
    decimal MaxHp,
    decimal CurrentResource,
    decimal MaxResource,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset Now,
    string? CurrentPhaseId = null,
    string? EventDefinitionId = null,
    EncounterTriggerType? EventType = null)
{
    public decimal HpPercent => MaxHp <= 0 ? 0 : CurrentHp / MaxHp * 100m;
    public decimal ResourcePercent => MaxResource <= 0 ? 0 : CurrentResource / MaxResource * 100m;
}

public static class EncounterDefinitionValidator
{
    public static IReadOnlyList<string> Validate(EncounterDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(definition.Id))
            errors.Add("Encounter id is required.");
        if (string.IsNullOrWhiteSpace(definition.MonsterId))
            errors.Add("Encounter monster id is required.");
        if (definition.Version <= 0)
            errors.Add("Encounter version must be positive.");
        if (definition.Phases.Count == 0)
            errors.Add("Encounter requires at least one phase.");

        string[] duplicatePhaseIds = definition.Phases
            .Where(phase => !string.IsNullOrWhiteSpace(phase.Id))
            .GroupBy(phase => phase.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        foreach (string duplicate in duplicatePhaseIds)
            errors.Add($"Duplicate encounter phase id '{duplicate}'.");

        foreach (EncounterPhaseDefinition phase in definition.Phases)
        {
            if (string.IsNullOrWhiteSpace(phase.Id))
                errors.Add("Encounter phase id is required.");
            if (phase.Actions.Count == 0 && phase.AbilityIds is not { Count: > 0 })
                errors.Add($"Encounter phase '{phase.Id}' requires at least one action or ability set.");

            ValidateTrigger(phase.Id, phase.Trigger, errors);
            foreach (EncounterActionDefinition action in phase.Actions)
                ValidateAction(phase.Id, action, errors);
        }

        return errors;
    }

    private static void ValidateTrigger(
        string phaseId,
        EncounterTriggerDefinition trigger,
        List<string> errors)
    {
        if (trigger.Type == EncounterTriggerType.HpAtOrBelow
            && (trigger.Threshold < 0 || trigger.Threshold > 100))
        {
            errors.Add($"Phase '{phaseId}' HP threshold must be between 0 and 100.");
        }

        if (trigger.Type is EncounterTriggerType.ResourceAtOrBelow
            or EncounterTriggerType.ResourceAtOrAbove
            && (trigger.Threshold < 0 || trigger.Threshold > 100))
        {
            errors.Add($"Phase '{phaseId}' resource threshold must be between 0 and 100.");
        }

        if (trigger.Type == EncounterTriggerType.ElapsedTime
            && (trigger.Elapsed is null || trigger.Elapsed <= TimeSpan.Zero))
        {
            errors.Add($"Phase '{phaseId}' elapsed-time trigger requires a positive duration.");
        }

        if (!trigger.Once
            && trigger.Type is EncounterTriggerType.CombatStart
                or EncounterTriggerType.HpAtOrBelow
                or EncounterTriggerType.ElapsedTime
                or EncounterTriggerType.ResourceAtOrBelow
                or EncounterTriggerType.ResourceAtOrAbove
                or EncounterTriggerType.PhaseStart)
        {
            errors.Add(
                $"Phase '{phaseId}' state/time trigger must be once-per-combat; repeatable triggers require an encounter event.");
        }

        if (trigger.Type is EncounterTriggerType.AddDeath
            or EncounterTriggerType.EffectExpired
            or EncounterTriggerType.CastInterrupted
            && string.IsNullOrWhiteSpace(trigger.DefinitionId))
        {
            errors.Add($"Phase '{phaseId}' event trigger requires a definition id.");
        }

        if (trigger.Type == EncounterTriggerType.PhaseStart
            && string.IsNullOrWhiteSpace(trigger.PhaseId))
        {
            errors.Add($"Phase '{phaseId}' phase-start trigger requires a source phase id.");
        }
    }

    private static void ValidateAction(
        string phaseId,
        EncounterActionDefinition action,
        List<string> errors)
    {
        if (action.Delay is { } delay && delay < TimeSpan.Zero)
            errors.Add($"Phase '{phaseId}' action delay cannot be negative.");
        if (!EncounterTargetSelectors.IsSupported(action.TargetSelector))
        {
            errors.Add(
                $"Phase '{phaseId}' action target selector '{action.TargetSelector}' is not supported.");
        }

        switch (action.Type)
        {
            case EncounterActionType.Summon:
                if (action.Summon is null
                    || string.IsNullOrWhiteSpace(action.Summon.MonsterId)
                    || action.Summon.Count <= 0
                    || action.Summon.MaxActive < 0
                    || action.Summon.Lifetime is { } lifetime && lifetime <= TimeSpan.Zero)
                {
                    errors.Add($"Phase '{phaseId}' summon action is invalid.");
                }
                break;
            case EncounterActionType.ApplyEffect:
                if (string.IsNullOrWhiteSpace(action.EffectId))
                    errors.Add($"Phase '{phaseId}' apply-effect action requires an effect id.");
                break;
            case EncounterActionType.ChangeAbilitySet:
                if (action.AbilityIds is not { Count: > 0 }
                    || action.AbilityIds.Any(string.IsNullOrWhiteSpace))
                {
                    errors.Add($"Phase '{phaseId}' change-ability-set action requires ability ids.");
                }
                break;
            case EncounterActionType.Shield:
            case EncounterActionType.Vulnerability:
                if (action.Magnitude <= 0)
                    errors.Add($"Phase '{phaseId}' {action.Type} action requires positive magnitude.");
                if (action.Duration is { } duration && duration <= TimeSpan.Zero)
                    errors.Add($"Phase '{phaseId}' {action.Type} duration must be positive when specified.");
                break;
            case EncounterActionType.ResourceChange:
                if (action.ResourceAmount == 0)
                    errors.Add($"Phase '{phaseId}' resource-change action requires a non-zero amount.");
                break;
            case EncounterActionType.SetPhase:
                if (string.IsNullOrWhiteSpace(action.PhaseId))
                    errors.Add($"Phase '{phaseId}' set-phase action requires a target phase id.");
                break;
        }
    }
}
