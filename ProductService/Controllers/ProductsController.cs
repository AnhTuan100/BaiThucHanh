using Microsoft.AspNetCore.Mvc;
using ProductService.DTOs;
using ProductService.Services;

namespace ProductService.Controllers;

/// <summary>
/// Controller quản lý sản phẩm - Bài Thực Hành Số 3
/// Tất cả các endpoint yêu cầu xác thực JWT từ dịch vụ BaiThucHanh2 (cổng 5194).
/// </summary>
[ApiController]
[Route("products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    // GET /products
    /// <summary>Lấy danh sách tất cả sản phẩm</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var products = await _productService.GetAllAsync();
        return Ok(new
        {
            Success = true,
            Data    = products
        });
    }

    // GET /products/{id}
    /// <summary>Lấy thông tin chi tiết một sản phẩm theo ID</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product == null)
            return NotFound(new { Success = false, Message = $"Không tìm thấy sản phẩm với ID = {id}" });

        return Ok(new { Success = true, Data = product });
    }

    // POST /products
    /// <summary>Thêm một sản phẩm mới</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { Success = false, Message = "Dữ liệu không hợp lệ", Errors = ModelState });

        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { Success = false, Message = "Tên sản phẩm không được để trống" });

        if (dto.Price < 0)
            return BadRequest(new { Success = false, Message = "Giá sản phẩm không được âm" });

        if (dto.Quantity < 0)
            return BadRequest(new { Success = false, Message = "Số lượng sản phẩm không được âm" });

        var created = await _productService.CreateAsync(dto);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new
        {
            Success = true,
            Message = "Tạo sản phẩm thành công",
            Data    = created
        });
    }

    // PUT /products/{id}
    /// <summary>Cập nhật thông tin sản phẩm</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { Success = false, Message = "Dữ liệu không hợp lệ", Errors = ModelState });

        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest(new { Success = false, Message = "Tên sản phẩm không được để trống" });

        if (dto.Price < 0)
            return BadRequest(new { Success = false, Message = "Giá sản phẩm không được âm" });

        if (dto.Quantity < 0)
            return BadRequest(new { Success = false, Message = "Số lượng sản phẩm không được âm" });

        var updated = await _productService.UpdateAsync(id, dto);
        if (updated == null)
            return NotFound(new { Success = false, Message = $"Không tìm thấy sản phẩm với ID = {id}" });

        return Ok(new
        {
            Success = true,
            Message = "Cập nhật sản phẩm thành công",
            Data    = updated
        });
    }

    // DELETE /products/{id}
    /// <summary>Xóa một sản phẩm theo ID</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _productService.DeleteAsync(id);
        if (!deleted)
            return NotFound(new { Success = false, Message = $"Không tìm thấy sản phẩm với ID = {id}" });

        return Ok(new { Success = true, Message = $"Xóa sản phẩm ID = {id} thành công" });
    }
}
