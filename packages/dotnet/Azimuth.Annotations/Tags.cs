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
    /// <remarks>
    /// The targets match exactly what the extractor walks — types and methods. Permitting a target
    /// the emitter does not read would let a tag vanish silently, which is the one failure a
    /// linkage tag must not have.
    /// </remarks>
    [AttributeUsage(
        AttributeTargets.Class
        | AttributeTargets.Struct
        | AttributeTargets.Interface
        | AttributeTargets.Enum
        | AttributeTargets.Method,
        AllowMultiple = true)]
    public sealed class RealizesAttribute : Attribute
    {
        /// <summary>Tags a site as being on the path of <paramref name="scenario"/>.</summary>
        public RealizesAttribute(string spec, string scenario)
        {
            Spec = spec;
            Scenario = scenario;
        }

        /// <summary>Stable spec id.</summary>
        public string Spec { get; }

        /// <summary>Stable scenario id, unique within the spec.</summary>
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
        /// <summary>Tags a test as verifying <paramref name="scenario"/> at the given form.</summary>
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

        /// <summary>Stable spec id.</summary>
        public string Spec { get; }

        /// <summary>Stable scenario id, unique within the spec.</summary>
        public string Scenario { get; }

        /// <summary>How much of the real system this test actually runs against.</summary>
        public Scope Scope { get; }

        /// <summary>Whether this test checks one case or ranges over all of them.</summary>
        public Quantification Quantification { get; }

        /// <summary>How the expected result was obtained. Descriptive, never gated.</summary>
        public Oracle Oracle { get; }
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
        Universal,
    }

    /// <summary>How the expected result was obtained. Descriptive, never gated.</summary>
    public enum Oracle
    {
        /// <summary>The expected result was written out directly.</summary>
        Direct,

        /// <summary>Compared against a recorded previous output.</summary>
        Golden,

        /// <summary>Checked by a relation between inputs rather than an absolute answer.</summary>
        Metamorphic,

        /// <summary>Compared against an independent model of the behaviour.</summary>
        ModelBased,

        /// <summary>Checked against an agreed interface contract.</summary>
        Contract,
    }
}
