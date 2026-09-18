# Raid Group Single Roster Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an authoritative RaidGroup of up to 20 players that enters one shared raid combat roster and shares applicable group effects without changing five-player Party or dungeon behavior.

**Architecture:** Add a durable Raid domain beside Party: entities, EF mappings, transactional service, typed endpoints, and a compact Vue roster. Extend combat creation with an explicit context and participant maximum so the five-player default remains intact and a raid captures up to twenty real members into one roster. Resolve group-effect recipients in CombatSession; EffectEngine continues applying each already-targeted effect.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, EF Core 10, PostgreSQL, SignalR, Vue 3, TypeScript, Pinia, Vitest, Playwright.

**Spec:** `docs/superpowers/specs/2026-09-18-raid-group-single-roster-design.md`

## Global Constraints

- PostgreSQL is authoritative; do not add Redis.
- Membership, encounter start, and rewards are server-authoritative, transactional, and replay-safe.
- Ordinary Party and ordinary dungeon combat remain capped at 5.
- Raid default capacity is 20; no subgroup state, UI, or subgroup-limited buffs.
- In a raid combat, group/ally/party/raid effects target every eligible active member of that combat roster.
- Reuse EffectEngine; never copy buff logic into HTTP endpoints.
- UI copy is Russian and mobile-first.

---

## File map

| Path | Responsibility |
| --- | --- |
| `src/Elyndor.Core/Raids/RaidGroupModels.cs` | Raid aggregate, members, invites, roles, ready check. |
| `src/Elyndor.Infrastructure/Raids/RaidService.cs` | Transactional and idempotent membership lifecycle. |
| `src/Elyndor.Infrastructure/Persistence/Configurations/RaidConfiguration.cs` | Tables, constraints, indexes and foreign keys. |
| `src/Elyndor.Infrastructure/Persistence/GameDbContext.cs` | Raid DbSets. |
| `src/Elyndor.Contracts/Raids/RaidContracts.cs` | API DTOs. |
| `src/Elyndor.Server/Raids/RaidEndpoints.cs` | Authenticated intent endpoints. |
| `src/Elyndor.Server/Raids/RaidUpdateFilter.cs` | Post-commit SignalR refresh. |
| `src/Elyndor.Infrastructure/Raids/RaidCombatRosterResolver.cs` | Captures real eligible raid members for a raid encounter. |
| `src/Elyndor.Infrastructure/Combat/CombatSessionFactory.cs` | Explicit combat context and roster size. |
| `src/Elyndor.Core/Combat/Participants/CombatParticipantModels.cs` | Validated default and raid limits. |
| `src/Elyndor.Core/Combat/Sessions/CombatSession.cs` | Raid-aware group-effect recipient resolution. |
| `web/elyndor-web/src/game/raid/raidStore.ts` | Typed raid client state and intents. |
| `web/elyndor-web/src/game/raid/views/RaidView.vue` | One mobile roster; no subgroup UI. |

## Task 1: Raid aggregate and additive persistence

**Files:**
- Create: `src/Elyndor.Core/Raids/RaidGroupModels.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Configurations/RaidConfiguration.cs`
- Modify: `src/Elyndor.Infrastructure/Persistence/GameDbContext.cs`
- Create: `src/Elyndor.Infrastructure/Persistence/Migrations/*_RaidGroupSingleRoster.cs`
- Modify: `src/Elyndor.Infrastructure/Persistence/Migrations/GameDbContextModelSnapshot.cs`
- Test: `tests/Elyndor.UnitTests/Raids/RaidGroupRulesTests.cs`

**Interfaces:**
- Produces `RaidGroup.Create(Guid id, Guid creationRequestId, Guid leaderCharacterId, int maximumMembers, DateTimeOffset createdAtUtc)`.
- Produces `AddMember`, `RemoveMember`, `PromoteAssistant`, `TransferLeadership`, `Disband`, and ready-check state changes.
- Produces `RaidMemberRole`, `RaidMemberState`, `RaidInviteStatus`, and `RaidReadyState`.

- [ ] **Step 1: Write failing domain tests.**

