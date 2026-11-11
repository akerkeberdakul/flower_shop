using System.Globalization;

namespace FlowerShop;

public static class InventoryLoader
{
    public const string ExpectedHeader = "Flower,Stock";

    public static Result<Inventory, AppError> Load(string? path) =>
        FileBoundary.ReadLines(path, "inventory")
            .Bind(lines => Parse(lines, path ?? ""));

    public static Result<Inventory, AppError> Parse(IReadOnlyList<string> lines, string source)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count == 0 || lines[0].Replace(" ", "") != ExpectedHeader)
        {
            return Malformed(source, $"the first line must be the header '{ExpectedHeader}'.");
        }

        var items = new List<StockItem>();
        var problems = new List<string>();
        var seen = new HashSet<FlowerType>();

        for (var i = 1; i < lines.Count; i++)
        {
            var lineNumber = i + 1;
            var fields = lines[i].Split(',');

            if (fields.Length != 2)
            {
                problems.Add($"Line {lineNumber}: expected 2 fields but found {fields.Length}.");
            }
            else if (!FlowerCatalog.TryParse(fields[0], out var flower))
            {
                problems.Add($"Line {lineNumber}: unknown flower '{fields[0].Trim()}'.");
            }
            else if (!int.TryParse(fields[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var stock) || stock < 0)
            {
                problems.Add($"Line {lineNumber}: stock must be a whole number of 0 or more.");
            }
            else if (!seen.Add(flower))
            {
                problems.Add($"Line {lineNumber}: duplicate flower '{flower}'.");
            }
            else
            {
                items.Add(new StockItem(flower, stock));
            }
        }

        return problems.Count > 0
            ? Malformed(source, string.Join(" ", problems))
            : Result<Inventory, AppError>.Ok(Inventory.FromItems(items));
    }

    private static Result<Inventory, AppError> Malformed(string source, string details) =>
        Result<Inventory, AppError>.Fail(
            AppError.Operational("malformed_content", $"Inventory file '{source}' is malformed: {details}"));
}
