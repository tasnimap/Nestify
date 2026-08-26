var builder = WebApplication.CreateBuilder(args);

const string ClientCorsPolicy = "NestifyClient";

builder.Services.AddCors(options =>
{
    options.AddPolicy(ClientCorsPolicy, policy =>
    {
        policy.WithOrigins("https://localhost:7100") // Blazor WASM dev URL — check your launchSettings.json
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
// ... EF Core / PostgreSQL, Identity, JWT, etc.

var app = builder.Build();

app.UseCors(ClientCorsPolicy);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();