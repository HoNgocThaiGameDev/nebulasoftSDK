using System.Collections.Generic;
using UnityEngine;

namespace NebulaSoft
{
    [CreateAssetMenu(fileName = "Rewards Set", menuName = "Data/Rewards/Rewards Set")]
    public class RewardsSet : ScriptableObject
    {
        [Group("System")]
        [SerializeReference] List<Reward> rewards = new List<Reward>();
        public List<Reward> Rewards => rewards;

        [Group("System")]
        [SerializeField] string notes;

        public event SimpleCallback RewardRecieved;

        public void ApplyReward()
        {
            if(!rewards.IsNullOrEmpty())
            {
                for (int i = 0; i < rewards.Count; i++)
                    rewards[i]?.ApplyReward();
            }

            RewardRecieved?.Invoke();
        }

        public void RestoreReward()
        {
            if (!rewards.IsNullOrEmpty())
            {
                for (int i = 0; i < rewards.Count; i++)
                    rewards[i]?.RestoreReward();
            }
        }

        public List<IRewardPreview> GetPreviews()
        {
            List<IRewardPreview> preview = new List<IRewardPreview>();
            foreach (Reward reward in rewards)
            {
                List<IRewardPreview> rewardPreview = reward.GetRewardPreviews();
                if (!rewardPreview.IsNullOrEmpty())
                {
                    preview.AddRange(rewardPreview);
                }
            }

            return preview;
        }
    }
}
