using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Infrastructure;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Web.Configuration;
using SistemaFinanciero.Web.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSystemAuthorization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    CultureInfo culture = CultureInfo.GetCultureInfo("es-CR");
    options.DefaultRequestCulture = new RequestCulture(culture);
    options.SupportedCultures = [culture];
    options.SupportedUICultures = [culture];
});
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-SistemaFinanciero.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
});
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

var app = builder.Build();

if (Array.Exists(
        args,
        argument => string.Equals(
            argument,
            "--bootstrap-admin",
            StringComparison.OrdinalIgnoreCase)))
{
    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
    IdentityBootstrapper bootstrapper = scope.ServiceProvider
        .GetRequiredService<IdentityBootstrapper>();
    await bootstrapper.ExecuteAsync(app.Configuration);
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseRequestLocalization();
app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseMiddleware<PasswordChangeRequirementMiddleware>();
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

/// <summary>
/// Punto de entrada expuesto para las pruebas de integración de ASP.NET Core.
/// </summary>
public partial class Program;
