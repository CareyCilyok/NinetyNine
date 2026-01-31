using System.Threading.Tasks;

namespace NinetyNine.Presentation.Services
{
    /// <summary>
    /// Abstraction for local file-based persistence.
    /// Default location: %APPDATA%\NinetyNine\ on Windows.
    /// </summary>
    public interface IStorageService
    {
        /// <summary>
        /// Gets the base storage directory path
        /// </summary>
        string BasePath { get; }

        /// <summary>
        /// Saves an object as JSON to the specified subdirectory
        /// </summary>
        Task<bool> SaveAsync<T>(string subdirectory, string filename, T data);

        /// <summary>
        /// Loads an object from JSON in the specified subdirectory
        /// </summary>
        Task<T?> LoadAsync<T>(string subdirectory, string filename) where T : class;

        /// <summary>
        /// Checks if a file exists
        /// </summary>
        bool Exists(string subdirectory, string filename);

        /// <summary>
        /// Gets all files in a subdirectory
        /// </summary>
        string[] GetFiles(string subdirectory, string pattern = "*.json");

        /// <summary>
        /// Deletes a file
        /// </summary>
        bool Delete(string subdirectory, string filename);
    }
}
