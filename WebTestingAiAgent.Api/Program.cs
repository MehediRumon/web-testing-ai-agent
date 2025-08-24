using WebTestingAiAgent.Core.Interfaces;
using WebTestingAiAgent.Core.Models;
using WebTestingAiAgent.Api.Services;
using WebTestingAiAgent.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Entity Framework with SQLite
builder.Services.AddDbContext<WebTestingDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? "Data Source=webtesting.db"));

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

// Register essential infrastructure services (needed by bug tracking)
builder.Services.AddScoped<IStorageService, StorageService>();

// Register bug tracking services (keeping for backward compatibility)
builder.Services.AddSingleton<IBugStorageService, BugStorageService>();
builder.Services.AddScoped<IBugService, BugService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IBugAuthorizationService, BugAuthorizationService>();
builder.Services.AddScoped<IBugValidationService, BugValidationService>();
builder.Services.AddScoped<IBugImageService, BugImageService>();

// Register new test case recording and execution services
builder.Services.AddScoped<ITestCaseService, TestCaseService>();
builder.Services.AddSingleton<IBrowserAutomationService, BrowserAutomationService>();
builder.Services.AddSingleton<IRecordingService, RecordingService>();
builder.Services.AddScoped<ITestExecutionService, TestExecutionService>();

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<WebTestingDbContext>();
    context.Database.EnsureCreated();
}

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
