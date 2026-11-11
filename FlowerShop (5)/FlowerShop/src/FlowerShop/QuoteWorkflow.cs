using System.Collections.Immutable;

namespace FlowerShop;

public sealed record QuoteDisplay(string OrderId, decimal Total, string Text)
{
    public static QuoteDisplay From(Quote quote)
    {
        ArgumentNullException.ThrowIfNull(quote);

        return new QuoteDisplay(
            quote.OrderId,
            quote.Total,
            FormattableString.Invariant(
                $"{quote.OrderId}: {quote.Quantity} x {quote.Flower} for {quote.Customer} = {quote.Total:F2} (subtotal {quote.FlowersSubtotal:F2}, discount {quote.Discount:F2}, delivery {quote.DeliveryFee:F2})"));
    }
}

public static class QuoteWorkflow
{
    public static Result<QuoteDisplay, AppError> RequestQuote(
        Order request,
        Inventory inventory,
        ShopRules rules,
        Func<Order, decimal> deliveryPolicy)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(deliveryPolicy);

        return inventory.Find(request.Flower)
            .ToResult(AppError.Domain(
                "flower_not_found", $"Order {request.Id}: {request.Flower} is not stocked."))
            .Bind(item => CheckRequest(request, item, rules))
            .Bind(order => PriceWithPipeline(order, rules, deliveryPolicy))
            .Map(quote => QuoteDisplay.From(quote));
    }

    public static Result<Order, AppError> CheckRequest(Order request, StockItem item, ShopRules rules)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(rules);

        if (request.Quantity < rules.MinQuantity)
        {
            return Reject("quantity_below_minimum",
                $"Order {request.Id}: quantity {request.Quantity} is below the minimum of {rules.MinQuantity} stems.");
        }

        if (request.DistanceKm > rules.MaxDistanceKm)
        {
            return Reject("distance_too_far",
                $"Order {request.Id}: distance {request.DistanceKm} km exceeds the delivery limit of {rules.MaxDistanceKm} km.");
        }

        if (item.Stock < request.Quantity)
        {
            return Reject("insufficient_stock",
                $"Order {request.Id}: only {item.Stock} {item.Flower} in stock, {request.Quantity} requested.");
        }

        return Result<Order, AppError>.Ok(request);
    }

    public static Result<Quote, AppError> PriceWithPipeline(Order order, ShopRules rules, Func<Order, decimal> deliveryPolicy)
    {
        var summary = OrderPipeline.Run(ImmutableArray.Create(order), rules, deliveryPolicy);

        if (summary.Quotes.IsEmpty)
        {
            var reason = summary.Rejected.IsEmpty ? "the order was not priced." : summary.Rejected[0].Reason;
            return Result<Quote, AppError>.Fail(AppError.Domain("order_rejected", $"Order {order.Id}: {reason}"));
        }

        return Result<Quote, AppError>.Ok(summary.Quotes[0]);
    }

    private static Result<Order, AppError> Reject(string code, string message) =>
        Result<Order, AppError>.Fail(AppError.Domain(code, message));
}
