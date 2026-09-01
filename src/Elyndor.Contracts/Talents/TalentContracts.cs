namespace Elyndor.Contracts.Talents;

public sealed record TalentPrerequisiteResponse(string TalentId, int RequiredRank);
public sealed record TalentNodeResponse(
    string Id, string BranchId, int Tier, int RequiredSpentPoints, string Name,
    string EnglishName, int MaxRank, IReadOnlyList<TalentPrerequisiteResponse> Prerequisites,
    string Description, int? RequiredLevel);
public sealed record TalentBranchResponse(string Id, string Name, string Fantasy, int NodeCount);
public sealed record TalentLoadoutResponse(string Id, IReadOnlyDictionary<string, int> SelectedRanks, int SpentPoints);
public sealed record TalentSnapshotResponse(
    string TreeId, string ClassId, int Version, string ActiveLoadoutId, long StateVersion,
    int EarnedPoints, int AvailablePoints, IReadOnlyList<TalentBranchResponse> Branches,
    IReadOnlyList<TalentNodeResponse> Nodes, IReadOnlyList<TalentLoadoutResponse> Loadouts);
public sealed record LearnTalentRequest(string TalentId, string LoadoutId, long ExpectedStateVersion);
public sealed record SwitchTalentLoadoutRequest(string LoadoutId, long ExpectedStateVersion);
public sealed record ResetTalentLoadoutRequest(string LoadoutId, long ExpectedStateVersion);
