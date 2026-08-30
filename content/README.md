# Elyndor game content

This directory contains versioned static game data. Player state must never be stored here.

`package.json` is the active Phase 0 package. It carries `ContentVersion`, `BalanceVersion`, an explicit UTC publication time, and typed definitions. IDs use uppercase ASCII letters, digits, and underscores. References are resolved by `(type, id)`.

Validate the package from the repository root:

```powershell
dotnet run --project tools/Elyndor.ContentValidator -- content/package.json
```

The server validates the copied package before accepting traffic. Category directories remain empty until their owning gameplay phase introduces approved definitions.
