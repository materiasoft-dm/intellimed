using System.ComponentModel.DataAnnotations;
using IntelliMed.Core.Entities;

namespace IntelliMed.Core.DTOs;

public class AddressBookContactDto
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? FirstName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Profession { get; set; }
    public string? Speciality { get; set; }
    public string? ProviderNumber { get; set; }
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public string? Fax { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Suburb { get; set; }
    public string? Postcode { get; set; }
    public string? State { get; set; }
    public int? ClinicId { get; set; }
    public string? ClinicName { get; set; }
    public ClinicType? ClinicType { get; set; }
    public string? Notes { get; set; }
    public bool IsArchived { get; set; }
}

/// <summary>Create and update share one shape — every field is user-editable.</summary>
public class SaveAddressBookContactDto
{
    [MaxLength(20)] public string? Title { get; set; }
    [MaxLength(100)] public string? FirstName { get; set; }
    [Required, MaxLength(200)] public string LastName { get; set; } = string.Empty;
    [MaxLength(100)] public string? Profession { get; set; }
    [MaxLength(100)] public string? Speciality { get; set; }
    [MaxLength(20)] public string? ProviderNumber { get; set; }
    [MaxLength(50)] public string? Phone { get; set; }
    [MaxLength(50)] public string? Mobile { get; set; }
    [MaxLength(50)] public string? Fax { get; set; }
    [MaxLength(200)] public string? Email { get; set; }
    [MaxLength(200)] public string? Address { get; set; }
    [MaxLength(100)] public string? Suburb { get; set; }
    [MaxLength(10)] public string? Postcode { get; set; }
    [MaxLength(10)] public string? State { get; set; }
    public int? ClinicId { get; set; }
    public string? Notes { get; set; }
}

public class AddressBookSearchDto
{
    /// <summary>Matches name, provider number, profession, speciality or workplace.</summary>
    public string? Query { get; set; }
    public bool IncludeArchived { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
