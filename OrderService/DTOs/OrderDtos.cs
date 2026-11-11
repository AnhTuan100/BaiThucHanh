using System.ComponentModel.DataAnnotations;

namespace OrderService.DTOs;

public class CreateOrderDto
{
    [Required, StringLength(255)]
    public string CustomerName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(255)]
    public string CustomerEmail { get; set; } = string.Empty;
    [Required, MinLength(1)]
    public List<OrderItemInput> Items { get; set; } = [];
}

public class OrderItemInput
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}

public class CreateOrderItemDto : OrderItemInput
{
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }
}

public class UpdateOrderStatusDto
{
    [Required, RegularExpression("^(pending|completed|cancelled)$")]
    public string Status { get; set; } = string.Empty;
}
