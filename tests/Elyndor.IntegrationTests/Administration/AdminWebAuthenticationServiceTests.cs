using Elyndor.Infrastructure.Administration;
using Elyndor.Server.Administration;
using Microsoft.Extensions.Options;

namespace Elyndor.IntegrationTests.Administration;

public sealed class AdminWebAuthenticationServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 5, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task IssuedCodeIsOneTimeAndExpiresAfterFiveMinutes()
    {
        RecordingSender sender = new();
        MutableTimeProvider timeProvider = new(Now);
        AdminWebAuthenticationService service =
            CreateService(sender, timeProvider, 42);

        AdminWebAuthenticationIssueResult issue =
            await service.IssueCodeAsync(42, CancellationToken.None);

        Assert.Equal(AdminWebAuthenticationIssueStatus.Issued, issue.Status);
        Assert.NotNull(issue.ChallengeId);
        Assert.Equal(Now.AddMinutes(5), issue.ExpiresAtUtc);
        Assert.Single(sender.Messages);

        string code = sender.Messages[0].Text
            .Split("Код входа: ", StringSplitOptions.None)[1]
            .Split('\n')[0];

        Assert.Equal(
            AdminWebAuthenticationVerificationStatus.Success,
            service.VerifyCode(issue.ChallengeId!.Value, 42, code));
        Assert.Equal(
            AdminWebAuthenticationVerificationStatus.Invalid,
            service.VerifyCode(issue.ChallengeId.Value, 42, code));
    }

    [Fact]
    public async Task DisallowedAdministratorNeverReceivesCode()
    {
        RecordingSender sender = new();
        AdminWebAuthenticationService service =
            CreateService(sender, new MutableTimeProvider(Now), 42);

        AdminWebAuthenticationIssueResult issue =
            await service.IssueCodeAsync(99, CancellationToken.None);

        Assert.Equal(AdminWebAuthenticationIssueStatus.NotAllowed, issue.Status);
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task RepeatedCodeRequestIsRateLimited()
    {
        RecordingSender sender = new();
        AdminWebAuthenticationService service =
            CreateService(sender, new MutableTimeProvider(Now), 42);

        AdminWebAuthenticationIssueResult first =
            await service.IssueCodeAsync(42, CancellationToken.None);
        AdminWebAuthenticationIssueResult second =
            await service.IssueCodeAsync(42, CancellationToken.None);

        Assert.Equal(AdminWebAuthenticationIssueStatus.Issued, first.Status);
        Assert.Equal(AdminWebAuthenticationIssueStatus.RateLimited, second.Status);
        Assert.Single(sender.Messages);
    }

    [Fact]
    public async Task ExpiredCodeCannotBeUsed()
    {
        RecordingSender sender = new();
        MutableTimeProvider timeProvider = new(Now);
        AdminWebAuthenticationService service =
            CreateService(sender, timeProvider, 42);
        AdminWebAuthenticationIssueResult issue =
            await service.IssueCodeAsync(42, CancellationToken.None);
        string code = sender.Messages[0].Text
            .Split("Код входа: ", StringSplitOptions.None)[1]
            .Split('\n')[0];

        timeProvider.UtcNow = Now.AddMinutes(6);

        Assert.Equal(
            AdminWebAuthenticationVerificationStatus.Expired,
            service.VerifyCode(issue.ChallengeId!.Value, 42, code));
    }

    private static AdminWebAuthenticationService CreateService(
        ITelegramMessageSender sender,
        TimeProvider timeProvider,
        params long[] allowedUserIds) =>
        new(
            sender,
            Options.Create(new TelegramAdminOptions
            {
                AllowedUserIds = allowedUserIds
            }),
            timeProvider);

    private sealed class RecordingSender : ITelegramMessageSender
    {
        public List<(long ChatId, string Text)> Messages { get; } = [];

        public Task SendAsync(
            long chatId,
            string text,
            CancellationToken cancellationToken)
        {
            Messages.Add((chatId, text));
            return Task.CompletedTask;
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
