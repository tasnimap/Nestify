// Program.cs
using Blazored.LocalStorage;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using Nestify.Web;
using Nestify.Web.Auth;
using Nestify.Web.Services;
using Nestify.Web.Services.Interfaces;
using Nestify.Web.Services.Implementations;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"]!;
if (builder.HostEnvironment.IsDevelopment())
{
    apiBaseUrl = builder.HostEnvironment.BaseAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        ? "https://localhost:7284/"
        : "http://localhost:5293/";
}

builder.Services.AddBlazoredLocalStorage();
// The SDK embeds Resources/SharedResource*.resx as Nestify.Web.SharedResource;
// leave ResourcesPath unset so IStringLocalizer<SharedResource> uses that base name.
builder.Services.AddLocalization();
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
builder.Services.AddScoped<IAssistantService, AssistantService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

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

// M2 - Domestic help, backed by api/v1/helpers (Domestic_Help.sql).
builder.Services.AddScoped<IHelperService, HelperService>();

// M4 - Second-hand marketplace, backed by api/v1/marketplace (Marketplace.sql).
builder.Services.AddScoped<IMarketplaceService, MarketplaceService>();

// M3 - Monthly settlement book, backed by api/v1/settlement (Settlement.sql).
builder.Services.AddScoped<ISettlementService, SettlementService>();

// Admin console — moderation, fees and plans, admin accounts and the audit
// log, backed by api/v1/admin (Admin.sql).
builder.Services.AddScoped<IAdminService, AdminService>();

// Kept for shared admin display helpers. Its former sample dashboard data is
// commented out in AdminConsoleService; dashboard values now come from the API.
builder.Services.AddSingleton<Nestify.Web.Admin.AdminConsoleService>();

// The admin profile page reads the signed-in admin's own users row.
builder.Services.AddScoped<Nestify.Web.Admin.AdminProfileClient>();

// Register utility services
builder.Services.AddScoped<MoneyFormatterService>();
builder.Services.AddScoped<DateFormatterService>();
builder.Services.AddScoped<ToastService>();

var host = builder.Build();
var js = host.Services.GetRequiredService<IJSRuntime>();
var savedLanguage = await js.InvokeAsync<string?>("nestifyLanguage.get");
var culture = new CultureInfo(string.Equals(savedLanguage, "bn", StringComparison.OrdinalIgnoreCase) ? "bn-BD" : "en");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;
CultureInfo.CurrentCulture = culture;
CultureInfo.CurrentUICulture = culture;
await js.InvokeVoidAsync("nestifyLanguage.apply", culture.Name);

await host.RunAsync();
