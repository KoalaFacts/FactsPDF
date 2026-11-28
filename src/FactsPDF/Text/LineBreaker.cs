using System.Globalization;
using System.Text;

namespace FactsPDF;

// The Line_Break property values of UAX #14, in the order scripts/generate_line_break_data.py numbers them.
internal enum LineBreakClass : byte
{
    XX, AI, AK, AL, AP, AS, B2, BA, BB, BK, CB, CJ, CL, CM, CP, CR, EB, EM, EX, GL, H2, H3, HL, HY, ID, IN, IS, JL, JT, JV,
    LF, NL, NS, NU, OP, PO, PR, QU, RI, SA, SG, SP, SY, VF, VI, WJ, ZW, ZWJ,
}

/// <summary>CSS word-break values that change where a line may break.</summary>
internal enum WordBreakMode : byte { Normal, KeepAll, BreakAll }

/// <summary>CSS line-break values: how strictly CJK non-starters are kept off the start of a line.</summary>
internal enum LineBreakStrictness : byte { Normal, Strict, Loose, Anywhere }

/// <summary>
/// The Unicode line breaking algorithm (UAX #14 rules LB2 to LB31) over a whole text. Each position
/// gets <see cref="No"/>, <see cref="Allowed"/> (a soft wrap opportunity) or <see cref="Mandatory"/>
/// for the boundary before it. An inline atom (a negative scalar) behaves as a conditional break
/// (CB), which LB20 breaks around. The analyzer keeps its work arrays between calls, so a thread
/// that wraps many paragraphs allocates nothing after the first.
/// </summary>
/// <remarks>
/// The result follows what Chrome prints rather than the bare default of the standard: a break is
/// allowed after every space whatever follows (Blink does not apply LB14 to LB17 to spaces), two
/// printable ASCII characters break by Blink's own table, and a hyphen or en dash that starts a
/// word is not split from the letter after it. word-break:keep-all, word-break:break-all and
/// line-break:strict, loose and anywhere change the result as Chrome does.
/// </remarks>
internal sealed class LineBreakAnalyzer
{
    internal const byte No = 0, Allowed = 1, Mandatory = 2;

    private byte[] unitClass = [];
    private int[] unitScalar = [];
    private int[] unitStart = [];
    private bool[] unitEndsWithJoiner = [];
    private bool[] unitIsComplexScript = [];
    private byte[] unitNumber = [];
    private byte[] breaks = [];

    private int charCount, decided, lastNonSpaceUnit, regionalIndicatorRun;
    private bool finished;

    /// <summary>The break opportunity before each scalar; the result is valid until the next call.</summary>
    /// <param name="text">The scalars, or a negative value for an atomic inline.</param>
    /// <param name="strictNonStarters">Resolve CJ (small kana and the like) as NS (line-break:strict) instead of ID.</param>
    internal ReadOnlySpan<byte> Analyze(ReadOnlySpan<int> text, bool strictNonStarters)
        => Analyze(text, strictNonStarters ? LineBreakStrictness.Strict : LineBreakStrictness.Normal, WordBreakMode.Normal);

    /// <summary>The break opportunity before each scalar under the given line-break and word-break values.</summary>
    internal ReadOnlySpan<byte> Analyze(ReadOnlySpan<int> text, LineBreakStrictness strictness, WordBreakMode wordBreak,
        CancellationToken cancellation = default)
    {
        Reset(strictness, wordBreak);
        Append(text, final: true, cancellation);
        return Breaks;
    }

    /// <summary>Starts a new text.</summary>
    internal void Reset(LineBreakStrictness strictness, WordBreakMode wordBreak)
    {
        this.wordBreak = wordBreak;
        this.strictness = strictness;
        charCount = unitCount = 0;
        decided = 1; lastNonSpaceUnit = -1; regionalIndicatorRun = 0; finished = false;
    }

    /// <summary>The opportunity before each appended scalar; only the first <see cref="FinalCount"/> are decided.</summary>
    internal ReadOnlySpan<byte> Breaks => breaks.AsSpan(0, charCount);

