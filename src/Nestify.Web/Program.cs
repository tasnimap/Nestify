// Program.cs
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Nestify.Web;
using Nestify.Web.Auth;
using Nestify.Web.Services;
using Nestify.Web.Services.Interfaces;
using Nestify.Web.Services.Implementations;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]!;

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(apiBaseUrl)
});

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CustomAuthStateProvider>());
builder.Services.AddScoped<IAuthService, AuthService>();

// M1 · Area cascade — swap MockAreaService for AreaService when the API lands
builder.Services.AddScoped<IAreaService, MockAreaService>();
builder.Services.AddScoped<IHousingService, MockHousingService>();

// M4 · Second-hand marketplace — swap MockMarketplaceService for MarketplaceService when the API lands
builder.Services.AddScoped<IMarketplaceService, MockMarketplaceService>();

// Register utility services
builder.Services.AddScoped<MoneyFormatterService>();
builder.Services.AddScoped<DateFormatterService>();
builder.Services.AddScoped<ToastService>();

await builder.Build().RunAsync();