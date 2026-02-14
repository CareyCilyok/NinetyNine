using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace NinetyNine.Presentation.Services
{
    /// <summary>
    /// File-based storage service using %APPDATA%\NinetyNine\ on Windows.
    /// </summary>
    public class StorageService : IStorageService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public string BasePath { get; }

        public StorageService()
        {
            BasePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NinetyNine");

            if (!Directory.Exists(BasePath))
            {
                Directory.CreateDirectory(BasePath);
            }
        }

        public async Task<bool> SaveAsync<T>(string subdirectory, string filename, T data)
        {
            try
            {
                var directory = Path.Combine(BasePath, subdirectory);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var filePath = Path.Combine(directory, filename);
                var json = JsonSerializer.Serialize(data, JsonOptions);
                await File.WriteAllTextAsync(filePath, json);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StorageService.SaveAsync error: {ex.Message}");
                return false;
            }
        }

        public async Task<T?> LoadAsync<T>(string subdirectory, string filename) where T : class
        {
            try
            {
                var filePath = Path.Combine(BasePath, subdirectory, filename);
                if (!File.Exists(filePath))
                {
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<T>(json, JsonOptions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"StorageService.LoadAsync error: {ex.Message}");
                return null;
            }
        }

        public bool Exists(string subdirectory, string filename)
        {
            var filePath = Path.Combine(BasePath, subdirectory, filename);
            return File.Exists(filePath);
        }

        public string[] GetFiles(string subdirectory, string pattern = "*.json")
        {
            var directory = Path.Combine(BasePath, subdirectory);
            if (!Directory.Exists(directory))
            {
                return Array.Empty<string>();
            }
            return Directory.GetFiles(directory, pattern);
        }

        public bool Delete(string subdirectory, string filename)
        {
            try
            {
                var filePath = Path.Combine(BasePath, subdirectory, filename);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
