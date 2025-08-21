using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;
using WebTestingAiAgent.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorWasm",
        policy =>
        {
            policy.WithOrigins("https://localhost:7001", "https://localhost:5001", "http://localhost:5201")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// Register application services
builder.Services.AddSingleton<IRunManager, RunManagerService>();
builder.Services.AddScoped<IValidationService, ValidationService>();
builder.Services.AddSingleton<IPlannerService, PlannerService>();
builder.Services.AddSingleton<IExecutorService, ExecutorService>();
builder.Services.AddScoped<IAssertionService, AssertionService>();
builder.Services.AddScoped<IHealingService, HealingService>();
builder.Services.AddScoped<IReportingService, ReportingService>();
builder.Services.AddScoped<IIntegrationService, IntegrationService>();
builder.Services.AddScoped<IStorageService, StorageService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowBlazorWasm");
app.UseAuthorization();
app.MapControllers();

app.Run();

// Make the implicit Program class accessible to test projects
public partial class Program { }
