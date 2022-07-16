using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
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



app.MapGet("/download/{jobId}", (string jobId) =>
{
    //string folder = @"C:\Users\SnakeKD\Desktop\PISIO\ImagesToPdf\FileService\" + jobId;
    string folder = @"C:\Users\SnakeKD\Desktop\PISIO\Storage";

   var ms = new MemoryStream();
   using (var stream = new FileStream(Path.Combine(Path.Combine(folder, jobId), jobId + ".zip"), FileMode.Open))
   {
       stream.CopyTo(ms);
   }

    ms.Position = 0;
   return Results.File(ms, "application/x-zip-compressed", "i2gp_file.zip");

});

app.Run();

