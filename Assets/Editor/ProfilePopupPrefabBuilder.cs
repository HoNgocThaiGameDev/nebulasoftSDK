#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using NebulaSoft;

namespace RecoveryRuntime.EditorTools
{
    public static class ProfilePopupPrefabBuilder
    {
        private const string ArtFolder = "Assets/Addon/UI/Sprites/ProfilePopup/";
        private const string PrefabPath = "Assets/Addon/UI/Prefabs/Pages/UI Profile Popup.prefab";
        private const string FontPath = "Assets/TextMesh Pro/Fonts/FredokaOne/FredokaOne 50/FredokaOne 50.asset";
        private const string RoundedContentAreaPath = ArtFolder + "Lavender_Content_Area_Rounded.png";

        private static TMP_FontAsset font;

        [MenuItem("Tools/Recovery Runtime/Rebuild Profile Popup")]
        public static void Build()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            EnsureAssetFolder("Assets/Addon/UI/Prefabs/Pages");
            EnsureRoundedContentAreaSprite();

            GameObject root = BuildPrefabRoot();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            InstallInMenuScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Profile popup rebuilt and installed: " + PrefabPath);
        }

        private static GameObject BuildPrefabRoot()
        {
            GameObject root = UIObject("UI Profile Popup", null);
            Stretch(root.GetComponent<RectTransform>());

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32760;
            root.AddComponent<GraphicRaycaster>();
            CanvasGroup rootGroup = root.AddComponent<CanvasGroup>();
            rootGroup.alpha = 0f;
            UIProfilePopup view = root.AddComponent<UIProfilePopup>();

            Image dim = AddImage("Dim Overlay", root.transform, Sprite("Dim_Overlay"));
            Stretch(dim.rectTransform);
            dim.color = new Color(0f, 0f, 0f, 0.72f);
            dim.raycastTarget = true;

            GameObject panel = UIObject("Profile Panel", dim.transform);
            SetRect(panel.GetComponent<RectTransform>(), Vector2.zero, new Vector2(810f, 1460f));
            Image panelBg = panel.AddComponent<Image>();
            panelBg.sprite = Sprite("Main_Profile_Popup_BG");
            panelBg.type = Image.Type.Sliced;
            panelBg.raycastTarget = true;

            AddImageRect("Title Ribbon", panel.transform, Sprite("Title_Ribbon_Medium"), new Vector2(0f, 700f), new Vector2(500f, 130f), Image.Type.Sliced);
            AddText("Title", panel.transform, "PROFILE", 62f, new Vector2(0f, 704f), new Vector2(460f, 100f), Color.white);

            Button closeButton = AddIconButton("Close Button", panel.transform, Sprite("Close_Red"), new Vector2(375f, 665f), new Vector2(96f, 96f));
            AnchorTopRight(closeButton.GetComponent<RectTransform>(), new Vector2(-20f, -30f));

            Image previewSlot = AddImageRect("Preview Slot", panel.transform, Sprite("Avatar_Thumbnail_Slot"), new Vector2(-240f, 510f), new Vector2(170f, 170f), Image.Type.Sliced);
            previewSlot.raycastTarget = false;
            Image previewAvatar = AddImageRect("Preview Avatar", previewSlot.transform, Sprite("Avatar_06"), Vector2.zero, new Vector2(158f, 158f), Image.Type.Simple);
            previewAvatar.preserveAspect = true;
            Image previewFrame = AddImageRect("Preview Frame", previewSlot.transform, Sprite("Frame_06"), Vector2.zero, new Vector2(170f, 170f), Image.Type.Simple);
            previewFrame.preserveAspect = true;

            Image inputBg = AddImageRect("Name Field", panel.transform, Sprite("Name_Input_Field"), new Vector2(80f, 520f), new Vector2(430f, 104f), Image.Type.Sliced);
            TMP_InputField input = inputBg.gameObject.AddComponent<TMP_InputField>();
            GameObject textArea = UIObject("Text Area", inputBg.transform);
            SetRect(textArea.GetComponent<RectTransform>(), Vector2.zero, new Vector2(360f, 90f));
            textArea.AddComponent<RectMask2D>();
            TextMeshProUGUI nameText = AddText("Text", textArea.transform, "Player2eea94", 42f, Vector2.zero, new Vector2(350f, 86f), new Color32(83, 103, 158, 255));
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
            input.textViewport = textArea.GetComponent<RectTransform>();
            input.textComponent = nameText;
            input.text = "Player2eea94";
            input.interactable = false;
            input.lineType = TMP_InputField.LineType.SingleLine;

            Button editButton = AddIconButton("Edit Name Button", panel.transform, Sprite("Edit_Green"), new Vector2(306f, 520f), new Vector2(92f, 92f));
            Button saveProgressButton = AddTextButton("Save Progress Button", panel.transform, Sprite("CTA_Blue_Large"), "Save progress", 52f, new Vector2(0f, 355f), new Vector2(650f, 125f), Color.white);

            Image avatarTabBg;
            Image frameTabBg;
            Button avatarTab = AddTab("Avatar Tab", panel.transform, "Avatar", true, new Vector2(-180f, 205f), out avatarTabBg);
            Button frameTab = AddTab("Frame Tab", panel.transform, "Frame", false, new Vector2(180f, 205f), out frameTabBg);

            Image contentBg = AddImageRect("Content Area", panel.transform, Sprite("Lavender_Content_Area_Rounded"), new Vector2(0f, -205f), new Vector2(700f, 740f), Image.Type.Sliced);
            contentBg.raycastTarget = false;

            List<ProfileSelectableItem> avatarItems;
            List<ProfileSelectableItem> frameItems;
            GameObject avatarContent = BuildGrid("Avatar Content", contentBg.transform, true, out avatarItems);
            GameObject frameContent = BuildGrid("Frame Content", contentBg.transform, false, out frameItems);
            frameContent.SetActive(false);

            Button saveButton = AddTextButton("Save Button", panel.transform, Sprite("Save_Disabled_Gray_Large"), "Save", 58f, new Vector2(0f, -650f), new Vector2(420f, 125f), Color.white);

            Image toast = AddImageRect("Toast Banner", root.transform, Sprite("Toast_Status_Banner"), new Vector2(0f, 40f), new Vector2(650f, 112f), Image.Type.Sliced);
            CanvasGroup toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            AddText("Toast Text", toast.transform, "Data saved!", 48f, Vector2.zero, new Vector2(600f, 90f), Color.white);
            toast.gameObject.SetActive(false);

            CanvasGroup dialogGroup;
            GameObject dialogRoot = BuildDialogs(root.transform, out dialogGroup);
            GameObject saveProgressDialog = dialogRoot.transform.Find("Save Progress Dialog").gameObject;
            GameObject conflictDialog = dialogRoot.transform.Find("Progress Conflict Dialog").gameObject;
            GameObject loginFailedDialog = dialogRoot.transform.Find("Login Failed Dialog").gameObject;
            GameObject loginSuccessDialog = dialogRoot.transform.Find("Login Successful Dialog").gameObject;

            SerializedObject so = new SerializedObject(view);
            Set(so, "registeredElements", new UnityEngine.Object[0]);
            Set(so, "popupRoot", panel.GetComponent<RectTransform>());
            Set(so, "dimOverlayImage", dim);
            Set(so, "rootCanvasGroup", rootGroup);
            Set(so, "dialogCanvasGroup", dialogGroup);
            Set(so, "dialogRoot", dialogRoot.GetComponent<RectTransform>());
            Set(so, "saveProgressDialog", saveProgressDialog);
            Set(so, "conflictDialog", conflictDialog);
            Set(so, "loginFailedDialog", loginFailedDialog);
            Set(so, "loginSuccessDialog", loginSuccessDialog);
            Set(so, "toastCanvasGroup", toastGroup);
            Set(so, "toastRoot", toast.rectTransform);
            Set(so, "closeButton", closeButton);
            Set(so, "editNameButton", editButton);
            Set(so, "saveProgressButton", saveProgressButton);
            Set(so, "saveButton", saveButton);
            Set(so, "nameInputField", input);
            Set(so, "nameText", nameText);
            Set(so, "previewAvatarImage", previewAvatar);
            Set(so, "previewFrameImage", previewFrame);
            Set(so, "saveButtonImage", saveButton.GetComponent<Image>());
            Set(so, "saveButtonInactiveSprite", Sprite("Save_Disabled_Gray_Large"));
            Set(so, "saveButtonActiveSprite", Sprite("SocialLogin_Green_Large"));
            Set(so, "avatarTabButton", avatarTab);
            Set(so, "frameTabButton", frameTab);
            Set(so, "avatarTabBackground", avatarTabBg);
            Set(so, "frameTabBackground", frameTabBg);
            Set(so, "tabActiveSprite", Sprite("Tab_Active"));
            Set(so, "tabInactiveSprite", Sprite("Tab_Inactive"));
            Set(so, "avatarContent", avatarContent);
            Set(so, "frameContent", frameContent);
            Set(so, "avatarItems", avatarItems.ToArray());
            Set(so, "frameItems", frameItems.ToArray());
            Set(so, "avatarSprites", SpriteArray("Avatar_", 9));
            Set(so, "frameSprites", SpriteArray("Frame_", 9));
            Set(so, "saveProgressCloseButton", saveProgressDialog.transform.Find("Panel/Close Button").GetComponent<Button>());
            Set(so, "facebookLoginButton", saveProgressDialog.transform.Find("Panel/Facebook Login Button").GetComponent<Button>());
            Set(so, "useLocalProgressButton", conflictDialog.transform.Find("Panel/Use Local Button").GetComponent<Button>());
            Set(so, "useCloudProgressButton", conflictDialog.transform.Find("Panel/Use Cloud Button").GetComponent<Button>());
            Set(so, "conflictCloseButton", conflictDialog.transform.Find("Panel/Close Button").GetComponent<Button>());
            Set(so, "loginFailedCloseButton", loginFailedDialog.transform.Find("Panel/Close Button").GetComponent<Button>());
            Set(so, "loginSuccessCloseButton", loginSuccessDialog.transform.Find("Panel/Close Button").GetComponent<Button>());
            Set(so, "tryAgainButton", loginFailedDialog.transform.Find("Panel/Try Again Button").GetComponent<Button>());
            so.ApplyModifiedPropertiesWithoutUndo();

            dialogRoot.SetActive(false);
            return root;
        }

