using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.DTOs;
using OrderService.Models;
using ServiceDefaults;

namespace OrderService.Services;

public sealed class OrderManager(OrderDbContext db, ProductClient products)
{
    private const decimal MaximumAmount = 99999999.99m;

    public async Task<Order> FindAsync(int id, CancellationToken ct) =>
        await db.Orders.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new ApiException(404, "Khong tim thay don hang.");

    public async Task<OrderItem> FindItemAsync(int id, CancellationToken ct) =>
        await db.OrderItems.Include(x => x.Order).ThenInclude(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new ApiException(404, "Khong tim thay chi tiet don hang.");

    public async Task<Order> CreateAsync(CreateOrderRequest request, string token, CancellationToken ct)
    {
        if (request.Items.Any(x => x is null))
            throw new ApiException(400, "Mat hang khong duoc null.");
        var now = DateTime.UtcNow;
        var order = new Order
        {
            CustomerName = request.CustomerName.Trim(), CustomerEmail = request.CustomerEmail.Trim(),
            CreatedAt = now, UpdatedAt = now
        };
        // Aggregate duplicates before checking stock.
        foreach (var group in request.Items.GroupBy(x => x.ProductId))
        {
            var quantity = group.Sum(x => (long)x.Quantity);
            var product = await products.GetAsync(group.Key, token, ct);
            CheckStock(product, quantity);
            order.Items.Add(BuildItem(product, (int)quantity));
        }
        Recalculate(order);
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        return order;
    }

    public async Task<Order> UpdateStatusAsync(int id, string status, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var order = await FindAsync(id, ct);
        if (order.Status != status)
        {
            RequirePending(order);
            order.Status = status;
            order.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return order;
    }

    public async Task<OrderItem> AddItemAsync(CreateItemRequest request, string token, CancellationToken ct)
    {
        var product = await products.GetAsync(request.ProductId, token, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var order = await FindAsync(request.OrderId, ct);
        RequirePending(order);
        if (order.Items.Any(x => x.ProductId == request.ProductId))
            throw new ApiException(409, "San pham da co trong don. Hay cap nhat chi tiet hien co.");
        CheckStock(product, request.Quantity);
        var item = BuildItem(product, request.Quantity);
        order.Items.Add(item);
        Recalculate(order);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return item;
    }

    public async Task<OrderItem> UpdateItemAsync(int id, ItemRequest request, string token, CancellationToken ct)
    {
        var product = await products.GetAsync(request.ProductId, token, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await FindItemAsync(id, ct);
        RequirePending(item.Order);
        if (item.Order.Items.Any(x => x.Id != id && x.ProductId == request.ProductId))
            throw new ApiException(409, "San pham da co trong don hang.");
        CheckStock(product, request.Quantity);
        // Keep the original snapshot when only quantity changes.
        if (item.ProductId != request.ProductId)
        {
            item.ProductId = product.Id;
            item.ProductName = product.Name;
            item.UnitPrice = product.Price;
        }
        item.Quantity = request.Quantity;
        item.TotalPrice = item.UnitPrice * item.Quantity;
        Recalculate(item.Order);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return item;
    }

    public async Task DeleteItemAsync(int id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var item = await FindItemAsync(id, ct);
        RequirePending(item.Order);
        if (item.Order.Items.Count == 1)
            throw new ApiException(409, "Don hang phai co it nhat mot mat hang. Hay xoa don hang neu can.");
        item.Order.Items.Remove(item);
        db.OrderItems.Remove(item);
        Recalculate(item.Order);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private static OrderItem BuildItem(ProductSnapshot product, int quantity) => new()
    {
        ProductId = product.Id, ProductName = product.Name, Quantity = quantity,
        UnitPrice = product.Price, TotalPrice = product.Price * quantity
    };

    private static void RequirePending(Order order)
    {
        if (order.Status != "pending")
            throw new ApiException(409, "Chi duoc sua don hang o trang thai pending.");
    }

    private static void CheckStock(ProductSnapshot product, long quantity)
    {
        if (quantity > product.Quantity)
            throw new ApiException(409, $"San pham {product.Id} chi con {product.Quantity} trong kho.");
    }

    private static void Recalculate(Order order)
    {
        var total = order.Items.Sum(x => x.TotalPrice);
        if (total > MaximumAmount)
            throw new ApiException(400, "Tong tien vuot gioi han DECIMAL(10,2).");
        order.TotalAmount = total;
        order.UpdatedAt = DateTime.UtcNow;
    }
}
