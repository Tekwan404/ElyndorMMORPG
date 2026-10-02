namespace Elyndor.Core.WorldBosses;

public enum WorldBossSpawnStatus
{
    Scheduled,
    Active,
    Defeated,
    Expired,
    Settling,
    Settled
}

public enum WorldBossRewardTier
{
    Qualified,
    Bronze,
    Silver,
    Gold,
    Epic,
    Legendary
}

public sealed class WorldBossSpawn
{
    private WorldBossSpawn()
    {
        BossDefinitionId = null!;
        ContentVersion = null!;
        BalanceVersion = null!;
    }

    public WorldBossSpawn(
        Guid id,
        string bossDefinitionId,
        decimal maxHealth,
        int initialPhase,
        DateTimeOffset spawnedAtUtc,
        DateTimeOffset expiresAtUtc,
        string contentVersion,
        string balanceVersion)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("World boss spawn identifier cannot be empty.", nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(bossDefinitionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(balanceVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxHealth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialPhase);
        EnsureUtc(spawnedAtUtc, nameof(spawnedAtUtc));
        EnsureUtc(expiresAtUtc, nameof(expiresAtUtc));
        if (expiresAtUtc <= spawnedAtUtc)
            throw new ArgumentException("World boss expiry must be after spawn time.", nameof(expiresAtUtc));

        Id = id;
        BossDefinitionId = bossDefinitionId;
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
        CurrentPhase = initialPhase;
        SpawnedAtUtc = spawnedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        ContentVersion = contentVersion;
        BalanceVersion = balanceVersion;
        Status = WorldBossSpawnStatus.Active;
        Version = 1;
    }

    public Guid Id { get; private set; }
    public string BossDefinitionId { get; private set; }
    public decimal MaxHealth { get; private set; }
    public decimal CurrentHealth { get; private set; }
    public int CurrentPhase { get; private set; }
    public DateTimeOffset SpawnedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? DefeatedAtUtc { get; private set; }
    public DateTimeOffset? SettledAtUtc { get; private set; }
    public WorldBossSpawnStatus Status { get; private set; }
    public string ContentVersion { get; private set; }
    public string BalanceVersion { get; private set; }
    public long Version { get; private set; }

    public decimal ApplyDamage(decimal requestedDamage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(requestedDamage);
        if (requestedDamage == 0 || Status != WorldBossSpawnStatus.Active || CurrentHealth <= 0)
            return 0;

        decimal appliedDamage = Math.Min(CurrentHealth, requestedDamage);
        CurrentHealth -= appliedDamage;
        Version++;
        return appliedDamage;
    }

    public bool TryChangePhase(int phase)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(phase);
        if (Status != WorldBossSpawnStatus.Active || phase <= CurrentPhase)
            return false;

        CurrentPhase = phase;
        Version++;
        return true;
    }

    public bool TryMarkDefeated(DateTimeOffset defeatedAtUtc)
    {
        EnsureUtc(defeatedAtUtc, nameof(defeatedAtUtc));
        if (Status != WorldBossSpawnStatus.Active || CurrentHealth > 0)
            return false;
        ArgumentOutOfRangeException.ThrowIfLessThan(defeatedAtUtc, SpawnedAtUtc);

        Status = WorldBossSpawnStatus.Defeated;
        DefeatedAtUtc = defeatedAtUtc;
        Version++;
        return true;
    }

    public bool TryExpire(DateTimeOffset nowUtc)
    {
        EnsureUtc(nowUtc, nameof(nowUtc));
        if (Status != WorldBossSpawnStatus.Active || nowUtc < ExpiresAtUtc)
            return false;

        Status = WorldBossSpawnStatus.Expired;
        Version++;
        return true;
    }

    public bool TryBeginSettlement()
    {
        if (Status is not (WorldBossSpawnStatus.Defeated or WorldBossSpawnStatus.Expired))
            return false;

        Status = WorldBossSpawnStatus.Settling;
        Version++;
        return true;
    }

