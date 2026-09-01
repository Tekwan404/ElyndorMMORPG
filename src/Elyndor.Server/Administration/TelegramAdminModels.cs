using System.Text.Json.Serialization;

namespace Elyndor.Server.Administration;

public enum AdminCommandType
{
    Help,
    ShowCharacter,
    SetLevel,
    Restore,
    SetLocation,
    Rename,
    SetClass,
    SetRace,
    Delete,
    Message
}

public sealed record AdminCommand(
    AdminCommandType Type,
    long? TargetTelegramUserId = null,
    string? Value = null,
    int? NumericValue = null);

public sealed record AdminCommandParseResult(bool IsSuccess, AdminCommand? Command, string? ErrorCode)
{
    public static AdminCommandParseResult Success(AdminCommand command) => new(true, command, null);

    public static AdminCommandParseResult Failure(string errorCode) => new(false, null, errorCode);
}

public sealed record TelegramUpdate(
    [property: JsonPropertyName("update_id")] long UpdateId,
    [property: JsonPropertyName("message")] TelegramMessage? Message);

public sealed record TelegramMessage(
    [property: JsonPropertyName("message_id")] long MessageId,
    [property: JsonPropertyName("from")] TelegramUser? From,
    [property: JsonPropertyName("chat")] TelegramChat Chat,
    [property: JsonPropertyName("text")] string? Text);

public sealed record TelegramUser([property: JsonPropertyName("id")] long Id);

public sealed record TelegramChat(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("type")] string Type);
