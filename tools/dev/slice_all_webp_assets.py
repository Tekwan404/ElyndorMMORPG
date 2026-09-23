from __future__ import annotations

import argparse
import re
import shutil
import unicodedata
from pathlib import Path
from typing import List, Dict, Any

from PIL import Image

# ============================================================
# SETTINGS
# ============================================================

DEFAULT_ROOT = r"C:\Users\tekwan\Downloads\ELYNDOR\pic"
DEFAULT_OUT = r"C:\Users\tekwan\Downloads\ELYNDOR\pic\ready_webp"

SUPPORTED_EXTS = {".png", ".webp", ".jpg", ".jpeg"}

# ============================================================
# HELPERS
# ============================================================

def norm_text(value: str) -> str:
    value = unicodedata.normalize("NFKC", value).lower()
    value = value.replace("ё", "е")
    value = value.replace("—", "-").replace("–", "-")
    value = re.sub(r"\s+", " ", value).strip()
    return value

def ensure_capacity(cols: int, rows: int, names: List[str]) -> None:
    capacity = cols * rows
    if len(names) > capacity:
        raise ValueError(
            f"Too many names ({len(names)}) for grid {cols}x{rows} (capacity {capacity})"
        )

def find_source_file(root: Path, source_folder: str, tokens: List[str]) -> Path:
    folder = root / source_folder
    if not folder.exists():
        raise FileNotFoundError(f"Source folder not found: {folder}")

    norm_tokens = [norm_text(t) for t in tokens]
    candidates = []

    for file in folder.iterdir():
        if not file.is_file():
            continue
        if file.suffix.lower() not in SUPPORTED_EXTS:
            continue

        stem = norm_text(file.stem)
        if all(token in stem for token in norm_tokens):
            candidates.append(file)

    if not candidates:
        raise FileNotFoundError(
            f"No file found in '{folder}' for tokens: {tokens}"
        )

    if len(candidates) > 1:
        names = ", ".join(c.name for c in candidates)
        raise RuntimeError(
            f"Ambiguous match in '{folder}' for tokens {tokens}: {names}"
        )

    return candidates[0]

def crop_grid(
    image: Image.Image,
    cols: int,
    rows: int,
    count: int
) -> List[Image.Image]:
    width, height = image.size
    cell_w = width / cols
    cell_h = height / rows

    result = []
    for idx in range(count):
        col = idx % cols
        row = idx // cols

        left = round(col * cell_w)
        upper = round(row * cell_h)
        right = round((col + 1) * cell_w)
        lower = round((row + 1) * cell_h)

        tile = image.crop((left, upper, right, lower))
        result.append(tile)

    return result

def save_webp(img: Image.Image, out_path: Path, size: int) -> None:
    if img.mode not in ("RGB", "RGBA"):
        img = img.convert("RGBA")

    if size > 0:
        img = img.resize((size, size), Image.Resampling.LANCZOS)

    out_path.parent.mkdir(parents=True, exist_ok=True)
    img.save(out_path, format="WEBP", quality=95, method=6)

def build_out_name(prefix: str, index: int, slug: str) -> str:
    return f"{prefix}_{index:02d}_{slug}.webp"

def clear_dir(path: Path) -> None:
    if path.exists():
        shutil.rmtree(path)
    path.mkdir(parents=True, exist_ok=True)

# ============================================================
# SHEET CONFIG
# ============================================================

