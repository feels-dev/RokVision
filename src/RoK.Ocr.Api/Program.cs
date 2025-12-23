using RoK.Ocr.Application.Features.Governor.Orchestrator;
using RoK.Ocr.Application.Features.Governor.Services;
using RoK.Ocr.Application.Features.Reports.Orchestrator;
using RoK.Ocr.Application.Features.Reports.Services; // Includes ReportScoreCalculator
using RoK.Ocr.Domain.Interfaces;
using RoK.Ocr.Infrastructure.Persistence;
using RoK.Ocr.Infrastructure.PythonEngine;
using RoK.Ocr.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// --- 1. SERVICE CONFIGURATION (DI) ---

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

// Infrastructure
builder.Services.AddSingleton<IImageStorage>(sp =>
    new LocalImageStorage(builder.Environment.WebRootPath ?? builder.Environment.ContentRootPath));

builder.Services.AddHttpClient<IOcrService, PythonOcrService>(client =>
{
    var pythonUrl = builder.Configuration["PythonServiceUrl"] ?? "http://localhost:8000/";
    client.BaseAddress = new Uri(pythonUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton<IVocabularyLoader>(sp =>
    new VocabularyLoader(
        builder.Environment.ContentRootPath,
        sp.GetRequiredService<ILogger<VocabularyLoader>>()
    ));

// --- APPLICATION SERVICES ---

// Feature: Governor
builder.Services.AddScoped<GovernorMagnifier>(); 
builder.Services.AddScoped<GovernorOrchestrator>();

// Feature: Reports
builder.Services.AddScoped<WarMagnifier>();
builder.Services.AddScoped<ReportScoreCalculator>(); // <--- NEW SERVICE REGISTERED
builder.Services.AddScoped<ReportOrchestrator>();

var app = builder.Build();

// --- 2. HTTP PIPELINE ---

// if (app.Environment.IsDevelopment())
// {
app.UseSwagger();
app.UseSwaggerUI();
// }

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health");
app.Run();