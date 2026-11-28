using System.Globalization;
using System.Text;
using FactsPDF.CssSyntax;

namespace FactsPDF;

/// <summary>
/// CSS animations as a printed page shows them. A page is written at animation time zero: an animation
/// that has started shows the keyframe its direction starts from, an animation still waiting for its
/// delay shows its first keyframe only with a backwards fill, and one that has already ended shows its
/// last keyframe only with a forwards fill. Those states are exactly a keyframe at offset 0% or 100%,
/// laid over the cascade like a declaration of the animations origin (above every normal declaration,
/// below !important). A state between the keyframes (a negative delay that lands inside an iteration,
/// a fractional iteration count, an easing that outputs an intermediate value at the start) would need
/// interpolation and fails explicitly.
/// </summary>
internal static class CssAnimations
{
    /// <summary>Separates the layers of an animation longhand held as one value.</summary>
    internal const char Separator = '\u001f';

    internal enum Outcome { None, From, To, Interpolated }

    /// <summary>Whether the property is one of the animation longhands the cascade carries.</summary>
    internal static bool IsCarried(string name) => name is "animation" or "animation-name" or "animation-duration"
        or "animation-delay" or "animation-iteration-count" or "animation-direction" or "animation-fill-mode"
        or "animation-timing-function";

    internal static CssProperty Property(string longhand) => longhand switch
    {
        "animation-name" => CssProperty.AnimationName,
        "animation-duration" => CssProperty.AnimationDuration,
        "animation-delay" => CssProperty.AnimationDelay,
        "animation-iteration-count" => CssProperty.AnimationIterationCount,
        "animation-direction" => CssProperty.AnimationDirection,
        "animation-fill-mode" => CssProperty.AnimationFillMode,
        "animation-timing-function" => CssProperty.AnimationTimingFunction,
        _ => throw new ArgumentOutOfRangeException(nameof(longhand))
    };

    private static readonly string[] Longhands =
    [
        "animation-name", "animation-duration", "animation-delay", "animation-iteration-count",
        "animation-direction", "animation-fill-mode", "animation-timing-function"
    ];

    private static bool Wide(string text) => text is "inherit" or "initial" or "unset" or "revert" or "revert-layer";

