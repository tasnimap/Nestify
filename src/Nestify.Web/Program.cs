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

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CustomAuthStateProvider>());

// HttpClient that automatically sends the stored bearer token on every API call.
builder.Services.AddScoped<AuthorizationMessageHandler>();
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthorizationMessageHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = new Uri(apiBaseUrl) };
});

builder.Services.AddScoped<IAuthService, AuthService>();

// Dev-only mock "who's logged in" state — must be Singleton so it survives page navigation
builder.Services.AddSingleton<ICurrentUserService, MockCurrentUserService>();

// M1 · Area cascade — served from the seeded administrative tables
builder.Services.AddScoped<IAreaService, AreaService>();
builder.Services.AddScoped<IHousingService, MockHousingService>();

// Delete this line + IHouseLookupService + MockHouseLookupService once that's on main.
builder.Services.AddScoped<IHouseLookupService, MockHouseLookupService>();

// M3 - Home module. Singleton so the in-memory home survives page navigation;
// swap MockHomeService for a real HomeService when the API lands.
builder.Services.AddSingleton<IHomeService, MockHomeService>();


builder.Services.AddScoped<IHelperService, HelperService>();

// M4 · Second-hand marketplace — swap MockMarketplaceService for MarketplaceService when the API lands
builder.Services.AddScoped<IMarketplaceService, MockMarketplaceService>();

// Admin console — mock data until the M5/M6 endpoints land
builder.Services.AddScoped<IAdminService, MockAdminService>();

// Register utility services
builder.Services.AddScoped<MoneyFormatterService>();
builder.Services.AddScoped<DateFormatterService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<SettlementWorkspaceService>();

await builder.Build().RunAsync();
