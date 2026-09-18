from pathlib import Path

path = Path("src/Elyndor.Core/Combat/Sessions/CombatSession.Threat.cs")
text = path.read_text(encoding="utf-8")
old = """    private void RegisterThreat(CombatEvent combatEvent)\n    {\n        ProcessExplicitThreatAction(combatEvent);\n\n        CombatPlayerRuntimeState previousActivePlayer = _activePlayerState;"""
new = """    private void RegisterThreat(CombatEvent combatEvent)\n    {\n        if (combatEvent.Type is CombatEventType.ThreatAdded\n            or CombatEventType.ThreatDropped\n            or CombatEventType.ThreatCleared\n            or CombatEventType.FixateApplied)\n        {\n            ProcessExplicitThreatAction(combatEvent);\n            return;\n        }\n\n        CombatPlayerRuntimeState previousActivePlayer = _activePlayerState;"""
count = text.count(old)
if count != 1:
    raise SystemExit(f"Expected explicit threat routing fragment exactly once, found {count}.")
path.write_text(text.replace(old, new, 1), encoding="utf-8")
