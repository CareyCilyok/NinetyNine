using System;
using System.Collections.Generic;

namespace NinetyNine.Model
{
    /// <summary>
    /// Represents a player in the game
    /// </summary>
    public class Player
    {
        public Guid PlayerId { get; set; }

        public string Username { get; set; } = String.Empty;

        public string EmailAddress { get; set; } = String.Empty;
        public string PhoneNumber { get; set; } = String.Empty;
        public string LastName { get; set; } = String.Empty;
        public string FirstName { get; set; } = String.Empty;
        public string MiddleName { get; set; } = String.Empty;

        public List<Game> Games { get; set; } = new List<Game>();
    }
}
