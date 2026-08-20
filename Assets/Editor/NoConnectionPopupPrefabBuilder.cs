#if UNITY_EDITOR
using NebulaSoft;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NebulaSoft.EditorTools
{
    public static class NoConnectionPopupPrefabBuilder
    {
        private const string PrefabPath = "Assets/Addon/UI/Prefabs/Shared/NoConnectionPopup.prefab";
        private const string SpriteRoot = "Assets/Addon/UI/Sprites/ProfilePopup/Extracted/";
        private const string FontPath = "Assets/TextMesh Pro/Fonts/FredokaOne/FredokaOne 50/FredokaOne 50.asset";

        [MenuItem("Tools/Recovery Runtime/Rebuild No Connection Popup")]
        public static void Build()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(PrefabPath);
            }

            GameObject root = new GameObject("NoConnectionPopup", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(NetworkCheckPopup));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Image overlay = AddImage("Dim Overlay", root.transform, null, new Color(0f, 0f, 0f, 0.72f), Vector2.zero, Vector2.zero);
            Stretch(overlay.rectTransform);
            overlay.raycastTarget = true;

            RectTransform panel = AddRect("Popup Panel", root.transform, new Vector2(0f, 120f), new Vector2(880f, 900f));

            Image background = AddImage("Dialog Background", panel, Sprite("02_Dialog_BG"), Color.white, new Vector2(0f, -32f), new Vector2(860f, 760f));
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 1f;

            Image title = AddImage("Title Label", panel, Sprite("03_Title_Label"), Color.white, new Vector2(0f, 365f), new Vector2(620f, 145f));
            title.type = Image.Type.Sliced;
            title.pixelsPerUnitMultiplier = 1f;
            AddText("Title Text", title.transform, "NO CONNECTION", 56f, Color.white, Vector2.zero, new Vector2(560f, 90f));

            GameObject closeButtonObject = AddButton("Close Button", panel, Sprite("04_Close_Button"), new Vector2(378f, 340f), new Vector2(118f, 122f), out _);
            closeButtonObject.GetComponent<Image>().preserveAspect = true;
            SerializedObject closeButton = new SerializedObject(closeButtonObject.AddComponent<PopupCloseButton>());
            closeButton.FindProperty("targetCanvas").objectReferenceValue = canvas;
            closeButton.ApplyModifiedPropertiesWithoutUndo();

            Image iconCard = AddImage("Icon Card", panel, null, Color.white, new Vector2(0f, 118f), new Vector2(700f, 250f));
            iconCard.raycastTarget = false;
            Shadow iconCardShadow = iconCard.gameObject.AddComponent<Shadow>();
            iconCardShadow.effectColor = new Color(0.25f, 0.31f, 0.48f, 0.18f);
            iconCardShadow.effectDistance = new Vector2(0f, -6f);
            Outline iconCardOutline = iconCard.gameObject.AddComponent<Outline>();
            iconCardOutline.effectColor = new Color(0.72f, 0.76f, 0.88f, 0.45f);
            iconCardOutline.effectDistance = new Vector2(2f, -2f);

            Image icon = AddImage("No Connection Icon", iconCard.transform, Sprite("06_NoConnection_Icon"), Color.white, Vector2.zero, new Vector2(320f, 225f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            GameObject textObject = new GameObject("Text Content", typeof(RectTransform));
            textObject.transform.SetParent(panel, false);
            RectTransform textObjectRect = textObject.GetComponent<RectTransform>();
            textObjectRect.anchoredPosition = new Vector2(0f, -104f);
            textObjectRect.sizeDelta = new Vector2(760f, 190f);
            AddText("Message Text", textObject.transform, "Please check your\ninternet connection.", 58f, new Color(0.325f, 0.404f, 0.62f, 1f), Vector2.zero, new Vector2(760f, 190f));

            GameObject okButton = AddButton("Retry Button", panel, Sprite("08_OK_Button"), new Vector2(0f, -318f), new Vector2(430f, 145f), out Button retryButton);
            Image okImage = okButton.GetComponent<Image>();
            okImage.type = Image.Type.Sliced;
            okImage.pixelsPerUnitMultiplier = 1f;
            AddText("OK Text", okButton.transform, "OK", 70f, Color.white, Vector2.zero, new Vector2(350f, 105f));

            GameObject loadingObject = new GameObject("Simple Loading", typeof(RectTransform));
            loadingObject.transform.SetParent(panel, false);
            RectTransform loadingRect = loadingObject.GetComponent<RectTransform>();
            loadingRect.anchoredPosition = new Vector2(0f, -104f);
            loadingRect.sizeDelta = new Vector2(760f, 190f);
            AddText("Loading Text", loadingObject.transform, "Checking...", 58f, new Color(0.325f, 0.404f, 0.62f, 1f), Vector2.zero, new Vector2(760f, 190f));
            loadingObject.SetActive(false);

            SerializedObject serializedPopup = new SerializedObject(root.GetComponent<NetworkCheckPopup>());
            serializedPopup.FindProperty("retryButton").objectReferenceValue = retryButton;
            serializedPopup.FindProperty("textObject").objectReferenceValue = textObject;
            serializedPopup.FindProperty("loadingObject").objectReferenceValue = loadingObject;
            serializedPopup.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[NoConnectionPopupPrefabBuilder] Rebuilt " + PrefabPath);
        }

        private static RectTransform AddRect(string name, Transform parent, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image AddImage(string name, Transform parent, Sprite sprite, Color color, Vector2 anchoredPosition, Vector2 size)
        {
            RectTransform rect = AddRect(name, parent, anchoredPosition, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static GameObject AddButton(string name, Transform parent, Sprite sprite, Vector2 anchoredPosition, Vector2 size, out Button button)
        {
            Image image = AddImage(name, parent, sprite, Color.white, anchoredPosition, size);
            image.raycastTarget = true;
            button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return image.gameObject;
        }

        private static TextMeshProUGUI AddText(string name, Transform parent, string text, float fontSize, Color color, Vector2 anchoredPosition, Vector2 size)
        {
            RectTransform rect = AddRect(name, parent, anchoredPosition, size);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = false;
            label.raycastTarget = false;
            return label;
        }

        private static Sprite Sprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + name + ".png");
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
#endif