    public bool TryMarkSettled(DateTimeOffset settledAtUtc)
    {
        EnsureUtc(settledAtUtc, nameof(settledAtUtc));
        if (Status != WorldBossSpawnStatus.Settling)
            return false;
        ArgumentOutOfRangeException.ThrowIfLessThan(settledAtUtc, SpawnedAtUtc);

        Status = WorldBossSpawnStatus.Settled;
        SettledAtUtc = settledAtUtc;
        Version++;
        return true;
    }

    private static void EnsureUtc(DateTimeOffset timestamp, string parameterName)
    {
        if (timestamp.Offset != TimeSpan.Zero)
            throw new ArgumentException("World boss timestamps must be UTC.", parameterName);
    }
}

public sealed class WorldBossContribution
{
    private WorldBossContribution() { }

    public WorldBossContribution(Guid spawnId, Guid characterId, DateTimeOffset firstActivityAtUtc)
    {
        if (spawnId == Guid.Empty || characterId == Guid.Empty)
            throw new ArgumentException("World boss contribution identifiers cannot be empty.");
        EnsureUtc(firstActivityAtUtc, nameof(firstActivityAtUtc));

        SpawnId = spawnId;
        CharacterId = characterId;
        FirstActivityAtUtc = firstActivityAtUtc;
        LastActivityAtUtc = firstActivityAtUtc;
    }

    public Guid SpawnId { get; private set; }
    public Guid CharacterId { get; private set; }
    public decimal Damage { get; private set; }
    public DateTimeOffset FirstActivityAtUtc { get; private set; }
    public DateTimeOffset LastActivityAtUtc { get; private set; }

    public void AddDamage(decimal appliedDamage, DateTimeOffset activityAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(appliedDamage);
        EnsureUtc(activityAtUtc, nameof(activityAtUtc));
        ArgumentOutOfRangeException.ThrowIfLessThan(activityAtUtc, LastActivityAtUtc);

        Damage = checked(Damage + appliedDamage);
        LastActivityAtUtc = activityAtUtc;
    }

    private static void EnsureUtc(DateTimeOffset timestamp, string parameterName)
    {
        if (timestamp.Offset != TimeSpan.Zero)
            throw new ArgumentException("World boss contribution timestamps must be UTC.", parameterName);
    }
}

public sealed class WorldBossPartyContribution
{
    private WorldBossPartyContribution() { }

    public WorldBossPartyContribution(Guid spawnId, Guid partyId)
    {
        if (spawnId == Guid.Empty || partyId == Guid.Empty)
            throw new ArgumentException("World boss party contribution identifiers cannot be empty.");

        SpawnId = spawnId;
        PartyId = partyId;
    }

    public Guid SpawnId { get; private set; }
    public Guid PartyId { get; private set; }
    public decimal Damage { get; private set; }

    public void AddDamage(decimal appliedDamage)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(appliedDamage);

        Damage = checked(Damage + appliedDamage);
    }
}

public sealed class WorldBossDamageMutation
{
    private WorldBossDamageMutation() { }

