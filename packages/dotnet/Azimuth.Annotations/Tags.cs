using System;

namespace Azimuth.Annotations
{
    /// <summary>
    /// Declares that a production-code site is on a claim's path, by the stable
    /// <c>(spec-id, scenario-id)</c> pair.
    /// </summary>
    /// <remarks>
    /// The pair, not a triple: the requirement id is redundant because scenario ids are unique per
    /// spec, and redundancy that can go stale is a liability. Dropping it is what makes splitting or
    /// merging a requirement free — scenarios move between parents without a tag being touched.
    /// <para>
    /// Carries no form. Form is how a <em>test</em> checks a behaviour, not a property of code.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RealizesAttribute : Attribute
    {
        public RealizesAttribute(string spec, string scenario)
        {
            Spec = spec;
            Scenario = scenario;
        }

        public string Spec { get; }

        public string Scenario { get; }
    }

    /// <summary>
    /// Declares that a test verifies a claim, at the form the test <em>actually</em> has.
    /// </summary>
    /// <remarks>
    /// What the form must <em>be</em> lives in the verification plan; this declares what it is, and
    /// the comparison between the two is <c>wrong-form</c>. Declaring it here rather than inferring
    /// it keeps the seam between generating a test and judging its sufficiency visible.
    /// <para>
    /// <see cref="Oracle"/> records how the expected result was obtained. Descriptive, never gated.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class CoversAttribute : Attribute
    {
        public CoversAttribute(
            string spec,
            string scenario,
            Scope scope,
            Quantification quantification,
            Oracle oracle = Oracle.Direct)
        {
            Spec = spec;
            Scenario = scenario;
            Scope = scope;
            Quantification = quantification;
            Oracle = oracle;
        }

        public string Spec { get; }

        public string Scenario { get; }

        public Scope Scope { get; }

        public Quantification Quantification { get; }

        public Oracle Oracle { get; }
    }

    /// <summary>
    /// Opts a test out of tracing: it legitimately covers no claim — setup, infrastructure, a smoke
    /// check.
    /// </summary>
    /// <remarks>
    /// A deliberate, attributable, reviewable exemption is fine anywhere; a silent absence is not.
    /// The <paramref name="reason"/> is what makes it the former.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class UntracedAttribute : Attribute
    {
        public UntracedAttribute(string reason)
        {
            Reason = reason;
        }

        public string Reason { get; }
    }

    /// <summary>
    /// How much of the real system a check runs against — defined by what must be <em>real</em>,
    /// not by how much runs.
    /// </summary>
    /// <remarks>
    /// Defined this way the rungs are partly machine-checkable rather than purely self-declared: a
    /// harness knows whether it started a real database, so a test using an in-memory repository
    /// cannot honestly claim <see cref="Component"/>.
    /// </remarks>
    public enum Scope
    {
        /// <summary>Nothing external; all collaborators substituted.</summary>
        Unit,

        /// <summary>Real persistence and real serialization; external services substituted.</summary>
        Component,

        /// <summary>Real process boundaries and real transport between the components under test.</summary>
        E2e,
    }

    /// <summary>How thoroughly the evidence ranges: one case, or all of them.</summary>
    public enum Quantification
    {
        /// <summary>One case. A wider sample is still a sample.</summary>
        Example,

        /// <summary>A property over all cases in the domain.</summary>
        Invariant,
    }

    /// <summary>How the expected result was obtained. Descriptive, never gated.</summary>
    public enum Oracle
    {
        Direct,
        Golden,
        Metamorphic,
        ModelBased,
        Contract,
    }
}
