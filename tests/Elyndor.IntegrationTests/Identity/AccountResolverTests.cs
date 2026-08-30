using Elyndor.Core.Identity;
using Elyndor.Infrastructure.Identity;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;

namespace Elyndor.IntegrationTests.Identity;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class AccountResolverTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset InitialTime =
        new(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ConcurrentFirstLoginConvergesOnOneAccount()
    {
        Task<Account>[] resolutions = Enumerable.Range(0, 8)
            .Select(_ => ResolveWithNewContextAsync(42, InitialTime))
            .ToArray();

        Account[] accounts = await Task.WhenAll(resolutions);

        Assert.Single(accounts.Select(account => account.Id).Distinct());

        await using GameDbContext verificationContext = postgres.CreateDbContext();
        Account stored = await verificationContext.Accounts.SingleAsync();

        Assert.Equal(42, stored.TelegramUserId);
        Assert.Equal(InitialTime, stored.LastSeenAtUtc);
    }

    [Fact]
    public async Task OlderConcurrentObservationCannotMoveLastSeenBackwards()
    {
        await ResolveWithNewContextAsync(84, InitialTime);
        await ResolveWithNewContextAsync(84, InitialTime.AddMinutes(5));

        Account account = await ResolveWithNewContextAsync(
            84,
            InitialTime.AddMinutes(3));

        Assert.Equal(InitialTime.AddMinutes(5), account.LastSeenAtUtc);
    }

    private async Task<Account> ResolveWithNewContextAsync(
        long telegramUserId,
        DateTimeOffset utcNow)
    {
        await using GameDbContext context = postgres.CreateDbContext();
        AccountResolver resolver = new(context, new FixedTimeProvider(utcNow));

        return await resolver.ResolveAsync(telegramUserId, CancellationToken.None);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
