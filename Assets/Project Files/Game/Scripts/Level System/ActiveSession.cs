#pragma warning disable 0649

using UnityEngine;

namespace NebulaSoft
{
    [StaticUnload]
    public class ActiveSession
    {
        private readonly LevelSave levelSave;

        public LevelSave Save => levelSave;

        public int DisplayLevelIndex => levelSave.DisplayLevelIndex;
        public int LevelIndex => levelSave.RealLevelIndex;
        public int MaxReachedLevelIndex => levelSave.MaxReachedLevelIndex;

        public bool FirstStart => levelSave.FirstStart;
        public bool FirstTimeCompletedLevel => levelSave.CompletedLevelIndex < levelSave.DisplayLevelIndex;

        private LevelData levelData;
        public LevelData LevelData => levelData;

        private static ActiveSession currentSession;
        public static ActiveSession Current
        {
            get
            {
                if (currentSession == null)
                    currentSession = new ActiveSession();

                return currentSession;
            }
        }

        public ActiveSession()
        {
            levelSave = SaveController.GetSaveObject<LevelSave>();
        }

        public void OnLevelStarted(LevelData levelData)
        {
            this.levelData = levelData;

            levelSave.FirstStart = false;
        }

        public void OnLevelCompleted()
        {
            levelSave.IsPlayingRandomLevel = false;
            levelSave.LastPlayerLevelIndex = -1;

            WinStreakProgress.RegisterWin();

            if (FirstTimeCompletedLevel)
            {
                levelSave.CompletedLevelIndex = levelSave.RealLevelIndex;
            }
        }

        public static void SetEditorLevelIndex(int levelIndex)
        {
            if(Application.isPlaying)
            {
                ActiveSession activeSession = Current;
                activeSession.SetLevelIndex(levelIndex);

                return;
            }

#if UNITY_EDITOR
            GlobalSave globalSave = SaveController.GetGlobalSave();

            LevelSave editorLevelSave = globalSave.GetSaveObject<LevelSave>();
            editorLevelSave.DisplayLevelIndex = levelIndex;
            editorLevelSave.FirstStart = true;

            if (levelIndex > editorLevelSave.MaxReachedLevelIndex)
            {
                editorLevelSave.MaxReachedLevelIndex = levelIndex;
            }

            SaveController.SaveCustom(globalSave);
#endif
        }

        public void SetLevelIndex(int levelNumber)
        {
            if(levelSave.DisplayLevelIndex != levelNumber)
                levelSave.IsPlayingRandomLevel = false;

            levelSave.DisplayLevelIndex = levelNumber;
            levelSave.FirstStart = true;

            if (levelNumber > levelSave.MaxReachedLevelIndex)
            {
                levelSave.MaxReachedLevelIndex = levelNumber;
            }
        }

public int GetLevelIndex(int levelIndex)
        {
            levelSave.IsPlayingRandomLevel = false;
            levelSave.RealLevelIndex = levelIndex;
            levelSave.LastPlayerLevelIndex = levelIndex;
            return levelIndex;
        }

        private static void UnloadStatic()
        {
            currentSession = null;
        }
    }
}


