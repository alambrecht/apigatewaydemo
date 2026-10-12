// =====================================================================
// DEMO API
// Knows nothing about passwords. It only trusts tokens signed by the
// auth service, and checks roles before doing anything (authorization).
// =====================================================================
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);
var auth = builder.Configuration.GetSection("Auth");

// AUTHENTICATION: verify the token's signature, issuer, audience and expiry.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Downloads http://auth:8080/.well-known/openid-configuration,
        // then the public keys from its jwks_uri. Issuer is taken from there too.
        options.Authority = auth["Authority"];
        options.Audience  = auth["Audience"];
        options.RequireHttpsMetadata = false;   // demo only — use HTTPS in production
        options.MapInboundClaims = false;       // keep "sub", "roles" etc. as-is
        options.TokenValidationParameters.NameClaimType = "preferred_username";
        options.TokenValidationParameters.RoleClaimType = "roles";
    });

// AUTHORIZATION: named policies, so rules live in one place.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CanRead", p => p.RequireRole("reader"))
    .AddPolicy("IsAdmin", p => p.RequireRole("admin"));

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

// Anyone
app.MapGet("/public", () => new { message = "Anyone can see this." });

// Any logged-in user: shows what's inside your token
app.MapGet("/me", (ClaimsPrincipal user) => new
{
    id = user.FindFirstValue("sub"),
    username = user.Identity!.Name,
    roles = user.FindAll("roles").Select(c => c.Value)
}).RequireAuthorization();

// Role: reader
app.MapGet("/items", () => new[]
{
    new { id = 1, name = "Oil change",    price = 49.99m },
    new { id = 2, name = "Brake pads",    price = 189.00m },
    new { id = 3, name = "Tire rotation", price = 29.99m }
}).RequireAuthorization("CanRead");

// Role: admin
app.MapGet("/admin/stats", (ClaimsPrincipal user) => new
{
    requestedBy = user.Identity!.Name,
    totalItems = 3,
    serverTime = DateTime.UtcNow
}).RequireAuthorization("IsAdmin");

app.Run();
