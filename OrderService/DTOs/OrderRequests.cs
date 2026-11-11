using System.ComponentModel.DataAnnotations;

namespace OrderService.DTOs;

public sealed class CreateOrderRequest
{
    [Required, StringLength(255)]
    public string CustomerName { get; set; } = "";
    [Required, EmailAddress, StringLength(255)]
    public string CustomerEmail { get; set; } = "";
    [Required, MinLength(1), MaxLength(100)]
    public List<ItemRequest> Items { get; set; } = [];
}

public class ItemRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}

public sealed class CreateItemRequest : ItemRequest
{
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }
}

public sealed class UpdateStatusRequest
{
    [Required, RegularExpression("^(pending|completed|cancelled)$")]
    public string Status { get; set; } = "";
}
