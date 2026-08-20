using UnityEngine;

namespace VoxelBusters.CoreLibrary
{
    /// <summary>
    /// This class is used to hide objects at runtime and show them only in the editor. As EditorOnly tag doesn't work for objects created at runtime, this class will be used.
    /// </summary>
    public class EditorOnlyObject : MonoBehaviour
    {
        [SerializeField]
        private bool m_allowInEditorRuntime = false;

        void Awake()
        {
            #if !UNITY_EDITOR
                Destroy(gameObject);
            #else
                if (!m_allowInEditorRuntime)
                    Destroy(gameObject);
            #endif
        }
    }
}