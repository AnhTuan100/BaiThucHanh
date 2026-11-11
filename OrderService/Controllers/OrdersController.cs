using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.DTOs;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("orders")]
public sealed class OrdersController(OrderDbContext db, OrderManager manager) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await db.Orders.AsNoTracking().Include(x => x.Items).OrderBy(x => x.Id).ToListAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(await manager.FindAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken ct)
    {
        var order = await manager.CreateAsync(request, Request.Headers.Authorization.ToString(), ct);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateStatusRequest request, CancellationToken ct) =>
        Ok(await manager.UpdateStatusAsync(id, request.Status, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        db.Orders.Remove(await manager.FindAsync(id, ct));
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
