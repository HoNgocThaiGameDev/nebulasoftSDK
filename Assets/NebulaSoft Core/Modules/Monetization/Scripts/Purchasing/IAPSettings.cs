using UnityEngine;

namespace NebulaSoft
{
    public class IAPSettings : ScriptableObject
    {
        [Group("Settings")]
        [SerializeField] bool useFakeStore;
        public bool UseFakeStore => useFakeStore;

        [Group("Settings")]
        [SerializeField] FakeStoreMode fakeStoreMode;
        public FakeStoreMode FakeStoreMode => fakeStoreMode;

        [SerializeField, Hide] IAPItem[] storeItems;
        public IAPItem[] StoreItems => storeItems;
    }
}