using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.DTOs;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController(OrderDbContext db, OrderManagementService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(new
    {
        Success = true,
        Data = await db.Orders.AsNoTracking().Include(o => o.Items).OrderByDescending(o => o.Id).ToListAsync(ct)
    });

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        Ok(new { Success = true, Data = await service.GetOrderAsync(id, ct) });

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderDto dto, CancellationToken ct)
    {
        var order = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, new { Success = true, Data = order });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateOrderStatusDto dto, CancellationToken ct) =>
        Ok(new { Success = true, Data = await service.UpdateStatusAsync(id, dto, ct) });

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteOrderAsync(id, ct);
        return Ok(new { Success = true, Message = "Đã xóa đơn hàng và các chi tiết liên quan." });
    }
}
