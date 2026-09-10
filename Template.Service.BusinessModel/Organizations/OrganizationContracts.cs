namespace Template.Service.BusinessModel.Organizations;

public sealed class OrganizationSearchRequest
{
    public string? Name { get; set; }

    public string? RegistrationNumber { get; set; }

    public string? Country { get; set; }

    public string? City { get; set; }

    public string? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

public sealed class OrganizationResponse
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string RegistrationNumber { get; set; } = string.Empty;

    public string TaxNumber { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}

