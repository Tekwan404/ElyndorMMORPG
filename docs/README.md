# Documentation

## Running and changing the game

- [Local setup](development/getting-started.md), [production runbook](deployment/vps-production.md).
- [Contribution rules](../CONTRIBUTING.md), [Git workflow](development/git-workflow.md).
- [Repository ownership and maintenance](development/repository-layout.md).
- [Specifications index](source-of-truth/00_MASTER_PROJECT_INDEX.md).

## Current engineering notes

- Combat: [event routing](development/combat-event-routing.md),
  [ability composition](development/ability-modifier-composition.md),
  [resource orchestration](development/combat-resource-runtime.md),
  [proc safety](development/proc-safety.md),
  [PvP player mechanics](development/pvp-player-mechanics-parity.md),
  [arena invitations](development/arena-friend-invitations.md).
- Items/economy: [itemization](development/itemization-v2-enhancement.md),
  [equipment sets and budgets](development/equipment-set-progression.md),
  [item special effects](development/item-special-effects.md),
  [spatial inventory](development/spatial-inventory-v1.md),
  [money denominations](development/money-denominations.md),
  [commerce settlement](development/player-commerce-settlement.md).
- Frontend: [UI kit](development/elyndor-ui-kit.md), [UI migration audit](development/ui-kit-audit.md).

## Document lifecycle

| Folder | Meaning |
| --- | --- |
| `source-of-truth/` | Gameplay/UI/architecture contracts. Historical phase checkpoints remain labelled by phase; verify runtime status independently. |
| `development/` | Engineering notes for existing implementations and reproducible workflows. |
| `deployment/` | Operations and production runbooks. |
| `plans/` | Current task plans, not new gameplay authority. |
| `design/` | Pending proposals; not evidence that a feature exists. |
| `archive/` | Earlier plans, design briefs, inputs and point-in-time audits. |

The [archive guide](archive/README.md) explains what was retained and why. When a
plan is completed or superseded, archive it and replace operational instructions
with concise notes in `development/`. Do not delete historical contracts merely
because they describe a feature outside the current task.
