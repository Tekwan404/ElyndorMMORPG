# Combat UI V3 and Targeting V1 Design

## Goal

Make the mobile combat screen read as a compact dark-fantasy MMORPG battlefield: allies on the left, enemies on the right, an aggro frontline in between, a fixed 12-slot action bar, and independent hostile and friendly player selections.

## Scope and constraints

- Branch from current `main`; do not merge or deploy from this work.
- Reuse the merged PR #156 local per-character hotbar preference. Extend its active area from six to twelve slots; do not add a second storage key.
- Do not change damage, crit, armor, block, shield, threat values, resources, boss stats, loot, XP, AFK, dungeon state machine, or database schema.
- Autoattack and Escape are fixed system controls outside the editable hotbar.
- The server remains authoritative for every ability target and combat result. Client selection is intent and presentation only.

## Existing state and issue

`CombatSession` already keeps `SelectedTargetActorId` on each `CombatPlayerRuntimeState`, normalizes it after an enemy dies, and projects it into the requesting participant's snapshot. `UseAbility` nevertheless reaches the session with `Guid.Empty` and therefore ignores the command's existing `TargetActorId`; `SingleAlly` has no command-target resolution yet. The feature must extend this existing model rather than create a second hostile-selection system.

PR #156 already provides `combatHotbarSettings.ts` with per-character localStorage normalization and `CombatHotbarSettings.vue`, but the active display is limited to six entries.

## Chosen architecture

### Per-participant targeting

Each player runtime state already owns its current hostile target and `SelectTarget` already changes it through the combat session's single writer. The player command carries an explicit optional target actor id. The combat session resolves and validates that intent using the ability's existing `AbilityTargetType`:

- `Self`, companion, and area target types ignore a selected target where appropriate.
- `SingleEnemy` accepts only a living enemy participant.
- `SingleAlly` accepts only a living active ally, respecting `AllowSelfTarget`.
- Invalid, dead, or vanished targets are rejected authoritatively with the existing invalid-target result.

For `SingleEnemy`, an empty command target falls back to the participant's existing selected hostile target; an explicit valid enemy becomes that participant's selected hostile target before the ability executes, preserving automatic attack behavior. The server already exposes the requesting participant's target in its snapshot. Target choice stays session-runtime state, not a database entity.

Frontend `useCombatSessionStore` reuses the authoritative `snapshot.selectedTargetActorId` as `selectedHostileTargetId` and owns only `selectedFriendlyTargetId` locally. It normalizes friendly selection whenever an authoritative snapshot changes: a vanished/dead friendly falls back to the local player. The existing combat session already normalizes hostile selection. SignalR updates never erase a still-valid choice or local hotbar order.

### Aggro presentation

Aggro is distinct from player selection. Each enemy snapshot gains an optional current aggro target actor id derived from the existing threat runtime data; no new threat calculation is introduced. The UI moves that ally into the visual frontline for that enemy, adds a labelled AGGRO marker and a lightweight 150–250 ms transition. Different enemies can point at different allies.

### Combat UI composition

Break the oversized combat view into focused components under the existing combat module:

- `CombatBattlefield`: composes the field and owns no combat calculations.
- `CombatPartyPanel` and member unit: compact interactive ally list, including local player, friendly-selection and aggro state.
- `CombatEnemyPanel` and enemy unit: interactive enemy list, hostile selection, HP/dead/cast states and per-enemy telegraph.
- `CombatHotbar` and slot: exactly twelve stable slots in a 2 × 6 grid, including empty slots, icon, keyboard number, cooldown, resource-disabled, and server-authoritative reactive availability styling.
- `CombatSystemActions`: Autoattack and Escape controls; Escape uses the existing confirmation and flee flow.

The components receive data and emit intent. The existing store remains the only SignalR/API owner. CSS uses current tokens and mobile `clamp()` sizing, keeps slots at roughly 44–56px across 320–430px widths, and avoids a full-screen dashboard/card layout.

### Hotbar preferences

Keep `elyndor:combat-hotbar:v1:<characterId>`. Normalization continues to remove stale IDs, remove duplicates, and append newly-known abilities. The active range changes to positions 1–12. Settings render two rows of twelve positional cells (2 × 6) plus reserve entries; tapping one occupied entry then another swaps them. Empty active positions exist even when a character knows fewer than twelve abilities. Autoattack and Escape are never represented in this preference.

## Data flow

1. User taps an ally or enemy card. The store updates only the corresponding friendly or hostile selection, leaving the other untouched.
2. User taps an ability. The UI determines the intended target from the server-provided ability target type and sends `UseAbility(sessionId, abilityId, targetActorId, commandId)` once.
3. The single-writer combat session validates the target from live participants, executes the existing ability pipeline, and emits its authoritative update.
4. The mapper returns participant-specific target selection and enemy aggro target IDs. The client applies the update, preserves valid selection/order, and only normalizes invalid selections.

## Failure and recovery behavior

- A fast repeat ability tap uses the existing store command guard; only one mutation is in flight.
- Server target rejection preserves the previous valid UI selection and shows the existing combat error path.
- Reconnect restores the authoritative snapshot; client-only friendly selection is normalized, not persisted to the server or database.
- No polling, deep snapshot watchers, per-slot timers, or canvas effects are added. The existing single time source drives cooldown and cast presentation.

## Verification

Add backend unit/integration coverage for per-participant hostile target ownership and ability target validation. Add frontend unit coverage for independent target selection, normalization, 12-slot ordering, duplicate tap prevention, cooldown/resource/reactive states, and system action behavior. Add accessible data attributes and Playwright/mobile checks for 320, 360, 390, and 430px widths, party/enemy selection, 12 slots, fixed controls, and horizontal overflow. Capture requested solo, party, aggro, targeting, and 320px screenshots when a Docker-backed browser environment is available.

## Non-goals

No new healing or support ability content is added in this slice. The `SingleAlly` command path is made real and extensible, but its gameplay is only exercised by existing/test content. No full threat-table UI, target synchronization across devices, target persistence after a completed combat, or redesigned global navigation is included.
