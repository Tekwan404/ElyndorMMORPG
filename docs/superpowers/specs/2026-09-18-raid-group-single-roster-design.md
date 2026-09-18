# Raid Group: single roster up to 20 players

## Decision

Elyndor raids use one `RaidGroup` roster with a default capacity of twenty characters. A raid with fewer members is still one raid: ten attendees form a ten-character roster. The initial implementation has no combat subgroups, no party-of-five assignment, and no subgroup UI.

The existing `Party` remains the five-character grouping used by ordinary co-op and dungeons. Raid is a separate context; it is not four Parties disguised as a raid.

## Why

The player expectation is a single shared boss fight: everyone present should receive applicable group buffs and participate in the same encounter. Five-person subgroups would introduce invisible restrictions, make raid support behavior surprising, and require an organisational UI before it has gameplay value.

## Scope of effects

The combat roster, rather than a subgroup, is the source of truth for raid-wide eligibility.

| Existing effect intent | Raid behavior |
| --- | --- |
| Self | Only the effect owner |
| Explicit single target | The selected valid target |
| Ally / party / group / raid | Every active, eligible participant in the same raid combat roster |

This mapping applies only inside a raid combat session. In ordinary Party and dungeon combat, current five-player behavior remains unchanged. The recipient set excludes characters who have not joined the encounter, fled, disconnected past their grace state, died if the encounter supports death, or otherwise ceased to be active combat participants.

The implementation must adapt the existing effect-targeting pipeline. It must not copy buffs manually per feature or introduce a parallel buff engine.

## Roster and encounter lifecycle

`RaidGroup` persists leader, members, state, capacity, timestamps, and a version/concurrency token. Its server-side mutation service owns invitations, accept/decline, removal, role changes, ready checks, and disbanding.

Starting a raid encounter captures the actual active participants into one `CombatParticipantRoster`. A raid-specific maximum of twenty is supplied by the encounter policy; the existing default five-player cap continues to protect non-raid combat. The captured combat roster is authoritative for effects, targeting, combat snapshots, contribution, and rewards. Changes to raid membership after capture do not retroactively alter an active encounter unless its existing join/leave policy explicitly allows it.

The encounter can accept fewer than twenty characters when its content policy permits it. It never creates placeholder members.

## Persistence, concurrency, and recovery

PostgreSQL is authoritative for `RaidGroup` membership and version. Each membership mutation is transactional, verifies capacity and conflicting Party/Raid membership, and increments the version. A unique membership constraint prevents a character from holding conflicting active group contexts.

Combat remains a single-writer session. Raid commands use the existing command/idempotency boundary; the client supplies an intent and operation id, never a combat result, roster, reward, or effect recipient list. Reconnect reads the durable raid and combat snapshots, while rewards remain governed by existing contribution and idempotent reward grant paths.

## Initial user flow

```text
Create raid → invite players → players accept → optional ready check
→ leader starts selected raid → eligible members enter one shared combat roster
→ all active members receive applicable group effects → contribution-based rewards
```

No subgroup management screen, drag-and-drop roster, or subgroup permissions are part of this slice.

## Non-goals

- No conversion of ordinary Party or dungeon runs to a twenty-player model.
- No separate generic buff engine.
- No auto-reward for being listed in the raid.
- No forced roster size or artificial five-player partitions.
- No cross-raid effect propagation.

## Verification

The implementation plan will cover tests for: a partially filled raid; capacity rejection; Party/Raid membership conflict; one shared combat roster; five-player limit retained outside raids; group effect reaching all active raid participants; exclusion after flee/disconnect; reward contribution gating; duplicate invitation/start/reward commands; and reconnect snapshot restoration.

## Follow-up extension point

If large-scale encounters later need organisational subgroups, they can be added as an optional presentation/assignment layer with an explicit design decision. They are not an implicit constraint in the initial data model, effect scope, or raid combat implementation.