    /// <summary>How many leading scalars have their break decision final: those before the first unit that still lacks lookahead.</summary>
    internal int FinalCount => finished || decided >= unitCount ? charCount : unitStart[decided];

    /// <summary>
    /// Appends scalars and decides every boundary that now has the units it reads ahead (two); with
    /// <paramref name="final"/> the text ends here and the rest is decided. Work is proportional to the appended
    /// scalars, not to the text so far.
    /// </summary>
    internal void Append(ReadOnlySpan<int> text, bool final, CancellationToken cancellation = default)
    {
        var need = charCount + text.Length;
        if (breaks.Length < need)
        {
            var size = Math.Max(need, breaks.Length * 2);
            Array.Resize(ref unitClass, size); Array.Resize(ref unitScalar, size); Array.Resize(ref unitStart, size);
            Array.Resize(ref unitEndsWithJoiner, size); Array.Resize(ref unitIsComplexScript, size);
            Array.Resize(ref unitNumber, size); Array.Resize(ref breaks, size);
        }
        for (var i = 0; i < text.Length; i++)
        {
            if ((i & 1023) == 0) cancellation.ThrowIfCancellationRequested();
            var scalar = text[i];
            var at = charCount++;
            breaks[at] = No;
            var cls = scalar < 0 ? LineBreakClass.CB : Resolve(scalar, strictness, wordBreak);
            var units = unitCount;
            if (cls is LineBreakClass.CM or LineBreakClass.ZWJ)
            {
                // LB9: a combining mark or ZWJ joins the unit before it, unless that is a break, a space or ZW;
                // LB10: otherwise it is a letter of its own.
                if (units > 0 && (LineBreakClass)unitClass[units - 1] is not (LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF
                    or LineBreakClass.NL or LineBreakClass.SP or LineBreakClass.ZW))
                {
                    unitEndsWithJoiner[units - 1] = cls == LineBreakClass.ZWJ;
                    continue;
                }
                unitClass[units] = (byte)LineBreakClass.AL;
                unitEndsWithJoiner[units] = cls == LineBreakClass.ZWJ;
            }
            else
            {
                unitClass[units] = (byte)cls;
                unitEndsWithJoiner[units] = false;
            }
            unitScalar[units] = scalar; unitStart[units] = at;
            unitIsComplexScript[units] = scalar >= 0 && ClassOf(scalar) == LineBreakClass.SA;
            // 1: a number NU (SY | IS)*, 2: such a number closed by CL or CP (the prefixes of LB25).
            var inNumber = units > 0 && unitNumber[units - 1] == 1;
            unitNumber[units] = (byte)(cls == LineBreakClass.NU ? 1
                : (LineBreakClass)unitClass[units] is LineBreakClass.SY or LineBreakClass.IS && inNumber ? 1
                : (LineBreakClass)unitClass[units] is LineBreakClass.CL or LineBreakClass.CP && inNumber ? 2 : 0);
            unitCount = units + 1;
        }
        finished = final;
        var limit = final ? unitCount : unitCount - 2;
        var work = 0;
        for (; decided < limit; decided++)
        {
            if ((++work & 1023) == 0) cancellation.ThrowIfCancellationRequested();
            var k = decided;
            if ((LineBreakClass)unitClass[k - 1] != LineBreakClass.SP) lastNonSpaceUnit = k - 1;
            regionalIndicatorRun = (LineBreakClass)unitClass[k - 1] == LineBreakClass.RI ? regionalIndicatorRun + 1 : 0;
            breaks[unitStart[k]] = Decide(k, lastNonSpaceUnit, regionalIndicatorRun);
        }
    }

