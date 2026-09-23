using Elyndor.Core.Characters;
using Elyndor.Core.Identity;
using Elyndor.Core.Parties;
using Elyndor.Infrastructure.Administration;
using Elyndor.Infrastructure.Parties;
using Elyndor.Infrastructure.Persistence;
using Elyndor.IntegrationTests.Postgres;
using Elyndor.Server.Administration;
using Elyndor.Server.Parties;
using Microsoft.Extensions.Logging.Abstractions;

namespace Elyndor.IntegrationTests.Parties;

[Collection(PostgresFixtureDefinition.Name)]
public sealed class PartyInviteTelegramNotifierTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 23, 14, 0, 0, TimeSpan.Zero);
    private static readonly Guid InviteId =
        Guid.Parse("55555555-5555-5555-5555-555555555599");

    public Task InitializeAsync() => postgres.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task NotificationUsesTargetTelegramIdInviterNameAndAcceptButton()
    {
        (Guid inviterCharacterId, Guid targetCharacterId) = await SeedCharactersAsync();
        RecordingTelegramMessageSender sender = new();

        await using GameDbContext context = postgres.CreateDbContext();
        PartyInviteTelegramNotifier notifier = new(
            context,
            sender,
            NullLogger<PartyInviteTelegramNotifier>.Instance);

        await notifier.NotifyAsync(
            CreateInvite(inviterCharacterId, targetCharacterId),
            CancellationToken.None);

        Assert.Equal(5002L, sender.ChatId);
        Assert.Equal(
            "👥 Leader приглашает вас в группу в Elyndor.\n\nПриглашение действует 5 минут.",
            sender.Text);
        Assert.Equal("✅ Принять и войти", sender.ButtonText);
        Assert.Equal(
            $"https://elyndor.su/world?partyInvite={InviteId:D}",
            sender.WebAppUrl);
        Assert.Equal(1, sender.WebAppSends);
        Assert.Equal(0, sender.PlainSends);
    }

    [Fact]
    public async Task TelegramFailureDoesNotFailCommittedInviteNotificationFlow()
    {
        (Guid inviterCharacterId, Guid targetCharacterId) = await SeedCharactersAsync();
        ThrowingTelegramMessageSender sender = new();

        await using GameDbContext context = postgres.CreateDbContext();
        PartyInviteTelegramNotifier notifier = new(
            context,
            sender,
            NullLogger<PartyInviteTelegramNotifier>.Instance);

        await notifier.NotifyAsync(
            CreateInvite(inviterCharacterId, targetCharacterId),
            CancellationToken.None);

        Assert.Equal(1, sender.Attempts);
    }

    private async Task<(Guid InviterCharacterId, Guid TargetCharacterId)> SeedCharactersAsync()
    {
        Guid inviterAccountId = Guid.Parse("50000000-0000-0000-0000-000000000001");
        Guid targetAccountId = Guid.Parse("50000000-0000-0000-0000-000000000002");
        Guid inviterCharacterId = Guid.Parse("55555555-5555-5555-5555-555555555551");
        Guid targetCharacterId = Guid.Parse("55555555-5555-5555-5555-555555555552");

        await using GameDbContext context = postgres.CreateDbContext();
        context.Accounts.AddRange(
            new Account(inviterAccountId, 5001, Now),
            new Account(targetAccountId, 5002, Now));
        context.Characters.AddRange(
            new Character(
                inviterCharacterId,
                inviterAccountId,
                Guid.NewGuid(),
                "Leader",
                "LEADER",
                "HUMAN",
                "MALE",
                "WARRIOR",
                Now),
            new Character(
                targetCharacterId,
                targetAccountId,
                Guid.NewGuid(),
                "Target",
                "TARGET",
                "HUMAN",
                "FEMALE",
                "MAGE",
                Now));
        await context.SaveChangesAsync();

        return (inviterCharacterId, targetCharacterId);
    }

    private static PartyInviteView CreateInvite(Guid inviterCharacterId, Guid targetCharacterId) =>
        new(
            InviteId,
            Guid.Parse("55555555-5555-5555-5555-555555555598"),
            inviterCharacterId,
            targetCharacterId,
            PartyInviteMode.Direct,
            PartyInviteStatus.Pending,
            Now,
            Now.AddMinutes(5));

    private sealed class RecordingTelegramMessageSender : ITelegramMessageSender, ITelegramWebAppMessageSender
    {
        public long? ChatId { get; private set; }

        public string? Text { get; private set; }

        public string? ButtonText { get; private set; }

        public string? WebAppUrl { get; private set; }

        public int PlainSends { get; private set; }

        public int WebAppSends { get; private set; }

        public Task SendAsync(long chatId, string text, CancellationToken cancellationToken)
        {
            PlainSends++;
            ChatId = chatId;
            Text = text;
            return Task.CompletedTask;
        }

        public Task SendWebAppAsync(
            long chatId,
            string text,
            string buttonText,
            string webAppUrl,
            CancellationToken cancellationToken)
        {
            WebAppSends++;
            ChatId = chatId;
            Text = text;
            ButtonText = buttonText;
            WebAppUrl = webAppUrl;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingTelegramMessageSender : ITelegramMessageSender, ITelegramWebAppMessageSender
    {
        public int Attempts { get; private set; }

        public Task SendAsync(long chatId, string text, CancellationToken cancellationToken)
        {
            Attempts++;
            throw new HttpRequestException("Telegram unavailable.");
        }

        public Task SendWebAppAsync(
            long chatId,
            string text,
            string buttonText,
            string webAppUrl,
            CancellationToken cancellationToken)
        {
            Attempts++;
            throw new HttpRequestException("Telegram unavailable.");
        }
    }
}
