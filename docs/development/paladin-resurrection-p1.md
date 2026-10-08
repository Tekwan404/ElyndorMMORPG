# Paladin resurrection and P1 contracts

Content 0.49.0 / balance 0.39.0 extends main's Paladin P0 corrections without
changing the 96 talent IDs or rebuilding the class tree.

## Resurrection

`RESURRECTION` is a base Paladin ability, available without a talent. It targets
one dead captured player ally during an active combat. Initial authored balance:
6 second interruptible Holy cast, 80 Mana, 5 minute personal cooldown starting at
cast completion, return with 30% maximum HP and 20% maximum resource. There is no
group quota, resurrection immunity or PvP ban. Current solo Arena has no eligible
allied corpse; group Arena itself is outside this change.

`SingleDeadAlly` / `Resurrect` / `ActorResurrected` are general kernel contracts.
The session validates captured membership. Completion checks the corpse again;
competing casts cannot restore an already living target twice. Mana spent on an
interrupted or overtaken cast is not refunded. Standard cast cooldown semantics
are retained. Ordinary healing still cannot revive.

The existing actor/runtime and contribution ledger survive. Cooldowns, command
deduplication and consumed-item state are retained. Death cancels unfinished
casts and delayed actions. Revival clears death-event dedup, restores the roster
to Active and advances the regeneration cursor past dead time. Existing effect
cleanup removes periodic effects and expired effects before revival, preventing
overdue ticks. Autoattack remains off until the revived player starts it again.
An ended fight is never reopened. No schema migration or separate reward is added.

The battle UI lets Paladins select dead roster members and sends only the cast
intent; server checks remain authoritative. Reconnect retains valid corpse
selection. Outside-combat checkpoint/respawn rules are unchanged.

## Five completed P1 contracts

| Talent | Content contract |
| --- | --- |
| H-3-3 Wisdom | Successful direct Judgement marks for 12s; a direct allied hit has 50% chance to restore 2% maximum Mana to its attacker, 2s personal ICD. Mana users only. |
| H-7-1 Light | Same mark window/chance/ICD; restore 2% attacker's maximum HP as secondary healing, without healing crit/Beacon recursion. |
| P-3-4 Consecrated Ground | Existing +10/20% periodic damage; add +10/20% threat to the resulting Consecration damage. |
| P-6-2 One-Handed Specialization | +2/4/6% outgoing damage and +1/2/3 Accuracy only with captured one-handed weapon **and** shield. Both factory paths capture the equipped off-hand category for PvE/Arena. |
| R-5-2 Fanaticism | Existing Judgement critical bonus; reduce autoattack, Judgement, Crusader Strike, Verdict, Divine Storm and seal damage threat by 10/20/30%. Healing/Consecration/tank retaliation retain their threat. |

Wisdom/Light ignore periodic, reflected and proc damage. Each beneficiary has
its own ICD per mark type, shared across Paladin owners, so multiple owners do
not multiply resource/healing grants or proc rolls. Eligibility uses captured
active players, not mutable Party membership. Values are authored on the talent
modifiers; RU descriptions state the same conditions and magnitudes.

These changes complete the five named P1 runtime gaps, not a claim of final
class balance. Global Mana scaling, effective HPS/DPS, gear scaling and PvP
balance still require measurement separately.
