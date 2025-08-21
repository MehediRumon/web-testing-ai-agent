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

// Register bug tracking services
builder.Services.AddSingleton<IBugStorageService, BugStorageService>();
builder.Services.AddScoped<IBugService, BugService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IBugAuthorizationService, BugAuthorizationService>();
builder.Services.AddScoped<IBugValidationService, BugValidationService>();
builder.Services.AddScoped<IBugImageService, BugImageService>();

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
