using Gwota.Net.Lan;
using Gwota.Net.Transport;

namespace Gwota.Net.Tests.Lan;

public class LanMatchTests
{
    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    [InlineData(2024UL)]
    public void Both_games_stay_identical_through_a_whole_match(ulong seed)
    {
        var (host, guest) = LanRig.Connect(seed);

        LanRig.PlayOut(host, guest);

        guest.Session.State.Outcome.ShouldBe(host.Session.State.Outcome);
        guest.Session.Checksum().ShouldBe(host.Session.Checksum());
        host.CloseReason.ShouldBeNull();
        guest.CloseReason.ShouldBeNull();
    }

    [Fact]
    public void A_mirror_match_stays_identical()
    {
        var (host, guest) = LanRig.Connect(5, Shipped.FirstDeck, Shipped.FirstDeck);

        LanRig.PlayOut(host, guest);

        guest.Session.Checksum().ShouldBe(host.Session.Checksum());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Concurrent_redraws_end_identical_whichever_arrives_first(bool guestFirst)
    {
        var (host, guest) = LanRig.Connect();
        var hostRedraw = Redraw(host);
        var guestRedraw = Redraw(guest);

        if (guestFirst)
        {
            guest.Send(guestRedraw);
            host.Poll();
            host.Send(hostRedraw);
        }
        else
        {
            host.Send(hostRedraw);
            guest.Send(guestRedraw);
        }

        LanRig.Pump(host, guest);

        host.Session.State.Phase.ShouldBe(MatchPhase.Playing);
        guest.Session.Checksum().ShouldBe(host.Session.Checksum());
    }

    [Fact]
    public void Sending_waits_for_the_command_to_come_back()
    {
        var (host, guest) = LanRig.Connect();

        guest.Send(FinishDraftCommand.KeepHand(Seat.Two));

        guest.CanSend.ShouldBeFalse();
        host.Poll();
        host.HasPending.ShouldBeTrue();
        host.ApplyNext().ShouldNotBeNull().IsSuccess.ShouldBeTrue();
        guest.Poll();
        guest.CanSend.ShouldBeFalse();
        guest.ApplyNext().ShouldNotBeNull().IsSuccess.ShouldBeTrue();
        guest.CanSend.ShouldBeTrue();
        guest.Session.State.Player(Seat.Two).HasFinishedDraft.ShouldBeTrue();
    }

    [Fact]
    public void The_host_applies_its_own_commands_through_the_queue()
    {
        var (host, _) = LanRig.Connect();

        host.Send(FinishDraftCommand.KeepHand(Seat.One));

        host.CanSend.ShouldBeFalse();
        host.Session.State.Player(Seat.One).HasFinishedDraft.ShouldBeFalse();
        host.ApplyNext().ShouldNotBeNull().IsSuccess.ShouldBeTrue();
        host.Session.State.Player(Seat.One).HasFinishedDraft.ShouldBeTrue();
        host.ApplyNext().ShouldBeNull();
    }

    [Fact]
    public void Only_the_local_seat_can_be_sent()
    {
        var (host, guest) = LanRig.Connect();

        Should.Throw<ArgumentException>(() => host.Send(new PassCommand(Seat.Two)));
        Should.Throw<ArgumentException>(() => guest.Send(new PassCommand(Seat.One)));
    }

    [Fact]
    public void An_illegal_guest_command_is_refused_and_changes_nothing()
    {
        var (host, guest) = LanRig.Connect();
        var before = host.Session.Checksum();

        guest.Send(new PlayCardCommand(Seat.Two, 9999));
        LanRig.Pump(host, guest);

        guest.CanSend.ShouldBeTrue();
        host.Session.Checksum().ShouldBe(before);
        guest.Session.Checksum().ShouldBe(before);
        host.CloseReason.ShouldBeNull();
    }

    [Fact]
    public void A_command_for_the_wrong_seat_is_refused_by_the_host()
    {
        var (hostEnd, guestEnd) = InMemoryConnection.CreatePair();
        var host = LanMatch.Host(hostEnd, Shipped.Data, Setup(), () => 1);
        var before = host.Session.Checksum();

        guestEnd.Send(NetCodec.Encode(new CommandMessage(CommandDto.From(FinishDraftCommand.KeepHand(Seat.One)))));
        host.Poll();

        host.HasPending.ShouldBeFalse();
        host.Session.Checksum().ShouldBe(before);
        guestEnd.TryReceive(out var line).ShouldBeTrue();
        NetCodec.Decode(line).ShouldBeOfType<RefusedMessage>();
    }

    [Fact]
    public void Each_applied_command_is_relayed_with_the_host_checksum()
    {
        var (hostEnd, guestEnd) = InMemoryConnection.CreatePair();
        var host = LanMatch.Host(hostEnd, Shipped.Data, Setup(), () => 1);

        host.Send(FinishDraftCommand.KeepHand(Seat.One));
        host.ApplyNext();

        guestEnd.TryReceive(out var line).ShouldBeTrue();
        var applied = NetCodec.Decode(line).ShouldBeOfType<AppliedMessage>();
        applied.Sequence.ShouldBe(1);
        applied.Command.ToCommand().ShouldBe(FinishDraftCommand.KeepHand(Seat.One));
        applied.Checksum.ShouldBe(host.Session.Checksum());
    }

    [Fact]
    public void Leaving_hands_the_opponent_a_forfeit_win()
    {
        var (host, guest) = LanRig.Connect();

        host.Leave();
        LanRig.Pump(guest);

        host.CloseReason.ShouldBe(LanMatch.YouLeftReason);
        guest.OpponentLeft.ShouldBeTrue();
        guest.CloseReason.ShouldBe(LanMatch.OpponentLeftReason);
        guest.Session.State.Outcome.ShouldBe(new MatchOutcome(Seat.Two, Seat.One));
        guest.CanSend.ShouldBeFalse();
    }

    [Fact]
    public void A_dropped_connection_hands_the_host_a_forfeit_win()
    {
        var (hostEnd, guestEnd) = InMemoryConnection.CreatePair();
        var host = LanMatch.Host(hostEnd, Shipped.Data, Setup(), () => 1);

        guestEnd.Close("crash");
        host.Poll();
        while (host.ApplyNext() is not null)
        {
        }

        host.CloseReason.ShouldBe(LanMatch.ConnectionLostReason);
        host.Session.State.Outcome.ShouldBe(new MatchOutcome(Seat.One, Seat.Two));
    }

    [Fact]
    public void Commands_already_relayed_are_applied_before_the_forfeit()
    {
        var (host, guest) = LanRig.Connect();
        host.Send(FinishDraftCommand.KeepHand(Seat.One));
        host.ApplyNext();

        host.Leave();
        LanRig.Pump(guest);

        guest.Session.State.Player(Seat.One).HasFinishedDraft.ShouldBeTrue();
        guest.Session.State.Outcome.ShouldNotBeNull().ForfeitedBy.ShouldBe(Seat.One);
    }

    [Fact]
    public void Leaving_after_the_match_keeps_the_result()
    {
        var (host, guest) = LanRig.Connect(3);
        LanRig.PlayOut(host, guest);
        var outcome = guest.Session.State.Outcome;

        host.Leave();
        LanRig.Pump(guest);

        guest.OpponentLeft.ShouldBeTrue();
        guest.Session.State.Outcome.ShouldBe(outcome);
    }

    [Fact]
    public void A_forfeit_sent_by_the_guest_ends_both_matches()
    {
        var (host, guest) = LanRig.Connect();

        guest.Send(new ForfeitCommand(Seat.Two));
        LanRig.Pump(host, guest);

        host.Session.State.Outcome.ShouldBe(new MatchOutcome(Seat.One, Seat.Two));
        guest.Session.State.Outcome.ShouldBe(host.Session.State.Outcome);
        host.CloseReason.ShouldBeNull();
    }

    [Fact]
    public void A_rematch_needs_both_players()
    {
        var (host, guest) = LanRig.Connect(11);
        LanRig.PlayOut(host, guest);
        var firstSeed = host.Setup.Seed;

        guest.RequestRematch();
        LanRig.Pump(host, guest);

        guest.Rematch.ShouldBe(RematchStatus.RequestedByYou);
        host.Rematch.ShouldBe(RematchStatus.RequestedByOpponent);
        host.MatchNumber.ShouldBe(1);

        host.RequestRematch();
        LanRig.Pump(host, guest);

        host.MatchNumber.ShouldBe(2);
        guest.MatchNumber.ShouldBe(2);
        host.Rematch.ShouldBe(RematchStatus.None);
        guest.Rematch.ShouldBe(RematchStatus.None);
        host.Setup.Seed.ShouldNotBe(firstSeed);
        guest.Setup.Seed.ShouldBe(host.Setup.Seed);
        host.Session.IsFinished.ShouldBeFalse();
        guest.Session.Checksum().ShouldBe(host.Session.Checksum());
    }

    [Fact]
    public void A_rematch_plays_out_identically()
    {
        var (host, guest) = LanRig.Connect(13);
        LanRig.PlayOut(host, guest);
        host.RequestRematch();
        guest.RequestRematch();
        LanRig.Pump(host, guest);

        LanRig.PlayOut(host, guest);

        guest.Session.Checksum().ShouldBe(host.Session.Checksum());
        guest.Session.State.Outcome.ShouldBe(host.Session.State.Outcome);
    }

    [Fact]
    public void A_rematch_cannot_be_asked_mid_match()
    {
        var (host, guest) = LanRig.Connect();

        host.RequestRematch();
        LanRig.Pump(host, guest);

        host.Rematch.ShouldBe(RematchStatus.None);
        guest.Rematch.ShouldBe(RematchStatus.None);
    }

    [Fact]
    public void A_checksum_mismatch_stops_the_match()
    {
        var (hostEnd, guestEnd) = InMemoryConnection.CreatePair();
        var guest = LanMatch.Guest(guestEnd, Shipped.Data, Setup());

        hostEnd.Send(NetCodec.Encode(new AppliedMessage(1, CommandDto.From(FinishDraftCommand.KeepHand(Seat.One)), 123)));
        guest.Poll();
        guest.ApplyNext();

        guest.CloseReason.ShouldBe(LanMatch.DesyncReason);
        guest.OpponentLeft.ShouldBeFalse();
        hostEnd.TryReceive(out var line).ShouldBeTrue();
        NetCodec.Decode(line).ShouldBe(new LeaveMessage(LeaveReason.Desync));
    }

    [Fact]
    public void A_skipped_sequence_number_stops_the_match()
    {
        var (hostEnd, guestEnd) = InMemoryConnection.CreatePair();
        var guest = LanMatch.Guest(guestEnd, Shipped.Data, Setup());

        hostEnd.Send(NetCodec.Encode(new AppliedMessage(2, CommandDto.From(FinishDraftCommand.KeepHand(Seat.One)), 0)));
        guest.Poll();

        guest.CloseReason.ShouldBe(LanMatch.DesyncReason);
        guest.HasPending.ShouldBeFalse();
    }

    [Fact]
    public void The_host_sees_a_guest_that_leaves()
    {
        var (host, guest) = LanRig.Connect();

        guest.Leave();
        host.Poll();

        guest.CloseReason.ShouldBe(LanMatch.YouLeftReason);
        host.CloseReason.ShouldBe(LanMatch.OpponentLeftReason);
        host.OpponentLeft.ShouldBeTrue();
    }

    [Fact]
    public void The_host_stops_when_the_guest_reports_a_desync()
    {
        var (hostEnd, guestEnd) = InMemoryConnection.CreatePair();
        var host = LanMatch.Host(hostEnd, Shipped.Data, Setup(), () => 1);

        guestEnd.Send(NetCodec.Encode(new LeaveMessage(LeaveReason.Desync)));
        host.Poll();

        host.CloseReason.ShouldBe(LanMatch.DesyncReason);
        host.OpponentLeft.ShouldBeFalse();
        host.HasPending.ShouldBeFalse();
    }
    [Fact]
    public void Nothing_is_sent_once_the_link_is_closed()
    {
        var (host, guest) = LanRig.Connect();
        guest.Leave();

        guest.Send(FinishDraftCommand.KeepHand(Seat.Two));

        guest.CanSend.ShouldBeFalse();
        guest.HasPending.ShouldBeFalse();
        LanRig.Pump(host);
        host.Session.State.Player(Seat.Two).HasFinishedDraft.ShouldBeFalse();
    }

    private static MatchSetup Setup() => new(Shipped.FirstDeck, Shipped.LastDeck, 4, "Alice", "Bob");

    private static FinishDraftCommand Redraw(LanMatch link) =>
        new(link.LocalSeat, [.. link.Session.State.Player(link.LocalSeat).Hand.Take(2).Select(c => c.Id)]);
}