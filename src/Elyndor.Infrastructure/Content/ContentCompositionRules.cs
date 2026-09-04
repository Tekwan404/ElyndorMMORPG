namespace Elyndor.Infrastructure.Content;

internal static class ContentCompositionRules
{
    internal static string HigherVersion(string current, string candidate)
    {
        if (Version.TryParse(current, out Version? currentVersion)
            && Version.TryParse(candidate, out Version? candidateVersion))
        {
            return candidateVersion > currentVersion ? candidate : current;
        }

        return string.CompareOrdinal(candidate, current) > 0 ? candidate : current;
    }

    internal static DateTimeOffset Later(DateTimeOffset current, DateTimeOffset candidate) =>
        candidate > current ? candidate : current;

    internal static IReadOnlyList<T> MergeByKey<T, TKey>(
        IReadOnlyList<T>? current,
        IReadOnlyList<T> incoming,
        Func<T, TKey> keySelector)
        where TKey : notnull
    {
        List<T> result = (current ?? []).ToList();
        Dictionary<TKey, int> positions = result
            .Select((item, index) => (Key: keySelector(item), Index: index))
            .ToDictionary(item => item.Key, item => item.Index);

        foreach (T item in incoming)
        {
            TKey key = keySelector(item);
            if (positions.TryGetValue(key, out int index))
                result[index] = item;
            else
            {
                positions.Add(key, result.Count);
                result.Add(item);
            }
        }

        return result.ToArray();
    }

    internal static IReadOnlyList<T>? MergeOptionalByKey<T, TKey>(
        IReadOnlyList<T>? current,
        IReadOnlyList<T>? incoming,
        Func<T, TKey> keySelector)
        where TKey : notnull =>
        incoming is null ? current : MergeByKey(current, incoming, keySelector);
}