    /// <summary>
    /// Forgets the first scalars, up to <paramref name="count"/> and ending at a unit boundary, and returns how many
    /// went. The rules that read behind keep what they need only while a few units stay, so callers keep some.
    /// </summary>
    internal int Discard(int count)
    {
        var drop = 0;
        while (drop + 1 < decided && unitStart[drop + 1] <= count) drop++;
        if (drop == 0) return 0;
        var chars = unitStart[drop];
        Array.Copy(unitClass, drop, unitClass, 0, unitCount - drop);
        Array.Copy(unitScalar, drop, unitScalar, 0, unitCount - drop);
        Array.Copy(unitEndsWithJoiner, drop, unitEndsWithJoiner, 0, unitCount - drop);
        Array.Copy(unitIsComplexScript, drop, unitIsComplexScript, 0, unitCount - drop);
        Array.Copy(unitNumber, drop, unitNumber, 0, unitCount - drop);
        Array.Copy(unitStart, drop, unitStart, 0, unitCount - drop);
        for (var i = 0; i < unitCount - drop; i++) unitStart[i] -= chars;
        Array.Copy(breaks, chars, breaks, 0, charCount - chars);
        unitCount -= drop; decided -= drop; charCount -= chars;
        lastNonSpaceUnit = lastNonSpaceUnit >= drop ? lastNonSpaceUnit - drop : -1;
        return chars;
    }

    // Sentinels outside the real classes; only the rules that name sot and eot compare with them.
    private const LineBreakClass SotClass = (LineBreakClass)200, EotClass = (LineBreakClass)201;
    private int unitCount;

    private LineBreakClass Class(int unit) =>
        unit < 0 ? SotClass : unit >= unitCount ? EotClass : (LineBreakClass)unitClass[unit];

    private WordBreakMode wordBreak;
    private LineBreakStrictness strictness;

    private byte Decide(int k, int lastNonSpace, int regionalIndicators)
    {
        var standard = StandardDecision(k, lastNonSpace, regionalIndicators);
        if (standard == Mandatory || (LineBreakClass)unitClass[k] is LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL)
            return standard;
        var a = Class(k - 1);
        var b = Class(k);
        var aScalar = unitScalar[k - 1];
        var bScalar = unitScalar[k];
        // line-break:anywhere breaks between any two typographic character units.
        if (strictness == LineBreakStrictness.Anywhere)
            return unitEndsWithJoiner[k - 1] || b == LineBreakClass.EM || a == LineBreakClass.RI && b == LineBreakClass.RI && regionalIndicators % 2 == 1
                || IsHangulSyllableSequence(a, b) ? No : Allowed;
        if (strictness == LineBreakStrictness.Loose && a == LineBreakClass.IN && b == LineBreakClass.IN) return Allowed;
        // Blink breaks after every run of spaces, whatever follows it.
        if (a == LineBreakClass.SP) return b is LineBreakClass.SP or LineBreakClass.ZW or LineBreakClass.WJ ? standard : Allowed;
        var breakAll = wordBreak == WordBreakMode.BreakAll;
        if (unitStart[k] == unitStart[k - 1] + 1 && AsciiLineBreakTables.IsCovered(aScalar) && AsciiLineBreakTables.IsCovered(bScalar))
        {
            // Two adjacent printable ASCII characters: Blink's table, where a minus sign before a digit
            // only splits from a letter or digit that precedes it.
            if (aScalar == '-' && bScalar is >= '0' and <= '9' && !breakAll)
                return k >= 2 && unitScalar[k - 2] is >= 0 and < 128 && char.IsAsciiLetterOrDigit((char)unitScalar[k - 2]) ? Allowed : No;
            return AsciiLineBreakTables.Allows(aScalar, bScalar, breakAll) ? Allowed : No;
        }
        // A hyphen or dash that starts a word stays with the letter after it.
        if (a is LineBreakClass.HY or LineBreakClass.BA && b is LineBreakClass.AL or LineBreakClass.HL or LineBreakClass.SA
            && Class(k - 2) is SotClass or LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL
                or LineBreakClass.SP or LineBreakClass.ZW or LineBreakClass.CB or LineBreakClass.GL)
            return No;
        // keep-all: no break between two letters, ideographs, kana or Hangul syllables.
        if (wordBreak == WordBreakMode.KeepAll && standard == Allowed && IsLetterUnit(a) && IsLetterUnit(b)
            && !unitIsComplexScript[k - 1] && !unitIsComplexScript[k]) return No;
        return standard;
    }

