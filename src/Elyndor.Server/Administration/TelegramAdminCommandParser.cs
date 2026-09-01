namespace Elyndor.Server.Administration;

public static class TelegramAdminCommandParser
{
    public static AdminCommandParseResult Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return AdminCommandParseResult.Failure("admin_command_empty");
        }

        string input = text.Trim();
        int separator = input.IndexOf(' ');
        string name = (separator < 0 ? input : input[..separator]).ToLowerInvariant();
        string arguments = separator < 0 ? string.Empty : input[(separator + 1)..].Trim();

        if (name.Contains('@'))
        {
            name = name[..name.IndexOf('@')];
        }

        if (name == "/help" && arguments.Length == 0)
        {
            return AdminCommandParseResult.Success(new(AdminCommandType.Help));
        }

        if (!TryTakeTarget(arguments, out long targetId, out string remainder))
        {
            return AdminCommandParseResult.Failure("admin_target_invalid");
        }

        return name switch
        {
            "/char" when remainder.Length == 0 => Success(AdminCommandType.ShowCharacter, targetId),
            "/restore" when remainder.Length == 0 => Success(AdminCommandType.Restore, targetId),
            "/level" => ParseLevel(targetId, remainder),
            "/location" => ParseSingleValue(AdminCommandType.SetLocation, targetId, remainder),
            "/class" => ParseSingleValue(AdminCommandType.SetClass, targetId, remainder),
            "/race" => ParseSingleValue(AdminCommandType.SetRace, targetId, remainder),
            "/rename" when remainder.Length > 0 =>
                AdminCommandParseResult.Success(new(AdminCommandType.Rename, targetId, remainder)),
            "/msg" when remainder is { Length: > 0 and <= 4096 } =>
                AdminCommandParseResult.Success(new(AdminCommandType.Message, targetId, remainder)),
            "/delete" => ParseDelete(targetId, remainder),
            _ => AdminCommandParseResult.Failure("admin_command_unknown")
        };
    }

    private static AdminCommandParseResult ParseLevel(long targetId, string value) =>
        int.TryParse(value, out int level) && level is >= 1 and <= 60
            ? AdminCommandParseResult.Success(new(AdminCommandType.SetLevel, targetId, NumericValue: level))
            : AdminCommandParseResult.Failure("admin_level_invalid");

    private static AdminCommandParseResult ParseSingleValue(
        AdminCommandType type,
        long targetId,
        string value) =>
        value.Length > 0 && !value.Contains(' ')
            ? AdminCommandParseResult.Success(new(type, targetId, value.ToUpperInvariant()))
            : AdminCommandParseResult.Failure("admin_value_invalid");

    private static AdminCommandParseResult ParseDelete(long targetId, string value)
    {
        const string confirmation = " CONFIRM";
        if (!value.EndsWith(confirmation, StringComparison.Ordinal)
            || value.Length == confirmation.Length)
        {
            return AdminCommandParseResult.Failure("admin_delete_confirmation_required");
        }

        return AdminCommandParseResult.Success(
            new(AdminCommandType.Delete, targetId, value[..^confirmation.Length].TrimEnd()));
    }

    private static AdminCommandParseResult Success(AdminCommandType type, long targetId) =>
        AdminCommandParseResult.Success(new(type, targetId));

    private static bool TryTakeTarget(string arguments, out long targetId, out string remainder)
    {
        int separator = arguments.IndexOf(' ');
        string target = separator < 0 ? arguments : arguments[..separator];
        remainder = separator < 0 ? string.Empty : arguments[(separator + 1)..].Trim();
        return long.TryParse(target, out targetId) && targetId > 0;
    }
}
