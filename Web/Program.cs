using Application;
using Infrastructure;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add support for Docker secrets
builder.Configuration.AddKeyPerFile("/run/secrets", optional: true);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    var policy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                     .RequireAuthenticatedUser()
                     .Build();
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter(policy));
});

// Register Options
builder.Services.Configure<Application.Options.ProcessingLimitsOptions>(
    builder.Configuration.GetSection("ProcessingLimits"));

// Register Application (Use Cases)
builder.Services.AddApplication();

// Register Infrastructure (AppDbContext, HttpClients, etc.)
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Seed the SQLite development database on startup
if (app.Environment.IsDevelopment())
{
    await Infrastructure.DB.DbSeederd.SeedAsync(app.Services);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);
app.UseStaticFiles();
app.MapStaticAssets();

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{port}");

app.Run();
