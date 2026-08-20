using UnityEngine;

namespace NebulaSoft
{
    [RegisterModule("Haptic")]
    public class HapticInitModule : InitModule
    {
        [SerializeField] bool verboseLogging = false;

        public override string ModuleName => "Haptic";

        public override void CreateComponent()
        {
            if (verboseLogging)
                Haptic.EnableVerboseLogging();

            Haptic.Init();
        }
    }
}
