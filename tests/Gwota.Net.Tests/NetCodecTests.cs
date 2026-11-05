namespace Gwota.Net.Tests;

public class NetCodecTests
{
    private static readonly DeckDto Deck = DeckDto.From(Shipped.FirstDeck);

    public static TheoryData<NetMessage> Messages() => new()
    {
        new HelloMessage(1, "0.7.0", "abc", "Alice", Deck),
        new RejectMessage("No."),
        new StartMessage(42, "Alice", Deck, "Bob", Deck with { CardBackId = "red_mist" }),
        new CommandMessage(CommandDto.From(new PassCommand(Seat.Two))),
        new AppliedMessage(3, CommandDto.From(new FinishDraftCommand(Seat.One, [1, 2])), ulong.MaxValue),
        new RefusedMessage("Illegal."),
        new RematchMessage(),
        new LeaveMessage(LeaveReason.Desync),
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public void Messages_survive_a_round_trip(NetMessage message)
    {
        var line = NetCodec.Encode(message);

        line.ShouldNotContain("\n");
        var decoded = NetCodec.Decode(line).ShouldNotBeNull();
        decoded.GetType().ShouldBe(message.GetType());
        NetCodec.Encode(decoded).ShouldBe(line);
    }

    [Fact]
    public void The_card_back_id_is_on_the_wire()
    {
        var line = NetCodec.Encode(new HelloMessage(1, "0.7.0", "abc", "Alice", Deck with { CardBackId = "aegis" }));

        line.ShouldContain("aegis");
        NetCodec.Decode(line).ShouldBeOfType<HelloMessage>().Deck.CardBackId.ShouldBe("aegis");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"type\":\"unknown\"}")]
    [InlineData("[1,2]")]
    public void Bad_lines_decode_to_null(string line) => NetCodec.Decode(line).ShouldBeNull();

    [Fact]
    public void Enums_are_written_as_names()
    {
        var line = NetCodec.Encode(new LeaveMessage(LeaveReason.Quit));

        line.ShouldContain("\"Quit\"");
        line.ShouldContain("\"type\":\"leave\"");
    }
}
