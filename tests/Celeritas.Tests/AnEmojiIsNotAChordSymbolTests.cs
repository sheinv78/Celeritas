// Copyright (c) 2025 Vladimir V. Shein

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

/// <summary>
/// A character outside the Basic Multilingual Plane — an emoji, the musical symbols block that
/// holds 𝄞 and 𝅘𝅥 — is two UTF-16 units. Both ANTLR wrappers fed the lexer those units one at a
/// time, so the lexer saw a lone high surrogate, and its own error display threw
/// <see cref="ArgumentException"/> on it before any error listener was told.
/// <para>
/// For the chord symbol parser that broke a promise: <c>TryParseChordSymbol</c> exists so a
/// caller can tell "not a chord" from an exception, and <c>ParseChordSymbol</c> documents an empty
/// array for anything it cannot parse. The native export swallowed the exception and answered
/// "refused", so the managed library and its C export disagreed on the same string — the
/// disagreement the parity table in <see cref="ThreeImplementationsAgreeTests"/> found first.
/// </para>
/// </summary>
public class AnEmojiIsNotAChordSymbolTests
{
    [Theory]
    [InlineData("🎵")]
    [InlineData("C🎵7")]
    [InlineData("𝄞")]
    [InlineData("C/𝄢")]
    public void TryParseChordSymbol_CharacterOutsideTheBasicPlane_IsRefusedNotThrown(string symbol)
    {
        var parsed = ProgressionAdvisor.TryParseChordSymbol(symbol, out var pitches, out var errors);

        Assert.False(parsed);
        Assert.Empty(pitches);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ParseChordSymbol_CharacterOutsideTheBasicPlane_ParsesToNothing()
    {
        Assert.Empty(ProgressionAdvisor.ParseChordSymbol("🎵"));
    }

    [Theory]
    [InlineData("🎵")]
    [InlineData("C4/4 𝅘𝅥 E4/4")]
    public void MusicNotationParse_CharacterOutsideTheBasicPlane_IsAParseError(string notation)
    {
        // The contract is an ArgumentException either way; what it says has to be about the
        // notation, not about the encoding of the string that carried it.
        var error = Assert.Throws<ArgumentException>(() => MusicNotation.Parse(notation));

        Assert.StartsWith("Parse errors", error.Message);
        Assert.DoesNotContain("surrogate", error.Message);
    }

    [Fact]
    public void ChordSymbols_InsideTheBasicPlane_StillParse()
    {
        // The stream the lexer reads changed; the symbols with Unicode accidentals, which are
        // inside the plane, read as they did.
        Assert.Equal([60, 64, 67, 70, 73, 78], ProgressionAdvisor.ParseChordSymbol("C7(♭9,♯11)"));
        Assert.Equal([61, 65, 68], ProgressionAdvisor.ParseChordSymbol("D♭"));
        Assert.Equal([60, 64, 67, 71], ProgressionAdvisor.ParseChordSymbol("CΔ7"));
    }
}
