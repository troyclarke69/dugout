using System.Text.Json.Serialization;
using Dugout.Api.Endpoints;
using Dugout.Api.Middleware;
using Dugout.Api.Services;
using Dugout.Storage;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict;
    options.SerializerOptions.PropertyNameCaseInsensitive = false;
    options.SerializerOptions.AllowDuplicateProperties = false;
});
builder.Services.AddSingleton<IBenchmarkRepository>(_ => new SqlBenchmarkRepository(
    ApiConnectionStrings.GetResultsConnectionString(builder.Configuration)));
builder.Services.AddSingleton<IDugoutApiService, DugoutApiService>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapOpenApi();
app.MapExperimentEndpoints();
app.MapRunEndpoints();
app.MapHistoricalEndpoints();
app.Run();

public partial class Program;
