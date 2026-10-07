using Elyndor.Core.Combat;
using Elyndor.Core.Combat.SetPassives;
using Elyndor.Core.Content;
using Elyndor.Core.Items;

namespace Elyndor.UnitTests.Combat;

public sealed class SetProgressionRuntimeTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TwoCopiesOfTheSameSetRingDoNotGrantTwoPieceStats()
    {
        var ring = new ItemDefinition("SET_RING", "Ring", ItemType.Equipment, ItemRarity.Epic,
            60, false, 1, EquipmentSlot.Ring1, new PrimaryStats(0, 0, 0, 0), "Ring", 1, SetId: "SET_TEST");
        var set = new EquipmentSetDefinition("SET_TEST", "Set", [new(2, MaxHpFlat: 100)]);
        var result = EquipmentStatModifierResolver.ResolveDetailed([ring, ring with { Slot = EquipmentSlot.Ring2 }], [set]);
        Assert.Empty(result.ActiveSetBonuses);
    }

    [Fact]
    public void ResourceWindowIgnoresGainsReplaysAndCooldownSpending()
    {
        Guid actor = Guid.NewGuid();
        SetPassiveDefinition effect = new("RESOURCE_TEST", "SET_TEST", 6,
            new(CombatEventType.ResourceChanged, SetPassiveActorRole.Source),
            new(InternalCooldown: TimeSpan.FromSeconds(6), ResourceSpent: 40,
                SpendingWindow: TimeSpan.FromSeconds(8)),
            [new(SetPassiveActionKind.AddShield, "SHIELD", Magnitude: 10, Duration: TimeSpan.FromSeconds(2))]);
        var runtime = new SetPassiveRuntime([effect]);
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, int>> pieces =
            new Dictionary<Guid, IReadOnlyDictionary<string, int>> { [actor] = new Dictionary<string, int> { ["SET_TEST"] = 6 } };
        CombatEvent Spend(decimal amount, int seconds) => new(CombatEventType.ResourceChanged,
            Start.AddSeconds(seconds), actor, Amount: amount, SourceActorId: actor);
        Assert.Empty(runtime.Evaluate(Spend(50, 0), pieces));
        var first = Spend(-20, 0);
        Assert.Empty(runtime.Evaluate(first, pieces));
        Assert.Empty(runtime.Evaluate(first, pieces));
        Assert.Single(runtime.Evaluate(Spend(-20, 1), pieces));
        Assert.Empty(runtime.Evaluate(Spend(-40, 2), pieces));
        Assert.Empty(runtime.Evaluate(Spend(-20, 7), pieces));
        Assert.Empty(runtime.Evaluate(Spend(-20, 16), pieces));
        Assert.Single(runtime.Evaluate(Spend(-20, 17), pieces));
    }

    [Fact]
    public void ChargesMatchAbilityExpireAndConsumeOnlyOnce()
    {
        var charges = new SetPassiveCharges();
        var action = new SetPassiveActionDefinition(SetPassiveActionKind.EmpowerNextDirect,
            "NEXT_SHOT", Magnitude: .20m, Duration: TimeSpan.FromSeconds(5), AbilityIds: ["AIMED_SHOT"]);
        charges.Arm(action, Start);
        Assert.Equal(1, charges.Consume("OTHER", false, Start));
        Assert.Equal(1.20m, charges.Consume("AIMED_SHOT", false, Start));
        Assert.Equal(1, charges.Consume("AIMED_SHOT", false, Start));
        charges.Arm(action, Start);
        Assert.Equal(1, charges.Consume("AIMED_SHOT", false, Start.AddSeconds(5)));
    }
}
