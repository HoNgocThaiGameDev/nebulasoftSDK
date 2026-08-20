using NebulaSoft;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;

public sealed class NetworkConnectionTests
{
    [Test]
    public void ProtocolError_StillMeansTheProbeHostResponded()
    {
        Assert.That(NetworkConnection.IsResponseReachable(UnityWebRequest.Result.ProtocolError), Is.True);
    }

    [Test]
    public void ConnectionAndDataProcessingErrors_AreNotReachable()
    {
        Assert.That(NetworkConnection.IsResponseReachable(UnityWebRequest.Result.ConnectionError), Is.False);
        Assert.That(NetworkConnection.IsResponseReachable(UnityWebRequest.Result.DataProcessingError), Is.False);
    }

    [Test]
    public void TimeoutDetection_IsCaseInsensitive()
    {
        Assert.That(NetworkConnection.IsTimeoutError("Request timeout"), Is.True);
        Assert.That(NetworkConnection.IsTimeoutError("REQUEST TIMEOUT"), Is.True);
        Assert.That(NetworkConnection.IsTimeoutError("Cannot resolve destination host"), Is.False);
    }

    [Test]
    public void Timeout_MakesTheProbeUnreachable()
    {
        Assert.That(
            NetworkConnection.IsResponseReachable(UnityWebRequest.Result.Success, timedOut: true),
            Is.False);
    }

    [Test]
    public void ProbeResult_PreservesWifiMobileAndUnknownTransport()
    {
        NetworkCheckResult wifiResult = new NetworkCheckResult(
            true,
            NetworkReachability.ReachableViaLocalAreaNetwork,
            204,
            false,
            null);
        NetworkCheckResult mobileResult = new NetworkCheckResult(
            true,
            NetworkReachability.ReachableViaCarrierDataNetwork,
            401,
            false,
            null);
        NetworkCheckResult noReachabilityMetadataResult = new NetworkCheckResult(
            true,
            NetworkReachability.NotReachable,
            302,
            false,
            null);

        Assert.That(wifiResult.IsReachable, Is.True);
        Assert.That(wifiResult.Transport, Is.EqualTo(NetworkReachability.ReachableViaLocalAreaNetwork));
        Assert.That(wifiResult.TransportKind, Is.EqualTo(NetworkTransportKind.WiFi));
        Assert.That(wifiResult.ResponseCode, Is.EqualTo(204));
        Assert.That(mobileResult.IsReachable, Is.True);
        Assert.That(mobileResult.Transport, Is.EqualTo(NetworkReachability.ReachableViaCarrierDataNetwork));
        Assert.That(mobileResult.TransportKind, Is.EqualTo(NetworkTransportKind.MobileData));
        Assert.That(mobileResult.ResponseCode, Is.EqualTo(401));
        Assert.That(noReachabilityMetadataResult.IsReachable, Is.True);
        Assert.That(noReachabilityMetadataResult.TransportKind, Is.EqualTo(NetworkTransportKind.Unknown));
        Assert.That(noReachabilityMetadataResult.ResponseCode, Is.EqualTo(302));
    }

    [Test]
    public void RedirectAndClientErrors_StillCountAsReachable()
    {
        NetworkCheckResult redirect = new NetworkCheckResult(
            true,
            NetworkReachability.ReachableViaLocalAreaNetwork,
            302,
            false,
            null);
        NetworkCheckResult unauthorized = new NetworkCheckResult(
            true,
            NetworkReachability.ReachableViaCarrierDataNetwork,
            401,
            false,
            null);
        NetworkCheckResult missing = new NetworkCheckResult(
            true,
            NetworkReachability.ReachableViaCarrierDataNetwork,
            404,
            false,
            null);

        Assert.That(redirect.IsReachable, Is.True);
        Assert.That(unauthorized.IsReachable, Is.True);
        Assert.That(missing.IsReachable, Is.True);
    }
}
