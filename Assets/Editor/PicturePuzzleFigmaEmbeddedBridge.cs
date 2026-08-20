#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace PicturePuzzle.EditorTools
{
    public enum PicturePuzzleFigmaBridgeMode
    {
        Unavailable,
        Embedded,
        External
    }

    /// <summary>
    /// Small localhost-only HTTP server implementing the protocol consumed by the Figma
    /// development plugin. It intentionally accepts constrained wireframe data and only serves
    /// images registered from the project's export directory.
    /// </summary>
    public sealed class PicturePuzzleFigmaBridgeServer : IDisposable
    {
        private const int MaxRequestCharacters = 32 * 1024 * 1024;
        private static readonly TimeSpan PluginHeartbeatLifetime = TimeSpan.FromSeconds(15);
        private static readonly Regex SafeId = new Regex("^[A-Za-z0-9][A-Za-z0-9_-]{0,127}$", RegexOptions.Compiled);
        private static readonly HashSet<string> WireframeRoles = new HashSet<string>(StringComparer.Ordinal)
        {
            "container", "image", "raw-image", "text", "button", "toggle", "slider", "scroll"
        };
        private static readonly HashSet<string> SourceKinds = new HashSet<string>(StringComparer.Ordinal)
        {
            "prefab", "scene-hierarchy"
        };

        private sealed class PluginHeartbeat
        {
            public string id;
            public string fileName;
            public string pageName;
            public string fileKey;
            public DateTime lastSeenUtc;
        }

        private readonly object stateLock = new object();
        private readonly List<JObject> queue = new List<JObject>();
        private readonly Dictionary<string, JObject> inFlight = new Dictionary<string, JObject>(StringComparer.Ordinal);
        private readonly List<JObject> results = new List<JObject>();
        private readonly Dictionary<string, PluginHeartbeat> plugins = new Dictionary<string, PluginHeartbeat>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> artifacts = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Func<DateTime> utcNow;
        private readonly string exportRoot;
        private readonly string spriteRoot;
        private readonly string prefix;
        private readonly string pluginPrefix;
        private HttpListener listener;
        private CancellationTokenSource cancellation;
        private Task listenerTask;

        public PicturePuzzleFigmaBridgeServer(string projectRoot, int port = 3907, Func<DateTime> utcNow = null)
        {
            if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException("Project root is required.", nameof(projectRoot));
            if (port <= 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));

            string normalizedProjectRoot = Path.GetFullPath(projectRoot);
            exportRoot = Path.GetFullPath(Path.Combine(normalizedProjectRoot, PicturePuzzleFigmaWireframeExporter.ExportRootRelative));
            spriteRoot = Path.GetFullPath(Path.Combine(normalizedProjectRoot, "tools", "design", "figma", "sprites"));
            prefix = "http://127.0.0.1:" + port + "/";
            pluginPrefix = "http://localhost:" + port + "/";
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public string BaseUrl => prefix.TrimEnd('/');
        public string LastError { get; private set; }
        public bool IsRunning => listener != null && listener.IsListening;

        public bool Start()
        {
            if (IsRunning) return true;
            Stop();

            try
            {
                listener = new HttpListener();
                listener.Prefixes.Add(prefix);
                listener.Prefixes.Add(pluginPrefix);
                listener.Start();
                cancellation = new CancellationTokenSource();
                HttpListener activeListener = listener;
                CancellationToken token = cancellation.Token;
                listenerTask = Task.Run(() => ListenAsync(activeListener, token), token);
                LastError = null;
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                Stop();
                return false;
            }
        }

        public void Stop()
        {
            if (cancellation != null)
            {
                cancellation.Cancel();
                cancellation.Dispose();
                cancellation = null;
            }

            if (listener != null)
            {
                try
                {
                    listener.Close();
                }
                catch
                {
                    // Domain reload can close the native listener first.
                }
                listener = null;
            }
            listenerTask = null;
        }

        public void Dispose()
        {
            Stop();
        }

        public static void ValidateWireframeBatch(JObject batch)
        {
            Require(batch != null, "batch must be an object");
            int schemaVersion = IntegerValue(batch["schemaVersion"]);
            Require(schemaVersion == 1 || schemaVersion == 2, "batch.schemaVersion must be 1 or 2");
            RequireSafeId(ValueString(batch["batchId"]), "batch.batchId");

            JObject canvas = batch["canvas"] as JObject;
            Require(canvas != null
                    && IntegerValue(canvas["width"]) == PicturePuzzleFigmaWireframeExporter.CanvasWidth
                    && IntegerValue(canvas["height"]) == PicturePuzzleFigmaWireframeExporter.CanvasHeight,
                "batch.canvas must be 1080x1920");

            JArray items = batch["items"] as JArray;
            Require(items != null && items.Count > 0 && items.Count <= 100, "batch.items must contain 1 to 100 items");
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            var artifactIds = new HashSet<string>(StringComparer.Ordinal);
            for (int itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                string label = "batch.items[" + itemIndex + "]";
                JObject item = items[itemIndex] as JObject;
                Require(item != null, label + " must be an object");

                string itemId = ValueString(item["itemId"]);
                RequireSafeId(itemId, label + ".itemId");
                Require(itemIds.Add(itemId), label + ".itemId must be unique");

                string sourceGuid = ValueString(item["sourceGuid"]);
                Require(!string.IsNullOrEmpty(sourceGuid) && sourceGuid.Length <= 128, label + ".sourceGuid is required");
                JToken sourcePrefabGuidToken = item["sourcePrefabGuid"];
                Require(sourcePrefabGuidToken == null
                        || sourcePrefabGuidToken.Type == JTokenType.Null
                        || sourcePrefabGuidToken.Type == JTokenType.String,
                    label + ".sourcePrefabGuid must be a safe identifier");
                string sourcePrefabGuid = ValueString(sourcePrefabGuidToken);
                if (!string.IsNullOrEmpty(sourcePrefabGuid))
                    RequireSafeId(sourcePrefabGuid, label + ".sourcePrefabGuid");
                string sourceKind = ValueString(item["sourceKind"]);
                Require(string.IsNullOrEmpty(sourceKind) || SourceKinds.Contains(sourceKind), label + ".sourceKind is unsupported");

                string assetPath = ValueString(item["assetPath"]);
                Require(!string.IsNullOrEmpty(assetPath)
                        && assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                        && assetPath.IndexOf("..", StringComparison.Ordinal) < 0,
                    label + ".assetPath must be an Assets path");

                RequireOptionalString(item["hierarchyPath"], 2000, label + ".hierarchyPath must be a string up to 2000 characters");
                string displayName = ValueString(item["displayName"]);
                Require(!string.IsNullOrEmpty(displayName) && displayName.Length <= 256, label + ".displayName is required");

                string referenceImageId = ValueString(item["referenceImageId"]);
                Require(string.IsNullOrEmpty(referenceImageId) || referenceImageId == itemId,
                    label + ".referenceImageId must match itemId");
                if (!string.IsNullOrEmpty(referenceImageId))
                    Require(artifactIds.Add(referenceImageId), label + ".referenceImageId must be unique across the batch");

                JArray nodes = item["nodes"] as JArray;
                Require(nodes != null && nodes.Count > 0 && nodes.Count <= 2500, label + ".nodes must contain 1 to 2500 nodes");
                JArray warnings = item["warnings"] as JArray;
                Require(warnings != null, label + ".warnings must be an array");
                foreach (JToken warning in warnings)
                    Require(warning.Type == JTokenType.String && warning.Value<string>().Length <= 1000,
                        label + ".warnings contains an invalid entry");

                var nodeIds = new HashSet<string>(StringComparer.Ordinal);
                for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
                {
                    string nodeLabel = label + ".nodes[" + nodeIndex + "]";
                    JObject node = nodes[nodeIndex] as JObject;
                    Require(node != null, nodeLabel + " must be an object");

                    string nodeId = ValueString(node["id"]);
                    RequireSafeId(nodeId, nodeLabel + ".id");
                    Require(nodeIds.Add(nodeId), nodeLabel + ".id must be unique");
                    string parentId = ValueString(node["parentId"]);
                    Require(string.IsNullOrEmpty(parentId) || SafeId.IsMatch(parentId), nodeLabel + ".parentId must be a safe identifier");
                    if (!string.IsNullOrEmpty(parentId))
                        Require(nodeIds.Contains(parentId), nodeLabel + ".parentId must reference an earlier node");

                    RequireOptionalString(node["name"], 256, nodeLabel + ".name must be a string up to 256 characters", false);
                    string role = ValueString(node["role"]);
                    Require(WireframeRoles.Contains(role), nodeLabel + ".role is unsupported");
                    ValidateRect(node["rect"] as JObject, nodeLabel);
                    RequireOptionalString(node["text"], 5000, nodeLabel + ".text must be a string up to 5000 characters");
                    ValidateColor(node["color"], nodeLabel);
                    if (schemaVersion >= 2)
                    {
                        int siblingIndex = IntegerValue(node["siblingIndex"]);
                        Require(siblingIndex >= 0, nodeLabel + ".siblingIndex must be a non-negative integer");
                        int renderOrder = IntegerValue(node["renderOrder"]);
                        Require(renderOrder >= 0, nodeLabel + ".renderOrder must be a non-negative integer");
                        string visualImageId = ValueString(node["visualImageId"]);
                        Require(string.IsNullOrEmpty(visualImageId) || visualImageId == nodeId,
                            nodeLabel + ".visualImageId must match node.id");
                        if (!string.IsNullOrEmpty(visualImageId))
                        {
                            Require(artifactIds.Add(visualImageId), nodeLabel + ".visualImageId must be unique across the batch");
                            ValidateRect(node["visualRect"] as JObject, nodeLabel + ".visualRect");
                        }
                        string assetImageId = ValueString(node["assetImageId"]);
                        if (!string.IsNullOrEmpty(assetImageId))
                        {
                            RequireSafeId(assetImageId, nodeLabel + ".assetImageId");
                            Require(artifactIds.Add(assetImageId), nodeLabel + ".assetImageId must be unique across the batch");
                            ValidateRect(node["assetRect"] as JObject, nodeLabel + ".assetRect");
                        }
                        string nestedPrefabPath = ValueString(node["nestedPrefabPath"]);
                        Require(string.IsNullOrEmpty(nestedPrefabPath)
                                || (nestedPrefabPath.Length <= 2000
                                    && nestedPrefabPath.StartsWith("Assets/", StringComparison.Ordinal)
                                    && nestedPrefabPath.IndexOf("..", StringComparison.Ordinal) < 0),
                            nodeLabel + ".nestedPrefabPath must be an Assets path");
                        Require(node["includeInAssetReview"] == null || node["includeInAssetReview"].Type == JTokenType.Boolean,
                            nodeLabel + ".includeInAssetReview must be a boolean");
                        Require(node["clipsContent"] != null && node["clipsContent"].Type == JTokenType.Boolean,
                            nodeLabel + ".clipsContent must be a boolean");
                        RequireFiniteNumber(node["opacity"], nodeLabel + ".opacity must be between 0 and 1");
                        double opacity = NumberValue(node["opacity"]);
                        Require(opacity >= 0d && opacity <= 1d, nodeLabel + ".opacity must be between 0 and 1");
                        if (role == "text")
                        {
                            Require(node["textStyle"] != null && node["textStyle"].Type != JTokenType.Null,
                                nodeLabel + ".textStyle is required for text nodes");
                            ValidateTextStyle(node["textStyle"], nodeLabel);
                        }
                    }
                }
            }
        }

        private async Task ListenAsync(HttpListener activeListener, CancellationToken token)
        {
            while (!token.IsCancellationRequested && activeListener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await activeListener.GetContextAsync();
                }
                catch (Exception exception) when (token.IsCancellationRequested
                                                   || exception is ObjectDisposedException
                                                   || exception is HttpListenerException)
                {
                    break;
                }

                _ = Task.Run(() => HandleRequestAsync(context), token);
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                AddCorsHeaders(context.Response);
                if (context.Request.HttpMethod == "OPTIONS")
                {
                    await SendJsonAsync(context.Response, 200, new JObject { ["ok"] = true });
                    return;
                }

                string path = context.Request.Url.AbsolutePath;
                if (context.Request.HttpMethod == "GET" && path == "/health")
                {
                    await SendJsonAsync(context.Response, 200, BuildHealth());
                    return;
                }

                if (context.Request.HttpMethod == "POST" && path == "/heartbeat")
                {
                    JObject body = await ReadJsonAsync(context.Request);
                    string pluginId = ValueString(body["pluginId"]);
                    RequireSafeId(pluginId, "pluginId");
                    string fileName = ValueString(body["fileName"]);
                    string pageName = ValueString(body["pageName"]);
                    string fileKey = ValueString(body["fileKey"]) ?? string.Empty;
                    Require(fileName != null && fileName.Length <= 256, "fileName is required");
                    Require(pageName != null && pageName.Length <= 256, "pageName is required");
                    Require(fileKey.Length == 0 || SafeId.IsMatch(fileKey), "fileKey is invalid");

                    var heartbeat = new PluginHeartbeat
                    {
                        id = pluginId,
                        fileName = fileName,
                        pageName = pageName,
                        fileKey = fileKey,
                        lastSeenUtc = utcNow()
                    };
                    lock (stateLock) plugins[pluginId] = heartbeat;
                    await SendJsonAsync(context.Response, 200, new JObject
                    {
                        ["ok"] = true,
                        ["plugin"] = PluginJson(heartbeat, false)
                    });
                    return;
                }

                if (context.Request.HttpMethod == "GET" && path == "/sprite")
                {
                    string requested = context.Request.QueryString["path"] ?? string.Empty;
                    string resolved = Path.GetFullPath(Path.Combine(spriteRoot, requested));
                    if (!IsWithin(spriteRoot, resolved))
                    {
                        await SendJsonAsync(context.Response, 403, Error("Sprite path outside allowed root"));
                        return;
                    }
                    await SendBytesAsync(context.Response, 200, File.ReadAllBytes(resolved), ContentTypeFor(resolved));
                    return;
                }

                if (context.Request.HttpMethod == "GET" && path == "/export-image")
                {
                    string batchId = context.Request.QueryString["batchId"] ?? string.Empty;
                    string itemId = context.Request.QueryString["itemId"] ?? string.Empty;
                    RequireSafeId(batchId, "batchId");
                    RequireSafeId(itemId, "itemId");
                    string resolved;
                    lock (stateLock) artifacts.TryGetValue(batchId + "/" + itemId, out resolved);
                    if (string.IsNullOrEmpty(resolved))
                    {
                        await SendJsonAsync(context.Response, 404, Error("Export artifact is not registered"));
                        return;
                    }
                    if (!IsWithin(exportRoot, resolved))
                    {
                        await SendJsonAsync(context.Response, 403, Error("Export image path outside allowed root"));
                        return;
                    }
                    if (!File.Exists(resolved))
                    {
                        await SendJsonAsync(context.Response, 404, Error("Export artifact does not exist"));
                        return;
                    }
                    await SendBytesAsync(context.Response, 200, File.ReadAllBytes(resolved), "image/png");
                    return;
                }

                if (context.Request.HttpMethod == "GET" && path == "/next")
                {
                    JObject command = null;
                    string requestingPluginId = context.Request.QueryString["pluginId"] ?? string.Empty;
                    if (!string.IsNullOrEmpty(requestingPluginId)) RequireSafeId(requestingPluginId, "pluginId");
                    lock (stateLock)
                    {
                        if (queue.Count > 0)
                        {
                            int commandIndex = string.IsNullOrEmpty(requestingPluginId)
                                ? 0
                                : queue.FindIndex(candidate => string.IsNullOrEmpty(ValueString(candidate["targetPluginId"]))
                                                              || ValueString(candidate["targetPluginId"]) == requestingPluginId);
                            if (commandIndex >= 0)
                            {
                                command = queue[commandIndex];
                                queue.RemoveAt(commandIndex);
                                command["startedAt"] = IsoNow();
                                inFlight[ValueString(command["id"])] = command;
                            }
                        }
                    }
                    await SendJsonAsync(context.Response, 200, command ?? new JObject { ["type"] = "idle" });
                    return;
                }

                if (context.Request.HttpMethod == "POST" && path == "/enqueue")
                {
                    JObject body = await ReadJsonAsync(context.Request);
                    JObject response = Enqueue(body);
                    await SendJsonAsync(context.Response, 200, response);
                    return;
                }

                if (context.Request.HttpMethod == "POST" && path == "/result")
                {
                    JObject body = await ReadJsonAsync(context.Request);
                    JObject result = StoreResult(body);
                    await SendJsonAsync(context.Response, 200, new JObject
                    {
                        ["ok"] = true,
                        ["result"] = result
                    });
                    return;
                }

                if (context.Request.HttpMethod == "GET" && path == "/results")
                {
                    await SendJsonAsync(context.Response, 200, BuildResults());
                    return;
                }

                await SendJsonAsync(context.Response, 404, Error("Not found"));
            }
            catch (Exception exception)
            {
                try
                {
                    await SendJsonAsync(context.Response, 400, Error(exception.Message));
                }
                catch
                {
                    TryClose(context.Response);
                }
            }
        }

        private JObject Enqueue(JObject body)
        {
            string type = ValueString(body["type"]);
            Require(!string.IsNullOrEmpty(type), "Missing command type");
            if (type == "wireframe-batch")
                ValidateWireframeBatch(body["batch"] as JObject);
            else if (type == "eval")
                Require(!string.IsNullOrEmpty(ValueString(body["code"])), "Legacy eval command is missing code");
            else
                throw new InvalidOperationException("Unsupported command type");

            string requestedId = ValueString(body["id"]);
            string targetPluginId = ValueString(body["targetPluginId"]);
            if (!string.IsNullOrEmpty(targetPluginId)) RequireSafeId(targetPluginId, "targetPluginId");
            string commandId = !string.IsNullOrEmpty(requestedId) && SafeId.IsMatch(requestedId)
                ? requestedId
                : Guid.NewGuid().ToString("N");
            var command = new JObject
            {
                ["id"] = commandId,
                ["name"] = ValueString(body["name"]) ?? type,
                ["type"] = type,
                ["targetPluginId"] = targetPluginId ?? string.Empty,
                ["enqueuedAt"] = IsoNow()
            };
            if (type == "wireframe-batch") command["batch"] = body["batch"].DeepClone();
            if (type == "eval") command["code"] = ValueString(body["code"]);

            lock (stateLock)
            {
                if (type == "wireframe-batch") RegisterArtifacts((JObject)command["batch"]);
                queue.Add(command);
                return new JObject
                {
                    ["ok"] = true,
                    ["command"] = new JObject
                    {
                        ["id"] = commandId,
                        ["name"] = ValueString(command["name"]),
                        ["type"] = type
                    },
                    ["queue"] = queue.Count
                };
            }
        }

        private void RegisterArtifacts(JObject batch)
        {
            string batchId = ValueString(batch["batchId"]);
            foreach (JObject item in ((JArray)batch["items"]).OfType<JObject>())
            {
                string referenceImageId = ValueString(item["referenceImageId"]);
                if (!string.IsNullOrEmpty(referenceImageId))
                {
                    string imagePath = Path.GetFullPath(Path.Combine(exportRoot, batchId, referenceImageId + ".png"));
                    Require(IsWithin(exportRoot, imagePath), "Export artifact path is outside the allowed root");
                    artifacts[batchId + "/" + referenceImageId] = imagePath;
                }

                foreach (JObject node in (item["nodes"] as JArray ?? new JArray()).OfType<JObject>())
                {
                    foreach (string artifactId in new[]
                             {
                                 ValueString(node["visualImageId"]),
                                 ValueString(node["assetImageId"])
                             })
                    {
                        if (string.IsNullOrEmpty(artifactId)) continue;
                        string artifactPath = Path.GetFullPath(Path.Combine(exportRoot, batchId, artifactId + ".png"));
                        Require(IsWithin(exportRoot, artifactPath), "Node artifact path is outside the allowed root");
                        artifacts[batchId + "/" + artifactId] = artifactPath;
                    }
                }
            }
        }

        private JObject StoreResult(JObject body)
        {
            string id = ValueString(body["id"]) ?? "unknown";
            JObject command = null;
            lock (stateLock)
            {
                inFlight.TryGetValue(id, out command);
                inFlight.Remove(id);
                var result = new JObject
                {
                    ["id"] = id,
                    ["name"] = command == null ? "unknown" : ValueString(command["name"]),
                    ["ok"] = body.Value<bool?>("ok") ?? false,
                    ["result"] = body["result"] == null ? JValue.CreateNull() : body["result"].DeepClone(),
                    ["error"] = body["error"] == null ? JValue.CreateNull() : body["error"].DeepClone(),
                    ["completedAt"] = IsoNow()
                };
                results.Insert(0, result);
                if (results.Count > 50) results.RemoveRange(50, results.Count - 50);
                return result;
            }
        }

        private JObject BuildHealth()
        {
            lock (stateLock)
            {
                DateTime threshold = utcNow() - PluginHeartbeatLifetime;
                foreach (string staleId in plugins.Where(pair => pair.Value.lastSeenUtc < threshold).Select(pair => pair.Key).ToList())
                    plugins.Remove(staleId);
                List<PluginHeartbeat> active = plugins.Values.OrderByDescending(plugin => plugin.lastSeenUtc).ToList();
                return new JObject
                {
                    ["ok"] = true,
                    ["protocol"] = "picturepuzzle-figma-bridge",
                    ["protocolVersion"] = 1,
                    ["queue"] = queue.Count,
                    ["inFlight"] = inFlight.Count,
                    ["results"] = results.Count,
                    ["pluginConnected"] = active.Count > 0,
                    ["plugins"] = new JArray(active.Select(plugin => PluginJson(plugin, true)))
                };
            }
        }

        private JObject BuildResults()
        {
            lock (stateLock)
            {
                return new JObject
                {
                    ["ok"] = true,
                    ["results"] = new JArray(results.Select(result => result.DeepClone())),
                    ["queue"] = new JArray(queue.Select(command => CommandSummary(command, "enqueuedAt"))),
                    ["inFlight"] = new JArray(inFlight.Values.Select(command => CommandSummary(command, "startedAt")))
                };
            }
        }

        private static JObject CommandSummary(JObject command, string timestampName)
        {
            return new JObject
            {
                ["id"] = command["id"],
                ["name"] = command["name"],
                ["type"] = command["type"],
                [timestampName] = command[timestampName]
            };
        }

        private static JObject PluginJson(PluginHeartbeat plugin, bool includeTimestamp)
        {
            var json = new JObject
            {
                ["id"] = plugin.id,
                ["fileName"] = plugin.fileName,
                ["pageName"] = plugin.pageName,
                ["fileKey"] = plugin.fileKey
            };
            if (includeTimestamp) json["lastSeenAt"] = plugin.lastSeenUtc.ToString("o");
            return json;
        }

        private string IsoNow()
        {
            return utcNow().ToString("o");
        }

        private static async Task<JObject> ReadJsonAsync(HttpListenerRequest request)
        {
            if (request.ContentLength64 > MaxRequestCharacters)
                throw new InvalidOperationException("Request body is too large");

            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
            {
                string raw = await reader.ReadToEndAsync();
                if (raw.Length > MaxRequestCharacters) throw new InvalidOperationException("Request body is too large");
                return string.IsNullOrWhiteSpace(raw) ? new JObject() : JObject.Parse(raw);
            }
        }

        private static async Task SendJsonAsync(HttpListenerResponse response, int statusCode, JToken body)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body.ToString(Formatting.Indented));
            await SendBytesAsync(response, statusCode, bytes, "application/json; charset=utf-8");
        }

        private static async Task SendBytesAsync(HttpListenerResponse response, int statusCode, byte[] bytes, string contentType)
        {
            AddCorsHeaders(response);
            response.StatusCode = statusCode;
            response.ContentType = contentType;
            response.ContentLength64 = bytes.LongLength;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            TryClose(response);
        }

        private static void AddCorsHeaders(HttpListenerResponse response)
        {
            response.Headers["Access-Control-Allow-Origin"] = "*";
            response.Headers["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS";
            response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
        }

        private static void TryClose(HttpListenerResponse response)
        {
            try
            {
                response.OutputStream.Close();
                response.Close();
            }
            catch
            {
                // The caller can close a Figma fetch while scripts are reloading.
            }
        }

        private static JObject Error(string message)
        {
            return new JObject { ["ok"] = false, ["error"] = message ?? "Unknown error" };
        }

        private static string ContentTypeFor(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".jpg" || extension == ".jpeg") return "image/jpeg";
            if (extension == ".webp") return "image/webp";
            return "image/png";
        }

        private static bool IsWithin(string root, string candidate)
        {
            try
            {
                string rootWithoutSeparator = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string normalizedRoot = rootWithoutSeparator + Path.DirectorySeparatorChar;
                string normalizedCandidate = Path.GetFullPath(candidate);
                if (!normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)) return false;
                if (IsReparsePoint(rootWithoutSeparator)) return false;

                string current = rootWithoutSeparator;
                string relative = normalizedCandidate.Substring(normalizedRoot.Length);
                foreach (string segment in relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
                {
                    current = Path.Combine(current, segment);
                    if (IsReparsePoint(current)) return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsReparsePoint(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return false;
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }

        private static void ValidateRect(JObject rect, string label)
        {
            Require(rect != null, label + ".rect is required");
            foreach (string key in new[] { "x", "y", "width", "height" })
                RequireFiniteNumber(rect[key], label + ".rect." + key + " must be a finite number");
            Require(NumberValue(rect["width"]) >= 0d && NumberValue(rect["height"]) >= 0d,
                label + ".rect dimensions cannot be negative");
        }

        private static void ValidateColor(JToken colorToken, string label)
        {
            if (colorToken == null || colorToken.Type == JTokenType.Null) return;
            JObject color = colorToken as JObject;
            Require(color != null, label + ".color must be an object");
            foreach (string key in new[] { "r", "g", "b", "a" })
            {
                RequireFiniteNumber(color[key], label + ".color." + key + " must be between 0 and 1");
                double value = NumberValue(color[key]);
                Require(value >= 0d && value <= 1d, label + ".color." + key + " must be between 0 and 1");
            }
        }

        private static void ValidateTextStyle(JToken styleToken, string label)
        {
            if (styleToken == null || styleToken.Type == JTokenType.Null) return;
            JObject style = styleToken as JObject;
            Require(style != null, label + ".textStyle must be an object");
            RequireOptionalString(style["fontFamily"], 256, label + ".textStyle.fontFamily is invalid", false);
            RequireOptionalString(style["fontStyle"], 128, label + ".textStyle.fontStyle is invalid", false);
            foreach (string key in new[] { "fontSize", "lineHeight", "letterSpacing" })
                RequireFiniteNumber(style[key], label + ".textStyle." + key + " must be a finite number");
            Require(NumberValue(style["fontSize"]) > 0d && NumberValue(style["fontSize"]) <= 1000d,
                label + ".textStyle.fontSize is invalid");
            Require(NumberValue(style["lineHeight"]) > 0d && NumberValue(style["lineHeight"]) <= 5000d,
                label + ".textStyle.lineHeight is invalid");
            Require(Math.Abs(NumberValue(style["letterSpacing"])) <= 1000d,
                label + ".textStyle.letterSpacing is invalid");
            string horizontal = ValueString(style["horizontalAlignment"]);
            string vertical = ValueString(style["verticalAlignment"]);
            Require(new[] { "left", "center", "right", "justified" }.Contains(horizontal),
                label + ".textStyle.horizontalAlignment is invalid");
            Require(new[] { "top", "center", "bottom" }.Contains(vertical),
                label + ".textStyle.verticalAlignment is invalid");
        }

        private static void RequireFiniteNumber(JToken token, string message)
        {
            Require(token != null && (token.Type == JTokenType.Integer || token.Type == JTokenType.Float), message);
            double value = NumberValue(token);
            Require(!double.IsNaN(value) && !double.IsInfinity(value), message);
        }

        private static double NumberValue(JToken token)
        {
            return token == null ? double.NaN : token.Value<double>();
        }

        private static int IntegerValue(JToken token)
        {
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) return int.MinValue;
            double value = token.Value<double>();
            if (double.IsNaN(value)
                || double.IsInfinity(value)
                || value < int.MinValue
                || value > int.MaxValue
                || value != Math.Truncate(value)) return int.MinValue;
            return (int)value;
        }

        private static void RequireInteger(JToken token, int expected, string message)
        {
            Require(IntegerValue(token) == expected, message);
        }

        private static string ValueString(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? null : token.Type == JTokenType.String ? token.Value<string>() : null;
        }

        private static void RequireOptionalString(JToken token, int maxLength, string message, bool allowNull = true)
        {
            if (allowNull && (token == null || token.Type == JTokenType.Null)) return;
            Require(token != null && token.Type == JTokenType.String && token.Value<string>().Length <= maxLength, message);
        }

        private static void RequireSafeId(string value, string label)
        {
            Require(!string.IsNullOrEmpty(value) && SafeId.IsMatch(value), label + " must be a safe identifier");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// Editor lifecycle wrapper. The embedded bridge starts automatically after scripts load and
    /// gracefully falls back to an already-running compatible bridge on the same port.
    /// </summary>
    [InitializeOnLoad]
    public static class PicturePuzzleFigmaEmbeddedBridge
    {
        public const string BaseUrl = "http://127.0.0.1:3907";
        private static readonly object LifecycleLock = new object();
        private static PicturePuzzleFigmaBridgeServer server;

        static PicturePuzzleFigmaEmbeddedBridge()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            EnsureStartedAfterLoad();
        }

        [InitializeOnLoadMethod]
        private static void InitializeAfterScriptLoad()
        {
            EnsureStartedAfterLoad();
        }

        public static PicturePuzzleFigmaBridgeMode Mode { get; private set; }
        public static string LastError { get; private set; }

        public static PicturePuzzleFigmaBridgeMode EnsureStarted()
        {
            lock (LifecycleLock)
            {
                if (server != null && server.IsRunning)
                {
                    Mode = PicturePuzzleFigmaBridgeMode.Embedded;
                    LastError = null;
                    return Mode;
                }

                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                var candidate = new PicturePuzzleFigmaBridgeServer(projectRoot);
                if (candidate.Start())
                {
                    server = candidate;
                    Mode = PicturePuzzleFigmaBridgeMode.Embedded;
                    LastError = null;
                    return Mode;
                }

                LastError = candidate.LastError;
                candidate.Dispose();
                if (CompatibleExternalBridgeIsRunning())
                {
                    Mode = PicturePuzzleFigmaBridgeMode.External;
                    LastError = null;
                }
                else
                {
                    Mode = PicturePuzzleFigmaBridgeMode.Unavailable;
                }
                return Mode;
            }
        }

        public static void Stop()
        {
            lock (LifecycleLock)
            {
                if (server != null)
                {
                    server.Dispose();
                    server = null;
                }
                Mode = PicturePuzzleFigmaBridgeMode.Unavailable;
            }
        }

        private static void EnsureStartedAfterLoad()
        {
            PicturePuzzleFigmaBridgeMode mode = EnsureStarted();
            if (mode == PicturePuzzleFigmaBridgeMode.Unavailable)
                Debug.LogWarning("[PicturePuzzle Figma] Local bridge could not start: " + LastError);
        }

        private static bool CompatibleExternalBridgeIsRunning()
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(BaseUrl + "/health");
                request.Method = "GET";
                request.Timeout = 600;
                request.ReadWriteTimeout = 600;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    JObject health = JObject.Parse(reader.ReadToEnd());
                    return response.StatusCode == HttpStatusCode.OK
                           && (health.Value<bool?>("ok") ?? false)
                           && health.Value<string>("protocol") == "picturepuzzle-figma-bridge"
                           && health.Value<int?>("protocolVersion") == 1;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif
