using Elyndor.Contracts.Arena;
using Elyndor.Contracts.Combat;
using Elyndor.Core.Content;
using Elyndor.Core.Pvp;
using Elyndor.Infrastructure.Pvp;
using Elyndor.Server.Combat;

namespace Elyndor.Server.Pvp;

internal static class ArenaContractMapper
{
    public static ArenaMatchResponse ToResponse(ArenaTestState state, GameContentPackage content) => new(
        state.Status.ToString(),
        state.MatchId,
        state.CharacterId,
        state.OpponentCharacterId,
        state.OpponentName,
        state.Outcome.ToString(),
        state.Battle?.Status.ToString(),
        state.Sequence,
        state.Battle is null ? null : CombatContractMapper.ToResponse(state.Battle, content),
        state.Events.Select(e => CombatContractMapper.ToResponse(e)).ToArray());

    public static ArenaCommandResponse ToResponse(ArenaTestCommandResult result, GameContentPackage content) => new(
        result.Succeeded,
        result.ErrorCode,
        result.State is null ? null : ToResponse(result.State, content));

    public static ArenaStatusResponse ToResponse(ArenaLobbyStatus status, bool enabled) => new(
        enabled,
        status.Standing.Honor,
        status.Standing.Rating,
        status.Standing.Wins,
        status.Standing.Losses,
        status.Standing.Draws,
        status.Queue.IsQueued,
        status.Queue.QueueMode?.ToString(),
        status.Queue.JoinedAtUtc,
        status.RuntimeMatchId ?? status.Queue.ActiveMatchId);

    public static ArenaStatusResponse Disabled() => new(false, 0, 0, 0, 0, 0, false, null, null, null);
}
