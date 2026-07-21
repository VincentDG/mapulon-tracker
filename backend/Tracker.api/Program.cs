using Microsoft.EntityFrameworkCore;
using Tracker.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Scalar.AspNetCore;
using Tracker.Api.Services;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        // Robust CORS origin parsing (handles both single strings and arrays)
        var originsList = new List<string>();
        var originsSection = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
        
        if (originsSection != null)
        {
            originsList.AddRange(originsSection);
        }
        else
        {
            var singleOrigin = builder.Configuration["AllowedOrigins"];
            if (!string.IsNullOrEmpty(singleOrigin))
            {
                originsList.Add(singleOrigin);
            }
        }
        
        if (originsList.Count == 0)
        {
            originsList.Add("http://localhost:4200");
        }
        // Clean up: trim trailing slashes (e.g. "domain.com/" -> "domain.com")
        var cleanedOrigins = originsList.Select(o => o.TrimEnd('/')).ToArray();
        policy.WithOrigins(cleanedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.HttpOnly = true;
    
    if (builder.Environment.IsDevelopment())
    {
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    }
    else
    {
        // Required for cross-site cookies between Vercel and Railway
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    }
})
.AddDiscord(options =>
{
    options.ClientId = builder.Configuration["Discord:ClientId"]!;
    options.ClientSecret = builder.Configuration["Discord:ClientSecret"]!;
    options.Scope.Add("identify");
    options.ClaimActions.MapJsonKey("urn:discord:avatar:hash", "avatar");
    options.ClaimActions.MapJsonKey("urn:discord:globalname", "global_name");
    options.CallbackPath = new PathString("/signin-discord");
    options.CorrelationCookie.HttpOnly = true;
    if (builder.Environment.IsDevelopment())
    {
        // Allowed locally on non-secure HTTP localhost
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    }
    else
    {
        // Enforced for production cross-site HTTPS proxies
        options.CorrelationCookie.SameSite = SameSiteMode.None;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
    }
});

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options => 
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
    
builder.Services.AddHttpClient<RiotApiService>();

builder.Services.AddHostedService<RankUpdateWorker>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

var app = builder.Build();

// Custom middleware to resolve multi-hop proxy headers (Vercel -> Railway)
app.Use((context, next) =>
{
    if (context.Request.Headers.TryGetValue("X-Forwarded-Host", out var forwardedHost))
    {
        // Extract the original host (Vercel) from the comma-separated list
        var firstHost = forwardedHost.ToString().Split(',').FirstOrDefault()?.Trim();
        if (!string.IsNullOrEmpty(firstHost))
        {
            context.Request.Headers["X-Forwarded-Host"] = firstHost;
        }
    }
    return next();
});

app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(options => { options.RouteTemplate = "openapi/{documentName}.json"; });
    app.MapScalarApiReference();
}

app.UseCors("AllowAngularDev");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();