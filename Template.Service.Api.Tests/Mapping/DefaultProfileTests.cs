using AutoMapper;
using Template.Service.Mapper;
using Xunit;

namespace Template.Service.Api.Tests.Mapping;

public sealed class DefaultProfileTests
{
    [Fact]
    public void Configuration_IsValidForAllTemplateContracts()
    {
        var configuration = new MapperConfiguration(options =>
            options.AddProfile<DefaultProfile>());

        configuration.AssertConfigurationIsValid();
    }
}

