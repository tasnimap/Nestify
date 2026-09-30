using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Nestify.Api.Admin;
using Nestify.Api.Assistant;
using Nestify.Api.Auth;
using Nestify.Api.Data;
using Nestify.Api.Helpers;
using Nestify.Api.Homes;
using Nestify.Api.Housing;
using Nestify.Api.Marketplace;
using Nestify.Api.Notifications;
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

string ConfigOrEnv(string configurationKey, string environmentKey) =>
    builder.Configuration[configurationKey]
    ?? Environment.GetEnvironmentVariable(environmentKey)
    ?? throw new InvalidOperationException(
        $"Missing configuration '{configurationKey}' (or environment variable '{environmentKey}').");

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? $"Host={Env("DB_HOST")};Port={Env("DB_PORT")};Database={Env("DB_NAME")};" +
       $"Username={Env("DB_USER")};Password={Env("DB_PASSWORD")};" +
       $"SSL Mode={Environment.GetEnvironmentVariable("DB_SSL_MODE") ?? "Prefer"};" +
       "Trust Server Certificate=true;Include Error Detail=true";

// Neon supplies a PostgreSQL URI, while local development commonly uses the
// semicolon-delimited Npgsql format. Normalize either form before opening connections.
NpgsqlConnectionStringBuilder BuildNpgsqlConnectionString(string value)
{
    if (Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme.Equals("postgresql", StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals("postgres", StringComparison.OrdinalIgnoreCase)))
    {
        var userInfo = uri.UserInfo.Split(':', 2);
        var parsed = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.Trim('/'),
            SslMode = SslMode.Require
        };

        if (userInfo.Length > 0)
        {
            parsed.Username = Uri.UnescapeDataString(userInfo[0]);
        }

        if (userInfo.Length > 1)
        {
            parsed.Password = Uri.UnescapeDataString(userInfo[1]);
        }

        return parsed;
    }

    var npgsqlBuilder = new NpgsqlConnectionStringBuilder(value)
    {
        SslMode = SslMode.Require
    };

    return npgsqlBuilder;
}

var npgsqlConnection = BuildNpgsqlConnectionString(connectionString);

// UPLOAD_PICTURE is the unsigned upload preset the pictures are sent with.
var cloudinarySettings = CloudinarySettings.Parse(
    Env("CLOUDINARY_URL"),
    Environment.GetEnvironmentVariable("UPLOAD_PICTURE"));

var jwtSettings = new JwtSettings
{
    Issuer = ConfigOrEnv("Jwt:Issuer", "JWT_ISSUER"),
    Audience = ConfigOrEnv("Jwt:Audience", "JWT_AUDIENCE"),
    SigningKey = ConfigOrEnv("Jwt:Secret", "JWT_SECRET"),
    AccessTokenMinutes = int.TryParse(
        builder.Configuration["Jwt:AccessTokenMinutes"] ?? Environment.GetEnvironmentVariable("JWT_ACCESS_MINUTES"),
        out var m) ? m : 120,
    RefreshTokenDays = int.TryParse(
        builder.Configuration["Jwt:RefreshTokenDays"] ?? Environment.GetEnvironmentVariable("JWT_REFRESH_DAYS"),
        out var d) ? d : 7
};

// snake_case columns map onto PascalCase row properties without an alias on every column.
Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
Dapper.SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

// ---- Services ----
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton(new DbConnectionFactory(npgsqlConnection.ConnectionString));
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<HelperService>();
builder.Services.AddScoped<HelperWorkspaceService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<UserProfileService>();
builder.Services.AddScoped<VerificationService>();
builder.Services.AddScoped<HomeService>();
builder.Services.AddScoped<HousingService>();
builder.Services.AddScoped<SettlementService>();
builder.Services.AddScoped<MarketplaceService>();
builder.Services.AddScoped<AdminConsoleService>();
builder.Services.AddSingleton(cloudinarySettings);
builder.Services.AddHttpClient<CloudinaryUploader>();
builder.Services.AddHttpClient<GeminiAssistantService>(client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("assistant-chat", context =>
    {
        var userId = context.User.FindFirst("sub")?.Value
                     ?? context.Connection.RemoteIpAddress?.ToString()
                     ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 12,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(ClientCorsPolicy, policy =>
    {
        policy.AllowAnyOrigin()
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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<SettlementService>().EnsureSchemaCompatibilityAsync();
}

app.UseCors(ClientCorsPolicy);
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapControllers();
app.Run();
