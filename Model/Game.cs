using System;
using System.Collections.Generic;

namespace NinetyNine.Model
{
    /// <summary>
    /// Represents a single game
    /// </summary>
    public class Game
    {
        public Guid GameId { get; set; }

        public Guid PlayerId { get; set; }
        public Player Player { get; set; }

        public Venue LocationPlayed { get; set; }
        public DateTime WhenPlayed { get; set; }
        public TableSize TableSize { get; set; } = TableSize.Unknown;
        public List<Frame> Frames { get; set; } = new List<Frame>(9);
    }
}