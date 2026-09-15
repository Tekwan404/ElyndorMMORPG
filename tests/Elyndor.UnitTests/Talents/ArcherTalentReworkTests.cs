using Elyndor.Core.Content;
using Elyndor.Core.Talents;
using Elyndor.Infrastructure.Content;
namespace Elyndor.UnitTests.Talents;
public sealed class ArcherTalentReworkTests
{
[Fact]
public async Task ArcherTreeV2ContainsThreeSupportedThirtyTwoNodeBranches()
{
GameContentPackage package = await GameContentPackageLoader.LoadAsync(
RepositoryContentPath());
TalentTreeDefinition tree = Assert.Single(
package.TalentTrees!,
item => string.Equals(item.Id, "ARCHER_TREE", StringComparison.Ordinal));
Assert.Equal(2, tree.Version);
Assert.Equal(59, tree.MaxSpendablePoints);
Assert.Equal(96, tree.Nodes.Count);
Assert.Equal(
32,
tree.Nodes.Count(node =>
string.Equals(node.BranchId, "MARKSMAN", StringComparison.Ordinal)));
Assert.Equal(
32,
tree.Nodes.Count(node =>
string.Equals(node.BranchId, "BEAST_MASTERY", StringComparison.Ordinal)));
Assert.Equal(
32,
tree.Nodes.Count(node =>
string.Equals(node.BranchId, "SURVIVAL", StringComparison.Ordinal)));
Assert.DoesNotContain(
tree.Nodes,
node => string.Equals(node.BranchId, "ARCANE_ARCHER", StringComparison.Ordinal));
Assert.DoesNotContain(
tree.Nodes,
node => node.Id.StartsWith("A-", StringComparison.Ordinal));
Assert.All(tree.Nodes, node =>
Assert.True(
TalentRuntimeAvailability.IsNodeFullySupported(node),
$"Talent {node.Id} ({node.Name}) is not fully runtime-supported."));
}
[Fact]
public async Task ArcherV2HasNoMageResourceOrSpellPowerModifiers()
{
GameContentPackage package = await GameContentPackageLoader.LoadAsync(
RepositoryContentPath());
TalentTreeDefinition tree = Assert.Single(
package.TalentTrees!,
item => string.Equals(item.Id, "ARCHER_TREE", StringComparison.Ordinal));
string[] forbiddenKeys =
[
TalentModifierKeys.IntellectPercent,
TalentModifierKeys.SpellPowerPercent,
TalentModifierKeys.MagicPenetrationPercent,
TalentModifierKeys.ResourceProfileOverride
];
Assert.DoesNotContain(
tree.Nodes.SelectMany(node => node.Modifiers ?? []),
modifier => forbiddenKeys.Contains(modifier.Key, StringComparer.Ordinal));
}
private static string RepositoryContentPath()
{
DirectoryInfo? directory = new(AppContext.BaseDirectory);
while (directory is not null)
{
string candidate = Path.Combine(directory.FullName, "content", "package.json");
if (File.Exists(candidate))
return candidate;
directory = directory.Parent;
}
throw new DirectoryNotFoundException("Repository content package was not found.");
}
}
