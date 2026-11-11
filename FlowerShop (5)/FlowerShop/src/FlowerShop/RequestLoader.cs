using System.Collections.Immutable;

namespace FlowerShop;

public static class RequestLoader
{
    public static Result<ImmutableArray<Order>, AppError> Load(string? path) =>
        FileBoundary.ReadLines(path, "quote requests")
            .Bind(lines => Parse(lines, path ?? ""));

    public static Result<ImmutableArray<Order>, AppError> Parse(IReadOnlyList<string> lines, string source)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count == 0 || lines[0].Replace(" ", "") != OrderParser.ExpectedHeader)
        {
            return Malformed(source, $"the first line must be the header '{OrderParser.ExpectedHeader}'.");
        }

        var parsed = OrderParser.ParseAll(lines.Skip(1), firstLineNumber: 2);

        return parsed.Errors.IsEmpty
            ? Result<ImmutableArray<Order>, AppError>.Ok(parsed.Orders)
            : Malformed(source, string.Join(" ", parsed.Errors));
    }

    private static Result<ImmutableArray<Order>, AppError> Malformed(string source, string details) =>
        Result<ImmutableArray<Order>, AppError>.Fail(
            AppError.Operational("malformed_content", $"Quote requests file '{source}' is malformed: {details}"));
}
