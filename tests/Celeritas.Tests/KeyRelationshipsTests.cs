// Copyright (c) 2025 Vladimir V. Shein
// Licensed under the Business Source License 1.1

using Celeritas.Core;
using Celeritas.Core.Analysis;

namespace Celeritas.Tests;

public class KeyRelationshipsTests
{
    private static KeySignature Key(string root, bool isMajor) => new(root, isMajor);

    // ── AreCloselyRelated ────────────────────────────────────────────────────
    // Closely related = same key, relative, or differs by one accidental
    // (dominant/subdominant and their relatives). For C major: G, F, Am, Em, Dm.

    [Theory]
    [InlineData("C", true, "C", true, true)]    // same key
    [InlineData("C", true, "G", true, true)]    // dominant
    [InlineData("C", true, "F", true, true)]    // subdominant
    [InlineData("C", true, "A", false, true)]   // relative minor
    [InlineData("C", true, "E", false, true)]   // relative of dominant
    [InlineData("C", true, "D", false, true)]   // relative of subdominant
    [InlineData("C", true, "C", false, false)]  // parallel minor: 3 accidentals away
    [InlineData("C", true, "B", true, false)]   // distant major
    [InlineData("C", true, "F#", true, false)]  // tritone
    [InlineData("A", false, "C", true, true)]   // relative major
    [InlineData("A", false, "E", false, true)]  // minor dominant
    [InlineData("A", false, "D", false, true)]  // minor subdominant
    [InlineData("A", false, "G", true, true)]   // relative of minor dominant
    [InlineData("A", false, "F", true, true)]   // relative of minor subdominant
    [InlineData("A", false, "A", true, false)]  // parallel major
    [InlineData("A", false, "B", false, false)] // two accidentals away
    public void AreCloselyRelated_KnownPairs(string rootA, bool majorA, string rootB, bool majorB, bool expected)
    {
        var a = Key(rootA, majorA);
        var b = Key(rootB, majorB);

        Assert.Equal(expected, KeyRelationships.AreCloselyRelated(a, b));
        // Relationship is symmetric
        Assert.Equal(expected, KeyRelationships.AreCloselyRelated(b, a));
    }

    // ── Describe ─────────────────────────────────────────────────────────────

    [Fact]
    public void Describe_ChromaticMediants_AreReachable()
    {
        // Same-mode third relations previously shadowed by the unguarded 4/9 arms.
        Assert.Equal("chromatic mediant (up M3)", KeyRelationships.Describe(Key("C", true), Key("E", true)));
        Assert.Equal("chromatic mediant (down m3)", KeyRelationships.Describe(Key("C", true), Key("A", true)));
        Assert.Equal("chromatic mediant (up m3)", KeyRelationships.Describe(Key("C", true), Key("D#", true)));
        Assert.Equal("chromatic mediant (down M3)", KeyRelationships.Describe(Key("C", true), Key("G#", true)));
    }

    [Fact]
    public void Describe_ModeAwareRelations()
    {
        Assert.Equal("relative minor", KeyRelationships.Describe(Key("C", true), Key("A", false)));
        Assert.Equal("relative major", KeyRelationships.Describe(Key("A", false), Key("C", true)));
        Assert.Equal("parallel minor", KeyRelationships.Describe(Key("C", true), Key("C", false)));
        Assert.Equal("parallel major", KeyRelationships.Describe(Key("C", false), Key("C", true)));
        Assert.Equal("same key", KeyRelationships.Describe(Key("C", true), Key("C", true)));
        Assert.Equal("dominant key (V)", KeyRelationships.Describe(Key("C", true), Key("G", true)));
        Assert.Equal("subdominant key (IV)", KeyRelationships.Describe(Key("C", true), Key("F", true)));
        Assert.Equal("mediant key (iii)", KeyRelationships.Describe(Key("C", true), Key("E", false)));
        Assert.Equal("submediant key (VI)", KeyRelationships.Describe(Key("C", false), Key("G#", true)));
    }

    [Fact]
    public void CommonTones_RelativeKeys_ShareAllSeven()
    {
        Assert.Equal(7, KeyRelationships.CommonTones(Key("C", true), Key("A", false)));
        Assert.Equal(6, KeyRelationships.CommonTones(Key("C", true), Key("G", true)));
    }
}
