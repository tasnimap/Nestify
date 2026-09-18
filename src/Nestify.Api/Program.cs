using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Nestify.Api.Admin;
using Nestify.Api.Auth;
using Nestify.Api.Data;
using Nestify.Api.Helpers;
using Nestify.Api.Homes;
using Nestify.Api.Housing;
using Nestify.Api.Marketplace;
using Nestify.Api.Profiles;
using Nestify.Api.Settlement;

// Load secrets from a .env file at (or above) the working directory.
DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

const string ClientCorsPolicy = "NestifyClient";

// ---- Configuration from environment ----
string Env(string key) =>
    Environment.GetEnvironmentVariable(key)
    ?? throw new InvalidOperationException($"Missing environment variable '{key}'. Add it to your .env file.");

var connectionString =
    $"Host={Env("DB_HOST")};Port={Env("DB_PORT")};Database={Env("DB_NAME")};" +
    $"Username={Env("DB_USER")};Password={Env("DB_PASSWORD")};Include Error Detail=true";

// UPLOAD_PICTURE is the unsigned upload preset the pictures are sent with.
var cloudinarySettings = CloudinarySettings.Parse(
    Env("CLOUDINARY_URL"),
    Environment.GetEnvironmentVariable("UPLOAD_PICTURE"));

var jwtSettings = new JwtSettings
{
    Issuer = Env("JWT_ISSUER"),
    Audience = Env("JWT_AUDIENCE"),
    SigningKey = Env("JWT_SECRET"),
    AccessTokenMinutes = int.TryParse(Environment.GetEnvironmentVariable("JWT_ACCESS_MINUTES"), out var m) ? m : 120,
    RefreshTokenDays = int.TryParse(Environment.GetEnvironmentVariable("JWT_REFRESH_DAYS"), out var d) ? d : 7
};

// snake_case columns map onto PascalCase row properties without an alias on every column.
Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
Dapper.SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

// ---- Services ----
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton(new DbConnectionFactory(connectionString));
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<HelperService>();
builder.Services.AddScoped<HelperWorkspaceService>();
builder.Services.AddScoped<UserProfileService>();
builder.Services.AddScoped<VerificationService>();
builder.Services.AddScoped<HomeService>();
builder.Services.AddScoped<HousingService>();
builder.Services.AddScoped<SettlementService>();
builder.Services.AddScoped<MarketplaceService>();
builder.Services.AddScoped<AdminConsoleService>();
builder.Services.AddSingleton(cloudinarySettings);
builder.Services.AddHttpClient<CloudinaryUploader>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(ClientCorsPolicy, policy =>
    {
        policy.WithOrigins("https://localhost:7205", "http://localhost:5290")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<SettlementService>().EnsureSchemaCompatibilityAsync();
}

app.UseCors(ClientCorsPolicy);
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