    public WorldBossDamageMutation(
        Guid spawnId,
        Guid mutationId,
        Guid characterId,
        Guid combatSessionId,
        Guid? partyId,
        decimal requestedDamage,
        decimal appliedDamage,
        DateTimeOffset committedAtUtc)
    {
        if (spawnId == Guid.Empty || mutationId == Guid.Empty || characterId == Guid.Empty || combatSessionId == Guid.Empty)
            throw new ArgumentException("World boss damage mutation identifiers cannot be empty.");
        if (requestedDamage < 0 || appliedDamage < 0 || appliedDamage > requestedDamage)
            throw new ArgumentException("World boss applied damage must be between zero and requested damage.");
        if (committedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("World boss mutation timestamps must be UTC.", nameof(committedAtUtc));

        SpawnId = spawnId;
        MutationId = mutationId;
        CharacterId = characterId;
        CombatSessionId = combatSessionId;
        PartyId = partyId;
        RequestedDamage = requestedDamage;
        AppliedDamage = appliedDamage;
        CommittedAtUtc = committedAtUtc;
    }

    public Guid SpawnId { get; private set; }
    public Guid MutationId { get; private set; }
    public Guid CharacterId { get; private set; }
    public Guid CombatSessionId { get; private set; }
    public Guid? PartyId { get; private set; }
    public decimal RequestedDamage { get; private set; }
    public decimal AppliedDamage { get; private set; }
    public DateTimeOffset CommittedAtUtc { get; private set; }
}

public sealed class WorldBossRewardSettlement
{
    private WorldBossRewardSettlement()
    {
        LootResultJson = "[]";
    }

    public WorldBossRewardSettlement(
        Guid spawnId,
        Guid characterId,
        decimal contributionScore,
        WorldBossRewardTier rewardTier,
        int gold,
        int experience,
        int tokens,
        Guid lootRollSeed,
        string lootResultJson,
        DateTimeOffset settledAtUtc)
    {
        if (spawnId == Guid.Empty || characterId == Guid.Empty || lootRollSeed == Guid.Empty)
            throw new ArgumentException("World boss settlement identifiers cannot be empty.");
        ArgumentOutOfRangeException.ThrowIfNegative(contributionScore);
        ArgumentOutOfRangeException.ThrowIfNegative(gold);
        ArgumentOutOfRangeException.ThrowIfNegative(experience);
        ArgumentOutOfRangeException.ThrowIfNegative(tokens);
        ArgumentException.ThrowIfNullOrWhiteSpace(lootResultJson);
        if (settledAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("World boss settlement timestamps must be UTC.", nameof(settledAtUtc));

        SpawnId = spawnId;
        CharacterId = characterId;
        ContributionScore = contributionScore;
        RewardTier = rewardTier;
        Gold = gold;
        Experience = experience;
        Tokens = tokens;
        LootRollSeed = lootRollSeed;
        LootResultJson = lootResultJson;
        SettledAtUtc = settledAtUtc;
    }

    public Guid SpawnId { get; private set; }
    public Guid CharacterId { get; private set; }
    public decimal ContributionScore { get; private set; }
    public WorldBossRewardTier RewardTier { get; private set; }
    public int Gold { get; private set; }
    public int Experience { get; private set; }
    public int Tokens { get; private set; }
    public Guid LootRollSeed { get; private set; }
    public string LootResultJson { get; private set; }
    public DateTimeOffset SettledAtUtc { get; private set; }
}

public sealed class WorldBossCombatSessionBinding
{
    private WorldBossCombatSessionBinding() { }

    public WorldBossCombatSessionBinding(
        Guid combatSessionId,
        Guid spawnId,
        Guid bossActorId,
        Guid? partyId,
        DateTimeOffset boundAtUtc)
    {
        if (combatSessionId == Guid.Empty || spawnId == Guid.Empty || bossActorId == Guid.Empty)
            throw new ArgumentException("World boss combat binding identifiers cannot be empty.");
        if (partyId == Guid.Empty)
            throw new ArgumentException("World boss party identifier cannot be empty.", nameof(partyId));
        if (boundAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("World boss binding timestamps must be UTC.", nameof(boundAtUtc));

        CombatSessionId = combatSessionId;
        SpawnId = spawnId;
        BossActorId = bossActorId;
        PartyId = partyId;
        BoundAtUtc = boundAtUtc;
    }

    public Guid CombatSessionId { get; private set; }
    public Guid SpawnId { get; private set; }
    public Guid BossActorId { get; private set; }
    public Guid? PartyId { get; private set; }
    public DateTimeOffset BoundAtUtc { get; private set; }
}
