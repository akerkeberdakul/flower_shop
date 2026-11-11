namespace FlowerShop;

public static class DeliveryPolicies
{
    public static Func<Order, decimal> FlatPlusPerKm(decimal baseFee, decimal perKm) =>
        order => order.DistanceKm == 0 ? 0m : baseFee + perKm * order.DistanceKm;

    public static Func<Order, decimal> Standard { get; } = FlatPlusPerKm(300m, 50m);

    public static Func<Order, decimal> Express { get; } = FlatPlusPerKm(600m, 100m);
}
