# Eclipsed Citadel PvE Abilities Implementation Plan

> For agentic workers: REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Add the approved learning encounters to the existing five-mob production ECLIPSED_CITADEL.

**Architecture:** Content defines abilities, effects, AI, Core and generic encounter phases. Existing CombatSession generic encounter runtime executes thresholds, linked Core aura/cleanup, add-death and cast-interrupt triggers; no new runtime, API, database or UI layer is added.

**Tech Stack:** .NET 10, C#, xUnit, existing content composer/validator, deterministic CombatSession.

**Spec:** docs/superpowers/specs/2026-09-23-eclipsed-citadel-pve-abilities-design.md

## Global Constraints

- Preserve existing Eclipsed Citadel dungeon/monster/loot/reward IDs and encounter order.
- The Core is a linked no-reward combat object and despawns when Archon dies.
- Raids remain frozen; do not add a raid abstraction.
- Do not add a dedicated Archon session runtime, database schema, API or UI.

---

### Task 1: Compose authored Citadel content

**Files:**

- Create: content/abilities/eclipsed-citadel.json
- Create: content/bosses/eclipsed-citadel.json
- Modify: content/monsters/eclipsed-citadel.json
- Create: tests/Elyndor.IntegrationTests/Content/EclipsedCitadelPveContentTests.cs

**Consumes:** GameContentSnapshot.Indexes, current category composer and validator.

**Produces:** Final composed package with all five authored monster profiles, Golem/Archon generic encounters and a no-reward Core.

- [ ] **Step 1: Write the failing content contract test**

~~~
[Fact]
public void EclipsedCitadelUsesAuthoredAbilitiesAndGenericEncounters()
{
    GameContentSnapshot snapshot = LoadPackage();

    Assert.Equal(
        "ECLIPSED_CITADEL_SENTINEL_AI",
        snapshot.Indexes.MonstersById["ECLIPSED_CITADEL_SENTINEL_L25"].AiProfileId);
    Assert.Equal(
        ["VOID_WEAVER_VOID_THREAD", "VOID_WEAVER_VEIL_RUPTURE"],
        snapshot.Indexes.MonstersById["ECLIPSED_CITADEL_WEAVER_L25"].AbilityIds);
    Assert.DoesNotContain(
        "BITE",
        snapshot.Indexes.MonstersById["ECLIPSED_CITADEL_EXECUTIONER_L25"].AbilityIds);
    Assert.Contains("ECLIPSED_CITADEL_ARCHON_ENCOUNTER", snapshot.Indexes.EncountersById.Keys);
}
~~~

- [ ] **Step 2: Verify RED**

Run: dotnet test tests/Elyndor.IntegrationTests/Elyndor.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~EclipsedCitadelPveContentTests

Expected: FAIL because current Citadel content has generic AI and no encounter definitions.

- [ ] **Step 3: Add the minimal content**

Create exactly the IDs/values in the approved spec: Sentinel, Golem, Weaver, Executioner and Archon abilities/effects; authored AI rules; 2,500-HP Core; Golem 50% shield; Archon 70% Core, Core-death Broken Veil and 35% final phase. Keep XP, gold and loot unchanged.

- [ ] **Step 4: Verify GREEN**

~~~
dotnet test tests/Elyndor.IntegrationTests/Elyndor.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~EclipsedCitadelPveContentTests
dotnet run --project tools/Elyndor.ContentValidator/Elyndor.ContentValidator.csproj --configuration Release --no-restore -- content/package.json
~~~

- [ ] **Step 5: Commit**

~~~
git add content/abilities/eclipsed-citadel.json content/bosses/eclipsed-citadel.json content/monsters/eclipsed-citadel.json tests/Elyndor.IntegrationTests/Content/EclipsedCitadelPveContentTests.cs
git commit -m "feat: add eclipsed citadel monster abilities"
~~~

### Task 2: Prove Core lifecycle and Collapse reward in a real CombatSession

**Files:**

- Create: tests/Elyndor.UnitTests/Combat/EclipsedCitadelEncounterTests.cs

**Consumes:** real CombatSession.ConfigureGenericEncounter, content-defined effects and existing command/tick APIs.

**Produces:** Core no-reward, shield removal, Broken Veil and Collapse-only Unstable Core regression coverage.

- [ ] **Step 1: Write the failing lifecycle tests**

~~~
[Fact]
public void CoreDeathRemovesVeilAndAppliesBrokenVeil()
{
    CombatSession session = CreateArchonSession();
    CombatActorSnapshot core = DamageBossTo(session, 70m)
        .Enemies!
        .Single(item => item.DefinitionId == "ARCHON_DEAD_STAR_CORE");

    Assert.False(core.RewardEligible);
    Assert.Contains(session.Snapshot(PlayerId).Enemy!.Effects, item => item.Id == "ARCHON_DEAD_STAR_VEIL");

    Kill(session, core.ActorId);

    Assert.DoesNotContain(session.Snapshot(PlayerId).Enemy!.Effects, item => item.Id == "ARCHON_DEAD_STAR_VEIL");
    Assert.Contains(session.Snapshot(PlayerId).Enemy!.Effects, item => item.Id == "ARCHON_BROKEN_VEIL");
}

