from pathlib import Path
p = Path('tools/_patch_combat_client.py')
text = p.read_text()
old = '''rep(f,
    "PartyService partyService,\\n        GameDbContext dbContext,\\n        ICombatActivityReader combatActivity,",
    "PartyService partyService,\\n        BootstrapService bootstrapService,\\n        GameDbContext dbContext,\\n        ICombatActivityReader combatActivity,")'''
new = '''rep(f,
    "private static async Task<IResult> TeleportAsync(\\n        TeleportToDungeonRequest request,\\n        ClaimsPrincipal user,\\n        DungeonService service,\\n        PartyService partyService,\\n        GameDbContext dbContext,\\n        ICombatActivityReader combatActivity,",
    "private static async Task<IResult> TeleportAsync(\\n        TeleportToDungeonRequest request,\\n        ClaimsPrincipal user,\\n        DungeonService service,\\n        PartyService partyService,\\n        BootstrapService bootstrapService,\\n        GameDbContext dbContext,\\n        ICombatActivityReader combatActivity,")'''
if text.count(old) != 1:
    raise SystemExit(f'patch-script anchor count={text.count(old)}')
p.write_text(text.replace(old, new, 1))
