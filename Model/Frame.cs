using System;
using System.Collections.Generic;
using System.Text;

namespace Model
{
    class Frame
    {
        public bool BreakBonus { get; set; } = false;
        public int BallCount { get; set; } = 0;
        public bool RunoutBonus { get; set; } = false;
        public int RunningTotal { get; set; } = 0;
    }
}
