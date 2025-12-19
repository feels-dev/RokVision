using RoK.Ocr.Application.Cognitive;
using RoK.Ocr.Application.Magnifier;
using RoK.Ocr.Application.Services;
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Infrastructure.PythonEngine;
using RoK.Ocr.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// --- 1. SERVICE CONFIGURATION (DI) ---

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Infrastructure: Image Storage
// Sets the base path as the API root (where the wwwroot folder is located)
builder.Services.AddSingleton<IImageStorage>(sp =>
    new LocalImageStorage(builder.Environment.WebRootPath ?? builder.Environment.ContentRootPath));

// Infrastructure: Python OCR Service
builder.Services.AddHttpClient<IOcrService, PythonOcrService>(client =>
{
    // Reading URL from appsettings or using the localhost default
    var pythonUrl = builder.Configuration["PythonServiceUrl"] ?? "http://localhost:8000/";
    client.BaseAddress = new Uri(pythonUrl);
    client.Timeout = TimeSpan.FromSeconds(30); // Generous timeout for heavy OCR processing
});

// Application: Core Services
builder.Services.AddScoped<TheMagnifier>();
builder.Services.AddScoped<OcrOrchestrator>();

var app = builder.Build();

// --- 2. HTTP PIPELINE ---

// if (app.Environment.IsDevelopment())
// {
app.UseSwagger();
app.UseSwaggerUI();
// }

app.UseHttpsRedirection();

// Allow serving static files (to view debugs/uploads if needed)
app.UseStaticFiles();

app.UseAuthorization();
app.MapControllers();

app.Run();