SHEETS: List[Dict[str, Any]] = [
    # --------------------------------------------------------
    # MATERIALS FOR PROFESSIONS
    # --------------------------------------------------------
    {
        "group": "materials_for_professions",
        "source_folder": "Materials for professions",
        "tokens": ["горное дело", "ранние"],
        "prefix": "mining_early",
        "cols": 4,
        "rows": 4,
        "names": [
            "soft_stone",
            "copper_fragments",
            "copper_ore",
            "pale_gem",
            "ferric_stone",
            "forest_quartz",
            "tin_ore",
            "rough_stone",
            "smoky_quartz",
            "iron_ore",
            "heavy_stone",
            "runic_quartz",
            "vein_heart",
        ],
    },
    {
        "group": "materials_for_professions",
        "source_folder": "Materials for professions",
        "tokens": ["горное дело", "средние"],
        "prefix": "mining_mid",
        "cols": 4,
        "rows": 3,
        "names": [
            "defiled_mineral",
            "dark_gem",
            "dark_iron",
            "fire_opal",
            "runic_ore",
            "silver_ore",
            "moonstone",
            "umbral_sapphire",
            "mirrored_ore",
            "runestone",
            "pure_mana_crystal",
            "rift_heart",
        ],
    },
    {
        "group": "materials_for_professions",
        "source_folder": "Materials for professions",
        "tokens": ["горное дело", "поздние"],
        "prefix": "mining_late",
        "cols": 3,
        "rows": 3,
        "names": [
            "black_iron",
            "obsidian",
            "runic_ruby",
            "heart_of_black_mountain",
            "blood_garnet",
            "necrotic_crystal",
            "bastionite",
            "black_diamond",
            "bastion_heart",
        ],
    },
    {
        "group": "materials_for_professions",
        "source_folder": "Materials for professions",
        "tokens": ["группа материалов", "кожевничества"],
        "prefix": "leatherworking_bundle",
        "cols": 4,
        "rows": 3,
        "names": [
            "light_and_durable_hides",
            "whole_hides",
            "beast_sinews",
            "basilisk_scales",
            "cave_membranes",
            "tainted_hides",
            "swamp_leathers",
            "blackstone_hides",
            "obsidian_hides",
            "bone_sinews",
        ],
    },
    {
        "group": "materials_for_professions",
        "source_folder": "Materials for professions",
        "tokens": ["снятие шкур", "ранние"],
        "prefix": "skinning_early",
        "cols": 3,
        "rows": 3,
        "names": [
            "light_hide",
            "whole_light_hide",
            "beast_sinew",
            "soft_hide",
            "whole_deer_hide",
            "durable_hide",
            "dark_hide",
            "predator_sinew",
            "whole_alpha_hide",
        ],
    },
    {
        "group": "materials_for_professions",
        "source_folder": "Materials for professions",
        "tokens": ["снятие шкур", "средние"],
        "prefix": "skinning_mid",
        "cols": 4,
        "rows": 4,
        "names": [
            "cave_leather",
            "dense_membrane",
            "mineralized_hide",
            "tainted_hide",
            "whole_tainted_hide",
            "plague_sinew",
            "beast_heartstring",
            "ashen_hide",
            "scorched_hide",
            "fire_sinew",
            "swamp_hide",
            "moon_scale",
            "whole_swamp_hide",
        ],
    },
    {
        "group": "materials_for_professions",
        "source_folder": "Materials for professions",
        "tokens": ["снятие шкур", "поздние"],
        "prefix": "skinning_late",
        "cols": 4,
        "rows": 3,
        "names": [
            "blackstone_hide",
            "dense_black_hide",
            "winged_membrane",
            "stoneback_hide",
            "dried_hide",
            "crimson_leather",
            "bone_sinew",
            "obsidian_hide",
            "whole_obsidian_hide",
            "black_sinew",
        ],
    },
    {
        "group": "materials_for_professions",
        "source_folder": "Materials for professions",
        "tokens": ["собирательство", "ранние"],
        "prefix": "gathering_early",
        "cols": 5,
        "rows": 4,
        "names": [
            "forest_mint",
            "silverleaf",
            "dewy_blossom",
            "field_flower",
            "honey_grass",
            "moon_petal",
            "dawn_pollen",
            "shadow_moss",
            "hushroot",
            "taint_bud",
            "pure_spore",
            "roadside_yarrow",
            "dustleaf",
            "mercenary_rosehip",
            "stone_moss",
            "glowcap",
            "runic_lichen",
        ],
    },
    {
        "group": "materials_for_professions",
        "source_folder": "Materials for professions",
        "tokens": ["собирательство", "средние"],
        "prefix": "gathering_mid",
        "cols": 4,
        "rows": 4,
        "names": [
            "tainted_fern",
            "witchroot",
            "black_spore",
            "purified_sprout",
            "smoldergrass",
            "fire_root",
            "ashen_rose",
            "spectral_reed",
            "moon_moss",
            "silver_swamplily",
            "swamp_spore",
            "night_bellflower",
            "umbral_root",
            "star_pollen",
        ],
    },
    {
        "group": "materials_for_professions",
        "source_folder": "Materials for professions",
        "tokens": ["собирательство", "поздние"],
        "prefix": "gathering_late",
        "cols": 4,
        "rows": 4,
        "names": [
            "mirror_thistle",
            "mana_root",
            "flower_of_twinned_light",
            "mana_dust",
            "ashroot",
            "black_heather",
            "fire_bloom",
            "bloodbloom",
            "grave_moss",
            "soulleaf",
            "essence_shard",
            "black_starbloom",
            "ashen_orchid",
            "abyss_flower",
        ],
    },

    # --------------------------------------------------------
    # MATERIALS NOT USED IN PROFESSIONS
    # --------------------------------------------------------
    {
        "group": "materials_not_used_in_professions",
        "source_folder": "Materials not used in professions",
        "tokens": ["охотничьи", "трофеи"],
        "prefix": "hunting_trophies",
        "cols": 4,
        "rows": 4,
        "names": [
            "tough_meat",
            "wolf_fang",
            "tattered_fur",
            "boar_meat",
            "boar_tusk",
            "coarse_bristle",
            "cave_slime",
            "mineralized_bone",
            "ashen_meat",
            "charred_fang",
            "swamp_meat",
            "slimy_fang",
            "bone_dust",
            "bone_fragment",
        ],
    },
    {
        "group": "materials_not_used_in_professions",
        "source_folder": "Materials not used in professions",
        "tokens": ["темные", "магические"],
        "prefix": "dark_magic_reagents",
        "cols": 5,
        "rows": 4,
        "names": [
            "taint_dust",
            "dark_essence",
            "runic_shards",
            "mana_salt",
            "moon_dust",
            "cursed_talisman",
            "spirit_shard",
            "eclipse_mark",
            "umbra_seal",
            "mana_crystal",
            "reflection_dust",
            "rift_shard",
            "soul_remnant",
            "marshal_seal",
            "black_mana",
            "magister_seal",
            "first_warden_shard",
        ],
    },
    {
        "group": "materials_not_used_in_professions",
        "source_folder": "Materials not used in professions",
        "tokens": ["боссовые", "особые"],
        "prefix": "boss_trophies",
        "cols": 4,
        "rows": 3,
        "names": [
            "veteran_packleader_pelt",
            "matriarch_stinger",
            "core_of_forest_taint",
            "heart_of_forest_taint",
            "heart_of_boglight",
            "broodmother_venom_heart",
            "defiled_seed",
            "mirror_core_heart",
            "bastion_heart_shard",
            "black_legacy_fragment",
        ],
    },

    # --------------------------------------------------------
    # ITEMS OUTSIDE OF SETS
    # --------------------------------------------------------
    {
        "group": "items_outside_of_sets",
        "source_folder": "Items outside of sets",
        "tokens": ["древняя шахта", "вне комплектов"],
        "prefix": "ancient_mine_nonset",
        "cols": 3,
        "rows": 2,
        "names": [
            "runic_depths_cleaver",
            "forgotten_drift_lantern",
            "mine_watch_crossbow",
            "stoneheart_ring",
            "depths_keeper_bulwark",
            "broodmother_venom_heart",
        ],
    },
    {
        "group": "items_outside_of_sets",
        "source_folder": "Items outside of sets",
        "tokens": ["цитадель затмения", "вне комплектов"],
        "prefix": "eclipse_citadel_nonset",
        "cols": 4,
        "rows": 2,
        "names": [
            "shattered_captain_blade",
            "umbra_smith_hammer",
            "censer_of_fading_light",
            "inner_gate_shield",
            "regent_crown",
            "eclipse_signet",
            "bastion_plate_blueprint",
        ],
    },
    {
        "group": "items_outside_of_sets",
        "source_folder": "Items outside of sets",
        "tokens": ["сердце оскверненной чащи", "вне комплектов"],
        "prefix": "defiled_grove_nonset",
        "cols": 4,
        "rows": 2,
        "names": [
            "crookedroot_crusher",
            "venom_matron_circlet",
            "living_vine_bowstring",
            "censer_of_cleansing_flame",
            "heart_of_the_grove",
            "defiled_seed",
            "purified_root_infusion_recipe",
        ],
    },
    {
        "group": "items_outside_of_sets",
        "source_folder": "Items outside of sets",
        "tokens": ["цитадель расколотого ордена", "вне комплектов"],
        "prefix": "shattered_order_nonset",
        "cols": 4,
        "rows": 2,
        "names": [
            "mirrored_bastard_sword",
            "shattered_reflection_ring",
            "residual_mana_focus",
            "last_reserve_orb",
            "stolen_soul_visage",
            "triple_aspect_bow",
            "trinity_seal",
            "mirror_core_heart",
        ],
    },
    {
        "group": "items_outside_of_sets",
        "source_folder": "Items outside of sets",
        "tokens": ["черный бастион", "вне комплектов"],
        "prefix": "black_bastion_nonset",
        "cols": 4,
        "rows": 4,
        "names": [
            "commandant_key",
            "black_gate_crossbow",
            "inquisitor_censer",
            "silent_judgment_signet",
            "arc_tor_hammer",
            "colossus_runic_heart",
            "ash_marshal_banner",
            "silent_banner_bow",
            "black_star_staff",
            "eclipsed_oracle_orb",
            "first_warden_blade",
            "bastion_heart_shard",
            "black_legacy_fragment",
        ],
    },
    {
        "group": "items_outside_of_sets",
        "source_folder": "Items outside of sets",
        "tokens": ["именные вещи", "ранний диапазон"],
        "prefix": "named_world_early",
        "cols": 4,
        "rows": 4,
        "names": [
            "seasoned_alpha_fang",
            "forest_path_cloak",
            "spider_talisman",
            "ringing_wing_bow",
            "herbalist_gloves",
            "rootdew_ring",
            "thicket_hatchet",
            "thicket_hood",
            "goblin_trapper_bracers",
            "dusty_road_blade",
            "old_road_watch_bow",
            "wandering_mercenary_cloak",
            "veinmaster_hammer",
            "runic_shaft_lantern",
            "stonegulper_cuirass",
        ],
    },
    {
        "group": "items_outside_of_sets",
        "source_folder": "Items outside of sets",
        "tokens": ["именные вещи", "средний диапазон"],
        "prefix": "named_world_mid",
        "cols": 5,
        "rows": 4,
        "names": [
            "forest_witch_staff",
            "shadow_poacher_hood",
            "seasoned_alpha_cuirass",
            "border_executioner_spear",
            "smoldering_focus",
            "ashen_watcher_pauldrons",
            "herald_mark",
            "basilisk_scale_cloak",
            "drowned_pilgrim_lantern",
            "marshmother_fang",
            "umbra_warden_shield",
            "black_constellation_blade",
            "shard_wand",
            "mirrored_reflection_blade",
            "interrupted_mana_staff",
            "double_echo_bow",
            "rift_keeper_seal",
        ],
    },
    {
        "group": "items_outside_of_sets",
        "source_folder": "Items outside of sets",
        "tokens": ["именные вещи", "поздний диапазон"],
        "prefix": "named_world_late",
        "cols": 4,
        "rows": 4,
        "names": [
            "blackstone_hatchet",
            "mountain_hunter_wing",
            "black_flame_orb",
            "pass_runic_chestplate",
            "bone_greatsword",
            "plague_priest_crown",
            "crimson_harpy_feathers",
            "caught_soul_phial",
            "ashen_knight_blade",
            "bastion_watch_bow",
            "blackstar_focus",
            "harbinger_signet",
        ],
    },
]

