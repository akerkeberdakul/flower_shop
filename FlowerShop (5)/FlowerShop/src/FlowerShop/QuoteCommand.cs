using System.Collections.Immutable;

namespace FlowerShop;

public static class QuoteCommand
{
    public static int Run(string[] args)
    {
        var inventoryPath = DataPaths.Resolve(args.Length > 0 ? args[0] : Path.Combine("data", "inventory.csv"));
        var requestsPath = DataPaths.Resolve(args.Length > 1 ? args[1] : Path.Combine("data", "quote_requests.csv"));

        var loaded = InventoryLoader.Load(inventoryPath)
            .Bind(inventory => RequestLoader.Load(requestsPath)
                .Map(requests => (Inventory: inventory, Requests: requests)));

        return loaded.Match(
            data =>
            {
                PrintQuotes(data.Inventory, data.Requests);
                return 0;
            },
            error =>
            {
                Console.WriteLine($"ERROR ({error.Kind}) [{error.Code}] {error.Message}");
                return 1;
            });
    }

    private static void PrintQuotes(Inventory inventory, ImmutableArray<Order> requests)
    {
        Console.WriteLine($"Loaded {inventory.Count} stock lines and {requests.Length} quote requests.");
        Console.WriteLine();

        foreach (var request in requests)
        {
            var result = QuoteWorkflow.RequestQuote(request, inventory, ShopRules.Default, DeliveryPolicies.Standard);

            Console.WriteLine(result.Match(
                quote => $"OK      {quote.Text}",
                error => $"FAILED  [{error.Code}] {error.Message}"));
        }
    }
}