```csharp
[Fact]
public void AddMember_AllowsTwentyAndRejectsTwentyFirst()
{
    RaidGroup raid = CreateRaid();
    for (var index = 0; index < 19; index++)
        raid.AddMember(Guid.NewGuid(), UtcNow);

    Assert.Equal(20, raid.Members.Count);
    Assert.Throws<InvalidOperationException>(() => raid.AddMember(Guid.NewGuid(), UtcNow));
}

[Fact]
public void RaidHasNoSubgroupState() =>
    Assert.DoesNotContain(typeof(RaidGroup).GetProperties(),
        property => property.Name.Contains("Subgroup", StringComparison.OrdinalIgnoreCase));
```

- [ ] **Step 2: Run the test and verify it fails.**

Run: `dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --filter FullyQualifiedName~RaidGroupRulesTests`

Expected: compile failure because `Elyndor.Core.Raids` does not exist.

- [ ] **Step 3: Implement the smallest domain and schema.**

Use `raids`, `raid_members`, `raid_invites`, and `raid_ready_checks`. Store maximum, version, creation request id, and UTC timestamps on the aggregate. `raid_members` has key `(RaidId, CharacterId)`; its active-character membership index is unique. Do not add subgroup fields or tables. Generate an additive migration only: no Party changes and no destructive data operation.

- [ ] **Step 4: Run focused tests and build.**

Run: `dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --filter FullyQualifiedName~RaidGroupRulesTests`

Run: `dotnet build Elyndor.slnx --configuration Release`

- [ ] **Step 5: Commit.**

```powershell
git add src/Elyndor.Core/Raids src/Elyndor.Infrastructure/Persistence tests/Elyndor.UnitTests/Raids
git commit -m "feat: add raid group persistence model"
```

## Task 2: Transactional RaidService and membership rules

**Files:**
- Create: `src/Elyndor.Infrastructure/Raids/RaidService.cs`
- Modify: `src/Elyndor.Infrastructure/DependencyInjection.cs`
- Test: `tests/Elyndor.IntegrationTests/Raids/RaidServiceTests.cs`
- Test: `tests/Elyndor.IntegrationTests/Raids/RaidMembershipConcurrencyTests.cs`

**Interfaces:**
- Consumes Task 1 and the existing `PartyService` transaction/character-lock conventions.
- Produces `RaidSnapshot`, `RaidInviteView`, `RaidOperationResult`, and `RaidErrorCodes`.
- Produces methods: `CreateAsync`, `InviteAsync`, `AcceptInviteAsync`, `DeclineInviteAsync`, `LeaveAsync`, `KickAsync`, `PromoteAssistantAsync`, `TransferLeadershipAsync`, `DisbandAsync`, `BeginReadyCheckAsync`, and `SetReadyStateAsync`.

- [ ] **Step 1: Write failing integration tests.**

```csharp
[Fact]
public async Task AcceptInvite_RejectsCharacterAlreadyInParty()
{
    await CreatePartyForAsync(targetAccountId);
    RaidOperationResult result = await service.AcceptInviteAsync(targetAccountId, inviteId, CancellationToken.None);

    Assert.False(result.IsSuccess);
    Assert.Equal(RaidErrorCodes.AlreadyInGroupContext, result.ErrorCode);
}

[Fact]
public async Task CreateReplay_ReturnsOriginalRaidWithoutSecondRow()
{
    RaidOperationResult first = await service.CreateAsync(leaderAccountId, requestId, CancellationToken.None);
    RaidOperationResult replay = await service.CreateAsync(leaderAccountId, requestId, CancellationToken.None);

    Assert.Equal(first.Snapshot!.RaidId, replay.Snapshot!.RaidId);
    Assert.Equal(1, await dbContext.RaidGroups.CountAsync());
}
```

- [ ] **Step 2: Run focused tests and verify failure.**

Run: `dotnet test tests/Elyndor.IntegrationTests/Elyndor.IntegrationTests.csproj --filter FullyQualifiedName~RaidServiceTests|FullyQualifiedName~RaidMembershipConcurrencyTests`

