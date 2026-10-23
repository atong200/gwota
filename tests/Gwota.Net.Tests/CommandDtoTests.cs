namespace Gwota.Net.Tests;

public class CommandDtoTests
{
    public static TheoryData<GameCommand> Commands() => new()
    {
        new FinishDraftCommand(Seat.One, [4, 9]),
        FinishDraftCommand.KeepHand(Seat.Two),
        new PlayCardCommand(Seat.One, 12),
        new PlayCardCommand(Seat.Two, 12, RowType.Ranged, 30),
        new UseLeaderCommand(Seat.One),
        new ResolveChoiceCommand(Seat.Two, 5),
        new PassCommand(Seat.One),
        new ForfeitCommand(Seat.Two),
    };

    [Theory]
    [MemberData(nameof(Commands))]
    public void Commands_survive_a_round_trip(GameCommand command)
    {
        var line = NetCodec.Encode(new CommandMessage(CommandDto.From(command)));

        var decoded = NetCodec.Decode(line).ShouldBeOfType<CommandMessage>();
        decoded.Command.ToCommand().ShouldBe(command);
    }

    [Fact]
    public void A_card_command_without_a_card_is_rejected()
    {
        new CommandDto(CommandKind.PlayCard, Seat.One).ToCommand().ShouldBeNull();
        new CommandDto(CommandKind.ResolveChoice, Seat.One).ToCommand().ShouldBeNull();
    }

    [Fact]
    public void Out_of_range_values_are_rejected()
    {
        new CommandDto(CommandKind.Pass, (Seat)7).ToCommand().ShouldBeNull();
        new CommandDto(CommandKind.PlayCard, Seat.One, 3, (RowType)9).ToCommand().ShouldBeNull();
        new CommandDto((CommandKind)99, Seat.One).ToCommand().ShouldBeNull();
    }

    [Fact]
    public void A_draft_without_card_ids_keeps_the_hand() =>
        new CommandDto(CommandKind.FinishDraft, Seat.Two).ToCommand().ShouldBe(FinishDraftCommand.KeepHand(Seat.Two));
}