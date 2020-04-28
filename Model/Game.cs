using System;
using System.Collections.Generic;

namespace NinetyNine.Model
{
    /// <summary>
    /// Represents a single Game
    /// </summary>
    /// <remarks>
    /// A Game is made up of 9 <see cref="Frames"/> <seealso cref="Frame"/>
    /// </remarks>
    public class Game
    {
        /// <summary>
        /// Gets/Sets the unique identifier for a single <see cref="Game"/> played
        /// </summary>
        public Guid GameId { get; set; }

        /// <summary>
        /// Gets/Sets the <see cref="NinetyNine.Model.Player"/> of this <see cref="Game"/>
        /// </summary>
        public Player Player { get; set; }

        /// <summary>
        /// Gets/Sets the location where this game took place. <seealso cref="Venue"/>
        /// </summary>
        public Venue LocationPlayed { get; set; }

        /// <summary>
        /// Gets/Sets the the date and time of this <see cref="Game"/>
        /// </summary>
        public DateTime WhenPlayed { get; set; }

        /// <summary>
        /// Gets/Sets the size of the pool table played on. <seealso cref="NinetyNine.Model.TableSize"/>
        /// </summary>
        public TableSize TableSize { get; set; } = TableSize.Unknown;

        /// <summary>
        /// Gets/Sets the list of frames played in this <see cref="Game"/>. <seealso cref="Frame"/>
        /// </summary>
        public List<Frame> Frames { get; set; } = new List<Frame>(9);
    }
}