Expected: compile failure for missing `RaidService`.

- [ ] **Step 3: Implement atomic operations.**

Mirror `PartyService`: wrap transaction work in `dbContext.Database.CreateExecutionStrategy().ExecuteAsync`, acquire advisory locks for the character and raid, reload after locking, and commit before building the snapshot. On accepting an invite verify pending/unexpired state, capacity, character existence, and absence of both Party and Raid membership. Replaying the same operation returns the stored result; reusing an id for different intent returns `raid_idempotency_conflict`.

- [ ] **Step 4: Add race and permission tests.**

```csharp
[Fact]
public async Task ConcurrentFinalSeatAccepts_AdmitsExactlyOneCharacter()
{
    RaidOperationResult[] results = await Task.WhenAll(
        serviceA.AcceptInviteAsync(accountA, inviteA, CancellationToken.None),
        serviceB.AcceptInviteAsync(accountB, inviteB, CancellationToken.None));

    Assert.Single(results.Where(result => result.IsSuccess));
    Assert.Equal(20, await dbContext.RaidMembers.CountAsync(member => member.RaidId == raidId));
}
```

Cover leader/assistant/member permissions, invite expiry, leader handoff, final-member disband, and ready check that never starts combat.

- [ ] **Step 5: Run focused tests and commit.**

Run: `dotnet test tests/Elyndor.IntegrationTests/Elyndor.IntegrationTests.csproj --filter FullyQualifiedName~RaidServiceTests|FullyQualifiedName~RaidMembershipConcurrencyTests`

```powershell
git add src/Elyndor.Infrastructure/Raids src/Elyndor.Infrastructure/DependencyInjection.cs tests/Elyndor.IntegrationTests/Raids
git commit -m "feat: add transactional raid membership service"
```

## Task 3: Typed API and post-commit realtime updates

**Files:**
- Create: `src/Elyndor.Contracts/Raids/RaidContracts.cs`
- Create: `src/Elyndor.Server/Raids/RaidEndpoints.cs`
- Create: `src/Elyndor.Server/Raids/RaidUpdateFilter.cs`
- Modify: `src/Elyndor.Server/Program.cs`
- Test: `tests/Elyndor.IntegrationTests/Raids/RaidEndpointsTests.cs`

**Interfaces:**
- Produces `/api/v1/raid` GET/POST, invite, accept/decline, leave, kick, role, disband, ready-check and readiness endpoints.
- Produces `RaidUpdated` only after successful mutations.

- [ ] **Step 1: Write endpoint tests.**

```csharp
[Fact]
public async Task CreateRaid_RequiresAuthenticatedAccount() =>
    Assert.Equal(HttpStatusCode.Unauthorized,
        (await client.PostAsJsonAsync("/api/v1/raid", new { requestId = Guid.NewGuid() })).StatusCode);

[Fact]
public async Task AcceptInvite_WhenTargetHasParty_ReturnsConflictWithStableCode()
{
    HttpResponseMessage response = await targetClient.PostAsync($"/api/v1/raid/invites/{inviteId}/accept", null);
    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("raid_already_in_group_context", await ReadProblemCodeAsync(response));
}
```

- [ ] **Step 2: Run and verify route failure.**

Run: `dotnet test tests/Elyndor.IntegrationTests/Elyndor.IntegrationTests.csproj --filter FullyQualifiedName~RaidEndpointsTests`

Expected: the endpoint does not exist.

- [ ] **Step 3: Implement typed intent endpoints.**

Follow `PartyEndpoints`. Requests contain intent IDs only; client never submits capacity, roster, effect recipients, or a combat outcome. Map missing resources to 404, permission denial to 403, membership/capacity/idempotency/state conflicts to 409, malformed input to 422. Publish SignalR only after the service commits.

- [ ] **Step 4: Test publication semantics and commit.**

Add a test that successful acceptance notifies leader and invitee, while a rejected acceptance publishes nothing.

Run: `dotnet test tests/Elyndor.IntegrationTests/Elyndor.IntegrationTests.csproj --filter FullyQualifiedName~RaidEndpointsTests`

