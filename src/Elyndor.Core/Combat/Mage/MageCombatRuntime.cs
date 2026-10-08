using Elyndor.Core.Combat.Abilities;
using Elyndor.Core.Combat.Damage;
using Elyndor.Core.Combat.Effects;
using Elyndor.Core.Combat.Randomness;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Talents;

namespace Elyndor.Core.Combat.Mage;

internal sealed class MageCombatRuntime
{
    internal MageCombatRuntime(MageCombatContext context)
    {
        Fire = new(context, this);
        Arcane = new(context, this);
        Frost = new(context, this);
    }
    internal FireMageRuntime Fire { get; }
    internal ArcaneMageRuntime Arcane { get; }
    internal FrostMageRuntime Frost { get; }
    internal void Sync(DateTimeOffset now)
    {
        Arcane.SyncMageConditionalEffects(now);
        Frost.SyncConditionalEffects(now);
    }
    internal DateTimeOffset? NextDueAt => Arcane.NextDueAt is not { } arcane ? Frost.NextDueAt
        : Frost.NextDueAt is not { } frost ? arcane : arcane < frost ? arcane : frost;
}
