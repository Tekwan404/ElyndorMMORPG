using Elyndor.Core.Content;
using Elyndor.Core.Talents;

namespace Elyndor.Infrastructure.Content;

public static class TalentContentService
{
    private static readonly Dictionary<string, TalentTreeProfile> _talentTreesCache = new();
    private static bool _isLoaded = false;
    private static readonly object _lock = new();

    public static async Task InitializeAsync(
        string talentsDirectory,
        CancellationToken cancellationToken = default)
    {
        if (_isLoaded) return;

        lock (_lock)
        {
            if (_isLoaded) return;
        }

        var talentTrees = await GameContentPackageLoader.LoadTalentTreesAsync(
            talentsDirectory,
            cancellationToken);

        lock (_lock)
        {
            _talentTreesCache.Clear();
            foreach (var tree in talentTrees)
            {
                _talentTreesCache[tree.TalentTreeId] = tree;
            }
            _isLoaded = true;
        }
    }

    public static TalentTreeProfile? GetTalentTree(string talentTreeId)
    {
        return _talentTreesCache.TryGetValue(talentTreeId, out var tree) ? tree : null;
    }

    public static IReadOnlyList<TalentTreeProfile> GetAllTalentTrees()
    {
        return _talentTreesCache.Values.ToList().AsReadOnly();
    }

    public static void Reset()
    {
        lock (_lock)
        {
            _talentTreesCache.Clear();
            _isLoaded = false;
        }
    }
}