    /// <summary>The longhand declarations a validated animation declaration stands for: the longhand
    /// itself with its layers, or for the shorthand all seven with the initial value for what a layer
    /// leaves out. A CSS-wide keyword applies to every longhand it covers. animation-play-state, which
    /// changes nothing at time zero, yields none.</summary>
    internal static List<(CssProperty Property, string Name, string Value)> Expand(CssDeclarationNode node, string name)
    {
        var result = new List<(CssProperty, string, string)>(7);
        var terms = node.Values.Where(x => x is not CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }).ToArray();
        if (terms.Length == 1 && terms[0] is CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } word }
            && Wide(word.Value.ToLowerInvariant()))
        {
            var keyword = word.Value.ToLowerInvariant();
            foreach (var longhand in name == "animation" ? Longhands : [name]) result.Add((Property(longhand), longhand, keyword));
            return result;
        }
        var layers = Layers(node.Values);
        if (name != "animation")
        {
            result.Add((Property(name), name, string.Join(Separator, layers.Select(layer => Text(layer[0])))));
            return result;
        }
        var parsed = layers.Select(Shorthand).ToArray();
        string Join(Func<Entry, string> select) => string.Join(Separator, parsed.Select(select));
        result.Add((CssProperty.AnimationName, "animation-name", Join(e => e.Name)));
        result.Add((CssProperty.AnimationDuration, "animation-duration", Join(e => e.Duration)));
        result.Add((CssProperty.AnimationDelay, "animation-delay", Join(e => e.Delay)));
        result.Add((CssProperty.AnimationIterationCount, "animation-iteration-count", Join(e => e.Count)));
        result.Add((CssProperty.AnimationDirection, "animation-direction", Join(e => e.Direction)));
        result.Add((CssProperty.AnimationFillMode, "animation-fill-mode", Join(e => e.Fill)));
        result.Add((CssProperty.AnimationTimingFunction, "animation-timing-function", Join(e => e.Easing)));
        return result;
    }

    private static List<List<CssSyntaxComponent>> Layers(IReadOnlyList<CssSyntaxComponent> values)
    {
        var layers = new List<List<CssSyntaxComponent>> { new() };
        foreach (var value in values)
        {
            if (value is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Whitespace }) continue;
            if (value is CssTokenComponent { Token.Kind: CssSyntaxTokenKind.Comma }) { layers.Add([]); continue; }
            layers[^1].Add(value);
        }
        return layers;
    }

    private static string Text(CssSyntaxComponent component) => component switch
    {
        CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.String } token } => token.Value,
        CssTokenComponent { Token: { Kind: CssSyntaxTokenKind.Ident } token } => token.Value.ToLowerInvariant() is var lower
            && (lower is "none" or "normal" or "reverse" or "alternate" or "alternate-reverse" or "forwards" or "backwards"
                or "both" or "infinite" or "auto" or "ease" or "linear" or "ease-in" or "ease-out" or "ease-in-out"
                or "step-start" or "step-end") ? lower : token.Value,
        CssTokenComponent { Token: var token } => token.Raw,
        CssFunctionComponent function => Function(function),
        _ => ""
    };

    private static string Function(CssFunctionComponent function)
    {
        var builder = new StringBuilder(function.Name.ToLowerInvariant()).Append('(');
        foreach (var value in function.Values)
            if (value is CssTokenComponent { Token: var token } && token.Kind != CssSyntaxTokenKind.Whitespace) builder.Append(token.Raw);
        return builder.Append(')').ToString();
    }

    private readonly record struct Entry(string Name, string Duration, string Delay, string Count, string Direction, string Fill, string Easing);

    // One layer of the shorthand: a keyword goes to the first slot it can fill, the name is the identifier left.
    private static Entry Shorthand(List<CssSyntaxComponent> layer)
    {
        string? name = null, duration = null, delay = null, count = null, direction = null, fill = null, easing = null;
        foreach (var component in layer)
        {
            if (component is CssFunctionComponent function) { easing = Function(function); continue; }
            if (component is not CssTokenComponent { Token: var token }) continue;
            switch (token.Kind)
            {
                case CssSyntaxTokenKind.Dimension:
                    if (duration is null) duration = token.Raw.ToLowerInvariant(); else delay = token.Raw.ToLowerInvariant();
                    continue;
                case CssSyntaxTokenKind.Number: count = token.Value; continue;
                case CssSyntaxTokenKind.String: name = token.Value; continue;
            }
            var term = token.Value.ToLowerInvariant();
            if (term == "auto" && duration is null) duration = "0s";
            else if (term is "ease" or "linear" or "ease-in" or "ease-out" or "ease-in-out" or "step-start" or "step-end" && easing is null) easing = term;
            else if (term == "infinite" && count is null) count = term;
            else if (term is "normal" or "reverse" or "alternate" or "alternate-reverse" && direction is null) direction = term;
            else if (term is "forwards" or "backwards" or "both" && fill is null) fill = term;
            else if (term is "running" or "paused") { }
            else if (term == "none" && name is null && fill is not null) name = term;
            else if (term == "none" && fill is null && name is null && layer.Count == 1) name = term;
            else if (term == "none" && fill is null) fill = term;
            else name = token.Value;
        }
        return new(name ?? "none", duration ?? "0s", delay ?? "0s", count ?? "1", direction ?? "normal", fill ?? "none", easing ?? "ease");
    }

    /// <summary>The layers of an animation longhand value built by <see cref="Expand"/>.</summary>
    internal static string[] Split(string value) => value.Split(Separator);

    // A time in seconds, or null when it is not a time.
    private static double? Seconds(string text)
    {
        text = text.Trim().ToLowerInvariant();
        var scale = 1d;
        if (text.EndsWith("ms", StringComparison.Ordinal)) { scale = 0.001; text = text[..^2]; }
        else if (text.EndsWith('s')) text = text[..^1];
        else return null;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value * scale : null;
    }

    /// <summary>The animation's value of a longhand layer (the list repeats to the length of the name list).</summary>
    internal static string Layer(string[] layers, int index) => layers[index % layers.Length];

    /// <summary>The keyframe state an animation shows at time zero.</summary>
    internal static Outcome StateAtZero(string duration, string delay, string count, string direction, string fill, string easing)
    {
        var length = duration == "auto" ? 0 : Seconds(duration) ?? 0;
        var start = Seconds(delay) ?? 0;
        var iterations = count == "infinite" ? double.PositiveInfinity
            : double.TryParse(count, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 1;
        var backwards = fill is "backwards" or "both";
        var forwards = fill is "forwards" or "both";
        // The iteration (k) whose simple progress (0 or 1) is shown.
        int iteration; double simple;
        if (start > 0)
        {
            // Before the start: only a backwards fill shows the first iteration's start.
            if (!backwards) return Outcome.None;
            iteration = 0; simple = 0;
        }
        else
        {
            var active = -start;
            var activeDuration = length == 0 || iterations == 0 ? 0 : length * iterations;
            if (active >= activeDuration)
            {
                // After the end: only a forwards fill keeps the last iteration's end.
                if (!forwards) return Outcome.None;
                if (iterations == 0) { iteration = 0; simple = 0; }
                else if (iterations == Math.Floor(iterations)) { iteration = (int)iterations - 1; simple = 1; }
                else return Outcome.Interpolated;
            }
            else
            {
                var overall = active / length;
                iteration = (int)Math.Floor(overall);
                if (overall != iteration) return Outcome.Interpolated;
                simple = 0;
            }
        }
        var forward = direction switch
        {
            "reverse" => false,
            "alternate" => iteration % 2 == 0,
            "alternate-reverse" => iteration % 2 != 0,
            _ => true
        };
        var directed = forward ? simple : 1 - simple;
        var eased = Ease(easing, directed);
        return eased switch { 0 => Outcome.From, 1 => Outcome.To, _ => Outcome.Interpolated };
    }

    // The easing function's output at progress 0 or 1: cubic Beziers and the keyword curves map the ends to
    // themselves; a step function that jumps at the start or on both ends does not.
    private static double Ease(string easing, double progress)
    {
        easing = easing.ToLowerInvariant();
        if (easing == "step-start") return 1;
        if (!easing.StartsWith("steps(", StringComparison.Ordinal)) return progress;
        var inner = easing[6..^1].Split(',', StringSplitOptions.TrimEntries);
        if (!int.TryParse(inner[0], NumberStyles.None, CultureInfo.InvariantCulture, out var steps) || steps < 1) return 0.5;
        var position = inner.Length > 1 ? inner[1] : "end";
        if (progress != 0) return 1;
        return position switch
        {
            "start" or "jump-start" => 1d / steps,
            "jump-both" => 1d / (steps + 1),
            _ => 0
        };
    }
}
