using System.Collections.Immutable;

namespace FlowerShop;

public static class FunctionalExtensions
{
    public static TOut Then<TIn, TOut>(this TIn value, Func<TIn, TOut> next) => next(value);
}

public static class OrderPipeline
{
    public static SelectionResult SelectEligible(ImmutableArray<Order> orders, ShopRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (orders.IsDefault)
        {
            throw new ArgumentException("Orders were not provided.", nameof(orders));
        }

        var accepted = ImmutableArray.CreateBuilder<Order>();
        var rejected = ImmutableArray.CreateBuilder<RejectedOrder>();

        foreach (var order in orders)
        {
            if (order.Quantity < rules.MinQuantity)
            {
                rejected.Add(new RejectedOrder(
                    order.Id, $"Quantity {order.Quantity} is below the minimum of {rules.MinQuantity} stems."));
            }
            else if (order.DistanceKm > rules.MaxDistanceKm)
            {
                rejected.Add(new RejectedOrder(
                    order.Id, $"Distance {order.DistanceKm} km exceeds the delivery limit of {rules.MaxDistanceKm} km."));
            }
            else
            {
                accepted.Add(order);
            }
        }

        return new SelectionResult(accepted.ToImmutable(), rejected.ToImmutable());
    }

    public static PricingResult PriceOrders(
        SelectionResult selection,
        ShopRules rules,
        Func<Order, decimal> deliveryPolicy)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(deliveryPolicy);

        var priced = ImmutableArray.CreateBuilder<PricedOrder>();

        foreach (var order in selection.Accepted)
        {
            var subtotal = order.Quantity * FlowerCatalog.UnitPrice(order.Flower);
            var discount = Math.Round(subtotal * DiscountRate(order, rules), 2, MidpointRounding.AwayFromZero);
            var deliveryFee = deliveryPolicy(order);

            if (deliveryFee < 0)
            {
                throw new InvalidOperationException(
                    $"The delivery policy returned a negative fee for order {order.Id}.");
            }

            priced.Add(new PricedOrder(order, subtotal, discount, deliveryFee));
        }

        return new PricingResult(priced.ToImmutable(), selection.Rejected);
    }

    public static decimal DiscountRate(Order order, ShopRules rules) =>
        (order.Quantity >= rules.BulkQuantity ? rules.BulkDiscountRate : 0m)
        + (order.IsMember ? rules.MemberDiscountRate : 0m);

    public static QuoteSummary BuildSummary(PricingResult pricing)
    {
        ArgumentNullException.ThrowIfNull(pricing);

        var quotes = pricing.Priced
            .Select(p => new Quote(
                p.Order.Id, p.Order.Customer, p.Order.Flower, p.Order.Quantity,
                p.FlowersSubtotal, p.Discount, p.DeliveryFee, p.Total))
            .OrderByDescending(q => q.Total)
            .ThenBy(q => q.OrderId, StringComparer.Ordinal)
            .ToImmutableArray();

        return new QuoteSummary(quotes, pricing.Rejected, quotes.Sum(q => q.Total));
    }

    public static QuoteSummary Run(
        ImmutableArray<Order> orders,
        ShopRules rules,
        Func<Order, decimal> deliveryPolicy) =>
        orders
            .Then(o => SelectEligible(o, rules))
            .Then(selection => PriceOrders(selection, rules, deliveryPolicy))
            .Then(pricing => BuildSummary(pricing));
}
