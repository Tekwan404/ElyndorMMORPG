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

[Fact]
public async Task ArcherStartsWithQuickShotAndCommandAttack()
{
GameContentPackage package = await GameContentPackageLoader.LoadAsync(
RepositoryContentPath());
ClassProfile archer = Assert.Single(
package.ClassProfiles!,
item => string.Equals(item.Id, "ARCHER", StringComparison.Ordinal));
Assert.Collection(
archer.StartingAbilityIds!,
abilityId => Assert.Equal("QUICK_SHOT", abilityId),
abilityId => Assert.Equal("COMMAND_ATTACK", abilityId));
Assert.Contains(
package.Abilities!,
ability => string.Equals(ability.Id, "QUICK_SHOT", StringComparison.Ordinal));
Assert.Contains(
package.Abilities!,
ability => string.Equals(ability.Id, "COMMAND_ATTACK", StringComparison.Ordinal));
}
[Fact]
public async Task BeastMasteryCommandTalentUpgradesBaselineCommandInsteadOfUnlockingIt()
{
GameContentPackage package = await GameContentPackageLoader.LoadAsync(
RepositoryContentPath());
TalentTreeDefinition tree = Assert.Single(
package.TalentTrees!,
item => string.Equals(item.Id, "ARCHER_TREE", StringComparison.Ordinal));
TalentDefinition commandTalent = Assert.Single(
tree.Nodes,
node => string.Equals(node.Id, "B-2-1", StringComparison.Ordinal));
Assert.DoesNotContain(
commandTalent.Modifiers ?? [],
modifier => string.Equals(modifier.Key, "UNLOCK_ABILITY", StringComparison.Ordinal));
Assert.Contains(
commandTalent.Modifiers ?? [],
modifier =>
string.Equals(modifier.Key, TalentModifierKeys.OnAbilityUsed, StringComparison.Ordinal)
&& string.Equals(modifier.TargetId, "COMMAND_ATTACK_DAMAGE", StringComparison.Ordinal));
Assert.True(TalentRuntimeAvailability.IsNodeFullySupported(commandTalent));
}
[Fact]
public async Task ArcherV2EventHookTargetIdsAreCanonical()
{
GameContentPackage package = await GameContentPackageLoader.LoadAsync(
RepositoryContentPath());
TalentTreeDefinition tree = Assert.Single(
package.TalentTrees!,
item => string.Equals(item.Id, "ARCHER_TREE", StringComparison.Ordinal));
string[] legacyTargetIds =
[
"AIMED_SHOT_AIM",
"PERFECT_SHOT",
"BOW_DAMAGE",
"JOINT_HUNT",
"MISS_REFUND",
"EXPOSED_DEFENSE",
"COMMANDING_VOICE",
"COORDINATION",
"IMPROVED_MEND",
"UNSTOPPABLE_PACK",
"TOXICOLOGY_CARRY",
"TRAP_CONTROL_DURATION",
"SURVIVAL_MASTER_PREP",
"TRAP_ENTRAPMENT",
"PRECISE_TEMPO",
"MASTER_ARROW_FOCUS",
"OWNER_CRIT_PET_NEXT",
"PET_CRIT_OWNER_NEXT",
"FRENZY",
"DETERRENCE_COUNTER",
"PET_FOCUS",
"COMBAT_RHYTHM",
"TRUESHOT_AURA_GROUP",
"BEST_MASTER_OWNER_SHOT"
];
Assert.DoesNotContain(
tree.Nodes.SelectMany(node => node.Modifiers ?? []),
modifier => legacyTargetIds.Contains(modifier.TargetId, StringComparer.Ordinal));
}

[Fact]
public async Task ArcherTalentReferenceMatchesEveryAuthoredDescription()
{
GameContentPackage package = await GameContentPackageLoader.LoadAsync(
RepositoryContentPath());
TalentTreeDefinition tree = Assert.Single(
package.TalentTrees!, item => item.Id == "ARCHER_TREE");
DirectoryInfo root = Directory.GetParent(Path.GetDirectoryName(RepositoryContentPath())!)!;
string documentation = File.ReadAllText(Path.Combine(
root.FullName, "docs", "source-of-truth", "gameplay", "23_ARCHER_TALENT_TREE.md"));
string[] rows = documentation.Split('\n')
.Where(line => line.StartsWith("| ", StringComparison.Ordinal)).ToArray();
foreach (TalentDefinition node in tree.Nodes)
{
Assert.Contains(rows, row =>
row.Split('|').Length > 7
&& row.Split('|')[2].Trim(' ', '`') == node.Id
&& row.Split('|')[6].Trim() == node.Description);
}
}

[Fact]
public async Task ArcherTrapDescriptionsReflectImmediateExecution()
{
GameContentPackage package = await GameContentPackageLoader.LoadAsync(
RepositoryContentPath());
foreach (string id in new[] { "FREEZING_TRAP", "IMMOLATION_TRAP", "EXPLOSIVE_TRAP" })
{
var ability = Assert.Single(package.Abilities!, candidate => candidate.Id == id);
Assert.Contains("Мгновенно", ability.Description, StringComparison.OrdinalIgnoreCase);
}
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
