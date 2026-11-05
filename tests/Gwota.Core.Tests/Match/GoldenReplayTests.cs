namespace Gwota.Core.Tests.Match;

/// <summary>
/// Locks the engine and AI behaviour: each seeded match must replay to exactly the same commands, events and states.
/// A change here means a gameplay change; refactors and optimisations must keep these values.
/// </summary>
public class GoldenReplayTests
{
    private static readonly ulong[] Expected =
    [
        0x2A3D61F7ED11B8C3, 0xDF1BDB36E128EF43, 0x18493246EE22F6D9, 0x0E8461C9DAD77FF4, 0xD354CE8B0AD69E56,
        0xECD9C954657BA488, 0xD9E329D3D002C08F, 0x5DEC5A4280CC9403, 0x87F2850CDB9C6531, 0x7F6EDE49BECADEA3,
        0x7F383C6F1819617D, 0xBED7889E155D2A4B, 0x40D3566D12879117, 0x260439CB20DF26D3, 0x85E7E103C4CF9A1B,
        0xD396FBB7571655D4, 0xF007F90C830C5458, 0x2B8E20C25EC1BB65, 0xBC0A517FE01DEFD2, 0x1BA634C26F0F23FB,
        0xBBD6A7FF3278914F, 0x8038D7944DE638C3, 0xFBFDDBE40359DDBC, 0xAB6C4CBBEB331EA2, 0x387A738FE0538BAE,
        0xA591E830B8C4D4F7, 0xE917B0F7DDEAA14C, 0x9DF613B97D069F8C, 0x238BD1DA589464FB, 0xE6B7A78049C26D11,
    ];

    [Fact]
    public void Seeded_matches_replay_identically()
    {
        var actual = Enumerable.Range(1, 30).Select(seed => Replay.Fingerprint((ulong)seed)).ToArray();

        actual.ShouldBe(Expected, $"Fingerprints: [{string.Join(", ", actual.Select(v => $"0x{v:X16}"))}]");
    }

    [Fact]
    public void Replays_are_deterministic() => Replay.Fingerprint(7).ShouldBe(Replay.Fingerprint(7));

    [Fact]
    public void Simulating_on_a_clone_never_changes_the_original()
    {
        for (ulong seed = 1; seed <= 6; seed++)
        {
            Replay.Fingerprint(seed, (engine, command) =>
            {
                var before = StateChecksum.Compute(engine.State);
                engine.Clone().Apply(command).IsSuccess.ShouldBeTrue();
                StateChecksum.Compute(engine.State).ShouldBe(before);
            });
        }
    }

    [Fact]
    public void A_clone_replays_the_same_future_as_the_original()
    {
        var (engine, _, _) = Replay.Setup(3);
        var clone = engine.Clone();
        var random = new RandomController(99);
        var mirror = new RandomController(99);

        while (!engine.IsFinished)
        {
            var seat = engine.State.SeatToAct!.Value;
            engine.Apply(random.Decide(engine, seat)).IsSuccess.ShouldBeTrue();
            clone.Apply(mirror.Decide(clone, seat)).IsSuccess.ShouldBeTrue();
            StateChecksum.Compute(clone.State).ShouldBe(StateChecksum.Compute(engine.State));
        }
    }
}
