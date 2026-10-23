using Gwota.Core.Settings;

namespace Gwota.Core.Tests.Settings;

public class SettingsSerializerTests
{
    [Fact]
    public void Defaults_are_sensible()
    {
        GameSettings.Default.Audio.MasterVolume.ShouldBe(0.8);
        GameSettings.Default.Interface.ShowMatchLog.ShouldBeTrue();
    }

    [Fact]
    public void Settings_survive_a_round_trip()
    {
        var settings = new GameSettings
        {
            Audio = new AudioSettings { MasterVolume = 0.35 },
            Interface = new InterfaceSettings { ShowMatchLog = false },
        };

        SettingsSerializer.Deserialize(SettingsSerializer.Serialize(settings)).ShouldBe(settings);
    }

    [Fact]
    public void Json_is_readable_and_camel_cased()
    {
        var json = SettingsSerializer.Serialize(GameSettings.Default);

        json.ShouldContain("\"audio\"");
        json.ShouldContain("\"masterVolume\": 0.8");
        json.ShouldContain("\"showMatchLog\": true");
        json.ShouldContain("\"username\": \"Player\"");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not json")]
    [InlineData("[1, 2]")]
    [InlineData("null")]
    [InlineData("{ \"audio\": { \"masterVolume\": \"loud\" } }")]
    public void Unreadable_content_falls_back_to_defaults(string? json) =>
        SettingsSerializer.Deserialize(json).ShouldBe(GameSettings.Default);

    [Fact]
    public void Missing_values_and_sections_use_defaults()
    {
        SettingsSerializer.Deserialize("{}").ShouldBe(GameSettings.Default);
        SettingsSerializer.Deserialize("{ \"audio\": null, \"interface\": null }").ShouldBe(GameSettings.Default);

        var partial = SettingsSerializer.Deserialize("{ \"interface\": { \"showMatchLog\": false } }");
        partial.Audio.MasterVolume.ShouldBe(AudioSettings.DefaultMasterVolume);
        partial.Interface.ShowMatchLog.ShouldBeFalse();
    }

    [Fact]
    public void Unknown_values_are_ignored()
    {
        var settings = SettingsSerializer.Deserialize("{ \"video\": { \"fullscreen\": true }, \"audio\": { \"masterVolume\": 0.5, \"music\": 1 } }");

        settings.Audio.MasterVolume.ShouldBe(0.5);
    }

    [Theory]
    [InlineData(-0.5, 0.0)]
    [InlineData(1.7, 1.0)]
    [InlineData(0.25, 0.25)]
    public void Volume_is_clamped(double stored, double expected) =>
        SettingsSerializer.Deserialize($"{{ \"audio\": {{ \"masterVolume\": {stored.ToString(System.Globalization.CultureInfo.InvariantCulture)} }} }}")
            .Audio.MasterVolume.ShouldBe(expected);

    [Fact]
    public void Serializing_normalizes_out_of_range_values()
    {
        var settings = new GameSettings { Audio = new AudioSettings { MasterVolume = double.NaN } };

        SettingsSerializer.Deserialize(SettingsSerializer.Serialize(settings)).Audio.MasterVolume.ShouldBe(AudioSettings.DefaultMasterVolume);
    }

    [Fact]
    public void Comments_and_trailing_commas_are_tolerated() =>
        SettingsSerializer.Deserialize("{ // hand-edited\n \"audio\": { \"masterVolume\": 0.1, }, }").Audio.MasterVolume.ShouldBe(0.1);

    [Fact]
    public void Older_files_without_a_profile_get_the_default_username() =>
        SettingsSerializer.Deserialize("{ \"audio\": { \"masterVolume\": 0.5 } }").Profile.Username.ShouldBe(ProfileSettings.DefaultUsername);

    [Theory]
    [InlineData(null, "Player")]
    [InlineData("", "Player")]
    [InlineData("   ", "Player")]
    [InlineData("  Tong  ", "Tong")]
    [InlineData("Big   Bad\tWolf", "Big Bad Wolf")]
    [InlineData("[b]Bold[/b]", "bBold/b")]
    [InlineData("[]", "Player")]
    [InlineData("Line\nBreak\u0007", "Line Break")]
    [InlineData("A name that is way too long", "A name that is w")]
    [InlineData("Sixteen chars  x", "Sixteen chars x")]
    [InlineData("Fifteen chars x y", "Fifteen chars x")]
    public void Usernames_are_normalized(string? input, string expected) =>
        ProfileSettings.NormalizeUsername(input).ShouldBe(expected);

    [Fact]
    public void Stored_usernames_are_normalized_on_load() =>
        SettingsSerializer.Deserialize("{ \"profile\": { \"username\": \"  [Tong]  \" } }").Profile.Username.ShouldBe("Tong");

    [Fact]
    public void Missing_profile_section_is_restored() =>
        SettingsSerializer.Deserialize("{ \"profile\": null }").ShouldBe(GameSettings.Default);

    [Fact]
    public void Animation_speed_defaults_to_normal_and_is_written_as_text()
    {
        GameSettings.Default.Gameplay.AnimationSpeed.ShouldBe(AnimationSpeed.Normal);
        SettingsSerializer.Serialize(GameSettings.Default).ShouldContain("\"animationSpeed\": \"normal\"");
    }

    [Theory]
    [InlineData(AnimationSpeed.Normal)]
    [InlineData(AnimationSpeed.Fast)]
    [InlineData(AnimationSpeed.Off)]
    public void Animation_speed_survives_a_round_trip(AnimationSpeed speed)
    {
        var settings = new GameSettings { Gameplay = new GameplaySettings { AnimationSpeed = speed } };

        SettingsSerializer.Deserialize(SettingsSerializer.Serialize(settings)).Gameplay.AnimationSpeed.ShouldBe(speed);
    }

    [Theory]
    [InlineData("{ \"gameplay\": null }")]
    [InlineData("{ \"gameplay\": { \"animationSpeed\": 42 } }")]
    public void Missing_or_unknown_animation_speed_falls_back_to_normal(string json) =>
        SettingsSerializer.Deserialize(json).Gameplay.AnimationSpeed.ShouldBe(AnimationSpeed.Normal);

    [Fact]
    public void Older_files_without_gameplay_keep_their_other_values() =>
        SettingsSerializer.Deserialize("{ \"audio\": { \"masterVolume\": 0.3 } }").ShouldSatisfyAllConditions(
            s => s.Audio.MasterVolume.ShouldBe(0.3),
            s => s.Gameplay.ShouldBe(new GameplaySettings()));

    [Theory]
    [InlineData(AnimationSpeed.Normal, 1.0)]
    [InlineData(AnimationSpeed.Fast, 0.5)]
    [InlineData(AnimationSpeed.Off, 0.0)]
    public void Duration_scale_follows_the_speed(AnimationSpeed speed, double expected) =>
        new GameplaySettings { AnimationSpeed = speed }.DurationScale.ShouldBe(expected);
}
