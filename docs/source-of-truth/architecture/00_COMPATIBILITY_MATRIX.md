# Elyndor — System Ownership & Runtime Integration Matrix

**Status:** Engineering / Architecture Source of Truth  
**Purpose:** показать владельца состояния и runtime-интеграции без ложного представления, что игровые документы являются отдельными assemblies/microservices.

> `Integrates with` — runtime/data contract interaction, **не compile-time dependency graph**. Elyndor.Core остаётся одним проектом с module boundaries.

| System | Owns | Integrates with |
|---|---|---|
| 01 Time | Server time, absolute timers, TimeProvider policy | all time-based modules |
| 02 Combat | CombatSession, participants, Auto Attack runtime, Threat runtime | 05,06,07,08,09,10,15,18,20,21 |
| 03 AFK | AFK session/profile/reward accrual | 01,04,05,11,14 |
| 04 World | Locations, territory rules, world presence context | 01,02,03,05,15,18,21 |
| 05 Character | Character identity, life/presence/activity state | 04,07,11,12,13,19,20,21 |
| 06 Stats | Approved stats, modifier pipeline, final stat calculation | 02,07,08,09,12,13,16,21 |
| 07 Resource | HP and Action Resources: Mana/Rage/Energy/Focus | 01,02,05,06,08,10,12,16,21 |
| 08 Effect | Buff/Debuff/DoT/HoT/Control/Shield/Lethal Prevention lifecycle | 01,02,06,07,09,10,16,21 |
| 09 Damage | Hit/Dodge/Crit/Damage/Healing/Mitigation/Shield resolution | 02,06,07,08,10,21 |
| 10 Ability | Ability definitions, cast/cooldown/GCD/target validation | 01,02,07,08,09,12,16,20,21 |
| 11 Progression | Level, XP, level-up state | 05,12,17,20 |
| 12 Class | ClassDefinition, base/growth/equipment/ability content references | 06,07,10,13,16,19,21 |
| 13 Items | ItemDefinition/Instance, inventory, equipment, affixes, sets | 05,06,12,14,24 |
| 14 Loot | Loot rolls, generated item reward, Pending Loot, reward idempotency | 13,17,18,20 |
| 15 Monster AI | Monster definitions and hostile decision policy | 02,04,10,18 |
| 16 Talents | Talent definitions, ranks, prerequisites, 2 loadouts | 06,07,08,10,12,13,21,22,23,25 |
| 17 Quest | QuestDefinition/State/objectives/claim orchestration | 04,11,14,18,20 |
| 18 Boss | Boss/world-event lifecycle, participation timeline | 02,04,14,15,20 |
| 19 Creation | Character creation input rules and playable roster UX contract | 05,12,21 |
| 20 Party | Party membership, invites, leader, Party Ally context | 02,10,11,14,17,18 |
| 21 Companion | Companion ownership, collection, life/recovery/AI/scaling | 02,05,06,07,08,09,10,12,16 |
| 22 Warrior Tree | Warrior talent content | 16,20 |
| 23 Archer Tree | Archer talent content | 07,16,21 |
| 24 Gear 5–30 | Early equipment content | 13,22,23,25 |
| 25 Mage Tree | Mage talent content: Fire / Arcane / Frost | 06,07,08,09,10,16 |

| 26 Economy | CurrencyDefinition, Wallet, ledger, reservations, merchants | 03,13,14,16,17,18,27,28,29 |
| 27 Trade/Auction | TradeSession, AuctionListing, auction escrow orchestration | 01,05,13,26,29 |
| 28 Dungeon | DungeonDefinition/Instance, member snapshot, encounter/checkpoint/lockout | 01,02,04,05,14,15,17,18,20,26 |
| 29 Crafting/Professions | Profession state, recipes, craft operations | 01,05,13,17,26,27 |
| 30 Guild | Guild identity, membership, ranks, permissions, XP, bank/chat/task state | 05,11,13,26,28,29 |
| 31 Raid Group | Organized raid membership, subgroups, leader/assistant, ready checks | 02,05,18,20 |

## Ownership rules

1. Только owner-system мутирует своё authoritative state.
2. Другой module получает данные через query/read model/context snapshot или domain/application contract.
3. `ClassDefinition` может хранить `TalentTreeId`/`CompanionProfileId`, но Class System не владеет talent ranks или Companion life state.
4. Combat владеет runtime participant context; Party владеет membership; Companion владеет pet identity/life state.
5. Ability возвращает валидированный action/result intent в Combat pipeline и не становится вторым владельцем CombatSession.
6. Character может хранить ссылки/summary, но не дублирует inventory/quest/talent/party authoritative collections.
7. Все cross-module writes с durable side effects проходят transaction/idempotency/outbox policy из engineering stack.
8. Economy owns balances/ledger; Trade/Auction/Crafting only request currency operations.
9. Item System owns ItemInstance/bind/escrow state; Trade/Auction/Crafting orchestrate transitions through Item contracts.
10. Dungeon owns instance membership/checkpoints/lockouts; Party owns live Party membership; Boss/Combat own their runtime states.
11. Guild owns persistent guild membership/ranks/permissions; Party/Raid membership is independent.
12. Raid Group owns organized raid roster/subgroups/ready-check state; Boss/Combat still own encounter runtime and reward eligibility.

## Compile-time rule

`01–21` и `26–31` — foundation/gameplay/social modules; `22–25` — class/item content documents внутри `Elyndor.Core`. Это один modular monolith, а не набор assemblies/microservices. Поэтому эта таблица не должна использоваться для построения project-reference DAG.

## Visual equipment ownership

`13 Items` owns equipped ItemInstance and `AppearanceProfileId` mapping. UI/renderer consumes confirmed equipment state; presentation never owns gameplay stats.
