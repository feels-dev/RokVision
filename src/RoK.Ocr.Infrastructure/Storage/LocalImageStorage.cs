using System;
using System.IO;
using System.Threading.Tasks;
using RoK.Ocr.Domain.Interfaces;

namespace RoK.Ocr.Infrastructure.Storage;

public class LocalImageStorage : IImageStorage
{
    private readonly string _basePath;

    // We receive the root path via dependency injection (configured in Program.cs)
    public LocalImageStorage(string webRootPath)
    {
        _basePath = webRootPath ?? throw new ArgumentNullException(nameof(webRootPath));
    }

    public string GetBasePath() => _basePath;

    public async Task<string> SaveImageAsync(Stream imageStream, string fileName)
    {
        // Creates: wwwroot/uploads/ocr/{guid}_name.png
        var uploadFolder = Path.Combine(_basePath, "uploads", "ocr");
        
        if (!Directory.Exists(uploadFolder))
            Directory.CreateDirectory(uploadFolder);

        // Generates a unique name to avoid collisions
        var uniqueName = $"{Guid.NewGuid()}_{fileName}";
        var fullPath = Path.Combine(uploadFolder, uniqueName);

        using (var fileStream = new FileStream(fullPath, FileMode.Create))
        {
            await imageStream.CopyToAsync(fileStream);
        }

        return fullPath;
    }

    public void DeleteImage(string filePath)
    {
        if (File.Exists(filePath))
        {
            try { File.Delete(filePath); } catch { /* Log error if necessary */ }
        }
    }
}