    private static bool IsHangulSyllableSequence(LineBreakClass a, LineBreakClass b) =>
        a == LineBreakClass.JL && b is LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.H2 or LineBreakClass.H3
        || a is LineBreakClass.JV or LineBreakClass.H2 && b is LineBreakClass.JV or LineBreakClass.JT
        || a is LineBreakClass.JT or LineBreakClass.H3 && b == LineBreakClass.JT;

    private static bool IsLetterUnit(LineBreakClass cls) => cls is LineBreakClass.AL or LineBreakClass.NU or LineBreakClass.ID
        or LineBreakClass.H2 or LineBreakClass.H3 or LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.JT
        or LineBreakClass.HL or LineBreakClass.NS;

    private byte StandardDecision(int k, int lastNonSpace, int regionalIndicators)
    {
        var a = Class(k - 1);
        var b = Class(k);
        var ns = Class(lastNonSpace);
        var beforeNs = Class(lastNonSpace - 1);
        var aScalar = unitScalar[k - 1];
        var bScalar = unitScalar[k];
        var next = Class(k + 1);
        // LB4 to LB6: hard breaks.
        if (a == LineBreakClass.BK) return Mandatory;
        if (a == LineBreakClass.CR) return b == LineBreakClass.LF ? No : Mandatory;
        if (a is LineBreakClass.LF or LineBreakClass.NL) return Mandatory;
        if (b is LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL) return No;
        // LB7, LB8, LB8a.
        if (b is LineBreakClass.SP or LineBreakClass.ZW) return No;
        if (ns == LineBreakClass.ZW) return Allowed;
        if (unitEndsWithJoiner[k - 1]) return No;
        // LB11, LB12, LB12a.
        if (b == LineBreakClass.WJ || a == LineBreakClass.WJ) return No;
        if (a == LineBreakClass.GL) return No;
        if (b == LineBreakClass.GL && a is not (LineBreakClass.SP or LineBreakClass.BA or LineBreakClass.HY)) return No;
        // LB13, LB14.
        if (b is LineBreakClass.CL or LineBreakClass.CP or LineBreakClass.EX or LineBreakClass.SY) return No;
        if (ns == LineBreakClass.OP) return No;
        // LB15a to LB15d.
        if (ns == LineBreakClass.QU && IsInitialQuote(unitScalar[lastNonSpace]) && beforeNs is SotClass or LineBreakClass.BK
            or LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL or LineBreakClass.OP or LineBreakClass.QU
            or LineBreakClass.GL or LineBreakClass.SP or LineBreakClass.ZW)
            return No;
        if (b == LineBreakClass.QU && IsFinalQuote(bScalar) && next is EotClass or LineBreakClass.SP or LineBreakClass.GL
            or LineBreakClass.WJ or LineBreakClass.CL or LineBreakClass.QU or LineBreakClass.CP or LineBreakClass.EX
            or LineBreakClass.IS or LineBreakClass.SY or LineBreakClass.BK or LineBreakClass.CR or LineBreakClass.LF
            or LineBreakClass.NL or LineBreakClass.ZW)
            return No;
        if (a == LineBreakClass.SP && b == LineBreakClass.IS && next == LineBreakClass.NU) return Allowed;
        if (b == LineBreakClass.IS) return No;
        // LB16, LB17.
        if (ns is LineBreakClass.CL or LineBreakClass.CP && b == LineBreakClass.NS) return No;
        if (ns == LineBreakClass.B2 && b == LineBreakClass.B2) return No;
        // LB18.
        if (a == LineBreakClass.SP) return Allowed;
        // LB19, LB19a.
        if (b == LineBreakClass.QU && !IsInitialQuote(bScalar)) return No;
        if (a == LineBreakClass.QU && !IsFinalQuote(aScalar)) return No;
        if (b == LineBreakClass.QU && (!IsEastAsian(aScalar) || next == EotClass || !IsEastAsian(unitScalar[k + 1]))) return No;
        if (a == LineBreakClass.QU && (!IsEastAsian(bScalar) || k == 1 || !IsEastAsian(unitScalar[k - 2]))) return No;
        // LB20, LB20a.
        if (a == LineBreakClass.CB || b == LineBreakClass.CB) return Allowed;
        if ((a == LineBreakClass.HY || aScalar == 0x2010) && b == LineBreakClass.AL && Class(k - 2) is SotClass or LineBreakClass.BK
            or LineBreakClass.CR or LineBreakClass.LF or LineBreakClass.NL or LineBreakClass.SP or LineBreakClass.ZW
            or LineBreakClass.CB or LineBreakClass.GL)
            return No;
        // LB21, LB21a, LB21b, LB22.
        if (b is LineBreakClass.BA or LineBreakClass.HY or LineBreakClass.NS || a == LineBreakClass.BB) return No;
        if ((a == LineBreakClass.HY || a == LineBreakClass.BA && !IsEastAsian(aScalar)) && Class(k - 2) == LineBreakClass.HL
            && b != LineBreakClass.HL)
            return No;
        if (a == LineBreakClass.SY && b == LineBreakClass.HL) return No;
        if (b == LineBreakClass.IN) return No;
        // LB23, LB23a, LB24.
        var aLetter = a is LineBreakClass.AL or LineBreakClass.HL;
        var bLetter = b is LineBreakClass.AL or LineBreakClass.HL;
        if (aLetter && b == LineBreakClass.NU || a == LineBreakClass.NU && bLetter) return No;
        if (a == LineBreakClass.PR && b is LineBreakClass.ID or LineBreakClass.EB or LineBreakClass.EM
            || a is LineBreakClass.ID or LineBreakClass.EB or LineBreakClass.EM && b == LineBreakClass.PO) return No;
        if (a is LineBreakClass.PR or LineBreakClass.PO && bLetter || aLetter && b is LineBreakClass.PR or LineBreakClass.PO) return No;
        // LB25.
        var number = unitNumber[k - 1];
        if (number != 0 && b is LineBreakClass.PO or LineBreakClass.PR) return No;
        if (number == 1 && b == LineBreakClass.NU) return No;
        if (a is LineBreakClass.PO or LineBreakClass.PR)
        {
            if (b == LineBreakClass.NU) return No;
            if (b == LineBreakClass.OP && (next == LineBreakClass.NU
                || next == LineBreakClass.IS && Class(k + 2) == LineBreakClass.NU)) return No;
        }
        if (a is LineBreakClass.HY or LineBreakClass.IS && b == LineBreakClass.NU) return No;
        // LB26, LB27.
        if (a == LineBreakClass.JL && b is LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.H2 or LineBreakClass.H3) return No;
        if (a is LineBreakClass.JV or LineBreakClass.H2 && b is LineBreakClass.JV or LineBreakClass.JT) return No;
        if (a is LineBreakClass.JT or LineBreakClass.H3 && b == LineBreakClass.JT) return No;
        var aKorean = a is LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.JT or LineBreakClass.H2 or LineBreakClass.H3;
        var bKorean = b is LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.JT or LineBreakClass.H2 or LineBreakClass.H3;
        if (aKorean && b == LineBreakClass.PO || a == LineBreakClass.PR && bKorean) return No;
        // LB28, LB28a.
        if (aLetter && bLetter) return No;
        if (a == LineBreakClass.AP && IsBrahmicBase(b, bScalar)) return No;
        if (IsBrahmicBase(a, aScalar) && b is LineBreakClass.VF or LineBreakClass.VI) return No;
        if (a == LineBreakClass.VI && k >= 2 && IsBrahmicBase(Class(k - 2), unitScalar[k - 2])
            && (b == LineBreakClass.AK || bScalar == 0x25CC)) return No;
        if (IsBrahmicBase(a, aScalar) && IsBrahmicBase(b, bScalar) && next == LineBreakClass.VF) return No;
        // LB29, LB30.
        if (a == LineBreakClass.IS && bLetter) return No;
        if ((aLetter || a == LineBreakClass.NU) && b == LineBreakClass.OP && !IsEastAsian(bScalar)) return No;
        if (a == LineBreakClass.CP && !IsEastAsian(aScalar) && (bLetter || b == LineBreakClass.NU)) return No;
        // LB30a, LB30b.
        if (a == LineBreakClass.RI && b == LineBreakClass.RI && regionalIndicators % 2 == 1) return No;
        if (b == LineBreakClass.EM && (a == LineBreakClass.EB
            || IsPictographic(aScalar) && Rune.GetUnicodeCategory(new Rune(aScalar)) == UnicodeCategory.OtherNotAssigned)) return No;
        // LB31.
        return Allowed;
    }

