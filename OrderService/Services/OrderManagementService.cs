using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.DTOs;
using OrderService.Models;

namespace OrderService.Services;

public sealed class OrderManagementService(OrderDbContext db, ProductClient products)
{
    private const decimal MaxMoney = 99999999.99m;

    public async Task<Order> GetOrderAsync(int id, CancellationToken ct) =>
        await db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == id, ct)
        ?? throw new ApiException(404, $"Không tìm thấy đơn hàng ID = {id}.");

    public async Task<OrderItem> GetItemAsync(int id, CancellationToken ct) =>
        await db.OrderItems.SingleOrDefaultAsync(i => i.Id == id, ct)
        ?? throw new ApiException(404, $"Không tìm thấy chi tiết đơn hàng ID = {id}.");

    public async Task<Order> CreateAsync(CreateOrderDto dto, CancellationToken ct)
    {
        if (dto.Items.Any(i => i is null))
            throw new ApiException(400, "Danh sách mặt hàng không được chứa phần tử null.");
        // Gộp sản phẩm trùng trước khi kiểm tra để không vượt tồn kho trong cùng đơn hàng.
        var order = new Order
        {
            CustomerName = dto.CustomerName.Trim(),
            CustomerEmail = dto.CustomerEmail.Trim()
        };
        foreach (var group in dto.Items.GroupBy(i => i.ProductId))
        {
            var quantity = group.Sum(i => (long)i.Quantity);
            var product = await products.GetAsync(group.Key, ct);
            CheckStock(product, quantity);
            order.Items.Add(NewItem(product, (int)quantity));
        }
        Recalculate(order);
        db.Orders.Add(order);
        // Một SaveChanges ghi cả đơn hàng và chi tiết trong cùng transaction.
        await db.SaveChangesAsync(ct);
        return order;
    }

    public async Task<Order> UpdateStatusAsync(int id, UpdateOrderStatusDto dto, CancellationToken ct)
    {
        var order = await GetOrderAsync(id, ct);
        order.Status = dto.Status;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return order;
    }

    public async Task DeleteOrderAsync(int id, CancellationToken ct)
    {
        var order = await GetOrderAsync(id, ct);
        db.Orders.Remove(order);
        await db.SaveChangesAsync(ct);
    }

    public async Task<OrderItem> CreateItemAsync(CreateOrderItemDto dto, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var order = await GetOrderAsync(dto.OrderId, ct);
        CheckEditable(order);
        var product = await products.GetAsync(dto.ProductId, ct);
        CheckStock(product, order.Items.Where(i => i.ProductId == dto.ProductId).Sum(i => (long)i.Quantity) + dto.Quantity);
        var item = NewItem(product, dto.Quantity);
        order.Items.Add(item);
        Recalculate(order);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return item;
    }

    public async Task<OrderItem> UpdateItemAsync(int id, OrderItemInput dto, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await GetItemAsync(id, ct);
        var order = await GetOrderAsync(item.OrderId, ct);
        CheckEditable(order);
        var product = await products.GetAsync(dto.ProductId, ct);
        CheckStock(product, order.Items.Where(i => i.Id != id && i.ProductId == dto.ProductId)
            .Sum(i => (long)i.Quantity) + dto.Quantity);
        // Giữ giá tại thời điểm đặt hàng nếu chỉ thay đổi số lượng của cùng sản phẩm.
        if (item.ProductId != dto.ProductId)
        {
            item.ProductId = product.Id;
            item.ProductName = product.Name;
            item.UnitPrice = Money(product.Price);
        }
        item.Quantity = dto.Quantity;
        item.TotalPrice = Money(item.UnitPrice * item.Quantity);
        Recalculate(order);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return item;
    }

    public async Task DeleteItemAsync(int id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await GetItemAsync(id, ct);
        var order = await GetOrderAsync(item.OrderId, ct);
        CheckEditable(order);
        order.Items.Remove(item);
        db.OrderItems.Remove(item);
        Recalculate(order);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static OrderItem NewItem(ProductSnapshot product, int quantity)
    {
        var unitPrice = Money(product.Price);
        return new OrderItem
        {
            ProductId = product.Id, ProductName = product.Name,
            Quantity = quantity, UnitPrice = unitPrice, TotalPrice = Money(unitPrice * quantity)
        };
    }

    private static void CheckStock(ProductSnapshot product, long quantity)
    {
        if (quantity > product.Quantity)
            throw new ApiException(409, $"Sản phẩm '{product.Name}' không đủ tồn kho: cần {quantity}, hiện còn {product.Quantity}.");
    }

    private static decimal Money(decimal value)
    {
        var rounded = decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        if (rounded < 0 || rounded > MaxMoney)
            throw new ApiException(400, "Số tiền vượt phạm vi DECIMAL(10,2).");
        return rounded;
    }

    private static void Recalculate(Order order)
    {
        order.TotalAmount = Money(order.Items.Sum(i => i.TotalPrice));
        order.UpdatedAt = DateTime.UtcNow;
    }

    private static void CheckEditable(Order order)
    {
        if (order.Status != "pending")
            throw new ApiException(409, "Chỉ được thay đổi mặt hàng khi đơn hàng đang pending.");
    }
}
