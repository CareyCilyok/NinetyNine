/// Copyright (c) 2020-2022
///
/// Permission is hereby granted, free of charge, to any person obtaining a copy
/// of this software and associated documentation files (the "Software"), to deal
/// in the Software without restriction, including without limitation the rights
/// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
/// copies of the Software, and to permit persons to whom the Software is
/// furnished to do so, subject to the following conditions:
/// 
/// The above copyright notice and this permission notice shall be included in all
/// copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
/// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
/// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
/// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
/// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
/// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NinetyNine.Model
{
    /// <summary>
    /// Represents a player in the game
    /// </summary>
    public class Player
    {
        public Guid PlayerId { get; set; } = Guid.NewGuid();

        public string Username { get; set; } = String.Empty;

        public string EmailAddress { get; set; } = String.Empty;
        public string PhoneNumber { get; set; } = String.Empty;
        public string LastName { get; set; } = String.Empty;
        public string FirstName { get; set; } = String.Empty;
        public string MiddleName { get; set; } = String.Empty;

        /// <summary>
        /// Hashed password for authentication (never store plain text)
        /// </summary>
        [JsonIgnore]
        public string PasswordHash { get; set; } = String.Empty;

        /// <summary>
        /// Salt used for password hashing
        /// </summary>
        [JsonIgnore]
        public string PasswordSalt { get; set; } = String.Empty;

        /// <summary>
        /// When the player account was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the player last logged in
        /// </summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// Whether the account is active
        /// </summary>
        public bool IsActive { get; set; } = true;

        public string Name
        {
            get
            {
                if (!string.IsNullOrEmpty(FirstName) || !string.IsNullOrEmpty(LastName))
                {
                    return $"{FirstName} {LastName}".Trim();
                }
                return Username;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    var parts = value.Split(' ', 2);
                    FirstName = parts[0];
                    LastName = parts.Length > 1 ? parts[1] : "";
                }
            }
        }

        [JsonIgnore]
        public List<Game> Games { get; set; } = new List<Game>();
    }
}
