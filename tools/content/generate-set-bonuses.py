"""Regenerate flat set bonuses from the shared itemization curve; preserve authored percentages/effects."""
import json
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"), parse_float=Decimal)


def generate():
    root = Path(__file__).resolve().parents[2]
    config = read(root / "tools/content/set-bonus-budgets.json")
    budget = read(root / "content/itemization/system.json")["itemization"]
    items = {}
    for path in sorted((root / "content/items").glob("*.json")):
        for item in read(path).get("items", []):
            items[item["id"]] = item
    for path in sorted((root / "content/sets").glob("*.json")):
        data = read(path)
        for definition in data.get("equipmentSets", []):
            allocation = config["sets"].get(definition["id"])
            if not allocation:
                continue
            pieces = [item for item in items.values() if item.get("setId") == definition["id"]]
            level = pieces[0]["requiredLevel"]
            rarity = config["preT1BudgetRarity"] if level == 55 else pieces[0]["rarity"].upper()
            x = Decimal(level - 1)
            power = (budget["templateBasePower"]
                     * (1 + budget["levelLinearCoefficient"] * x + budget["levelQuadraticCoefficient"] * x * x)
                     * budget["rarityMultipliers"][rarity]
                     * config["levelFlatPowerFractions"][str(level)])
            for bonus in definition["bonuses"]:
                shares = allocation.get(str(bonus["requiredPieces"]))
                if not shares:
                    continue
                total = sum(shares.values())
                for field, share in shares.items():
                    weight = budget["statPowerWeights"][config["flatStats"][field]]
                    bonus[field] = float((power * share / total / weight).quantize(Decimal("0.0001"), rounding=ROUND_HALF_UP))
        # Decimal encoding retains numeric JSON fields instead of serializing them as strings.
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2,
                                   default=lambda value: float(value)) + "\n", encoding="utf-8")


if __name__ == "__main__":
    generate()