    private static bool IsBrahmicBase(LineBreakClass cls, int scalar) =>
        cls is LineBreakClass.AK or LineBreakClass.AS || scalar == 0x25CC;

    private static bool IsInitialQuote(int scalar) =>
        Rune.GetUnicodeCategory(new Rune(scalar)) == UnicodeCategory.InitialQuotePunctuation;

    private static bool IsFinalQuote(int scalar) =>
        Rune.GetUnicodeCategory(new Rune(scalar)) == UnicodeCategory.FinalQuotePunctuation;

    /// <summary>Whether the scalar is East Asian Fullwidth, Wide or Halfwidth, as UAX #14 names $EastAsian.</summary>
    internal static bool IsEastAsian(int scalar) => InPairs(LineBreakData.EastAsian, scalar);

    private static bool IsPictographic(int scalar) => InPairs(LineBreakData.Pictographic, scalar);

    private static bool InPairs(ReadOnlySpan<int> pairs, int scalar)
    {
        var low = 0;
        var high = pairs.Length / 2 - 1;
        while (low <= high)
        {
            var middle = (low + high) >> 1;
            if (scalar < pairs[2 * middle]) high = middle - 1;
            else if (scalar > pairs[2 * middle + 1]) low = middle + 1;
            else return true;
        }
        return false;
    }

