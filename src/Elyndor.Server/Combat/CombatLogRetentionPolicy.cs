using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Content;
using Elyndor.Core.Monsters;

namespace Elyndor.Server.Combat;

/// <summary>
/// Archive and export limits are deliberately separate from the combat-session
/// reconnect buffer and from the authoritative cumulative combat statistics.
/// </summary>
internal static class CombatLogRetentionPolicy
{
    public const int NormalArchiveEvents = 5_000;
    public const int BossOrDungeonArchiveEvents = 10_000;
    public const int StandardExportEvents = 10_000;
    public const int FullExportMaxBytes = 40 * 1024 * 1024;

    public static int ArchiveLimit(
        CombatSessionSnapshot snapshot,
        GameContentSnapshot? pinnedContent)
    {
        IReadOnlyList<CombatActorSnapshot> enemies = snapshot.Enemies ?? [snapshot.Enemy];
        if (enemies.Any(enemy => enemy.MonsterRank == MonsterRank.Boss))
            return BossOrDungeonArchiveEvents;

        // Dungeon encounters cannot be distinguished from world encounters by rank
        // alone. Use the content version pinned to the combat session.
        bool isDungeon = pinnedContent?.Package.Dungeons?.Any(dungeon =>
            dungeon.Encounters.Any(encounter => enemies.Any(enemy =>
                string.Equals(enemy.DefinitionId, encounter.MonsterId,
                    StringComparison.Ordinal)))) == true;

        return isDungeon ? BossOrDungeonArchiveEvents : NormalArchiveEvents;
    }
}
