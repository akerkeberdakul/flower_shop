namespace FlowerShop;

public static class FlowerCatalog
{
    public static decimal UnitPrice(FlowerType flower) => flower switch
    {
        FlowerType.Rose => 200m,
        FlowerType.Tulip => 120m,
        FlowerType.Lily => 180m,
        FlowerType.Sunflower => 100m,
        _ => throw new ArgumentOutOfRangeException(nameof(flower), flower, "Unknown flower type.")
    };

    public static bool TryParse(string text, out FlowerType flower)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "rose": flower = FlowerType.Rose; return true;
            case "tulip": flower = FlowerType.Tulip; return true;
            case "lily": flower = FlowerType.Lily; return true;
            case "sunflower": flower = FlowerType.Sunflower; return true;
            default: flower = default; return false;
        }
    }
}
