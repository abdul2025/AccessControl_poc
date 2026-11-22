using System.Globalization;
using AccessControl.Application;
using AccessControl.Infrastructure;
using AccessControl.Web.Configuration;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;

var builder = WebApplication.CreateBuilder(args);

// Every state-changing form post requires an anti-forgery token (CORE: web, data and integrations).
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.Configure<RazorViewEngineOptions>(options =>
    options.ViewLocationExpanders.Add(new FeatureViewLocationExpander()));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Pin the culture so number/date binding and display don't depend on the host's regional settings.
var appCulture = new CultureInfo("en-US");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(appCulture),
    SupportedCultures = [appCulture],
    SupportedUICultures = [appCulture],
});

app.UseRouting();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();

app.Run();
