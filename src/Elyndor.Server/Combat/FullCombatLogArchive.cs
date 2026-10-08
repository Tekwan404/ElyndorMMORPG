using System.Collections.Concurrent;
using System.Text;
using Elyndor.Core.Combat;
using Elyndor.Core.Combat.Sessions;

namespace Elyndor.Server.Combat;

/// <summary>
/// Explicitly armed developer-only capture. One copy per combat session (not
/// per party member). Nothing is written to the database or collected by default.
/// </summary>
internal static class FullCombatLogArchive
{
    private const int MaxConcurrentSessions = 2;
    private const int MaxEstimatedEventBytes = CombatLogRetentionPolicy.FullExportMaxBytes - (1024 * 1024);
    private static readonly TimeSpan ArmLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ArchiveLifetime = TimeSpan.FromMinutes(45);
    private static readonly ConcurrentDictionary<Guid, DateTimeOffset> ArmedAccounts = [];
    private static readonly ConcurrentDictionary<Guid, FullEntry> Sessions = [];

    public static void Arm(Guid accountId, DateTimeOffset nowUtc)
    {
        if (accountId == Guid.Empty)
            throw new ArgumentException("Account is required.", nameof(accountId));

        Purge(nowUtc);
        ArmedAccounts[accountId] = nowUtc;
    }

    public static void Capture(
        Guid accountId,
        CombatSessionSnapshot snapshot,
        IReadOnlyList<CombatEvent> events,
        DateTimeOffset nowUtc)
    {
        Purge(nowUtc);

        if (ArmedAccounts.TryGetValue(accountId, out DateTimeOffset armedAt))
        {
            ArmedAccounts.TryRemove(accountId, out _);
            if (nowUtc - armedAt <= ArmLifetime)
            {
                // Do not evict an ongoing capture to start a third enormous log.
                if (Sessions.ContainsKey(snapshot.SessionId)
                    || Sessions.Count < MaxConcurrentSessions)
                {
                    Sessions.GetOrAdd(
                        snapshot.SessionId,
                        _ => new FullEntry(nowUtc, events.FirstOrDefault()?.Sequence ?? snapshot.Sequence));
                }
            }
        }

        if (!Sessions.TryGetValue(snapshot.SessionId, out FullEntry? entry))
            return;

        lock (entry.Gate)
        {
            foreach (CombatEvent combatEvent in events)
            {
                if (entry.Events.ContainsKey(combatEvent.Sequence))
                    continue; // A party update may be published once per account.

                int estimatedBytes = EstimateBytes(combatEvent);
                if (entry.EstimatedBytes > MaxEstimatedEventBytes - estimatedBytes)
                {
                    entry.SizeLimitReached = true;
                    continue;
                }

                entry.Events.Add(combatEvent.Sequence, combatEvent);
                entry.EstimatedBytes += estimatedBytes;
            }
            entry.UpdatedAtUtc = nowUtc;
        }
    }

    public static FullCombatLogSnapshot? Read(Guid sessionId, DateTimeOffset nowUtc)
    {
        Purge(nowUtc);
        if (!Sessions.TryGetValue(sessionId, out FullEntry? entry))
            return null;

        lock (entry.Gate)
        {
            entry.UpdatedAtUtc = nowUtc;
            return new FullCombatLogSnapshot(
                entry.Events.Values.ToArray(),
                entry.SizeLimitReached,
                entry.FirstCapturedSequence);
        }
    }

    private static int EstimateBytes(CombatEvent combatEvent) =>
        320 + Encoding.UTF8.GetByteCount(combatEvent.DefinitionId ?? "")
            + Encoding.UTF8.GetByteCount(combatEvent.WeaponDefinitionId ?? "");

    private static void Purge(DateTimeOffset nowUtc)
    {
        foreach (KeyValuePair<Guid, DateTimeOffset> armed in ArmedAccounts)
        {
            if (nowUtc - armed.Value > ArmLifetime)
                ArmedAccounts.TryRemove(armed.Key, out _);
        }

        foreach (KeyValuePair<Guid, FullEntry> pair in Sessions)
        {
            DateTimeOffset updatedAtUtc;
            lock (pair.Value.Gate)
                updatedAtUtc = pair.Value.UpdatedAtUtc;
            if (nowUtc - updatedAtUtc > ArchiveLifetime)
                Sessions.TryRemove(pair.Key, out _);
        }
    }

    private sealed class FullEntry(DateTimeOffset nowUtc, long firstSequence)
    {
        public object Gate { get; } = new();
        public SortedDictionary<long, CombatEvent> Events { get; } = [];
        public DateTimeOffset UpdatedAtUtc { get; set; } = nowUtc;
        public long FirstCapturedSequence { get; } = firstSequence;
        public int EstimatedBytes { get; set; }
        public bool SizeLimitReached { get; set; }
    }
}

internal sealed record FullCombatLogSnapshot(
    CombatEvent[] Events,
    bool SizeLimitReached,
    long FirstCapturedSequence);
