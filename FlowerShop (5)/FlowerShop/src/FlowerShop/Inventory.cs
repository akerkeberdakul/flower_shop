using System.Collections.Immutable;

namespace FlowerShop;

public sealed record StockItem(FlowerType Flower, int Stock);

public sealed class Inventory
{
    private readonly ImmutableDictionary<FlowerType, int> _stock;

    private Inventory(ImmutableDictionary<FlowerType, int> stock) { _stock = stock; }

    public int Count => _stock.Count;

    public static Inventory FromItems(IEnumerable<StockItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var builder = ImmutableDictionary.CreateBuilder<FlowerType, int>();
        foreach (var item in items)
        {
            if (item.Stock < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(items), $"Stock of {item.Flower} cannot be negative.");
            }

            if (builder.ContainsKey(item.Flower))
            {
                throw new ArgumentException($"{item.Flower} appears more than once.", nameof(items));
            }

            builder.Add(item.Flower, item.Stock);
        }

        return new Inventory(builder.ToImmutable());
    }

    public Maybe<StockItem> Find(FlowerType flower) =>
        _stock.TryGetValue(flower, out var stock)
            ? Maybe<StockItem>.Some(new StockItem(flower, stock))
            : Maybe<StockItem>.None;
}
