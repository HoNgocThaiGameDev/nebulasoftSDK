using System.Collections.Generic;
using NUnit.Framework;

namespace NebulaSoft.Tests
{
    public class FirebaseProgressSnapshotTests
    {
        [Test]
        public void ToSnapshot_KeepsCloudInventorySeparateFromLocalSource()
        {
            FirebasePlayerProgress cloud = new FirebasePlayerProgress
            {
                PlayerName = "Cloud Fox",
                AvatarIndex = 2,
                FrameIndex = 4,
                MaxReachedLevelIndex = 48,
                CoinBalance = 920,
                CoinSafeAmount = 230,
                PowerUpAmounts = new Dictionary<string, int>
                {
                    ["FreezeTimer"] = 2,
                    ["Hammer"] = 5,
                    ["Merge"] = 7,
                    ["FreeMovement"] = 9
                },
                HasProfile = true,
                HasLevelProgress = true,
                HasCoinBalance = true,
                HasCoinSafeAmount = true,
                HasPowerUpAmounts = true
            };

            PlayerProgressSnapshot card = cloud.ToSnapshot();
            cloud.PowerUpAmounts["Hammer"] = 0;

            Assert.That(card.PlayerName, Is.EqualTo("Cloud Fox"));
            Assert.That(card.CoinBalance, Is.EqualTo(920));
            Assert.That(card.CoinSafeAmount, Is.EqualTo(230));
            Assert.That(card.PowerUpAmounts["Hammer"], Is.EqualTo(5));
            Assert.That(card.HasPowerUpAmounts, Is.True);
        }
    }
}
