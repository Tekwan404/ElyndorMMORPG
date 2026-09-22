from __future__ import annotations

import json
import re
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WEB_SRC = ROOT / "web" / "elyndor-web" / "src"
ASSET_ROOT = WEB_SRC / "assets" / "items"
ALLOWED = {".png", ".jpg", ".jpeg", ".webp", ".svg"}


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def write(path: str, text: str) -> None:
    (ROOT / path).write_text(text, encoding="utf-8")


def replace_once(path: str, old: str, new: str) -> None:
    text = read(path)
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{path}: expected exactly one occurrence, found {count}: {old[:80]!r}")
    write(path, text.replace(old, new, 1))


def sub_once(path: str, pattern: str, replacement: str) -> None:
    text = read(path)
    updated, count = re.subn(pattern, replacement, text, count=1, flags=re.S)
    if count != 1:
        raise RuntimeError(f"{path}: expected exactly one regex match, found {count}: {pattern[:80]!r}")
    write(path, updated)


def canonical_assets() -> tuple[dict[str, list[str]], dict[str, list[str]], dict[str, list[str]]]:
    exact: dict[str, list[str]] = defaultdict(list)
    ignore_case: dict[str, list[str]] = defaultdict(list)
    basename: dict[str, list[str]] = defaultdict(list)
    for path in sorted(ASSET_ROOT.rglob("*")):
        if not path.is_file() or path.suffix.lower() not in ALLOWED:
            continue
        relative = path.relative_to(ASSET_ROOT).as_posix()
        icon_id = relative[: -len(path.suffix)]
        exact[icon_id].append(icon_id)
        ignore_case[icon_id.lower()].append(icon_id)
        basename[Path(icon_id).name.lower()].append(icon_id)
    return exact, ignore_case, basename


def migrate_content_icon_ids() -> int:
    exact, ignore_case, basename = canonical_assets()
    changes = 0
    for path in sorted((ROOT / "content" / "items").rglob("*.json")):
        data = json.loads(path.read_text(encoding="utf-8"))
        items = data.get("items", []) if isinstance(data, dict) else []
        replacements: dict[str, str] = {}
        for item in items:
            if not isinstance(item, dict):
                continue
            icon_id = item.get("iconId")
            if not isinstance(icon_id, str) or not icon_id.strip() or icon_id in exact:
                continue

            canonical: str | None = None
            case_matches = sorted(set(ignore_case.get(icon_id.lower(), [])))
            if len(case_matches) == 1:
                canonical = case_matches[0]
            elif "/" not in icon_id:
                legacy_matches = sorted(set(basename.get(icon_id.lower(), [])))
                if len(legacy_matches) == 1:
                    canonical = legacy_matches[0]

            if canonical is not None and canonical != icon_id:
                replacements[icon_id] = canonical

        if not replacements:
            continue

        text = path.read_text(encoding="utf-8")
        for old, new in replacements.items():
            old_json = json.dumps(old, ensure_ascii=False)
            new_json = json.dumps(new, ensure_ascii=False)
            needle = f'"iconId": {old_json}'
            count = text.count(needle)
            if count == 0:
                raise RuntimeError(f"{path}: parsed IconId {old!r}, but raw field was not found")
            text = text.replace(needle, f'"iconId": {new_json}')
            changes += count
            print(f"migrated IconId {old} -> {new} ({path.relative_to(ROOT)}, {count} occurrence(s))")
        path.write_text(text, encoding="utf-8")
    return changes