        private static GameObject BuildGrid(string name, Transform parent, bool avatars, out List<ProfileSelectableItem> items)
        {
            GameObject scrollGo = UIObject(name, parent);
            SetRect(scrollGo.GetComponent<RectTransform>(), new Vector2(0f, -8f), new Vector2(660f, 680f));
            ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            GameObject viewport = UIObject("Viewport", scrollGo.transform);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<RectMask2D>();
            Image viewportRaycast = viewport.AddComponent<Image>();
            viewportRaycast.color = new Color(1f, 1f, 1f, 0.001f);

            GameObject content = UIObject("Content", viewport.transform);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 620f);
            GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(180f, 180f);
            grid.spacing = new Vector2(35f, 25f);
            grid.padding = new RectOffset(25, 25, 16, 16);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;
            items = new List<ProfileSelectableItem>();

            for (int i = 0; i < 9; i++)
            {
                GameObject item = UIObject((avatars ? "Avatar " : "Frame ") + (i + 1).ToString("00"), content.transform);
                Image hit = item.AddComponent<Image>();
                hit.color = new Color(1f, 1f, 1f, 0.001f);
                Button button = item.AddComponent<Button>();
                button.targetGraphic = hit;
                ProfileSelectableItem selectable = item.AddComponent<ProfileSelectableItem>();

                Image icon = null;
                if (avatars)
                {
                    icon = AddImageRect("Avatar", item.transform, Sprite("Avatar_" + (i + 1).ToString("00")), Vector2.zero, new Vector2(158f, 158f), Image.Type.Simple);
                    icon.preserveAspect = true;
                }

                Image frame = AddImageRect("Frame", item.transform, Sprite("Frame_" + (i + 1).ToString("00")), Vector2.zero, new Vector2(170f, 170f), Image.Type.Simple);
                frame.preserveAspect = true;
                if (avatars)
                    frame.sprite = Sprite("Frame_06");

                Image check = AddImageRect("Selected", item.transform, Sprite("Check_Selection"), new Vector2(64f, -64f), new Vector2(64f, 64f), Image.Type.Simple);
                check.preserveAspect = true;
                check.gameObject.SetActive(i == 5);

                SerializedObject itemSo = new SerializedObject(selectable);
                Set(itemSo, "button", button);
                Set(itemSo, "iconImage", icon);
                Set(itemSo, "frameImage", frame);
                Set(itemSo, "checkMark", check.gameObject);
                itemSo.ApplyModifiedPropertiesWithoutUndo();
                items.Add(selectable);
            }

