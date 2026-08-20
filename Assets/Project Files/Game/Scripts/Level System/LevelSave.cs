namespace NebulaSoft
{
    [System.Serializable]
    public class LevelSave : ISaveObject
    {
        public int MaxReachedLevelIndex = 0;

        public int RealLevelIndex = 0;
        public int DisplayLevelIndex = 0;
        public bool IsPlayingRandomLevel = false;

        public int LastPlayerLevelIndex = -1;

        public int CompletedLevelIndex = -1;

        public bool FirstStart = true;

        public void Flush()
        {

        }
    }
}


