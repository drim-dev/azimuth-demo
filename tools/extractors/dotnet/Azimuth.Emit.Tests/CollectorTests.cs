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
    private static Collector.Result Collect() =>
        Collector.Collect(
            [typeof(Azimuth.Fixture.Production).Assembly],
            Directory.GetCurrentDirectory());

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

    [Fact]
    public void Symbols_are_emitted_independently_of_linkage_tags()
    {
        var artifact = Assert.Single(
            Collect().Artifacts,
            artifact => artifact.Id == "dotnet-symbol:Azimuth.Fixture.Production.Untagged");
        Assert.Equal("dotnet-method", artifact.Kind);
        Assert.EndsWith("Fixture.cs", artifact.File);
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
        Assert.Equal("universal", entry.Quantification);
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

    /// <summary>
    /// A finding reported against "some assembly" is a finding nobody acts on.
    /// </summary>
    [Fact]
    public void Sites_carry_a_source_path()
    {
        Assert.All(Collect().Realizes, entry => Assert.EndsWith("Fixture.cs", entry.File));
    }

    /// <summary>
    /// Most tagged methods in a service are async, and an async method's sequence points live on
    /// the compiler-generated state machine rather than on the method the tag sits on. Without the
    /// fallback the manifest carried almost no paths, which stayed invisible until the agent tier
    /// needed to fingerprint over evidence files.
    /// </summary>
    [Fact]
    public void An_async_method_still_carries_a_source_path()
    {
        var entry = Assert.Single(Collect().Realizes.Where(r => r.Scenario == "async-thing"));
        Assert.EndsWith("Fixture.cs", entry.File);
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
        using var document = JsonDocument.Parse(Collector.ToJson(Collect()));
        var root = document.RootElement;

        var realizes = root.GetProperty("realizes")[0];
        Assert.True(realizes.TryGetProperty("spec", out _));
        Assert.True(realizes.TryGetProperty("scenario", out _));
        Assert.False(realizes.TryGetProperty("req", out _));
        Assert.Equal("csharp", realizes.GetProperty("lang").GetString());
        Assert.False(realizes.TryGetProperty("scope", out _));

        Assert.NotEmpty(root.GetProperty("covers").EnumerateArray());
        Assert.NotEmpty(root.GetProperty("artifacts").EnumerateArray());
        Assert.False(root.TryGetProperty("untraced_tests", out _));
    }
}
