using System;

namespace NinetyNine.Model
{
    /// <summary>
    /// Represents a single player's turn
    /// </summary>
    public class Frame
    {
        public Guid FrameId { get; set; }

        public Guid GameId { get; set; }
        public Game Game { get; set; }

        public bool BreakBonus { get; set; } = false;
        public int BallCount { get; set; } = 0;
        public bool RunoutBonus { get; set; } = false;
        public int RunningTotal { get; set; } = 0;
    }
}
