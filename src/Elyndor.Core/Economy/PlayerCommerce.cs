namespace Elyndor.Core.Economy;

public sealed class CommerceRuleException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed class PlayerTrade
{
    private PlayerTrade() { }
    public PlayerTrade(Guid id, Guid a, Guid b, DateTimeOffset now)
    {
        if (id == Guid.Empty || a == Guid.Empty || b == Guid.Empty || a == b)
            throw new CommerceRuleException("trade_invalid_participants");
        Id = id; CharacterAId = a; CharacterBId = b; ExpiresAt = now.AddMinutes(5);
    }
    public Guid Id { get; private set; }
    public Guid CharacterAId { get; private set; }
    public Guid CharacterBId { get; private set; }
    public string State { get; private set; } = "OPEN";
    public int Revision { get; private set; }
    public Guid[] ItemsA { get; private set; } = [];
    public Guid[] ItemsB { get; private set; } = [];
    public long GoldA { get; private set; }
    public long GoldB { get; private set; }
    public bool LockedA { get; private set; }
    public bool LockedB { get; private set; }
    public bool ConfirmedA { get; private set; }
    public bool ConfirmedB { get; private set; }
    public string? ConnectionA { get; private set; }
    public string? ConnectionB { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public bool Ready => State == "OPEN" && LockedA && LockedB && ConfirmedA && ConfirmedB
        && ConnectionA is not null && ConnectionB is not null;

    public bool IsA(Guid character)
    {
        if (character != CharacterAId && character != CharacterBId)
            throw new CommerceRuleException("trade_not_participant");
        return character == CharacterAId;
    }
    public void Connect(Guid character, string connection)
    {
        Open();
        if (string.IsNullOrWhiteSpace(connection)) throw new CommerceRuleException("trade_disconnected");
        if (IsA(character)) ConnectionA = connection; else ConnectionB = connection;
        ClearConfirmations();
    }
    public void RequireConnection(Guid character, string connection)
    {
        if ((IsA(character) ? ConnectionA : ConnectionB) != connection)
            throw new CommerceRuleException("trade_disconnected");
    }
    public void ChangeOffer(Guid character, Guid[] items, long gold)
    {
        Open();
        if (gold < 0 || items.Length > 30 || items.Any(x => x == Guid.Empty) || items.Distinct().Count() != items.Length)
            throw new CommerceRuleException("trade_invalid_offer");
        if (IsA(character)) { ItemsA = items.ToArray(); GoldA = gold; }
        else { ItemsB = items.ToArray(); GoldB = gold; }
        Revision = checked(Revision + 1);
        ClearConfirmations();
    }
    public void Lock(Guid character, int revision)
    {
        CheckRevision(revision);
        if (IsA(character)) LockedA = true; else LockedB = true;
    }
    public void Confirm(Guid character, int revision)
    {
        CheckRevision(revision);
        if (!LockedA || !LockedB || ConnectionA is null || ConnectionB is null)
            throw new CommerceRuleException("trade_not_locked");
        if (IsA(character)) ConfirmedA = true; else ConfirmedB = true;
    }
    public void Complete()
    {
        if (!Ready) throw new CommerceRuleException("trade_not_confirmed");
        State = "COMPLETED";
    }
    public void Cancel() { if (State == "OPEN") { State = "CANCELLED"; ClearConfirmations(); } }
    public void Disconnect(string connection)
    {
        if (ConnectionA == connection || ConnectionB == connection) Cancel();
    }
    private void ClearConfirmations() { LockedA = LockedB = ConfirmedA = ConfirmedB = false; }
    private void Open() { if (State != "OPEN") throw new CommerceRuleException("trade_closed"); }
    private void CheckRevision(int revision)
    {
        Open();
        if (revision != Revision) throw new CommerceRuleException("trade_stale_revision");
    }
}

public sealed class AuctionListing
{
    private AuctionListing() { }
    public AuctionListing(Guid id, Guid seller, Guid item, long price, long fee, long tax, DateTimeOffset now)
    {
        if (id == Guid.Empty || seller == Guid.Empty || item == Guid.Empty || price <= 0 || fee < 0 || tax < 0 || tax > price)
            throw new CommerceRuleException("auction_invalid_listing");
        Id = id; SellerId = seller; ItemId = item; Price = price; Fee = fee; Tax = tax;
        CreatedAt = now; ExpiresAt = now.AddHours(48);
    }
    public Guid Id { get; private set; }
    public Guid SellerId { get; private set; }
    public Guid ItemId { get; private set; }
    public long Price { get; private set; }
    public long Fee { get; private set; }
    public long Tax { get; private set; }
    public string State { get; private set; } = "ACTIVE";
    public Guid? BuyerId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? SettledAt { get; private set; }
    public void Sell(Guid buyer, DateTimeOffset now)
    {
        if (State != "ACTIVE" || now >= ExpiresAt) throw new CommerceRuleException("auction_unavailable");
        if (buyer == SellerId) throw new CommerceRuleException("auction_self_purchase");
        State = "SOLD"; BuyerId = buyer; SettledAt = now;
    }
    public void Return(bool expired)
    {
        if (State != "ACTIVE") throw new CommerceRuleException("auction_unavailable");
        State = expired ? "EXPIRED" : "CANCELLED";
    }
}

public sealed class CommerceMail
{
    private CommerceMail() { }
    public CommerceMail(Guid id, Guid characterId, Guid itemId, DateTimeOffset now)
    { Id = id; CharacterId = characterId; ItemId = itemId; CreatedAt = now; }
    public Guid Id { get; private set; }
    public Guid CharacterId { get; private set; }
    public Guid ItemId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ClaimedAt { get; private set; }
    public void Claim(DateTimeOffset now) => ClaimedAt ??= now;
}
