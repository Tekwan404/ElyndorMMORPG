using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Economy;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Economy;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.Server.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Elyndor.IntegrationTests.Economy;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class PlayerCommerceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly Clock _clock = new();
    private IContentSnapshotProvider _content = null!;
    public async Task InitializeAsync()
    {
        await postgres.ResetAsync();
        _content = new StaticContentSnapshotProvider(await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json")));
    }
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TradeTransfersBothOffersAtomicallyAndReplayDoesNotPayTwice()
    {
        var (a, b, id, item) = await PreparedTrade();
        Guid request = Guid.NewGuid();
        Assert.True((await Act(a, id, "CONFIRM", 1)).Succeeded);
        Assert.Equal("COMPLETED", (await Act(b, id, "CONFIRM", 1, request)).Snapshot!.State);
        Assert.True((await Act(b, id, "CONFIRM", 1, request)).Succeeded);
        await using var db = postgres.CreateDbContext();
        Assert.Equal(b.Character, (await db.CharacterItems.SingleAsync(x => x.Id == item)).CharacterId);
        Assert.Equal(90, (await db.Characters.FindAsync(a.Character))!.Gold);
        Assert.Equal(110, (await db.Characters.FindAsync(b.Character))!.Gold);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedFinalValidationRollsBackEverything(bool insufficientFunds)
    {
        var (a, b, id, item) = await PreparedTrade();
        Assert.True((await Act(a, id, "CONFIRM", 1)).Succeeded);
        await using (var db = postgres.CreateDbContext())
        {
            if (insufficientFunds) await db.Characters.Where(x => x.Id == a.Character).ExecuteUpdateAsync(s => s.SetProperty(x => x.Gold, 0));
            else await db.CharacterItems.Where(x => x.Id == item).ExecuteDeleteAsync();
        }
        var result = await Act(b, id, "CONFIRM", 1);
        Assert.False(result.Succeeded);
        Assert.Equal(insufficientFunds ? "commerce_insufficient_funds" : "commerce_item_missing", result.ErrorCode);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(100, (await verify.Characters.FindAsync(b.Character))!.Gold);
        Assert.Equal("OPEN", (await verify.PlayerTrades.FindAsync(id))!.State);
        Assert.False((await verify.PlayerTrades.FindAsync(id))!.ConfirmedB);
    }

    [Fact]
    public async Task DisconnectBeforeCommitReleasesItemsAndNeverTransfersMoney()
    {
        var (a, b, id, item) = await PreparedTrade();
        Assert.True((await Act(a, id, "CONFIRM", 1)).Succeeded);
        await using (var db = postgres.CreateDbContext()) await Trade(db).DisconnectAsync(b.Account, b.Connection, default);
        Assert.False((await Act(b, id, "CONFIRM", 1)).Succeeded);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(a.Character, (await verify.CharacterItems.FindAsync(item))!.CharacterId);
        Assert.Null((await verify.CharacterItems.FindAsync(item))!.TransactionLockId);
        Assert.Equal(100, (await verify.Characters.FindAsync(a.Character))!.Gold);
        Assert.Equal(100, (await verify.Characters.FindAsync(b.Character))!.Gold);
    }

    [Fact]
    public async Task RecipientCanDeclineBeforeJoiningAndOfferLocksAreReleased()
    {
        var a = await Player(); var b = await Player(); var item = await Item(a);
        await using var db = postgres.CreateDbContext();
        var service = Trade(db);
        var opened = await service.OpenAsync(a.Account, b.Character, Guid.NewGuid(), a.Connection, default);
        var id = opened.Snapshot!.Id;
        Assert.True((await service.ActAsync(a.Account, id, Guid.NewGuid(), "OFFER", 0, [item], 10, a.Connection, default)).Succeeded);
        var declined = await service.ActAsync(b.Account, id, Guid.NewGuid(), "DECLINE", 1, null, 0, b.Connection, default);
        Assert.Equal("CANCELLED", declined.Snapshot!.State);
        Assert.Null((await db.CharacterItems.FindAsync(item))!.TransactionLockId);
        Assert.Equal(100, (await db.Characters.FindAsync(a.Character))!.Gold);
        Assert.Equal(100, (await db.Characters.FindAsync(b.Character))!.Gold);
    }

    [Fact]
    public async Task OpeningTradePushesInvitationToOtherAccountThroughAuthenticatedHub()
    {
        var a = await Player(); var b = await Player(); var stranger = await Player();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:game", postgres.ConnectionString);
            builder.UseSetting("Authentication:Issuer", "Elyndor.Tests");
            builder.UseSetting("Authentication:Audience", "Elyndor.Tests.Client");
            builder.UseSetting("Authentication:SigningKey", "player-commerce-test-signing-key-with-more-than-32-bytes");
            builder.UseSetting("Authentication:Telegram:BotToken", "123456:TEST_TOKEN");
            builder.UseSetting("Authentication:Development:Enabled", "false");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_clock);
            });
        });
        var issuer = factory.Services.GetRequiredService<JwtTokenIssuer>();
        async Task<HubConnection> Connect(Guid account)
        {
            var token = issuer.Issue(account, Random.Shared.NextInt64(1, long.MaxValue));
            var hub = new HubConnectionBuilder().WithUrl("http://localhost/hubs/trade", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token.AccessToken);
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            }).Build();
            await hub.StartAsync();
            return hub;
        }
        await using var sender = await Connect(a.Account);
        await using var recipient = await Connect(b.Account);
        await using var unrelated = await Connect(stranger.Account);
        var received = new TaskCompletionSource<TradeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        recipient.On<TradeResponse>("TradeUpdated", snapshot => received.TrySetResult(snapshot));
        var result = await sender.InvokeAsync<CommerceResult<TradeResponse>>("Open", b.Character, Guid.NewGuid());
        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(result.Snapshot!.Id, (await received.Task.WaitAsync(TimeSpan.FromSeconds(5))).Id);
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => unrelated.InvokeAsync<TradeResponse>("Get", result.Snapshot.Id));
        Assert.Contains("trade_not_participant", denied.Message);
    }

    [Fact]
    public async Task ConcurrentConfirmationsSettleOnce()
    {
        var (a, b, id, _) = await PreparedTrade();
        await Act(a, id, "CONFIRM", 1);
        var results = await Task.WhenAll(Act(b, id, "CONFIRM", 1), Act(b, id, "CONFIRM", 1));
        Assert.Single(results, x => x.Succeeded);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal(90, (await verify.Characters.FindAsync(a.Character))!.Gold);
        Assert.Equal(110, (await verify.Characters.FindAsync(b.Character))!.Gold);
    }

    [Fact]
    public async Task OfferReplayDoesNotInvalidateNewConfirmationAndChangedPayloadConflicts()
    {
        var (a, b, id, item) = await PreparedTrade();
        Guid request = Guid.NewGuid();
        await using var db = postgres.CreateDbContext();
        var service = Trade(db);
        Assert.True((await service.ActAsync(a.Account, id, request, "OFFER", 1, [item], 20, a.Connection, default)).Succeeded);
        await Act(a, id, "LOCK", 2); await Act(b, id, "LOCK", 2); await Act(b, id, "CONFIRM", 2);
        var replay = await service.ActAsync(a.Account, id, request, "OFFER", 1, [item], 20, a.Connection, default);
        Assert.True(replay.Snapshot!.ConfirmedB);
        Assert.Equal("commerce_request_conflict", (await service.ActAsync(a.Account, id, request, "OFFER", 1, [item], 30, a.Connection, default)).ErrorCode);
    }

    [Fact]
    public async Task BuyDebitsCreditsTaxesAndDeliversExactlyOnce()
    {
        var seller = await Player(); var buyer = await Player();
        var item = await Item(seller);
        var lot = await Listing(seller, item);
        Guid request = Guid.NewGuid();
        Assert.True((await Buy(buyer, lot.Id, request)).Succeeded);
        Assert.True((await Buy(buyer, lot.Id, request)).Succeeded);
        await using var db = postgres.CreateDbContext();
        Assert.Equal(194, (await db.Characters.FindAsync(seller.Character))!.Gold); // 100 - 1 fee + 95 proceeds
        Assert.Equal(0, (await db.Characters.FindAsync(buyer.Character))!.Gold);
        Assert.Empty(await db.CharacterItems.ToArrayAsync());
        Assert.Single(await db.CommerceMails.ToArrayAsync());
        Assert.Equal(buyer.Character, (await db.CharacterItems.IgnoreQueryFilters().SingleAsync()).CharacterId);
        var service = Auction(db);
        var mailbox = await service.MailAsync(buyer.Account, default);
        Assert.Equal("SMALL_HEALING_POTION", Assert.Single(mailbox).ItemDefinitionId);
        Assert.Equal(1, mailbox[0].Quantity);
        Assert.Equal("PURCHASE", mailbox[0].Source);
        Assert.True((await service.ClaimAsync(buyer.Account, lot.Id, Guid.NewGuid(), default)).Succeeded);
        Assert.True((await service.ClaimAsync(buyer.Account, lot.Id, Guid.NewGuid(), default)).Succeeded);
        Assert.Single(await db.CharacterItems.ToArrayAsync());
    }

    [Fact]
    public async Task AuctionCatalogExposesOnlyActiveOwnedEscrowWithItemPresentation()
    {
        var seller = await Player(); var buyer = await Player();
        var item = await Item(seller);
        var lot = await Listing(seller, item);
        await using var db = postgres.CreateDbContext();
        var service = Auction(db);
        var row = Assert.Single(await service.ListingsAsync(buyer.Account, false, null, null, 0, default));
        Assert.Equal(lot.Id, row.Id);
        Assert.Equal("100", row.Price);
        Assert.Equal(item, row.ItemId);
        Assert.Equal("SMALL_HEALING_POTION", row.ItemDefinitionId);
        Assert.False(string.IsNullOrWhiteSpace(row.Name));
        Assert.Empty(await service.ListingsAsync(buyer.Account, true, null, null, 0, default));
        Assert.Single(await service.ListingsAsync(seller.Account, true, row.Name, row.Type, 0, default));
        Assert.Empty(await service.ListingsAsync(buyer.Account, false, "not-a-real-item", null, 0, default));
        Assert.Empty(await service.ListingsAsync(buyer.Account, false, null, "Equipment", 0, default));
        Assert.Empty(await service.ListingsAsync(buyer.Account, false, null, null, 1, default));
        Assert.True((await service.BuyAsync(buyer.Account, lot.Id, Guid.NewGuid(), default)).Succeeded);
        Assert.Empty(await service.ListingsAsync(buyer.Account, false, null, null, 0, default));
    }

    [Fact]
    public async Task TwoBuyersCannotSettleSameListing()
    {
        var seller = await Player(); var a = await Player(); var b = await Player();
        var lot = await Listing(seller, await Item(seller));
        var results = await Task.WhenAll(Buy(a, lot.Id), Buy(b, lot.Id));
        Assert.Single(results, x => x.Succeeded);
        await using var db = postgres.CreateDbContext();
        Assert.Equal(194, (await db.Characters.FindAsync(seller.Character))!.Gold);
        Assert.Equal(100, await db.Characters.Where(x => x.Id == a.Character || x.Id == b.Character).SumAsync(x => x.Gold));
        Assert.Single(await db.CommerceMails.ToArrayAsync());
    }

    [Fact]
    public async Task ConcurrentReplayOfSameBuyRequestHasOneSettlement()
    {
        var seller = await Player(); var buyer = await Player();
        var lot = await Listing(seller, await Item(seller));
        Guid requestId = Guid.NewGuid();
        var results = await Task.WhenAll(Buy(buyer, lot.Id, requestId), Buy(buyer, lot.Id, requestId));
        Assert.All(results, x => Assert.True(x.Succeeded, x.ErrorCode));
        await using var db = postgres.CreateDbContext();
        Assert.Equal(194, (await db.Characters.FindAsync(seller.Character))!.Gold);
        Assert.Equal(0, (await db.Characters.FindAsync(buyer.Character))!.Gold);
        Assert.Single(await db.CommerceMails.ToArrayAsync());
        Assert.Single(await db.CharacterMutations.Where(x => x.CharacterId == buyer.Character && x.MutationId == requestId).ToArrayAsync());
    }

    [Fact]
    public async Task RequestIdWithDifferentAuctionPayloadCannotCreateAnotherListing()
    {
        var seller = await Player();
        var first = await Item(seller); var second = await Item(seller);
        Guid requestId = Guid.NewGuid();
        await using var db = postgres.CreateDbContext();
        var service = Auction(db);
        Assert.True((await service.CreateAsync(seller.Account, new AuctionCreateRequest(requestId, first, 100), default)).Succeeded);
        Assert.Equal("commerce_request_conflict", (await service.CreateAsync(seller.Account, new AuctionCreateRequest(requestId, second, 100), default)).ErrorCode);
        Assert.Single(await db.AuctionListings.ToArrayAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancellationOrExpirationCannotRaceSale(bool expired)
    {
        var seller = await Player(); var buyer = await Player();
        var lot = await Listing(seller, await Item(seller));
        if (expired) _clock.Now = _clock.Now.AddHours(48);
        await using var db = postgres.CreateDbContext();
        var result = await Task.WhenAll(Buy(buyer, lot.Id), Auction(db).ReturnAsync(seller.Account, lot.Id, Guid.NewGuid(), expired, default));
        Assert.Single(result, x => x.Succeeded);
        await using var verify = postgres.CreateDbContext();
        string state = (await verify.AuctionListings.FindAsync(lot.Id))!.State;
        Assert.Equal(state is "SOLD" or "EXPIRED" ? 1 : 0, await verify.CommerceMails.CountAsync());
        Assert.Equal(state == "SOLD" ? 194 : 99, (await verify.Characters.FindAsync(seller.Character))!.Gold);
        Assert.Equal(state == "SOLD" ? 0 : 100, (await verify.Characters.FindAsync(buyer.Character))!.Gold);
        if (expired) Assert.Equal("EXPIRED", state);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BuyRevalidatesFundsAndItemOwner(bool insufficientFunds)
    {
        var seller = await Player(); var buyer = await Player(insufficientFunds ? 0 : 100);
        Guid item = await Item(seller);
        var lot = await Listing(seller, item);
        await using (var db = postgres.CreateDbContext())
            if (!insufficientFunds) await db.CharacterItems.IgnoreQueryFilters().Where(x => x.Id == item)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.CharacterId, buyer.Character));
        Assert.False((await Buy(buyer, lot.Id)).Succeeded);
        await using var verify = postgres.CreateDbContext();
        Assert.Equal("ACTIVE", (await verify.AuctionListings.FindAsync(lot.Id))!.State);
        Assert.Equal(99, (await verify.Characters.FindAsync(seller.Character))!.Gold);
        Assert.Empty(await verify.CommerceMails.ToArrayAsync());
    }

    [Fact]
    public async Task DatabaseFailureAfterWalletDebitRollsBackSettlement()
    {
        var seller = await Player(); var buyer = await Player();
        Guid item = await Item(seller);
        var lot = await Listing(seller, item);
        await using (var setup = postgres.CreateDbContext())
        {
            setup.CommerceMails.Add(new CommerceMail(lot.Id, seller.Character, item, _clock.Now));
            await setup.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<DbUpdateException>(async () => await Buy(buyer, lot.Id));
        await using var verify = postgres.CreateDbContext();
        Assert.Equal("ACTIVE", (await verify.AuctionListings.FindAsync(lot.Id))!.State);
        Assert.Equal(99, (await verify.Characters.FindAsync(seller.Character))!.Gold);
        Assert.Equal(100, (await verify.Characters.FindAsync(buyer.Character))!.Gold);
        var persistedItem = await verify.CharacterItems.IgnoreQueryFilters().SingleAsync(x => x.Id == item);
        Assert.Equal(seller.Character, persistedItem.CharacterId);
        Assert.Equal("AUCTION", persistedItem.Storage);
    }

    [Fact]
    public async Task CancellationAfterExpirationStillReturnsItemThroughMailbox()
    {
        var seller = await Player();
        Guid item = await Item(seller);
        var lot = await Listing(seller, item);
        _clock.Now = _clock.Now.AddHours(48);
        await using var db = postgres.CreateDbContext();
        var result = await Auction(db).ReturnAsync(seller.Account, lot.Id, Guid.NewGuid(), false, default);
        Assert.Equal("EXPIRED", result.Snapshot!.State);
        Assert.Single(await db.CommerceMails.ToArrayAsync());
        Assert.Equal("MAILBOX", (await db.CharacterItems.IgnoreQueryFilters().SingleAsync(x => x.Id == item)).Storage);
        Assert.Equal("RETURN", Assert.Single(await Auction(db).MailAsync(seller.Account, default)).Source);
    }

    [Fact]
    public async Task StaleInventoryWriterCannotOverwriteTransactionLock()
    {
        var owner = await Player(); Guid id = await Item(owner);
        await using var stale = postgres.CreateDbContext();
        var item = await stale.CharacterItems.SingleAsync(x => x.Id == id);
        item.AddQuantity(1, 99);
        await Listing(owner, id);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
    }

    private async Task<(PlayerIds A, PlayerIds B, Guid Trade, Guid Item)> PreparedTrade()
    {
        var a = await Player(); var b = await Player(); Guid item = await Item(a);
        await using var db = postgres.CreateDbContext();
        var trade = Trade(db);
        var opened = await trade.OpenAsync(a.Account, b.Character, Guid.NewGuid(), a.Connection, default);
        Assert.True(opened.Succeeded, opened.ErrorCode); Guid id = opened.Snapshot!.Id;
        Assert.True((await Act(b, id, "CONNECT", 0)).Succeeded);
        Assert.True((await trade.ActAsync(a.Account, id, Guid.NewGuid(), "OFFER", 0, [item], 10, a.Connection, default)).Succeeded);
        Assert.True((await Act(a, id, "LOCK", 1)).Succeeded);
        Assert.True((await Act(b, id, "LOCK", 1)).Succeeded);
        return (a, b, id, item);
    }
    private async Task<CommerceResult<TradeResponse>> Act(PlayerIds player, Guid id, string action, int revision, Guid? request = null)
    {
        await using var db = postgres.CreateDbContext();
        return await Trade(db).ActAsync(player.Account, id, request ?? Guid.NewGuid(), action, revision, null, 0, player.Connection, default);
    }
    private async Task<CommerceResult<AuctionResponse>> Buy(PlayerIds buyer, Guid id, Guid? request = null)
    {
        await using var db = postgres.CreateDbContext();
        return await Auction(db).BuyAsync(buyer.Account, id, request ?? Guid.NewGuid(), default);
    }
    private async Task<AuctionResponse> Listing(PlayerIds seller, Guid item)
    {
        await using var db = postgres.CreateDbContext();
        var result = await Auction(db).CreateAsync(seller.Account, new AuctionCreateRequest(Guid.NewGuid(), item, 100), default);
        Assert.True(result.Succeeded, result.ErrorCode);
        return result.Snapshot!;
    }
    private async Task<Guid> Item(PlayerIds owner)
    {
        await using var db = postgres.CreateDbContext();
        var item = new CharacterItem(Guid.NewGuid(), owner.Character, "SMALL_HEALING_POTION", 1, _clock.Now);
        db.CharacterItems.Add(item); await db.SaveChangesAsync(); return item.Id;
    }
    private async Task<PlayerIds> Player(long gold = 100)
    {
        await using var db = postgres.CreateDbContext();
        Guid account = Guid.NewGuid(); Guid id = Guid.NewGuid();
        db.Accounts.Add(new Account(account, Random.Shared.NextInt64(1, long.MaxValue), _clock.Now));
        var character = new Character(id, account, Guid.NewGuid(), "Trader", $"T{id:N}"[..16], "HUMAN", "MALE", "WARRIOR", _clock.Now);
        character.AddGold(gold); db.Characters.Add(character);
        db.CharacterLocations.Add(new CharacterLocation(id, "STARTER_TOWN", 1, _clock.Now));
        await db.SaveChangesAsync(); return new PlayerIds(account, id, Guid.NewGuid().ToString());
    }
    private PlayerTradeService Trade(GameDbContext db) => new(db, new CommerceTransaction(db, _content, _clock), _content, _clock);
    private AuctionSettlementService Auction(GameDbContext db) => new(db, new CommerceTransaction(db, _content, _clock), _content, _clock, Options.Create(new AuctionOptions()));
    private sealed record PlayerIds(Guid Account, Guid Character, string Connection);
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 28, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
