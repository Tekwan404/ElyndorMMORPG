# Composed content inspection exports

`current/01-items.json`, `02-enemies.json`, `03-locations.json` and the current
exporter's `04-combat-balance.json` are denormalized inspection snapshots for human
review. They are not loaded by the game and must
not be edited to change balance, loot or definitions.

Regenerate them from all current content categories/overrides:

```powershell
dotnet run --project tools/Elyndor.ContentValidator -- content/package.json --export-analysis=content-analysis/current
```

The export metadata identifies the captured content/balance version. Check it
before relying on numbers: a tracked snapshot is not necessarily current after
content changes. Runtime authoring remains under `content/`; original historical
input JSON is retained under `docs/archive/content-inputs/`.
