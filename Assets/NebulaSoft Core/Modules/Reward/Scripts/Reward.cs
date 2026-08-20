using System;
using System.Collections.Generic;

namespace NebulaSoft
{
    [Serializable]
    public abstract class Reward
    {
        public abstract void ApplyReward();
        public virtual void RestoreReward() { }

        public virtual List<IRewardPreview> GetRewardPreviews()
        {
            return null;
        }

        /// <summary>
        /// Return true if you want to disable offer object.
        /// For example: if you already purchased NoAds as a part of the Pack, you want other NoAds offers to be disabled. 
        /// </summary>
        /// <returns></returns>
        public virtual bool CheckDisableState() { return false; }
    }
}
