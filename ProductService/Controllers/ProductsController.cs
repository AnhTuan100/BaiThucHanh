using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductService.Data;

namespace ProductService.Controllers;

public sealed class ProductRequest : IValidatableObject
{
    [Required, StringLength(255)]
    public string Name { get; set; } = "";
    [Range(typeof(decimal), "0", "99999999.99")]
    public decimal Price { get; set; }
    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (decimal.Round(Price, 2) != Price)
            yield return new ValidationResult("Gia chi duoc co toi da hai chu so thap phan.", [nameof(Price)]);
    }
}

[ApiController]
[Route("products")]
public sealed class ProductsController(ProductDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await db.Products.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var product = await db.Products.FindAsync([id], ct);
        return product is null ? NotFound(new { error = "Khong tim thay san pham." }) : Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> Create(ProductRequest request, CancellationToken ct)
    {
        var product = new Product { Name = request.Name.Trim(), Price = request.Price, Quantity = request.Quantity };
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ProductRequest request, CancellationToken ct)
    {
        var product = await db.Products.FindAsync([id], ct);
        if (product is null) return NotFound(new { error = "Khong tim thay san pham." });
        product.Name = request.Name.Trim();
        product.Price = request.Price;
        product.Quantity = request.Quantity;
        await db.SaveChangesAsync(ct);
        return Ok(product);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var product = await db.Products.FindAsync([id], ct);
        if (product is null) return NotFound(new { error = "Khong tim thay san pham." });
        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
