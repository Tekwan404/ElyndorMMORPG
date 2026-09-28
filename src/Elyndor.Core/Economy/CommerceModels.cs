namespace Elyndor.Core.Economy;

public sealed record TradeOfferRequest(Guid RequestId, int Revision, Guid[] ItemIds, long Gold);
public sealed record TradeRevisionRequest(Guid RequestId, int Revision);
public sealed record AuctionCreateRequest(Guid RequestId, Guid ItemId, long Price);
public sealed record CommerceRequest(Guid RequestId);
public sealed record TradeResponse(Guid Id, string State, int Revision, Guid CharacterAId, Guid CharacterBId,
    Guid[] ItemsA, Guid[] ItemsB, string GoldA, string GoldB,
    bool LockedA, bool LockedB, bool ConfirmedA, bool ConfirmedB);
public sealed record TradeItemView(Guid Id, string Name, string? IconId, string Type, string Rarity, int Quantity);
public sealed record AuctionResponse(Guid Id, string State, Guid SellerId, Guid ItemId, string Price,
    string Fee, string Tax, Guid? BuyerId, DateTimeOffset ExpiresAt);
public sealed record AuctionListingView(Guid Id, Guid SellerId, string SellerName, Guid ItemId,
    string ItemDefinitionId, string Name, string? IconId, string Type, string Rarity, int Quantity,
    string Price, DateTimeOffset ExpiresAt);
public sealed record MailResponse(Guid Id, Guid ItemId, DateTimeOffset CreatedAt,
    string ItemDefinitionId, string Name, string? IconId, int Quantity, string Source);
public sealed record CommerceResult<T>(bool Succeeded, string? ErrorCode, T? Snapshot);
public static class CommerceResult
{
    public static CommerceResult<T> Success<T>(T snapshot) => new(true, null, snapshot);
    public static CommerceResult<T> Failure<T>(string code) => new(false, code, default);
}
