using System;
using System.Collections.Generic;
using System.Text;

namespace Model
{
    class Game
    {
        public Venue LocationPlayed { get; set; }
        public DateTime WhenPlayed { get; set; }
        public Player Player { get; set; }

        public List<Frame> Frames { get; set; } = new List<Frame>(9);
    }
}