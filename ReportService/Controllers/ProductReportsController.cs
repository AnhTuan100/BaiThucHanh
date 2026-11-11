using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReportService.Data;
using ReportService.DTOs;
using ReportService.Services;

namespace ReportService.Controllers;

[ApiController]
[Route("reports/products")]
public sealed class ProductReportsController(ReportDbContext db, ReportManager manager) : ControllerBase
{
    // GET /reports/products
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await db.ProductReports.AsNoTracking()
                                  .OrderBy(x => x.Id)
                                  .ToListAsync(ct));

    // GET /reports/products/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Ok(await manager.FindProductReportAsync(id, ct));

    // POST /reports/products
    [HttpPost]
    public async Task<IActionResult> Create(CreateProductReportRequest request, CancellationToken ct)
    {
        var report = await manager.CreateProductReportAsync(
            request, Request.Headers.Authorization.ToString(), ct);
        return CreatedAtAction(nameof(Get), new { id = report.Id }, report);
    }

    // DELETE /reports/products/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        db.ProductReports.Remove(await manager.FindProductReportAsync(id, ct));
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
