using Microsoft.EntityFrameworkCore;
using ReportService.Data;
using ReportService.DTOs;
using ReportService.Models;
using ServiceDefaults;

namespace ReportService.Services;

public sealed class ReportManager(ReportDbContext db, OrderClient orderClient)
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    public async Task<OrderReport> FindOrderReportAsync(int id, CancellationToken ct) =>
        await db.OrderReports.Include(x => x.ProductReports)
                             .SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new ApiException(404, "Không tìm thấy báo cáo đơn hàng.");

    public async Task<ProductReport> FindProductReportAsync(int id, CancellationToken ct) =>
        await db.ProductReports.Include(x => x.OrderReport)
                               .SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new ApiException(404, "Không tìm thấy báo cáo sản phẩm.");

    // ── Tạo báo cáo đơn hàng ─────────────────────────────────────────────────
    // POST /reports/orders
    // Gọi OrderService lấy chi tiết đơn, tính revenue/cost/profit cho từng
    // sản phẩm rồi lưu vào orders_reports + product_reports.
    // CostPrice mặc định = UnitPrice * 0.7 (giả lập giá vốn 70%).

    public async Task<OrderReport> CreateOrderReportAsync(
        CreateOrderReportRequest request, string token, CancellationToken ct)
    {
        // Kiểm tra báo cáo cho đơn hàng này đã tồn tại chưa
        if (await db.OrderReports.AnyAsync(x => x.OrderId == request.OrderId, ct))
            throw new ApiException(409, $"Báo cáo cho đơn hàng {request.OrderId} đã tồn tại.");

        var order = await orderClient.GetOrderAsync(request.OrderId, token, ct);

        decimal totalRevenue = 0, totalCost = 0;
        var productReports = new List<ProductReport>();

        foreach (var item in order.Items)
        {
            var revenue = item.TotalPrice;
            // Giá vốn ước tính = đơn giá * 70% * số lượng (nếu không có nguồn giá vốn thực tế)
            var costPerUnit = decimal.Round(item.UnitPrice * 0.7m, 2);
            var cost = costPerUnit * item.Quantity;
            var profit = revenue - cost;

            productReports.Add(new ProductReport
            {
                ProductId = item.ProductId,
                TotalSold = item.Quantity,
                Revenue = decimal.Round(revenue, 2),
                Cost = decimal.Round(cost, 2),
                Profit = decimal.Round(profit, 2)
            });

            totalRevenue += revenue;
            totalCost += cost;
        }

        var orderReport = new OrderReport
        {
            OrderId = order.Id,
            TotalRevenue = decimal.Round(totalRevenue, 2),
            TotalCost = decimal.Round(totalCost, 2),
            TotalProfit = decimal.Round(totalRevenue - totalCost, 2),
            ProductReports = productReports
        };

        db.OrderReports.Add(orderReport);
        await db.SaveChangesAsync(ct);
        return orderReport;
    }

    // ── Tạo báo cáo sản phẩm ─────────────────────────────────────────────────
    // POST /reports/products
    // Tổng hợp tất cả order_items trong mọi đơn hàng có product_id khớp.
    // Người dùng cung cấp cost_price (giá vốn/đơn vị) vì ProductService
    // không lưu giá vốn.

    public async Task<ProductReport> CreateProductReportAsync(
        CreateProductReportRequest request, string token, CancellationToken ct)
    {
        // Kiểm tra báo cáo tổng hợp theo product_id này đã tồn tại chưa
        // (không gắn vào order_report nào cụ thể — order_report_id = 0 theo quy ước)
        if (await db.ProductReports.AnyAsync(
                x => x.ProductId == request.ProductId && x.OrderReportId == 0, ct))
            throw new ApiException(409, $"Báo cáo tổng hợp cho sản phẩm {request.ProductId} đã tồn tại.");

        var allOrders = await orderClient.GetAllOrdersAsync(token, ct);

        var items = allOrders
            .SelectMany(o => o.Items)
            .Where(i => i.ProductId == request.ProductId)
            .ToList();

        if (items.Count == 0)
            throw new ApiException(404, $"Không có dữ liệu bán hàng cho sản phẩm {request.ProductId}.");

        int totalSold = items.Sum(i => i.Quantity);
        decimal revenue = decimal.Round(items.Sum(i => i.TotalPrice), 2);
        decimal cost = decimal.Round(request.CostPrice * totalSold, 2);
        decimal profit = decimal.Round(revenue - cost, 2);

        var productReport = new ProductReport
        {
            OrderReportId = 0,   // 0 = báo cáo tổng hợp độc lập, không thuộc đơn hàng cụ thể
            ProductId = request.ProductId,
            TotalSold = totalSold,
            Revenue = revenue,
            Cost = cost,
            Profit = profit
        };

        db.ProductReports.Add(productReport);
        await db.SaveChangesAsync(ct);
        return productReport;
    }
}
