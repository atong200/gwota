using Gwota.Net.Lan;

namespace Gwota.Net.Tests.Lan;

public class LanAddressTests
{
    [Theory]
    [InlineData("192.168.1.20", "192.168.1.20", 7777)]
    [InlineData("  192.168.1.20:7790 ", "192.168.1.20", 7790)]
    [InlineData("127.0.0.1:1", "127.0.0.1", 1)]
    [InlineData("alice-pc", "alice-pc", 7777)]
    [InlineData("alice-pc.local:65535", "alice-pc.local", 65535)]
    [InlineData("[::1]:7780", "::1", 7780)]
    [InlineData("[fe80::1]", "fe80::1", 7777)]
    [InlineData("fe80::1", "fe80::1", 7777)]
    public void Valid_addresses_are_parsed(string text, string host, int port)
    {
        LanAddress.Parse(text).ShouldBe(new LanAddress(host, port));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("192.168.1.20:")]
    [InlineData("192.168.1.20:0")]
    [InlineData("192.168.1.20:65536")]
    [InlineData("192.168.1.20:-5")]
    [InlineData("192.168.1.20:77a")]
    [InlineData("my pc")]
    [InlineData("http://host")]
    [InlineData(":7777")]
    [InlineData("-host")]
    [InlineData("[::1")]
    [InlineData("[::1]7777")]
    [InlineData("[nothost]:7777")]
    public void Invalid_addresses_are_refused(string? text)
    {
        LanAddress.Parse(text).ShouldBeNull();
    }

    [Theory]
    [InlineData("10.0.0.2", 7777, "10.0.0.2:7777")]
    [InlineData("::1", 7780, "[::1]:7780")]
    public void Addresses_print_as_typed(string host, int port, string expected)
    {
        new LanAddress(host, port).ToString().ShouldBe(expected);
    }
}