    /// <summary>The Line_Break class of a scalar as the data file lists it (before LB1).</summary>
    internal static LineBreakClass ClassOf(int scalar)
    {
        var runs = LineBreakData.Runs;
        var key = (uint)scalar << 6;
        var low = 0;
        var high = runs.Length - 1;
        while (low < high)
        {
            var middle = (low + high + 1) >> 1;
            if (runs[middle] <= (key | 63)) low = middle;
            else high = middle - 1;
        }
        return (LineBreakClass)(runs[low] & 63);
    }

    // LB1: ambiguous, surrogate and unknown letters are alphabetic, complex-context letters are
    // combining marks when they combine and alphabetic otherwise, and conditional Japanese starters
    // are non-starters only under line-break:strict.
    private static LineBreakClass Resolve(int scalar, LineBreakStrictness strictness, WordBreakMode wordBreak)
    {
        var cls = ClassOf(scalar);
        switch (cls)
        {
            case LineBreakClass.AI or LineBreakClass.SG or LineBreakClass.XX:
                return LineBreakClass.AL;
            case LineBreakClass.SA:
                return Rune.GetUnicodeCategory(new Rune(scalar)) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
                    ? LineBreakClass.CM : wordBreak == WordBreakMode.BreakAll ? LineBreakClass.ID : LineBreakClass.AL;
            case LineBreakClass.CJ:
                return strictness == LineBreakStrictness.Strict ? LineBreakClass.NS : LineBreakClass.ID;
            case LineBreakClass.AL or LineBreakClass.NU or LineBreakClass.HL when wordBreak == WordBreakMode.BreakAll:
                return LineBreakClass.ID;
            // line-break:loose lets the iteration marks start a line.
            case LineBreakClass.NS when strictness == LineBreakStrictness.Loose && scalar is 0x3005 or 0x303B or 0x309D or 0x309E or 0x30FD or 0x30FE:
                return LineBreakClass.ID;
            default:
                return cls;
        }
    }
}
