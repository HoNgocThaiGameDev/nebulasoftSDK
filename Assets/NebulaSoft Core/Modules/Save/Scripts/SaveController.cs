using System;
using System.Collections;
using UnityEngine;
using System.Threading;

namespace NebulaSoft
{
    [StaticUnload]
    public static class SaveController
    {
        private const string SAVE_FILE_NAME = "save";

        private static GlobalSave globalSave;

        private static bool isSaveLoaded;
        public static bool IsSaveLoaded => isSaveLoaded;

        private static bool isSaveRequired;

        public static float GameTime => globalSave.GameTime;

        public static DateTime LastExitTime => globalSave.LastExitTime;

        public static event SimpleCallback OnSaveLoaded;

        public static void Init(float autoSaveDelay, bool clearSave = false, float overrideTime = -1f)
        {
            Serializer.Init();

            GameObject saveCallbackReciever = new GameObject("[SAVE CALLBACK RECIEVER]");
            saveCallbackReciever.hideFlags = HideFlags.HideInHierarchy;

            GameObject.DontDestroyOnLoad(saveCallbackReciever);

            UnityCallbackReciever unityCallbackReciever = saveCallbackReciever.AddComponent<UnityCallbackReciever>();

            if (clearSave)
            {
                InitClear(overrideTime != -1f ? overrideTime : Time.time);
            }
            else
            {
                Load(overrideTime != -1f ? overrideTime : Time.time);
            }

            if (autoSaveDelay > 0)
            {
                // Enable auto-save coroutine
                unityCallbackReciever.StartCoroutine(AutoSaveCoroutine(autoSaveDelay));
            }
        }

        public static void UpdateTime(float time)
        {
            globalSave.Time = time;
        }

        public static T GetSaveObject<T>(int hash) where T : ISaveObject, new()
        {
            if (!isSaveLoaded)
            {
                Debug.LogError("Save controller has not been initialized");
                return default;
            }

            return globalSave.GetSaveObject<T>(hash);
        }

        public static T GetSaveObject<T>() where T : ISaveObject, new()
        {
            return GetSaveObject<T>(typeof(T).ToString().GetHashCode());
        }

        public static T GetSaveObject<T>(string uniqueName) where T : ISaveObject, new()
        {
            return GetSaveObject<T>(uniqueName.GetHashCode());
        }

        private static void InitClear(float time)
        {
            globalSave = new GlobalSave();
            globalSave.Init(time);

            Debug.Log("[Save Controller]: Created clear save!");

            isSaveLoaded = true;
        }

        private static void Load(float time)
        {
            if (isSaveLoaded)
                return;

            // Try to read and deserialize file or create new one
            globalSave = BaseSaveWrapper.ActiveWrapper.Load(SAVE_FILE_NAME);

            globalSave.Init(time);

            Debug.Log("[Save Controller]: Save is loaded!");

            isSaveLoaded = true;

            OnSaveLoaded?.Invoke();
        }

        public static void Save(bool forceSave = false, bool useThreads = true)
        {
            if (!forceSave && !isSaveRequired) return;
            if (globalSave == null) return;

            globalSave.Flush(true);

            BaseSaveWrapper saveWrapper = BaseSaveWrapper.ActiveWrapper;
            if(useThreads && saveWrapper.UseThreads())
            {
                Thread saveThread = new Thread(() => BaseSaveWrapper.ActiveWrapper.Save(globalSave, SAVE_FILE_NAME));
                saveThread.Start();
            }
            else
            {
                BaseSaveWrapper.ActiveWrapper.Save(globalSave, SAVE_FILE_NAME);
            }

            Debug.Log("[Save Controller]: Game is saved!");

            isSaveRequired = false;
        }

        public static void SaveCustom(GlobalSave globalSave)
        {
            if(globalSave != null)
            {
                globalSave.Flush(false);

                BaseSaveWrapper.ActiveWrapper.Save(globalSave, SAVE_FILE_NAME);
            }
        }

        public static void MarkAsSaveIsRequired()
        {
            isSaveRequired = true;
        }

        private static IEnumerator AutoSaveCoroutine(float saveDelay)
        {
            WaitForSeconds waitForSeconds = new WaitForSeconds(saveDelay);

            while (true)
            {
                yield return waitForSeconds;

                Save();
            }
        }

        public static void PresetsSave(string fullFileName)
        {
            globalSave.Flush(false);

            BaseSaveWrapper.ActiveWrapper.Save(globalSave, fullFileName);
        }

        public static void Info()
        {
            globalSave.Info();
        }

        public static void DeleteSaveFile()
        {
            BaseSaveWrapper.ActiveWrapper.Delete(SAVE_FILE_NAME);
        }

        public static GlobalSave GetGlobalSave()
        {
            GlobalSave tempGlobalSave = BaseSaveWrapper.ActiveWrapper.Load(SAVE_FILE_NAME);

            tempGlobalSave.Init(Time.time);

            return tempGlobalSave;
        }

        private static void UnloadStatic()
        {
            globalSave = null;

            isSaveLoaded = false;
            isSaveRequired = false;

            OnSaveLoaded = null;
        }

        private class UnityCallbackReciever : MonoBehaviour
        {
            private void OnDestroy()
            {
#if UNITY_EDITOR
                SaveController.Save(true);
#endif
            }

            private void OnApplicationFocus(bool focus)
            {
#if !UNITY_EDITOR
                if(!focus) SaveController.Save();
#endif
            }
        }
    }
}