```powershell
git add src/Elyndor.Contracts/Raids src/Elyndor.Server/Raids src/Elyndor.Server/Program.cs tests/Elyndor.IntegrationTests/Raids
git commit -m "feat: expose authoritative raid endpoints"
```

## Task 4: One raid combat roster, without raising the Party default

**Files:**
- Modify: `src/Elyndor.Core/Combat/Participants/CombatParticipantModels.cs`
- Modify: `src/Elyndor.Core/Combat/Sessions/CombatSession.cs`
- Modify: `src/Elyndor.Infrastructure/Combat/CombatSessionFactory.cs`
- Modify: `src/Elyndor.Infrastructure/Combat/CombatApplicationService.cs`
- Create: `src/Elyndor.Infrastructure/Raids/RaidCombatRosterResolver.cs`
- Test: `tests/Elyndor.UnitTests/Combat/RaidCombatRosterTests.cs`
- Test: `tests/Elyndor.IntegrationTests/Raids/RaidCombatStartTests.cs`

**Interfaces:**
- Produces an explicit `CombatGroupContext` and an explicit maximum: default party 5 and maximum raid 20.
- Produces `RaidCombatRosterResolver.ResolveAsync(Guid raidId, Guid leaderCharacterId, string locationId, int encounterMaximum, CancellationToken)`.
- Extends `CombatSessionFactory.CreateAsync` with creation options; it must never infer raid mode from participant count.

- [ ] **Step 1: Write limit tests.**

```csharp
[Fact]
public void DefaultRoster_RejectsSixParticipants() =>
    Assert.Throws<ArgumentException>(() => new CombatParticipantRoster(Participants(6), UtcNow));

[Fact]
public void RaidRoster_AllowsTwentyParticipants()
{
    CombatParticipantRoster roster = new(Participants(20), UtcNow, CombatParticipantLimit.MaximumRaid);
    Assert.Equal(20, roster.Participants.Count);
}

[Fact]
public void RaidRoster_RejectsTwentyFirstParticipant() =>
    Assert.Throws<ArgumentException>(() => new CombatParticipantRoster(Participants(21), UtcNow, CombatParticipantLimit.MaximumRaid));
```

- [ ] **Step 2: Run and verify the current global five-player cap fails the raid case.**

Run: `dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --filter FullyQualifiedName~RaidCombatRosterTests`

- [ ] **Step 3: Implement explicit context and captured roster.**

Keep `CombatParticipantRoster.DefaultMaximumParticipants = 5`. Add a separately validated maximum of 20 and have `CombatSession` receive the selected maximum/context. Resolver returns real, eligible raid members only after checking leader ownership, current encounter policy, location, no travel/AFK/combat conflict, and capacity. Capture once: later membership changes cannot alter this combat roster.

- [ ] **Step 4: Add integration tests.**

```csharp
[Fact]
public async Task StartRaidWithTenMembers_CreatesOneTenMemberCombatRoster()
{
    CombatSessionCreationResult result = await StartRaidAsync(memberCount: 10);
    Assert.True(result.Succeeded);
    Assert.Equal(10, result.Participants!.Count);
}

[Fact]
public async Task StartOrdinaryPartyWithSixMembers_RemainsRejected()
{
    CombatSessionCreationResult result = await StartPartyCombatWithMembersAsync(memberCount: 6);
    Assert.False(result.Succeeded);
}
```

Also cover a full 20-member roster, no placeholder member, a member at another location, and a fled member that cannot reattach.

- [ ] **Step 5: Run and commit.**

Run: `dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --filter FullyQualifiedName~RaidCombatRosterTests`

Run: `dotnet test tests/Elyndor.IntegrationTests/Elyndor.IntegrationTests.csproj --filter FullyQualifiedName~RaidCombatStartTests|FullyQualifiedName~MultiplayerCombatFlowTests`

```powershell
git add src/Elyndor.Core/Combat src/Elyndor.Infrastructure/Combat src/Elyndor.Infrastructure/Raids tests/Elyndor.UnitTests/Combat tests/Elyndor.IntegrationTests/Raids
git commit -m "feat: support single-roster raid combat"
```

