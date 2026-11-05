using Godot;

namespace Gwota.Game.Ui;

/// <summary>BBCode formatting of match log lines.</summary>
public static class LogFormat
{
    private static readonly string ErrorColor = Palette.Error.ToHtml(includeAlpha: false);
    private static readonly string MutedColor = Palette.Muted.ToHtml(includeAlpha: false);

    public static string Error(string text) => $"[color=#{ErrorColor}]{text}[/color]";

    public static string Muted(string text) => $"[color=#{MutedColor}]{text}[/color]";
}
