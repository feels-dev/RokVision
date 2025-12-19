using System.IO;
using System.Threading.Tasks;

namespace RoK.Ocr.Domain.Interfaces;

public interface IImageStorage
{
    // Saves the stream (upload) and returns the physical path where it was saved
    Task<string> SaveImageAsync(Stream imageStream, string fileName);
    
    // Deletes (for cleanup)
    void DeleteImage(string filePath);
    
    // Returns where we are saving (to be used as a base by the Magnifier)
    string GetBasePath();
}