def patch_inventory() -> None:
    path = "web/elyndor-web/src/game/character/views/InventoryView.vue"
    replace_once(path, "import { itemArtUrl } from '@/assets/itemArt'\n", "")
    replace_once(
        path,
        "import { consumableSummary } from '@/game/items/consumablePresentation'\n",
        "import ItemIcon from '@/game/items/ItemIcon.vue'\nimport { consumableSummary } from '@/game/items/consumablePresentation'\n",
    )
    replace_once(path, "import type { GlyphName, IconConfig } from '@/ui/icons/icon.types'\n", "")
    sub_once(
        path,
        r"\nfunction itemArt\(item: InventoryItem\): string \| undefined \{.*?\n\}\n\nfunction resetFilters",
        "\nfunction resetFilters",
    )
    replace_once(
        path,
        '              <img v-if="itemArt(item)" :src="itemArt(item)" :alt="item.name" loading="lazy" decoding="async" />\n              <IconGenerator v-else :config="itemIconConfig(item, \'item\')" />',
        '              <ItemIcon\n                class="bag-cell__item-icon"\n                :icon-id="item.iconId"\n                :item-id="item.id"\n                :name="item.name"\n                :type="String(item.type)"\n                :slot="item.slot"\n                :weapon-category="item.weaponCategory"\n                :rarity="item.rarity"\n              />',
    )
    replace_once(
        path,
        '            <img v-if="itemArt(selectedItem)" :src="itemArt(selectedItem)" :alt="selectedItem.name" decoding="async" />\n            <IconGenerator v-else :config="itemIconConfig(selectedItem, \'item-detail\')" />',
        '            <ItemIcon\n              class="item-detail__item-icon"\n              :icon-id="selectedItem.iconId"\n              :item-id="selectedItem.id"\n              :name="selectedItem.name"\n              :type="String(selectedItem.type)"\n              :slot="selectedItem.slot"\n              :weapon-category="selectedItem.weaponCategory"\n              :rarity="selectedItem.rarity"\n              loading="eager"\n            />',
    )
    replace_once(
        path,
        ".bag-cell__icon img {\n  width: 68%;\n  height: 68%;\n  object-fit: contain;\n}",
        ".bag-cell__item-icon {\n  width: 68%;\n  height: 68%;\n}",
    )
    replace_once(
        path,
        ".item-detail__icon img {\n  width: 100%;\n  height: 100%;\n  object-fit: cover;\n}",
        ".item-detail__item-icon {\n  width: 100%;\n  height: 100%;\n}",
    )


def patch_merchant() -> None:
    path = "web/elyndor-web/src/game/world/components/MerchantShop.vue"
    replace_once(path, "import { itemArtUrl } from '@/assets/itemArt'\n", "")
    replace_once(
        path,
        "import { consumableActionLabel } from '@/game/items/consumablePresentation'\n",
        "import ItemIcon from '@/game/items/ItemIcon.vue'\nimport { consumableActionLabel } from '@/game/items/consumablePresentation'\n",
    )
    replace_once(path, "import type { GlyphName } from '@/ui/icons/icon.types'\n", "")
    sub_once(
        path,
        r"\nfunction itemArt\(item: MerchantItem\): string \| undefined \{.*?\n\}\n\nfunction itemTypeLabel",
        "\nfunction itemTypeLabel",
    )
    sub_once(
        path,
        r"\nfunction inventoryItemArt\(item: InventoryItem\): string \| undefined \{.*?\n\}\n\nfunction inventoryItemTypeLabel",
        "\nfunction inventoryItemTypeLabel",
    )
    sub_once(
        path,
        r'<img v-if="itemArt\(item\)" :src="itemArt\(item\)" :alt="item.name" loading="lazy" decoding="async" />\s*<IconGenerator\s+v-else\s+:config="\{ id: `merchant-\$\{item.definitionId\}`, glyph: itemGlyph\(item\), category: itemCategory\(item.type\) \}"\s*/>',
        '<ItemIcon :icon-id="item.iconId" :item-id="item.definitionId" :name="item.name" :type="item.type" :rarity="item.rarity" />',
    )
    sub_once(
        path,
        r'<img v-if="itemArt\(selectedOffer\)" :src="itemArt\(selectedOffer\)" :alt="selectedOffer.name" decoding="async" />\s*<IconGenerator\s+v-else\s+:config="\{ id: `merchant-detail-\$\{selectedOffer.definitionId\}`, glyph: itemGlyph\(selectedOffer\), category: itemCategory\(selectedOffer.type\) \}"\s*/>',
        '<ItemIcon :icon-id="selectedOffer.iconId" :item-id="selectedOffer.definitionId" :name="selectedOffer.name" :type="selectedOffer.type" :rarity="selectedOffer.rarity" loading="eager" />',
    )
    sub_once(
        path,
        r'<img v-if="inventoryItemArt\(item\)" :src="inventoryItemArt\(item\)" :alt="item.name" loading="lazy" decoding="async" />\s*<IconGenerator\s+v-else\s+:config="\{ id: `merchant-inventory-\$\{item.id\}`, glyph: inventoryItemGlyph\(item\), category: item.type === \'Equipment\' \? \'equipment\' : item.type === \'Consumable\' \? \'consumable\' : \'resource\' \}"\s*/>',
        '<ItemIcon :icon-id="item.iconId" :item-id="item.id" :name="item.name" :type="item.type" :slot="item.slot" :weapon-category="item.weaponCategory" :rarity="item.rarity" />',
    )


