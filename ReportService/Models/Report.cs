using System.Text.Json.Serialization;

namespace ReportService.Models;

/// <summary>Báo cáo đơn hàng — bảng orders_reports.</summary>
public sealed class OrderReport
{
    public int Id { get; set; }

    /// <summary>Lưu vết — ID đơn hàng từ OrderService.</summary>
    public int OrderId { get; set; }

    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }

    public List<ProductReport> ProductReports { get; set; } = [];
}

/// <summary>Báo cáo sản phẩm — bảng product_reports.</summary>
public sealed class ProductReport
{
    public int Id { get; set; }

    /// <summary>Khóa ngoại sang orders_reports.</summary>
    public int OrderReportId { get; set; }

    /// <summary>Lưu vết — ID sản phẩm từ ProductService.</summary>
    public int ProductId { get; set; }

    public int TotalSold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; }

    [JsonIgnore]
    public OrderReport OrderReport { get; set; } = null!;
}
