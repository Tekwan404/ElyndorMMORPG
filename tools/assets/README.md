# Elyndor art pipeline

The source folders `pic/` and `talant/` are local working assets and are intentionally ignored by Git. The repeatable pipeline creates the tracked runtime assets and content mappings:

```powershell
.\tools\assets\slice-pic-assets.ps1
.\tools\assets\convert-generated-assets-to-webp.ps1
.\tools\assets\validate-pic-assets.ps1
```

The player bundle receives talent, item and non-admin character art. Admin-only artwork is written under `web/elyndor-admin/src/assets/admin` and is never copied into `web/elyndor-web`.

`asset-manifest.json` records the source sheet, grid cell, generated icon and known source limitations. Archer talent sheets contain 15 cells per branch, so cells are intentionally reused for nodes 16–32 until additional source art is supplied.
