using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.DTOs;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("order_items")]
public sealed class OrderItemsController(OrderDbContext db, OrderManager manager) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await db.OrderItems.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(await manager.FindItemAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateItemRequest request, CancellationToken ct)
    {
        var item = await manager.AddItemAsync(request, Request.Headers.Authorization.ToString(), ct);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ItemRequest request, CancellationToken ct) =>
        Ok(await manager.UpdateItemAsync(id, request, Request.Headers.Authorization.ToString(), ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await manager.DeleteItemAsync(id, ct);
        return NoContent();
    }
}
