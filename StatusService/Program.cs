using Microsoft.Extensions.Caching.Distributed;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddStackExchangeRedisCache(options =>
{
    //options.ConfigurationOptions.Password = "eYVX7EwVmmxKPCDmwMtyKVge8oLd2t81";
    options.Configuration = "localhost:6378";
});
builder.Services.AddCors();
var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseCors(p =>
{
    p.AllowAnyOrigin();
    p.AllowAnyMethod();
    p.AllowAnyHeader();
});
app.UseHttpsRedirection();


app.MapGet("/status/{jobId}", (IDistributedCache distributedCache,string jobId) =>
{
    var txt = distributedCache.GetString($"JOB:{jobId}");
    if(txt == null)
        return Results.NotFound(jobId);
    return Results.Ok(txt);
});

app.MapPost("/abort/{jobId}", (IDistributedCache distributedCache, string jobId) =>
{
    var txt = distributedCache.GetString($"JOB:{jobId}");
    if (txt == null)
        return Results.NotFound(jobId);
    else
        distributedCache.SetString($"JOB:{jobId}", "ABORT");
    return Results.Ok(txt);
});



app.Run();
