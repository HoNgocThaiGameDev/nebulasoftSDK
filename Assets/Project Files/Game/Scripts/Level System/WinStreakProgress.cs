namespace NebulaSoft
{
    [System.Serializable]
    public sealed class WinStreakSave : ISaveObject
    {
        public int Current;

        public void Flush()
        {
        }
    }

    public static class WinStreakProgress
    {
        private const string SAVE_KEY = "Win Streak";

        public static event System.Action Changed;

        public static int Current
        {
            get
            {
                WinStreakSave save = GetSave();
                return save != null ? save.Current : 0;
            }
        }

        public static void RegisterWin()
        {
            WinStreakSave save = GetSave();
            if (save == null)
                return;

            save.Current++;
            SaveController.MarkAsSaveIsRequired();
            Changed?.Invoke();
        }

        public static void Reset()
        {
            WinStreakSave save = GetSave();
            if (save == null || save.Current == 0)
                return;

            save.Current = 0;
            SaveController.MarkAsSaveIsRequired();
            Changed?.Invoke();
        }

        private static WinStreakSave GetSave()
        {
            if (!SaveController.IsSaveLoaded)
                return null;

            return SaveController.GetSaveObject<WinStreakSave>(SAVE_KEY);
        }
    }
}


