using System.ComponentModel.DataAnnotations;

namespace Orders.Bll.Dtos;

public class CreateCustomerDto
{
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [Required, StringLength(200)] public string FullName { get; set; } = "";
    [StringLength(30)] public string? Phone { get; set; }
}

public class UpdateCustomerDto : CreateCustomerDto;

public record CustomerDto(long Id, string Email, string FullName, string? Phone, DateTime CreatedAt);