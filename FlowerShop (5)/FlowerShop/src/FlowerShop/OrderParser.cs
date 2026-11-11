using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace FlowerShop;

public static class OrderParser
{
    public const int FieldCount = 6;
    public const string ExpectedHeader = "OrderId,Customer,Flower,Quantity,DistanceKm,IsMember";

    public static bool TryParse(string? line, [NotNullWhen(true)] out Order? order, out string error)
    {
        order = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            error = "The line is missing or empty.";
            return false;
        }

        var fields = line.Split(',');
        if (fields.Length != FieldCount)
        {
            error = $"Expected {FieldCount} fields but found {fields.Length}.";
            return false;
        }

        var id = fields[0].Trim();
        if (id.Length == 0)
        {
            error = "OrderId is required.";
            return false;
        }

        var customer = fields[1].Trim();
        if (customer.Length == 0)
        {
            error = "Customer name is required.";
            return false;
        }

        if (!FlowerCatalog.TryParse(fields[2], out var flower))
        {
            error = $"Unknown flower '{fields[2].Trim()}'.";
            return false;
        }

        if (!int.TryParse(fields[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity)
            || quantity <= 0)
        {
            error = "Quantity must be a whole number greater than zero.";
            return false;
        }

        if (!int.TryParse(fields[4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var distanceKm)
            || distanceKm < 0)
        {
            error = "DistanceKm must be a whole number of 0 or more.";
            return false;
        }

        if (!bool.TryParse(fields[5].Trim(), out var isMember))
        {
            error = "IsMember must be 'true' or 'false'.";
            return false;
        }

        order = new Order(id, customer, flower, quantity, distanceKm, isMember);
        error = "";
        return true;
    }

    public static ParsedBatch ParseAll(IEnumerable<string?> lines, int firstLineNumber = 1)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var orders = ImmutableArray.CreateBuilder<Order>();
        var errors = ImmutableArray.CreateBuilder<string>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lineNumber = firstLineNumber - 1;

        foreach (var line in lines)
        {
            lineNumber++;
            if (!TryParse(line, out var order, out var error))
            {
                errors.Add($"Line {lineNumber}: {error}");
            }
            else if (!seenIds.Add(order.Id))
            {
                errors.Add($"Line {lineNumber}: Duplicate OrderId '{order.Id}'.");
            }
            else
            {
                orders.Add(order);
            }
        }

        return new ParsedBatch(orders.ToImmutable(), errors.ToImmutable());
    }
}
