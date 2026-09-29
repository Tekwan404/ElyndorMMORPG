using Elyndor.Core.Characters;
using Elyndor.Core.Content;
using Elyndor.Core.Economy;
using Elyndor.Core.Identity;
using Elyndor.Core.Items;
using Elyndor.Core.World;
using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Economy;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Elyndor.IntegrationTests.Economy;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class AuctionTelegramNotificationTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly Clock _clock = new();
    private IContentSnapshotProvider _content = null!;

    public async Task InitializeAsync()
    {
        await postgres.ResetAsync();
        _content = new StaticContentSnapshotProvider(
            await GameContentPackageLoader.LoadAsync(Path.GetFullPath("content/package.json")));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SuccessfulSaleNotifiesSellerOnceAndReplayDoesNotDuplicateMessage()
    {
        Player seller = await CreatePlayerAsync();
        Player buyer = await CreatePlayerAsync();
        Guid itemId = await CreateItemAsync(seller.CharacterId);
        RecordingTelegramMessageSender sender = new();

        await using GameDbContext db = postgres.CreateDbContext();
        AuctionSettlementService service = CreateService(db, sender);
        CommerceResult<AuctionResponse> created = await service.CreateAsync(
            seller.AccountId,
            new AuctionCreateRequest(Guid.NewGuid(), itemId, 100),
            CancellationToken.None);
        Assert.True(created.Succeeded, created.ErrorCode);

        Guid requestId = Guid.NewGuid();
        CommerceResult<AuctionResponse> first = await service.BuyAsync(
            buyer.AccountId,
            created.Snapshot!.Id,
            requestId,
            CancellationToken.None);
        CommerceResult<AuctionResponse> replay = await service.BuyAsync(
            buyer.AccountId,
            created.Snapshot.Id,
            requestId,
            CancellationToken.None);

        Assert.True(first.Succeeded, first.ErrorCode);
        Assert.True(replay.Succeeded, replay.ErrorCode);
        TelegramMessage message = Assert.Single(sender.Messages);
        Assert.Equal(seller.TelegramUserId, message.ChatId);
        string itemName = _content.GetCurrent().Indexes.ItemsById["SMALL_HEALING_POTION"].Name;
        Assert.Equal($"Ваш предмет «{itemName}» был куплен.", message.Text);
    }

    [Fact]
    public async Task TelegramFailureDoesNotRollbackCommittedAuctionSale()
    {
        Player seller = await CreatePlayerAsync();
        Player buyer = await CreatePlayerAsync();
        Guid itemId = await CreateItemAsync(seller.CharacterId);
        RecordingTelegramMessageSender sender = new() { ThrowOnSend = true };

        await using GameDbContext db = postgres.CreateDbContext();
        AuctionSettlementService service = CreateService(db, sender);
        CommerceResult<AuctionResponse> created = await service.CreateAsync(
            seller.AccountId,
            new AuctionCreateRequest(Guid.NewGuid(), itemId, 100),
            CancellationToken.None);
        Assert.True(created.Succeeded, created.ErrorCode);

        CommerceResult<AuctionResponse> result = await service.BuyAsync(
            buyer.AccountId,
            created.Snapshot!.Id,
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.True(result.Succeeded, result.ErrorCode);
        await using GameDbContext verify = postgres.CreateDbContext();
        Assert.Equal("SOLD", (await verify.AuctionListings.FindAsync(created.Snapshot.Id))!.State);
        Assert.Equal(194, (await verify.Characters.FindAsync(seller.CharacterId))!.Gold);
        Assert.Equal(0, (await verify.Characters.FindAsync(buyer.CharacterId))!.Gold);
        Assert.Single(await verify.CommerceMails.ToArrayAsync());
    }

    private AuctionSettlementService CreateService(GameDbContext db, ITelegramMessageSender sender) =>
        new(
            db,
            new CommerceTransaction(db, _content, _clock),
            _content,
            _clock,
            Options.Create(new AuctionOptions()),
            sender);

    private async Task<Player> CreatePlayerAsync(long gold = 100)
    {
        await using GameDbContext db = postgres.CreateDbContext();
        Guid accountId = Guid.NewGuid();
        Guid characterId = Guid.NewGuid();
        long telegramUserId = Random.Shared.NextInt64(1, long.MaxValue);
        db.Accounts.Add(new Account(accountId, telegramUserId, _clock.Now));
        Character character = new(
            characterId,
            accountId,
            Guid.NewGuid(),
            "Trader",
            $"T{characterId:N}"[..16],
            "HUMAN",
            "MALE",
            "WARRIOR",
            _clock.Now);
        character.AddGold(gold);
        db.Characters.Add(character);
        db.CharacterLocations.Add(new CharacterLocation(characterId, "STARTER_TOWN", 1, _clock.Now));
        await db.SaveChangesAsync();
        return new Player(accountId, characterId, telegramUserId);
    }

    private async Task<Guid> CreateItemAsync(Guid characterId)
    {
        await using GameDbContext db = postgres.CreateDbContext();
        CharacterItem item = new(Guid.NewGuid(), characterId, "SMALL_HEALING_POTION", 1, _clock.Now);
        db.CharacterItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private sealed record Player(Guid AccountId, Guid CharacterId, long TelegramUserId);
    private sealed record TelegramMessage(long ChatId, string Text);

    private sealed class RecordingTelegramMessageSender : ITelegramMessageSender
    {
        public List<TelegramMessage> Messages { get; } = [];
        public bool ThrowOnSend { get; init; }

        public Task SendAsync(long chatId, string text, CancellationToken cancellationToken)
        {
            if (ThrowOnSend) throw new InvalidOperationException("telegram unavailable");
            Messages.Add(new TelegramMessage(chatId, text));
            return Task.CompletedTask;
        }
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 29, 15, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
