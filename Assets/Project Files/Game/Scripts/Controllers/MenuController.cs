using UnityEngine;
using UnityEngine.SceneManagement;

namespace NebulaSoft
{
    public sealed class MenuController : MonoBehaviour
    {
        [SerializeField] UIController uiController;
        private void Awake() { uiController?.Init(); uiController?.InitPages(); }
        private void Start()
        {
            UIController.ShowPage<UIMainMenu>();
            _ = LocalLeaderboardService.PreloadLeaderboardsAsync();
            AdsManager.EnableBanner();
            Overlay.Hide(0.3f);
        }
        public static void LoadGame() { if (SceneUtils.DoesSceneExist(GameConsts.SCENE_GAME)) SceneManager.LoadScene(GameConsts.SCENE_GAME); }
        public static void Unload(SimpleCallback callback) => callback?.Invoke();
        public static void OnPlayButtonClicked() => LoadGame();
    }
}
