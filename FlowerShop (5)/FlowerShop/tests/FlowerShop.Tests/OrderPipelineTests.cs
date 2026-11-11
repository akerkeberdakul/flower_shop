using System.Collections.Immutable;
using FlowerShop;

namespace FlowerShop.Tests;

public class OrderPipelineTests
{
    private static readonly string[] SampleLines =
    {
        "O1,Aigerim,Rose,10,5,false",
        "O2,Dana,Tulip,20,0,true",
        "O3,Marat,Lily,5,20,false",
        "O4,Sara,Sunflower,4,3,false",
        "O5,Timur,Rose,8,25,false",
        "O6,Alina,Tulip,12,10,true"
    };

    private static ImmutableArray<Order> SampleOrders()
    {
        var parsed = OrderParser.ParseAll(SampleLines);
        Assert.Empty(parsed.Errors);
        return parsed.Orders;
    }

    private static Order MakeOrder(string id, FlowerType flower, int quantity, int distanceKm, bool isMember = false) =>
        new(id, "Test", flower, quantity, distanceKm, isMember);

    [Fact]
    public void NormalWorkflow_StandardPolicy_ProducesDocumentedResult()
    {
        var summary = OrderPipeline.Run(SampleOrders(), ShopRules.Default, DeliveryPolicies.Standard);

        Assert.Equal(new[] { "O1", "O3", "O6", "O2" }, summary.Quotes.Select(q => q.OrderId).ToArray());
        Assert.Equal(new[] { 2550m, 2200m, 2168m, 2040m }, summary.Quotes.Select(q => q.Total).ToArray());
        Assert.Equal(8958m, summary.GrandTotal);
        Assert.Equal(new[] { "O4", "O5" }, summary.Rejected.Select(r => r.OrderId).ToArray());
    }

    [Fact]
    public void NormalWorkflow_ExpressPolicy_ProducesDocumentedResult()
    {
        var summary = OrderPipeline.Run(SampleOrders(), ShopRules.Default, DeliveryPolicies.Express);

        Assert.Equal(new[] { "O3", "O1", "O6", "O2" }, summary.Quotes.Select(q => q.OrderId).ToArray());
        Assert.Equal(new[] { 3500m, 3100m, 2968m, 2040m }, summary.Quotes.Select(q => q.Total).ToArray());
        Assert.Equal(11608m, summary.GrandTotal);
    }

    [Fact]
    public void EmptyDataset_ParsesAsValidEmptyBatch()
    {
        var parsed = OrderParser.ParseAll(Array.Empty<string>());

        Assert.True(parsed.Orders.IsEmpty);
        Assert.True(parsed.Errors.IsEmpty);
    }

    [Fact]
    public void EmptyDataset_ProducesEmptySummaryWithoutCrashing()
    {
        var summary = OrderPipeline.Run(ImmutableArray<Order>.Empty, ShopRules.Default, DeliveryPolicies.Standard);

        Assert.Empty(summary.Quotes);
        Assert.Empty(summary.Rejected);
        Assert.Equal(0m, summary.GrandTotal);
    }

    [Fact]
    public void Boundary_ExactMinimumQuantityAndMaximumDistance_AreAccepted()
    {
        var order = MakeOrder("B", FlowerType.Lily, quantity: 5, distanceKm: 20);

        var selection = OrderPipeline.SelectEligible(ImmutableArray.Create(order), ShopRules.Default);

        Assert.Single(selection.Accepted);
        Assert.Empty(selection.Rejected);
    }

    [Fact]
    public void Boundary_JustOutsideLimits_AreRejectedWithReason()
    {
        var tooFew = MakeOrder("A", FlowerType.Rose, quantity: 4, distanceKm: 1);
        var tooFar = MakeOrder("B", FlowerType.Rose, quantity: 10, distanceKm: 21);

        var selection = OrderPipeline.SelectEligible(ImmutableArray.Create(tooFew, tooFar), ShopRules.Default);

        Assert.Empty(selection.Accepted);
        Assert.Equal(2, selection.Rejected.Length);
        Assert.Contains("minimum", selection.Rejected[0].Reason);
        Assert.Contains("limit", selection.Rejected[1].Reason);
    }

