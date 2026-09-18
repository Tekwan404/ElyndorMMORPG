from pathlib import Path

path = Path("src/Elyndor.Core/Combat/Abilities/AbilityEngine.cs")
text = path.read_text(encoding="utf-8")
replacements = [
    (
        """        if (ability.TargetType is not (AbilityTargetType.Self
            or AbilityTargetType.SingleAlly
            or AbilityTargetType.SingleEnemy
            or AbilityTargetType.AllEnemiesInCombat
            or AbilityTargetType.NEnemiesInCombat
            or AbilityTargetType.SelfAndPartyMembersInCombat))
            return AbilityErrorCode.InvalidTarget;""",
        """        if (ability.TargetType is not (AbilityTargetType.Self
            or AbilityTargetType.SingleAlly
            or AbilityTargetType.SingleEnemy
            or AbilityTargetType.AllEnemiesInCombat
            or AbilityTargetType.NEnemiesInCombat
            or AbilityTargetType.SelfAndPartyMembersInCombat
            or AbilityTargetType.Owner))
            return AbilityErrorCode.InvalidTarget;""",
    ),
    (
        """        if (ability.TargetType == AbilityTargetType.SingleAlly
            && (targetIds.Length != 1
                || targetIds[0] == runtime.Actor.ActorId && !ability.AllowSelfTarget))
            return AbilityErrorCode.InvalidTarget;
        if (ability.TargetType is AbilityTargetType.AllEnemiesInCombat""",
        """        if (ability.TargetType == AbilityTargetType.SingleAlly
            && (targetIds.Length != 1
                || targetIds[0] == runtime.Actor.ActorId && !ability.AllowSelfTarget))
            return AbilityErrorCode.InvalidTarget;
        if (ability.TargetType == AbilityTargetType.Owner
            && (targetIds.Length != 1 || targetIds[0] == runtime.Actor.ActorId))
            return AbilityErrorCode.InvalidTarget;
        if (ability.TargetType is AbilityTargetType.AllEnemiesInCombat""",
    ),
]
for index, (old, new) in enumerate(replacements, start=1):
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"Patch fragment {index} expected exactly once, found {count}.")
    text = text.replace(old, new, 1)
path.write_text(text, encoding="utf-8")
