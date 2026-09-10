namespace Template.Service.DataModel.Crm;

public abstract class CrmApiResponse
{
    public bool Ok { get; set; }

    public string? Error { get; set; }
}