    [Fact]
    public void Boundary_BulkThreshold_20StemsGetsDiscount_19DoNot()
    {
        var at20 = MakeOrder("T20", FlowerType.Tulip, quantity: 20, distanceKm: 0);
        var at19 = MakeOrder("T19", FlowerType.Tulip, quantity: 19, distanceKm: 0);

        var priced = OrderPipeline.PriceOrders(
            OrderPipeline.SelectEligible(ImmutableArray.Create(at20, at19), ShopRules.Default),
            ShopRules.Default,
            DeliveryPolicies.Standard).Priced;

        Assert.Equal(2400m, priced[0].FlowersSubtotal);
        Assert.Equal(240m, priced[0].Discount);
        Assert.Equal(2160m, priced[0].Total);
        Assert.Equal(2280m, priced[1].FlowersSubtotal);
        Assert.Equal(0m, priced[1].Discount);
    }

    [Theory]
    [InlineData("B2,Dana,Orchid,10,5,false", "Unknown flower")]
    [InlineData("B3,Marat,Lily,abc,5,false", "Quantity")]
    [InlineData("B3,Marat,Lily,0,5,false", "Quantity")]
    [InlineData("B4,,Tulip,10,5,false", "Customer")]
    [InlineData("B5,Sara,Rose,10,-3,false", "DistanceKm")]
    [InlineData("B6,Timur,Rose,10,5", "fields")]
    [InlineData("B7,Alina,Rose,10,5,maybe", "IsMember")]
    [InlineData("", "missing")]
    [InlineData(null, "missing")]
    public void InvalidOrMissingLine_IsRejectedWithClearMessage(string? line, string expectedPart)
    {
        var ok = OrderParser.TryParse(line, out var order, out var error);

        Assert.False(ok);
        Assert.Null(order);
        Assert.Contains(expectedPart, error);
    }

    [Fact]
    public void ZeroDistanceAndFalseMembership_AreValidValues()
    {
        var ok = OrderParser.TryParse("Z1,Dana,Rose,5,0,false", out var order, out var error);

        Assert.True(ok);
        Assert.Equal("", error);
        Assert.NotNull(order);
        Assert.Equal(0, order.DistanceKm);
        Assert.False(order.IsMember);
    }

    [Fact]
    public void ParseAll_ReportsLineNumbersAndKeepsValidOrders()
    {
        var lines = new[] { "A,Dana,Rose,10,5,false", "B,Dana,Orchid,10,5,false", "A,Dana,Rose,10,5,false" };

        var parsed = OrderParser.ParseAll(lines, firstLineNumber: 2);

        Assert.Single(parsed.Orders);
        Assert.Equal(2, parsed.Errors.Length);
        Assert.StartsWith("Line 3:", parsed.Errors[0]);
        Assert.Contains("Duplicate", parsed.Errors[1]);
    }

