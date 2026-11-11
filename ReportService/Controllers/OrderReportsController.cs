using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportService.Data;
using ReportService.DTOs;
using ReportService.Services;

namespace ReportService.Controllers;

[ApiController]
[Route("reports/orders")]
public sealed class OrderReportsController(ReportDbContext db, ReportManager manager) : ControllerBase
{
    // GET /reports/orders
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await db.OrderReports.AsNoTracking()
                                .Include(x => x.ProductReports)
                                .OrderBy(x => x.Id)
                                .ToListAsync(ct));

    // GET /reports/orders/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(await manager.FindOrderReportAsync(id, ct));

    // POST /reports/orders
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderReportRequest request, CancellationToken ct)
    {
        var report = await manager.CreateOrderReportAsync(
            request, Request.Headers.Authorization.ToString(), ct);
        return CreatedAtAction(nameof(Get), new { id = report.Id }, report);
    }

    // DELETE /reports/orders/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        db.OrderReports.Remove(await manager.FindOrderReportAsync(id, ct));
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
