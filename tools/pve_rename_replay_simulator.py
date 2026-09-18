from pathlib import Path

paths = [
    Path("src/Elyndor.Core/Combat/Simulation/CombatScenarioSimulator.cs"),
    Path("tests/Elyndor.UnitTests/Combat/CombatScenarioSimulatorTests.cs"),
]

replacements = [
    ("CombatSimulationEndReason", "CombatReplayEndReason"),
    ("CombatSimulationDecisionContext", "CombatReplayDecisionContext"),
    ("CombatSimulationScenario", "CombatReplayScenario"),
    ("CombatSimulationMetrics", "CombatReplayMetrics"),
    ("CombatSimulationResult", "CombatReplayResult"),
    ("CombatScenarioSimulator", "CombatReplaySimulator"),
]

for path in paths:
    text = path.read_text(encoding="utf-8")
    original = text
    for old, new in replacements:
        text = text.replace(old, new)
    if text == original:
        raise SystemExit(f"{path}: no simulator identifiers were replaced")
    path.write_text(text, encoding="utf-8")