    [Fact]
    public void MissingCollections_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => OrderParser.ParseAll(null!));

        Assert.Throws<ArgumentException>(() =>
            OrderPipeline.SelectEligible(default, ShopRules.Default));
        Assert.Throws<ArgumentNullException>(() =>
            OrderPipeline.Run(ImmutableArray<Order>.Empty, ShopRules.Default, null!));
    }

    [Fact]
    public void TwoPolicies_SameInput_GiveDifferentDocumentedResults()
    {
        var orders = SampleOrders();

        var standard = OrderPipeline.Run(orders, ShopRules.Default, DeliveryPolicies.Standard);
        var express = OrderPipeline.Run(orders, ShopRules.Default, DeliveryPolicies.Express);

        Assert.Equal(2550m, standard.Quotes.Single(q => q.OrderId == "O1").Total);
        Assert.Equal(3100m, express.Quotes.Single(q => q.OrderId == "O1").Total);

        Assert.Equal("O1", standard.Quotes[0].OrderId);
        Assert.Equal("O3", express.Quotes[0].OrderId);

        Assert.Equal(2040m, standard.Quotes.Single(q => q.OrderId == "O2").Total);
        Assert.Equal(2040m, express.Quotes.Single(q => q.OrderId == "O2").Total);
    }

    [Fact]
    public void HigherOrderStage_InvokesTheSuppliedDelegateOncePerAcceptedOrder()
    {
        var selection = OrderPipeline.SelectEligible(SampleOrders(), ShopRules.Default);
        var calls = 0;

        var pricing = OrderPipeline.PriceOrders(selection, ShopRules.Default, order =>
        {
            calls++;
            return 7m;
        });

        Assert.Equal(4, calls);
        Assert.All(pricing.Priced, p => Assert.Equal(7m, p.DeliveryFee));
    }

    [Fact]
    public void PolicyReturningNegativeFee_IsRejected()
    {
        var selection = OrderPipeline.SelectEligible(SampleOrders(), ShopRules.Default);

        Assert.Throws<InvalidOperationException>(() =>
            OrderPipeline.PriceOrders(selection, ShopRules.Default, _ => -1m));
    }

    [Fact]
    public void Stage1_SelectEligible_DoesNotChangeCallerOwnedInput()
    {
        var source = new List<Order>(SampleOrders());
        var snapshot = source.ToList();
        var input = source.ToImmutableArray();

        var result = OrderPipeline.SelectEligible(input, ShopRules.Default);

        Assert.Equal(4, result.Accepted.Length);
        Assert.Equal(2, result.Rejected.Length);
        Assert.True(snapshot.SequenceEqual(source));
        Assert.Equal(6, input.Length);
        Assert.True(snapshot.SequenceEqual(input));
    }

    [Fact]
    public void Stage2_PriceOrders_CalculatesSubtotalDiscountAndDelivery()
    {
        var member = MakeOrder("M", FlowerType.Tulip, quantity: 12, distanceKm: 10, isMember: true);
        var selection = new SelectionResult(ImmutableArray.Create(member), ImmutableArray<RejectedOrder>.Empty);

        var priced = OrderPipeline.PriceOrders(selection, ShopRules.Default, DeliveryPolicies.Standard).Priced.Single();

        Assert.Equal(1440m, priced.FlowersSubtotal);
        Assert.Equal(72m, priced.Discount);
        Assert.Equal(800m, priced.DeliveryFee);
        Assert.Equal(2168m, priced.Total);
        Assert.Single(selection.Accepted);
    }

    [Fact]
    public void Stage3_BuildSummary_OrdersByTotalThenIdAndSumsGrandTotal()
    {
        var a = new PricedOrder(MakeOrder("B", FlowerType.Rose, 5, 1), 1000m, 0m, 100m);
        var b = new PricedOrder(MakeOrder("A", FlowerType.Rose, 5, 1), 1000m, 0m, 100m);
        var c = new PricedOrder(MakeOrder("C", FlowerType.Rose, 5, 1), 2000m, 0m, 0m);
        var pricing = new PricingResult(ImmutableArray.Create(a, b, c), ImmutableArray<RejectedOrder>.Empty);

        var summary = OrderPipeline.BuildSummary(pricing);

        Assert.Equal(new[] { "C", "A", "B" }, summary.Quotes.Select(q => q.OrderId).ToArray());
        Assert.Equal(4200m, summary.GrandTotal);
    }

    [Fact]
    public void SameInputAndPolicy_AlwaysGiveTheSameOutput()
    {
        var orders = SampleOrders();

        var first = OrderPipeline.Run(orders, ShopRules.Default, DeliveryPolicies.Express);
        var second = OrderPipeline.Run(orders, ShopRules.Default, DeliveryPolicies.Express);

        Assert.Equal(first.GrandTotal, second.GrandTotal);
        Assert.True(first.Quotes.SequenceEqual(second.Quotes));
    }
}
