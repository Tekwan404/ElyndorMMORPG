# Unified AFK Farming Implementation Plan
> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace mode-based AFK with one deterministic location-farming session that optionally targets one normal monster and never causes AFK death.

**Architecture:** Preserve `AfkFarmSession`, `AfkFarmIntervalGrant`, transaction/retry boundaries, inventory reward flow, and deterministic simulator. Remove mode from persistence and transport, add optional target selection, and derive efficiency from simulated successful kills and available interval time.

**Tech Stack:** C#/.NET, EF Core/PostgreSQL, ASP.NET minimal APIs, Vue 3/TypeScript, Vitest.

**Spec:** `docs/superpowers/specs/2026-09-12-unified-afk-farming-design.md`

## Global Constraints

- Server authoritatively validates target, calculates simulation, efficiency, XP, gold, and loot.
- AFK never spends HP, creates no CombatSession, and never transitions to death.
- `allowAfk`, not `DangerLevel`, is the location permission.
- Preserve `AfkFarmIntervalGrant` uniqueness and atomic retry safety.
- Do not invent a second day/night system; pass existing interval timestamps through simulation.

---

## File Structure

- `src/Elyndor.Core/Afk/AfkFarmSession.cs`: remove mode, add nullable target.
- `src/Elyndor.Core/Afk/AfkFarmSimulator.cs`: target filter and simulation-derived efficiency.
- `src/Elyndor.Infrastructure/Afk/AfkFarmService.cs`, `AfkFarmProgressService.cs`: unified validation and target-aware durable processing.
- `src/Elyndor.Contracts/Afk/AfkFarmContracts.cs`, `src/Elyndor.Server/Afk/AfkFarmEndpoints.cs`: mode-free API.
- `src/Elyndor.Infrastructure/Persistence/Configurations/AfkFarmSessionConfiguration.cs` and an EF migration: schema update.
- `src/Elyndor.Infrastructure/World/BootstrapService.cs`, `src/Elyndor.Contracts/World/WorldContracts.cs`, `src/Elyndor.Server/World/WorldEndpoints.cs`: state projection.
- `web/elyndor-web/src/api/contracts.ts`, `stores/gameSession.ts`, `game/world/views/WorldView.vue`: optional target UI.
- Existing AFK unit/integration tests and `WorldView.spec.ts`: focused regressions.

### Task 1: Canonical AFK domain and schema

**Files:**
- Modify: `src/Elyndor.Core/Afk/AfkFarmSession.cs`
- Modify: `src/Elyndor.Core/Afk/AfkFarmSimulator.cs`
- Modify: `src/Elyndor.Infrastructure/Persistence/Configurations/AfkFarmSessionConfiguration.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Migrations/<timestamp>_UnifiedAfkFarming.cs`
- Test: `tests/Elyndor.UnitTests/Afk/AfkFarmSessionTests.cs`
- Test: `tests/Elyndor.UnitTests/Afk/AfkFarmSimulatorTests.cs`

**Interfaces:**
- Produces `AfkFarmSession(..., string? targetMonsterId, ...)`.
- Produces `AfkFarmSimulationRequest(..., string? TargetMonsterId, ...)`.
- Produces `AfkFarmSimulationResult(..., int EfficiencyPercent, ...)`.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact]
public void Simulate_WithTarget_UsesOnlyTargetMonster()
{
    var result = AfkFarmSimulator.Simulate(CreateRequest(targetMonsterId: wolf.Id));
    Assert.All(result.LootCandidates, candidate => Assert.Equal(wolf.Id, candidate.MonsterId));
}

[Fact]
public void Simulate_WeakCharacter_FarmsLessEfficientlyWithoutDeath()
{
    var weak = AfkFarmSimulator.Simulate(CreateRequest(CreateSnapshot(8)));
    var strong = AfkFarmSimulator.Simulate(CreateRequest(CreateSnapshot(28)));
    Assert.True(weak.EfficiencyPercent < strong.EfficiencyPercent);
    Assert.Equal(request.EndsAtUtc - request.StartedAtUtc, weak.SimulatedDuration);
}
```

- [ ] **Step 2: Verify the tests fail**

Run: `dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --filter FullyQualifiedName~AfkFarmSimulatorTests`

Expected: compile failure because target and efficiency do not exist.

- [ ] **Step 3: Implement model and simulator**

Remove `AfkFarmMode` and `Mode`. Add nullable `TargetMonsterId`. Filter eligible ordinary encounters to the target if present. A failed encounter consumes its simulated time and the interval continues. Derive `EfficiencyPercent` from successful completed-kill time divided by base farming opportunity time, clamped to `0..100`; do not use HP thresholds, survival scores, or death chance.

- [ ] **Step 4: Add safe migration**

Map `TargetMonsterId` as nullable max length 64. Generate a migration that drops `Mode` and adds `TargetMonsterId`, leaving grants, inventory, characters, and historical rewards unchanged.

- [ ] **Step 5: Verify and commit**

Run: `dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --filter FullyQualifiedName~AfkFarm`

Expected: AFK session and simulator tests pass.

```bash
git add src/Elyndor.Core/Afk src/Elyndor.Infrastructure/Persistence tests/Elyndor.UnitTests/Afk
git commit -m "feat: unify AFK session simulation"
```

### Task 2: Unified server flow

**Files:**
- Modify: `src/Elyndor.Contracts/Afk/AfkFarmContracts.cs`
- Modify: `src/Elyndor.Infrastructure/Afk/AfkFarmService.cs`
- Modify: `src/Elyndor.Infrastructure/Afk/AfkFarmProgressService.cs`
- Modify: `src/Elyndor.Server/Afk/AfkFarmEndpoints.cs`
- Modify: bootstrap contract/projection files listed above.
- Test: `tests/Elyndor.IntegrationTests/Postgres/AfkFarmServiceTests.cs`

**Interfaces:**
- Produces `StartAsync(accountId, locationId, targetMonsterId, duration, ct)`.
- Produces `PreviewAsync(accountId, locationId, targetMonsterId, duration, ct)`.
- Returns target and `EfficiencyPercent`, never mode.

- [ ] **Step 1: Write failing integration tests**

```csharp
[Fact]
public async Task Start_AllowsDangerousLocationWhenContentAllowsAfk()
{
    var result = await service.StartAsync(accountId, dangerousLocation.Id, null, TimeSpan.FromHours(1), default);
    Assert.True(result.Succeeded);
}

