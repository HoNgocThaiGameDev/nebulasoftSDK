using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NebulaSoft
{
    public class UIGameOver : UIPage
    {
        [BoxGroup("References", "References")]
        [SerializeField] CanvasGroup backgroundFade;
        [BoxGroup("References")]
        [SerializeField] RectTransform safeAreaRectTransform;

        [BoxGroup("Content", "Content")]
        [SerializeField] GameObject gameOverPanel;
        [BoxGroup("Content")]
        [SerializeField] UIScaleAnimation levelFailed;

        [BoxGroup("Revive", "Revive")]
        [SerializeField] GameObject revivePanel;
        [BoxGroup("Revive")]
        [SerializeField] TextMeshProUGUI reviveTimerText;
        [BoxGroup("Revive")]
        [SerializeField] Image reviveFillbarImage;
        [BoxGroup("Revive")]
        [SerializeField] Button reviveButton;
        [BoxGroup("Revive")]
        [SerializeField] Button reviveSkipButton;

        [BoxGroup("Buttons", "Buttons")]
        [SerializeField] Button replayButton;

        private bool showRevive;
        private bool resetWinStreakOnFinalFailure;
        private TweenCase floatTweenCase;

        public override void Init()
        {
            NotchSaveArea.RegisterRectTransform(safeAreaRectTransform);

            replayButton.onClick.AddListener(OnReplayButtonClicked);
            reviveButton.onClick.AddListener(OnReviveButtonClicked);
            reviveSkipButton.onClick.AddListener(OnReviveSkipButtonClicked);

            showRevive = false;
        }

        public override void PlayShowAnimation()
        {
            backgroundFade.alpha = 0.0f;

            if (showRevive)
            {
                backgroundFade.DOFade(0.7f, 0.3f);

                revivePanel.SetActive(true);
                gameOverPanel.SetActive(false);

                int reviveDuration = 5;
                int targetTime = 1;

                reviveTimerText.text = reviveDuration.ToString();

                floatTweenCase = Tween.DoFloat(0, reviveDuration, reviveDuration, (value) =>
                {
                    reviveFillbarImage.fillAmount = 1.0f - floatTweenCase.State;

                    if (value >= targetTime)
                    {
                        targetTime += 1;
                        reviveDuration -= 1;

                        reviveTimerText.text = reviveDuration.ToString();
                    }
                }).OnComplete(() =>
                {
                    ShowLevelFailUI();
                });
            }
            else
            {
                ShowLevelFailUI();
            }

            UIController.OnPageOpened(this);
        }

        public override void PlayHideAnimation()
        {
            UIController.OnPageClosed(this);
        }

        private void ShowLevelFailUI()
        {
            if (resetWinStreakOnFinalFailure)
            {
                resetWinStreakOnFinalFailure = false;
                WinStreakProgress.Reset();
            }

#if MODULE_MONETIZATION
            AdsManager.ShowInterstitial((result) =>
            {
                LivesSystem.TakeLife();

                backgroundFade.DOFade(1f, 0.3f);

                revivePanel.SetActive(false);
                gameOverPanel.SetActive(true);
            });
#else
            LivesSystem.TakeLife();

            backgroundFade.DOFade(1f, 0.3f);

            revivePanel.SetActive(false);
            gameOverPanel.SetActive(true);
#endif
        }

        private void OnDestroy()
        {
            floatTweenCase.KillActive();
        }

        public static void Show(bool showRevive, bool resetWinStreakOnFinalFailure = false)
        {
            UIGameOver gameOverUI = UIController.GetPage<UIGameOver>();
            gameOverUI.showRevive = showRevive;
            gameOverUI.resetWinStreakOnFinalFailure = resetWinStreakOnFinalFailure;

            AdsSettings adsSettings = Monetization.AdsSettings;
            if (adsSettings.RewardedVideoType == AdProvider.Disable || !Monetization.IsActive)
                gameOverUI.showRevive = false;

            UIController.ShowPage<UIGameOver>();
        }

        #region Buttons 
        public void OnReplayButtonClicked()
        {
            GameController.Replay();
        }

        private void OnReviveButtonClicked()
        {
            floatTweenCase.Pause();

            AdsManager.ShowRewardBasedVideo((reward) =>
            {
                if(reward)
                {
                    GameController.Revive(GameData.Data.ReviveExtraSeconds);
                }
                else
                {
                    floatTweenCase.CompleteActive();
                }
            });

        }

        private void OnReviveSkipButtonClicked()
        {
            floatTweenCase.CompleteActive();
        }
        #endregion
    }
}
