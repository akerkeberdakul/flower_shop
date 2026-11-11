using FlowerShop;

if (args.Length > 0 && args[0] == "quote")
{
    return QuoteCommand.Run(args.Skip(1).ToArray());
}

const string ExpectedHeader = "OrderId,Customer,Flower,Quantity,DistanceKm,IsMember";

var path = DataPaths.Resolve(args.Length > 0 ? args[0] : Path.Combine("data", "orders.csv"));

string[] lines;
try
{
    lines = File.ReadAllLines(path);
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    Console.WriteLine($"Cannot read '{path}': {ex.Message}");
    return 1;
}

if (lines.Length == 0 || lines[0].Replace(" ", "") != ExpectedHeader)
{
    Console.WriteLine($"The file must start with the header: {ExpectedHeader}");
    return 1;
}

var parsed = OrderParser.ParseAll(lines.Skip(1), firstLineNumber: 2);

if (parsed.Errors.Length > 0)
{
    Console.WriteLine("Invalid input:");
    foreach (var error in parsed.Errors)
    {
        Console.WriteLine($"  {error}");
    }
    Console.WriteLine();
}

if (parsed.Orders.IsEmpty && parsed.Errors.Length > 0)
{
    Console.WriteLine("No valid orders to process.");
    return 2;
}

if (parsed.Orders.IsEmpty)
{
    Console.WriteLine("The dataset is valid but contains no orders.");
    Console.WriteLine();
}

var rules = ShopRules.Default;

var standard = OrderPipeline.Run(parsed.Orders, rules, DeliveryPolicies.Standard);
var express = OrderPipeline.Run(parsed.Orders, rules, DeliveryPolicies.Express);

Console.WriteLine(ReportFormatter.Format("STANDARD delivery", standard));
Console.WriteLine(ReportFormatter.Format("EXPRESS delivery", express));

return parsed.Errors.IsEmpty ? 0 : 2;
