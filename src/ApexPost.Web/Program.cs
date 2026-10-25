using ApexPost.Application;
using ApexPost.Infrastructure;
using ApexPost.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCounterSaleConfiguration(builder.Configuration);
builder.Services.AddWebSecurity(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ApexPost.Web.Middleware.CorrelationIdMiddleware>();
app.UseMiddleware<ApexPost.Web.Middleware.RequestTimingMiddleware>();
app.UseMiddleware<ApexPost.Web.Middleware.SafeExceptionMiddleware>();
app.UseMiddleware<ApexPost.Web.Middleware.SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors();
app.UseRateLimiter();
app.UseMiddleware<ApexPost.Web.Middleware.MaintenanceModeMiddleware>();
app.UseMiddleware<ApexPost.Web.Middleware.EndpointProtectionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

public partial class Program { }
