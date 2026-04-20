using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services
    .AddControllersWithViews()
    .AddViewLocalization()              
    .AddDataAnnotationsLocalization(); 

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
  .AddCookie(options =>
  {
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
  });

builder.Services.AddHttpClient();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<MVC.Services.IUnsplashService, MVC.Services.UnsplashService>();
builder.Services.AddScoped<MVC.Services.AuthApiService>();
// Removed LocalizationService singleton as we are using .NET standard localization

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
  app.UseExceptionHandler("/Home/Error");
  app.UseHsts();
  app.UseHttpsRedirection();
}

var supportedCultures = new[] { "en", "hi", "gu" };

var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

localizationOptions.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());


app.UseRequestLocalization(localizationOptions);
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapControllerRoute(
  name: "default",
  pattern: "{controller=Home}/{action=Index}/{id?}"
  );

app.Run();