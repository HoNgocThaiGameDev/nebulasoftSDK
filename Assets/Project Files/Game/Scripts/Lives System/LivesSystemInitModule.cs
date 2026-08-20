using UnityEngine;

namespace NebulaSoft
{
    [RegisterModule("Lives System", core: false)]
    public class LivesSystemInitModule : InitModule
    {
        public override string ModuleName => "Lives System";

        [SerializeField] LivesData livesData;

        public override void CreateComponent()
        {
            if (livesData == null)
            {
                Debug.LogError("LivesData is not assigned in Project Init Settings", this);

                return;
            }

            LivesSystem.Init(livesData);
        }
    }
}