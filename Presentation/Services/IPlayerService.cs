using System.Threading.Tasks;
using NinetyNine.Model;

namespace NinetyNine.Presentation.Services
{
    /// <summary>
    /// Service for managing the default player profile.
    /// Single-player mode: one profile created on first launch, loaded on subsequent launches.
    /// </summary>
    public interface IPlayerService
    {
        /// <summary>
        /// Gets the current player (loaded or created on initialization)
        /// </summary>
        Player CurrentPlayer { get; }

        /// <summary>
        /// Initializes the player service - loads existing or creates default player
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Updates the current player profile
        /// </summary>
        Task<bool> UpdatePlayerAsync(Player player);
    }
}
