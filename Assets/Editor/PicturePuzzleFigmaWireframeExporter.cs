#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace PicturePuzzle.EditorTools
{
    [Serializable]
    public sealed class FigmaWireframeCanvas
    {
        public int width;
        public int height;
    }

    [Serializable]
    public sealed class FigmaWireframeRect
    {
        public float x;
        public float y;
        public float width;
        public float height;
    }

    [Serializable]
    public sealed class FigmaWireframeColor
    {
        public float r;
        public float g;
        public float b;
        public float a;
    }

    [Serializable]
    public sealed class FigmaWireframeTextStyle
    {
        public string fontFamily;
        public string fontStyle;
        public float fontSize;
        public float lineHeight;
        public float letterSpacing;
        public string horizontalAlignment;
        public string verticalAlignment;
    }

    [Serializable]
    public sealed class FigmaWireframeNode
    {
        public string id;
        public string parentId;
        public string name;
        public string role;
        public int siblingIndex;
        public int renderOrder;
        public FigmaWireframeRect rect;
        public string text;
        public FigmaWireframeColor color;
        public string visualImageId;
        public FigmaWireframeRect visualRect;
        public string assetImageId;
        public FigmaWireframeRect assetRect;
        public string nestedPrefabPath;
        public bool includeInAssetReview = true;
        public bool clipsContent;
        public float opacity = 1f;
        public FigmaWireframeTextStyle textStyle;
    }

    [Serializable]
    public sealed class FigmaWireframeItem
    {
        public string itemId;
        public string sourceGuid;
        public string sourcePrefabGuid;
        public string sourceKind;
        public string assetPath;
        public string hierarchyPath;
        public string displayName;
        public string referenceImageId;
        public List<FigmaWireframeNode> nodes = new List<FigmaWireframeNode>();
        public List<string> warnings = new List<string>();
    }

    [Serializable]
    public sealed class FigmaWireframeBatch
    {
        public int schemaVersion = 2;
        public string batchId;
        public FigmaWireframeCanvas canvas = new FigmaWireframeCanvas
        {
            width = PicturePuzzleFigmaWireframeExporter.CanvasWidth,
            height = PicturePuzzleFigmaWireframeExporter.CanvasHeight
        };
        public List<FigmaWireframeItem> items = new List<FigmaWireframeItem>();
    }

    public sealed class FigmaWireframeExportResult
    {
        public FigmaWireframeBatch batch;
        public string manifestPath;
        public List<string> exportedPaths = new List<string>();
        public List<string> skippedPaths = new List<string>();
    }

    /// <summary>
    /// Produces a small, versioned interchange model for the local Figma bridge. The exporter only
    /// touches preview-scene instances, never the source prefab or an open Unity scene hierarchy.
    /// </summary>
    public static class PicturePuzzleFigmaWireframeExporter
    {
        public const int CanvasWidth = 1080;
        public const int CanvasHeight = 1920;
        public const string ExportRootRelative = "Library/PicturePuzzleFigma/exports";

        private sealed class NestedPrefabDescriptor
        {
            public List<int> pathFromRoot;
            public string assetPath;
        }

        public static List<string> ResolvePrefabPaths(Object source)
        {
            if (source == null) return new List<string>();
            return ResolvePrefabPaths(AssetDatabase.GetAssetPath(source));
        }

        public static List<string> ResolvePrefabPathsFromSelection(IEnumerable<Object> sources)
        {
            return (sources ?? Array.Empty<Object>())
                .Where(source => source != null)
                .SelectMany(source => ResolvePrefabPaths(source))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        public static List<string> ResolvePrefabPaths(string assetPath)
        {
            var paths = new List<string>();
            if (string.IsNullOrWhiteSpace(assetPath)) return paths;

            if (assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (IsUguiPrefab(prefab)) paths.Add(assetPath);
                return paths;
            }

            if (!AssetDatabase.IsValidFolder(assetPath)) return paths;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { assetPath }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (IsUguiPrefab(prefab)) paths.Add(path);
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        public static bool IsUguiPrefab(GameObject prefab)
        {
            return prefab != null
                && prefab.GetComponentInChildren<RectTransform>(true) != null
                && (prefab.GetComponentInChildren<Graphic>(true) != null
                    || prefab.GetComponentInChildren<Selectable>(true) != null
                    || prefab.GetComponentInChildren<ScrollRect>(true) != null);
        }

        public static Texture2D CreatePrefabPreviewTexture(string prefabPath, int width = 216, int height = 384)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!IsUguiPrefab(prefab)) return null;

            using (var preview = new UguiPreviewScene(Mathf.Max(64, width), Mathf.Max(64, height)))
            {
                GameObject instance = preview.InstantiatePrefab(prefab);
                if (instance == null) return null;

                PrepareInstance(instance, preview);
                RebuildLayout(instance);
                preview.Render();
                return FinishPreviewTexture(preview, prefab.name);
            }
        }

        public static bool IsUguiSceneHierarchy(GameObject root)
        {
            return root != null
                && !EditorUtility.IsPersistent(root)
                && root.transform is RectTransform
                && root.scene.IsValid()
                && root.scene.isLoaded
                && root.scene.path.StartsWith("Assets/", StringComparison.Ordinal)
                && FindOutermostCanvas(root.transform) != null;
        }

        public static Texture2D CreateScenePreviewTexture(GameObject sourceRoot, int width = 216, int height = 384)
        {
            if (!IsUguiSceneHierarchy(sourceRoot)) return null;

            Canvas sourceCanvas = FindOutermostCanvas(sourceRoot.transform);
            List<int> pathFromCanvas = BuildSiblingIndexPath(sourceCanvas.transform, sourceRoot.transform);
            using (var preview = new UguiPreviewScene(Mathf.Max(64, width), Mathf.Max(64, height)))
            {
                GameObject canvasInstance = preview.InstantiateSceneObject(sourceCanvas.gameObject);
                if (canvasInstance == null) return null;

                Transform instanceRoot = FollowSiblingIndexPath(canvasInstance.transform, pathFromCanvas);
                ActivatePreviewBranch(canvasInstance.transform, instanceRoot);
                PrepareInstance(canvasInstance, preview);
                RebuildLayout(canvasInstance);
                DisableRenderingOutsideBranch(canvasInstance, instanceRoot);
                preview.Render();
                return FinishPreviewTexture(preview, sourceRoot.name);
            }
        }

        private static Texture2D FinishPreviewTexture(UguiPreviewScene preview, string sourceName)
        {
            Texture2D texture = preview.EndStaticPreview();
            if (texture == null) return null;
            if (!HasVisiblePixels(texture))
            {
                Object.DestroyImmediate(texture);
                return null;
            }

            texture.name = sourceName + " Figma Preview";
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        public static string BuildSceneSourceId(GameObject root)
        {
            if (!IsUguiSceneHierarchy(root))
                throw new ArgumentException("The object must be a RectTransform under a Canvas in a saved scene.", nameof(root));

            string sceneGuid = AssetDatabase.AssetPathToGUID(root.scene.path);
            GlobalObjectId globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(root);
            if (string.IsNullOrEmpty(sceneGuid) || globalObjectId.targetObjectId == 0)
                throw new InvalidOperationException("The scene hierarchy object does not have a stable saved object ID.");

            return sceneGuid + "-" + globalObjectId.targetObjectId.ToString(CultureInfo.InvariantCulture);
        }

        public static string GetSceneHierarchyPath(GameObject root)
        {
            return root == null ? string.Empty : GetReadablePath(root.transform);
        }

        public static List<GameObject> GetSceneUguiHierarchyRoots(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return new List<GameObject>();

            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                .Where(canvas => FindOutermostCanvas(canvas.transform) == canvas)
                .Select(canvas => canvas.gameObject)
                .Distinct()
                .OrderBy(GetSceneHierarchyPath, StringComparer.Ordinal)
                .ToList();
        }

        public static string BuildStableNodeId(string sourceGuid, string transformPath)
        {
            return sourceGuid + "-" + transformPath.Replace("/", "-");
        }

        public static FigmaWireframeExportResult Export(IEnumerable<string> prefabPaths)
        {
            return ExportSources(prefabPaths, Enumerable.Empty<GameObject>());
        }

        public static FigmaWireframeExportResult ExportSceneHierarchies(IEnumerable<GameObject> roots)
        {
            var session = CreateSceneExportSession(roots);
            while (session.ExportNext()) { }
            return session.result;
        }

        /// <summary>
        /// Exports one checked Scene UI root at a time. Editor UI can advance this session on separate
        /// update ticks, avoiding a single long-running capture for a whole selection.
        /// </summary>
        public sealed class SceneExportSession
        {
            private readonly List<GameObject> roots;
            private readonly string outputDirectory;
            private int nextRootIndex;
            private bool completed;

            internal SceneExportSession(IEnumerable<GameObject> sourceRoots)
            {
                result = CreateResult(out outputDirectory);
                roots = (sourceRoots ?? Enumerable.Empty<GameObject>())
                    .Where(root => root != null)
                    .Distinct()
                    .ToList();
            }

            public FigmaWireframeExportResult result { get; private set; }
            public int totalCount => roots.Count;
            public int completedCount => nextRootIndex;
            public bool isComplete => completed;

            public bool ExportNext()
            {
                if (completed) return false;
                if (nextRootIndex < roots.Count)
                    ExportSceneRoot(result, roots[nextRootIndex++], outputDirectory);

                if (nextRootIndex >= roots.Count)
                {
                    WriteManifest(result, outputDirectory);
                    completed = true;
                }

                return true;
            }

            public void Cancel()
            {
                completed = true;
            }
        }

        public static SceneExportSession CreateSceneExportSession(IEnumerable<GameObject> roots)
        {
            return new SceneExportSession(roots);
        }

        public static FigmaWireframeExportResult ExportSources(
            IEnumerable<string> prefabPaths,
            IEnumerable<GameObject> sceneRoots)
        {
            string outputDirectory;
            FigmaWireframeExportResult result = CreateResult(out outputDirectory);

            foreach (string prefabPath in (prefabPaths ?? Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (!IsUguiPrefab(prefab))
                {
                    result.skippedPaths.Add(prefabPath);
                    continue;
                }

                try
                {
                    FigmaWireframeItem item = ExportItem(prefab, prefabPath, outputDirectory);
                    result.batch.items.Add(item);
                    result.exportedPaths.Add(prefabPath);
                }
                catch (Exception exception)
                {
                    result.skippedPaths.Add(prefabPath + " - " + exception.Message);
                    Debug.LogWarning("[PicturePuzzle Figma] Skipped " + prefabPath + ": " + exception);
                }
            }

            foreach (GameObject root in (sceneRoots ?? Enumerable.Empty<GameObject>()).Where(candidate => candidate != null).Distinct())
                ExportSceneRoot(result, root, outputDirectory);

            WriteManifest(result, outputDirectory);
            return result;
        }

        private static void ExportSceneRoot(FigmaWireframeExportResult result, GameObject root, string outputDirectory)
        {
            string sourceLabel = root.scene.IsValid()
                ? root.scene.path + " :: " + GetSceneHierarchyPath(root)
                : root.name;
            if (!IsUguiSceneHierarchy(root))
            {
                result.skippedPaths.Add(sourceLabel);
                return;
            }

            try
            {
                FigmaWireframeItem item = ExportSceneItem(root, outputDirectory);
                result.batch.items.Add(item);
                result.exportedPaths.Add(sourceLabel);
            }
            catch (Exception exception)
            {
                result.skippedPaths.Add(sourceLabel + " - " + exception.Message);
                Debug.LogWarning("[PicturePuzzle Figma] Skipped " + sourceLabel + ": " + exception);
            }
        }

        private static FigmaWireframeExportResult CreateResult(out string outputDirectory)
        {
            var result = new FigmaWireframeExportResult
            {
                batch = new FigmaWireframeBatch
                {
                    batchId = "wireframe-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8)
                }
            };
            outputDirectory = Path.Combine(ProjectRoot, ExportRootRelative, result.batch.batchId);
            Directory.CreateDirectory(outputDirectory);
            return result;
        }

        private static void WriteManifest(FigmaWireframeExportResult result, string outputDirectory)
        {
            result.manifestPath = Path.Combine(outputDirectory, "manifest.json");
            File.WriteAllText(result.manifestPath, JsonUtility.ToJson(result.batch, true), new UTF8Encoding(false));
        }

        private static FigmaWireframeItem ExportItem(GameObject prefab, string prefabPath, string outputDirectory)
        {
            string sourceGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            List<NestedPrefabDescriptor> nestedPrefabs = FindNestedPrefabDescriptors(prefab.transform, prefabPath);
            var item = new FigmaWireframeItem
            {
                itemId = sourceGuid,
                sourceGuid = sourceGuid,
                sourcePrefabGuid = sourceGuid,
                sourceKind = "prefab",
                assetPath = prefabPath,
                hierarchyPath = string.Empty,
                displayName = Path.GetFileNameWithoutExtension(prefabPath)
            };

            using (var preview = new UguiPreviewScene())
            {
                GameObject instance = preview.InstantiatePrefab(prefab);
                if (instance == null) throw new InvalidOperationException("Could not instantiate prefab in preview scene.");

                PrepareInstance(instance, preview);
                RebuildLayout(instance);

                var transformNodes = new Dictionary<Transform, FigmaWireframeNode>();
                DescribeTransform(instance.transform, sourceGuid, "0", null, preview.camera, item.nodes, item.warnings, transformNodes);
                if (item.nodes.Count == 0)
                    throw new InvalidOperationException("The prefab has no active UGUI nodes to export.");
                MarkAssetReviewNodes(item.nodes);

                CaptureVisualLayers(preview, instance.transform, transformNodes, item, outputDirectory);
                CaptureNestedPrefabAssets(preview, instance.transform, nestedPrefabs, transformNodes, item, outputDirectory);
                CaptureReference(preview, item, outputDirectory);
            }

            return item;
        }

        private static FigmaWireframeItem ExportSceneItem(GameObject sourceRoot, string outputDirectory)
        {
            string sourceId = BuildSceneSourceId(sourceRoot);
            Canvas sourceCanvas = FindOutermostCanvas(sourceRoot.transform);
            List<int> pathFromCanvas = BuildSiblingIndexPath(sourceCanvas.transform, sourceRoot.transform);
            List<NestedPrefabDescriptor> nestedPrefabs = FindNestedPrefabDescriptors(sourceRoot.transform, sourceRoot.scene.path);
            var item = new FigmaWireframeItem
            {
                itemId = sourceId,
                sourceGuid = sourceId,
                sourcePrefabGuid = GetSceneSourcePrefabGuid(sourceRoot),
                sourceKind = "scene-hierarchy",
                assetPath = sourceRoot.scene.path,
                hierarchyPath = GetSceneHierarchyPath(sourceRoot),
                displayName = sourceRoot.name
            };

            using (var preview = new UguiPreviewScene())
            {
                GameObject canvasInstance = preview.InstantiateSceneObject(sourceCanvas.gameObject);
                if (canvasInstance == null)
                    throw new InvalidOperationException("Could not clone the source Canvas into the preview scene.");

                Transform instanceRoot = FollowSiblingIndexPath(canvasInstance.transform, pathFromCanvas);
                ActivatePreviewBranch(canvasInstance.transform, instanceRoot);
                PrepareInstance(canvasInstance, preview);
                RebuildLayout(canvasInstance);
                DisableRenderingOutsideBranch(canvasInstance, instanceRoot);

                var transformNodes = new Dictionary<Transform, FigmaWireframeNode>();
                DescribeTransform(instanceRoot, sourceId, "0", null, preview.camera, item.nodes, item.warnings, transformNodes);
                if (item.nodes.Count == 0)
                    throw new InvalidOperationException("The selected hierarchy has no active UGUI nodes to export.");
                MarkAssetReviewNodes(item.nodes);

                CaptureVisualLayers(preview, instanceRoot, transformNodes, item, outputDirectory);
                CaptureNestedPrefabAssets(preview, instanceRoot, nestedPrefabs, transformNodes, item, outputDirectory);
                CaptureReference(preview, item, outputDirectory);
            }

            return item;
        }

        private static string GetSceneSourcePrefabGuid(GameObject sourceRoot)
        {
            if (sourceRoot == null || PrefabUtility.GetNearestPrefabInstanceRoot(sourceRoot) != sourceRoot)
                return string.Empty;

            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(sourceRoot);
            return string.IsNullOrEmpty(prefabPath) ? string.Empty : AssetDatabase.AssetPathToGUID(prefabPath);
        }

        private static void PrepareInstance(GameObject instance, UguiPreviewScene preview)
        {
            Canvas[] canvases = instance.GetComponentsInChildren<Canvas>(true);
            if (canvases.Length == 0)
            {
                instance.transform.SetParent(preview.canvas.transform, false);
            }

            foreach (Canvas canvas in instance.GetComponentsInChildren<Canvas>(true))
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = preview.camera;
                canvas.planeDistance = 10f;
            }

            SetLayerRecursively(instance, 5);
        }

        private static void RebuildLayout(GameObject instance)
        {
            Canvas.ForceUpdateCanvases();
            foreach (RectTransform rect in instance.GetComponentsInChildren<RectTransform>(true))
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            Canvas.ForceUpdateCanvases();
        }

        private static void CaptureReference(UguiPreviewScene preview, FigmaWireframeItem item, string outputDirectory)
        {
            try
            {
                string imagePath = Path.Combine(outputDirectory, item.itemId + ".png");
                Capture(preview, imagePath);
                item.referenceImageId = item.itemId;
            }
            catch (Exception exception)
            {
                item.warnings.Add("Reference image was not captured: " + exception.Message);
            }
        }

        private static void CaptureVisualLayers(
            UguiPreviewScene preview,
            Transform branchRoot,
            IDictionary<Transform, FigmaWireframeNode> transformNodes,
            FigmaWireframeItem item,
            string outputDirectory)
        {
            List<Graphic> graphics = branchRoot.GetComponentsInChildren<Graphic>(false)
                .Where(graphic => graphic != null && graphic.isActiveAndEnabled && transformNodes.ContainsKey(graphic.transform))
                .ToList();
            if (graphics.Count == 0) return;

            var originalGraphicEnabled = graphics.ToDictionary(graphic => graphic, graphic => graphic.enabled);
            Mask[] masks = branchRoot.GetComponentsInChildren<Mask>(true);
            var originalShowMaskGraphic = masks.ToDictionary(mask => mask, mask => mask.showMaskGraphic);

            try
            {
                foreach (Graphic target in graphics)
                {
                    FigmaWireframeNode node = transformNodes[target.transform];
                    try
                    {
                        var requiredMaskGraphics = new HashSet<Graphic>();
                        Transform ancestor = target.transform.parent;
                        while (ancestor != null)
                        {
                            Mask ancestorMask = ancestor.GetComponent<Mask>();
                            if (ancestorMask != null && ancestorMask.isActiveAndEnabled)
                            {
                                Graphic maskGraphic = ancestor.GetComponent<Graphic>();
                                if (maskGraphic != null) requiredMaskGraphics.Add(maskGraphic);
                            }
                            if (ancestor == branchRoot) break;
                            ancestor = ancestor.parent;
                        }

                        foreach (Graphic graphic in graphics)
                            graphic.enabled = graphic == target || requiredMaskGraphics.Contains(graphic);
                        foreach (Mask mask in masks)
                            mask.showMaskGraphic = mask.transform == target.transform && originalShowMaskGraphic[mask];

                        Canvas.ForceUpdateCanvases();
                        RectInt captureRect = GetVisualCaptureRect(target, preview.camera, node.rect);
                        if (captureRect.width <= 0 || captureRect.height <= 0) continue;

                        Texture2D blackCapture = null;
                        Texture2D whiteCapture = null;
                        Texture2D captured = null;
                        Texture2D trimmed = null;
                        try
                        {
                            blackCapture = preview.CaptureTexture(captureRect, Color.black);
                            whiteCapture = preview.CaptureTexture(captureRect, Color.white);
                            captured = ReconstructTransparentTexture(blackCapture, whiteCapture);
                            FigmaWireframeRect visualRect;
                            if (!TryTrimTransparent(captured, captureRect, out trimmed, out visualRect)) continue;

                            string artifactPath = Path.Combine(outputDirectory, node.id + ".png");
                            File.WriteAllBytes(artifactPath, trimmed.EncodeToPNG());
                            node.visualImageId = node.id;
                            node.visualRect = visualRect;
                        }
                        finally
                        {
                            if (blackCapture != null) Object.DestroyImmediate(blackCapture);
                            if (whiteCapture != null) Object.DestroyImmediate(whiteCapture);
                            if (captured != null) Object.DestroyImmediate(captured);
                            if (trimmed != null) Object.DestroyImmediate(trimmed);
                        }
                    }
                    catch (Exception exception)
                    {
                        item.warnings.Add("Visual layer omitted for " + GetReadablePath(target.transform) + ": " + exception.Message);
                    }
                }
            }
            finally
            {
                foreach (KeyValuePair<Graphic, bool> pair in originalGraphicEnabled)
                    if (pair.Key != null) pair.Key.enabled = pair.Value;
                foreach (KeyValuePair<Mask, bool> pair in originalShowMaskGraphic)
                    if (pair.Key != null) pair.Key.showMaskGraphic = pair.Value;
                Canvas.ForceUpdateCanvases();
            }
        }

        private static List<NestedPrefabDescriptor> FindNestedPrefabDescriptors(Transform sourceRoot, string sourceAssetPath)
        {
            if (sourceRoot == null) return new List<NestedPrefabDescriptor>();

            var descriptors = new List<NestedPrefabDescriptor>();
            foreach (Transform candidate in sourceRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!IsActiveBelowRoot(candidate, sourceRoot)
                    || !PrefabUtility.IsAnyPrefabInstanceRoot(candidate.gameObject))
                    continue;

                string nestedAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(candidate.gameObject);
                if (string.IsNullOrEmpty(nestedAssetPath)
                    || string.Equals(nestedAssetPath, sourceAssetPath, StringComparison.Ordinal))
                    continue;

                descriptors.Add(new NestedPrefabDescriptor
                {
                    pathFromRoot = BuildSiblingIndexPath(sourceRoot, candidate),
                    assetPath = nestedAssetPath
                });
            }

            // Deepest instances run first so a prefab nested inside another prefab owns the
            // individual artifacts for its own subtree. The outer root still receives a composite.
            return descriptors
                .OrderByDescending(descriptor => descriptor.pathFromRoot.Count)
                .ThenBy(descriptor => descriptor.assetPath, StringComparer.Ordinal)
                .ToList();
        }

        private static bool IsActiveBelowRoot(Transform candidate, Transform root)
        {
            Transform current = candidate;
            while (current != null)
            {
                if (current == root) return true;
                if (!current.gameObject.activeSelf) return false;
                current = current.parent;
            }
            return false;
        }

        private static void CaptureNestedPrefabAssets(
            UguiPreviewScene preview,
            Transform branchRoot,
            IEnumerable<NestedPrefabDescriptor> descriptors,
            IDictionary<Transform, FigmaWireframeNode> transformNodes,
            FigmaWireframeItem item,
            string outputDirectory)
        {
            List<Graphic> sourceGraphics = branchRoot.GetComponentsInChildren<Graphic>(true)
                .Where(graphic => graphic != null)
                .ToList();
            var sourceGraphicEnabled = sourceGraphics.ToDictionary(graphic => graphic, graphic => graphic.enabled);
            try
            {
                foreach (NestedPrefabDescriptor descriptor in descriptors ?? Enumerable.Empty<NestedPrefabDescriptor>())
                {
                    GameObject isolatedObject = null;
                    try
                    {
                        Transform nestedRoot = FollowSiblingIndexPath(branchRoot, descriptor.pathFromRoot);
                        FigmaWireframeNode rootNode;
                        if (!transformNodes.TryGetValue(nestedRoot, out rootNode)) continue;
                        rootNode.nestedPrefabPath = descriptor.assetPath;
                        if (descriptor.pathFromRoot == null || descriptor.pathFromRoot.Count == 0) continue;

                        isolatedObject = preview.InstantiateSceneObject(nestedRoot.gameObject);
                        if (isolatedObject == null)
                            throw new InvalidOperationException("Could not clone the nested prefab subtree.");
                        foreach (Graphic graphic in sourceGraphics) graphic.enabled = false;

                        isolatedObject.name = nestedRoot.name;
                        PrepareInstance(isolatedObject, preview);
                        if (isolatedObject.transform.parent != preview.canvas.transform)
                            isolatedObject.transform.SetParent(preview.canvas.transform, false);
                        foreach (Canvas rootCanvas in isolatedObject.GetComponents<Canvas>())
                            rootCanvas.enabled = true;
                        CenterAndFitIsolatedUi(isolatedObject, preview);

                        CaptureNestedGraphicAssets(
                            preview,
                            isolatedObject.transform,
                            nestedRoot,
                            transformNodes,
                            descriptor.assetPath,
                            outputDirectory);

                        if (!ShouldIncludeAssetReviewNode(rootNode)) continue;

                        Canvas.ForceUpdateCanvases();
                        Texture2D composite = CaptureTransparentTrimmed(
                            preview,
                            new RectInt(0, 0, CanvasWidth, CanvasHeight));
                        if (composite == null)
                            throw new InvalidOperationException("The isolated nested prefab rendered no visible pixels.");
                        try
                        {
                            string artifactId = BuildAssetArtifactId(rootNode, "composite");
                            File.WriteAllBytes(Path.Combine(outputDirectory, artifactId + ".png"), composite.EncodeToPNG());
                            rootNode.assetImageId = artifactId;
                            rootNode.assetRect = CreateAssetRect(composite);
                        }
                        finally
                        {
                            Object.DestroyImmediate(composite);
                        }
                    }
                    catch (Exception exception)
                    {
                        item.warnings.Add("Nested prefab asset omitted for " + descriptor.assetPath + ": " + exception.Message);
                    }
                    finally
                    {
                        if (isolatedObject != null) Object.DestroyImmediate(isolatedObject);
                        foreach (KeyValuePair<Graphic, bool> pair in sourceGraphicEnabled)
                            if (pair.Key != null) pair.Key.enabled = pair.Value;
                        Canvas.ForceUpdateCanvases();
                    }
                }
            }
            finally
            {
                foreach (KeyValuePair<Graphic, bool> pair in sourceGraphicEnabled)
                    if (pair.Key != null) pair.Key.enabled = pair.Value;
                Canvas.ForceUpdateCanvases();
            }
        }

        private static void CenterAndFitIsolatedUi(GameObject isolatedObject, UguiPreviewScene preview)
        {
            RectTransform rootRect = isolatedObject.transform as RectTransform;
            if (rootRect == null)
                throw new InvalidOperationException("Nested prefab root is not a RectTransform.");

            Vector2 size = rootRect.rect.size;
            if (size.x <= 0f || size.y <= 0f) size = rootRect.sizeDelta;
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = new Vector2(
                (rootRect.pivot.x - 0.5f) * size.x,
                (rootRect.pivot.y - 0.5f) * size.y);
            rootRect.localPosition = new Vector3(rootRect.localPosition.x, rootRect.localPosition.y, 0f);
            RebuildLayout(isolatedObject);

            FigmaWireframeRect bounds = GetActiveGraphicBounds(rootRect, preview.camera);
            if (bounds == null)
                throw new InvalidOperationException("Nested prefab contains no active Graphic to capture.");

            const float margin = 24f;
            float fitScale = Mathf.Min(
                1f,
                Mathf.Min(
                    (CanvasWidth - margin * 2f) / Mathf.Max(1f, bounds.width),
                    (CanvasHeight - margin * 2f) / Mathf.Max(1f, bounds.height)));
            if (fitScale < 1f)
            {
                rootRect.localScale *= fitScale;
                RebuildLayout(isolatedObject);
                bounds = GetActiveGraphicBounds(rootRect, preview.camera);
            }

            if (bounds != null)
            {
                float currentCenterX = bounds.x + bounds.width * 0.5f;
                float currentCenterY = bounds.y + bounds.height * 0.5f;
                rootRect.anchoredPosition += new Vector2(
                    CanvasWidth * 0.5f - currentCenterX,
                    currentCenterY - CanvasHeight * 0.5f);
                RebuildLayout(isolatedObject);
            }
        }

        private static FigmaWireframeRect GetActiveGraphicBounds(Transform root, Camera camera)
        {
            List<FigmaWireframeRect> rects = root.GetComponentsInChildren<Graphic>(false)
                .Where(graphic => graphic != null && graphic.isActiveAndEnabled)
                .Select(graphic => GetFigmaRect(graphic.rectTransform, camera))
                .Where(rect => rect != null && rect.width > 0f && rect.height > 0f)
                .ToList();
            if (rects.Count == 0) return null;

            float left = rects.Min(rect => rect.x);
            float top = rects.Min(rect => rect.y);
            float right = rects.Max(rect => rect.x + rect.width);
            float bottom = rects.Max(rect => rect.y + rect.height);
            return new FigmaWireframeRect
            {
                x = left,
                y = top,
                width = Mathf.Max(0f, right - left),
                height = Mathf.Max(0f, bottom - top)
            };
        }

        private static void CaptureNestedGraphicAssets(
            UguiPreviewScene preview,
            Transform isolatedRoot,
            Transform sourceNestedRoot,
            IDictionary<Transform, FigmaWireframeNode> transformNodes,
            string nestedAssetPath,
            string outputDirectory)
        {
            List<Graphic> graphics = isolatedRoot.GetComponentsInChildren<Graphic>(false)
                .Where(graphic => graphic != null && graphic.isActiveAndEnabled)
                .ToList();
            var originalGraphicEnabled = graphics.ToDictionary(graphic => graphic, graphic => graphic.enabled);
            Mask[] masks = isolatedRoot.GetComponentsInChildren<Mask>(true);
            var originalShowMaskGraphic = masks.ToDictionary(mask => mask, mask => mask.showMaskGraphic);

            try
            {
                foreach (Graphic target in graphics)
                {
                    if (target.transform == isolatedRoot) continue;

                    Transform sourceTransform = FollowSiblingIndexPath(
                        sourceNestedRoot,
                        BuildSiblingIndexPath(isolatedRoot, target.transform));
                    FigmaWireframeNode node;
                    if (!transformNodes.TryGetValue(sourceTransform, out node)
                        || !ShouldIncludeAssetReviewNode(node)
                        || !string.IsNullOrEmpty(node.assetImageId))
                        continue;

                    var requiredMaskGraphics = new HashSet<Graphic>();
                    Transform ancestor = target.transform.parent;
                    while (ancestor != null)
                    {
                        Mask ancestorMask = ancestor.GetComponent<Mask>();
                        if (ancestorMask != null && ancestorMask.isActiveAndEnabled)
                        {
                            Graphic maskGraphic = ancestor.GetComponent<Graphic>();
                            if (maskGraphic != null) requiredMaskGraphics.Add(maskGraphic);
                        }
                        if (ancestor == isolatedRoot) break;
                        ancestor = ancestor.parent;
                    }

                    foreach (Graphic graphic in graphics)
                        graphic.enabled = graphic == target || requiredMaskGraphics.Contains(graphic);
                    foreach (Mask mask in masks)
                        mask.showMaskGraphic = mask.transform == target.transform && originalShowMaskGraphic[mask];
                    Canvas.ForceUpdateCanvases();

                    FigmaWireframeRect fallback = GetFigmaRect(target.rectTransform, preview.camera);
                    RectInt captureRect = GetVisualCaptureRect(target, preview.camera, fallback);
                    if (captureRect.width <= 0 || captureRect.height <= 0) continue;

                    Texture2D texture = CaptureTransparentTrimmed(preview, captureRect);
                    if (texture == null) continue;
                    try
                    {
                        string artifactId = BuildAssetArtifactId(node, "graphic");
                        File.WriteAllBytes(Path.Combine(outputDirectory, artifactId + ".png"), texture.EncodeToPNG());
                        node.assetImageId = artifactId;
                        node.assetRect = CreateAssetRect(texture);
                        node.nestedPrefabPath = nestedAssetPath;
                    }
                    finally
                    {
                        Object.DestroyImmediate(texture);
                    }
                }
            }
            finally
            {
                foreach (KeyValuePair<Graphic, bool> pair in originalGraphicEnabled)
                    if (pair.Key != null) pair.Key.enabled = pair.Value;
                foreach (KeyValuePair<Mask, bool> pair in originalShowMaskGraphic)
                    if (pair.Key != null) pair.Key.showMaskGraphic = pair.Value;
                Canvas.ForceUpdateCanvases();
            }
        }

        internal static bool ShouldIncludeAssetReviewNode(FigmaWireframeNode node)
        {
            return node != null && node.includeInAssetReview;
        }

        private static void MarkAssetReviewNodes(IList<FigmaWireframeNode> nodes)
        {
            if (nodes == null) return;

            var rootIds = new HashSet<string>(nodes
                .Where(node => node != null && string.IsNullOrEmpty(node.parentId))
                .Select(node => node.id), StringComparer.Ordinal);
            foreach (FigmaWireframeNode node in nodes.Where(node => node != null))
            {
                node.includeInAssetReview = !rootIds.Contains(node.id)
                                            && !(rootIds.Contains(node.parentId) && IsFullCanvasBackground(node));
            }
        }

        private static bool IsFullCanvasBackground(FigmaWireframeNode node)
        {
            if (node == null
                || node.rect == null
                || (!string.Equals(node.role, "image", StringComparison.Ordinal)
                    && !string.Equals(node.role, "raw-image", StringComparison.Ordinal)))
                return false;

            const float tolerance = 1f;
            return node.rect.x <= tolerance
                   && node.rect.y <= tolerance
                   && node.rect.x + node.rect.width >= CanvasWidth - tolerance
                   && node.rect.y + node.rect.height >= CanvasHeight - tolerance;
        }

        private static Texture2D CaptureTransparentTrimmed(UguiPreviewScene preview, RectInt captureRect)
        {
            Texture2D blackCapture = null;
            Texture2D whiteCapture = null;
            Texture2D reconstructed = null;
            Texture2D trimmed = null;
            try
            {
                blackCapture = preview.CaptureTexture(captureRect, Color.black);
                whiteCapture = preview.CaptureTexture(captureRect, Color.white);
                reconstructed = ReconstructTransparentTexture(blackCapture, whiteCapture);
                FigmaWireframeRect ignored;
                return TryTrimTransparent(reconstructed, captureRect, out trimmed, out ignored) ? trimmed : null;
            }
            finally
            {
                if (blackCapture != null) Object.DestroyImmediate(blackCapture);
                if (whiteCapture != null) Object.DestroyImmediate(whiteCapture);
                if (reconstructed != null) Object.DestroyImmediate(reconstructed);
            }
        }

        private static string BuildAssetArtifactId(FigmaWireframeNode node, string kind)
        {
            return "asset-" + Hash128.Compute(node.id + "|" + kind).ToString();
        }

        private static FigmaWireframeRect CreateAssetRect(Texture2D texture)
        {
            return new FigmaWireframeRect
            {
                x = 0f,
                y = 0f,
                width = texture.width,
                height = texture.height
            };
        }

        private static RectInt GetVisualCaptureRect(Graphic graphic, Camera camera, FigmaWireframeRect fallback)
        {
            FigmaWireframeRect visualBounds = fallback;
            Mesh mesh = graphic.canvasRenderer.GetMesh();
            if (mesh != null && mesh.vertexCount > 0)
            {
                Bounds bounds = mesh.bounds;
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;
                visualBounds = GetFigmaRectFromWorldPoints(new[]
                {
                    graphic.transform.TransformPoint(new Vector3(min.x, min.y, min.z)),
                    graphic.transform.TransformPoint(new Vector3(min.x, max.y, min.z)),
                    graphic.transform.TransformPoint(new Vector3(max.x, max.y, max.z)),
                    graphic.transform.TransformPoint(new Vector3(max.x, min.y, max.z))
                }, camera);
            }

            const int antialiasPadding = 4;
            int left = Mathf.Clamp(Mathf.FloorToInt(visualBounds.x) - antialiasPadding, 0, CanvasWidth);
            int top = Mathf.Clamp(Mathf.FloorToInt(visualBounds.y) - antialiasPadding, 0, CanvasHeight);
            int right = Mathf.Clamp(Mathf.CeilToInt(visualBounds.x + visualBounds.width) + antialiasPadding, 0, CanvasWidth);
            int bottom = Mathf.Clamp(Mathf.CeilToInt(visualBounds.y + visualBounds.height) + antialiasPadding, 0, CanvasHeight);
            return new RectInt(left, CanvasHeight - bottom, Mathf.Max(0, right - left), Mathf.Max(0, bottom - top));
        }

        private static FigmaWireframeRect GetFigmaRectFromWorldPoints(IEnumerable<Vector3> worldPoints, Camera camera)
        {
            return GetFigmaRectFromViewport(worldPoints.Select(point =>
            {
                Vector3 viewport = camera.WorldToViewportPoint(point);
                return new Vector2(viewport.x, viewport.y);
            }));
        }

        private static bool TryTrimTransparent(
            Texture2D source,
            RectInt captureRect,
            out Texture2D trimmed,
            out FigmaWireframeRect visualRect)
        {
            trimmed = null;
            visualRect = null;
            Color32[] pixels = source.GetPixels32();
            int minX = source.width;
            int minY = source.height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < source.height; y++)
            {
                int row = y * source.width;
                for (int x = 0; x < source.width; x++)
                {
                    if (pixels[row + x].a == 0) continue;
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }
            if (maxX < minX || maxY < minY) return false;

            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            var croppedPixels = new Color32[width * height];
            for (int row = 0; row < height; row++)
                Array.Copy(pixels, (minY + row) * source.width + minX, croppedPixels, row * width, width);

            trimmed = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            trimmed.SetPixels32(croppedPixels);
            trimmed.Apply(false, false);
            visualRect = new FigmaWireframeRect
            {
                x = captureRect.x + minX,
                y = CanvasHeight - (captureRect.y + maxY + 1),
                width = width,
                height = height
            };
            return true;
        }

        private static Texture2D ReconstructTransparentTexture(Texture2D blackCapture, Texture2D whiteCapture)
        {
            if (blackCapture.width != whiteCapture.width || blackCapture.height != whiteCapture.height)
                throw new InvalidOperationException("Graphic matte captures have different dimensions.");

            Color32[] black = blackCapture.GetPixels32();
            Color32[] white = whiteCapture.GetPixels32();
            var transparent = new Color32[black.Length];
            for (int index = 0; index < black.Length; index++)
            {
                int backgroundContribution = Mathf.Clamp(Mathf.RoundToInt(((white[index].r - black[index].r)
                                                                           + (white[index].g - black[index].g)
                                                                           + (white[index].b - black[index].b)) / 3f), 0, 255);
                int alpha = 255 - backgroundContribution;
                if (alpha <= 1)
                {
                    transparent[index] = new Color32(0, 0, 0, 0);
                    continue;
                }

                transparent[index] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(black[index].r * 255f / alpha), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(black[index].g * 255f / alpha), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(black[index].b * 255f / alpha), 0, 255),
                    (byte)alpha);
            }

            var texture = new Texture2D(blackCapture.width, blackCapture.height, TextureFormat.RGBA32, false, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(transparent);
            texture.Apply(false, false);
            return texture;
        }

        private static Canvas FindOutermostCanvas(Transform transform)
        {
            Canvas outermost = null;
            Transform current = transform;
            while (current != null)
            {
                Canvas canvas = current.GetComponent<Canvas>();
                if (canvas != null) outermost = canvas;
                current = current.parent;
            }
            return outermost;
        }

        private static List<int> BuildSiblingIndexPath(Transform ancestor, Transform target)
        {
            var indices = new Stack<int>();
            Transform current = target;
            while (current != ancestor)
            {
                if (current == null)
                    throw new InvalidOperationException("The selected hierarchy is not below the resolved Canvas.");
                indices.Push(current.GetSiblingIndex());
                current = current.parent;
            }
            return indices.ToList();
        }

        private static Transform FollowSiblingIndexPath(Transform root, IEnumerable<int> indices)
        {
            Transform current = root;
            foreach (int index in indices)
            {
                if (index < 0 || index >= current.childCount)
                    throw new InvalidOperationException("The cloned scene hierarchy no longer matches the source hierarchy.");
                current = current.GetChild(index);
            }
            return current;
        }

        private static void ActivatePreviewBranch(Transform canvasRoot, Transform branchRoot)
        {
            Transform current = branchRoot;
            while (current != null)
            {
                current.gameObject.SetActive(true);
                Vector3 scale = current.localScale;
                if (Mathf.Approximately(scale.x, 0f) || Mathf.Approximately(scale.y, 0f))
                {
                    if (Mathf.Approximately(scale.x, 0f)) scale.x = 1f;
                    if (Mathf.Approximately(scale.y, 0f)) scale.y = 1f;
                    if (Mathf.Approximately(scale.z, 0f)) scale.z = 1f;
                    current.localScale = scale;

                    RectTransform rectTransform = current as RectTransform;
                    if (rectTransform != null
                        && Mathf.Approximately(rectTransform.rect.width, 0f)
                        && Mathf.Approximately(rectTransform.rect.height, 0f))
                    {
                        rectTransform.anchorMin = Vector2.zero;
                        rectTransform.anchorMax = Vector2.one;
                        rectTransform.offsetMin = Vector2.zero;
                        rectTransform.offsetMax = Vector2.zero;
                        rectTransform.anchoredPosition = Vector2.zero;
                    }
                }
                if (current == canvasRoot) return;
                current = current.parent;
            }

            throw new InvalidOperationException("The selected hierarchy is not below the cloned Canvas.");
        }

        private static void DisableRenderingOutsideBranch(GameObject canvasInstance, Transform branchRoot)
        {
            foreach (Graphic graphic in canvasInstance.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.transform != branchRoot && !graphic.transform.IsChildOf(branchRoot))
                    graphic.enabled = false;
            }

            foreach (Canvas canvas in canvasInstance.GetComponentsInChildren<Canvas>(true))
            {
                bool containsBranch = branchRoot == canvas.transform || branchRoot.IsChildOf(canvas.transform);
                bool belongsToBranch = canvas.transform.IsChildOf(branchRoot);
                if (containsBranch)
                    canvas.enabled = true;
                else if (!belongsToBranch)
                    canvas.enabled = false;
            }
        }

        private static void Capture(UguiPreviewScene preview, string outputPath)
        {
            Texture2D texture = null;
            try
            {
                preview.Render();
                texture = preview.EndStaticPreview();
                if (texture == null) throw new InvalidOperationException("Preview renderer did not return a texture.");
                if (!HasVisiblePixels(texture)) throw new InvalidOperationException("Preview renderer returned a blank image.");
                File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            }
            finally
            {
                if (texture != null) Object.DestroyImmediate(texture);
            }
        }

        private static bool HasVisiblePixels(Texture2D texture)
        {
            foreach (Color32 pixel in texture.GetPixels32())
            {
                if (pixel.r != 0 || pixel.g != 0 || pixel.b != 0) return true;
            }
            return false;
        }

        private static void DescribeTransform(
            Transform transform,
            string sourceGuid,
            string transformPath,
            string parentId,
            Camera camera,
            List<FigmaWireframeNode> nodes,
            List<string> warnings,
            IDictionary<Transform, FigmaWireframeNode> transformNodes)
        {
            if (!transform.gameObject.activeSelf)
            {
                warnings.Add("Inactive node omitted: " + GetReadablePath(transform));
                return;
            }

            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform == null)
            {
                warnings.Add("Non-UGUI transform omitted: " + GetReadablePath(transform));
                return;
            }

            var node = new FigmaWireframeNode
            {
                id = BuildStableNodeId(sourceGuid, transformPath),
                parentId = parentId,
                name = transform.name,
                role = ResolveRole(transform.gameObject),
                siblingIndex = transform.GetSiblingIndex(),
                renderOrder = GetRenderOrder(transform.gameObject, nodes.Count),
                rect = GetFigmaRect(rectTransform, camera),
                color = GetColor(transform.gameObject),
                text = GetText(transform.gameObject),
                clipsContent = transform.GetComponent<Mask>() != null || transform.GetComponent<RectMask2D>() != null,
                opacity = GetCanvasGroupOpacity(transform),
                textStyle = GetTextStyle(transform.gameObject)
            };
            nodes.Add(node);
            transformNodes[transform] = node;

            for (int index = 0; index < transform.childCount; index++)
            {
                Transform child = transform.GetChild(index);
                DescribeTransform(child, sourceGuid, transformPath + "/" + index, node.id, camera, nodes, warnings, transformNodes);
            }
        }

        private static FigmaWireframeRect GetFigmaRect(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var viewportCorners = new Vector2[corners.Length];
            for (int index = 0; index < corners.Length; index++)
            {
                Vector3 viewport = camera.WorldToViewportPoint(corners[index]);
                viewportCorners[index] = new Vector2(viewport.x, viewport.y);
            }
            return GetFigmaRectFromViewport(viewportCorners);
        }

        public static FigmaWireframeRect GetFigmaRectFromViewport(IEnumerable<Vector2> viewportCorners)
        {
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;

            foreach (Vector2 viewport in viewportCorners)
            {
                float figmaX = viewport.x * CanvasWidth;
                float figmaY = (1f - viewport.y) * CanvasHeight;
                minX = Mathf.Min(minX, figmaX);
                maxX = Mathf.Max(maxX, figmaX);
                minY = Mathf.Min(minY, figmaY);
                maxY = Mathf.Max(maxY, figmaY);
            }

            return new FigmaWireframeRect
            {
                x = Round(minX),
                y = Round(minY),
                width = Round(Mathf.Max(0f, maxX - minX)),
                height = Round(Mathf.Max(0f, maxY - minY))
            };
        }

        private static string ResolveRole(GameObject gameObject)
        {
            if (gameObject.GetComponent<Button>() != null) return "button";
            if (gameObject.GetComponent<Toggle>() != null) return "toggle";
            if (gameObject.GetComponent<Slider>() != null) return "slider";
            if (gameObject.GetComponent<ScrollRect>() != null) return "scroll";
            if (gameObject.GetComponent<TextMeshProUGUI>() != null || gameObject.GetComponent<Text>() != null) return "text";
            if (gameObject.GetComponent<RawImage>() != null) return "raw-image";
            if (gameObject.GetComponent<Image>() != null) return "image";
            return "container";
        }

        private static string GetText(GameObject gameObject)
        {
            TextMeshProUGUI tmpText = gameObject.GetComponent<TextMeshProUGUI>();
            if (tmpText != null) return tmpText.text;
            Text legacyText = gameObject.GetComponent<Text>();
            return legacyText != null ? legacyText.text : null;
        }

        private static FigmaWireframeTextStyle GetTextStyle(GameObject gameObject)
        {
            TextMeshProUGUI tmpText = gameObject.GetComponent<TextMeshProUGUI>();
            if (tmpText != null)
            {
                string family = null;
                string style = tmpText.fontStyle.ToString();
                float lineHeight = tmpText.fontSize * 1.2f;
                if (tmpText.font != null)
                {
                    var face = tmpText.font.faceInfo;
                    family = string.IsNullOrEmpty(face.familyName) ? tmpText.font.name : face.familyName;
                    if (!string.IsNullOrEmpty(face.styleName) && string.Equals(style, "Normal", StringComparison.OrdinalIgnoreCase))
                        style = face.styleName;
                    if (face.pointSize > 0f && face.lineHeight > 0f)
                        lineHeight = tmpText.fontSize * face.lineHeight / face.pointSize;
                }

                return new FigmaWireframeTextStyle
                {
                    fontFamily = family ?? "Inter",
                    fontStyle = style,
                    fontSize = Round(tmpText.fontSize),
                    lineHeight = Round(Mathf.Max(1f, lineHeight)),
                    letterSpacing = Round(tmpText.characterSpacing),
                    horizontalAlignment = NormalizeHorizontalAlignment(tmpText.horizontalAlignment.ToString()),
                    verticalAlignment = NormalizeVerticalAlignment(tmpText.verticalAlignment.ToString())
                };
            }

            Text legacyText = gameObject.GetComponent<Text>();
            if (legacyText == null) return null;
            return new FigmaWireframeTextStyle
            {
                fontFamily = legacyText.font != null ? legacyText.font.name : "Arial",
                fontStyle = legacyText.fontStyle.ToString(),
                fontSize = legacyText.fontSize,
                lineHeight = Round(Mathf.Max(1f, legacyText.fontSize * legacyText.lineSpacing)),
                letterSpacing = 0f,
                horizontalAlignment = NormalizeHorizontalAlignment(legacyText.alignment.ToString()),
                verticalAlignment = NormalizeVerticalAlignment(legacyText.alignment.ToString())
            };
        }

        private static int GetRenderOrder(GameObject gameObject, int fallback)
        {
            Graphic graphic = gameObject.GetComponent<Graphic>();
            return graphic != null && graphic.canvasRenderer != null && graphic.canvasRenderer.absoluteDepth >= 0
                ? graphic.canvasRenderer.absoluteDepth
                : fallback;
        }

        private static string NormalizeHorizontalAlignment(string alignment)
        {
            if (alignment.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0) return "right";
            if (alignment.IndexOf("Center", StringComparison.OrdinalIgnoreCase) >= 0
                || alignment.IndexOf("Midline", StringComparison.OrdinalIgnoreCase) >= 0) return "center";
            if (alignment.IndexOf("Justified", StringComparison.OrdinalIgnoreCase) >= 0
                || alignment.IndexOf("Flush", StringComparison.OrdinalIgnoreCase) >= 0) return "justified";
            return "left";
        }

        private static string NormalizeVerticalAlignment(string alignment)
        {
            if (alignment.IndexOf("Bottom", StringComparison.OrdinalIgnoreCase) >= 0) return "bottom";
            if (alignment.IndexOf("Middle", StringComparison.OrdinalIgnoreCase) >= 0
                || alignment.IndexOf("Midline", StringComparison.OrdinalIgnoreCase) >= 0
                || alignment.IndexOf("Center", StringComparison.OrdinalIgnoreCase) >= 0) return "center";
            return "top";
        }

        private static float GetCanvasGroupOpacity(Transform transform)
        {
            float opacity = 1f;
            Transform current = transform;
            while (current != null)
            {
                bool ignoreParents = false;
                foreach (CanvasGroup group in current.GetComponents<CanvasGroup>())
                {
                    if (!group.isActiveAndEnabled) continue;
                    opacity *= group.alpha;
                    ignoreParents |= group.ignoreParentGroups;
                }
                if (ignoreParents) break;
                current = current.parent;
            }
            return Round(Mathf.Clamp01(opacity));
        }

        private static FigmaWireframeColor GetColor(GameObject gameObject)
        {
            Graphic graphic = gameObject.GetComponent<Graphic>();
            Color color = graphic != null ? graphic.color : Color.white;
            return new FigmaWireframeColor { r = color.r, g = color.g, b = color.b, a = color.a };
        }

        private static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private static string GetReadablePath(Transform transform)
        {
            var names = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", names);
        }

        private static float Round(float value)
        {
            return (float)Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        private sealed class UguiPreviewScene : IDisposable
        {
            private readonly PreviewRenderUtility preview;
            public readonly Camera camera;
            public readonly Canvas canvas;

            public UguiPreviewScene(int previewWidth = CanvasWidth, int previewHeight = CanvasHeight)
            {
                preview = new PreviewRenderUtility();
                camera = preview.camera;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.cullingMask = 1 << 5;
                camera.orthographic = true;
                camera.aspect = previewWidth / (float)previewHeight;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.cameraType = CameraType.Game;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 1000f;
                preview.BeginStaticPreview(new Rect(0f, 0f, previewWidth, previewHeight));

                var canvasObject = new GameObject("PicturePuzzle Figma Preview Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.layer = 5;
                preview.AddSingleGO(canvasObject);
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                canvasObject.layer = 5;

                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(CanvasWidth, CanvasHeight);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            public GameObject InstantiatePrefab(GameObject prefab)
            {
                return preview.InstantiatePrefabInScene(prefab);
            }

            public GameObject InstantiateSceneObject(GameObject source)
            {
                GameObject instance = Object.Instantiate(source);
                instance.name = source.name;
                preview.AddSingleGO(instance);
                return instance;
            }

            public void Render()
            {
                preview.Render(true, true);
            }

            public Texture2D EndStaticPreview()
            {
                return preview.EndStaticPreview();
            }

            public Texture2D CaptureTexture(RectInt pixelRect, Color clearColor)
            {
                if (pixelRect.width <= 0 || pixelRect.height <= 0)
                    throw new ArgumentOutOfRangeException(nameof(pixelRect), "Capture bounds must be non-empty.");

                RenderTexture previousActive = RenderTexture.active;
                RenderTexture previousTarget = camera.targetTexture;
                CameraClearFlags previousClearFlags = camera.clearFlags;
                Color previousBackgroundColor = camera.backgroundColor;
                RenderTexture target = RenderTexture.GetTemporary(
                    CanvasWidth,
                    CanvasHeight,
                    24,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
                try
                {
                    target.name = "PicturePuzzle Figma Graphic Capture";
                    camera.targetTexture = target;
                    RenderTexture.active = target;
                    GL.Clear(true, true, clearColor);
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = clearColor;
                    camera.Render();

                    var texture = new Texture2D(pixelRect.width, pixelRect.height, TextureFormat.RGBA32, false, false)
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    texture.ReadPixels(new Rect(pixelRect.x, pixelRect.y, pixelRect.width, pixelRect.height), 0, 0, false);
                    texture.Apply(false, false);
                    return texture;
                }
                finally
                {
                    camera.targetTexture = previousTarget;
                    camera.clearFlags = previousClearFlags;
                    camera.backgroundColor = previousBackgroundColor;
                    RenderTexture.active = previousActive;
                    RenderTexture.ReleaseTemporary(target);
                }
            }

            public void Dispose()
            {
                preview.Cleanup();
            }
        }
    }

    public sealed class PicturePuzzleFigmaWireframeExporterWindow : EditorWindow
    {
        private const string BridgeUrl = PicturePuzzleFigmaEmbeddedBridge.BaseUrl;
        private const string PluginManifestRelativePath = "tools/design/figma-bridge/manifest.json";
        private const string LastFigmaFileKeyPreference = "PicturePuzzle.Figma.LastFileKey";
        private const int MaximumReviewedSceneSources = 12;
        private const int AssetPreviewCardsPerPage = 15;
        private const int LocalPreviewImageCacheLimit = 18;

        private enum LocalPreviewStage
        {
            Assets,
            Wireframe,
            UiResult
        }

        [Serializable]
        private sealed class BridgePluginInfo
        {
            public string id;
            public string fileName;
            public string pageName;
            public string fileKey;
        }

        [Serializable]
        private sealed class BridgeHealthInfo
        {
            public bool ok;
            public int queue;
            public int inFlight;
            public int results;
            public bool pluginConnected;
            public BridgePluginInfo[] plugins;
        }

        [Serializable]
        private sealed class BridgeCommandInfo
        {
            public string id;
        }

        [Serializable]
        private sealed class BridgeEnqueueInfo
        {
            public bool ok;
            public BridgeCommandInfo command;
            public int queue;
        }

        [Serializable]
        private sealed class BridgeEnqueueRequest
        {
            public string type;
            public string name;
            public string targetPluginId;
            public FigmaWireframeBatch batch;
        }

        [Serializable]
        private sealed class BridgeRenderResultInfo
        {
            public string pageName;
            public int exported;
        }

        [Serializable]
        private sealed class BridgeResultInfo
        {
            public string id;
            public bool ok;
            public string error;
            public BridgeRenderResultInfo result;
        }

        [Serializable]
        private sealed class BridgeResultsInfo
        {
            public BridgeResultInfo[] results;
        }

        [SerializeField] private Object pendingProjectSource;
        [SerializeField] private List<string> projectPrefabPaths = new List<string>();
        [SerializeField] private List<string> selectedPrefabPaths = new List<string>();
        [SerializeField] private List<GameObject> selectedSceneRoots = new List<GameObject>();
        [SerializeField] private GameObject previewSceneRoot;
        [SerializeField] private string previewPrefabPath;
        [SerializeField] private string bridgeStatus = "Starting the Unity bridge automatically...";
        [SerializeField] private string exportStatus = "Select one or more prefab or scene UI sources.";
        [SerializeField] private LocalPreviewStage localPreviewStage = LocalPreviewStage.Assets;
        [SerializeField] private int localPreviewItemIndex;
        [SerializeField] private int localAssetPreviewPage;
        private bool showAdvanced;
        private readonly List<string> skippedSourcePaths = new List<string>();
        private readonly HashSet<int> expandedSceneNodes = new HashSet<int>();
        private readonly HashSet<int> initializedSceneRoots = new HashSet<int>();
        private readonly Dictionary<string, Texture2D> prefabPreviewCache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly HashSet<string> queuedPrefabPreviews = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> failedPrefabPreviews = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> prefabPreviewQueue = new Queue<string>();
        private readonly Dictionary<int, Texture2D> scenePreviewCache = new Dictionary<int, Texture2D>();
        private readonly HashSet<int> queuedScenePreviews = new HashSet<int>();
        private readonly HashSet<int> failedScenePreviews = new HashSet<int>();
        private readonly Queue<GameObject> scenePreviewQueue = new Queue<GameObject>();
        private readonly Dictionary<string, Texture2D> localPreviewImageCache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Queue<string> localPreviewImageCacheOrder = new Queue<string>();
        private readonly Dictionary<string, Texture2D> localResultPreviewCache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly HashSet<string> submittedBatchIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> discardedPreviewBatchIds = new HashSet<string>(StringComparer.Ordinal);
        private Vector2 sceneColumnScroll;
        private Vector2 actionColumnScroll;
        private Vector2 windowScroll;
        private Vector2 sceneTreeScroll;
        private Vector2 prefabListScroll;
        private Vector2 selectedPreviewScroll;
        private Vector2 localAssetPreviewScroll;
        private string sceneFilter = string.Empty;
        private string lastManifestPath;
        private string lastQueueUrl;
        private FigmaWireframeExportResult localPreviewExport;
        private PicturePuzzleFigmaWireframeExporter.SceneExportSession localPreviewBuildSession;
        private bool previewGenerationScheduled;
        private bool scenePreviewGenerationScheduled;
        private BridgeHealthInfo bridgeHealth;
        private Task bridgeRefreshTask;
        private bool exportInProgress;
        private bool sendWhenPluginConnects;
        private bool localPreviewRebuildPending;
        private bool automaticFigmaOpenAttempted;
        private bool windowActive;
        private double nextBridgeRefreshAt;
        private int selectedPluginIndex;
        [SerializeField] private string selectedPluginId;
        [SerializeField] private string selectedPluginLabel;

        [MenuItem("Tools/Picture Puzzle/Figma/Export Wireframes")]
        public static void Open()
        {
            var window = GetWindow<PicturePuzzleFigmaWireframeExporterWindow>("Unity UI to Figma");
            window.minSize = new Vector2(780f, 680f);
            window.EnsureComfortableSize();
            window.BeginAutomaticFigmaConnection();
        }

        [MenuItem("Tools/Picture Puzzle/Figma/Preview Current Scene UI for Figma", false, 1)]
        public static void PreviewCurrentSelectionForFigma()
        {
            var window = GetWindow<PicturePuzzleFigmaWireframeExporterWindow>("Unity UI to Figma");
            window.minSize = new Vector2(780f, 680f);
            window.EnsureComfortableSize();
            window.ReplaceWithCurrentUnitySelection();
            window.ScheduleCurrentSelectionPreview();
            window.BeginAutomaticFigmaConnection();
        }

        [MenuItem("Tools/Picture Puzzle/Figma/Preview Current Scene UI for Figma", true)]
        private static bool CanPreviewCurrentSelectionForFigma()
        {
            return Selection.gameObjects.Any(PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy);
        }

        [MenuItem("GameObject/Picture Puzzle/Preview UI Hierarchy for Figma", false, 49)]
        public static void OpenForSelectedHierarchy()
        {
            PreviewCurrentSelectionForFigma();
        }

        private void OnEnable()
        {
            windowActive = true;
            bridgeRefreshTask = null;
            exportInProgress = false;
            localPreviewRebuildPending = false;
            automaticFigmaOpenAttempted = false;
            projectPrefabPaths.Clear();
            selectedPrefabPaths.Clear();
            previewPrefabPath = null;
            if (localPreviewExport == null
                && exportStatus.StartsWith("Local preview ready", StringComparison.Ordinal))
                exportStatus = "Local preview needs to be rebuilt after the editor reloaded.";
            PicturePuzzleFigmaEmbeddedBridge.EnsureStarted();
            EditorApplication.update -= RefreshBridgeWhenNeeded;
            EditorApplication.update += RefreshBridgeWhenNeeded;
            EditorSceneManager.sceneDirtied -= OnSceneDirtied;
            EditorSceneManager.sceneDirtied += OnSceneDirtied;
            nextBridgeRefreshAt = 0d;
            CheckBridgeAsync();
        }

        [MenuItem("GameObject/Picture Puzzle/Preview UI Hierarchy for Figma", true)]
        private static bool CanOpenForSelectedHierarchy()
        {
            return PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy(Selection.activeGameObject);
        }

        private void OnGUI()
        {
            PruneSelections();
            windowScroll = EditorGUILayout.BeginScrollView(windowScroll);
            DrawHeader();

            using (new EditorGUILayout.HorizontalScope())
            {
                float sceneColumnWidth = Mathf.Max(480f, (position.width - 20f) * 0.66f);
                sceneColumnScroll = EditorGUILayout.BeginScrollView(sceneColumnScroll, GUILayout.Width(sceneColumnWidth));
                DrawSceneSourcePanel();
                EditorGUILayout.EndScrollView();

                GUILayout.Space(6f);
                actionColumnScroll = EditorGUILayout.BeginScrollView(actionColumnScroll);
                DrawBridgePanel();
                DrawAdvancedPanel();
                EditorGUILayout.EndScrollView();
            }
            DrawLocalExportPreviewPanel();
            DrawExportPanel();
            EditorGUILayout.EndScrollView();
        }

        private void OnDisable()
        {
            windowActive = false;
            sendWhenPluginConnects = false;
            localPreviewRebuildPending = false;
            EditorApplication.update -= RefreshBridgeWhenNeeded;
            CancelLocalPreviewBuild();
            EditorApplication.delayCall -= DeleteDiscardedPreviewArtifacts;
            EditorSceneManager.sceneDirtied -= OnSceneDirtied;
            EditorApplication.delayCall -= ExportSelectedAsync;
            EditorApplication.delayCall -= BuildLocalPreview;
            EditorApplication.delayCall -= GenerateNextPrefabPreview;
            EditorApplication.delayCall -= GenerateNextScenePreview;
            ClearPrefabPreviewCache();
            ClearScenePreviewCache();
            ClearLocalPreviewCache();
            DeleteDiscardedPreviewArtifacts();
        }

        private void OnProjectChange()
        {
            CancelLocalPreviewBuild();
            EditorApplication.delayCall -= DeleteDiscardedPreviewArtifacts;
            ClearPrefabPreviewCache();
            ClearScenePreviewCache();
            ClearLocalPreviewCache();
            DeleteDiscardedPreviewArtifacts();
            PruneSelections();
            Repaint();
        }

        private void EnsureComfortableSize()
        {
            Rect current = position;
            current.width = Mathf.Max(current.width, 780f);
            current.height = Mathf.Max(current.height, 680f);
            position = current;
        }

        private void DrawHeader()
        {
            int total = selectedSceneRoots.Count;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Unity UI  →  Figma", EditorStyles.largeLabel);
                EditorGUILayout.LabelField("Tick Scene UI trong cây Hierarchy, review 3 trang, rồi gửi sang Figma.", EditorStyles.miniLabel);
                EditorGUILayout.Space(2f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Selected: " + total, EditorStyles.miniBoldLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Assets  •  Wireframes  •  UI Results", EditorStyles.miniBoldLabel);
                }
            }
        }

        private void OnHierarchyChange()
        {
            ClearScenePreviewCache();
            if (PruneSelections(false)) ScheduleCurrentSelectionPreview();
            Repaint();
        }

        private void OnSceneDirtied(Scene scene)
        {
            if (!selectedSceneRoots.Any(root => root != null && root.scene == scene)) return;

            sendWhenPluginConnects = false;
            EditorApplication.delayCall -= ExportSelectedAsync;
            if (localPreviewBuildSession != null)
            {
                CancelLocalPreviewBuild();
                localPreviewRebuildPending = true;
                exportStatus = "A checked Scene UI source changed while the preview was building. Build it again.";
                Repaint();
                return;
            }

            if (localPreviewExport == null) return;
            ClearLocalPreviewCache();
            localPreviewRebuildPending = true;
            exportStatus = "A checked Scene UI source changed. Build the local preview again.";
            Repaint();
        }

        private void OnSelectionChange()
        {
            if (selectedSceneRoots.Contains(Selection.activeGameObject))
                FocusScenePreview(Selection.activeGameObject);
            Repaint();
        }

        private void AddCurrentUnitySelection()
        {
            GameObject[] selectedSceneUi = Selection.gameObjects
                .Where(candidate => candidate != null
                                    && candidate.scene == SceneManager.GetActiveScene()
                                    && PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy(candidate))
                .ToArray();
            if (selectedSceneUi.Length > 0)
            {
                AddSceneSources(selectedSceneUi);
                GameObject previewCandidate = PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy(Selection.activeGameObject)
                    ? Selection.activeGameObject
                    : selectedSceneUi[0];
                FocusScenePreview(previewCandidate);
            }

        }

        private void ReplaceWithCurrentUnitySelection()
        {
            ClearSceneSelection();
            AddSceneSources(Selection.gameObjects);
            PruneSelections();
        }

        private void ScheduleCurrentSelectionPreview()
        {
            sendWhenPluginConnects = false;
            EditorApplication.delayCall -= ExportSelectedAsync;
            EditorApplication.delayCall -= BuildLocalPreview;
            CancelLocalPreviewBuild();
            int total = selectedSceneRoots.Count;
            if (total == 0)
            {
                localPreviewRebuildPending = false;
                ClearLocalPreviewCache();
                exportStatus = "Tick at least one Scene UI GameObject in the Hierarchy tree first.";
                Repaint();
                return;
            }

            localPreviewRebuildPending = true;
            if (exportInProgress)
            {
                exportStatus = "Checked UI selection changed. The local preview will rebuild after the current Figma send finishes.";
                Repaint();
                return;
            }

            exportStatus = total > MaximumReviewedSceneSources
                ? total + " UI sources selected. Review batches are limited to " + MaximumReviewedSceneSources
                  + " sources to keep Unity responsive; split this selection into smaller batches."
                : total + " UI source" + (total == 1 ? string.Empty : "s")
                  + " selected. Click Build 3-page review when the selection is ready.";
            Repaint();
        }

        private static bool CanBuildReviewedSceneSourceCount(int sourceCount)
        {
            return sourceCount > 0 && sourceCount <= MaximumReviewedSceneSources;
        }

        private void RefreshBridgeWhenNeeded()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < nextBridgeRefreshAt) return;
            nextBridgeRefreshAt = now + 3d;
            CheckBridgeAsync();
        }

        private void DrawSceneSourcePanel()
        {
            Scene scene = SceneManager.GetActiveScene();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("1. SCENE UI — GAMEOBJECT HIERARCHY", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(scene.IsValid() ? scene.name : "No active scene", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(scene.IsValid() ? scene.path : "Open a saved scene to select UI branches.", EditorStyles.miniLabel);

                EditorGUILayout.Space(4f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(Selection.gameObjects.Length == 0))
                    {
                        if (GUILayout.Button("Add selected")) AddSceneSources(Selection.gameObjects);
                    }
                    if (GUILayout.Button("All Canvas roots"))
                        AddSceneSources(PicturePuzzleFigmaWireframeExporter.GetSceneUguiHierarchyRoots(scene));
                    using (new EditorGUI.DisabledScope(selectedSceneRoots.Count == 0))
                    {
                        if (GUILayout.Button("Clear")) ClearSceneSelection();
                    }
                }

                sceneFilter = EditorGUILayout.TextField("Search", sceneFilter);
                EditorGUILayout.LabelField(selectedSceneRoots.Count + " scene object" + (selectedSceneRoots.Count == 1 ? string.Empty : "s") + " selected", EditorStyles.miniBoldLabel);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    sceneTreeScroll = EditorGUILayout.BeginScrollView(sceneTreeScroll, GUILayout.MinHeight(390f), GUILayout.MaxHeight(560f));
                    List<GameObject> roots = PicturePuzzleFigmaWireframeExporter.GetSceneUguiHierarchyRoots(scene);
                    if (roots.Count == 0)
                    {
                        EditorGUILayout.HelpBox("The active scene has no UGUI Canvas hierarchy.", MessageType.None);
                    }
                    else
                    {
                        foreach (GameObject root in roots)
                        {
                            if (initializedSceneRoots.Add(root.GetInstanceID()))
                                expandedSceneNodes.Add(root.GetInstanceID());
                            DrawSceneNode(root.transform, 0);
                        }
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawSceneNode(Transform transform, int depth)
        {
            if (!(transform is RectTransform)) return;
            if (!SubtreeMatchesFilter(transform)) return;

            GameObject gameObject = transform.gameObject;
            int instanceId = gameObject.GetInstanceID();
            bool hasChildren = Enumerable.Range(0, transform.childCount)
                .Select(transform.GetChild)
                .Any(child => child is RectTransform && SubtreeMatchesFilter(child));
            bool expanded = expandedSceneNodes.Contains(instanceId) || !string.IsNullOrEmpty(sceneFilter);
            bool exportable = PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy(gameObject);
            bool selected = selectedSceneRoots.Contains(gameObject);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(depth * 14f);
                if (hasChildren)
                {
                    bool nextExpanded = GUILayout.Toggle(expanded, GUIContent.none, EditorStyles.foldout, GUILayout.Width(14f));
                    if (nextExpanded) expandedSceneNodes.Add(instanceId);
                    else expandedSceneNodes.Remove(instanceId);
                    expanded = nextExpanded;
                }
                else
                {
                    GUILayout.Space(14f);
                }

                using (new EditorGUI.DisabledScope(!exportable))
                {
                    bool nextSelected = EditorGUILayout.Toggle(selected, GUILayout.Width(18f));
                    if (nextSelected != selected)
                    {
                        SetSceneSourceChecked(gameObject, nextSelected);
                        selected = nextSelected;
                    }
                }

                string suffix = gameObject.activeInHierarchy ? string.Empty : " (inactive)";
                if (GUILayout.Button(gameObject.name + suffix, EditorStyles.label))
                {
                    Selection.activeGameObject = gameObject;
                    EditorGUIUtility.PingObject(gameObject);
                    FocusScenePreview(gameObject);
                }
                if (gameObject.GetComponent<Canvas>() != null)
                    GUILayout.Label("Canvas", EditorStyles.miniLabel, GUILayout.Width(48f));
                if (selected && GUILayout.Button(
                        localPreviewRebuildPending ? "Build & preview" : "Preview",
                        GUILayout.Width(localPreviewRebuildPending ? 96f : 58f)))
                    FocusLocalPreviewForScene(gameObject);
            }

            if (!expanded) return;
            for (int index = 0; index < transform.childCount; index++)
                DrawSceneNode(transform.GetChild(index), depth + 1);
        }

        private bool SubtreeMatchesFilter(Transform transform)
        {
            if (string.IsNullOrWhiteSpace(sceneFilter)) return true;
            if (transform.name.IndexOf(sceneFilter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            for (int index = 0; index < transform.childCount; index++)
            {
                if (SubtreeMatchesFilter(transform.GetChild(index))) return true;
            }
            return false;
        }

        private void DrawSelectedUiPreviewPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                int total = selectedSceneRoots.Count;
                EditorGUILayout.LabelField("SELECTED SCENE UI", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(total == 0
                    ? "No Scene UI selected."
                    : total + " Scene object" + (total == 1 ? string.Empty : "s") + " selected for export.", EditorStyles.miniLabel);

                float previewHeight = Mathf.Clamp(Mathf.Max(168f, total * 154f), 168f, 500f);
                selectedPreviewScroll = EditorGUILayout.BeginScrollView(selectedPreviewScroll, GUILayout.Height(previewHeight));
                if (total == 0)
                {
                    GUILayout.Space(56f);
                    EditorGUILayout.LabelField("No UI selected", EditorStyles.centeredGreyMiniLabel);
                    GUILayout.Space(56f);
                }

                foreach (GameObject sceneRoot in selectedSceneRoots.Where(PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy).ToList())
                {
                    RequestScenePreview(sceneRoot);
                    Texture2D texture;
                    scenePreviewCache.TryGetValue(sceneRoot.GetInstanceID(), out texture);
                    DrawSelectedPreviewCard(
                        sceneRoot.name,
                        "Scene  |  " + PicturePuzzleFigmaWireframeExporter.GetSceneHierarchyPath(sceneRoot),
                        texture,
                        failedScenePreviews.Contains(sceneRoot.GetInstanceID()),
                        previewSceneRoot == sceneRoot,
                        () => FocusScenePreview(sceneRoot));
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawAdvancedPanel()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced", true);
                if (!showAdvanced) return;

                EditorGUILayout.LabelField(bridgeStatus, EditorStyles.wordWrappedMiniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Figma plugin setup")) ShowFigmaPluginSetup();
                    if (GUILayout.Button("Refresh destinations")) CheckBridgeAsync();
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(lastManifestPath)))
                    {
                        if (GUILayout.Button("Export folder"))
                            EditorUtility.RevealInFinder(Path.GetDirectoryName(lastManifestPath));
                    }
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(lastQueueUrl)))
                    {
                        if (GUILayout.Button("Queue results")) Application.OpenURL(lastQueueUrl);
                    }
                }
            }
        }

        private void DrawLocalExportPreviewPanel()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("3. REVIEW BEFORE SEND - LARGE FIGMA PAGE PREVIEW", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(
                    "The 3 tabs below match the 3 managed pages created in Figma.",
                    EditorStyles.wordWrappedMiniLabel);

                localPreviewStage = (LocalPreviewStage)GUILayout.Toolbar(
                    (int)localPreviewStage,
                    new[] { "1. Assets", "2. Wireframe", "3. UI Result" },
                    GUILayout.Height(30f));

                if (localPreviewBuildSession != null)
                {
                    float progress = localPreviewBuildSession.totalCount <= 0
                        ? 0f
                        : localPreviewBuildSession.completedCount / (float)localPreviewBuildSession.totalCount;
                    Rect progressRect = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
                    EditorGUI.ProgressBar(
                        progressRect,
                        progress,
                        "Building review " + localPreviewBuildSession.completedCount + "/"
                        + localPreviewBuildSession.totalCount + " UI source(s)");
                    EditorGUILayout.LabelField(
                        "One source is rendered per editor update. You can cancel before the next source starts.",
                        EditorStyles.wordWrappedMiniLabel);
                    if (GUILayout.Button("Cancel preview build", GUILayout.Height(28f)))
                    {
                        CancelLocalPreviewBuild();
                        localPreviewRebuildPending = true;
                        exportStatus = "Preview build canceled. Adjust the checked UI sources, then build the review again.";
                        Repaint();
                    }
                    return;
                }

                FigmaWireframeItem item = GetLocalPreviewItem();
                if (item == null)
                {
                    int checkedSourceCount = selectedSceneRoots.Count;
                    EditorGUILayout.HelpBox(
                        checkedSourceCount == 0
                            ? "Tick one or more Scene UI GameObjects in the Hierarchy tree, then build the 3-page preview when the selection is ready."
                            : checkedSourceCount + " UI source" + (checkedSourceCount == 1 ? string.Empty : "s")
                              + " selected. Build the 3-page review before sending to Figma.",
                        MessageType.Info);
                    bool canBuildReview = CanBuildReviewedSceneSourceCount(checkedSourceCount);
                    if (checkedSourceCount > MaximumReviewedSceneSources)
                    {
                        EditorGUILayout.HelpBox(
                            "To keep Unity responsive, build and send at most " + MaximumReviewedSceneSources
                            + " UI sources at a time. The checked sources are unchanged; split them into smaller batches.",
                            MessageType.Warning);
                    }
                    using (new EditorGUI.DisabledScope(!canBuildReview || exportInProgress))
                    {
                        if (GUILayout.Button(
                                "Build 3-page review for " + checkedSourceCount + " selected UI"
                                + (checkedSourceCount == 1 ? string.Empty : "s"),
                                GUILayout.Height(40f)))
                            BuildLocalPreview();
                    }
                    return;
                }

                List<FigmaWireframeItem> items = localPreviewExport.batch.items;
                if (localPreviewRebuildPending)
                {
                    EditorGUILayout.HelpBox(
                        "This review contains " + items.Count + " old UI source(s), while " + selectedSceneRoots.Count
                        + " are currently selected. Click Build & preview for a child branch, or refresh all 3 reviews. "
                        + "Untick its parent if only the child dialogs should be exported.",
                        MessageType.Info);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        items.Count + " checked UI source" + (items.Count == 1 ? string.Empty : "s") + " ready for review",
                        EditorStyles.miniBoldLabel);
                    using (new EditorGUI.DisabledScope(!CanBuildReviewedSceneSourceCount(selectedSceneRoots.Count)
                                                       || exportInProgress))
                    {
                        if (GUILayout.Button("Refresh all 3 reviews", GUILayout.Width(150f))) BuildLocalPreview();
                    }
                }
                string[] labels = items
                    .Select((candidate, index) => (index + 1) + ". " + candidate.displayName)
                    .ToArray();
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(localPreviewItemIndex == 0))
                    {
                        if (GUILayout.Button("<", GUILayout.Width(30f)))
                        {
                            localPreviewItemIndex--;
                            localAssetPreviewPage = 0;
                        }
                    }

                    int selectedIndex = EditorGUILayout.Popup(localPreviewItemIndex, labels);
                    if (selectedIndex != localPreviewItemIndex)
                    {
                        localPreviewItemIndex = selectedIndex;
                        localAssetPreviewPage = 0;
                    }

                    using (new EditorGUI.DisabledScope(localPreviewItemIndex >= items.Count - 1))
                    {
                        if (GUILayout.Button(">", GUILayout.Width(30f)))
                        {
                            localPreviewItemIndex++;
                            localAssetPreviewPage = 0;
                        }
                    }
                }

                item = GetLocalPreviewItem();
                EditorGUILayout.LabelField(
                    (localPreviewItemIndex + 1) + " of " + items.Count + " popup(s)  |  " + GetPreviewSourceLabel(item),
                    EditorStyles.wordWrappedMiniLabel);

                switch (localPreviewStage)
                {
                    case LocalPreviewStage.Assets:
                        DrawAssetOutputPreview(item);
                        break;
                    case LocalPreviewStage.Wireframe:
                        DrawWireframeOutputPreview(item);
                        break;
                    case LocalPreviewStage.UiResult:
                        DrawUiResultOutputPreview(item);
                        break;
                }

                if (item.warnings != null && item.warnings.Count > 0)
                {
                    EditorGUILayout.HelpBox(
                        "Export notes for this UI source:\n" + string.Join("\n", item.warnings),
                        MessageType.Warning);
                }
            }
        }

        private FigmaWireframeItem GetLocalPreviewItem()
        {
            if (localPreviewExport == null
                || localPreviewExport.batch == null
                || localPreviewExport.batch.items == null
                || localPreviewExport.batch.items.Count == 0)
                return null;

            localPreviewItemIndex = Mathf.Clamp(localPreviewItemIndex, 0, localPreviewExport.batch.items.Count - 1);
            return localPreviewExport.batch.items[localPreviewItemIndex];
        }

        private static string GetPreviewSourceLabel(FigmaWireframeItem item)
        {
            if (item == null) return string.Empty;
            return string.IsNullOrEmpty(item.hierarchyPath)
                ? item.assetPath
                : item.assetPath + " / " + item.hierarchyPath;
        }

        private void DrawAssetOutputPreview(FigmaWireframeItem item)
        {
            List<FigmaWireframeNode> visualNodes = item.nodes
                .Where(node => node != null
                               && PicturePuzzleFigmaWireframeExporter.ShouldIncludeAssetReviewNode(node)
                               && (!string.IsNullOrEmpty(node.assetImageId)
                                   || !string.IsNullOrEmpty(node.visualImageId)))
                .ToList();
            EditorGUILayout.LabelField(
                visualNodes.Count + " component PNG asset(s), in the same render order used by Figma.",
                EditorStyles.wordWrappedMiniLabel);
            if (visualNodes.Count == 0)
            {
                EditorGUILayout.HelpBox("This popup has no captured Graphic layers.", MessageType.Warning);
                return;
            }

            int pageCount = Mathf.Max(1, Mathf.CeilToInt(visualNodes.Count / (float)AssetPreviewCardsPerPage));
            localAssetPreviewPage = Mathf.Clamp(localAssetPreviewPage, 0, pageCount - 1);
            if (pageCount > 1)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(localAssetPreviewPage == 0))
                    {
                        if (GUILayout.Button("<", GUILayout.Width(30f))) localAssetPreviewPage--;
                    }

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(
                        "Asset page " + (localAssetPreviewPage + 1) + " / " + pageCount,
                        EditorStyles.miniBoldLabel,
                        GUILayout.Width(130f));
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(localAssetPreviewPage >= pageCount - 1))
                    {
                        if (GUILayout.Button(">", GUILayout.Width(30f))) localAssetPreviewPage++;
                    }
                }
            }

            List<FigmaWireframeNode> pageNodes = visualNodes
                .Skip(localAssetPreviewPage * AssetPreviewCardsPerPage)
                .Take(AssetPreviewCardsPerPage)
                .ToList();
            int columns = Mathf.Max(1, Mathf.Min(5, pageNodes.Count));
            localAssetPreviewScroll = EditorGUILayout.BeginScrollView(localAssetPreviewScroll, GUILayout.Height(520f));
            for (int firstIndex = 0; firstIndex < pageNodes.Count; firstIndex += columns)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int column = 0; column < columns && firstIndex + column < pageNodes.Count; column++)
                    {
                        int sourceIndex = localAssetPreviewPage * AssetPreviewCardsPerPage + firstIndex + column;
                        DrawAssetOutputCard(sourceIndex, pageNodes[firstIndex + column]);
                    }
                    GUILayout.FlexibleSpace();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawAssetOutputCard(int index, FigmaWireframeNode node)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(188f), GUILayout.Height(258f)))
            {
                EditorGUILayout.LabelField((index + 1) + ". " + node.name, EditorStyles.miniBoldLabel);
                Rect imageArea = GUILayoutUtility.GetRect(164f, 172f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(imageArea, new Color(0.08f, 0.09f, 0.12f, 1f));
                string artifactId = !string.IsNullOrEmpty(node.assetImageId)
                    ? node.assetImageId
                    : node.visualImageId;
                Texture2D texture = GetLocalPreviewImage(artifactId);
                if (texture != null)
                    GUI.DrawTexture(imageArea, texture, ScaleMode.ScaleToFit, true);
                else
                    GUI.Label(imageArea, "Missing PNG", EditorStyles.centeredGreyMiniLabel);

                FigmaWireframeRect bounds = !string.IsNullOrEmpty(node.assetImageId)
                    ? node.assetRect ?? node.rect
                    : node.visualRect ?? node.rect;
                EditorGUILayout.LabelField(
                    node.role + "  |  " + Mathf.RoundToInt(bounds.width) + " x " + Mathf.RoundToInt(bounds.height),
                    EditorStyles.miniLabel);
                EditorGUILayout.LabelField(
                    string.IsNullOrEmpty(node.nestedPrefabPath) ? "draw " + node.renderOrder : "nested prefab  |  draw " + node.renderOrder,
                    EditorStyles.miniLabel);
            }
        }

        private void DrawWireframeOutputPreview(FigmaWireframeItem item)
        {
            EditorGUILayout.LabelField(
                "Editable frame geometry generated for the Figma Wireframes page. Visual PNG layers are intentionally replaced by editable structure here.",
                EditorStyles.wordWrappedMiniLabel);
            Rect previewArea = GUILayoutUtility.GetRect(0f, 620f, GUILayout.ExpandWidth(true));
            DrawWireframeCanvas(previewArea, item);
        }

        private static void DrawWireframeCanvas(Rect area, FigmaWireframeItem item)
        {
            EditorGUI.DrawRect(area, new Color(0.08f, 0.09f, 0.12f, 1f));
            float canvasHeight = PicturePuzzleFigmaWireframeExporter.CanvasHeight;
            foreach (FigmaWireframeNode node in item.nodes.Where(node => node != null && node.rect != null))
                canvasHeight = Mathf.Max(canvasHeight, node.rect.y + node.rect.height);

            float scale = Mathf.Min(
                Mathf.Max(0.01f, (area.width - 36f) / PicturePuzzleFigmaWireframeExporter.CanvasWidth),
                Mathf.Max(0.01f, (area.height - 28f) / canvasHeight));
            Rect canvas = new Rect(
                area.center.x - PicturePuzzleFigmaWireframeExporter.CanvasWidth * scale * 0.5f,
                area.center.y - canvasHeight * scale * 0.5f,
                PicturePuzzleFigmaWireframeExporter.CanvasWidth * scale,
                canvasHeight * scale);
            EditorGUI.DrawRect(canvas, Color.white);
            DrawWireframeOutline(canvas, new Color(0.44f, 0.49f, 0.58f, 1f));

            foreach (FigmaWireframeNode node in item.nodes
                         .Where(node => node != null && node.rect != null)
                         .OrderBy(node => node.renderOrder)
                         .ThenBy(node => node.siblingIndex))
            {
                Rect rect = new Rect(
                    canvas.x + node.rect.x * scale,
                    canvas.y + node.rect.y * scale,
                    Mathf.Max(1f, node.rect.width * scale),
                    Mathf.Max(1f, node.rect.height * scale));
                Color color = GetWireframeColor(node);
                bool isContainer = string.Equals(node.role, "container", StringComparison.Ordinal);
                EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, isContainer ? 0.03f : 0.19f));
                DrawWireframeOutline(rect, new Color(color.r, color.g, color.b, isContainer ? 0.35f : 0.85f));

                if (rect.width < 42f || rect.height < 12f) continue;
                string label = GetWireframePreviewLabel(node);
                if (string.IsNullOrEmpty(label)) continue;
                GUI.Label(rect, label, EditorStyles.centeredGreyMiniLabel);
            }
        }

        private static string GetWireframePreviewLabel(FigmaWireframeNode node)
        {
            if (node == null
                || !string.Equals(node.role, "text", StringComparison.Ordinal)
                || string.IsNullOrEmpty(node.text))
                return string.Empty;

            return node.text.Replace('\n', ' ');
        }

        private static Color GetWireframeColor(FigmaWireframeNode node)
        {
            if (node.color != null)
                return new Color(node.color.r, node.color.g, node.color.b, Mathf.Clamp01(node.color.a));
            if (string.Equals(node.role, "button", StringComparison.Ordinal)
                || string.Equals(node.role, "toggle", StringComparison.Ordinal)
                || string.Equals(node.role, "slider", StringComparison.Ordinal))
                return new Color(0.26f, 0.53f, 0.9f, 1f);
            return new Color(0.48f, 0.55f, 0.68f, 1f);
        }

        private static void DrawWireframeOutline(Rect rect, Color color)
        {
            const float thickness = 1f;
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
        }

        private void DrawUiResultOutputPreview(FigmaWireframeItem item)
        {
            EditorGUILayout.LabelField(
                "Compare the composited component layers (left) with Unity's full 1080 x 1920 reference capture (right) before sending.",
                EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawOutputPortrait(
                    "Layered UI result",
                    GetLayeredResultPreview(item),
                    "The same component PNG layering Figma creates.",
                    Color.black);
                DrawOutputPortrait(
                    "Unity reference",
                    GetLocalPreviewImage(item.referenceImageId),
                    "Full Unity capture retained for visual comparison.",
                    Color.black);
            }
        }

        private static void DrawOutputPortrait(string title, Texture2D texture, string description, Color background)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
                Rect imageArea = GUILayoutUtility.GetRect(0f, 560f, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(imageArea, background);
                if (texture != null)
                    GUI.DrawTexture(imageArea, texture, ScaleMode.ScaleToFit, true);
                else
                    GUI.Label(imageArea, "Missing preview", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private Texture2D GetLocalPreviewImage(string artifactId)
        {
            return LoadLocalPreviewImage(artifactId, true);
        }

        private Texture2D LoadLocalPreviewImage(string artifactId, bool cacheForGui)
        {
            if (string.IsNullOrEmpty(artifactId) || localPreviewExport == null || string.IsNullOrEmpty(localPreviewExport.manifestPath))
                return null;

            Texture2D texture = null;
            if (cacheForGui && localPreviewImageCache.TryGetValue(artifactId, out texture)) return texture;

            string outputDirectory = Path.GetDirectoryName(localPreviewExport.manifestPath);
            string imagePath = Path.Combine(outputDirectory, artifactId + ".png");
            if (!File.Exists(imagePath)) return null;

            try
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
                {
                    name = artifactId + " Figma Export Preview",
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(imagePath), cacheForGui))
                {
                    Object.DestroyImmediate(texture);
                    return null;
                }

                if (cacheForGui)
                    CacheLocalPreviewImage(artifactId, texture);
                return texture;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[PicturePuzzle Figma] Could not load local preview image " + imagePath + ": " + exception.Message);
                if (texture != null) Object.DestroyImmediate(texture);
                return null;
            }
        }

        private void CacheLocalPreviewImage(string artifactId, Texture2D texture)
        {
            while (localPreviewImageCache.Count >= LocalPreviewImageCacheLimit && localPreviewImageCacheOrder.Count > 0)
            {
                string oldestArtifactId = localPreviewImageCacheOrder.Dequeue();
                Texture2D oldestTexture;
                if (!localPreviewImageCache.TryGetValue(oldestArtifactId, out oldestTexture)) continue;
                localPreviewImageCache.Remove(oldestArtifactId);
                if (oldestTexture != null) Object.DestroyImmediate(oldestTexture);
            }

            localPreviewImageCache[artifactId] = texture;
            localPreviewImageCacheOrder.Enqueue(artifactId);
        }

        private Texture2D GetLayeredResultPreview(FigmaWireframeItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.itemId)) return null;

            Texture2D cached;
            if (localResultPreviewCache.TryGetValue(item.itemId, out cached)) return cached;

            foreach (Texture2D previous in localResultPreviewCache.Values.Where(texture => texture != null))
                Object.DestroyImmediate(previous);
            localResultPreviewCache.Clear();

            var composite = new Texture2D(
                PicturePuzzleFigmaWireframeExporter.CanvasWidth,
                PicturePuzzleFigmaWireframeExporter.CanvasHeight,
                TextureFormat.RGBA32,
                false,
                false)
            {
                name = item.displayName + " Layered Figma Result",
                hideFlags = HideFlags.HideAndDontSave
            };
            try
            {
                Color32[] pixels = new Color32[PicturePuzzleFigmaWireframeExporter.CanvasWidth
                                                 * PicturePuzzleFigmaWireframeExporter.CanvasHeight];

                foreach (FigmaWireframeNode node in item.nodes
                             .Where(node => node != null && !string.IsNullOrEmpty(node.visualImageId))
                             .OrderBy(node => node.renderOrder))
                {
                    Texture2D layer = LoadLocalPreviewImage(node.visualImageId, false);
                    FigmaWireframeRect rect = node.visualRect ?? node.rect;
                    if (layer == null || rect == null)
                    {
                        if (layer != null) Object.DestroyImmediate(layer);
                        continue;
                    }

                    try
                    {
                        int targetWidth = Mathf.Max(1, Mathf.RoundToInt(rect.width));
                        int targetHeight = Mathf.Max(1, Mathf.RoundToInt(rect.height));
                        int targetX = Mathf.RoundToInt(rect.x);
                        int targetY = PicturePuzzleFigmaWireframeExporter.CanvasHeight
                                      - Mathf.RoundToInt(rect.y + rect.height);
                        Color32[] layerPixels = layer.GetPixels32();

                        for (int y = 0; y < targetHeight; y++)
                        {
                            int destinationY = targetY + y;
                            if (destinationY < 0 || destinationY >= PicturePuzzleFigmaWireframeExporter.CanvasHeight) continue;
                            int sourceY = Mathf.Min(layer.height - 1, Mathf.FloorToInt(y * layer.height / (float)targetHeight));
                            for (int x = 0; x < targetWidth; x++)
                            {
                                int destinationX = targetX + x;
                                if (destinationX < 0 || destinationX >= PicturePuzzleFigmaWireframeExporter.CanvasWidth) continue;
                                int sourceX = Mathf.Min(layer.width - 1, Mathf.FloorToInt(x * layer.width / (float)targetWidth));
                                int destinationIndex = destinationY * PicturePuzzleFigmaWireframeExporter.CanvasWidth + destinationX;
                                pixels[destinationIndex] = AlphaOver(pixels[destinationIndex], layerPixels[sourceY * layer.width + sourceX]);
                            }
                        }
                    }
                    finally
                    {
                        Object.DestroyImmediate(layer);
                    }
                }

                composite.SetPixels32(pixels);
                composite.Apply(false, true);
                localResultPreviewCache.Add(item.itemId, composite);
                return composite;
            }
            catch
            {
                Object.DestroyImmediate(composite);
                throw;
            }
        }

        private static Color32 AlphaOver(Color32 background, Color32 foreground)
        {
            float foregroundAlpha = foreground.a / 255f;
            if (foregroundAlpha <= 0f) return background;
            float backgroundAlpha = background.a / 255f;
            float outputAlpha = foregroundAlpha + backgroundAlpha * (1f - foregroundAlpha);
            if (outputAlpha <= 0f) return new Color32(0, 0, 0, 0);

            float red = (foreground.r * foregroundAlpha + background.r * backgroundAlpha * (1f - foregroundAlpha)) / outputAlpha;
            float green = (foreground.g * foregroundAlpha + background.g * backgroundAlpha * (1f - foregroundAlpha)) / outputAlpha;
            float blue = (foreground.b * foregroundAlpha + background.b * backgroundAlpha * (1f - foregroundAlpha)) / outputAlpha;
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(red), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(green), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(blue), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(outputAlpha * 255f), 0, 255));
        }

        private void ClearLocalPreviewCache()
        {
            bool clearedReadyPreview = localPreviewExport != null;
            FigmaWireframeExportResult discardedPreview = localPreviewExport;
            foreach (Texture2D texture in localPreviewImageCache.Values.Where(texture => texture != null))
                Object.DestroyImmediate(texture);
            foreach (Texture2D texture in localResultPreviewCache.Values.Where(texture => texture != null))
                Object.DestroyImmediate(texture);
            localPreviewImageCache.Clear();
            localPreviewImageCacheOrder.Clear();
            localResultPreviewCache.Clear();
            localPreviewExport = null;
            localPreviewItemIndex = 0;
            localAssetPreviewPage = 0;
            localPreviewStage = LocalPreviewStage.Assets;
            lastManifestPath = null;
            lastQueueUrl = null;
            RememberDiscardedPreviewArtifacts(discardedPreview);
            if (clearedReadyPreview && windowActive && !exportInProgress)
                exportStatus = "The checked UI selection changed. Build the three local page reviews again.";
        }

        private void RememberDiscardedPreviewArtifacts(FigmaWireframeExportResult preview)
        {
            string batchId = preview != null && preview.batch != null ? preview.batch.batchId : null;
            if (!string.IsNullOrEmpty(batchId))
                discardedPreviewBatchIds.Add(batchId);
        }

        private void DeleteDiscardedPreviewArtifacts()
        {
            if (discardedPreviewBatchIds.Count == 0) return;

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string exportRoot = Path.GetFullPath(Path.Combine(projectRoot, PicturePuzzleFigmaWireframeExporter.ExportRootRelative));
            string exportRootPrefix = exportRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                      + Path.DirectorySeparatorChar;
            foreach (string batchId in discardedPreviewBatchIds.ToArray())
            {
                if (submittedBatchIds.Contains(batchId)) continue;
                string batchDirectory = Path.GetFullPath(Path.Combine(exportRoot, batchId));
                if (!batchDirectory.StartsWith(exportRootPrefix, StringComparison.OrdinalIgnoreCase)
                    || !Directory.Exists(batchDirectory))
                {
                    discardedPreviewBatchIds.Remove(batchId);
                    continue;
                }

                try
                {
                    Directory.Delete(batchDirectory, true);
                    discardedPreviewBatchIds.Remove(batchId);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[PicturePuzzle Figma] Could not remove discarded local preview " + batchId + ": " + exception.Message);
                }
            }
        }

        private void ScheduleDiscardedPreviewArtifactCleanup()
        {
            EditorApplication.delayCall -= DeleteDiscardedPreviewArtifacts;
            EditorApplication.delayCall += DeleteDiscardedPreviewArtifacts;
        }

        private static void DrawSelectedPreviewCard(
            string title,
            string source,
            Texture2D texture,
            bool failed,
            bool focused,
            Action focus)
        {
            Color previousBackground = GUI.backgroundColor;
            if (focused) GUI.backgroundColor = new Color(0.55f, 0.78f, 1f, 1f);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.Height(148f)))
            {
                GUI.backgroundColor = previousBackground;
                Rect previewArea = GUILayoutUtility.GetRect(78f, 132f, GUILayout.Width(78f), GUILayout.Height(132f));
                EditorGUI.DrawRect(previewArea, new Color(0.06f, 0.06f, 0.06f, 1f));
                Rect imageRect = GetCenteredPortraitRect(previewArea);
                if (texture != null)
                    GUI.DrawTexture(imageRect, texture, ScaleMode.ScaleToFit, true);
                else
                    GUI.Label(previewArea, failed ? "Unavailable" : "Rendering...", EditorStyles.centeredGreyMiniLabel);

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(source, EditorStyles.wordWrappedMiniLabel, GUILayout.MaxHeight(54f));
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField(texture != null ? "Preview ready" : failed ? "Preview unavailable" : "Queued for preview", EditorStyles.miniLabel);
                    if (GUILayout.Button(focused ? "Focused" : "Focus", GUILayout.Height(24f))) focus();
                }

                if (Event.current.type == EventType.MouseDown && previewArea.Contains(Event.current.mousePosition))
                {
                    focus();
                    Event.current.Use();
                }
            }
            GUI.backgroundColor = previousBackground;
        }

        private static Rect GetCenteredPortraitRect(Rect area)
        {
            float height = Mathf.Max(0f, area.height - 8f);
            float width = height * PicturePuzzleFigmaWireframeExporter.CanvasWidth / PicturePuzzleFigmaWireframeExporter.CanvasHeight;
            return new Rect(area.center.x - width * 0.5f, area.y + 4f, width, height);
        }

        private void FocusScenePreview(GameObject root)
        {
            if (!PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy(root)) return;
            previewSceneRoot = root;
            previewPrefabPath = null;
            Repaint();
        }

        private void FocusPrefabPreview(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            previewSceneRoot = null;
            previewPrefabPath = path;
            RequestPrefabPreview(path);
            Repaint();
        }

        private void FocusFallbackPreview()
        {
            GameObject sceneRoot = selectedSceneRoots.LastOrDefault(root => PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy(root));
            if (sceneRoot != null)
            {
                FocusScenePreview(sceneRoot);
                return;
            }

            previewSceneRoot = null;
            previewPrefabPath = null;
        }

        private void RequestScenePreview(GameObject root)
        {
            if (!PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy(root)) return;
            int instanceId = root.GetInstanceID();
            if (scenePreviewCache.ContainsKey(instanceId)
                || failedScenePreviews.Contains(instanceId)
                || !queuedScenePreviews.Add(instanceId)) return;

            scenePreviewQueue.Enqueue(root);
            ScheduleScenePreview();
        }

        private void ScheduleScenePreview()
        {
            if (scenePreviewGenerationScheduled || scenePreviewQueue.Count == 0) return;
            scenePreviewGenerationScheduled = true;
            EditorApplication.delayCall += GenerateNextScenePreview;
        }

        private void GenerateNextScenePreview()
        {
            scenePreviewGenerationScheduled = false;
            if (this == null) return;
            while (scenePreviewQueue.Count > 0)
            {
                GameObject root = scenePreviewQueue.Dequeue();
                if (root == null) continue;
                int instanceId = root.GetInstanceID();
                queuedScenePreviews.Remove(instanceId);
                if (!PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy(root)) continue;
                try
                {
                    Texture2D preview = PicturePuzzleFigmaWireframeExporter.CreateScenePreviewTexture(root);
                    if (preview != null) scenePreviewCache[instanceId] = preview;
                    else failedScenePreviews.Add(instanceId);
                }
                catch (Exception exception)
                {
                    failedScenePreviews.Add(instanceId);
                    Debug.LogWarning("[PicturePuzzle Figma] Could not preview scene UI " + root.name + ": " + exception.Message);
                }
                break;
            }
            ScheduleScenePreview();
            Repaint();
        }

        private void ClearScenePreviewCache()
        {
            EditorApplication.delayCall -= GenerateNextScenePreview;
            scenePreviewGenerationScheduled = false;
            foreach (Texture2D texture in scenePreviewCache.Values)
                if (texture != null) Object.DestroyImmediate(texture);
            scenePreviewCache.Clear();
            queuedScenePreviews.Clear();
            failedScenePreviews.Clear();
            scenePreviewQueue.Clear();
        }

        private void DrawPrefabSourcePanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("1. UI SOURCES — PREFABS", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField(
                    selectedPrefabPaths.Count + " of " + projectPrefabPaths.Count + " prefab" + (projectPrefabPaths.Count == 1 ? string.Empty : "s") + " selected",
                    EditorStyles.miniLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    pendingProjectSource = EditorGUILayout.ObjectField("Prefab or folder", pendingProjectSource, typeof(Object), false);
                    using (new EditorGUI.DisabledScope(pendingProjectSource == null))
                    {
                        if (GUILayout.Button("Add", GUILayout.Width(52f)))
                        {
                            AddProjectSources(new[] { pendingProjectSource });
                            pendingProjectSource = null;
                        }
                    }
                }

                Rect dropRect = GUILayoutUtility.GetRect(0f, 28f, GUILayout.ExpandWidth(true));
                GUI.Box(dropRect, "Drop prefab or folder here", EditorStyles.helpBox);
                HandleProjectDrop(dropRect);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add Unity selection")) AddProjectSources(Selection.objects);
                    using (new EditorGUI.DisabledScope(projectPrefabPaths.Count == 0))
                    {
                        if (GUILayout.Button("Select all")) SetAllPrefabChecks(true);
                        if (GUILayout.Button("Clear")) ClearPrefabSelection();
                    }
                }

                prefabListScroll = EditorGUILayout.BeginScrollView(prefabListScroll, GUILayout.MinHeight(170f), GUILayout.MaxHeight(300f));
                if (projectPrefabPaths.Count == 0)
                {
                    GUILayout.Space(26f);
                    EditorGUILayout.LabelField("Drop a UI prefab or folder above", EditorStyles.centeredGreyMiniLabel);
                    GUILayout.Space(26f);
                }
                for (int index = 0; index < projectPrefabPaths.Count; index++)
                {
                    if (DrawPrefabCard(index, projectPrefabPaths[index])) break;
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private bool DrawPrefabCard(int index, string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            bool checkedForExport = selectedPrefabPaths.Contains(path);
            if (checkedForExport) RequestPrefabPreview(path);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.Height(98f)))
            {
                Rect previewRect = GUILayoutUtility.GetRect(54f, 86f, GUILayout.Width(54f), GUILayout.Height(86f));
                DrawPrefabPreview(previewRect, path, prefab, checkedForExport);

                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(prefab != null ? prefab.name : Path.GetFileNameWithoutExtension(path), EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
                    bool nextChecked = EditorGUILayout.ToggleLeft("Export to Figma", checkedForExport, EditorStyles.boldLabel);
                    if (nextChecked != checkedForExport)
                    {
                        SetPrefabChecked(path, nextChecked);
                        checkedForExport = nextChecked;
                    }
                    GUILayout.FlexibleSpace();
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(!checkedForExport))
                        {
                            if (GUILayout.Button("Preview")) FocusLocalPreviewForPrefab(path);
                        }
                        if (GUILayout.Button("Remove"))
                        {
                            RemovePrefabAt(index, path);
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void DrawPrefabPreview(Rect rect, string path, GameObject prefab, bool checkedForExport)
        {
            EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.08f, 1f));
            Texture2D texture;
            if (!checkedForExport || !prefabPreviewCache.TryGetValue(path, out texture))
                texture = AssetPreview.GetAssetPreview(prefab) ?? AssetPreview.GetMiniThumbnail(prefab);

            if (texture != null)
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            else
                GUI.Label(rect, "Preview", EditorStyles.centeredGreyMiniLabel);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                if (checkedForExport) FocusLocalPreviewForPrefab(path);
                Event.current.Use();
            }
        }

        private void RequestPrefabPreview(string path)
        {
            if (string.IsNullOrEmpty(path)
                || !selectedPrefabPaths.Contains(path)
                || prefabPreviewCache.ContainsKey(path)
                || failedPrefabPreviews.Contains(path)
                || !queuedPrefabPreviews.Add(path)) return;

            prefabPreviewQueue.Enqueue(path);
            SchedulePrefabPreview();
        }

        private void SchedulePrefabPreview()
        {
            if (previewGenerationScheduled || prefabPreviewQueue.Count == 0) return;
            previewGenerationScheduled = true;
            EditorApplication.delayCall += GenerateNextPrefabPreview;
        }

        private void GenerateNextPrefabPreview()
        {
            previewGenerationScheduled = false;
            if (this == null || prefabPreviewQueue.Count == 0) return;

            string path = prefabPreviewQueue.Dequeue();
            queuedPrefabPreviews.Remove(path);
            if (selectedPrefabPaths.Contains(path))
            {
                try
                {
                    Texture2D texture = PicturePuzzleFigmaWireframeExporter.CreatePrefabPreviewTexture(path);
                    if (texture != null) prefabPreviewCache[path] = texture;
                    else failedPrefabPreviews.Add(path);
                }
                catch (Exception exception)
                {
                    failedPrefabPreviews.Add(path);
                    Debug.LogWarning("[PicturePuzzle Figma] Could not preview " + path + ": " + exception.Message);
                }
            }

            Repaint();
            SchedulePrefabPreview();
        }

        private void RemovePrefabAt(int index, string path)
        {
            projectPrefabPaths.RemoveAt(index);
            selectedPrefabPaths.Remove(path);
            DestroyPrefabPreview(path);
            failedPrefabPreviews.Remove(path);
            ClearLocalPreviewCache();
            if (previewPrefabPath == path) FocusFallbackPreview();
            ScheduleCurrentSelectionPreview();
        }

        private void ClearPrefabSelection()
        {
            if (projectPrefabPaths.Count == 0 && selectedPrefabPaths.Count == 0) return;
            projectPrefabPaths.Clear();
            selectedPrefabPaths.Clear();
            ClearPrefabPreviewCache();
            ClearLocalPreviewCache();
            if (!string.IsNullOrEmpty(previewPrefabPath)) FocusFallbackPreview();
            ScheduleCurrentSelectionPreview();
        }

        private void SetPrefabChecked(string path, bool isChecked)
        {
            bool changed;
            if (isChecked)
            {
                changed = !selectedPrefabPaths.Contains(path);
                if (changed)
                {
                    selectedPrefabPaths.Add(path);
                    selectedPrefabPaths.Sort(StringComparer.Ordinal);
                    RequestPrefabPreview(path);
                    FocusPrefabPreview(path);
                }
            }
            else
            {
                changed = selectedPrefabPaths.Remove(path);
                if (changed)
                {
                    DestroyPrefabPreview(path);
                    failedPrefabPreviews.Remove(path);
                    if (previewPrefabPath == path) FocusFallbackPreview();
                }
            }

            if (!changed) return;
            ClearLocalPreviewCache();
            ScheduleCurrentSelectionPreview();
        }

        private void SetAllPrefabChecks(bool isChecked)
        {
            if (isChecked)
            {
                foreach (string path in projectPrefabPaths)
                {
                    if (!selectedPrefabPaths.Contains(path)) selectedPrefabPaths.Add(path);
                }
                selectedPrefabPaths.Sort(StringComparer.Ordinal);
                foreach (string path in selectedPrefabPaths) RequestPrefabPreview(path);
            }
            else
            {
                selectedPrefabPaths.Clear();
                ClearPrefabPreviewCache();
                if (!string.IsNullOrEmpty(previewPrefabPath)) FocusFallbackPreview();
            }

            ClearLocalPreviewCache();
            if (isChecked && selectedPrefabPaths.Count > 0)
                FocusPrefabPreview(selectedPrefabPaths.Last());
            ScheduleCurrentSelectionPreview();
        }

        private void FocusLocalPreviewForPrefab(string path)
        {
            if (!selectedPrefabPaths.Contains(path)) return;
            FocusPrefabPreview(path);
            if (localPreviewRebuildPending
                || localPreviewExport == null
                || localPreviewExport.batch == null
                || localPreviewExport.batch.items == null)
            {
                BuildLocalPreview();
                return;
            }

            int itemIndex = localPreviewExport.batch.items.FindIndex(item => item != null
                && string.Equals(item.assetPath, path, StringComparison.Ordinal));
            if (itemIndex < 0)
            {
                exportStatus = "This prefab is not in the current preview batch. Rebuild local preview.";
                return;
            }

            localPreviewItemIndex = itemIndex;
            localAssetPreviewPage = 0;
            localPreviewStage = LocalPreviewStage.Assets;
            exportStatus = "Reviewing " + localPreviewExport.batch.items[itemIndex].displayName
                           + ": Assets, Wireframe, and UI Result.";
            Repaint();
        }

        private void FocusLocalPreviewForScene(GameObject root)
        {
            if (!selectedSceneRoots.Contains(root) || root == null) return;
            FocusScenePreview(root);
            if (localPreviewRebuildPending
                || localPreviewExport == null
                || localPreviewExport.batch == null
                || localPreviewExport.batch.items == null)
            {
                BuildLocalPreview();
                return;
            }

            string scenePath = root.scene.path;
            string hierarchyPath = PicturePuzzleFigmaWireframeExporter.GetSceneHierarchyPath(root);
            int itemIndex = localPreviewExport.batch.items.FindIndex(item => item != null
                && string.Equals(item.sourceKind, "scene-hierarchy", StringComparison.Ordinal)
                && string.Equals(item.assetPath, scenePath, StringComparison.Ordinal)
                && string.Equals(item.hierarchyPath, hierarchyPath, StringComparison.Ordinal));
            if (itemIndex < 0)
            {
                exportStatus = "This Scene UI is not in the current preview batch. Build the preview again.";
                return;
            }

            localPreviewItemIndex = itemIndex;
            localAssetPreviewPage = 0;
            localPreviewStage = LocalPreviewStage.Assets;
            exportStatus = "Reviewing " + localPreviewExport.batch.items[itemIndex].displayName
                           + ": Assets, Wireframe, and UI Result.";
            Repaint();
        }

        private void ClearPrefabPreviewCache()
        {
            EditorApplication.delayCall -= GenerateNextPrefabPreview;
            foreach (Texture2D texture in prefabPreviewCache.Values.Where(texture => texture != null))
                Object.DestroyImmediate(texture);
            prefabPreviewCache.Clear();
            failedPrefabPreviews.Clear();
            queuedPrefabPreviews.Clear();
            prefabPreviewQueue.Clear();
            previewGenerationScheduled = false;
        }

        private void DestroyPrefabPreview(string path)
        {
            Texture2D texture;
            if (!prefabPreviewCache.TryGetValue(path, out texture)) return;
            prefabPreviewCache.Remove(path);
            if (texture != null) Object.DestroyImmediate(texture);
        }

        private void HandleProjectDrop(Rect dropRect)
        {
            Event current = Event.current;
            if (!dropRect.Contains(current.mousePosition)) return;
            if (current.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                current.Use();
            }
            else if (current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                AddProjectSources(DragAndDrop.objectReferences);
                current.Use();
            }
        }

        private static bool TryValidateLocalPreviewArtifacts(FigmaWireframeExportResult result, out string error)
        {
            error = "Build and review the three local pages before sending.";
            if (result == null
                || result.batch == null
                || result.batch.items == null
                || result.batch.items.Count == 0
                || string.IsNullOrEmpty(result.manifestPath))
                return false;

            string outputDirectory = Path.GetDirectoryName(result.manifestPath);
            if (string.IsNullOrEmpty(outputDirectory) || !File.Exists(result.manifestPath))
            {
                error = "The reviewed manifest is missing from disk.";
                return false;
            }

            foreach (FigmaWireframeItem item in result.batch.items)
            {
                if (item == null)
                {
                    error = "The reviewed batch contains an invalid UI source.";
                    return false;
                }

                if (string.IsNullOrEmpty(item.referenceImageId)
                    || !File.Exists(Path.Combine(outputDirectory, item.referenceImageId + ".png")))
                {
                    error = "Unity reference image is missing for " + item.displayName + ".";
                    return false;
                }

                if (item.nodes == null)
                {
                    error = "The reviewed component list is missing for " + item.displayName + ".";
                    return false;
                }

                foreach (FigmaWireframeNode node in item.nodes.Where(node => node != null))
                {
                    foreach (string artifactId in new[] { node.assetImageId, node.visualImageId }
                                 .Where(id => !string.IsNullOrEmpty(id))
                                 .Distinct(StringComparer.Ordinal))
                    {
                        if (File.Exists(Path.Combine(outputDirectory, artifactId + ".png"))) continue;
                        error = "Component PNG is missing for " + item.displayName + " / " + node.name + ".";
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        private void DrawExportPanel()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("4. SEND REVIEWED BATCH", EditorStyles.miniBoldLabel);
                int total = selectedSceneRoots.Count;
                BridgePluginInfo[] activePlugins = GetActivePlugins();
                string destination = activePlugins.Length > 0 && selectedPluginIndex >= 0 && selectedPluginIndex < activePlugins.Length
                    ? activePlugins[selectedPluginIndex].fileName
                    : "Figma not connected";
                EditorGUILayout.LabelField(
                    total + " UI  →  " + destination,
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Sends exactly the Assets, Wireframe, and UI Result batch reviewed above.", EditorStyles.miniLabel);

                string artifactError;
                bool artifactsReady = TryValidateLocalPreviewArtifacts(localPreviewExport, out artifactError);
                if (!artifactsReady)
                    EditorGUILayout.HelpBox(artifactError, MessageType.Info);
                else if (localPreviewRebuildPending)
                    EditorGUILayout.HelpBox(
                        "The checked UI selection changed. Build and review the updated 3-page preview before sending.",
                        MessageType.Info);

                bool reviewReadyToSend = artifactsReady && !localPreviewRebuildPending;
                if (!reviewReadyToSend)
                {
                    bool canBuildReview = CanBuildReviewedSceneSourceCount(total);
                    string buildLabel = artifactsReady
                        ? "1. Rebuild 3-page review for " + total + " selected UI"
                        : "1. Build 3-page review for " + total + " selected UI";
                    using (new EditorGUI.DisabledScope(!canBuildReview || exportInProgress))
                    {
                        if (GUILayout.Button(buildLabel, GUILayout.Height(40f)))
                            BuildLocalPreview();
                    }

                    EditorGUILayout.LabelField(
                        "After the review is ready, inspect Assets, Wireframe, and UI Result above. Step 2 will then send that exact batch to Figma.",
                        EditorStyles.wordWrappedMiniLabel);
                }
                else
                {
                    using (new EditorGUI.DisabledScope(exportInProgress || sendWhenPluginConnects))
                    {
                        string sendLabel = exportInProgress
                            ? "Sending reviewed batch to Figma..."
                            : sendWhenPluginConnects
                                ? "Waiting for Figma..."
                                : "2. Send " + total + " reviewed UI to Figma";
                        if (GUILayout.Button(sendLabel, GUILayout.Height(40f))) ExportSelectedAsync();
                    }
                }

                if (reviewReadyToSend)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(exportInProgress))
                        {
                            if (GUILayout.Button("Rebuild 3-page review")) BuildLocalPreview();
                        }
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.LabelField("1080 × 1920 source canvas", EditorStyles.miniLabel, GUILayout.Width(150f));
                    }
                }

                if (!string.IsNullOrWhiteSpace(exportStatus))
                    EditorGUILayout.LabelField(exportStatus, EditorStyles.wordWrappedMiniLabel);
                DrawSourceList("Skipped or failed", skippedSourcePaths, MessageType.Warning);
            }
        }

        private void DrawBridgePanel()
        {
            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("2. FIGMA FILE", EditorStyles.miniBoldLabel);
                BridgePluginInfo[] activePlugins = GetActivePlugins();
                if (activePlugins.Length > 0)
                {
                    SelectCurrentPlugin(activePlugins);
                    string[] labels = activePlugins.Select(FormatPluginLabel).ToArray();
                    int nextIndex = EditorGUILayout.Popup("Figma project", selectedPluginIndex, labels);
                    if (nextIndex != selectedPluginIndex)
                    {
                        selectedPluginIndex = nextIndex;
                        selectedPluginId = activePlugins[nextIndex].id;
                        selectedPluginLabel = FormatPluginLabel(activePlugins[nextIndex]);
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(
                            "Ready • updates only the 3 managed pages in this file",
                            EditorStyles.miniLabel);
                        if (GUILayout.Button("Refresh", GUILayout.Width(72f))) CheckBridgeAsync();
                    }
                }
                else
                {
                    string disconnectedLabel = string.IsNullOrWhiteSpace(selectedPluginLabel)
                        ? "No Figma project detected"
                        : selectedPluginLabel + "  •  disconnected";
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.Popup("Figma project", 0, new[] { disconnectedLabel });
                    EditorGUILayout.HelpBox(
                        "Unity auto-detects Figma Desktop. Figma requires you to run PicturePuzzle Local Bridge once in the target file; Unity reconnects automatically after that.",
                        MessageType.Warning);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Open / reconnect Figma")) BeginAutomaticFigmaConnection(true);
                        if (GUILayout.Button("Plugin setup")) ShowFigmaPluginSetup();
                        if (GUILayout.Button("Refresh projects")) CheckBridgeAsync();
                    }
                }
            }
        }

        private void AddSceneSources(IEnumerable<GameObject> candidates)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            GameObject previewCandidate = null;
            foreach (GameObject candidate in candidates.Where(item => item != null))
            {
                if (candidate.scene == activeScene
                    && PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy(candidate)
                    && !selectedSceneRoots.Contains(candidate))
                {
                    selectedSceneRoots.Add(candidate);
                    previewCandidate = candidate;
                }
            }
            if (previewCandidate != null)
            {
                FocusScenePreview(previewCandidate);
                ScheduleCurrentSelectionPreview();
            }
        }

        private void SetSceneSourceChecked(GameObject root, bool isChecked)
        {
            if (root == null) return;

            bool changed;
            if (isChecked)
            {
                if (!PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy(root)) return;
                changed = !selectedSceneRoots.Contains(root);
                if (changed)
                {
                    selectedSceneRoots.Add(root);
                    FocusScenePreview(root);
                }
            }
            else
            {
                changed = selectedSceneRoots.Remove(root);
                if (changed && previewSceneRoot == root) FocusFallbackPreview();
            }

            if (!changed) return;
            ScheduleCurrentSelectionPreview();
        }

        private void ClearSceneSelection()
        {
            if (selectedSceneRoots.Count == 0) return;
            selectedSceneRoots.Clear();
            if (previewSceneRoot != null) FocusFallbackPreview();
            ScheduleCurrentSelectionPreview();
        }

        private void EnsurePrefabCatalog()
        {
            foreach (string path in selectedPrefabPaths.Where(path => !string.IsNullOrEmpty(path)).ToList())
            {
                if (!projectPrefabPaths.Contains(path)) projectPrefabPaths.Add(path);
            }
            projectPrefabPaths = projectPrefabPaths
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            selectedPrefabPaths = selectedPrefabPaths
                .Where(path => projectPrefabPaths.Contains(path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        private void AddProjectSources(IEnumerable<Object> candidates, bool checkForExport = false)
        {
            string previewCandidate = null;
            foreach (Object candidate in candidates.Where(item => item != null))
            {
                foreach (string path in PicturePuzzleFigmaWireframeExporter.ResolvePrefabPaths(candidate))
                {
                    if (!projectPrefabPaths.Contains(path)) projectPrefabPaths.Add(path);
                    if (!checkForExport || selectedPrefabPaths.Contains(path)) continue;
                    selectedPrefabPaths.Add(path);
                    previewCandidate = path;
                }
            }
            projectPrefabPaths.Sort(StringComparer.Ordinal);
            selectedPrefabPaths.Sort(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(previewCandidate))
            {
                ClearLocalPreviewCache();
                RequestPrefabPreview(previewCandidate);
                FocusPrefabPreview(previewCandidate);
                ScheduleCurrentSelectionPreview();
            }
        }

        private bool PruneSelections(bool scheduleAfterSceneChange = true)
        {
            int sceneSourceCountBeforePrune = selectedSceneRoots.Count;
            Scene activeScene = SceneManager.GetActiveScene();
            selectedSceneRoots.RemoveAll(root => root == null
                || root.scene != activeScene
                || !PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy(root));
            foreach (string path in projectPrefabPaths
                         .Where(path => !PicturePuzzleFigmaWireframeExporter.IsUguiPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path)))
                         .ToList())
            {
                projectPrefabPaths.Remove(path);
                selectedPrefabPaths.Remove(path);
                DestroyPrefabPreview(path);
            }

            if (previewSceneRoot != null && !PicturePuzzleFigmaWireframeExporter.IsUguiSceneHierarchy(previewSceneRoot))
            {
                previewSceneRoot = null;
                FocusFallbackPreview();
            }
            else if (!string.IsNullOrEmpty(previewPrefabPath) && !selectedPrefabPaths.Contains(previewPrefabPath))
            {
                previewPrefabPath = null;
                FocusFallbackPreview();
            }

            bool sceneSelectionChanged = sceneSourceCountBeforePrune != selectedSceneRoots.Count;
            if (sceneSelectionChanged)
            {
                if (scheduleAfterSceneChange) ScheduleCurrentSelectionPreview();
            }
            return sceneSelectionChanged;
        }

        private static void DrawSourceList(string label, List<string> paths, MessageType messageType)
        {
            if (paths.Count == 0) return;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(label + " (" + paths.Count + ")", EditorStyles.miniBoldLabel);
            string preview = string.Join("\n", paths.Take(12));
            if (paths.Count > 12) preview += "\n... and " + (paths.Count - 12) + " more.";
            EditorGUILayout.HelpBox(preview, messageType);
        }

        private async void CheckBridgeAsync()
        {
            await RefreshBridgeAsync();
        }

        private Task RefreshBridgeAsync()
        {
            if (bridgeRefreshTask != null && !bridgeRefreshTask.IsCompleted) return bridgeRefreshTask;
            bridgeRefreshTask = RefreshBridgeCoreAsync();
            return bridgeRefreshTask;
        }

        private async Task RefreshBridgeCoreAsync()
        {
            try
            {
                PicturePuzzleFigmaBridgeMode mode = PicturePuzzleFigmaEmbeddedBridge.EnsureStarted();
                if (mode == PicturePuzzleFigmaBridgeMode.Unavailable)
                    throw new InvalidOperationException(PicturePuzzleFigmaEmbeddedBridge.LastError ?? "The local Unity bridge could not start.");

                string response = await BridgeRequest.Get(BridgeUrl + "/health");
                bridgeHealth = JsonUtility.FromJson<BridgeHealthInfo>(response);
                if (bridgeHealth == null || !bridgeHealth.ok)
                    throw new InvalidOperationException("The local bridge returned an invalid health response.");

                BridgePluginInfo[] activePlugins = GetActivePlugins();
                SelectCurrentPlugin(activePlugins);
                string serverLabel = mode == PicturePuzzleFigmaBridgeMode.Embedded ? "Unity bridge is automatic." : "Compatible bridge detected.";
                if (bridgeHealth.pluginConnected && activePlugins.Length > 0)
                {
                    BridgePluginInfo target = activePlugins[selectedPluginIndex];
                    RememberFigmaFile(target);
                    bridgeStatus = "Ready - " + activePlugins.Length + " Figma file(s) detected. Selected: "
                                   + FormatPluginLabel(target) + ". " + serverLabel;
                    EditorPrefs.SetBool("PicturePuzzle.Figma.PluginSeen", true);
                    if (sendWhenPluginConnects && !exportInProgress)
                    {
                        sendWhenPluginConnects = false;
                        EditorApplication.delayCall += ExportSelectedAsync;
                    }
                }
                else
                {
                    bridgeStatus = serverLabel
                                   + " Auto-detection is active. Figma requires one manual plugin start in the target file; Unity reconnects automatically afterward.";
                }
            }
            catch (Exception exception)
            {
                bridgeHealth = null;
                bridgeStatus = "Bridge unavailable: " + exception.Message;
            }
            finally
            {
                if (windowActive) Repaint();
            }
        }

        private void ShowFigmaPluginSetup()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string manifestPath = Path.GetFullPath(Path.Combine(projectRoot, PluginManifestRelativePath));
            EditorGUIUtility.systemCopyBuffer = manifestPath;
            EditorUtility.RevealInFinder(manifestPath);
            EditorUtility.DisplayDialog(
                "Figma plugin setup (one time)",
                "The manifest path is copied and the file is selected.\n\n"
                + "1. In Figma Desktop open Plugins > Development.\n"
                + "2. Choose Import plugin from manifest.\n"
                + "3. Select manifest.json, then run PicturePuzzle Local Bridge in your target file.\n\n"
                + "After that, Unity detects Figma automatically.",
                "Got it");
        }

        private void BeginAutomaticFigmaConnection(bool forceOpen = false)
        {
            if (!forceOpen && automaticFigmaOpenAttempted)
            {
                nextBridgeRefreshAt = 0d;
                CheckBridgeAsync();
                return;
            }

            automaticFigmaOpenAttempted = true;
            bool openedRememberedFile = TryOpenRememberedFigmaFile();
            bool desktopReady = openedRememberedFile || IsFigmaDesktopRunning() || TryStartFigmaDesktop();
            bridgeStatus = desktopReady
                ? "Figma Desktop is opening or already running. Unity is auto-detecting the local plugin..."
                : "Figma Desktop was not found. Install/open it, then run PicturePuzzle Local Bridge once in the target file.";
            nextBridgeRefreshAt = 0d;
            CheckBridgeAsync();
            Repaint();
        }

        private static void RememberFigmaFile(BridgePluginInfo plugin)
        {
            if (plugin != null && IsSafeFigmaFileKey(plugin.fileKey))
                EditorPrefs.SetString(LastFigmaFileKeyPreference, plugin.fileKey);
        }

        private static bool TryOpenRememberedFigmaFile()
        {
            string fileKey = EditorPrefs.GetString(LastFigmaFileKeyPreference, string.Empty);
            if (!IsSafeFigmaFileKey(fileKey)) return false;
            Application.OpenURL("https://www.figma.com/design/" + Uri.EscapeDataString(fileKey));
            return true;
        }

        private static bool IsSafeFigmaFileKey(string fileKey)
        {
            return !string.IsNullOrWhiteSpace(fileKey)
                   && fileKey.Length <= 128
                   && fileKey.All(character => char.IsLetterOrDigit(character) || character == '-' || character == '_');
        }

        private static bool IsFigmaDesktopRunning()
        {
            try
            {
                System.Diagnostics.Process[] processes = System.Diagnostics.Process.GetProcessesByName("Figma");
                try
                {
                    return processes.Any(process => !process.HasExited);
                }
                finally
                {
                    foreach (System.Diagnostics.Process process in processes) process.Dispose();
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool TryStartFigmaDesktop()
        {
            try
            {
#if UNITY_EDITOR_WIN
                string figmaRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Figma");
                string launcher = Path.Combine(figmaRoot, "Figma.exe");
                string executable = File.Exists(launcher)
                    ? launcher
                    : Directory.Exists(figmaRoot)
                        ? Directory.GetDirectories(figmaRoot, "app-*")
                            .Select(directory => Path.Combine(directory, "Figma.exe"))
                            .Where(File.Exists)
                            .OrderByDescending(File.GetLastWriteTimeUtc)
                            .FirstOrDefault()
                        : null;
                if (string.IsNullOrEmpty(executable)) return false;
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = executable,
                    UseShellExecute = true
                });
                return true;
#elif UNITY_EDITOR_OSX
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/usr/bin/open",
                    Arguments = "-a \"Figma\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                return true;
#else
                return false;
#endif
            }
            catch
            {
                return false;
            }
        }

        private void BuildLocalPreview()
        {
            EditorApplication.delayCall -= BuildLocalPreview;
            if (localPreviewBuildSession != null) return;
            if (exportInProgress)
            {
                localPreviewRebuildPending = true;
                return;
            }

            if (sendWhenPluginConnects)
            {
                sendWhenPluginConnects = false;
                EditorApplication.delayCall -= ExportSelectedAsync;
            }

            PruneSelections(false);
            List<GameObject> sceneRoots = selectedSceneRoots.Where(root => root != null).Distinct().ToList();
            if (sceneRoots.Count == 0)
            {
                localPreviewRebuildPending = false;
                ClearLocalPreviewCache();
                skippedSourcePaths.Clear();
                exportStatus = "Tick at least one Scene UI hierarchy before building a preview.";
                Repaint();
                return;
            }

            if (!CanBuildReviewedSceneSourceCount(sceneRoots.Count))
            {
                localPreviewRebuildPending = true;
                exportStatus = "Review batches are limited to " + MaximumReviewedSceneSources
                               + " Scene UI sources to keep Unity responsive. "
                               + "Split the " + sceneRoots.Count + " checked sources into smaller batches.";
                Repaint();
                return;
            }

            localPreviewRebuildPending = true;
            ClearLocalPreviewCache();
            DeleteDiscardedPreviewArtifacts();
            skippedSourcePaths.Clear();
            try
            {
                localPreviewBuildSession = PicturePuzzleFigmaWireframeExporter.CreateSceneExportSession(sceneRoots);
                exportStatus = "Building local Assets, Wireframe, and UI Result preview: 0/"
                               + localPreviewBuildSession.totalCount + " UI source(s)...";
                EditorApplication.update -= BuildNextLocalPreview;
                EditorApplication.update += BuildNextLocalPreview;
            }
            catch (Exception exception)
            {
                CancelLocalPreviewBuild();
                exportStatus = "Could not build local preview: " + exception.Message;
            }
            finally
            {
                Repaint();
            }
        }

        private void BuildNextLocalPreview()
        {
            PicturePuzzleFigmaWireframeExporter.SceneExportSession session = localPreviewBuildSession;
            if (session == null)
            {
                EditorApplication.update -= BuildNextLocalPreview;
                return;
            }

            try
            {
                session.ExportNext();
                if (session.isComplete)
                {
                    CompleteLocalPreviewBuild(session);
                    return;
                }

                exportStatus = "Building local Assets, Wireframe, and UI Result preview: "
                               + session.completedCount + "/" + session.totalCount + " UI source(s)...";
            }
            catch (Exception exception)
            {
                CancelLocalPreviewBuild();
                localPreviewRebuildPending = true;
                exportStatus = "Could not build local preview: " + exception.Message;
            }

            Repaint();
        }

        private void CompleteLocalPreviewBuild(PicturePuzzleFigmaWireframeExporter.SceneExportSession session)
        {
            EditorApplication.update -= BuildNextLocalPreview;
            if (!ReferenceEquals(localPreviewBuildSession, session)) return;

            localPreviewBuildSession = null;
            FigmaWireframeExportResult result = session.result;
            skippedSourcePaths.AddRange(result.skippedPaths);
            if (result.batch.items.Count == 0)
            {
                RememberDiscardedPreviewArtifacts(result);
                DeleteDiscardedPreviewArtifacts();
                localPreviewRebuildPending = true;
                exportStatus = "Nothing was exported. " + string.Join(" | ", result.skippedPaths);
                Repaint();
                return;
            }

            localPreviewExport = result;
            lastManifestPath = result.manifestPath;
            int focusedItemIndex = FindFocusedLocalPreviewItemIndex(result.batch.items);
            localPreviewItemIndex = focusedItemIndex >= 0 ? focusedItemIndex : 0;
            localAssetPreviewPage = 0;
            localPreviewStage = LocalPreviewStage.Assets;
            localPreviewRebuildPending = false;
            exportStatus = "Local preview ready for " + result.batch.items.Count
                           + " popup(s). Review each stage, then send this exact batch to Figma.";
            Repaint();
        }

        private void CancelLocalPreviewBuild()
        {
            if (localPreviewBuildSession == null) return;

            EditorApplication.update -= BuildNextLocalPreview;
            localPreviewBuildSession.Cancel();
            RememberDiscardedPreviewArtifacts(localPreviewBuildSession.result);
            localPreviewBuildSession = null;
            ScheduleDiscardedPreviewArtifactCleanup();
        }

        private int FindFocusedLocalPreviewItemIndex(List<FigmaWireframeItem> items)
        {
            if (items == null || items.Count == 0) return -1;
            if (previewSceneRoot == null) return -1;
            string scenePath = previewSceneRoot.scene.path;
            string hierarchyPath = PicturePuzzleFigmaWireframeExporter.GetSceneHierarchyPath(previewSceneRoot);
            return items.FindIndex(item => item != null
                && string.Equals(item.sourceKind, "scene-hierarchy", StringComparison.Ordinal)
                && string.Equals(item.assetPath, scenePath, StringComparison.Ordinal)
                && string.Equals(item.hierarchyPath, hierarchyPath, StringComparison.Ordinal));
        }

        private async void ExportSelectedAsync()
        {
            if (exportInProgress) return;
            PruneSelections();
            if (localPreviewRebuildPending)
            {
                exportStatus = "The checked UI selection changed. Build and review the updated local preview before sending.";
                Repaint();
                return;
            }
            if (localPreviewExport == null
                || localPreviewExport.batch == null
                || localPreviewExport.batch.items == null
                || localPreviewExport.batch.items.Count == 0)
            {
                exportStatus = "Build and review Assets, Wireframe, and UI Result before sending to Figma.";
                Repaint();
                return;
            }

            FigmaWireframeExportResult result = localPreviewExport;
            string artifactError;
            if (!TryValidateLocalPreviewArtifacts(result, out artifactError))
            {
                exportStatus = artifactError + " Rebuild and review the local preview before sending.";
                Repaint();
                return;
            }

            exportInProgress = true;
            string requestedPluginId = selectedPluginId;
            await RefreshBridgeAsync();
            if (!IsReviewedBatchCurrent(result, out artifactError))
            {
                exportStatus = artifactError + " The stale batch was not sent; review the refreshed local preview.";
                FinishExportOperation();
                return;
            }

            BridgePluginInfo[] activePlugins = GetActivePlugins();
            if (!string.IsNullOrEmpty(requestedPluginId)
                && activePlugins.Length > 0
                && activePlugins.All(plugin => plugin.id != requestedPluginId))
            {
                exportStatus = "The previously selected Figma file disconnected. Confirm the destination dropdown, then press Send again.";
                FinishExportOperation();
                return;
            }

            bool pluginConnected = bridgeHealth != null
                                   && bridgeHealth.pluginConnected
                                   && activePlugins.Length > 0;
            if (!pluginConnected)
            {
                exportInProgress = false;
                sendWhenPluginConnects = true;
                exportStatus = "Waiting for Figma. Run 'PicturePuzzle Local Bridge' in the target Figma file; this send continues automatically.";
                BeginAutomaticFigmaConnection(true);
                if (!EditorPrefs.GetBool("PicturePuzzle.Figma.PluginSeen", false)) ShowFigmaPluginSetup();
                CheckBridgeAsync();
                Repaint();
                return;
            }

            exportStatus = "Sending the reviewed preview to " + GetSelectedPluginLabel() + "...";
            Repaint();

            try
            {
                var request = new BridgeEnqueueRequest
                {
                    type = "wireframe-batch",
                    name = "Unity UGUI to Figma",
                    targetPluginId = GetSelectedPluginId(),
                    batch = result.batch
                };
                string payload = JsonUtility.ToJson(request);
                // Treat a network timeout as ambiguous: the bridge may already have queued this batch.
                // Keeping its artifacts is safer than deleting images Figma still needs.
                submittedBatchIds.Add(result.batch.batchId);
                string response = await BridgeRequest.PostJson(BridgeUrl + "/enqueue", payload);
                BridgeEnqueueInfo enqueue = JsonUtility.FromJson<BridgeEnqueueInfo>(response);
                if (enqueue == null)
                    throw new InvalidOperationException("The bridge did not return a command id.");
                if (!enqueue.ok)
                {
                    submittedBatchIds.Remove(result.batch.batchId);
                    throw new InvalidOperationException("The bridge did not return a command id.");
                }
                if (enqueue.command == null || string.IsNullOrEmpty(enqueue.command.id))
                    throw new InvalidOperationException("The bridge did not return a command id.");

                exportStatus = "Converting " + result.batch.items.Count + " UI source(s) in " + GetSelectedPluginLabel() + "...";
                lastQueueUrl = BridgeUrl + "/results";
                await WaitForFigmaResultAsync(enqueue.command.id, result.batch.items.Count, result.batch.batchId);
            }
            catch (Exception exception)
            {
                exportStatus = "Manifest and reference images were created, but queueing failed: " + exception.Message;
                FinishExportOperation();
            }
            Repaint();
        }

        private bool IsReviewedBatchCurrent(FigmaWireframeExportResult result, out string error)
        {
            if (localPreviewRebuildPending || !ReferenceEquals(localPreviewExport, result))
            {
                error = "The checked UI selection changed while preparing the Figma send.";
                return false;
            }

            var selectedSceneKeys = new HashSet<string>(
                selectedSceneRoots
                    .Where(root => root != null)
                    .Select(root => root.scene.path + "\n" + PicturePuzzleFigmaWireframeExporter.GetSceneHierarchyPath(root)),
                StringComparer.Ordinal);
            List<FigmaWireframeItem> reviewedItems = result != null && result.batch != null
                ? result.batch.items
                : null;
            if (reviewedItems == null
                || reviewedItems.Count != selectedSceneKeys.Count
                || reviewedItems.Any(item => item == null
                    || !string.Equals(item.sourceKind, "scene-hierarchy", StringComparison.Ordinal)
                    || !selectedSceneKeys.Contains(item.assetPath + "\n" + item.hierarchyPath)))
            {
                error = "The reviewed batch no longer matches the checked Scene UI GameObjects.";
                return false;
            }

            return TryValidateLocalPreviewArtifacts(result, out error);
        }

        private void FinishExportOperation()
        {
            exportInProgress = false;
            if (localPreviewRebuildPending)
                ScheduleCurrentSelectionPreview();
            else if (windowActive)
                Repaint();
        }

        private string GetSelectedPluginId()
        {
            BridgePluginInfo[] plugins = GetActivePlugins();
            if (plugins.Length == 0) return string.Empty;
            SelectCurrentPlugin(plugins);
            return selectedPluginId;
        }

        private string GetSelectedPluginLabel()
        {
            BridgePluginInfo[] plugins = GetActivePlugins();
            if (plugins.Length == 0)
                return string.IsNullOrWhiteSpace(selectedPluginLabel) ? "Figma" : selectedPluginLabel;
            SelectCurrentPlugin(plugins);
            selectedPluginLabel = FormatPluginLabel(plugins[selectedPluginIndex]);
            return selectedPluginLabel;
        }

        private BridgePluginInfo[] GetActivePlugins()
        {
            return bridgeHealth != null && bridgeHealth.plugins != null
                ? bridgeHealth.plugins
                    .Where(plugin => plugin != null && !string.IsNullOrEmpty(plugin.id))
                    .ToArray()
                : Array.Empty<BridgePluginInfo>();
        }

        private static string FormatPluginLabel(BridgePluginInfo plugin)
        {
            if (plugin == null) return "Unknown Figma file";
            string fileName = string.IsNullOrWhiteSpace(plugin.fileName) ? "Untitled Figma file" : plugin.fileName;
            string pageName = string.IsNullOrWhiteSpace(plugin.pageName) ? "Untitled page" : plugin.pageName;
            return fileName + "  •  " + pageName;
        }

        private void SelectCurrentPlugin(BridgePluginInfo[] plugins)
        {
            if (plugins == null || plugins.Length == 0)
            {
                selectedPluginIndex = 0;
                return;
            }

            int matchingIndex = Array.FindIndex(plugins, plugin => plugin != null && plugin.id == selectedPluginId);
            selectedPluginIndex = matchingIndex >= 0 ? matchingIndex : 0;
            selectedPluginId = plugins[selectedPluginIndex].id;
            selectedPluginLabel = FormatPluginLabel(plugins[selectedPluginIndex]);
        }

        private async Task WaitForFigmaResultAsync(string commandId, int exportedCount, string batchId)
        {
            DateTime deadline = DateTime.UtcNow.AddMinutes(2);
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(800);
                if (!windowActive)
                {
                    exportInProgress = false;
                    localPreviewRebuildPending = false;
                    return;
                }
                string response = await BridgeRequest.Get(BridgeUrl + "/results");
                BridgeResultsInfo state = JsonUtility.FromJson<BridgeResultsInfo>(response);
                BridgeResultInfo result = state != null && state.results != null
                    ? state.results.FirstOrDefault(entry => entry.id == commandId)
                    : null;
                if (result == null) continue;

                // The plugin has posted its final result, so it will not fetch this batch's images again.
                submittedBatchIds.Remove(batchId);
                DeleteDiscardedPreviewArtifacts();

                if (result.ok)
                {
                    string pageName = result.result != null && !string.IsNullOrEmpty(result.result.pageName)
                        ? result.result.pageName
                        : GetSelectedPluginLabel();
                    int count = result.result != null && result.result.exported > 0 ? result.result.exported : exportedCount;
                    exportStatus = "Done - " + count + " UI source(s) updated across the 3 Figma pages. Opened '" + pageName + "'.";
                }
                else
                {
                    exportStatus = "Figma could not create the UI: " + (string.IsNullOrEmpty(result.error) ? "Unknown plugin error." : result.error);
                }
                FinishExportOperation();
                return;
            }

            exportStatus = "Figma is still processing or disconnected. The batch remains in Queue results; do not resend unless it failed.";
            FinishExportOperation();
        }

        private static class BridgeRequest
        {
            public static async Task<string> Get(string url)
            {
                using (var request = UnityWebRequest.Get(url))
                    return await Send(request);
            }

            public static async Task<string> PostJson(string url, string json)
            {
                using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    return await Send(request);
                }
            }

            private static Task<string> Send(UnityWebRequest request)
            {
                var completion = new TaskCompletionSource<string>();
                request.timeout = 5;
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                operation.completed += _ =>
                {
                    if (request.result == UnityWebRequest.Result.Success)
                        completion.TrySetResult(request.downloadHandler.text);
                    else
                        completion.TrySetException(new WebException(request.error + " (HTTP " + request.responseCode + ")"));
                };
                return completion.Task;
            }
        }
    }
}
#endif
