// Program.cs
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
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
builder.Services.AddScoped(sp => new AuthorizationMessageHandler(
    sp.GetRequiredService<ILocalStorageService>(),
    sp.GetRequiredService<CustomAuthStateProvider>(),
    sp.GetRequiredService<NavigationManager>(),
    apiBaseUrl));
builder.Services.AddScoped(sp =>
{
    var handler = sp.GetRequiredService<AuthorizationMessageHandler>();
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = new Uri(apiBaseUrl) };
});

builder.Services.AddScoped<IAuthService, AuthService>();

// Extra profile details (picture, occupation, address, socials)
builder.Services.AddScoped<IUserProfileService, UserProfileService>();

// Dev-only mock "who's logged in" state — must be Singleton so it survives page navigation
builder.Services.AddSingleton<ICurrentUserService, MockCurrentUserService>();

// M1 · Area cascade — served from the seeded administrative tables
builder.Services.AddScoped<IAreaService, AreaService>();
// M1/M2 - Housing posts and bookings, backed by api/v1/housing (Housing.sql).
// The same client also answers the house selector on /housing/new.
builder.Services.AddScoped<IHousingService, HousingService>();
builder.Services.AddScoped<IHouseLookupService, HousingService>();

// M3 - Home module, backed by api/v1/homes (User_Home.sql).
builder.Services.AddScoped<IHomeService, HomeService>();


builder.Services.AddScoped<IHelperService, HelperService>();

// M4 - Second-hand marketplace, backed by api/v1/marketplace (Marketplace.sql).
builder.Services.AddScoped<IMarketplaceService, MarketplaceService>();

// M3 - Monthly settlement book, backed by api/v1/settlement (Settlement.sql).
builder.Services.AddScoped<ISettlementService, SettlementService>();

// Admin console — moderation, fees and plans, admin accounts and the audit
// log, backed by api/v1/admin (Admin.sql).
builder.Services.AddScoped<IAdminService, AdminService>();

// Verification queue and dashboard charts are still sample data, kept as a
// Singleton so decisions survive navigating between the admin pages.
builder.Services.AddSingleton<Nestify.Web.Admin.AdminConsoleService>();

// The admin profile page reads the signed-in admin's own users row.
builder.Services.AddScoped<Nestify.Web.Admin.AdminProfileClient>();

// Helper (maid) workspace — sample availability, schedule, requests and reviews
// kept as a Singleton so edits survive navigating between the helper pages.
builder.Services.AddSingleton<Nestify.Web.Maid.MaidWorkspaceService>();

// The helper profile page reads the signed-in helper's own users row.
builder.Services.AddScoped<Nestify.Web.Maid.MaidAccountClient>();

// Register utility services
builder.Services.AddScoped<MoneyFormatterService>();
builder.Services.AddScoped<DateFormatterService>();
builder.Services.AddScoped<ToastService>();

await builder.Build().RunAsync();
