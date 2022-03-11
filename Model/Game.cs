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