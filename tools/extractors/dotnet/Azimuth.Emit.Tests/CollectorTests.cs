using System.Text.Json;
using Xunit;

namespace Azimuth.Emit.Tests;

/// <summary>
/// Tests against the synthetic fixture beside this project (D2). A silently wrong emitter produces
/// a green matrix, which is the exact failure the framework exists to prevent — so these assert on
/// the shape of what is emitted, not merely that something was.
/// </summary>
public sealed class CollectorTests
{
    private const string TracedRoot = "Azimuth.Fixture.Traced";

    private static Collector.Result Collect(params string[] tracedRoots) =>
        Collector.Collect(
            [typeof(Azimuth.Fixture.Production).Assembly],
            Directory.GetCurrentDirectory(),
            tracedRoots);

    [Fact]
    public void A_type_level_tag_names_its_site_by_the_type()
    {
        var entry = Assert.Single(
            Collect().Realizes.Where(r => r.Scenario == "type-level-thing"));
        Assert.Equal("alpha", entry.Spec);
        Assert.Equal("Azimuth.Fixture.Production", entry.Site);
    }

    [Fact]
    public void A_method_level_tag_names_its_site_by_type_and_method()
    {
        var entry = Assert.Single(
            Collect().Realizes.Where(r => r.Scenario == "method-level-thing"));
        Assert.Equal("Azimuth.Fixture.Production.Method", entry.Site);
    }

    [Fact]
    public void A_site_may_realize_several_claims()
    {
        var branches = Collect()
            .Realizes.Where(r => r.Site == "Azimuth.Fixture.Production.Branching")
            .Select(r => r.Scenario)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(["first-branch", "second-branch"], branches);
    }

    /// <summary>
    /// The attribute targets must match what the extractor walks, or a tag vanishes silently. A
    /// struct-level tag was rejected by the compiler until the annotation package was widened —
    /// found by tagging Money, which is itself the top-rung enforcement mechanism.
    /// </summary>
    [Fact]
    public void A_value_type_is_a_realization_site()
    {
        var entry = Assert.Single(
            Collect().Realizes.Where(r => r.Scenario == "struct-level-thing"));
        Assert.Equal("Azimuth.Fixture.Amount", entry.Site);
    }

    [Fact]
    public void Untagged_code_produces_nothing()
    {
        Assert.DoesNotContain(
            Collect().Realizes,
            r => r.Site.EndsWith(".Untagged", StringComparison.Ordinal));
    }

    /// <summary>Form is how a test checks, not a property of code.</summary>
    [Fact]
    public void Realizes_carries_no_form()
    {
        Assert.All(Collect().Realizes, entry =>
        {
            Assert.Null(entry.Scope);
            Assert.Null(entry.Quantification);
            Assert.Null(entry.Oracle);
        });
    }

    /// <summary>
    /// Enum arguments arrive boxed as their underlying integer, so an emitter that reads the value
    /// rather than resolving it against the declared type emits "1" instead of "component". The
    /// manifest speaks kebab-case because the specs do.
    /// </summary>
    [Fact]
    public void Covers_carries_its_form_in_kebab_case()
    {
        var entry = Assert.Single(
            Collect().Covers.Where(c => c.Site.EndsWith(".Covered", StringComparison.Ordinal)));
        Assert.Equal("component", entry.Scope);
        Assert.Equal("invariant", entry.Quantification);
    }

    [Fact]
    public void A_multi_word_oracle_becomes_kebab_case()
    {
        var entry = Assert.Single(
            Collect().Covers.Where(c => c.Site.EndsWith(".CoveredWithOracle", StringComparison.Ordinal)));
        Assert.Equal("e2e", entry.Scope);
        Assert.Equal("model-based", entry.Oracle);
    }

    [Fact]
    public void An_omitted_oracle_takes_its_default()
    {
        var entry = Assert.Single(
            Collect().Covers.Where(c => c.Site.EndsWith(".Covered", StringComparison.Ordinal)));
        Assert.Equal("direct", entry.Oracle);
    }

    /// <summary>The dual of an uncovered claim.</summary>
    [Fact]
    public void A_bare_test_in_a_traced_root_is_untraced()
    {
        var sites = Collect(TracedRoot).UntracedTests.Select(u => u.Site).ToList();
        Assert.Equal(["Azimuth.Fixture.Traced.Tests.Bare"], sites);
    }

    /// <summary>
    /// Holding every test in a repo to this would be noise, and partial adoption is what makes the
    /// ratchet work (D8).
    /// </summary>
    [Fact]
    public void A_bare_test_outside_every_traced_root_is_not_a_finding()
    {
        Assert.DoesNotContain(
            Collect(TracedRoot).UntracedTests,
            u => u.Site.Contains(".Untraced.", StringComparison.Ordinal));
    }

    [Fact]
    public void With_no_traced_root_nothing_is_untraced()
    {
        Assert.Empty(Collect().UntracedTests);
    }

    [Fact]
    public void An_exemption_is_honoured()
    {
        Assert.DoesNotContain(
            Collect(TracedRoot).UntracedTests,
            u => u.Site.EndsWith(".Exempt", StringComparison.Ordinal));
    }

    /// <summary>
    /// A finding reported against "some assembly" is a finding nobody acts on.
    /// </summary>
    [Fact]
    public void Sites_carry_a_source_path()
    {
        Assert.All(Collect().Realizes, entry => Assert.EndsWith("Fixture.cs", entry.File));
    }

    [Fact]
    public void Entries_are_ordered_so_the_manifest_diffs()
    {
        var scenarios = Collect().Realizes.Select(r => r.Scenario).ToList();
        Assert.Equal(scenarios.OrderBy(s => s, StringComparer.Ordinal), scenarios);
    }

    [Fact]
    public void The_manifest_is_keyed_on_the_pair()
    {
        using var document = JsonDocument.Parse(Collector.ToJson(Collect(TracedRoot)));
        var root = document.RootElement;

        var realizes = root.GetProperty("realizes")[0];
        Assert.True(realizes.TryGetProperty("spec", out _));
        Assert.True(realizes.TryGetProperty("scenario", out _));
        Assert.False(realizes.TryGetProperty("req", out _));
        Assert.Equal("csharp", realizes.GetProperty("lang").GetString());
        Assert.False(realizes.TryGetProperty("scope", out _));

        Assert.NotEmpty(root.GetProperty("covers").EnumerateArray());
        Assert.NotEmpty(root.GetProperty("untraced_tests").EnumerateArray());
    }
}
