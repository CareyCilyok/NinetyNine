using System;
using System.Threading.Tasks;
using NinetyNine.Model;

namespace NinetyNine.Presentation.Services
{
    /// <summary>
    /// Manages the default player profile.
    /// Creates a default player on first launch, loads existing player on subsequent launches.
    /// </summary>
    public class PlayerService : IPlayerService
    {
        private const string PlayersDirectory = "Players";
        private const string DefaultPlayerFile = "default.json";

        private readonly IStorageService _storageService;
        private Player _currentPlayer = null!;

        public Player CurrentPlayer => _currentPlayer;

        public PlayerService() : this(new StorageService())
        {
        }

        public PlayerService(IStorageService storageService)
        {
            _storageService = storageService;
        }

        public async Task InitializeAsync()
        {
            // Try to load existing player
            var existingPlayer = await _storageService.LoadAsync<Player>(PlayersDirectory, DefaultPlayerFile);

            if (existingPlayer != null)
            {
                _currentPlayer = existingPlayer;
                System.Diagnostics.Debug.WriteLine($"Loaded existing player: {_currentPlayer.Name} ({_currentPlayer.PlayerId})");
            }
            else
            {
                // Create default player on first launch
                _currentPlayer = new Player
                {
                    PlayerId = Guid.NewGuid(),
                    FirstName = "Player",
                    LastName = "1",
                    Username = "Player1"
                };

                await _storageService.SaveAsync(PlayersDirectory, DefaultPlayerFile, _currentPlayer);
                System.Diagnostics.Debug.WriteLine($"Created default player: {_currentPlayer.Name} ({_currentPlayer.PlayerId})");
            }
        }

        public async Task<bool> UpdatePlayerAsync(Player player)
        {
            if (player.PlayerId != _currentPlayer.PlayerId)
            {
                return false; // Can only update the current player
            }

            _currentPlayer = player;
            return await _storageService.SaveAsync(PlayersDirectory, DefaultPlayerFile, _currentPlayer);
        }
    }
}
