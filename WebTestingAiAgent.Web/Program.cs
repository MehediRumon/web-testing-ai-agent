using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using WebTestingAiAgent.Web;
using WebTestingAiAgent.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient for API communication
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5146/") });

// Register custom services
builder.Services.AddScoped<IUserContextService, UserContextService>();

await builder.Build().RunAsync();
