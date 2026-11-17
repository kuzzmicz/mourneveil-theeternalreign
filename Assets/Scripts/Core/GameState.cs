using System;

namespace Mourneveil
{
    [Serializable]
    public class GameState
    {
        public int saveVersion = 1;
        public double blood = 0;
        public int clickUpgradeLevel = 0;
        public int servantLevel = 0;
        public float awakening = 0f;
        public bool firstWhisperSeen = false;
    }
}
