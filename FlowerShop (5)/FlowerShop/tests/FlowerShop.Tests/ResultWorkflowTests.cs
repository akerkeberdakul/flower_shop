using System.Collections.Immutable;
using FlowerShop;
using Xunit.Sdk;

namespace FlowerShop.Tests;

public class ResultWorkflowTests
{
    private static T ValueOf<T>(Result<T, AppError> result) =>
        result.Match(
            value => value,
            error => throw new XunitException($"Expected success but got [{error.Code}] {error.Message}"));

    private static AppError ErrorOf<T>(Result<T, AppError> result) =>
        result.Match<AppError>(
            _ => throw new XunitException("Expected a failure but got a success."),
            error => error);

    private static Inventory Stock(params (FlowerType Flower, int Stock)[] items) =>
        Inventory.FromItems(items.Select(i => new StockItem(i.Flower, i.Stock)));

    private static Result<QuoteDisplay, AppError> RunQuote(
        Order order, Inventory inventory, Func<Order, decimal>? policy = null) =>
        QuoteWorkflow.RequestQuote(order, inventory, ShopRules.Default, policy ?? DeliveryPolicies.Standard);

    private static Order MakeOrder(FlowerType flower, int quantity, int distanceKm, bool member = false) =>
        new("Q", "Test", flower, quantity, distanceKm, member);

