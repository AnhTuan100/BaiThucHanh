using System.ComponentModel.DataAnnotations;

namespace ReportService.DTOs;

/// <summary>
/// Yêu cầu tạo báo cáo đơn hàng. Chỉ cần truyền order_id;
/// service sẽ tự gọi OrderService để lấy chi tiết và tính toán.
/// </summary>
public sealed class CreateOrderReportRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "order_id phải lớn hơn 0.")]
    public int OrderId { get; set; }
}

/// <summary>
/// Yêu cầu tạo báo cáo sản phẩm (đứng độc lập, không gắn vào đơn hàng).
/// Service sẽ gọi OrderService để lấy tất cả order_items có product_id này,
/// sau đó tính tổng. cost_price là giá vốn nhập do người dùng cung cấp
/// vì ProductService hiện không lưu giá vốn.
/// </summary>
public sealed class CreateProductReportRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "product_id phải lớn hơn 0.")]
    public int ProductId { get; set; }

    [Range(0, 99999999.99, ErrorMessage = "cost_price phải trong [0, 99999999.99].")]
    public decimal CostPrice { get; set; }
}
