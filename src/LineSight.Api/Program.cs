using System.Text.Json.Serialization;
using LineSight.Core.Traceability;
using LineSight.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    // Enums travel as strings ("Running", "Scrap") - readable in Swagger and in logs.
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();   // consistent RFC 7807 error bodies, including unhandled exceptions

var connectionString = builder.Configuration.GetConnectionString("LineSight")
    ?? throw new InvalidOperationException("Connection string 'LineSight' is missing from configuration.");

builder.Services.AddLineSightData(connectionString);       // SQL Server repositories (stored procedures)
builder.Services.AddScoped<TraceabilityService>();         // routing rules from LineSight.Core
builder.Services.AddSingleton(TimeProvider.System);        // injectable clock -> deterministic tests

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

/// <summary>Exposed so the test project can host the API in-memory with WebApplicationFactory.</summary>
public partial class Program;
