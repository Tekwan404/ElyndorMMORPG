using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text.RegularExpressions;
using Elyndor.Contracts.Combat;
using Elyndor.Core.Combat.Sessions;
using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Combat;
using Elyndor.Infrastructure.Progression;
using Elyndor.Infrastructure.World;
using Elyndor.Server.Combat;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Combat;

public sealed partial class AutoAttackFlowTests
{
    [Fact]
    public void EveryLiteralClientHubInvocationHasAPublicServerMethod()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Elyndor.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        string source = File.ReadAllText(Path.Combine(root.FullName, "web/elyndor-web/src/stores/combatSession.ts"));
        string[] calls = Regex.Matches(source,
            @"(?:invokeHub(?:<[^()]*>)?\(\s*|invoke(?:WithOutcome)?\(\s*[^,]+,\s*)'(?<name>[A-Z][A-Za-z]+)'")
            .Select(match => match.Groups["name"].Value).Distinct().Order().ToArray();
        string[] methods = typeof(CombatHub).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.GetCustomAttribute<HubMethodNameAttribute>()?.Name ?? method.Name).ToArray();
        Assert.Contains("UseAbility", calls);
        Assert.Contains("StartCombat", calls);
        Assert.Contains("ResumeCombatFromSequence", calls);
        Assert.Contains("ChooseLootRoll", calls);
        Assert.Empty(calls.Except(methods));
        Assert.Equal(typeof(Guid), typeof(CombatHub).GetMethod("StartCombat")!.GetParameters()[0].ParameterType);
    }

    [Fact]
    public async Task RealHubStartsUsesAbilityResumesAndLeavesWithOneTerminalEventName()
    {
        Guid accountId = Guid.CreateVersion7();
        await using (var db = postgres.CreateDbContext())
        {
            db.Accounts.Add(new Account(accountId, 9680, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        await using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = CreateAuthenticatedClient(factory, accountId, 9680);
        var character = await CreateCharacterAsync(client, "HubLifecycle", "ARCHER");
        await using (var db = postgres.CreateDbContext())
        {
            var location = await db.CharacterLocations.SingleAsync(item => item.CharacterId == character.Id);
            location.Relocate("WHISPERING_FOREST", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }
        var encounters = factory.Services.GetRequiredService<WorldEncounterRegistry>();
        var encounter = encounters.Register(accountId, "WHISPERING_FOREST", "WHISPERING_FOREST_LESNOI_VOLK_L3");
        await using HubConnection hub = CreateHubConnection(factory, IssueToken(factory, accountId, 9680));
        await hub.StartAsync();
        ConcurrentQueue<string> received = new();
        using var updated = hub.On<CombatUpdateResponse>("CombatUpdated", _ => received.Enqueue("CombatUpdated"));
        using var ended = hub.On<CombatUpdateResponse>("CombatEnded", _ => received.Enqueue("CombatEnded"));
        var started = await hub.InvokeAsync<CombatUpdateResponse>("StartCombat", encounter.EncounterId.ToString());
        Assert.True(started.Succeeded, started.ErrorCode);
        var snapshot = started.Snapshot!;
        var ability = snapshot.Player.Abilities.First(item => item.TargetType == "SingleEnemy" && item.ResourceCost <= snapshot.Player.Resource);
        var used = await hub.InvokeAsync<CombatUpdateResponse>("UseAbility", snapshot.SessionId, ability.Id, snapshot.Enemy.ActorId, "lifecycle-ability");
        Assert.True(used.Succeeded, used.ErrorCode);
        var resumed = await hub.InvokeAsync<CombatUpdateResponse>("ResumeCombatFromSequence", used.Snapshot!.Sequence);
        Assert.Equal(character.Id, resumed.Snapshot!.Player.ActorId);
        Assert.Equal(snapshot.SessionId, resumed.Snapshot.SessionId);
        var left = await hub.InvokeAsync<CombatUpdateResponse>("LeaveCombat", "lifecycle-leave");
        Assert.True(left.Succeeded, left.ErrorCode);
        Assert.NotEqual("Active", left.Snapshot!.Status);
        // A subsequent invocation is a transport ordering barrier for prior publications.
        Assert.Equal("pong", await hub.InvokeAsync<string>("Ping"));
        Assert.Contains("CombatUpdated", received);
        Assert.DoesNotContain("CombatEnded", received);
    }

    [Fact]
    public async Task HeldGateReportsCombatBusyAndEmitsHoldDuration()
    {
        Guid accountId = Guid.CreateVersion7();
        await using (var db = postgres.CreateDbContext())
        {
            db.Accounts.Add(new Account(accountId, 9681, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        ConcurrentQueue<double> holds = new();
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == CombatSessionMetrics.MeterName && instrument.Name == "elyndor.combat.gate.hold.duration")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
        {
            Assert.Equal(0, tags.Length);
            holds.Enqueue(value);
        });
        listener.Start();
        await using WebApplicationFactory<Program> factory = CreateFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<CombatSessionRegistry>();
                services.AddSingleton(provider => new CombatSessionRegistry(
                    provider.GetRequiredService<TimeProvider>(), provider.GetRequiredService<ICombatUpdatePublisher>(),
                    provider.GetRequiredService<ICombatSessionFinalizer>(), provider.GetRequiredService<ILogger<CombatSessionRegistry>>(),
                    ioTimeout: TimeSpan.FromMilliseconds(200)));
            }));
        using HttpClient client = CreateAuthenticatedClient(factory, accountId, 9681);
        await CreateCharacterAsync(client, "HubBusy", "ARCHER");
        await using HubConnection hub = CreateHubConnection(factory, IssueToken(factory, accountId, 9681));
        await hub.StartAsync();
        var start = await hub.InvokeAsync<CombatUpdateResponse>("StartTraining");
        Assert.True(start.Succeeded, start.ErrorCode);
        CombatSessionRegistry registry = factory.Services.GetRequiredService<CombatSessionRegistry>();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<CombatOperationResult> holder = registry.ExecuteParticipantAsync(accountId, async (session, characterId, _, _) =>
        {
            entered.SetResult();
            await release.Task;
            return new CombatCommandResult(true, null, session.Snapshot(characterId), []);
        }, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            HubException commandError = await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync<CombatUpdateResponse>(
                "UseAbility", start.Snapshot!.SessionId, "UNKNOWN", start.Snapshot.Enemy.ActorId, "busy"));
            Assert.Contains("combat_busy", commandError.Message);
            HubException resumeError = await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync<CombatUpdateResponse>("ResumeCombatFromSequence", 0L));
            Assert.Contains("combat_busy", resumeError.Message);
        }
        finally { release.TrySetResult(); await holder; }
        Assert.Contains(holds, duration => duration >= 300);
        await hub.InvokeAsync<CombatUpdateResponse>("LeaveCombat", "busy-cleanup");
    }
}