## Task 5: Group effects target all active raid participants

**Files:**
- Modify: `src/Elyndor.Core/Combat/Sessions/CombatSession.cs`
- Test: `tests/Elyndor.UnitTests/Combat/RaidCombatRosterTests.cs`
- Test: `tests/Elyndor.UnitTests/Combat/EffectEngineTests.cs`

**Interfaces:**
- Consumes Task 4's group context and participant roster.
- Produces one private recipient resolver in `CombatSession`.
- Leaves `EffectEngine.Apply(CombatActorState target, Guid sourceId, EffectDefinition definition, DateTimeOffset now)` unchanged.

- [ ] **Step 1: Write recipient tests.**

```csharp
[Fact]
public void RaidGroupEffect_AppliesToAllTenActiveRaidParticipants()
{
    CombatSession session = CreateRaidSession(memberCount: 10);
    session.ApplyGroupEffect(sourceActorId, definition, UtcNow);

    Assert.All(session.ActivePlayerActors, actor =>
        Assert.Contains(actor.ActiveEffects, effect => effect.Definition.Id == definition.Id));
}

[Fact]
public void RaidGroupEffect_ExcludesFledAndNotYetAttachedParticipants()
{
    CombatSession session = CreateRaidSession(memberCount: 10, attachedMembers: 8);
    session.Flee(fledCharacterId, UtcNow);
    session.ApplyGroupEffect(sourceActorId, definition, UtcNow);

    Assert.Equal(7, CountTargetsWithEffect(session, definition.Id));
}
```

- [ ] **Step 2: Run and verify failure.**

Run: `dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --filter FullyQualifiedName~RaidCombatRosterTests`

- [ ] **Step 3: Implement recipient selection in CombatSession.**

Use one resolver conceptually equivalent to:

```csharp
private IReadOnlyList<CombatParticipantDefinition> ResolveGroupEffectRecipients(Guid sourceActorId) =>
    _groupContext == CombatGroupContext.Raid
        ? ActivePlayerDefinitionsInParticipantRoster()
        : ExistingPartyRecipientRules(sourceActorId);
```

Recipients are only player definitions with `CombatParticipantStatus.Active`. Exclude companions, monsters, dead/fled players, and rostered-but-not-attached players. Apply existing `EffectEngine.Apply` once per recipient and preserve existing event order/source/target data.

- [ ] **Step 4: Add non-raid regression tests.**

Verify self and explicit-target effects do not expand in raids, normal solo combat is unchanged, and five-player Party/dungeon behavior is unchanged.

- [ ] **Step 5: Run and commit.**

Run: `dotnet test tests/Elyndor.UnitTests/Elyndor.UnitTests.csproj --filter FullyQualifiedName~RaidCombatRosterTests|FullyQualifiedName~EffectEngineTests`

```powershell
git add src/Elyndor.Core/Combat/Sessions/CombatSession.cs tests/Elyndor.UnitTests/Combat
git commit -m "feat: share group effects across raid roster"
```

## Task 6: Compact raid UI and client contracts

**Files:**
- Modify: `web/elyndor-web/src/api/contracts.ts`
- Create: `web/elyndor-web/src/game/raid/raidStore.ts`
- Create: `web/elyndor-web/src/game/raid/views/RaidView.vue`
- Modify: the existing party navigation host that renders `PartyView.vue`
- Test: `web/elyndor-web/src/__tests__/raidStore.spec.ts`
- Test: `web/elyndor-web/src/__tests__/RaidView.spec.ts`

**Interfaces:**
- Produces `useRaidStore()` with refresh, creation, invitation, role, ready-check, and membership operations.
- Produces a single roster displaying `N / 20`, roles and readiness.

- [ ] **Step 1: Write failing store and view tests.**