# ============================================================
# MAIN
# ============================================================

def process_sheet(root: Path, out_root: Path, size: int, cfg: Dict[str, Any]) -> None:
    ensure_capacity(cfg["cols"], cfg["rows"], cfg["names"])

    src = find_source_file(root, cfg["source_folder"], cfg["tokens"])

    group_dir = out_root / cfg["group"]
    dest_dir = group_dir / cfg["prefix"]
    clear_dir(dest_dir)

    print("")
    print("==================================================")
    print(f"SOURCE : {src.name}")
    print(f"PREFIX : {cfg['prefix']}")
    print(f"GRID   : {cfg['cols']}x{cfg['rows']}")
    print(f"COUNT  : {len(cfg['names'])}")
    print(f"OUTPUT : {dest_dir}")
    print("==================================================")

    with Image.open(src) as img:
        img = img.convert("RGBA")
        tiles = crop_grid(img, cfg["cols"], cfg["rows"], len(cfg["names"]))

        for idx, (slug, tile) in enumerate(zip(cfg["names"], tiles), start=1):
            out_name = build_out_name(cfg["prefix"], idx, slug)
            out_path = dest_dir / out_name
            save_webp(tile, out_path, size)
            print(f"[OK] {out_name}")

def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=DEFAULT_ROOT, help="Root folder with source image folders")
    parser.add_argument("--out", default=DEFAULT_OUT, help="Output folder")
    parser.add_argument("--size", type=int, default=256, help="Final icon size, e.g. 256")
    args = parser.parse_args()

    root = Path(args.root)
    out_root = Path(args.out)
    out_root.mkdir(parents=True, exist_ok=True)

    print(f"[ROOT] {root}")
    print(f"[OUT ] {out_root}")
    print(f"[SIZE] {args.size}")

    errors = []

    for cfg in SHEETS:
        try:
            process_sheet(root, out_root, args.size, cfg)
        except Exception as ex:
            errors.append((cfg["prefix"], str(ex)))
            print(f"[ERROR] {cfg['prefix']}: {ex}")

    print("")
    print("==================================================")
    if errors:
        print("DONE WITH ERRORS")
        for prefix, msg in errors:
            print(f" - {prefix}: {msg}")
    else:
        print("DONE SUCCESSFULLY")
    print("==================================================")

if __name__ == "__main__":
    main()