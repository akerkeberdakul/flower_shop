using System.Text;

namespace FlowerShop;

public static class ReportFormatter
{
    public static string Format(string title, QuoteSummary summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== {title} ===");

        if (summary.Quotes.IsEmpty)
        {
            sb.AppendLine("No quotes.");
        }

        foreach (var q in summary.Quotes)
        {
            sb.AppendLine(FormattableString.Invariant(
                $"{q.OrderId,-4} {q.Customer,-10} {q.Quantity,3} x {q.Flower,-9} subtotal {q.FlowersSubtotal,8:F2}  discount {q.Discount,7:F2}  delivery {q.DeliveryFee,7:F2}  TOTAL {q.Total,8:F2}"));
        }

        foreach (var r in summary.Rejected)
        {
            sb.AppendLine($"REJECTED {r.OrderId}: {r.Reason}");
        }

        sb.AppendLine(FormattableString.Invariant($"Grand total: {summary.GrandTotal:F2}"));
        return sb.ToString();
    }
}
