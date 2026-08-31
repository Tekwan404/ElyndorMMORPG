# Elyndor game content

This directory contains versioned static game data. Player state must never be stored here.

`package.json` is the active versioned package. It carries `ContentVersion`, `BalanceVersion`, an explicit UTC publication time, typed character profiles, and Phase 3B Warrior abilities. IDs use uppercase ASCII letters, digits, and underscores. References are resolved by `(type, id)`.

Validate the package from the repository root:

```powershell
dotnet run --project tools/Elyndor.ContentValidator -- content/package.json
```

The server validates the copied package before accepting traffic. `package.json` remains the single entry point; category directories may be introduced only when their owning phase needs package splitting.
