using System.Collections.Generic;
using UnityEngine;

namespace NebulaSoft
{
    public class SkinController : MonoBehaviour, ISkinsProvider
    {
        public static SkinController Instance { get; private set; }

        [UnpackNested]
        [SerializeField] SkinsHandler handler;
        public SkinsHandler Handler => handler;

        private SkinControllerSave save;

        private Dictionary<AbstractSkinDatabase, ISkinData> selectedSkins;

        public static event SkinCallback SkinUnlocked;
        public static event SkinCallback SkinSelected;

        public delegate void SkinCallback(ISkinData skinData);

        public void Init()
        {
            Instance = this;

            save = SaveController.GetSaveObject<SkinControllerSave>("Skin Controller Save");

            selectedSkins = new Dictionary<AbstractSkinDatabase, ISkinData>();

            for (int i = 0; i < handler.ProvidersCount; i++)
            {
                AbstractSkinDatabase provider = handler.GetSkinsProvider(i);

                InitProvider(provider);
            }

            UpdateSave();
        }

        private void InitProvider(AbstractSkinDatabase provider)
        {
            provider.Init();

            for (int i = 0; i < provider.SkinsCount; i++)
            {
                ISkinData skinData = provider.GetSkinData(i);

                for (int j = 0; j < save.SelectedSkinsCount; j++)
                {
                    int selectedSkinHash = save.GetSelectedSkin(j);

                    if (skinData.Hash == selectedSkinHash)
                    {
                        selectedSkins.Add(provider, skinData);

                        return;
                    }
                }
            }

            UnlockAndSelectDefaultSkin(provider);
        }

        public ISkinData GetSelectedSkin<T>() where T : AbstractSkinDatabase
        {
            AbstractSkinDatabase provider = GetProvider<T>();

            return GetSelectedSkin(provider);
        }

        public List<ISkinData> GetUnlockedSkins<T>() where T : AbstractSkinDatabase
        {
            AbstractSkinDatabase provider = GetProvider<T>();

            List<ISkinData> unlockedSkins = new List<ISkinData>();
            if (provider != null)
            {
                for(int i = 0; i < provider.SkinsCount; i++)
                {
                    ISkinData skin = provider.GetSkinData(i);
                    if(skin.IsUnlocked)
                    {
                        unlockedSkins.Add(skin);
                    }
                }
            }

            return unlockedSkins;
        }

        public ISkinData GetRandomSkin<T>() where T : AbstractSkinDatabase
        {
            AbstractSkinDatabase provider = GetProvider<T>();

            if (provider != null)
            {
                return provider.GetSkinData(Random.Range(0, provider.SkinsCount));
            }

            return null;
        }

        private ISkinData GetSelectedSkin(AbstractSkinDatabase provider)
        {
            if (provider == null || !selectedSkins.ContainsKey(provider)) return null;

            return selectedSkins[provider];
        }

        public bool IsSkinSelected(string skinId)
        {
            ISkinData skinData = GetSkinData(skinId);

            if (skinData == null) return false;

            return IsSkinSelected(skinData);
        }

        public bool IsSkinSelected(ISkinData skinData)
        {
            foreach (ISkinData selectedSkin in selectedSkins.Values)
            {
                if(skinData == selectedSkin) return true;
            }

            return false;
        }

        private ISkinData UnlockAndSelect<T>() where T : AbstractSkinDatabase
        {
            AbstractSkinDatabase provider = GetProvider<T>();

            if (provider == null) return null;

            return UnlockAndSelectDefaultSkin(provider);
        }

        private ISkinData UnlockAndSelectDefaultSkin(AbstractSkinDatabase provider)
        {
            if (provider.SkinsCount == 0) return null;

            ISkinData defaultSkin = provider.GetSkinData(0);

            defaultSkin.Unlock();

            if (selectedSkins.ContainsKey(provider))
            {
                selectedSkins[provider] = defaultSkin;
            }
            else
            {
                selectedSkins.Add(provider, defaultSkin);
            }

            return defaultSkin;
        }

        public void SelectSkin(string skinId)
        {
            ISkinData skinData = GetSkinData(skinId);

            if (skinData != null)
            {
                SelectSkin(skinData);
            }
        }

        public void SelectSkin(ISkinData data)
        {
            AbstractSkinDatabase provider = data.SkinsProvider;

            if (selectedSkins.ContainsKey(provider))
            {
                selectedSkins[provider] = data;
            }
            else
            {
                selectedSkins.Add(provider, data);
            }

            UpdateSave();

            SkinSelected?.Invoke(data);
        }

        public void UnlockSkin(string skinId, bool select = false)
        {
            ISkinData skinData = GetSkinData(skinId);

            if (skinData != null)
            {
                UnlockSkin(skinData, select);
            }
        }

        public void UnlockSkin(ISkinData skinData, bool select = false)
        {
            skinData.Unlock();
            SkinUnlocked?.Invoke(skinData);

            if (select)
            {
                SelectSkin(skinData);
            }
        }

        public ISkinData GetSkinData(string skinId)
        {
            int hash = skinId.GetHashCode();

            for (int i = 0; i < handler.ProvidersCount; i++)
            {
                AbstractSkinDatabase provider = handler.GetSkinsProvider(i);

                for (int j = 0; j < provider.SkinsCount; j++)
                {
                    ISkinData skinData = provider.GetSkinData(j);

                    if (skinData.Hash == hash)
                    {
                        return skinData;
                    }
                }
            }

            Debug.LogError($"[Skin Controller] There are no skin with id:'{skinId}' in any connected skin databases");

            return null;
        }

        public bool IsSkinUnlocked(string skinId)
        {
            int hash = skinId.GetHashCode();

            for (int i = 0; i < handler.ProvidersCount; i++)
            {
                AbstractSkinDatabase provider = handler.GetSkinsProvider(i);

                for (int j = 0; j < provider.SkinsCount; j++)
                {
                    ISkinData skin = provider.GetSkinData(j);

                    if (skin.Hash == hash)
                    {
                        return skin.IsUnlocked;
                    }
                }
            }

            return false;
        }

        private AbstractSkinDatabase GetProvider<T>() where T : AbstractSkinDatabase
        {
            for (int i = 0; i < handler.ProvidersCount; i++)
            {
                AbstractSkinDatabase provider = handler.GetSkinsProvider(i);

                if (provider is T)
                {
                    return provider;
                }
            }

            return null;
        }

        private void UpdateSave()
        {
            int[] selectedHashes = new int[selectedSkins.Count];

            int i = 0;

            foreach (ISkinData data in selectedSkins.Values)
            {
                selectedHashes[i++] = data.Hash;
            }

            save.Update(selectedHashes);
        }
    }

    [System.Serializable]
    public class SkinControllerSave : ISaveObject
    {
        [SerializeField] int[] selectedSkins;

        public int SelectedSkinsCount => selectedSkins != null ? selectedSkins.Length : 0;

        public int GetSelectedSkin(int index)
        {
            return selectedSkins[index];
        }

        public void Update(int[] newSelectedSkins)
        {
            selectedSkins = newSelectedSkins;
        }

        public void Flush()
        {

        }
    }
}
