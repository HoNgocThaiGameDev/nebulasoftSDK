using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NebulaSoft
{
    public class HapticTestController : MonoBehaviour
    {
        [SerializeField] HapticHandler longHapticHandler;

        public void Play()
        {
            Haptic.Play(0.3f);
        }

        public void PlayLong()
        {
            longHapticHandler.Play();
        }

        public void PlayLight()
        {
            Haptic.Play(Haptic.HAPTIC_LIGHT);
        }

        public void PlayMedium()
        {
            Haptic.Play(Haptic.HAPTIC_MEDIUM);
        }

        public void PlayHard()
        {
            Haptic.Play(Haptic.HAPTIC_HARD);
        }

        public void PlayPattern()
        {
            Haptic.Play(Haptic.PATTERN_LIGHT);
        }
    }
}