[Fact]
public void OnlyInterruptedCollapseAppliesUnstableCore()
{
    CombatSession session = CreateArchonSession(hpPercent: 34m);

    Interrupt(session, "ARCHON_STAR_FRACTURE");
    Assert.DoesNotContain(session.Snapshot(PlayerId).Enemy!.Effects, item => item.Id == "ARCHON_UNSTABLE_CORE");

    Interrupt(session, "ARCHON_DEAD_STAR_COLLAPSE");
    Assert.Contains(session.Snapshot(PlayerId).Enemy!.Effects, item => item.Id == "ARCHON_UNSTABLE_CORE");
}
~~~

- [ ] **Step 2: Verify RED**

Run: dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --no-restore --filter FullyQualifiedName~EclipsedCitadelEncounterTests

Expected: FAIL because the new content and session helper are absent.

- [ ] **Step 3: Implement test fixtures only**

Load the composed Archon/Core records, configure the real generic encounter, and drive it through existing commands/ticks. Do not mock the encounter runtime or add test-only production methods.

- [ ] **Step 4: Verify GREEN**

Run: dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --no-restore --filter FullyQualifiedName~EclipsedCitadelEncounterTests

- [ ] **Step 5: Commit**

~~~
git add tests/Elyndor.UnitTests/Combat/EclipsedCitadelEncounterTests.cs
git commit -m "test: cover eclipsed citadel encounter phases"
~~~

### Task 3: Cover authored AI and unchanged dungeon entry

**Files:**

- Modify: tests/Elyndor.UnitTests/Combat/MonsterAiDecisionEngineTests.cs
- Modify: tests/Elyndor.IntegrationTests/Combat/CitadelCombatFlowTests.cs
- Modify: tests/Elyndor.IntegrationTests/Content/GameContentPackageLoaderTests.cs

**Consumes:** authored profiles and existing dungeon service integration fixture.

**Produces:** Priority/HP gate/target selector assertions and preserved solo dungeon start coverage.

- [ ] **Step 1: Write failing observable behavior tests**

~~~
[Theory]
[InlineData("ECLIPSED_CITADEL_SENTINEL_AI", "SENTINEL_OATH_OF_OUTER_SEAL")]
[InlineData("ECLIPSED_CITADEL_VOID_WEAVER_AI", "VOID_WEAVER_VEIL_RUPTURE")]
public void CitadelAiSelectsItsHighestEligibleAbility(string profileId, string abilityId)
{
    Assert.Equal(abilityId, SelectHighestEligible(profileId).AbilityId);
}
~~~

Also assert that the Executioner's Verdict is unavailable above 35% self HP and that a level-25 solo run still starts ECLIPSED_CITADEL_SENTINEL_L25.

- [ ] **Step 2: Verify RED**

~~~
dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --no-restore --filter FullyQualifiedName~MonsterAiDecisionEngineTests
dotnet test tests/Elyndor.IntegrationTests/Elyndor.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~CitadelCombatFlowTests
~~~

- [ ] **Step 3: Make only content/fixture corrections demanded by the test**

Use CurrentThreatTarget for tank pressure, RandomEnemy for DoTs, AllEnemiesInCombat for AoE and MaxHpPercent: 35 only for the Executioner's own final phase.

- [ ] **Step 4: Verify GREEN and commit**

~~~
dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --no-restore --filter FullyQualifiedName~MonsterAiDecisionEngineTests
dotnet test tests/Elyndor.IntegrationTests/Elyndor.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~CitadelCombatFlowTests|FullyQualifiedName~GameContentPackageLoaderTests"
git add tests/Elyndor.UnitTests/Combat/MonsterAiDecisionEngineTests.cs tests/Elyndor.IntegrationTests/Combat/CitadelCombatFlowTests.cs tests/Elyndor.IntegrationTests/Content/GameContentPackageLoaderTests.cs
git commit -m "test: cover citadel ai and dungeon flow"
~~~

### Task 4: Verify, fix and review

**Files:** Only corrections exposed by verification.

- [ ] **Step 1: Run targeted combat/dungeon suites**

~~~
dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --no-restore --filter "FullyQualifiedName~EclipsedCitadel|FullyQualifiedName~CombatSessionGenericEncounterTests|FullyQualifiedName~MonsterAiDecisionEngineTests"
dotnet test tests/Elyndor.IntegrationTests/Elyndor.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~CitadelCombatFlowTests|FullyQualifiedName~GameContentPackageLoaderTests|FullyQualifiedName~DungeonSyncV2PartyTests"
~~~

- [ ] **Step 2: Run content validation, build and wider combat regressions**

~~~
dotnet run --project tools/Elyndor.ContentValidator/Elyndor.ContentValidator.csproj --configuration Release --no-restore -- content/package.json
dotnet build Elyndor.slnx --configuration Release --no-restore
dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --no-restore --filter FullyQualifiedName~Combat
dotnet test tests/Elyndor.IntegrationTests/Elyndor.IntegrationTests.csproj --no-restore --filter "FullyQualifiedName~Combat|FullyQualifiedName~Dungeon"
~~~

- [ ] **Step 3: Inspect final change set**

~~~
git diff main...HEAD --check
git diff main...HEAD --stat
git status --short
~~~

- [ ] **Step 4: Commit only verified fixes and report exact results**

Do not merge or push without explicit user direction.

