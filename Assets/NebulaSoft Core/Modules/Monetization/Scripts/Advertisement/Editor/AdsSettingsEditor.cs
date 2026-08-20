using UnityEngine;
using UnityEditor;

namespace NebulaSoft
{
    [CustomEditor(typeof(AdsSettings))]
    public class AdsSettingsEditor : CustomInspector
    {
        private readonly EditorAdsContainer[] adsContainers = new EditorAdsContainer[]
        {
            new EditorDummyContainer("Dummy", "dummyContainer"),
        };

        protected override void OnEnable()
        {
            base.OnEnable();

            for (int i = 0; i < adsContainers.Length; i++)
            {
                adsContainers[i].Init(serializedObject);
            }

            serializedObject.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.Update();

            GUILayout.Space(8);

            for (int i = 0; i < adsContainers.Length; i++)
            {
                adsContainers[i].DrawContainer();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
