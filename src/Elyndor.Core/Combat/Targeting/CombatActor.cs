namespace Elyndor.Core.Combat.Targeting;

public enum CombatActorSide
{
    Friendly,
    Hostile
}

public sealed record CombatActor(Guid ActorId, CombatActorSide Side);
