using System.Reflection;
using Elyndor.Contracts.Combat;
using Elyndor.Core.Combat;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.Combat;
using Elyndor.Server.Combat;

namespace Elyndor.IntegrationTests.Combat;

public sealed class CombatRealtimeEventFilteringTests
{
    [Fact]
    public void RealtimePayloadKeepsCompleteSequencedEventStream()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Guid actorId = Guid.CreateVersion7();
        CombatEvent[] events =
        [
            new(CombatEventType.ResourceChanged, now, actorId, "COMBAT_REGEN", 0.25m, SourceActorId: actorId, TargetActorId: actorId, Sequence: 1),
            new(CombatEventType.ResourceChanged, now, actorId, "MAGE_FIREBALL", -20m, SourceActorId: actorId, TargetActorId: actorId, Sequence: 2),
            new(CombatEventType.AbilityStarted, now, actorId, "MAGE_FIREBALL", SourceActorId: actorId, Sequence: 3)
        ];
        CombatOperationResult result = new(true, null, null, events);
        GameContentPackage content = new("test-content", "test-balance", now, [], []);

        Type mapperType = typeof(CombatHub).Assembly.GetType("Elyndor.Server.Combat.CombatContractMapper", throwOnError: true)!;
        MethodInfo method = mapperType.GetMethod(
            "ToResponse",
            BindingFlags.Static | BindingFlags.Public,
            binder: null,
            types: [typeof(CombatOperationResult), typeof(GameContentPackage)],
            modifiers: null)!;

        CombatUpdateResponse response = (CombatUpdateResponse)method.Invoke(null, [result, content])!;

        Assert.Equal(3, response.Events.Count);
        Assert.Equal([1, 2, 3], response.Events.Select(item => item.Sequence));
        Assert.Contains(response.Events, item => item.DefinitionId == "COMBAT_REGEN");
        Assert.Contains(response.Events, item => item.DefinitionId == "MAGE_FIREBALL" && item.Type == CombatEventType.ResourceChanged.ToString());
        Assert.Contains(response.Events, item => item.DefinitionId == "MAGE_FIREBALL" && item.Type == CombatEventType.AbilityStarted.ToString());
    }
}
