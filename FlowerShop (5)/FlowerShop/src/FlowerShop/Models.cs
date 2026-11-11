using System.Collections.Immutable;

namespace FlowerShop;

public enum FlowerType { Rose, Tulip, Lily, Sunflower }

public sealed record Order(
    string Id,
    string Customer,
    FlowerType Flower,
    int Quantity,
    int DistanceKm,
    bool IsMember);

public sealed record ShopRules(
    int MinQuantity,
    int MaxDistanceKm,
    int BulkQuantity,
    decimal BulkDiscountRate,
    decimal MemberDiscountRate)
{
    public static ShopRules Default { get; } = new(
        MinQuantity: 5,
        MaxDistanceKm: 20,
        BulkQuantity: 20,
        BulkDiscountRate: 0.10m,
        MemberDiscountRate: 0.05m);
}

public sealed record RejectedOrder(string OrderId, string Reason);

public sealed record SelectionResult(
    ImmutableArray<Order> Accepted,
    ImmutableArray<RejectedOrder> Rejected);

public sealed record PricedOrder(
    Order Order,
    decimal FlowersSubtotal,
    decimal Discount,
    decimal DeliveryFee)
{
    public decimal Total => FlowersSubtotal - Discount + DeliveryFee;
}

public sealed record PricingResult(
    ImmutableArray<PricedOrder> Priced,
    ImmutableArray<RejectedOrder> Rejected);

public sealed record Quote(
    string OrderId,
    string Customer,
    FlowerType Flower,
    int Quantity,
    decimal FlowersSubtotal,
    decimal Discount,
    decimal DeliveryFee,
    decimal Total);

public sealed record QuoteSummary(
    ImmutableArray<Quote> Quotes,
    ImmutableArray<RejectedOrder> Rejected,
    decimal GrandTotal);

public sealed record ParsedBatch(
    ImmutableArray<Order> Orders,
    ImmutableArray<string> Errors);
