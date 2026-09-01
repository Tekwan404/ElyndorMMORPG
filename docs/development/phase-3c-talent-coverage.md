# Phase 3C Warrior Talent Coverage

Generated from `content/package.json` by `scripts/update-phase3c-talent-content.mjs`.

- Nodes: 96
- Supported hooks: 35
- Deferred hooks: 69
- Deferred hooks remain data contracts until their owning phase supplies CombatSession, Party, Monster, Boss/Elite, or equipment runtime.

Phase 4A activates only G-1-2 ON_DAMAGE_TAKEN, B-3-1 ON_CRITICAL_HIT, and B-1-2 ON_ENEMY_KILLED through typed CombatSession hooks.

| ID | Branch | Talent | Runtime contracts | Icon |
| --- | --- | --- | --- | --- |
| G-1-1 | GUARDIAN | Iron Skin | Supported ARMOR_PERCENT | generated |
| G-1-2 | GUARDIAN | Combat Stance | Supported ON_DAMAGE_TAKEN | generated |
| G-1-3 | GUARDIAN | Endurance | Supported STAMINA_PERCENT | generated |
| G-1-4 | GUARDIAN | Heavy Presence | Deferred ON_AUTO_ATTACK -> COMBAT_SESSION | generated |
| G-2-1 | GUARDIAN | Shield Reflex | Supported DODGE_PERCENT<br>Deferred ON_HP_THRESHOLD -> COMBAT_SESSION | generated |
| G-2-2 | GUARDIAN | Provocateur | Deferred ON_ABILITY_USED -> COMBAT_SESSION | generated |
| G-2-3 | GUARDIAN | Thick Hide | Supported INCOMING_PHYSICAL_DAMAGE_REDUCTION_PERCENT | generated |
| G-2-4 | GUARDIAN | Front Line | Deferred ON_HP_THRESHOLD -> COMBAT_SESSION | generated |
| G-3-1 | GUARDIAN | Counterattack | Deferred ON_DODGE -> COMBAT_SESSION | generated |
| G-3-2 | GUARDIAN | Fortified Mind | Deferred ON_ABILITY_USED -> COMBAT_SESSION | generated |
| G-3-3 | GUARDIAN | Guardian's Rage | Supported MAX_RESOURCE_FLAT | generated |
| G-3-4 | GUARDIAN | Battle Hardened | Deferred ON_DAMAGE_TAKEN -> COMBAT_SESSION | generated |
| G-4-1 | GUARDIAN | Indomitable | Deferred ON_HP_THRESHOLD -> COMBAT_SESSION | generated |
| G-4-2 | GUARDIAN | Taunt Mastery | Supported ABILITY_RESOURCE_COST_FLAT:PROVOKE<br>Deferred ON_ABILITY_USED:PROVOKE -> COMBAT_SESSION | generated |
| G-4-3 | GUARDIAN | War Armor | Supported MAGIC_RESISTANCE_PERCENT | generated |
| G-4-4 | GUARDIAN | Defiant Fury | Deferred ON_DODGE -> COMBAT_SESSION | generated |
| G-5-1 | GUARDIAN | Bastion | Supported UNLOCK_ABILITY:BASTION | generated |
| G-5-2 | GUARDIAN | Threat Presence | Deferred ON_DAMAGE_TAKEN -> COMBAT_SESSION | generated |
| G-5-3 | GUARDIAN | Unyielding | Supported INCOMING_MAGICAL_DAMAGE_REDUCTION_PERCENT | generated |
| G-6-1 | GUARDIAN | Living Shield | Deferred ON_AUTO_ATTACK -> COMBAT_SESSION | generated |
| G-6-2 | GUARDIAN | Blood Armor | Deferred ON_HP_THRESHOLD -> COMBAT_SESSION | generated |
| G-6-3 | GUARDIAN | Iron Will | Deferred ON_ABILITY_USED -> COMBAT_SESSION | generated |
| G-6-4 | GUARDIAN | Reinforced Barriers | Deferred ON_ABILITY_USED -> COMBAT_SESSION | generated |
| G-7-1 | GUARDIAN | Immortal Warrior | Supported ABILITY_COOLDOWN_SECONDS:BASTION | generated |
| G-7-2 | GUARDIAN | Eternal Guard | Deferred ON_HP_THRESHOLD -> COMBAT_SESSION | generated |
| G-7-3 | GUARDIAN | Perpetual Endurance | Supported MAX_HP_PERCENT | generated |
| G-7-4 | GUARDIAN | Last Stand | Deferred ON_HP_THRESHOLD -> COMBAT_SESSION | generated |
| G-8-1 | GUARDIAN | Retaliation | Deferred ON_DAMAGE_TAKEN -> COMBAT_SESSION | generated |
| G-8-2 | GUARDIAN | Unbreakable Bastion | Deferred ON_ABILITY_USED:BASTION -> COMBAT_SESSION | generated |
| G-8-3 | GUARDIAN | Fortress Heart | Deferred ON_DAMAGE_TAKEN -> COMBAT_SESSION | generated |
| G-9-1 | GUARDIAN | ETERNAL GUARDIAN | Supported INCOMING_PHYSICAL_DAMAGE_REDUCTION_PERCENT<br>Supported INCOMING_MAGICAL_DAMAGE_REDUCTION_PERCENT<br>Deferred ON_DAMAGE_TAKEN -> PARTY | generated |
| B-1-1 | BERSERKER | Battle Frenzy | Supported ATTACK_POWER_PERCENT | BERSERKER_WAR_MASK |
| B-1-2 | BERSERKER | Bloodthirst | Supported ON_ENEMY_KILLED | BERSERKER_BLOOD_RENEWAL |
| B-1-3 | BERSERKER | Keen Senses | Supported ACCURACY_PERCENT | BERSERKER_KEEN_EYE |
| B-1-4 | BERSERKER | Savage Strength | Supported STRENGTH_PERCENT | BERSERKER_CRUSHING_BLOW |
| B-2-1 | BERSERKER | Blood Rage | Deferred ON_HP_THRESHOLD -> COMBAT_SESSION | BERSERKER_WAR_MASK |
| B-2-2 | BERSERKER | Wild Strike | Supported UNLOCK_ABILITY:WILD_STRIKE | BERSERKER_RAGE_SLASH |
| B-2-3 | BERSERKER | Unrelenting | Supported CRITICAL_CHANCE_PERCENT | BERSERKER_KEEN_EYE |
| B-2-4 | BERSERKER | Momentum | Deferred ON_ABILITY_USED -> COMBAT_SESSION | BERSERKER_SUNDERING_BLADE |
| B-3-1 | BERSERKER | Critical Instinct | Supported ON_CRITICAL_HIT | BERSERKER_KEEN_EYE |
| B-3-2 | BERSERKER | Whirlwind | Supported UNLOCK_ABILITY:WHIRLWIND | BERSERKER_BLOOD_BLADES |
| B-3-3 | BERSERKER | Rending Fury | Supported ARMOR_PENETRATION_PERCENT | BERSERKER_SHATTER_GUARD |
| B-3-4 | BERSERKER | Blood Trail | Deferred ON_CRITICAL_HIT -> COMBAT_SESSION | BERSERKER_BLOOD_BLADES |
| B-4-1 | BERSERKER | Double Strike | Deferred ON_AUTO_ATTACK -> COMBAT_SESSION | BERSERKER_SUNDERING_BLADE |
| B-4-2 | BERSERKER | Whirlwind Mastery | Supported ABILITY_DAMAGE_PERCENT:WHIRLWIND<br>Supported ABILITY_COOLDOWN_SECONDS:WHIRLWIND | BERSERKER_BLOOD_BLADES |
| B-4-3 | BERSERKER | Piercing Blow | Supported ABILITY_ARMOR_PENETRATION_PERCENT:WILD_STRIKE | BERSERKER_SHATTER_GUARD |
| B-4-4 | BERSERKER | Recklessness | Deferred ON_HP_THRESHOLD -> COMBAT_SESSION | BERSERKER_WAR_MASK |
| B-5-1 | BERSERKER | Berserk | Supported UNLOCK_ABILITY:BERSERK | BERSERKER_WAR_MASK |
| B-5-2 | BERSERKER | Adrenaline | Supported ATTACK_SPEED_PERCENT | BERSERKER_RAGE_SLASH |
| B-5-3 | BERSERKER | Lethal Crits | Supported CRITICAL_DAMAGE_PERCENT | BERSERKER_BLOOD_BLADES |
| B-5-4 | BERSERKER | Frenzy | Deferred ON_ABILITY_USED -> COMBAT_SESSION | BERSERKER_WAR_MASK |
| B-6-1 | BERSERKER | Blood Hunger | Supported VAMPIRISM_PERCENT | BERSERKER_BLOOD_RENEWAL |
| B-6-2 | BERSERKER | Devastating Blow | Deferred ON_CRITICAL_HIT -> COMBAT_SESSION | BERSERKER_SHATTER_GUARD |
| B-6-3 | BERSERKER | Battle Trance | Deferred ON_DAMAGE_TAKEN -> COMBAT_SESSION | BERSERKER_IRON_WILL |
| B-6-4 | BERSERKER | Blood Momentum | Deferred ON_CRITICAL_HIT -> COMBAT_SESSION | BERSERKER_RAGE_SLASH |
| B-7-1 | BERSERKER | Unstoppable Force | Deferred ON_AUTO_ATTACK -> COMBAT_SESSION | BERSERKER_SUNDERING_BLADE |
| B-7-2 | BERSERKER | Death's Strength | Deferred ON_HP_THRESHOLD -> COMBAT_SESSION | BERSERKER_WAR_MASK |
| B-7-3 | BERSERKER | Rending Rampage | Deferred ON_ABILITY_USED -> COMBAT_SESSION | BERSERKER_BLOOD_BLADES |
| B-7-4 | BERSERKER | Executioner | Deferred ON_HP_THRESHOLD -> COMBAT_SESSION | BERSERKER_KEEN_EYE |
| B-8-1 | BERSERKER | Death Whirlwind | Deferred ON_ABILITY_USED -> COMBAT_SESSION | BERSERKER_RAGE_SLASH |
| B-8-2 | BERSERKER | Berserker's Agony | Supported ABILITY_COOLDOWN_SECONDS:BERSERK<br>Deferred ON_ENEMY_KILLED:WILD_STRIKE -> COMBAT_SESSION | BERSERKER_WAR_MASK |
| B-8-3 | BERSERKER | Death's Embrace | Deferred ON_HP_THRESHOLD -> COMBAT_SESSION | BERSERKER_BLADE_GUARD |
| B-9-1 | BERSERKER | AVATAR OF RAGE | Supported ATTACK_POWER_PERCENT<br>Supported EFFECT_DURATION_SECONDS:BERSERK<br>Deferred ON_CRITICAL_HIT -> COMBAT_SESSION | BERSERKER_WAR_MASK |
| W-1-1 | WARLORD | Voice of Command | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-1-2 | WARLORD | Inspiring Presence | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-1-3 | WARLORD | Tactical Awareness | Supported ACCURACY_PERCENT | generated |
| W-1-4 | WARLORD | Battle Formation | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-2-1 | WARLORD | Battle Cry | Deferred UNLOCK_ABILITY:BATTLE_CRY -> PARTY | generated |
| W-2-2 | WARLORD | Comrade's Shield | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-2-3 | WARLORD | Formation Strength | Supported STAMINA_PERCENT | generated |
| W-2-4 | WARLORD | Unified Rhythm | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-3-1 | WARLORD | Endurance Cry | Deferred UNLOCK_ABILITY:ENDURANCE_CRY -> PARTY | generated |
| W-3-2 | WARLORD | Tactical Strike | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-3-3 | WARLORD | War Banner | Deferred UNLOCK_ABILITY:WAR_BANNER -> PARTY | generated |
| W-3-4 | WARLORD | Banner of Unity | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-4-1 | WARLORD | Amplified Cry | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-4-2 | WARLORD | Cry of Vengeance | Deferred UNLOCK_ABILITY:CRY_OF_VENGEANCE -> PARTY | generated |
| W-4-3 | WARLORD | Iron Discipline | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-4-4 | WARLORD | Echoing Command | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-5-1 | WARLORD | Victory Flag | Deferred UNLOCK_ABILITY:VICTORY_FLAG -> PARTY | generated |
| W-5-2 | WARLORD | Fearlessness | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-5-3 | WARLORD | Will to Win | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-5-4 | WARLORD | Order to Advance | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-6-1 | WARLORD | Rally Cry | Deferred UNLOCK_ABILITY:RALLY_CRY -> PARTY | generated |
| W-6-2 | WARLORD | Banner of Endurance | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-6-3 | WARLORD | War Leader | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-6-4 | WARLORD | Coordinated Supply | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-7-1 | WARLORD | Battle Standard | Deferred UNLOCK_ABILITY:BATTLE_STANDARD -> PARTY | generated |
| W-7-2 | WARLORD | Unbroken Formation | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-7-3 | WARLORD | Command Rhythm | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-7-4 | WARLORD | Hold the Line | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-8-1 | WARLORD | Legendary Cry | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-8-2 | WARLORD | Vanguard Unbroken | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-8-3 | WARLORD | Warlord's Rage | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-8-4 | WARLORD | Supreme Commander | Deferred ON_PARTY_EVENT -> PARTY | generated |
| W-9-1 | WARLORD | WARLORD OF ELYNDOR | Deferred ON_PARTY_EVENT -> PARTY | generated |
