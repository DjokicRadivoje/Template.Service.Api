using Serilog;
using Template.Service.Mapper;
using Autofac;
using Template.Service.DependencyInjection;
using Autofac.Extensions.DependencyInjection;
using Framework.Logger.Correlation;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.OpenApi.Models;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using Template.Service.Api.Infrastructure.Authentication;
using Template.Service.Api.Infrastructure.Http;
using Template.Service.Api.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// Konfigurisemo Serilog za logovanje u fajl citanjem konfiguracije iz appsettings.json
// Log u fajl, kreira novi fajl svaki dan
// Menjamo podrazumevani logger sa Serilogom (da bi logove cuvali u fajlu).
builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.ApplicationInsights(
            services.GetRequiredService<TelemetryConfiguration>(),
            TelemetryConverter.Traces));


//Dodajemo automapper
builder.Services.AddAutoMapper(typeof(DefaultProfile));

// Add services to the container.
builder.Services.AddSingleton<IResponseHttpStatusCodeResolver, ResponseHttpStatusCodeResolver>();
builder.Services.AddControllers(options =>
    options.Filters.Add<ResponseHttpStatusFilter>());
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddSingleton<ITelemetryInitializer, CorrelationIdTelemetryInitializer>();
builder.Services.AddCorrelationId();
//builder.Services.AddKeycloakAuthentication(builder.Configuration);
var allowInsecureCrm = builder.Environment.IsDevelopment()
    || builder.Environment.IsEnvironment("UAT");
DependencyInjectionConfig.ConfigureHttpClients(
    builder.Services,
    builder.Configuration,
    allowInsecureCrm);

builder.Services.AddHealthChecks();

builder.Services.AddSwaggerGen(options =>
{
    var xmlDocumentationFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(
        AppContext.BaseDirectory,
        xmlDocumentationFile));

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a Keycloak access token."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        }] = Array.Empty<string>()
    });
});

//chose Autofac
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
//todo: 001 Check if this is necessary
builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
{
    DependencyInjectionConfig.ConfigureContainer(containerBuilder);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() ||
    app.Environment.IsEnvironment("UAT"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCorrelationId();
app.UseApplicationInsightsCorrelationId();

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    service = "Service.Api.Template",
    status = "running",
    environment = app.Environment.EnvironmentName
}))
.AllowAnonymous();

app.MapHealthChecks("/")
   .AllowAnonymous();

app.MapControllers();

app.Run();

public partial class Program
{
}

