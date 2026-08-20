using System.Collections.Generic;
using UnityEngine;

namespace NebulaSoft
{
    [StaticUnload]
    public static class AudioController
    {
        private static List<AudioSourceCase> audioSourcesPool;

        private static AudioClips audioClips;
        public static AudioClips AudioClips => audioClips;

        private static AudioListener audioListener;
        public static AudioListener AudioListener => audioListener;

        private static AudioSave save;

        // Default 3D audio settings
        private static float maxDistance = 30;
        private static float spread = 180;
        private static AnimationCurve rolloffCurve = new AnimationCurve(new Keyframe(0.0f, 1.0f), new Keyframe(1.0f, 0.0f));

        public static OnVolumeChangedCallback VolumeChanged;

        private static Dictionary<AudioType, float> volumeDictionary;

        public static void Init(AudioClips audioClips, int audioSourcesPoolSize)
        {
            if (audioClips == null)
            {
                Debug.LogError("[AudioController]: Audio Clips is NULL! Please assign audio clips scriptable on Audio Controller script.");

                return;
            }

            // Get volume save
            save = SaveController.GetSaveObject<AudioSave>("audio");

            volumeDictionary = new Dictionary<AudioType, float>();
            if(save.VolumeDatas != null)
            {
                foreach (AudioSave.VolumeData volumeData in save.VolumeDatas)
                {
                    volumeDictionary.Add(volumeData.AudioType, volumeData.Volume);
                }
            }

            // Create audio listener
            CreateAudioListener();

            AudioController.audioClips = audioClips;

            //Create audio source objects
            audioSourcesPool = new List<AudioSourceCase>();
            for (int i = 0; i < audioSourcesPoolSize; i++)
            {
                audioSourcesPool.Add(new AudioSourceCase());
            }
        }

        public static void OverrideDefault3DAudioSettings(float maxDistance, float spread, AnimationCurve rolloffCurve)
        {
            AudioController.maxDistance = maxDistance;
            AudioController.spread = spread;
            AudioController.rolloffCurve = rolloffCurve;
        }

        public static void ApplyDefaultSettings(ref AudioSource audioSource)
        {
            audioSource.maxDistance = maxDistance;
            audioSource.spread = spread;
            audioSource.rolloffMode = AudioRolloffMode.Custom;
            audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, rolloffCurve);
        }

        private static void CreateAudioListener()
        {
            if (audioListener != null)
                return;

            // Create game object for listener
            GameObject listenerObject = new GameObject("[AUDIO LISTENER]");
            listenerObject.transform.position = Vector3.zero;

            // Mark as non-destroyable
            GameObject.DontDestroyOnLoad(listenerObject);

            // Add listener component to created object
            audioListener = listenerObject.AddComponent<AudioListener>();
        }

        public static Transform AttachAudioListener(Transform parentObject)
        {
            if (audioListener == null)
                CreateAudioListener();

            Transform audioListenerTransform = audioListener.transform;
            audioListenerTransform.SetParent(parentObject);
            audioListenerTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            return audioListenerTransform;
        }

        public static void ResetAudioLisenerParent()
        {
            if (audioListener == null) return;

            audioListener.transform.SetParent(null);

            GameObject.DontDestroyOnLoad(audioListener.gameObject);
        }

        /// <summary>
        /// Stop all active streams
        /// </summary>
        public static void ReleaseSources()
        {
            foreach(AudioSourceCase sourceCase in audioSourcesPool)
            {
                if(sourceCase.IsPlaying)
                {
                    sourceCase.AudioSource.Stop();
                }
            }
        }

        public static void PlaySound(AudioClip clip, float volumePercentage = 1.0f, float pitch = 1.0f, float minDelay = 0f)
        {
            if (clip == null)
                Debug.LogError("[AudioController]: Audio clip is null");

            AudioSourceCase sourceCase = GetAudioSource();

            AudioSource source = sourceCase.AudioSource;
            source.spatialBlend = 0.0f; // 2D sound
            source.pitch = pitch;

            sourceCase.Play(clip, volumePercentage, AudioType.Sound);
        }

        public static void PlaySound(AudioClip clip, Vector3 position, float volumePercentage = 1.0f, float pitch = 1.0f, float minDelay = 0f)
        {
            if (clip == null)
                Debug.LogError("[AudioController]: Audio clip is null");

            AudioSourceCase sourceCase = GetAudioSource();

            AudioSource source = sourceCase.AudioSource;
            source.transform.position = position;
            source.spatialBlend = 1.0f; // 3D sound
            source.pitch = pitch;

            sourceCase.Play(clip, volumePercentage, AudioType.Sound);
        }

        private static AudioSourceCase GetAudioSource()
        {
            foreach(AudioSourceCase audioSource in audioSourcesPool)
            {
                if (!audioSource.IsPlaying)
                {
                    return audioSource;
                }
            }

            AudioSourceCase createdSource = new AudioSourceCase();
            audioSourcesPool.Add(createdSource);

            return createdSource;
        }

        public static void SetVolume(AudioType audioType, float volume)
        {
            foreach (AudioSourceCase audioSource in audioSourcesPool)
            {
                audioSource.OverrideVolume(audioType, volume);
            }

            volumeDictionary[audioType] = volume;

            SaveController.MarkAsSaveIsRequired();

            VolumeChanged?.Invoke(audioType, volume);
        }

        public static float GetVolume(AudioType audioType)
        {
            if (volumeDictionary != null && volumeDictionary.ContainsKey(audioType))
                return volumeDictionary[audioType];

            return 1.0f;
        }

        public static bool IsAudioTypeActive(AudioType audioType)
        {
            return GetVolume(audioType) == 1.0f;
        }

        private static void UnloadStatic()
        {
            audioSourcesPool = null;

            audioClips = null;
            audioListener = null;

            save = null;

            volumeDictionary = null;

            VolumeChanged = null;
        }

        public delegate void OnVolumeChangedCallback(AudioType audioType, float volume);
    }

    public enum AudioType
    {
        Music = 0,
        Sound = 1
    }
}
