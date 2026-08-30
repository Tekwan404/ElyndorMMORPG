using System.Text;
using Elyndor.Contracts.System;
using Elyndor.Core.Content;
using Elyndor.Infrastructure.Content;
using Elyndor.Infrastructure.Identity.Telegram;
using Elyndor.Server.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string contentPackagePath = builder.Configuration["Content:PackagePath"]
    ?? Path.Combine(AppContext.BaseDirectory, "content", "package.json");
GameContentPackage gameContentPackage =
    await GameContentPackageLoader.LoadAsync(contentPackagePath);

string frontendDistPath = Path.GetFullPath(
    builder.Configuration["Frontend:DistPath"]
        ?? Path.Combine(
            builder.Environment.ContentRootPath,
            "..",
            "..",
            "web",
            "elyndor-web",
            "dist"));
PhysicalFileProvider? frontendFileProvider = File.Exists(
    Path.Combine(frontendDistPath, "index.html"))
        ? new PhysicalFileProvider(frontendDistPath)
        : null;

builder.AddServiceDefaults();
builder.AddElyndorInfrastructure();

builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(gameContentPackage);
builder.Services.AddSingleton<TelegramInitDataValidator>();
builder.Services.AddSingleton<JwtTokenIssuer>();
builder.Services.AddOptions<AuthenticationOptions>()
    .BindConfiguration(AuthenticationOptions.SectionName)
    .Validate(
        options => options.IsValid(),
        "Authentication requires issuer, audience, a 32-byte signing key, Telegram Bot Token, valid time limits, and a positive enabled development identity.")
    .ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<AuthenticationOptions>, TimeProvider>(
        (jwtOptions, configuredOptions, timeProvider) =>
        {
            AuthenticationOptions options = configuredOptions.Value;
            jwtOptions.MapInboundClaims = false;
            jwtOptions.SaveToken = false;
            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = options.Issuer,
                ValidateAudience = true,
                ValidAudience = options.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(options.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(
                    AuthenticationOptions.TokenValidationClockSkewSeconds),
                LifetimeValidator = (notBefore, expires, _, parameters) =>
                    ValidateTokenLifetime(notBefore, expires, timeProvider, parameters.ClockSkew)
            };
        });
builder.Services.AddAuthorization();

WebApplication app = builder.Build();

if (frontendFileProvider is not null)
{
    app.Lifetime.ApplicationStopped.Register(frontendFileProvider.Dispose);
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = frontendFileProvider
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = frontendFileProvider
    });
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

bool mapDevelopmentAuthentication = app.Environment.IsDevelopment()
    && app.Configuration.GetValue<bool>("Authentication:Development:Enabled");
app.MapAuthenticationEndpoints(mapDevelopmentAuthentication);

app.MapGet(
        "/api/v1/status",
        (TimeProvider timeProvider) => new ApiStatusResponse(
            "Elyndor.Server",
            "ready",
            timeProvider.GetUtcNow()))
    .WithName("GetApiStatus")
    .WithTags("System");

app.MapDefaultEndpoints();
app.Map("/api/{**path}", () => Results.NotFound());
app.Map("/hubs/{**path}", () => Results.NotFound());

if (frontendFileProvider is not null)
{
    app.MapFallbackToFile(
        "index.html",
        new StaticFileOptions
        {
            FileProvider = frontendFileProvider
        });
}

app.Run();

static bool ValidateTokenLifetime(
    DateTime? notBefore,
    DateTime? expires,
    TimeProvider timeProvider,
    TimeSpan clockSkew)
{
    DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;

    return expires.HasValue
        && expires.Value >= utcNow - clockSkew
        && (!notBefore.HasValue || notBefore.Value <= utcNow + clockSkew);
}

public partial class Program;
