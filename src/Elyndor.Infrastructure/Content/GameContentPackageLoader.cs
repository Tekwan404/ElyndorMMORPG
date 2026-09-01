using System.Text.Json;
using System.Text.Json.Serialization;
using Elyndor.Core.Content;
using Elyndor.Core.Talents;

namespace Elyndor.Infrastructure.Content;

public static class GameContentPackageLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static async Task<GameContentPackage> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            await using FileStream stream = File.OpenRead(path);
            GameContentPackage? package = await JsonSerializer.DeserializeAsync<GameContentPackage>(
                stream,
                SerializerOptions,
                cancellationToken);

            if (package is null)
            {
                throw new InvalidDataException($"Game content package '{path}' is empty.");
            }

            IReadOnlyList<ContentValidationError> errors =
                GameContentPackageValidator.Validate(package);

            if (errors.Count > 0)
            {
                throw new ContentPackageValidationException(errors);
            }

            return package;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Game content package '{path}' does not match the required JSON shape.",
                exception);
        }
    }

    public static async Task<IReadOnlyList<TalentTreeProfile>> LoadTalentTreesAsync(
        string talentsDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(talentsDirectory);

        var talentTrees = new List<TalentTreeProfile>();

        if (!Directory.Exists(talentsDirectory))
        {
            return talentTrees.AsReadOnly();
        }

        var jsonFiles = Directory.GetFiles(talentsDirectory, "*.json", SearchOption.AllDirectories);

        foreach (var file in jsonFiles)
        {
            try
            {
                await using FileStream stream = File.OpenRead(file);
                var document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip
                }, cancellationToken);

                var root = document.RootElement;

                if (root.TryGetProperty("talentTrees", out var treesElement) && treesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var treeElement in treesElement.EnumerateArray())
                    {
                        var tree = DeserializeTalentTree(treeElement);
                        if (tree != null)
                        {
                            talentTrees.Add(tree);
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(
                    $"Talent tree file '{file}' contains invalid JSON.", ex);
            }
        }

        return talentTrees.AsReadOnly();
    }

    private static TalentTreeProfile? DeserializeTalentTree(JsonElement element)
    {
        if (!element.TryGetProperty("talentTreeId", out var idElem) || idElem.ValueKind != JsonValueKind.String)
            return null;
        if (!element.TryGetProperty("classId", out var classIdElem) || classIdElem.ValueKind != JsonValueKind.String)
            return null;
        if (!element.TryGetProperty("maxSpendablePoints", out var maxPointsElem) || maxPointsElem.ValueKind != JsonValueKind.Number)
            return null;
        if (!element.TryGetProperty("version", out var versionElem) || versionElem.ValueKind != JsonValueKind.String)
            return null;
        if (!element.TryGetProperty("branches", out var branchesElem) || branchesElem.ValueKind != JsonValueKind.Array)
            return null;

        var branches = new List<TalentBranchProfile>();
        foreach (var branchElem in branchesElem.EnumerateArray())
        {
            var branch = DeserializeTalentBranch(branchElem);
            if (branch != null)
            {
                branches.Add(branch);
            }
        }

        return new TalentTreeProfile(
            idElem.GetString()!,
            classIdElem.GetString()!,
            maxPointsElem.GetInt32(),
            versionElem.GetString()!,
            branches.AsReadOnly());
    }

    private static TalentBranchProfile? DeserializeTalentBranch(JsonElement element)
    {
        if (!element.TryGetProperty("branchId", out var idElem) || idElem.ValueKind != JsonValueKind.String)
            return null;
        if (!element.TryGetProperty("name", out var nameElem) || nameElem.ValueKind != JsonValueKind.String)
            return null;
        if (!element.TryGetProperty("description", out var descElem) || descElem.ValueKind != JsonValueKind.String)
            return null;
        if (!element.TryGetProperty("nodes", out var nodesElem) || nodesElem.ValueKind != JsonValueKind.Array)
            return null;

        var nodes = new List<TalentNodeProfile>();
        foreach (var nodeElem in nodesElem.EnumerateArray())
        {
            var node = DeserializeTalentNode(nodeElem);
            if (node != null)
            {
                nodes.Add(node);
            }
        }

        return new TalentBranchProfile(
            idElem.GetString()!,
            nameElem.GetString()!,
            descElem.GetString()!,
            nodes.AsReadOnly());
    }

    private static TalentNodeProfile? DeserializeTalentNode(JsonElement element)
    {
        if (!element.TryGetProperty("talentId", out var idElem) || idElem.ValueKind != JsonValueKind.String)
            return null;
        if (!element.TryGetProperty("branchId", out var branchIdElem) || branchIdElem.ValueKind != JsonValueKind.String)
            return null;
        if (!element.TryGetProperty("tier", out var tierElem) || tierElem.ValueKind != JsonValueKind.Number)
            return null;
        if (!element.TryGetProperty("name", out var nameElem) || nameElem.ValueKind != JsonValueKind.String)
            return null;
        if (!element.TryGetProperty("description", out var descElem) || descElem.ValueKind != JsonValueKind.String)
            return null;
        if (!element.TryGetProperty("maxRank", out var maxRankElem) || maxRankElem.ValueKind != JsonValueKind.Number)
            return null;
        if (!element.TryGetProperty("effectType", out var effectTypeElem) || effectTypeElem.ValueKind != JsonValueKind.String)
            return null;
        if (!element.TryGetProperty("requiredSpentPointsInBranch", out var reqPointsElem) || reqPointsElem.ValueKind != JsonValueKind.Number)
            return null;
        if (!element.TryGetProperty("canTriggerFromProc", out var canProcElem) || canProcElem.ValueKind != JsonValueKind.True && canProcElem.ValueKind != JsonValueKind.False)
            return null;

        IReadOnlyList<string>? prerequisites = null;
        if (element.TryGetProperty("prerequisites", out var prereqElem) && prereqElem.ValueKind == JsonValueKind.Array)
        {
            var prereqs = new List<string>();
            foreach (var prereq in prereqElem.EnumerateArray())
            {
                if (prereq.ValueKind == JsonValueKind.String)
                {
                    prereqs.Add(prereq.GetString()!);
                }
            }
            prerequisites = prereqs.AsReadOnly();
        }

        decimal? statValue = null;
        if (element.TryGetProperty("statValue", out var statValueElem) && statValueElem.ValueKind == JsonValueKind.Number)
        {
            statValue = statValueElem.GetDecimal();
        }

        string? statType = null;
        if (element.TryGetProperty("statType", out var statTypeElem) && statTypeElem.ValueKind == JsonValueKind.String)
        {
            statType = statTypeElem.GetString();
        }

        string? abilityId = null;
        if (element.TryGetProperty("abilityId", out var abilityIdElem) && abilityIdElem.ValueKind == JsonValueKind.String)
        {
            abilityId = abilityIdElem.GetString();
        }

        int? cooldownSeconds = null;
        if (element.TryGetProperty("cooldownSeconds", out var cdElem) && cdElem.ValueKind == JsonValueKind.Number)
        {
            cooldownSeconds = cdElem.GetInt32();
        }

        int? internalCooldownSeconds = null;
        if (element.TryGetProperty("internalCooldownSeconds", out var icdElem) && icdElem.ValueKind == JsonValueKind.Number)
        {
            internalCooldownSeconds = icdElem.GetInt32();
        }

        string? triggerEvent = null;
        if (element.TryGetProperty("triggerEvent", out var triggerElem) && triggerElem.ValueKind == JsonValueKind.String)
        {
            triggerEvent = triggerElem.GetString();
        }

        return new TalentNodeProfile(
            idElem.GetString()!,
            branchIdElem.GetString()!,
            tierElem.GetInt32(),
            nameElem.GetString()!,
            descElem.GetString()!,
            maxRankElem.GetInt32(),
            effectTypeElem.GetString()!,
            prerequisites,
            reqPointsElem.GetInt32(),
            statValue,
            statType,
            abilityId,
            cooldownSeconds,
            internalCooldownSeconds,
            canProcElem.GetBoolean(),
            triggerEvent);
    }
}
