using System.Net;
using Microsoft.Extensions.Time.Testing;
using MyRemoteControll.Agent.Features.Pairing;
using MyRemoteControll.Agent.Infrastructure.Security;

namespace MyRemoteControll.Agent.Tests.Pairing;

public sealed class PairingLockoutTests
{
    [Fact]
    public void FifthFailureWithinWindow_LocksForFifteenMinutes()
    {
        var time = new FakeTimeProvider();
        var lockout = new PairingLockout(time);

        for (var i = 0; i < 4; i++)
        {
            Assert.False(lockout.RegisterFailure());
        }

        Assert.True(lockout.RegisterFailure());
        Assert.True(lockout.IsLocked(out _));
        time.Advance(TimeSpan.FromMinutes(15));
        Assert.False(lockout.IsLocked(out _));
    }

    [Fact]
    public void FailuresOlderThanTenMinutes_DoNotCount()
    {
        var time = new FakeTimeProvider();
        var lockout = new PairingLockout(time);

        for (var i = 0; i < 4; i++)
        {
            lockout.RegisterFailure();
        }

        time.Advance(TimeSpan.FromMinutes(10));

        Assert.False(lockout.RegisterFailure());
        Assert.False(lockout.IsLocked(out _));
    }
}

public sealed class PairingPayloadTests
{
    [Fact]
    public void Payload_HasVersionHostsPortCodePinAndEncodedPcName()
    {
        var payload = PairingPayload.Build(["192.168.0.10", "100.101.1.2", "pc.tail1234.ts.net"], 47800, "codigo_ABC-1", "pin_XYZ-2", "PC DO NETO");

        Assert.Equal(
            "mrc://pair?v=1&h=192.168.0.10,100.101.1.2,pc.tail1234.ts.net&p=47800&c=codigo_ABC-1&f=pin_XYZ-2&n=PC%20DO%20NETO",
            payload);
    }
}

public sealed class HostAddressClassificationTests
{
    [Theory]
    [InlineData("10.0.0.5", true)]
    [InlineData("172.16.1.1", true)]
    [InlineData("172.31.255.1", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("192.168.1.20", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("100.100.1.1", false)]
    public void PrivateRanges_AreRecognized(string address, bool expected) =>
        Assert.Equal(expected, HostAddressProvider.IsPrivate(IPAddress.Parse(address)));

    [Theory]
    [InlineData("100.64.0.1", true)]
    [InlineData("100.127.255.254", true)]
    [InlineData("100.128.0.1", false)]
    [InlineData("100.63.0.1", false)]
    public void TailscaleRange_IsRecognized(string address, bool expected) =>
        Assert.Equal(expected, HostAddressProvider.IsTailscale(IPAddress.Parse(address)));
}

public sealed class SecureTokensTests
{
    [Fact]
    public void DeviceToken_Has256BitsInBase64Url()
    {
        var token = SecureTokens.Create(SecureTokens.DeviceTokenBytes);

        Assert.Equal(43, token.Length);
        Assert.True(SecureTokens.HasValidFormat(token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("tem espaço")]
    [InlineData("com/barra+mais")]
    public void InvalidFormats_AreRejected(string value) => Assert.False(SecureTokens.HasValidFormat(value));

    [Fact]
    public void Hashes_DependOnThePepper()
    {
        var token = SecureTokens.Create(SecureTokens.DeviceTokenBytes);

        var first = new TokenHasher([1, 2, 3]).Hash(token);
        var second = new TokenHasher([4, 5, 6]).Hash(token);

        Assert.False(TokenHasher.AreEqual(first, second));
        Assert.Equal(TokenHasher.HashLength, first.Length);
    }
}
