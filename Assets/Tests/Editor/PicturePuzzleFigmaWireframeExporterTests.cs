using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PicturePuzzle.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PicturePuzzleFigmaWireframeExporterTests
{
    private const string PopupPath = "Assets/Addon/UI/Prefabs/Shared/UpdateAvailablePopup.prefab";
    private const string PopupFolder = "Assets/Addon/UI/Prefabs/Shared";
    private const string StorePath = "Assets/Project Files/Game/Prefabs/UI Store/UI Store.prefab";
    private const string GameScenePath = "Assets/Project Files/Game/Scenes/Game.unity";
    private const string MenuScenePath = "Assets/Project Files/Game/Scenes/Menu.unity";

    [Test]
    public void ResolvePrefabPaths_FindsSelectedUguiPrefabAndFolderContents()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath);

        Assert.That(PicturePuzzleFigmaWireframeExporter.IsUguiPrefab(prefab), Is.True);
        Assert.That(PicturePuzzleFigmaWireframeExporter.ResolvePrefabPaths(PopupPath), Is.EqualTo(new List<string> { PopupPath }));
        Assert.That(PicturePuzzleFigmaWireframeExporter.ResolvePrefabPaths(PopupFolder), Does.Contain(PopupPath));
    }

    [Test]
    public void ResolvePrefabPathsFromSelection_AcceptsPrefabAndFolderWithoutDuplicates()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath);
        DefaultAsset folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(PopupFolder);
        Assert.That(folder, Is.Not.Null);

        List<string> paths = PicturePuzzleFigmaWireframeExporter.ResolvePrefabPathsFromSelection(
            new Object[] { prefab, folder, prefab });

        Assert.That(paths, Does.Contain(PopupPath));
        Assert.That(paths.Count(path => path == PopupPath), Is.EqualTo(1));
        Assert.That(paths, Is.EqualTo(paths.OrderBy(path => path).ToList()));
    }

    [Test]
    public void CreatePrefabPreviewTexture_RendersPortraitThumbnailWithoutChangingPrefab()
    {
        Hash128 sourceHash = AssetDatabase.GetAssetDependencyHash(PopupPath);
        Texture2D preview = PicturePuzzleFigmaWireframeExporter.CreatePrefabPreviewTexture(PopupPath);

        try
        {
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview.width, Is.EqualTo(216));
            Assert.That(preview.height, Is.EqualTo(384));
            Assert.That(preview.GetPixels32().Any(pixel => pixel.r != 0 || pixel.g != 0 || pixel.b != 0), Is.True);
            Assert.That(AssetDatabase.GetAssetDependencyHash(PopupPath), Is.EqualTo(sourceHash));
        }
        finally
        {
            if (preview != null) Object.DestroyImmediate(preview);
        }
    }

    [Test]
    public void StableNodeId_IsDeterministicForPrefabHierarchyPaths()
    {
        string guid = AssetDatabase.AssetPathToGUID(PopupPath);

        Assert.That(PicturePuzzleFigmaWireframeExporter.BuildStableNodeId(guid, "0/2/1"),
            Is.EqualTo(PicturePuzzleFigmaWireframeExporter.BuildStableNodeId(guid, "0/2/1")));
        Assert.That(PicturePuzzleFigmaWireframeExporter.BuildStableNodeId(guid, "0/2/1"),
            Is.Not.EqualTo(PicturePuzzleFigmaWireframeExporter.BuildStableNodeId(guid, "0/2/2")));
    }

    [Test]
    public void ViewportBounds_AlwaysMapToThe1080x1920FigmaCanvas()
    {
        FigmaWireframeRect fullCanvas = PicturePuzzleFigmaWireframeExporter.GetFigmaRectFromViewport(new[]
        {
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f)
        });
        FigmaWireframeRect topRight = PicturePuzzleFigmaWireframeExporter.GetFigmaRectFromViewport(new[]
        {
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0.5f)
        });

        Assert.That(fullCanvas.x, Is.EqualTo(0f));
        Assert.That(fullCanvas.y, Is.EqualTo(0f));
        Assert.That(fullCanvas.width, Is.EqualTo(1080f));
        Assert.That(fullCanvas.height, Is.EqualTo(1920f));
        Assert.That(topRight.x, Is.EqualTo(540f));
        Assert.That(topRight.y, Is.EqualTo(0f));
        Assert.That(topRight.width, Is.EqualTo(540f));
        Assert.That(topRight.height, Is.EqualTo(960f));
    }

    [Test]
    public void Export_CreatesVersioned1080x1920ManifestWithoutChangingSourcePrefab()
    {
        Hash128 sourceHash = AssetDatabase.GetAssetDependencyHash(PopupPath);
        FigmaWireframeExportResult result = PicturePuzzleFigmaWireframeExporter.Export(new[] { PopupPath });

        Assert.That(result.batch.schemaVersion, Is.EqualTo(2));
        Assert.That(result.batch.canvas.width, Is.EqualTo(1080));
        Assert.That(result.batch.canvas.height, Is.EqualTo(1920));
        Assert.That(result.batch.items, Has.Count.EqualTo(1));
        FigmaWireframeItem popup = result.batch.items[0];
        Assert.That(popup.sourceGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(PopupPath)));
        Assert.That(popup.nodes, Is.Not.Empty);
        Assert.That(popup.referenceImageId, Is.EqualTo(popup.itemId));
        AssertReferenceImageIsVisible(result, popup);

        FigmaWireframeNode visualNode = popup.nodes.FirstOrDefault(node => !string.IsNullOrEmpty(node.visualImageId));
        Assert.That(visualNode, Is.Not.Null);
        Assert.That(visualNode.visualImageId, Is.EqualTo(visualNode.id));
        Assert.That(visualNode.visualRect, Is.Not.Null);
        Assert.That(visualNode.visualRect.width, Is.GreaterThan(0f));
        Assert.That(visualNode.visualRect.height, Is.GreaterThan(0f));
        AssertVisualImageIsVisible(result, visualNode);

        FigmaWireframeNode textNode = popup.nodes.FirstOrDefault(node => node.role == "text" && node.textStyle != null);
        Assert.That(textNode, Is.Not.Null);
        Assert.That(textNode.text, Is.Not.Empty);
        Assert.That(textNode.textStyle.fontFamily, Is.Not.Empty);
        Assert.That(textNode.textStyle.fontSize, Is.GreaterThan(0f));
        Assert.That(textNode.textStyle.lineHeight, Is.GreaterThan(0f));
        string manifestJson = File.ReadAllText(result.manifestPath);
        Assert.That(manifestJson, Does.Contain("\"parentId\""));
        Assert.That(manifestJson, Does.Contain("\"visualImageId\""));
        Assert.That(manifestJson, Does.Contain("\"textStyle\""));
        Assert.That(manifestJson, Does.Not.Contain("\"children\""));
        Assert.That(AssetDatabase.GetAssetDependencyHash(PopupPath), Is.EqualTo(sourceHash));
    }

    [Test]
    public void Export_RecognizesVisualLayersClippingTextButtonsAndScrollHierarchyInExistingStorePrefab()
    {
        FigmaWireframeExportResult result = PicturePuzzleFigmaWireframeExporter.Export(new[] { StorePath });

        Assert.That(result.batch.items, Has.Count.EqualTo(1));
        FigmaWireframeItem store = result.batch.items[0];
        Assert.That(store.sourcePrefabGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(StorePath)));
        Assert.That(store.nodes[0].includeInAssetReview, Is.False);
        List<FigmaWireframeNode> visualNodes = store.nodes
            .Where(node => !string.IsNullOrEmpty(node.visualImageId))
            .ToList();
        Assert.That(visualNodes.Count, Is.GreaterThan(1));
        Assert.That(visualNodes.Select(node => node.visualImageId).Distinct().Count(), Is.EqualTo(visualNodes.Count));
        Assert.That(visualNodes.All(node => node.visualImageId == node.id && node.visualRect != null), Is.True);
        Assert.That(visualNodes.All(node =>
        {
            string artifactPath = Path.Combine(Path.GetDirectoryName(result.manifestPath), node.visualImageId + ".png");
            return File.Exists(artifactPath) && new FileInfo(artifactPath).Length > 0;
        }), Is.True);

        List<FigmaWireframeNode> nestedAssetNodes = store.nodes
            .Where(node => !string.IsNullOrEmpty(node.assetImageId))
            .ToList();
        Assert.That(nestedAssetNodes, Is.Not.Empty);
        Assert.That(nestedAssetNodes.Select(node => node.assetImageId).Distinct().Count(), Is.EqualTo(nestedAssetNodes.Count));
        Assert.That(nestedAssetNodes.All(node => node.assetRect != null
                                                 && node.assetRect.width > 0f
                                                 && node.assetRect.height > 0f
                                                 && node.nestedPrefabPath.StartsWith("Assets/")), Is.True);
        Assert.That(nestedAssetNodes.All(node =>
        {
            string artifactPath = Path.Combine(Path.GetDirectoryName(result.manifestPath), node.assetImageId + ".png");
            return File.Exists(artifactPath) && new FileInfo(artifactPath).Length > 0;
        }), Is.True);

        FigmaWireframeNode offerBig = nestedAssetNodes.Single(node => node.name == "Offer Money Big"
                                                                       && node.nestedPrefabPath.EndsWith("IAP Money Big.prefab"));
        FigmaWireframeNode timerMoney = nestedAssetNodes.Single(node => node.name == "Timer Money"
                                                                         && node.nestedPrefabPath.EndsWith("Timer Money.prefab"));
        FigmaWireframeNode adsMoney = nestedAssetNodes.Single(node => node.name == "Ads Money"
                                                                       && node.nestedPrefabPath.EndsWith("Ads Money.prefab"));
        Assert.That(offerBig.assetRect.width, Is.GreaterThan(500f));
        Assert.That(offerBig.assetRect.height, Is.GreaterThan(100f));
        Assert.That(timerMoney.assetRect.height, Is.GreaterThan(100f));
        Assert.That(adsMoney.assetRect.height, Is.GreaterThan(100f));
        Assert.That(string.IsNullOrEmpty(timerMoney.visualImageId), Is.True,
            "The exact Canvas layer is clipped, but the isolated Page 1 asset must still exist.");
        AssertAssetImageIsVisible(result, offerBig);
        AssertAssetImageIsVisible(result, timerMoney);
        AssertAssetImageIsVisible(result, adsMoney);
        Assert.That(store.nodes.Any(node => node.clipsContent), Is.True);
        Assert.That(store.nodes.Any(node => node.role == "text"
                                                && !string.IsNullOrEmpty(node.text)
                                                && node.textStyle != null), Is.True);
        Assert.That(store.nodes.Select(node => node.role), Does.Contain("button"));
        Assert.That(store.nodes.Select(node => node.role), Does.Contain("scroll"));
        Assert.That(store.nodes.Any(node => node.parentId == null), Is.True);
    }

    [Test]
    public void ExportSceneHierarchy_PreservesStableIdentityAndDoesNotDirtyTheScene()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject canvasRoot = scene.GetRootGameObjects().Single(root => root.name == "UI Main Canvas");
            GameObject hierarchyRoot = canvasRoot.transform.Find("UI Quit Pop Up").gameObject;
            bool wasDirty = scene.isDirty;
            string sourceId = PicturePuzzleFigmaWireframeExporter.BuildSceneSourceId(hierarchyRoot);

            Assert.That(PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy(hierarchyRoot), Is.True);
            Assert.That(PicturePuzzleFigmaWireframeExporter.BuildSceneSourceId(hierarchyRoot), Is.EqualTo(sourceId));

            FigmaWireframeExportResult result = PicturePuzzleFigmaWireframeExporter.ExportSceneHierarchies(new[] { hierarchyRoot });
            Assert.That(result.batch.items, Has.Count.EqualTo(1));
            FigmaWireframeItem item = result.batch.items[0];
            Assert.That(item.itemId, Is.EqualTo(sourceId));
            Assert.That(item.sourceGuid, Is.EqualTo(sourceId));
            Assert.That(item.sourceKind, Is.EqualTo("scene-hierarchy"));
            Assert.That(item.assetPath, Is.EqualTo(GameScenePath));
            Assert.That(item.hierarchyPath, Is.EqualTo("UI Main Canvas/UI Quit Pop Up"));
            Assert.That(item.nodes, Is.Not.Empty);
            Assert.That(item.nodes[0].name, Is.EqualTo("UI Quit Pop Up"));
            Assert.That(item.nodes.Any(node => node.name == "UI Game"), Is.False);
            AssertReferenceImageIsVisible(result, item);
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
        }
        finally
        {
            if (openedForTest && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ExportWindow_InactiveSceneUiCanBeCheckedAndReviewedWithoutChangingTheSource()
    {
        Scene scene = SceneManager.GetSceneByPath(MenuScenePath);
        bool openedForTest = !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Additive);

        Scene previousActiveScene = SceneManager.GetActiveScene();
        var window = ScriptableObject.CreateInstance<PicturePuzzleFigmaWireframeExporterWindow>();
        try
        {
            Assert.That(SceneManager.SetActiveScene(scene), Is.True);
            GameObject canvasRoot = scene.GetRootGameObjects().Single(root => root.name == "UI Main Canvas");
            GameObject inactivePopup = canvasRoot.transform.Find("DailyRewardPopup").gameObject;
            bool wasDirty = scene.isDirty;

            Assert.That(inactivePopup.activeSelf, Is.False, "The regression fixture must remain inactive in Menu.unity.");
            Assert.That(inactivePopup.activeInHierarchy, Is.False);
            Assert.That(PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy(inactivePopup), Is.True,
                "Inactive saved Scene UI must remain selectable in the exporter tree.");

            InvokePrivate(window, "SetSceneSourceChecked", inactivePopup, true);
            Assert.That(GetPrivateField<List<GameObject>>(window, "selectedSceneRoots"), Does.Contain(inactivePopup));
            BuildLocalPreview(window);

            FigmaWireframeExportResult review = GetPrivateField<FigmaWireframeExportResult>(window, "localPreviewExport");
            Assert.That(review, Is.Not.Null);
            FigmaWireframeItem item = review.batch.items.Single();
            Assert.That(item.hierarchyPath, Is.EqualTo("UI Main Canvas/DailyRewardPopup"));
            Assert.That(item.nodes, Is.Not.Empty);
            Assert.That(item.nodes[0].name, Is.EqualTo("DailyRewardPopup"));
            Assert.That(item.nodes.Any(node => !string.IsNullOrEmpty(node.visualImageId)), Is.True);
            AssertReferenceImageIsVisible(review, item);
            Assert.That(inactivePopup.activeSelf, Is.False, "Preview activation must only affect the cloned hierarchy.");
            Assert.That(inactivePopup.activeInHierarchy, Is.False);
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
        }
        finally
        {
            Object.DestroyImmediate(window);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded && previousActiveScene != scene)
                SceneManager.SetActiveScene(previousActiveScene);
            if (openedForTest && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ExportSceneHierarchy_ActiveChildBelowInactiveParentRendersFromCloneOnly()
    {
        Scene scene = SceneManager.GetSceneByPath(MenuScenePath);
        bool openedForTest = !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject canvasRoot = scene.GetRootGameObjects().Single(root => root.name == "UI Main Canvas");
            GameObject inactivePopup = canvasRoot.transform.Find("DailyRewardPopup").gameObject;
            GameObject activeChild = inactivePopup.transform.Find("Safe Area").gameObject;
            bool wasDirty = scene.isDirty;

            Assert.That(inactivePopup.activeSelf, Is.False);
            Assert.That(activeChild.activeSelf, Is.True);
            Assert.That(activeChild.activeInHierarchy, Is.False);
            Assert.That(PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy(activeChild), Is.True);

            FigmaWireframeExportResult result = PicturePuzzleFigmaWireframeExporter.ExportSceneHierarchies(new[] { activeChild });
            FigmaWireframeItem item = result.batch.items.Single();

            Assert.That(item.hierarchyPath, Is.EqualTo("UI Main Canvas/DailyRewardPopup/Safe Area"));
            Assert.That(item.nodes[0].name, Is.EqualTo("Safe Area"));
            Assert.That(item.nodes.Any(node => !string.IsNullOrEmpty(node.visualImageId)), Is.True);
            AssertReferenceImageIsVisible(result, item);
            Assert.That(inactivePopup.activeSelf, Is.False, "Inactive ancestors must only be activated in the preview clone.");
            Assert.That(activeChild.activeSelf, Is.True);
            Assert.That(activeChild.activeInHierarchy, Is.False);
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
        }
        finally
        {
            if (openedForTest && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ExportSceneHierarchy_InactiveZeroScalePopupRendersFromCloneOnly()
    {
        Scene scene = SceneManager.GetSceneByPath(MenuScenePath);
        bool openedForTest = !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject canvasRoot = scene.GetRootGameObjects().Single(root => root.name == "UI Main Canvas");
            GameObject popup = canvasRoot.transform.Find("NoConnectionPopup").gameObject;
            Vector3 sourceScale = popup.transform.localScale;
            bool wasDirty = scene.isDirty;

            Assert.That(popup.activeSelf, Is.False);
            Assert.That(sourceScale.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(sourceScale.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy(popup), Is.True);

            FigmaWireframeExportResult result = PicturePuzzleFigmaWireframeExporter.ExportSceneHierarchies(new[] { popup });
            FigmaWireframeItem item = result.batch.items.Single();

            Assert.That(item.hierarchyPath, Is.EqualTo("UI Main Canvas/NoConnectionPopup"));
            Assert.That(item.nodes, Is.Not.Empty);
            Assert.That(item.nodes[0].rect.width, Is.GreaterThan(0f));
            Assert.That(item.nodes[0].rect.height, Is.GreaterThan(0f));
            Assert.That(item.nodes.Any(node => !string.IsNullOrEmpty(node.visualImageId)), Is.True);
            AssertReferenceImageIsVisible(result, item);
            Assert.That(popup.activeSelf, Is.False, "Preview activation must only affect the cloned hierarchy.");
            Assert.That(popup.transform.localScale, Is.EqualTo(sourceScale));
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
        }
        finally
        {
            if (openedForTest && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ExportWindow_ProfilePopupChildDialogsBuildAsSeparateReviews()
    {
        Scene scene = SceneManager.GetSceneByPath(MenuScenePath);
        bool openedForTest = !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Additive);

        Scene previousActiveScene = SceneManager.GetActiveScene();
        var window = ScriptableObject.CreateInstance<PicturePuzzleFigmaWireframeExporterWindow>();
        try
        {
            Assert.That(SceneManager.SetActiveScene(scene), Is.True);
            List<GameObject> dialogs = GetProfilePopupDialogRoots(scene);
            GameObject dialogRoot = dialogs[0].transform.parent.gameObject;
            bool wasDirty = scene.isDirty;

            Assert.That(dialogRoot.activeSelf, Is.False);
            Assert.That(dialogs.Select(dialog => dialog.name), Is.EquivalentTo(new[]
            {
                "Save Progress Dialog",
                "Login Result Dialog",
                "Save Progress Found Dialog"
            }));
            Assert.That(dialogs.All(PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy), Is.True,
                "Every dialog below an inactive parent must remain selectable as an independent Scene UI branch.");

            foreach (GameObject dialog in dialogs)
                InvokePrivate(window, "SetSceneSourceChecked", dialog, true);
            Assert.That(GetPrivateField<List<GameObject>>(window, "selectedSceneRoots"), Is.EquivalentTo(dialogs));

            BuildLocalPreview(window);

            FigmaWireframeExportResult review = GetPrivateField<FigmaWireframeExportResult>(window, "localPreviewExport");
            Assert.That(review.batch.items, Has.Count.EqualTo(3));
            Assert.That(review.batch.items.Select(item => item.hierarchyPath), Is.EquivalentTo(
                dialogs.Select(PicturePuzzleFigmaWireframeExporter.GetSceneHierarchyPath)));
            Assert.That(review.batch.items.All(item => item.nodes.Count > 0), Is.True);
            foreach (FigmaWireframeItem item in review.batch.items)
                AssertReferenceImageIsVisible(review, item);
            Assert.That(dialogRoot.activeSelf, Is.False, "Only the cloned preview branch may be activated.");
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
        }
        finally
        {
            Object.DestroyImmediate(window);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded && previousActiveScene != scene)
                SceneManager.SetActiveScene(previousActiveScene);
            if (openedForTest && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ExportWindow_ChildPreviewBuildsAndFocusesWhenTheReviewIsStale()
    {
        Scene scene = SceneManager.GetSceneByPath(MenuScenePath);
        bool openedForTest = !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Additive);

        Scene previousActiveScene = SceneManager.GetActiveScene();
        var window = ScriptableObject.CreateInstance<PicturePuzzleFigmaWireframeExporterWindow>();
        try
        {
            Assert.That(SceneManager.SetActiveScene(scene), Is.True);
            GameObject canvasRoot = scene.GetRootGameObjects().Single(root => root.name == "UI Main Canvas");
            GameObject parentPopup = canvasRoot.transform.Find("UI Profile Popup").gameObject;
            GameObject childDialog = GetProfilePopupDialogRoots(scene)
                .Single(dialog => dialog.name == "Login Result Dialog");

            InvokePrivate(window, "SetSceneSourceChecked", parentPopup, true);
            BuildLocalPreview(window);
            InvokePrivate(window, "SetSceneSourceChecked", childDialog, true);
            Assert.That(GetPrivateField<bool>(window, "localPreviewRebuildPending"), Is.True);

            InvokePrivate(window, "FocusLocalPreviewForScene", childDialog);
            Assert.That(GetPrivateField<object>(window, "localPreviewBuildSession"), Is.Not.Null,
                "Build & preview must rebuild a stale review instead of searching its old batch for the child dialog.");

            BuildLocalPreview(window);

            FigmaWireframeExportResult review = GetPrivateField<FigmaWireframeExportResult>(window, "localPreviewExport");
            int itemIndex = GetPrivateField<int>(window, "localPreviewItemIndex");
            Assert.That(review.batch.items, Has.Count.EqualTo(2));
            Assert.That(review.batch.items[itemIndex].hierarchyPath,
                Is.EqualTo(PicturePuzzleFigmaWireframeExporter.GetSceneHierarchyPath(childDialog)));
            Assert.That(GetPrivateField<bool>(window, "localPreviewRebuildPending"), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(window);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded && previousActiveScene != scene)
                SceneManager.SetActiveScene(previousActiveScene);
            if (openedForTest && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ExportWindow_AllCanvasRootsKeepsInactiveCanvasSelectable()
    {
        Scene scene = SceneManager.GetSceneByPath(MenuScenePath);
        bool openedForTest = !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Additive);

        Scene previousActiveScene = SceneManager.GetActiveScene();
        var window = ScriptableObject.CreateInstance<PicturePuzzleFigmaWireframeExporterWindow>();
        try
        {
            Assert.That(SceneManager.SetActiveScene(scene), Is.True);
            GameObject inactiveCanvas = scene.GetRootGameObjects().Single(root => root.name == "IngameDebugConsole");
            List<GameObject> canvasRoots = PicturePuzzleFigmaWireframeExporter.GetSceneUguiHierarchyRoots(scene);
            bool wasDirty = scene.isDirty;

            Assert.That(inactiveCanvas.activeSelf, Is.False);
            Assert.That(canvasRoots, Does.Contain(inactiveCanvas));
            InvokePrivate(window, "AddSceneSources", canvasRoots);

            List<GameObject> selectedRoots = GetPrivateField<List<GameObject>>(window, "selectedSceneRoots");
            Assert.That(selectedRoots, Does.Contain(inactiveCanvas));
            Assert.That((bool)InvokePrivate(window, "PruneSelections", false), Is.False,
                "Pruning must not remove a valid inactive Canvas selected from All Canvas roots.");
            Assert.That(selectedRoots, Does.Contain(inactiveCanvas));
            Assert.That(inactiveCanvas.activeSelf, Is.False);
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
        }
        finally
        {
            Object.DestroyImmediate(window);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded && previousActiveScene != scene)
                SceneManager.SetActiveScene(previousActiveScene);
            if (openedForTest && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void SceneHierarchyRoots_ListTheCurrentScenesOutermostCanvas()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        try
        {
            List<GameObject> roots = PicturePuzzleFigmaWireframeExporter.GetSceneUguiHierarchyRoots(scene);

            Assert.That(roots.Select(root => root.name), Does.Contain("UI Main Canvas"));
            Assert.That(roots.All(root => root.GetComponent<Canvas>() != null), Is.True);
        }
        finally
        {
            if (openedForTest && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void CreateScenePreviewTexture_RendersOnlyTheSelectedUiBranchWithoutDirtyingScene()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        Texture2D preview = null;
        try
        {
            GameObject canvasRoot = scene.GetRootGameObjects().Single(root => root.name == "UI Main Canvas");
            GameObject hierarchyRoot = canvasRoot.transform.Find("UI Quit Pop Up").gameObject;
            bool wasDirty = scene.isDirty;

            preview = PicturePuzzleFigmaWireframeExporter.CreateScenePreviewTexture(hierarchyRoot);

            Assert.That(preview, Is.Not.Null);
            Assert.That(preview.width, Is.EqualTo(216));
            Assert.That(preview.height, Is.EqualTo(384));
            Assert.That(preview.GetPixels32().Any(pixel => pixel.r != 0 || pixel.g != 0 || pixel.b != 0), Is.True);
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
        }
        finally
        {
            if (preview != null) Object.DestroyImmediate(preview);
            if (openedForTest && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ExportSources_CombinesMultiplePrefabsAndSceneObjectsInOneBatch()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject canvasRoot = scene.GetRootGameObjects().Single(root => root.name == "UI Main Canvas");
            GameObject uiGame = canvasRoot.transform.Find("UI Game").gameObject;
            GameObject quitPopup = canvasRoot.transform.Find("UI Quit Pop Up").gameObject;
            bool wasDirty = scene.isDirty;

            FigmaWireframeExportResult result = PicturePuzzleFigmaWireframeExporter.ExportSources(
                new[] { PopupPath, StorePath, PopupPath },
                new[] { uiGame, quitPopup, uiGame });

            Assert.That(result.batch.items, Has.Count.EqualTo(4));
            Assert.That(result.batch.items.Count(item => item.sourceKind == "prefab"), Is.EqualTo(2));
            Assert.That(result.batch.items.Count(item => item.sourceKind == "scene-hierarchy"), Is.EqualTo(2));
            Assert.That(result.batch.items.Select(item => item.itemId).Distinct().Count(), Is.EqualTo(4));
            Assert.That(result.batch.items.All(item => item.referenceImageId == item.itemId), Is.True);
            Assert.That(result.batch.items.All(item => File.Exists(Path.Combine(Path.GetDirectoryName(result.manifestPath), item.itemId + ".png"))), Is.True);
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
        }
        finally
        {
            if (openedForTest && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ExportWindow_LegacyPrefabSelectionIsIgnoredBySceneOnlyReview()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath);
        var window = ScriptableObject.CreateInstance<PicturePuzzleFigmaWireframeExporterWindow>();
        try
        {
            InvokePrivate(window, "AddProjectSources", new Object[] { prefab }, false);
            InvokePrivate(window, "SetPrefabChecked", PopupPath, true);
            BuildLocalPreview(window);

            Assert.That(GetPrivateField<FigmaWireframeExportResult>(window, "localPreviewExport"), Is.Null);
            Assert.That(GetPrivateField<string>(window, "exportStatus"), Does.Contain("Scene UI"));
        }
        finally
        {
            Object.DestroyImmediate(window);
        }
    }

    [Test]
    public void ExportWindow_CheckedSceneUiBuildsThreeReviewsAndSurvivesPreviewHierarchyChanges()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        Scene previousActiveScene = SceneManager.GetActiveScene();
        var window = ScriptableObject.CreateInstance<PicturePuzzleFigmaWireframeExporterWindow>();
        try
        {
            Assert.That(SceneManager.SetActiveScene(scene), Is.True);
            GameObject canvasRoot = scene.GetRootGameObjects().Single(root => root.name == "UI Main Canvas");
            GameObject quitPopup = canvasRoot.transform.Find("UI Quit Pop Up").gameObject;

            InvokePrivate(window, "SetSceneSourceChecked", quitPopup, true);
            InvokePrivate(window, "BuildLocalPreview");
            Assert.That(GetPrivateField<object>(window, "localPreviewBuildSession"), Is.Not.Null);
            InvokePrivate(window, "OnHierarchyChange");
            Assert.That(GetPrivateField<object>(window, "localPreviewBuildSession"), Is.Not.Null,
                "Preview-scene clone hierarchy events must not cancel the active incremental review build.");
            BuildLocalPreview(window);

            FigmaWireframeExportResult review = GetPrivateField<FigmaWireframeExportResult>(window, "localPreviewExport");
            Assert.That(review, Is.Not.Null);
            Assert.That(review.batch.items, Has.Count.EqualTo(1));
            Assert.That(review.batch.items[0].sourceKind, Is.EqualTo("scene-hierarchy"));
            Assert.That(review.batch.items[0].hierarchyPath, Is.EqualTo("UI Main Canvas/UI Quit Pop Up"));
            Assert.That(review.batch.items[0].nodes.Any(node => !string.IsNullOrEmpty(node.visualImageId)), Is.True,
                "Page 1 Assets review must have component PNGs.");
            Assert.That(review.batch.items[0].nodes.Any(node => node.rect != null), Is.True,
                "Page 2 Wireframe review must have editable geometry.");
            AssertReferenceImageIsVisible(review, review.batch.items[0]);

            InvokePrivate(window, "OnHierarchyChange");
            Assert.That(GetPrivateField<FigmaWireframeExportResult>(window, "localPreviewExport"), Is.SameAs(review),
                "Temporary preview-scene hierarchy events must not erase the completed Page 1-3 review batch.");
        }
        finally
        {
            Object.DestroyImmediate(window);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded && previousActiveScene != scene)
                SceneManager.SetActiveScene(previousActiveScene);
            if (openedForTest && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ExportSceneHierarchy_DisabledNestedCanvasStillCapturesUiResultWithoutChangingScene()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        Scene previousActiveScene = SceneManager.GetActiveScene();
        try
        {
            Assert.That(SceneManager.SetActiveScene(scene), Is.True);
            GameObject canvasRoot = scene.GetRootGameObjects().Single(root => root.name == "UI Main Canvas");
            GameObject store = canvasRoot.transform.Find("UI Store").gameObject;
            Canvas nestedCanvas = store.GetComponent<Canvas>();
            bool wasDirty = scene.isDirty;

            Assert.That(nestedCanvas, Is.Not.Null);
            Assert.That(nestedCanvas.enabled, Is.False, "The regression fixture must keep the Scene UI Store Canvas disabled.");

            FigmaWireframeExportResult result = PicturePuzzleFigmaWireframeExporter.ExportSceneHierarchies(new[] { store });
            FigmaWireframeItem item = result.batch.items.Single();

            Assert.That(item.sourcePrefabGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(StorePath)));
            Assert.That(item.referenceImageId, Is.EqualTo(item.itemId));
            AssertReferenceImageIsVisible(result, item);
            FigmaWireframeNode visualNode = item.nodes.FirstOrDefault(node => !string.IsNullOrEmpty(node.visualImageId));
            Assert.That(visualNode, Is.Not.Null, "UI Result must contain at least one rendered component layer.");
            AssertVisualImageIsVisible(result, visualNode);
            FigmaWireframeNode rootNode = item.nodes.Single(node => string.IsNullOrEmpty(node.parentId));
            Assert.That(rootNode.name, Is.EqualTo("UI Store"));
            Assert.That(rootNode.includeInAssetReview, Is.False);
            Assert.That(rootNode.assetImageId, Is.Null.Or.Empty,
                "The selected root is review context, not a reusable child asset.");
            Assert.That(rootNode.nestedPrefabPath, Is.EqualTo(StorePath),
                "Prefab identity metadata must not depend on generating a root asset PNG.");

            FigmaWireframeNode directBackground = item.nodes.Single(node =>
                node.parentId == rootNode.id
                && node.name == "Background"
                && (node.role == "image" || node.role == "raw-image"));
            Assert.That(directBackground.includeInAssetReview, Is.False);
            Assert.That(directBackground.assetImageId, Is.Null.Or.Empty);
            Assert.That(directBackground.visualImageId, Is.Not.Empty,
                "The excluded Assets card must remain in the layered UI Result.");

            FigmaWireframeNode nestedBackground = item.nodes.FirstOrDefault(node =>
                node.parentId != rootNode.id
                && node.name == "Background"
                && node.includeInAssetReview);
            Assert.That(nestedBackground, Is.Not.Null,
                "Backgrounds inside reusable child components must stay on the Assets page.");
            FigmaWireframeNode reusableChild = item.nodes.FirstOrDefault(node =>
                node.includeInAssetReview && !string.IsNullOrEmpty(node.assetImageId));
            Assert.That(reusableChild, Is.Not.Null);
            AssertAssetImageIsVisible(result, reusableChild);
            Assert.That(item.warnings.Any(warning => warning.Contains("Preview renderer returned a blank image")), Is.False);
            Assert.That(item.warnings.Any(warning => warning.Contains("The isolated nested prefab rendered no visible pixels")), Is.False);
            Assert.That(nestedCanvas.enabled, Is.False, "Preview normalization must not modify the source Scene Canvas.");
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
        }
        finally
        {
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded && previousActiveScene != scene)
                SceneManager.SetActiveScene(previousActiveScene);
            if (openedForTest && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ExportSceneHierarchy_SelectedPrefabRootKeepsFullResolutionVisualAssets()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        Scene previousActiveScene = SceneManager.GetActiveScene();
        try
        {
            Assert.That(SceneManager.SetActiveScene(scene), Is.True);
            GameObject canvasRoot = scene.GetRootGameObjects().Single(root => root.name == "UI Main Canvas");
            GameObject addLivesPanel = canvasRoot.transform.Find("UI Add Lives Panel").gameObject;
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(addLivesPanel);
            bool wasDirty = scene.isDirty;

            FigmaWireframeExportResult result = PicturePuzzleFigmaWireframeExporter.ExportSceneHierarchies(
                new[] { addLivesPanel });
            FigmaWireframeItem item = result.batch.items.Single();
            FigmaWireframeNode rootNode = item.nodes.Single(node => string.IsNullOrEmpty(node.parentId));

            Assert.That(prefabPath, Is.Not.Empty);
            Assert.That(rootNode.nestedPrefabPath, Is.EqualTo(prefabPath),
                "The selected prefab root must retain its canonical identity for Figma upserts.");
            Assert.That(rootNode.assetImageId, Is.Null.Or.Empty);
            Assert.That(item.nodes.Any(node => !string.IsNullOrEmpty(node.assetImageId)
                                               && string.Equals(node.nestedPrefabPath, prefabPath)), Is.False,
                "The selected source root must not isolate itself and replace correct visual PNGs with tiny assets.");

            FigmaWireframeNode panelGraphics = item.nodes.Single(node => node.name == "Panel Graphics");
            Assert.That(panelGraphics.visualImageId, Is.Not.Empty);
            Assert.That(panelGraphics.assetImageId, Is.Null.Or.Empty);
            Assert.That(panelGraphics.visualRect.width, Is.GreaterThan(700f));
            Assert.That(panelGraphics.visualRect.height, Is.GreaterThan(900f));
            AssertVisualImageIsVisible(result, panelGraphics);
            Assert.That(scene.isDirty, Is.EqualTo(wasDirty));
        }
        finally
        {
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded && previousActiveScene != scene)
                SceneManager.SetActiveScene(previousActiveScene);
            if (openedForTest && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ExportSceneHierarchies_SamePrefabAcrossScenesEmitsSharedSourcePrefabGuid()
    {
        Scene gameScene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedGameScene = !gameScene.isLoaded;
        if (openedGameScene)
            gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        Scene menuScene = SceneManager.GetSceneByPath(MenuScenePath);
        bool openedMenuScene = !menuScene.isLoaded;
        if (openedMenuScene)
            menuScene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Additive);

        Scene previousActiveScene = SceneManager.GetActiveScene();
        try
        {
            GameObject gameCanvas = gameScene.GetRootGameObjects().Single(root => root.name == "UI Main Canvas");
            GameObject menuCanvas = menuScene.GetRootGameObjects().Single(root => root.name == "UI Main Canvas");
            GameObject gameStore = gameCanvas.transform.Find("UI Store").gameObject;
            GameObject menuStore = menuCanvas.transform.Find("UI Store").gameObject;
            bool gameDirty = gameScene.isDirty;
            bool menuDirty = menuScene.isDirty;

            FigmaWireframeItem gameItem = PicturePuzzleFigmaWireframeExporter
                .ExportSceneHierarchies(new[] { gameStore }).batch.items.Single();
            FigmaWireframeItem menuItem = PicturePuzzleFigmaWireframeExporter
                .ExportSceneHierarchies(new[] { menuStore }).batch.items.Single();

            Assert.That(gameItem.sourceKind, Is.EqualTo("scene-hierarchy"));
            Assert.That(menuItem.sourceKind, Is.EqualTo("scene-hierarchy"));
            Assert.That(gameItem.sourcePrefabGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(StorePath)));
            Assert.That(menuItem.sourcePrefabGuid, Is.EqualTo(gameItem.sourcePrefabGuid),
                "The same prefab instance root in Game.unity and Menu.unity must share one Figma identity.");
            Assert.That(gameItem.assetPath, Is.Not.EqualTo(menuItem.assetPath));
            Assert.That(gameItem.hierarchyPath, Is.EqualTo(menuItem.hierarchyPath));
            Assert.That(gameScene.isDirty, Is.EqualTo(gameDirty));
            Assert.That(menuScene.isDirty, Is.EqualTo(menuDirty));
        }
        finally
        {
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
            if (openedMenuScene && menuScene.IsValid() && menuScene.isLoaded)
                EditorSceneManager.CloseScene(menuScene, true);
            if (openedGameScene && gameScene.IsValid() && gameScene.isLoaded)
                EditorSceneManager.CloseScene(gameScene, true);
        }
    }

    [Test]
    public void ExportWindow_CheckingSceneUiMarksReviewDirtyUntilTheUserBuildsIt()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        Scene previousActiveScene = SceneManager.GetActiveScene();
        var window = ScriptableObject.CreateInstance<PicturePuzzleFigmaWireframeExporterWindow>();
        try
        {
            Assert.That(SceneManager.SetActiveScene(scene), Is.True);
            GameObject canvasRoot = scene.GetRootGameObjects().Single(root => root.name == "UI Main Canvas");
            GameObject quitPopup = canvasRoot.transform.Find("UI Quit Pop Up").gameObject;
            GameObject addLivesPanel = canvasRoot.transform.Find("UI Add Lives Panel").gameObject;

            InvokePrivate(window, "SetSceneSourceChecked", quitPopup, true);
            BuildLocalPreview(window);
            FigmaWireframeExportResult previousReview = GetPrivateField<FigmaWireframeExportResult>(window, "localPreviewExport");

            InvokePrivate(window, "SetSceneSourceChecked", addLivesPanel, true);
            Assert.That(GetPrivateField<FigmaWireframeExportResult>(window, "localPreviewExport"), Is.SameAs(previousReview),
                "Checking another Scene UI source must keep the old review visible without destroying its cached textures.");
            Assert.That(GetPrivateField<bool>(window, "localPreviewRebuildPending"), Is.True,
                "A checkbox change must only mark the review dirty; it must not synchronously re-export the growing batch.");
            Assert.That(GetPrivateField<string>(window, "exportStatus"), Does.Contain("Click Build 3-page review"));
            InvokePrivate(window, "BuildLocalPreview");
            Assert.That(GetPrivateField<object>(window, "localPreviewBuildSession"), Is.Not.Null);
            Assert.That(GetPrivateField<FigmaWireframeExportResult>(window, "localPreviewExport"), Is.Null,
                "The old review is released only after the user explicitly starts a replacement build.");
            InvokePrivate(window, "BuildNextLocalPreview");
            Assert.That(GetPrivateField<object>(window, "localPreviewBuildSession"), Is.Not.Null,
                "Each editor update must render only one checked source, not the entire batch.");
            InvokePrivate(window, "BuildNextLocalPreview");

            FigmaWireframeExportResult refreshedReview = GetPrivateField<FigmaWireframeExportResult>(window, "localPreviewExport");
            int focusedIndex = GetPrivateField<int>(window, "localPreviewItemIndex");
            FigmaWireframeItem focusedItem = refreshedReview.batch.items[focusedIndex];
            Assert.That(refreshedReview, Is.Not.SameAs(previousReview));
            Assert.That(refreshedReview.batch.items, Has.Count.EqualTo(2));
            Assert.That(refreshedReview.batch.items.All(item => item.sourceKind == "scene-hierarchy"), Is.True);
            Assert.That(focusedItem.sourceKind, Is.EqualTo("scene-hierarchy"));
            Assert.That(focusedItem.hierarchyPath, Is.EqualTo("UI Main Canvas/UI Add Lives Panel"),
                "The newly checked Scene UI hierarchy must replace the old hierarchy as the visible review item.");
        }
        finally
        {
            Object.DestroyImmediate(window);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded && previousActiveScene != scene)
                SceneManager.SetActiveScene(previousActiveScene);
            if (openedForTest && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ExportWindow_SceneChangeCancelsIncrementalPreviewBeforeItCanBeSent()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        Scene previousActiveScene = SceneManager.GetActiveScene();
        var window = ScriptableObject.CreateInstance<PicturePuzzleFigmaWireframeExporterWindow>();
        try
        {
            Assert.That(SceneManager.SetActiveScene(scene), Is.True);
            GameObject canvasRoot = scene.GetRootGameObjects().Single(root => root.name == "UI Main Canvas");
            GameObject quitPopup = canvasRoot.transform.Find("UI Quit Pop Up").gameObject;
            GameObject addLivesPanel = canvasRoot.transform.Find("UI Add Lives Panel").gameObject;

            InvokePrivate(window, "SetSceneSourceChecked", quitPopup, true);
            InvokePrivate(window, "SetSceneSourceChecked", addLivesPanel, true);
            InvokePrivate(window, "BuildLocalPreview");
            InvokePrivate(window, "BuildNextLocalPreview");
            Assert.That(GetPrivateField<object>(window, "localPreviewBuildSession"), Is.Not.Null);

            InvokePrivate(window, "OnSceneDirtied", scene);

            Assert.That(GetPrivateField<object>(window, "localPreviewBuildSession"), Is.Null,
                "A Scene edit must cancel the incremental capture rather than mix old and new UI into one reviewed batch.");
            Assert.That(GetPrivateField<bool>(window, "localPreviewRebuildPending"), Is.True,
                "A canceled review stays dirty, which keeps Send disabled until the user builds it again.");
            Assert.That(GetPrivateField<FigmaWireframeExportResult>(window, "localPreviewExport"), Is.Null);
            Assert.That(GetPrivateField<string>(window, "exportStatus"), Does.Contain("changed while the preview was building"));
        }
        finally
        {
            Object.DestroyImmediate(window);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded && previousActiveScene != scene)
                SceneManager.SetActiveScene(previousActiveScene);
            if (openedForTest && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ExportWindow_ReviewedBatchLimitPreventsOversizedSynchronousBuilds()
    {
        System.Type windowType = typeof(PicturePuzzleFigmaWireframeExporterWindow);

        Assert.That(InvokePrivateStatic<bool>(windowType, "CanBuildReviewedSceneSourceCount", 1), Is.True);
        Assert.That(InvokePrivateStatic<bool>(windowType, "CanBuildReviewedSceneSourceCount", 12), Is.True);
        Assert.That(InvokePrivateStatic<bool>(windowType, "CanBuildReviewedSceneSourceCount", 13), Is.False,
            "The local 3-page renderer must refuse an oversized batch rather than allocating every capture at once.");
    }

    [Test]
    public void ExportWindow_LocalPreviewImageCacheIsBounded()
    {
        var window = ScriptableObject.CreateInstance<PicturePuzzleFigmaWireframeExporterWindow>();
        var textures = new List<Texture2D>();
        try
        {
            for (int index = 0; index < 19; index++)
            {
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                textures.Add(texture);
                InvokePrivate(window, "CacheLocalPreviewImage", "artifact-" + index, texture);
            }

            Assert.That(GetPrivateField<Dictionary<string, Texture2D>>(window, "localPreviewImageCache").Count, Is.EqualTo(18));
            Assert.That(textures[0] == null, Is.True,
                "The least-recent preview texture must be released before the cache grows unbounded.");
        }
        finally
        {
            Object.DestroyImmediate(window);
            foreach (Texture2D texture in textures.Where(texture => texture != null))
                Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void ExportWindow_CheckingManySceneUiDoesNotBuildAnyExportUntilRequested()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        Scene previousActiveScene = SceneManager.GetActiveScene();
        var window = ScriptableObject.CreateInstance<PicturePuzzleFigmaWireframeExporterWindow>();
        try
        {
            Assert.That(SceneManager.SetActiveScene(scene), Is.True);
            GameObject canvasRoot = scene.GetRootGameObjects().Single(root => root.name == "UI Main Canvas");
            List<GameObject> candidates = canvasRoot.GetComponentsInChildren<RectTransform>(true)
                .Select(transform => transform.gameObject)
                .Where(PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy)
                .Where(candidate => candidate != canvasRoot)
                .Take(13)
                .ToList();
            Assert.That(candidates, Has.Count.EqualTo(13));

            string exportRoot = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                PicturePuzzleFigmaWireframeExporter.ExportRootRelative);
            int exportCountBefore = Directory.Exists(exportRoot) ? Directory.GetDirectories(exportRoot).Length : 0;
            foreach (GameObject candidate in candidates)
                InvokePrivate(window, "SetSceneSourceChecked", candidate, true);

            Assert.That(GetPrivateField<List<GameObject>>(window, "selectedSceneRoots"), Has.Count.EqualTo(13));
            Assert.That(GetPrivateField<FigmaWireframeExportResult>(window, "localPreviewExport"), Is.Null);
            Assert.That(GetPrivateField<bool>(window, "localPreviewRebuildPending"), Is.True);
            Assert.That(Directory.Exists(exportRoot) ? Directory.GetDirectories(exportRoot).Length : 0, Is.EqualTo(exportCountBefore),
                "Checkboxes must only collect UI roots; they must not create a progressively larger export batch.");
        }
        finally
        {
            Object.DestroyImmediate(window);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded && previousActiveScene != scene)
                SceneManager.SetActiveScene(previousActiveScene);
            if (openedForTest && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ExportWindow_SelectionChangeCancelsWaitingSendAndKeepsBusyRebuildPending()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        Scene previousActiveScene = SceneManager.GetActiveScene();
        var window = ScriptableObject.CreateInstance<PicturePuzzleFigmaWireframeExporterWindow>();
        try
        {
            Assert.That(SceneManager.SetActiveScene(scene), Is.True);
            GameObject canvasRoot = scene.GetRootGameObjects().Single(root => root.name == "UI Main Canvas");
            GameObject quitPopup = canvasRoot.transform.Find("UI Quit Pop Up").gameObject;
            GameObject addLivesPanel = canvasRoot.transform.Find("UI Add Lives Panel").gameObject;

            InvokePrivate(window, "SetSceneSourceChecked", quitPopup, true);
            BuildLocalPreview(window);
            FigmaWireframeExportResult staleReview = GetPrivateField<FigmaWireframeExportResult>(window, "localPreviewExport");

            SetPrivateField(window, "sendWhenPluginConnects", true);
            InvokePrivate(window, "SetSceneSourceChecked", addLivesPanel, true);
            Assert.That(GetPrivateField<bool>(window, "sendWhenPluginConnects"), Is.False,
                "Changing checked Scene UI must cancel the pending automatic Figma send.");
            Assert.That(GetPrivateField<bool>(window, "localPreviewRebuildPending"), Is.True);
            Assert.That(GetPrivateField<FigmaWireframeExportResult>(window, "localPreviewExport"), Is.SameAs(staleReview));

            object[] validationArguments = { staleReview, null };
            Assert.That((bool)InvokePrivate(window, "IsReviewedBatchCurrent", validationArguments), Is.False,
                "A batch captured before the Scene UI selection changed must never be sent after an await.");
            Assert.That((string)validationArguments[1], Does.Contain("selection changed"));

            SetPrivateField(window, "exportInProgress", true);
            BuildLocalPreview(window);
            Assert.That(GetPrivateField<bool>(window, "localPreviewRebuildPending"), Is.True,
                "A busy send must keep the rebuild pending instead of dropping it.");
            SetPrivateField(window, "exportInProgress", false);
            InvokePrivate(window, "FinishExportOperation");
            BuildLocalPreview(window);

            FigmaWireframeExportResult refreshedReview = GetPrivateField<FigmaWireframeExportResult>(window, "localPreviewExport");
            int focusedIndex = GetPrivateField<int>(window, "localPreviewItemIndex");
            Assert.That(GetPrivateField<bool>(window, "localPreviewRebuildPending"), Is.False);
            Assert.That(refreshedReview.batch.items[focusedIndex].hierarchyPath,
                Is.EqualTo("UI Main Canvas/UI Add Lives Panel"));
        }
        finally
        {
            Object.DestroyImmediate(window);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded && previousActiveScene != scene)
                SceneManager.SetActiveScene(previousActiveScene);
            if (openedForTest && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ExportWindow_ExplicitBuildCancelsWaitingFigmaSend()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        Scene previousActiveScene = SceneManager.GetActiveScene();
        var window = ScriptableObject.CreateInstance<PicturePuzzleFigmaWireframeExporterWindow>();
        try
        {
            Assert.That(SceneManager.SetActiveScene(scene), Is.True);
            GameObject canvasRoot = scene.GetRootGameObjects().Single(root => root.name == "UI Main Canvas");
            GameObject quitPopup = canvasRoot.transform.Find("UI Quit Pop Up").gameObject;

            InvokePrivate(window, "SetSceneSourceChecked", quitPopup, true);
            SetPrivateField(window, "sendWhenPluginConnects", true);

            InvokePrivate(window, "BuildLocalPreview");

            Assert.That(GetPrivateField<bool>(window, "sendWhenPluginConnects"), Is.False,
                "An explicit preview build must supersede a pending automatic Figma send.");
            Assert.That(GetPrivateField<object>(window, "localPreviewBuildSession"), Is.Not.Null,
                "Build must start immediately instead of silently returning while Figma is reconnecting.");
        }
        finally
        {
            Object.DestroyImmediate(window);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded && previousActiveScene != scene)
                SceneManager.SetActiveScene(previousActiveScene);
            if (openedForTest && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void ExportWindow_PrunedSceneSelectionCancelsWaitingSendAndKeepsBusyRebuildPending()
    {
        Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        bool openedForTest = !scene.isLoaded;
        if (openedForTest)
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

        Scene previousActiveScene = SceneManager.GetActiveScene();
        var window = ScriptableObject.CreateInstance<PicturePuzzleFigmaWireframeExporterWindow>();
        try
        {
            Assert.That(SceneManager.SetActiveScene(scene), Is.True);
            GameObject canvasRoot = scene.GetRootGameObjects().Single(root => root.name == "UI Main Canvas");
            GameObject quitPopup = canvasRoot.transform.Find("UI Quit Pop Up").gameObject;

            InvokePrivate(window, "SetSceneSourceChecked", quitPopup, true);
            BuildLocalPreview(window);
            List<GameObject> selectedRoots = GetPrivateField<List<GameObject>>(window, "selectedSceneRoots");

            selectedRoots.Add(null);
            SetPrivateField(window, "sendWhenPluginConnects", true);
            Assert.That((bool)InvokePrivate(window, "PruneSelections", true), Is.True);
            Assert.That(GetPrivateField<bool>(window, "sendWhenPluginConnects"), Is.False,
                "Pruning a destroyed checked root must cancel an automatic send waiting for Figma.");
            Assert.That(GetPrivateField<bool>(window, "localPreviewRebuildPending"), Is.True);
            Assert.That(GetPrivateField<FigmaWireframeExportResult>(window, "localPreviewExport"), Is.Not.Null,
                "Pruning a stale selection must not synchronously destroy every review texture.");

            BuildLocalPreview(window);
            selectedRoots.Add(null);
            SetPrivateField(window, "exportInProgress", true);
            Assert.That((bool)InvokePrivate(window, "PruneSelections", true), Is.True);
            Assert.That(GetPrivateField<bool>(window, "localPreviewRebuildPending"), Is.True,
                "Pruning a destroyed checked root during a send must preserve the delayed rebuild.");
        }
        finally
        {
            Object.DestroyImmediate(window);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded && previousActiveScene != scene)
                SceneManager.SetActiveScene(previousActiveScene);
            if (openedForTest && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void WireframePreviewLabels_OnlyShowRealPopupText()
    {
        FigmaWireframeExportResult result = PicturePuzzleFigmaWireframeExporter.Export(new[] { PopupPath });
        FigmaWireframeItem popup = result.batch.items.Single();
        System.Type windowType = typeof(PicturePuzzleFigmaWireframeExporterWindow);

        List<string> labels = popup.nodes
            .Select(node => InvokePrivateStatic<string>(windowType, "GetWireframePreviewLabel", node))
            .Where(label => !string.IsNullOrEmpty(label))
            .ToList();

        Assert.That(labels, Is.EquivalentTo(new[] { "UPDATE AVAILABLE", "A new version is out!", "Update" }));
        Assert.That(popup.nodes
            .Where(node => node.role != "text")
            .All(node => string.IsNullOrEmpty(InvokePrivateStatic<string>(windowType, "GetWireframePreviewLabel", node))),
            Is.True,
            "Container, image, icon, and button names must not be drawn over the local wireframe canvas.");
        Assert.That(labels, Does.Not.Contain("UpdateAvailablePopup"));
        Assert.That(labels, Does.Not.Contain("Popup Panel"));
        Assert.That(labels, Does.Not.Contain("Title Ribbon"));
        Assert.That(labels, Does.Not.Contain("Update Icon"));
        Assert.That(labels, Does.Not.Contain("Update Button"));
    }

    private static void BuildLocalPreview(PicturePuzzleFigmaWireframeExporterWindow window)
    {
        InvokePrivate(window, "BuildLocalPreview");
        for (int step = 0; step < 32; step++)
        {
            if (GetPrivateField<object>(window, "localPreviewBuildSession") == null) return;
            InvokePrivate(window, "BuildNextLocalPreview");
        }

        Assert.Fail("The incremental local preview build did not complete within the expected source budget.");
    }

    private static List<GameObject> GetProfilePopupDialogRoots(Scene scene)
    {
        GameObject canvasRoot = scene.GetRootGameObjects().Single(root => root.name == "UI Main Canvas");
        Transform dialogRoot = canvasRoot.transform.Find("UI Profile Popup/Dialog Root");
        Assert.That(dialogRoot, Is.Not.Null);
        return Enumerable.Range(0, dialogRoot.childCount)
            .Select(index => dialogRoot.GetChild(index).gameObject)
            .Where(PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy)
            .ToList();
    }

    private static object InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == methodName && candidate.GetParameters().Length == arguments.Length);
        return method.Invoke(target, arguments);
    }

    private static T InvokePrivateStatic<T>(System.Type targetType, string methodName, params object[] arguments)
    {
        MethodInfo method = targetType
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == methodName && candidate.GetParameters().Length == arguments.Length);
        return (T)method.Invoke(null, arguments);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing private field " + fieldName);
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing private field " + fieldName);
        field.SetValue(target, value);
    }

    private static void AssertReferenceImageIsVisible(FigmaWireframeExportResult result, FigmaWireframeItem item)
    {
        string imagePath = Path.Combine(Path.GetDirectoryName(result.manifestPath), item.itemId + ".png");
        Assert.That(File.Exists(imagePath), Is.True);
        var referenceImage = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Assert.That(ImageConversion.LoadImage(referenceImage, File.ReadAllBytes(imagePath)), Is.True);
            Assert.That(referenceImage.GetPixels32().Any(pixel => pixel.r != 0 || pixel.g != 0 || pixel.b != 0), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(referenceImage);
        }
    }

    private static void AssertVisualImageIsVisible(FigmaWireframeExportResult result, FigmaWireframeNode node)
    {
        string imagePath = Path.Combine(Path.GetDirectoryName(result.manifestPath), node.visualImageId + ".png");
        Assert.That(File.Exists(imagePath), Is.True);
        Assert.That(new FileInfo(imagePath).Length, Is.GreaterThan(0));
        var visualImage = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Assert.That(ImageConversion.LoadImage(visualImage, File.ReadAllBytes(imagePath)), Is.True);
            Assert.That(visualImage.GetPixels32().Any(pixel => pixel.a != 0), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(visualImage);
        }
    }

    private static void AssertAssetImageIsVisible(FigmaWireframeExportResult result, FigmaWireframeNode node)
    {
        string imagePath = Path.Combine(Path.GetDirectoryName(result.manifestPath), node.assetImageId + ".png");
        Assert.That(File.Exists(imagePath), Is.True);
        var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Assert.That(ImageConversion.LoadImage(image, File.ReadAllBytes(imagePath)), Is.True);
            Assert.That(image.width, Is.EqualTo(Mathf.RoundToInt(node.assetRect.width)));
            Assert.That(image.height, Is.EqualTo(Mathf.RoundToInt(node.assetRect.height)));
            Assert.That(image.GetPixels32().Any(pixel => pixel.a != 0), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(image);
        }
    }
}
