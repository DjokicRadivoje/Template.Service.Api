using AutoMapper;
using Template.Service.BusinessModel.Crm;

namespace Template.Service.Mapper;

public sealed class DefaultProfile : Profile
{
    public DefaultProfile()
    {
        CreateMap<CrmRequest, DataModel.Crm.CrmRequest>();
        CreateMap<DataModel.Crm.CrmResponse, CrmResponse>();
    }
}