def patch_premium_store() -> None:
    path = "web/elyndor-web/src/game/economy/views/PremiumStoreView.vue"
    replace_once(path, "import { itemArtUrl } from '@/assets/itemArt'\n", "import ItemIcon from '@/game/items/ItemIcon.vue'\n")
    replace_once(
        path,
        '      <img v-if="offer.iconId" :src="itemArtUrl(offer.iconId)" alt="" />',
        '      <ItemIcon class="store-offer__icon" :icon-id="offer.iconId" :item-id="offer.itemDefinitionId" :name="offer.name" :rarity="offer.rarity" />',
    )
    replace_once(
        path,
        ".store-offer img{width:44px;height:44px;object-fit:contain}",
        ".store-offer__icon{width:44px;height:44px}",
    )


def patch_pending_loot_api() -> None:
    contracts = "src/Elyndor.Contracts/Items/InventoryContracts.cs"
    text = read(contracts)
    pattern = r"(public sealed record PendingLootItemResponse\(.*?GeneratedItemSummaryResponse\? GeneratedItem = null)(\);)"
    updated, count = re.subn(pattern, r"\1,\n    string? IconId = null\2", text, count=1, flags=re.S)
    if count != 1:
        raise RuntimeError(f"{contracts}: PendingLootItemResponse shape not found")
    write(contracts, updated)

    endpoint = "src/Elyndor.Server/Items/InventoryEndpoints.cs"
    replace_once(
        endpoint,
        "            ToGeneratedItemResponse(item.GeneratedItem));",
        "            ToGeneratedItemResponse(item.GeneratedItem),\n            item.Definition.IconId);",
    )


def remove_legacy_resolver_alias() -> None:
    path = "web/elyndor-web/src/assets/itemArt.ts"
    text = read(path)
    text = re.sub(
        r"\n/\*\* @deprecated Use ItemIcon\.vue; retained only until the branch migration replaces old call sites\. \*/\nexport const itemArtUrl = resolveItemArtUrl\n?",
        "\n",
        text,
        count=1,
    )
    write(path, text)


def ensure_no_legacy_item_art_call_sites() -> None:
    offenders: list[str] = []
    for path in WEB_SRC.rglob("*"):
        if not path.is_file() or path.suffix not in {".ts", ".vue"}:
            continue
        relative = path.relative_to(ROOT).as_posix()
        if relative in {
            "web/elyndor-web/src/assets/itemArt.ts",
            "web/elyndor-web/src/game/items/ItemIcon.vue",
        }:
            continue
        text = path.read_text(encoding="utf-8")
        if "itemArtUrl" in text or "@/assets/itemArt" in text:
            offenders.append(relative)
    if offenders:
        raise RuntimeError("legacy item icon resolver call sites remain: " + ", ".join(offenders))


def cleanup_bootstrap_files() -> None:
    for relative in [
        ".github/workflows/item-icon-pipeline-migrate.yml",
        "tools/dev/item_icon_pipeline_migrate.py",
    ]:
        path = ROOT / relative
        if path.exists():
            path.unlink()


def main() -> None:
    patch_inventory()
    patch_merchant()
    patch_premium_store()
    patch_pending_loot_api()
    migrated = migrate_content_icon_ids()
    remove_legacy_resolver_alias()
    ensure_no_legacy_item_art_call_sites()
    print(f"content IconId migrations applied: {migrated}")
    cleanup_bootstrap_files()


if __name__ == "__main__":
    main()
