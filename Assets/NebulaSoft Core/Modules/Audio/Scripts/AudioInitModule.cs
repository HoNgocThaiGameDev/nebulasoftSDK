#pragma warning disable 0649

using UnityEngine;

namespace NebulaSoft
{
    [RegisterModule("Audio Controller", core: true)]
    public class AudioInitModule : InitModule
    {
        public override string ModuleName => "Audio Controller";

        [SerializeField] AudioClips audioSettings;
        [SerializeField] int audioSourcesPoolSize = 4;

        [Header("3D")]
        [SerializeField] float maxDistance = 30;

        [Slider(0.0f, 360.0f)]
        [SerializeField] float spread = 180;
        [SerializeField] AnimationCurve rolloffCurve = new AnimationCurve(new Keyframe(0.0f, 1.0f), new Keyframe(1.0f, 0.0f));

        public override void CreateComponent()
        {
            AudioController.OverrideDefault3DAudioSettings(maxDistance, spread, rolloffCurve);
            AudioController.Init(audioSettings, audioSourcesPoolSize);
        }
    }
}