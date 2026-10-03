# Elyndor art pipeline

## Offline utilities

All Python image tools require Pillow (`py -m pip install -r tools/assets/requirements.txt`). They are
offline authoring helpers, not dependencies of the server/frontend build.

- `slice-icons.py`: the maintained grid slicer; supports PNG/WebP, cell count,
  trimming, padding and manifests. Supersedes the old PNG-only copy.
- `slice-all-webp-assets.py`: batch sheet preparation using repository-relative
  defaults; use `--root`/`--out` to select other local source/output folders.
- `prepare-personal-art.py`: approved PersonalArt import naming and background
  processing. Review its specific background assumptions before new source sets;
  do not use it as a generic background remover.

```powershell
py tools/assets/slice-icons.py --help
py tools/assets/slice-all-webp-assets.py --help
py tools/assets/prepare-personal-art.py SOURCE_DIRECTORY OUTPUT_DIRECTORY
py -m unittest discover -s tools/assets -p 'test_*.py'
```

Source files are not deleted by these utilities. The old hardcoded converter that
deleted original PNGs after conversion has been removed. Keep originals under
local ignored source folders or the tracked `reference/` collection as appropriate.

## Existing content import pipeline

The source folders `pic/` and `talant/` are local working assets and are intentionally ignored by Git. The repeatable pipeline creates the tracked runtime assets and content mappings:

```powershell
.\tools\assets\slice-pic-assets.ps1
.\tools\assets\convert-generated-assets-to-webp.ps1
.\tools\assets\validate-pic-assets.ps1
```

The player bundle receives talent, item and non-admin character art. Admin-only artwork is written under `web/elyndor-admin/src/assets/admin` and is never copied into `web/elyndor-web`.

`asset-manifest.json` records the source sheet, grid cell, generated icon and known source limitations. Archer talent sheets contain 15 cells per branch, so cells are intentionally reused for nodes 16–32 until additional source art is supplied.

To import the class talent and spell sheets from `pic/V2`, run:

```powershell
.\tools\assets\import-v2-class-art.ps1 -SourceRoot 'C:\path\to\ELYNDOR\pic\V2'
.\tools\assets\validate-pic-assets.ps1
```

The importer crops each class/branch sheet, converts the crops to WebP, updates the existing talent and ability `iconId` content fields, and writes `v2-art-manifest.json`. Spell art is indexed by the ability's `iconId`; abilities without dedicated spell-sheet art continue to use the existing talent-art fallback. Source sheets remain local/ignored and are not required to build the game.
