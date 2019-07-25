﻿using System;

namespace Model
{
    /// <summary>
    /// Represents a player in the game
    /// </summary>
    public class Player
    {
        public Guid Id { get; set; }

        public string LastName { get; set; } = String.Empty;
        public string FirstName { get; set; } = String.Empty;
        public string MiddleName { get; set; } = String.Empty;

        public string PhoneNumber { get; set; } = String.Empty;
        public string EmailAddress { get; set; } = String.Empty;
    }
}