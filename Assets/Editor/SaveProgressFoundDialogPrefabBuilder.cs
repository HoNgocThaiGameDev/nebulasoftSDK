#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NebulaSoft.EditorTools
{
    public static class SaveProgressFoundDialogPrefabBuilder
    {
        private const string DialogPrefabPath = "Assets/Addon/UI/Prefabs/Shared/SaveProgressFoundDialog.prefab";
        private const string ProfilePopupPrefabPath = "Assets/Addon/UI/Prefabs/Pages/UI Profile Popup.prefab";

        [InitializeOnLoadMethod]
        private static void EnsurePrefabOnEditorLoad()
        {
            EditorApplication.delayCall -= EnsurePrefabAndReferences;
            EditorApplication.delayCall += EnsurePrefabAndReferences;
        }

        [MenuItem("Tools/Picture Puzzle/UI/Rebuild Save Progress Found Dialog")]
        public static void RebuildPrefabAndReferences()
        {
            EnsurePrefabAndReferences(forceRebuild: true);
        }

        public static void EnsurePrefabAndReferences()
        {
            EnsurePrefabAndReferences(forceRebuild: false);
        }

        private static void EnsurePrefabAndReferences(bool forceRebuild)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            SaveProgressFoundDialogView dialogPrefab = CreateOrUpdateDialogPrefab(forceRebuild);
            if (dialogPrefab != null)
                AttachDialogToProfilePopup(dialogPrefab.gameObject);
        }

        private static SaveProgressFoundDialogView CreateOrUpdateDialogPrefab(bool forceRebuild)
        {
            bool loadedExisting = AssetDatabase.LoadAssetAtPath<GameObject>(DialogPrefabPath) != null;
            if (loadedExisting && !forceRebuild)
                return UpgradeExistingDialogPrefab();

            GameObject root = loadedExisting ? PrefabUtility.LoadPrefabContents(DialogPrefabPath) : null;
            if (root == null)
                root = new GameObject("SaveProgressFoundDialog", typeof(RectTransform), typeof(SaveProgressFoundDialogView));

            ClearChildren(root.transform);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);

            SaveProgressFoundDialogView view = root.GetComponent<SaveProgressFoundDialogView>();
            if (view == null)
                view = root.AddComponent<SaveProgressFoundDialogView>();

            TextMeshProUGUI templateText = GetTemplateText();
            Sprite panelSprite = LoadSprite("Assets/Addon/UI/Sprites/ProfilePopup/Small_Popup_BG.png");
            Sprite cardSprite = LoadSprite("Assets/Addon/UI/Sprites/ProfilePopup/Small_Popup_BG.png");
            Sprite titleSprite = LoadSprite("Assets/Addon/UI/Sprites/ProfilePopup/Title_Ribbon_Medium.png");
            Sprite buttonSprite = LoadSprite("Assets/Addon/UI/Sprites/ProfilePopup/Retry_Green_Large.png");
            Sprite coinSprite = LoadSprite("Assets/Project Files/Game/Images/Currency/coin.png");

            GameObject panel = CreateImage("Panel", root.transform, panelSprite, Image.Type.Sliced);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            Center(panelRect, new Vector2(0f, 0f), new Vector2(760f, 1120f));

            GameObject titleRibbon = CreateImage("Title Ribbon", panel.transform, titleSprite, Image.Type.Sliced);
            Center(titleRibbon.GetComponent<RectTransform>(), new Vector2(0f, 540f), new Vector2(700f, 126f));
            CreateText("Title", titleRibbon.transform, "SAVE PROGRESS FOUND", 50f, Color.white, TextAlignmentOptions.Center, Vector2.zero, new Vector2(660f, 96f), templateText);

            CreateText("Message", panel.transform, "We found another saved\nversion of your game.\nChoose one:", 52f, TextColor(), TextAlignmentOptions.Center, new Vector2(0f, 355f), new Vector2(660f, 250f), templateText);

            CardRefs device = CreateCard(panel.transform, "Device Card", "Local Version", new Vector2(-185f, -180f), cardSprite, buttonSprite, coinSprite, templateText, true);
            CardRefs server = CreateCard(panel.transform, "Server Card", "Cloud Version", new Vector2(185f, -180f), cardSprite, buttonSprite, coinSprite, templateText, false);

            SerializedObject serializedView = new SerializedObject(view);
            Assign(serializedView, "deviceLevelText", device.LevelText);
            Assign(serializedView, "deviceCoinText", device.CoinText);
            Assign(serializedView, "deviceCoinSafeAmountText", device.CoinSafeAmountText);
            Assign(serializedView, "deviceTimestampText", device.TimestampText);
            Assign(serializedView, "deviceSelectButton", device.SelectButton);
            Assign(serializedView, "serverLevelText", server.LevelText);
            Assign(serializedView, "serverCoinText", server.CoinText);
            Assign(serializedView, "serverCoinSafeAmountText", server.CoinSafeAmountText);
            Assign(serializedView, "serverTimestampText", server.TimestampText);
            Assign(serializedView, "serverSelectButton", server.SelectButton);
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);
            EnsureFolder("Assets/Addon/UI/Prefabs/Shared");
            PrefabUtility.SaveAsPrefabAsset(root, DialogPrefabPath);

            if (loadedExisting)
                PrefabUtility.UnloadPrefabContents(root);
            else
                Object.DestroyImmediate(root);

            GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialogPrefabPath);
            return savedPrefab != null ? savedPrefab.GetComponent<SaveProgressFoundDialogView>() : null;
        }

        private static SaveProgressFoundDialogView UpgradeExistingDialogPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(DialogPrefabPath);
            if (root == null)
                return null;

            bool changed = false;
            try
            {
                Transform deviceCard = FindDeep(root.transform, "Device Card");
                Transform serverCard = FindDeep(root.transform, "Server Card");
                TextMeshProUGUI deviceLabel = deviceCard != null
                    ? deviceCard.Find("Collection Label")?.GetComponent<TextMeshProUGUI>()
                    : null;
                TextMeshProUGUI serverLabel = serverCard != null
                    ? serverCard.Find("Collection Label")?.GetComponent<TextMeshProUGUI>()
                    : null;
                TextMeshProUGUI deviceValue = deviceCard != null
                    ? deviceCard.Find("Collection Value")?.GetComponent<TextMeshProUGUI>()
                    : null;
                TextMeshProUGUI serverValue = serverCard != null
                    ? serverCard.Find("Collection Value")?.GetComponent<TextMeshProUGUI>()
                    : null;
                TextMeshProUGUI deviceTitle = deviceCard != null
                    ? deviceCard.Find("Card Title")?.GetComponent<TextMeshProUGUI>()
                    : null;
                TextMeshProUGUI serverTitle = serverCard != null
                    ? serverCard.Find("Card Title")?.GetComponent<TextMeshProUGUI>()
                    : null;

                changed |= SetTextIfChanged(deviceTitle, "Local Version");
                changed |= SetTextIfChanged(serverTitle, "Cloud Version");
                changed |= SetTextIfChanged(deviceLabel, "Progress Box");
                changed |= SetTextIfChanged(serverLabel, "Progress Box");

                SaveProgressFoundDialogView view = root.GetComponent<SaveProgressFoundDialogView>();
                if (view != null && deviceValue != null && serverValue != null)
                {
                    SerializedObject serializedView = new SerializedObject(view);
                    changed |= Assign(serializedView, "deviceCoinSafeAmountText", deviceValue);
                    changed |= Assign(serializedView, "serverCoinSafeAmountText", serverValue);
                    serializedView.ApplyModifiedPropertiesWithoutUndo();
                }

                if (changed)
                    PrefabUtility.SaveAsPrefabAsset(root, DialogPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DialogPrefabPath);
            return savedPrefab != null ? savedPrefab.GetComponent<SaveProgressFoundDialogView>() : null;
        }

        private static void AttachDialogToProfilePopup(GameObject dialogPrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ProfilePopupPrefabPath);
            if (root == null)
                return;

            try
            {
                Transform dialogRoot = FindDeep(root.transform, "Dialog Root");
                if (dialogRoot == null)
                    return;

                Transform oldConflict = dialogRoot.Find("Progress Conflict Dialog");
                if (oldConflict != null)
                    Object.DestroyImmediate(oldConflict.gameObject);

                Transform existing = dialogRoot.Find("Save Progress Found Dialog");
                SaveProgressFoundDialogView dialogView;
                if (existing == null)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(dialogPrefab, dialogRoot);
                    instance.name = "Save Progress Found Dialog";
                    Stretch(instance.GetComponent<RectTransform>());
                    instance.SetActive(false);
                    dialogView = instance.GetComponent<SaveProgressFoundDialogView>();
                }
                else
                {
                    dialogView = existing.GetComponent<SaveProgressFoundDialogView>();
                }

                UIProfilePopup profilePopup = root.GetComponent<UIProfilePopup>();
                if (profilePopup != null)
                {
                    SerializedObject serializedPopup = new SerializedObject(profilePopup);
                    Assign(serializedPopup, "saveProgressFoundDialog", dialogView);
                    serializedPopup.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, ProfilePopupPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

        }

        private static CardRefs CreateCard(Transform parent, string name, string title, Vector2 position, Sprite cardSprite, Sprite buttonSprite, Sprite coinSprite, TextMeshProUGUI templateText, bool device)
        {
            GameObject card = CreateImage(name, parent, cardSprite, Image.Type.Sliced);
            Center(card.GetComponent<RectTransform>(), position, new Vector2(330f, 730f));

            CreateText("Card Title", card.transform, title, 42f, device ? new Color32(24, 137, 36, 255) : TextColor(), TextAlignmentOptions.Center, new Vector2(0f, 300f), new Vector2(280f, 72f), templateText);
            CreateDivider(card.transform, new Vector2(0f, 252f), new Vector2(235f, 3f));
            CreateText("Level Label", card.transform, "Level:", 44f, TextColor(), TextAlignmentOptions.Center, new Vector2(0f, 188f), new Vector2(270f, 70f), templateText);
            TextMeshProUGUI levelValue = CreateText("Level Value", card.transform, "1", 70f, TextColor(), TextAlignmentOptions.Center, new Vector2(0f, 112f), new Vector2(270f, 92f), templateText);
            CreateText("Collection Label", card.transform, "Progress Box", 44f, TextColor(), TextAlignmentOptions.Center, new Vector2(0f, 24f), new Vector2(285f, 70f), templateText);
            TextMeshProUGUI coinSafeAmountValue = CreateText("Collection Value", card.transform, "0", 70f, TextColor(), TextAlignmentOptions.Center, new Vector2(0f, -56f), new Vector2(270f, 92f), templateText);

            GameObject coinPill = CreateImage("Coin Pill", card.transform, null, Image.Type.Sliced);
            Image pillImage = coinPill.GetComponent<Image>();
            pillImage.color = new Color32(236, 246, 255, 255);
            Center(coinPill.GetComponent<RectTransform>(), new Vector2(28f, -150f), new Vector2(190f, 58f));

            GameObject coinIcon = CreateImage("Coin Icon", card.transform, coinSprite, Image.Type.Simple);
            Center(coinIcon.GetComponent<RectTransform>(), new Vector2(-98f, -150f), new Vector2(74f, 74f));
            TextMeshProUGUI coinValue = CreateText("Coin Value", coinPill.transform, "0", 36f, TextColor(), TextAlignmentOptions.Center, new Vector2(15f, 0f), new Vector2(140f, 54f), templateText);

            CreateDivider(card.transform, new Vector2(0f, -215f), new Vector2(235f, 3f));
            TextMeshProUGUI timestamp = CreateText("Timestamp", card.transform, device ? "Uploaded Now" : "Last saved: --", 32f, TextColor(), TextAlignmentOptions.Center, new Vector2(0f, -270f), new Vector2(285f, 100f), templateText);

            Button selectButton = CreateButton("Select Button", card.transform, buttonSprite, "Select", new Vector2(0f, -335f), new Vector2(260f, 94f), templateText);

            return new CardRefs
            {
                LevelText = levelValue,
                CoinText = coinValue,
                CoinSafeAmountText = coinSafeAmountValue,
                TimestampText = timestamp,
                SelectButton = selectButton
            };
        }

        private static Button CreateButton(string name, Transform parent, Sprite sprite, string label, Vector2 position, Vector2 size, TextMeshProUGUI templateText)
        {
            GameObject go = CreateImage(name, parent, sprite, Image.Type.Sliced);
            Center(go.GetComponent<RectTransform>(), position, size);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            CreateText("Label", go.transform, label, 52f, Color.white, TextAlignmentOptions.Center, Vector2.zero, size, templateText);
            return button;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, Color color, TextAlignmentOptions alignment, Vector2 position, Vector2 size, TextMeshProUGUI templateText)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            Center(go.GetComponent<RectTransform>(), position, size);
            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;

            if (templateText != null)
            {
                label.font = templateText.font;
                label.fontSharedMaterial = templateText.fontSharedMaterial;
                label.fontStyle = templateText.fontStyle;
            }

            return label;
        }

        private static GameObject CreateImage(string name, Transform parent, Sprite sprite, Image.Type type)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = type;
            image.color = Color.white;
            image.raycastTarget = true;
            return go;
        }

        private static void CreateDivider(Transform parent, Vector2 position, Vector2 size)
        {
            GameObject divider = CreateImage("Divider", parent, null, Image.Type.Simple);
            Image image = divider.GetComponent<Image>();
            image.color = new Color32(219, 221, 231, 255);
            image.raycastTarget = false;
            Center(divider.GetComponent<RectTransform>(), position, size);
        }

        private static TextMeshProUGUI GetTemplateText()
        {
            GameObject loginPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Addon/UI/Prefabs/Shared/LoginResultDialog.prefab");
            return loginPrefab != null ? loginPrefab.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        }

        private static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Color32 TextColor()
        {
            return new Color32(83, 103, 158, 255);
        }

        private static bool Assign(SerializedObject serializedObject, string fieldName, Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(fieldName);
            if (property == null || property.objectReferenceValue == value)
                return false;

            property.objectReferenceValue = value;
            return true;
        }

        private static bool SetTextIfChanged(TextMeshProUGUI text, string value)
        {
            if (text == null || text.text == value)
                return false;

            text.text = value;
            return true;
        }

        private static void Center(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
                return null;

            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.GetChild(i).gameObject);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private struct CardRefs
        {
            public TextMeshProUGUI LevelText;
            public TextMeshProUGUI CoinText;
            public TextMeshProUGUI CoinSafeAmountText;
            public TextMeshProUGUI TimestampText;
            public Button SelectButton;
        }
    }
}
#endif
