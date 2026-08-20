using UnityEngine;
using UnityEngine.SceneManagement;

namespace NebulaSoft
{
    /// <summary>Game scene controller.</summary>
    [StaticUnload]
    public sealed class GameController : MonoBehaviour
    {
        private static bool isGameActivated;
        private static bool isGameFinished;
        public static bool IsGameActivated => isGameActivated;

        private void Awake()
        {
            isGameActivated = false;
            isGameFinished = false;
            UIController ui = GetComponent<UIController>();
            ui?.Init();
            ui?.InitPages();
            AdsManager.EnableBanner();
        }

        private void Start()
        {
            UIController.ShowPage<UIGame>();
            Overlay.Hide(0f);
        }

        public static void ActivateGame() => isGameActivated = true;
        public static void GameComplete()
        {
            if (isGameFinished) return;
            isGameFinished = true;
            isGameActivated = false;
            ActiveSession.Current.OnLevelCompleted();
            QuestService.ReportProgress(QuestGoalType.CompleteLevels);
        }
        public static void ShowCompleteUI() => UIController.ShowPage<UIComplete>();
        public static void GameOver(bool allowRevive, float uiDelay = 0f, bool resetWinStreakOnFinalFailure = false)
        {
            if (isGameFinished) return;
            isGameFinished = true;
            isGameActivated = false;
            if (uiDelay > 0f) Tween.DelayedCall(uiDelay, () => UIGameOver.Show(allowRevive, resetWinStreakOnFinalFailure));
            else UIGameOver.Show(allowRevive, resetWinStreakOnFinalFailure);
        }
        public static void Revive(int seconds) { isGameFinished = false; isGameActivated = true; UIController.HidePage<UIGameOver>(); }
        public static void OnLevelCompleted() => ActiveSession.Current.OnLevelCompleted();
        public static void OnLevelFailed() { }
        public static void Replay(SimpleCallback callback = null) { callback?.Invoke(); LoadScene(GameConsts.SCENE_GAME); }
        public static void OnCompleteRewardRecieved() => LoadScene(GameConsts.SCENE_GAME);
        public static void LoadMenu(SimpleCallback callback = null) { callback?.Invoke(); LoadScene(GameConsts.SCENE_MENU); }
        public static void Unload(SimpleCallback callback) => callback?.Invoke();
        private static void LoadScene(string scene) { if (SceneUtils.DoesSceneExist(scene)) SceneManager.LoadScene(scene); }
        private static void UnloadStatic() { isGameActivated = false; isGameFinished = false; }
    }
}