[Fact]
public async Task Start_RejectsTargetOutsideLocation()
{
    var result = await service.StartAsync(accountId, location.Id, foreignMonster.Id, TimeSpan.FromHours(1), default);
    Assert.Equal(AfkFarmErrorCodes.InvalidTarget, result.ErrorCode);
}
```

- [ ] **Step 2: Verify failure**

Run: `dotnet test tests/Elyndor.IntegrationTests/Elyndor.IntegrationTests.csproj --filter FullyQualifiedName~AfkFarmServiceTests`

Expected: compile failure against the mode-based signature.

- [ ] **Step 3: Implement server validation and projections**

Remove enum parsing and Safe-only checks from endpoints/services. Keep combat, travel, dungeon, active-session, unlock, and `allowAfk` checks. Remove current-HP and danger-level gates. Reject absent, foreign, elite, or boss targets with `afk_invalid_target`. Pass target and efficiency through preview, state, bootstrap, and progress processing. Keep existing grant transaction/replay code intact.

- [ ] **Step 4: Verify and commit**

Run: `dotnet test tests/Elyndor.IntegrationTests/Elyndor.IntegrationTests.csproj --filter "FullyQualifiedName~AfkFarm|FullyQualifiedName~PartyDungeonRunCoordinatorTests"`

Expected: AFK target/danger validation, durable grants, and dungeon conflict tests pass.

```bash
git add src/Elyndor.Contracts/Afk src/Elyndor.Infrastructure/Afk src/Elyndor.Server/Afk src/Elyndor.Infrastructure/World src/Elyndor.Contracts/World src/Elyndor.Server/World tests/Elyndor.IntegrationTests
git commit -m "feat: expose unified AFK farming API"
```

### Task 3: Mobile target selection

**Files:**
- Modify: `web/elyndor-web/src/api/contracts.ts`
- Modify: `web/elyndor-web/src/stores/gameSession.ts`
- Modify: `web/elyndor-web/src/game/world/views/WorldView.vue`
- Test: `web/elyndor-web/src/__tests__/WorldView.spec.ts`

**Interfaces:**
- Store calls `previewAfkFarm(locationId, durationMinutes, targetMonsterId?)` and `startAfkFarm(...)`.
- UI sends no mode, defaults to `Any enemies`, and renders efficiency.

- [ ] **Step 1: Write failing UI test**

```ts
it('sends no mode and allows any enemy or an ordinary location target', async () => {
  await wrapper.get('[data-afk-farming]').trigger('click')
  expect(wrapper.text()).toContain('Любые противники')
  await wrapper.get('[data-afk-target="forest-wolf"]').trigger('click')
  expect(previewAfkFarm).toHaveBeenCalledWith('dark-forest', 60, 'forest-wolf')
})
```

- [ ] **Step 2: Verify failure**

Run: `npm run test:unit -- --run src/__tests__/WorldView.spec.ts --reporter=verbose`

Expected: the selector and mode-free store call are absent.

- [ ] **Step 3: Implement minimal UI**

Populate the selector from ordinary current-location encounters, show `Любые противники` first, send `targetMonsterId: null` for it, show `Эффективность: N%`, and remove Safe/risk/death wording. Do not add navigation.

- [ ] **Step 4: Verify and commit**

Run: `npm run test:unit -- --run src/__tests__/WorldView.spec.ts --reporter=verbose`

Run: `npm run type-check && npm run lint && npm run build`

Expected: focused test, typecheck, lint, and build pass.

```bash
git add web/elyndor-web/src
git commit -m "feat: add unified AFK target selection"
```

### Task 4: Final safety review

- [ ] **Step 1: Inspect migration**

Run: `dotnet ef migrations script --project src/Elyndor.Infrastructure --startup-project src/Elyndor.Server --idempotent --output /tmp/elyndor-afk.sql`

Expected: only AFK session mode/target schema changes; no inventory or character data reset.

- [ ] **Step 2: Run proportional regression verification**

Run: `dotnet build Elyndor.slnx --no-restore -v:minimal`

Run: `dotnet test Elyndor.slnx --no-restore --filter "FullyQualifiedName~AfkFarm|FullyQualifiedName~PartyDungeonRunCoordinatorTests"`

Run: `npm run type-check && npm run lint && npm run build`

Expected: all commands pass.

- [ ] **Step 3: Diff review and commit verified status**

Run: `git diff main...HEAD --check`

Update `docs/development/AFK_FARMING_IMPLEMENTATION_PLAN.md` only with verified Phase 6 facts, then commit:

```bash
git add docs/development/AFK_FARMING_IMPLEMENTATION_PLAN.md
git commit -m "docs: record unified AFK farming completion"
```