    private sealed class TempDirectory : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "FlowerShopTests_" + Guid.NewGuid().ToString("N"));

        public TempDirectory() { Directory.CreateDirectory(Root); }

        public string PathFor(string name) => Path.Combine(Root, name);

        public string Write(string name, string content)
        {
            var path = PathFor(name);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    [Fact]
    public void CompleteSuccess_ChainProducesTheCorrectDisplayModel()
    {
        var order = new Order("Q1", "Aigerim", FlowerType.Rose, 10, 5, false);

        var display = ValueOf(RunQuote(order, Stock((FlowerType.Rose, 100))));

        Assert.Equal("Q1", display.OrderId);
        Assert.Equal(2550m, display.Total);
        Assert.Equal(
            "Q1: 10 x Rose for Aigerim = 2550.00 (subtotal 2000.00, discount 0.00, delivery 550.00)",
            display.Text);
    }

    [Fact]
    public void DomainFailure_QuantityBelowMinimum_NamesTheRule()
    {
        var error = ErrorOf(RunQuote(MakeOrder(FlowerType.Rose, 4, 5), Stock((FlowerType.Rose, 100))));

        Assert.Equal(ErrorKind.Domain, error.Kind);
        Assert.Equal("quantity_below_minimum", error.Code);
        Assert.Contains("quantity 4", error.Message);
        Assert.Contains("minimum of 5", error.Message);
    }

    [Fact]
    public void DomainFailure_FlowerNotStocked_NamesTheMissingItem()
    {
        var error = ErrorOf(RunQuote(MakeOrder(FlowerType.Tulip, 10, 5), Stock((FlowerType.Rose, 100))));

        Assert.Equal(ErrorKind.Domain, error.Kind);
        Assert.Equal("flower_not_found", error.Code);
        Assert.Contains("Tulip", error.Message);
    }

    [Fact]
    public void DomainFailure_InsufficientStock_ShowsStockAndRequestedQuantity()
    {
        var error = ErrorOf(RunQuote(MakeOrder(FlowerType.Lily, 10, 5), Stock((FlowerType.Lily, 8))));

        Assert.Equal("insufficient_stock", error.Code);
        Assert.Contains("only 8 Lily", error.Message);
        Assert.Contains("10 requested", error.Message);
    }

    [Fact]
    public void DomainFailure_DistanceTooFar_NamesTheLimit()
    {
        var error = ErrorOf(RunQuote(MakeOrder(FlowerType.Rose, 10, 25), Stock((FlowerType.Rose, 100))));

        Assert.Equal("distance_too_far", error.Code);
        Assert.Contains("25 km", error.Message);
        Assert.Contains("20 km", error.Message);
    }

    [Fact]
    public void Lookup_PresentAndAbsent_AreBothHandled()
    {
        var inventory = Stock((FlowerType.Rose, 100));

        var present = inventory.Find(FlowerType.Rose);
        var absent = inventory.Find(FlowerType.Tulip);

        Assert.True(present.IsSome);
        Assert.Equal(100, present.Match(item => item.Stock, () => -1));
        Assert.True(absent.IsNone);
        Assert.Equal(-1, absent.Match(item => item.Stock, () => -1));
    }

    [Fact]
    public void Lookup_ZeroStock_IsPresentData_NotAbsence()
    {
        var inventory = Stock((FlowerType.Sunflower, 0));

        var found = inventory.Find(FlowerType.Sunflower);
        var error = ErrorOf(RunQuote(MakeOrder(FlowerType.Sunflower, 5, 2), inventory));

        Assert.True(found.IsSome);
        Assert.Equal(0, found.Match(item => item.Stock, () => -1));
        Assert.Equal("insufficient_stock", error.Code);
    }

    [Fact]
    public void LegitimatePrimitiveValues_AreSuccesses_AndNullIsRejected()
    {
        Assert.True(Result<int, AppError>.Ok(0).IsSuccess);
        Assert.True(Result<bool, AppError>.Ok(false).IsSuccess);
        Assert.True(Result<string, AppError>.Ok("").IsSuccess);
        Assert.True(Maybe<int>.Some(0).IsSome);
        Assert.True(Maybe<bool>.Some(false).IsSome);

        Assert.Throws<ArgumentNullException>(() => Result<string, AppError>.Ok(null!));
        Assert.Throws<ArgumentNullException>(() => Result<string, AppError>.Fail(null!));
        Assert.Throws<ArgumentNullException>(() => Maybe<string>.Some(null!));
    }

    [Fact]
    public void Quote_WithMemberFalse_IsNotTreatedAsMissingData()
    {
        var withoutMembership = ValueOf(RunQuote(MakeOrder(FlowerType.Lily, 5, 0, member: false), Stock((FlowerType.Lily, 8))));
        var withMembership = ValueOf(RunQuote(MakeOrder(FlowerType.Lily, 5, 0, member: true), Stock((FlowerType.Lily, 8))));

        Assert.Equal(900m, withoutMembership.Total);
        Assert.Equal(855m, withMembership.Total);
    }

    [Fact]
    public void Map_Success_ReceivesTheValueAndReturnsTheTransformedValue()
    {
        var received = -1;

        var mapped = Result<int, AppError>.Ok(5).Map(x =>
        {
            received = x;
            return x * 2;
        });

        Assert.Equal(5, received);
        Assert.Equal(10, ValueOf(mapped));
    }

    [Fact]
    public void Bind_Success_ReceivesTheValueAndKeepsTheNextResult()
    {
        var received = -1;
        var rejection = AppError.Domain("rejected", "The next step said no.");

        var continued = Result<int, AppError>.Ok(5).Bind(x =>
        {
            received = x;
            return Result<string, AppError>.Ok($"value {x}");
        });
        var rejected = Result<int, AppError>.Ok(5).Bind(_ => Result<string, AppError>.Fail(rejection));

        Assert.Equal(5, received);
        Assert.Equal("value 5", ValueOf(continued));
        Assert.Equal(rejection, ErrorOf(rejected));
    }

    [Fact]
    public void FailurePropagation_MapAndBind_KeepTheErrorAndDoNotCallTheirFunctions()
    {
        var error = AppError.Domain("boom", "Original failure.");
        var failure = Result<int, AppError>.Fail(error);
        var mapCalls = 0;
        var bindCalls = 0;

        var mapped = failure.Map(x =>
        {
            mapCalls++;
            return x + 1;
        });
        var bound = failure.Bind(x =>
        {
            bindCalls++;
            return Result<int, AppError>.Ok(x + 1);
        });

        Assert.Equal(0, mapCalls);
        Assert.Equal(0, bindCalls);
        Assert.Equal(error, ErrorOf(mapped));
        Assert.Equal(error, ErrorOf(bound));
    }

    [Fact]
    public void FailurePropagation_MissingItem_SkipsValidationAndPricing()
    {
        var pricingCalls = 0;
        Func<Order, decimal> countingPolicy = _ =>
        {
            pricingCalls++;
            return 0m;
        };

        var failed = RunQuote(MakeOrder(FlowerType.Tulip, 10, 5), Stock((FlowerType.Rose, 100)), countingPolicy);
        var rejected = RunQuote(MakeOrder(FlowerType.Rose, 4, 5), Stock((FlowerType.Rose, 100)), countingPolicy);

        Assert.Equal("flower_not_found", ErrorOf(failed).Code);
        Assert.Equal("quantity_below_minimum", ErrorOf(rejected).Code);
        Assert.Equal(0, pricingCalls);

        ValueOf(RunQuote(MakeOrder(FlowerType.Rose, 10, 5), Stock((FlowerType.Rose, 100)), countingPolicy));
        Assert.Equal(1, pricingCalls);
    }

    [Fact]
    public void MissingFile_IsReportedAsOperationalFailure_AndStateStaysConsistent()
    {
        using var temp = new TempDirectory();
        var goodPath = temp.Write("inventory.csv", "Flower,Stock\nRose,100\nLily,8\n");
        var loadedBefore = ValueOf(InventoryLoader.Load(goodPath));
        var missingPath = temp.PathFor("does_not_exist.csv");

        var failed = InventoryLoader.Load(missingPath);

        var error = ErrorOf(failed);
        Assert.Equal(ErrorKind.Operational, error.Kind);
        Assert.Equal("file_not_found", error.Code);
        Assert.Contains(missingPath, error.Message);
        Assert.False(File.Exists(missingPath));
        Assert.Equal(2, loadedBefore.Count);
        Assert.True(loadedBefore.Find(FlowerType.Rose).IsSome);
    }

    [Fact]
    public void MissingFile_BlankPath_IsReportedAsInvalidPath()
    {
        var error = ErrorOf(FileBoundary.ReadLines("  ", "inventory"));

        Assert.Equal("invalid_path", error.Code);
    }

    [Fact]
    public void MalformedFile_BecomesOneClearFailure_WithoutAPartialInventory()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("inventory.csv", "Flower,Stock\nRose,100\nLily,abc\nOrchid,5\n");

        var result = InventoryLoader.Load(path);

        var error = ErrorOf(result);
        Assert.Equal(ErrorKind.Operational, error.Kind);
        Assert.Equal("malformed_content", error.Code);
        Assert.Contains(path, error.Message);
        Assert.Contains("Line 3", error.Message);
        Assert.Contains("Line 4", error.Message);
    }

    [Theory]
    [InlineData("Flower,Stock\nRose,abc")]
    [InlineData("Flower,Stock\nRose,-1")]
    [InlineData("Flower,Stock\nOrchid,5")]
    [InlineData("Flower,Stock\nRose,5\nRose,6")]
    [InlineData("Flower,Stock\nRose")]
    [InlineData("Name,Count\nRose,5")]
    [InlineData("")]
    public void MalformedContent_InMemory_IsAlwaysAMalformedFailure(string content)
    {
        var result = InventoryLoader.Parse(content.Split('\n'), "test.csv");

        Assert.Equal("malformed_content", ErrorOf(result).Code);
    }

    [Fact]
    public void ValidInventoryContent_WithZeroStock_LoadsEveryLine()
    {
        var inventory = ValueOf(InventoryLoader.Parse(new[] { "Flower,Stock", "Rose,5", "Sunflower,0" }, "test.csv"));

        Assert.Equal(2, inventory.Count);
        Assert.Equal(0, inventory.Find(FlowerType.Sunflower).Match(item => item.Stock, () => -1));
    }

    [Fact]
    public void RequestFile_ValidAndMalformed_AreHandledAtTheBoundary()
    {
        using var temp = new TempDirectory();
        var good = temp.Write("good.csv", "OrderId,Customer,Flower,Quantity,DistanceKm,IsMember\nQ1,Aigerim,Rose,10,5,false\n");
        var bad = temp.Write("bad.csv", "OrderId,Customer,Flower,Quantity,DistanceKm,IsMember\nQ1,Aigerim,Orchid,10,5,false\n");

        var orders = ValueOf(RequestLoader.Load(good));
        var error = ErrorOf(RequestLoader.Load(bad));

        Assert.Single(orders);
        Assert.Equal("malformed_content", error.Code);
        Assert.Contains("Line 2", error.Message);
    }

    [Fact]
    public void Integration_LoadedFiles_FlowThroughTheWholeChain()
    {
        using var temp = new TempDirectory();
        var inventoryPath = temp.Write("inventory.csv", "Flower,Stock\nRose,100\n");
        var requestsPath = temp.Write(
            "requests.csv",
            "OrderId,Customer,Flower,Quantity,DistanceKm,IsMember\nQ1,Aigerim,Rose,10,5,false\nQ2,Dana,Tulip,10,5,false\n");

        var loaded = InventoryLoader.Load(inventoryPath)
            .Bind(inventory => RequestLoader.Load(requestsPath).Map(requests => (Inventory: inventory, Requests: requests)));
        var (stock, requests) = ValueOf(loaded);

        var outcomes = requests
            .Select(r => QuoteWorkflow.RequestQuote(r, stock, ShopRules.Default, DeliveryPolicies.Standard))
            .ToList();

        Assert.Equal(2550m, ValueOf(outcomes[0]).Total);
        Assert.Equal("flower_not_found", ErrorOf(outcomes[1]).Code);
    }
}
