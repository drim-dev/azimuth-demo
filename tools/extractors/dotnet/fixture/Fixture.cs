using System;
using Azimuth.Annotations;

namespace Azimuth.Fixture
{
    /// A type-level tag names its site by the type; a method-level tag by Type.Method.
    [Realizes("alpha", "type-level-thing")]
    public sealed class Production
    {
        [Realizes("alpha", "method-level-thing")]
        public void Method()
        {
        }

        /// A site may realize several claims — branches of one method.
        [Realizes("alpha", "first-branch")]
        [Realizes("alpha", "second-branch")]
        public void Branching()
        {
        }

        public void Untagged()
        {
        }
    }

    /// Stands in for a test framework attribute. Matched by simple name, so the emitter needs no
    /// reference to any test framework.
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class FactAttribute : Attribute
    {
    }
}

namespace Azimuth.Fixture.Traced
{
    using Azimuth.Fixture;

    public sealed class Tests
    {
        [Fact]
        [Covers("alpha", "method-level-thing", Scope.Component, Quantification.Invariant)]
        public void Covered()
        {
        }

        [Fact]
        [Covers("alpha", "first-branch", Scope.E2e, Quantification.Example, Oracle.ModelBased)]
        public void CoveredWithOracle()
        {
        }

        [Fact]
        [Untraced("smoke check; maps to no claim by design")]
        public void Exempt()
        {
        }

        /// The dual of an uncovered claim.
        [Fact]
        public void Bare()
        {
        }
    }
}

namespace Azimuth.Fixture.Untraced
{
    using Azimuth.Fixture;

    /// Outside every traced root: a bare test here is not a finding, because the area was never
    /// opted in. D8's ratchet is what makes partial adoption work.
    public sealed class Tests
    {
        [Fact]
        public void BareButOutsideAnyTracedRoot()
        {
        }
    }
}
