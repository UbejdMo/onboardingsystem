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

app.UseAuthorization();

app.MapControllers();

app.Run();