            return scrollGo;
        }

        private static GameObject BuildDialogs(Transform root, out CanvasGroup group)
        {
            GameObject dialogRoot = UIObject("Dialog Root", root);
            Stretch(dialogRoot.GetComponent<RectTransform>());
            group = dialogRoot.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            Image backdrop = dialogRoot.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.62f);
            backdrop.raycastTarget = true;

            BuildSaveProgressDialog(dialogRoot.transform);
            BuildConflictDialog(dialogRoot.transform);
            BuildStatusDialog(dialogRoot.transform, false);
            BuildStatusDialog(dialogRoot.transform, true);
            return dialogRoot;
        }

        private static void BuildSaveProgressDialog(Transform parent)
        {
            GameObject holder = UIObject("Save Progress Dialog", parent);
            Stretch(holder.GetComponent<RectTransform>());
            GameObject panel = BuildDialogPanel(holder.transform, new Vector2(700f, 980f));
            AddImageRect("Title Ribbon", panel.transform, Sprite("Title_Ribbon_Long"), new Vector2(0f, 490f), new Vector2(620f, 135f), Image.Type.Sliced);
            AddText("Title", panel.transform, "SAVE PROGRESS", 55f, new Vector2(0f, 494f), new Vector2(570f, 100f), Color.white);
            AddIconButton("Close Button", panel.transform, Sprite("Close_Red"), new Vector2(325f, 450f), new Vector2(92f, 92f));
            AddText("Description", panel.transform, "Sync your progress across\ndevices", 45f, new Vector2(0f, 285f), new Vector2(610f, 180f), new Color32(83, 103, 158, 255));
            Button facebook = AddTextButton("Facebook Login Button", panel.transform, Sprite("SocialLogin_Green_Large"), "Sign in with Facebook", 43f, new Vector2(0f, 90f), new Vector2(620f, 130f), Color.white);
            Image fbIcon = AddImageRect("Facebook Icon", facebook.transform, Sprite("Facebook_Icon"), new Vector2(-245f, 0f), new Vector2(86f, 86f), Image.Type.Simple);
            fbIcon.preserveAspect = true;
        }

        private static void BuildConflictDialog(Transform parent)
        {
            GameObject holder = UIObject("Progress Conflict Dialog", parent);
            Stretch(holder.GetComponent<RectTransform>());
            GameObject panel = BuildDialogPanel(holder.transform, new Vector2(680f, 1020f));
            AddImageRect("Title Ribbon", panel.transform, Sprite("Title_Ribbon_Long"), new Vector2(0f, 510f), new Vector2(600f, 135f), Image.Type.Sliced);
            AddText("Title", panel.transform, "SYNC CONFLICT", 52f, new Vector2(0f, 515f), new Vector2(560f, 100f), Color.white);
            AddIconButton("Close Button", panel.transform, Sprite("Close_Red"), new Vector2(315f, 470f), new Vector2(92f, 92f));
            AddText("Message", panel.transform, "Choose which progress\nyou want to keep", 47f, new Vector2(0f, 210f), new Vector2(590f, 190f), new Color32(83, 103, 158, 255));
            AddTextButton("Use Local Button", panel.transform, Sprite("SocialLogin_Green_Large"), "Use Local", 50f, new Vector2(0f, -80f), new Vector2(540f, 128f), Color.white);
            AddTextButton("Use Cloud Button", panel.transform, Sprite("Retry_Green_Large"), "Use Cloud", 50f, new Vector2(0f, -250f), new Vector2(540f, 128f), Color.white);
        }

        private static void BuildStatusDialog(Transform parent, bool success)
        {
            string holderName = success ? "Login Successful Dialog" : "Login Failed Dialog";
            GameObject holder = UIObject(holderName, parent);
            Stretch(holder.GetComponent<RectTransform>());
            GameObject panel = BuildDialogPanel(holder.transform, new Vector2(680f, success ? 970f : 1020f));
            AddImageRect("Title Ribbon", panel.transform, Sprite("Title_Ribbon_Long"), new Vector2(0f, success ? 485f : 510f), new Vector2(600f, 135f), Image.Type.Sliced);
            AddText("Title", panel.transform, success ? "LOGIN SUCCESSFUL" : "LOGIN FAILED", 52f, new Vector2(0f, success ? 490f : 515f), new Vector2(560f, 100f), Color.white);
            AddIconButton("Close Button", panel.transform, Sprite("Close_Red"), new Vector2(315f, success ? 445f : 470f), new Vector2(92f, 92f));

            Image user = AddImageRect("User", panel.transform, Sprite("User_Profile_Large"), new Vector2(0f, 230f), new Vector2(230f, 230f), Image.Type.Simple);
            user.preserveAspect = true;
            Image badge = AddImageRect("Status Badge", panel.transform, Sprite(success ? "Check_Selection" : "Error_Badge_Red"), new Vector2(0f, 105f), new Vector2(82f, 82f), Image.Type.Simple);
            badge.preserveAspect = true;

            AddText("Message", panel.transform, success ? "Your progress is now\nsaved" : "Facebook sign-in wasn't\nsuccessful", 47f, new Vector2(0f, -85f), new Vector2(590f, 190f), new Color32(83, 103, 158, 255));
            if (!success)
                AddTextButton("Try Again Button", panel.transform, Sprite("Retry_Green_Large"), "Try again", 54f, new Vector2(0f, -315f), new Vector2(500f, 130f), Color.white);
        }

        private static GameObject BuildDialogPanel(Transform parent, Vector2 size)
        {
            GameObject panel = UIObject("Panel", parent);
            SetRect(panel.GetComponent<RectTransform>(), Vector2.zero, size);
            Image image = panel.AddComponent<Image>();
            image.sprite = Sprite("Small_Popup_BG");
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            return panel;
        }

        private static Button AddTab(string name, Transform parent, string label, bool active, Vector2 position, out Image background)
        {
            background = AddImageRect(name, parent, Sprite(active ? "Tab_Active" : "Tab_Inactive"), position, new Vector2(310f, 130f), Image.Type.Sliced);
            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            AddText("Label", background.transform, label, 48f, new Vector2(0f, 5f), new Vector2(280f, 95f), Color.white);
            return button;
        }

        private static void InstallInMenuScene()
        {
            GameObject mainCanvas = GameObject.Find("UI Main Canvas");
            if (mainCanvas == null)
                throw new MissingReferenceException("UI Main Canvas was not found in the active Menu scene.");

            Transform existing = mainCanvas.transform.Find("UI Profile Popup");
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, mainCanvas.transform);
            instance.name = "UI Profile Popup";
            RectTransform instanceRect = instance.GetComponent<RectTransform>();
            Stretch(instanceRect);
            instance.GetComponent<Canvas>().enabled = false;

            Transform safeZone = mainCanvas.transform.Find("UI Main Menu (Active)/Safe Zone");
            if (safeZone == null)
                safeZone = mainCanvas.transform.Find("UI Main Menu/Safe Zone");
            if (safeZone != null)
            {
                InstallProfileButton(safeZone);
                LayoutMainMenuTopBar(safeZone);
            }

            EditorSceneManager.MarkSceneDirty(mainCanvas.scene);
            EditorSceneManager.SaveScene(mainCanvas.scene);
            Selection.activeGameObject = instance;
        }

        private static void LayoutMainMenuTopBar(Transform safeZone)
        {
            RectTransform profileButton = safeZone.Find("Profile Button") as RectTransform;
            if (profileButton != null)
                SetTopLeft(profileButton, new Vector2(46f, -20f), new Vector2(146f, 146f));

            RectTransform currencyPanel = safeZone.Find("Currency Panel Simple") as RectTransform;
            if (currencyPanel != null)
                SetTopLeft(currencyPanel, new Vector2(630f, -50f), new Vector2(255f, 78f));

            RectTransform livesIndicator = safeZone.Find("Lives Indicator") as RectTransform;
            if (livesIndicator != null)
                SetTopLeft(livesIndicator, new Vector2(275f, -50f), new Vector2(255f, 78f));

            RectTransform settingsButton = safeZone.Find("Settings Button") as RectTransform;
            if (settingsButton != null)
                SetTopRight(settingsButton, new Vector2(-46f, -42f), new Vector2(100f, 100f));
        }

        private static void InstallProfileButton(Transform safeZone)
        {
            Transform existing = safeZone.Find("Profile Button");
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);

            GameObject buttonGo = UIObject("Profile Button", safeZone);
            RectTransform rect = buttonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(28f, -34f);
            rect.sizeDelta = new Vector2(138f, 138f);
            Image hit = buttonGo.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.001f);
            Button button = buttonGo.AddComponent<Button>();
            button.targetGraphic = hit;
            buttonGo.AddComponent<UIProfileOpenButton>();

            Image avatar = AddImageRect("Avatar", buttonGo.transform, Sprite("Avatar_06"), Vector2.zero, new Vector2(124f, 124f), Image.Type.Simple);
            avatar.preserveAspect = true;
            Image frame = AddImageRect("Frame", buttonGo.transform, Sprite("Frame_06"), Vector2.zero, new Vector2(142f, 142f), Image.Type.Simple);
            frame.preserveAspect = true;
        }

        private static Button AddIconButton(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            Image image = AddImageRect(name, parent, sprite, position, size, Image.Type.Simple);
            image.preserveAspect = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private static Button AddTextButton(string name, Transform parent, Sprite sprite, string label, float fontSize, Vector2 position, Vector2 size, Color textColor)
        {
            Image image = AddImageRect(name, parent, sprite, position, size, Image.Type.Sliced);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            AddText("Label", image.transform, label, fontSize, Vector2.zero, size - new Vector2(40f, 20f), textColor);
            return button;
        }

        private static TextMeshProUGUI AddText(string name, Transform parent, string value, float fontSize, Vector2 position, Vector2 size, Color color)
        {
            GameObject go = UIObject(name, parent);
            SetRect(go.GetComponent<RectTransform>(), position, size);
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        private static Image AddImage(string name, Transform parent, Sprite sprite)
        {
            GameObject go = UIObject(name, parent);
            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            return image;
        }

        private static Image AddImageRect(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size, Image.Type type)
        {
            Image image = AddImage(name, parent, sprite);
            SetRect(image.rectTransform, position, size);
            image.type = type;
            return image;
        }

        private static GameObject UIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
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
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void AnchorTopRight(RectTransform rect, Vector2 anchoredPosition)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.localScale = Vector3.one;
        }

        private static void SetTopLeft(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void SetTopRight(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static Sprite Sprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + name + ".png");
        }

        private static void EnsureRoundedContentAreaSprite()
        {
            string sourcePath = ArtFolder + "Lavender_Content_Area.png";
            byte[] sourceBytes = File.ReadAllBytes(sourcePath);
            Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(sourceBytes, false);

            Color32[] pixels = source.GetPixels32();
            int width = source.width;
            int height = source.height;
            int radius = 28;

            for (int y = height - radius; y < height; y++)
            {
                int mirroredBottomY = height - 1 - y;
                int colorSourceY = Mathf.Min(height - 3, y);

                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    Color32 pixel = pixels[index];
                    Color32 colorSource = pixels[colorSourceY * width + x];
                    pixel.r = colorSource.r;
                    pixel.g = colorSource.g;
                    pixel.b = colorSource.b;
                    pixel.a = pixels[mirroredBottomY * width + x].a;
                    pixels[index] = pixel;
                }
            }

            Texture2D rounded = new Texture2D(width, height, TextureFormat.RGBA32, false);
            rounded.SetPixels32(pixels);
            rounded.Apply(false, false);
            File.WriteAllBytes(RoundedContentAreaPath, rounded.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(rounded);

            AssetDatabase.ImportAsset(RoundedContentAreaPath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(RoundedContentAreaPath) as TextureImporter;
            if (importer == null)
                throw new MissingReferenceException("Unable to import rounded content-area sprite.");

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = new Vector4(28f, 28f, 28f, 28f);
            importer.SaveAndReimport();
        }

        private static Sprite[] SpriteArray(string prefix, int count)
        {
            Sprite[] result = new Sprite[count];
            for (int i = 0; i < count; i++)
                result[i] = Sprite(prefix + (i + 1).ToString("00"));
            return result;
        }

        private static void Set(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
                throw new MissingFieldException(serializedObject.targetObject.GetType().Name, propertyName);
            property.objectReferenceValue = value;
        }

        private static void Set(SerializedObject serializedObject, string propertyName, UnityEngine.Object[] values)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
                throw new MissingFieldException(serializedObject.targetObject.GetType().Name, propertyName);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void EnsureAssetFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
