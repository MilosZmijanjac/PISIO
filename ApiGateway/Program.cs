using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System.Net;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOcelot();
builder.WebHost.ConfigureAppConfiguration(config =>
{
    config.AddJsonFile("ocelot.json");
});
builder.Services.AddCors();

builder.Services.AddAuthentication(option =>
{
    option.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    option.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(async options =>
    {
        using var httpClient = new HttpClient();

        var jwks = await httpClient.GetStringAsync("https://www.googleapis.com/oauth2/v3/certs");
        var signingKeys = new JsonWebKeySet(jwks).Keys;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            IssuerSigningKeys = signingKeys
        };
    });


var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseCors(p =>
{
    p.AllowAnyOrigin();
    p.AllowAnyMethod();
    p.AllowAnyHeader();
});
app.UseAuthentication();
//app.UseHttpsRedirection();
app.UseOcelot().Wait();




app.Run();