```ts
it('creates a raid with an operation id and refreshes canonical state', async () => {
  await useRaidStore().create()
  expect(request).toHaveBeenCalledWith('/api/v1/raid', expect.objectContaining({ method: 'POST' }))
})

it('renders ten members as one roster and never renders subgroup labels', () => {
  mount(RaidView, { global: { plugins: [piniaWithTenMembers()] } })
  expect(screen.getByText('10 / 20')).toBeTruthy()
  expect(screen.queryByText(/подгруппа/i)).toBeNull()
})
```

- [ ] **Step 2: Run and verify failure.**

Run: `npm run test:unit --prefix web/elyndor-web -- raidStore RaidView`

- [ ] **Step 3: Implement the UI.**

Follow `partyStore.ts` for API/error handling and use existing `UIPanel`, `UIButton`, and `UIModal`. Show one ordered list of members, leader/assistant labels, invitation and ready controls. Do not expose capacity editing, recipient selection, drag/drop, sorting, or any subgroup representation. All labels/errors are Russian.

- [ ] **Step 4: Cover leader-only and member actions.**

Test Russian empty state, member ready response, leader-only kick/promote/disband actions, and API errors. Assert client requests never contain roster, capacity, combat result, effect recipients, or rewards.

- [ ] **Step 5: Run and commit.**

Run: `npm run test:unit --prefix web/elyndor-web -- raidStore RaidView`

Run: `npm run build --prefix web/elyndor-web`

```powershell
git add web/elyndor-web/src/api/contracts.ts web/elyndor-web/src/game/raid web/elyndor-web/src/__tests__
git commit -m "feat: add single-roster raid interface"
```

## Task 7: Recovery, regression suite, and PR

**Files:**
- Modify: `tests/Elyndor.IntegrationTests/Combat/MultiplayerCombatFlowTests.cs`
- Modify: `tests/Elyndor.IntegrationTests/Raids/RaidCombatStartTests.cs`
- Create: `web/elyndor-web/e2e/raid-group.spec.ts`
- Modify: `docs/source-of-truth/gameplay/31_RAID_GROUP_SYSTEM.md` only if the implementation exposes a documented mismatch.

**Interfaces:**
- Consumes Tasks 1–6 and creates no new gameplay architecture.

- [ ] **Step 1: Add browser acceptance.**

```ts
test('ten-player raid uses one roster and has no subgroup controls', async ({ page }) => {
  await seedRaidWithMembers(page, 10)
  await page.goto('/party')
  await expect(page.getByText('10 / 20')).toBeVisible()
  await expect(page.getByText(/подгруппа/i)).toHaveCount(0)
})
```

- [ ] **Step 2: Add restart/replay integration coverage.**

Start a ten-character raid encounter, persist it, create a new service scope/recovery path, and assert the same character IDs and statuses are restored. Replay the start operation and assert it returns the original session, not a second combat/reward path.

- [ ] **Step 3: Run full verification.**

Run: `dotnet build Elyndor.slnx --configuration Release`

Run: `dotnet test Elyndor.slnx --configuration Release`

Run: `dotnet run --project tools/Elyndor.ContentValidator -- content/package.json`

Run: `npm run lint --prefix web/elyndor-web`

Run: `npm run format:check --prefix web/elyndor-web`

Run: `npm run test:unit --prefix web/elyndor-web`

Run: `npm run build --prefix web/elyndor-web`

Run: `npm run test:e2e --prefix web/elyndor-web`

- [ ] **Step 4: Review migration and compatibility.**

Confirm Party and dungeon limits remain five; no subgroup data/UI exists; frontend controls no reward or effect recipients; migration is additive; no secrets/build artifacts are staged.

- [ ] **Step 5: Commit final verification and open, but do not merge, the PR.**

```powershell
git add tests web/elyndor-web/e2e docs/source-of-truth/gameplay/31_RAID_GROUP_SYSTEM.md
git commit -m "test: cover single-roster raid flow"
git push -u origin feat/raid-group-single-roster
gh pr create --base main --head feat/raid-group-single-roster --title "feat: add single-roster raid groups" --fill
```

The PR description must state that raids are one roster of up to 20 active players, group effects reach eligible active raid participants, and ordinary Party/dungeon behavior remains capped at five. Do not merge the PR.

