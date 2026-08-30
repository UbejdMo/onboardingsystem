using MerchantOnboarding.Api.Data;
using MerchantOnboarding.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

// Pinned rather than auto-detected: AutoDetect opens a connection at startup,
// which would make the app unable to boot without a running database.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36))));

// Stateless rules, so a single shared instance is fine.
builder.Services.AddSingleton<IMerchantService, MerchantService>();

// The React dev server runs on its own origin, so the browser blocks its
// calls unless the API opts in. Restricted to that one origin rather than
// a wildcard - this API will eventually carry real merchant data.
const string FrontendCorsPolicy = "FrontendDevServer";
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

// Apply any pending migrations at startup so a fresh container comes up with
// a usable schema. Fine for a demo; a production deployment would run
// migrations as a separate, deliberate step rather than on every boot.
if (app.Configuration.GetValue<bool>("ApplyMigrationsAtStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Must come before the endpoints it applies to.
app.UseCors(FrontendCorsPolicy);

app.UseAuthorization();

app.MapControllers();

app.Run();
