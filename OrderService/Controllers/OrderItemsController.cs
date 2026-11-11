using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.DTOs;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("order_items")]
public class OrderItemsController(OrderDbContext db, OrderManagementService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(new
    {
        Success = true,
        Data = await db.OrderItems.AsNoTracking().OrderBy(i => i.Id).ToListAsync(ct)
    });

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        Ok(new { Success = true, Data = await service.GetItemAsync(id, ct) });

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderItemDto dto, CancellationToken ct)
    {
        var item = await service.CreateItemAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, new { Success = true, Data = item });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, OrderItemInput dto, CancellationToken ct) =>
        Ok(new { Success = true, Data = await service.UpdateItemAsync(id, dto, ct) });

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await service.DeleteItemAsync(id, ct);
        return Ok(new { Success = true, Message = "Đã xóa chi tiết đơn hàng và tính lại tổng tiền." });
    }
}
