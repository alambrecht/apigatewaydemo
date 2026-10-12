// =====================================================================
// AUTH SERVICE
// Its only job: check usernames/passwords and issue signed JWTs.
// It decides WHO you are (authentication) and WHAT ROLES you have.
// The gateway and the demo API enforce those roles (authorization).
// No NuGet packages needed — everything here ships with .NET 10.
// =====================================================================

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration.GetSection("Auth");

string issuer   = cfg["Issuer"]   ?? "http://auth:8080";
string audience = cfg["Audience"] ?? "demo-api";
string keyPath  = cfg["KeyPath"]  ?? "/keys/signing-key.pem";
int tokenMinutes = int.Parse(cfg["TokenMinutes"] ?? "15");

// Private key signs tokens; public key is published so others can verify them.
var signingKey = SigningKey.LoadOrCreate(keyPath);

builder.Services.AddSingleton(signingKey);
builder.Services.AddSingleton<UserStore>();

var app = builder.Build();

// ---------------------------------------------------------------------
// POST /login  { "username": "...", "password": "..." }  ->  access token
// ---------------------------------------------------------------------
app.MapPost("/login", (LoginRequest req, UserStore users, SigningKey key) =>
{
    var user = users.Validate(req.Username, req.Password);
    if (user is null)
        return Results.Unauthorized();   // same answer for "no such user" and "wrong password"

    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    var token = key.CreateJwt(new Dictionary<string, object>
    {
        ["iss"] = issuer,                       // who made this token
        ["aud"] = audience,                     // who it is meant for
        ["sub"] = user.Id,                      // stable user id
        ["preferred_username"] = user.Username,
        ["roles"] = user.Roles,                 // always a JSON array, e.g. ["reader"]
        ["iat"] = now,                          // issued at
        ["nbf"] = now,                          // not valid before
        ["exp"] = now + tokenMinutes * 60,      // expires
        ["jti"] = Guid.NewGuid().ToString()     // unique token id
    });

    return Results.Ok(new
    {
        access_token = token,
        token_type = "Bearer",
        expires_in = tokenMinutes * 60
    });
});

// ---------------------------------------------------------------------
// Public key discovery: lets the gateway and the API verify signatures
// without ever seeing the private key.
// ---------------------------------------------------------------------
app.MapGet("/.well-known/openid-configuration", () => new
{
    issuer,
    jwks_uri = $"{issuer}/.well-known/jwks.json",
    id_token_signing_alg_values_supported = new[] { "RS256" }
});

app.MapGet("/.well-known/jwks.json", (SigningKey key) => new { keys = new[] { key.PublicJwk } });

app.Run();

record LoginRequest(string? Username, string? Password);
