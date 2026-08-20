using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using PicturePuzzle.EditorTools;
using UnityEngine;

public sealed class PicturePuzzleFigmaEmbeddedBridgeTests
{
    private const string StorePath = "Assets/Project Files/Game/Prefabs/UI Store/UI Store.prefab";

    private sealed class TimeoutWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            request.Timeout = 5000;
            return request;
        }
    }

    [Test]
    public void Validator_RejectsInvalidCanvasUnsafeIdsAndInvalidVisualOrNestedAssetMetadata()
    {
        FigmaWireframeExportResult export = PicturePuzzleFigmaWireframeExporter.Export(new[] { StorePath });
        JObject valid = JObject.Parse(JsonUtility.ToJson(export.batch));

        int nonTextNodeIndex = export.batch.items[0].nodes.FindIndex(node => node.role != "text");
        int textNodeIndex = export.batch.items[0].nodes.FindIndex(node => node.role == "text");
        Assert.That(nonTextNodeIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(textNodeIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(export.batch.items[0].nodes[nonTextNodeIndex].textStyle, Is.Null);
        JObject defaultTextStyle = (JObject)valid["items"][0]["nodes"][nonTextNodeIndex]["textStyle"];
        Assert.That(defaultTextStyle.Value<string>("fontFamily"), Is.Empty);
        Assert.That(defaultTextStyle.Value<float>("fontSize"), Is.EqualTo(0f));
        Assert.DoesNotThrow(() => PicturePuzzleFigmaBridgeServer.ValidateWireframeBatch(valid));

        JObject invalidTextStyle = (JObject)valid.DeepClone();
        invalidTextStyle["items"][0]["nodes"][textNodeIndex]["textStyle"] = defaultTextStyle.DeepClone();
        Assert.That(
            Assert.Throws<InvalidOperationException>(() => PicturePuzzleFigmaBridgeServer.ValidateWireframeBatch(invalidTextStyle)).Message,
            Does.Contain("textStyle.fontSize is invalid"));

        JObject invalidCanvas = (JObject)valid.DeepClone();
        invalidCanvas["canvas"]["width"] = 720;
        Assert.That(
            Assert.Throws<InvalidOperationException>(() => PicturePuzzleFigmaBridgeServer.ValidateWireframeBatch(invalidCanvas)).Message,
            Does.Contain("1080x1920"));

        JObject unsafeId = (JObject)valid.DeepClone();
        unsafeId["batchId"] = "../../outside";
        Assert.That(
            Assert.Throws<InvalidOperationException>(() => PicturePuzzleFigmaBridgeServer.ValidateWireframeBatch(unsafeId)).Message,
            Does.Contain("safe identifier"));

        JObject unsafeSourcePrefabGuid = (JObject)valid.DeepClone();
        unsafeSourcePrefabGuid["items"][0]["sourcePrefabGuid"] = "../unsafe";
        Assert.That(
            Assert.Throws<InvalidOperationException>(() => PicturePuzzleFigmaBridgeServer.ValidateWireframeBatch(unsafeSourcePrefabGuid)).Message,
            Does.Contain("sourcePrefabGuid"));

        JObject invalidAssetReviewFlag = (JObject)valid.DeepClone();
        invalidAssetReviewFlag["items"][0]["nodes"][0]["includeInAssetReview"] = "false";
        Assert.That(
            Assert.Throws<InvalidOperationException>(() => PicturePuzzleFigmaBridgeServer.ValidateWireframeBatch(invalidAssetReviewFlag)).Message,
            Does.Contain("includeInAssetReview"));

        int visualNodeIndex = export.batch.items[0].nodes.FindIndex(node => !string.IsNullOrEmpty(node.visualImageId));
        Assert.That(visualNodeIndex, Is.GreaterThanOrEqualTo(0));
        JObject mismatchedVisualId = (JObject)valid.DeepClone();
        mismatchedVisualId["items"][0]["nodes"][visualNodeIndex]["visualImageId"] = "different-visual-id";
        Assert.That(
            Assert.Throws<InvalidOperationException>(() => PicturePuzzleFigmaBridgeServer.ValidateWireframeBatch(mismatchedVisualId)).Message,
            Does.Contain("visualImageId must match node.id"));

        int assetNodeIndex = export.batch.items[0].nodes.FindIndex(node => !string.IsNullOrEmpty(node.assetImageId));
        Assert.That(assetNodeIndex, Is.GreaterThanOrEqualTo(0));
        JObject unsafeAssetId = (JObject)valid.DeepClone();
        unsafeAssetId["items"][0]["nodes"][assetNodeIndex]["assetImageId"] = "../outside";
        Assert.That(
            Assert.Throws<InvalidOperationException>(() => PicturePuzzleFigmaBridgeServer.ValidateWireframeBatch(unsafeAssetId)).Message,
            Does.Contain("assetImageId"));

        JObject unsafeNestedPath = (JObject)valid.DeepClone();
        unsafeNestedPath["items"][0]["nodes"][assetNodeIndex]["nestedPrefabPath"] = "Assets/../Library/secret.prefab";
        Assert.That(
            Assert.Throws<InvalidOperationException>(() => PicturePuzzleFigmaBridgeServer.ValidateWireframeBatch(unsafeNestedPath)).Message,
            Does.Contain("nestedPrefabPath"));
    }

    [Test]
    public void EmbeddedServer_CompletesTargetedWireframeQueueAndServesRegisteredReferenceVisualAndNestedAssetPngs()
    {
        int port = FindFreeTcpPort();
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        using (var server = new PicturePuzzleFigmaBridgeServer(projectRoot, port))
        {
            Assert.That(server.Start(), Is.True, server.LastError);
            FigmaWireframeExportResult export = PicturePuzzleFigmaWireframeExporter.Export(new[] { StorePath });
            FigmaWireframeItem item = export.batch.items[0];
            FigmaWireframeNode visualNode = item.nodes.Find(node => !string.IsNullOrEmpty(node.visualImageId));
            FigmaWireframeNode assetNode = item.nodes.Find(node => !string.IsNullOrEmpty(node.assetImageId));
            Assert.That(visualNode, Is.Not.Null);
            Assert.That(assetNode, Is.Not.Null);

            using (var client = new TimeoutWebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                JObject heartbeat = JObject.Parse(client.UploadString(
                    server.BaseUrl + "/heartbeat",
                    "POST",
                    "{\"pluginId\":\"figma-test\",\"fileName\":\"PicturePuzzle\",\"pageName\":\"Wireframes\",\"fileKey\":\"abc123-file\"}"));
                Assert.That(heartbeat.Value<bool>("ok"), Is.True);

                JObject health = JObject.Parse(client.DownloadString(server.BaseUrl + "/health"));
                Assert.That(health.Value<string>("protocol"), Is.EqualTo("picturepuzzle-figma-bridge"));
                Assert.That(health.Value<bool>("pluginConnected"), Is.True);
                Assert.That(health["plugins"][0].Value<string>("fileKey"), Is.EqualTo("abc123-file"));
                Assert.That(client.ResponseHeaders["Access-Control-Allow-Origin"], Is.EqualTo("*"));
                JObject localhostHealth = JObject.Parse(client.DownloadString("http://localhost:" + port + "/health"));
                Assert.That(localhostHealth.Value<string>("protocol"), Is.EqualTo("picturepuzzle-figma-bridge"));
                Assert.That(localhostHealth.Value<bool>("pluginConnected"), Is.True);

                var enqueueBody = new JObject
                {
                    ["type"] = "wireframe-batch",
                    ["name"] = "Unity UGUI wireframes",
                    ["targetPluginId"] = "figma-test",
                    ["batch"] = JObject.Parse(JsonUtility.ToJson(export.batch))
                };
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                JObject enqueued = JObject.Parse(client.UploadString(server.BaseUrl + "/enqueue", "POST", enqueueBody.ToString()));
                string commandId = enqueued["command"].Value<string>("id");
                Assert.That(commandId, Is.Not.Empty);

                JObject wrongTarget = JObject.Parse(client.DownloadString(server.BaseUrl + "/next?pluginId=other-plugin"));
                Assert.That(wrongTarget.Value<string>("type"), Is.EqualTo("idle"));

                JObject command = JObject.Parse(client.DownloadString(server.BaseUrl + "/next?pluginId=figma-test"));
                Assert.That(command.Value<string>("id"), Is.EqualTo(commandId));
                Assert.That(command["batch"].Value<string>("batchId"), Is.EqualTo(export.batch.batchId));

                string imageUrl = server.BaseUrl + "/export-image?batchId=" + Uri.EscapeDataString(export.batch.batchId)
                                  + "&itemId=" + Uri.EscapeDataString(item.itemId);
                byte[] servedImage = client.DownloadData(imageUrl);
                byte[] exportedImage = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(export.manifestPath), item.itemId + ".png"));
                Assert.That(servedImage, Is.EqualTo(exportedImage));

                string visualUrl = server.BaseUrl + "/export-image?batchId=" + Uri.EscapeDataString(export.batch.batchId)
                                   + "&itemId=" + Uri.EscapeDataString(visualNode.visualImageId);
                byte[] servedVisual = client.DownloadData(visualUrl);
                byte[] exportedVisual = File.ReadAllBytes(Path.Combine(
                    Path.GetDirectoryName(export.manifestPath),
                    visualNode.visualImageId + ".png"));
                Assert.That(servedVisual, Is.EqualTo(exportedVisual));

                string assetUrl = server.BaseUrl + "/export-image?batchId=" + Uri.EscapeDataString(export.batch.batchId)
                                  + "&itemId=" + Uri.EscapeDataString(assetNode.assetImageId);
                byte[] servedAsset = client.DownloadData(assetUrl);
                byte[] exportedAsset = File.ReadAllBytes(Path.Combine(
                    Path.GetDirectoryName(export.manifestPath),
                    assetNode.assetImageId + ".png"));
                Assert.That(servedAsset, Is.EqualTo(exportedAsset));

                string invalidVisualUrl = server.BaseUrl + "/export-image?batchId=" + Uri.EscapeDataString(export.batch.batchId)
                                          + "&itemId=unregistered-visual-id";
                WebException invalidVisualError = Assert.Throws<WebException>(() => client.DownloadData(invalidVisualUrl));
                using (var response = invalidVisualError.Response as HttpWebResponse)
                {
                    Assert.That(response, Is.Not.Null);
                    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                }

                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                JObject postedResult = JObject.Parse(client.UploadString(
                    server.BaseUrl + "/result",
                    "POST",
                    "{\"id\":\"" + commandId + "\",\"ok\":true,\"result\":{\"pageName\":\"Wireframes\",\"exported\":1}}"));
                Assert.That(postedResult.Value<bool>("ok"), Is.True);

                JObject results = JObject.Parse(client.DownloadString(server.BaseUrl + "/results"));
                Assert.That(results["results"][0].Value<string>("id"), Is.EqualTo(commandId));
                Assert.That(results["inFlight"], Is.Empty);
            }
        }
    }

    [Test]
    public void EmbeddedServer_StartIsIdempotentAndHeartbeatExpiresAfterFifteenSeconds()
    {
        int port = FindFreeTcpPort();
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        DateTime now = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);
        using (var server = new PicturePuzzleFigmaBridgeServer(projectRoot, port, () => now))
        using (var client = new TimeoutWebClient())
        {
            Assert.That(server.Start(), Is.True, server.LastError);
            Assert.That(server.Start(), Is.True, "Starting an active bridge should be a no-op.");

            client.Encoding = Encoding.UTF8;
            client.Headers[HttpRequestHeader.ContentType] = "application/json";
            client.UploadString(
                server.BaseUrl + "/heartbeat",
                "POST",
                "{\"pluginId\":\"figma-expiry\",\"fileName\":\"PicturePuzzle\",\"pageName\":\"Wireframes\"}");
            Assert.That(JObject.Parse(client.DownloadString(server.BaseUrl + "/health")).Value<bool>("pluginConnected"), Is.True);

            now = now.AddSeconds(16);
            Assert.That(JObject.Parse(client.DownloadString(server.BaseUrl + "/health")).Value<bool>("pluginConnected"), Is.False);
        }
    }

    private static int FindFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
