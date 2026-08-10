//! The derived model.
//!
//! `claim = (domain, predicate)` (D13). The steel thread exercises only the behavioural domain,
//! which scenarios take implicitly and never name — so `domain` is not represented yet. When a
//! second domain arrives it becomes a field here, not a second artifact type.

use crate::json::Json;

#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord)]
pub enum Criticality {
    Routine,
    Standard,
    Critical,
}

impl Criticality {
    pub fn parse(s: &str) -> Option<Self> {
        match s {
            "critical" => Some(Criticality::Critical),
            "standard" => Some(Criticality::Standard),
            "routine" => Some(Criticality::Routine),
            _ => None,
        }
    }

    pub fn name(self) -> &'static str {
        match self {
            Criticality::Critical => "critical",
            Criticality::Standard => "standard",
            Criticality::Routine => "routine",
        }
    }

    /// D6.5: the level gates which artifacts are required at all. A `routine` claim needs a spec
    /// entry and nothing else, so it cannot be `uncovered` — no evidence was ever required of it.
    pub fn requires_evidence(self) -> bool {
        self != Criticality::Routine
    }
}

/// Ladders. A stronger form on any axis satisfies a requirement for a weaker one, so the derived
/// `Ord` is the comparison `wrong-form` uses.
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord)]
pub enum Scope {
    Unit,
    Component,
    E2e,
}

impl Scope {
    pub fn parse(s: &str) -> Option<Self> {
        match s {
            "unit" => Some(Scope::Unit),
            "component" => Some(Scope::Component),
            "e2e" => Some(Scope::E2e),
            _ => None,
        }
    }

    pub fn name(self) -> &'static str {
        match self {
            Scope::Unit => "unit",
            Scope::Component => "component",
            Scope::E2e => "e2e",
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord)]
pub enum Quantification {
    Example,
    Universal,
}

impl Quantification {
    pub fn parse(s: &str) -> Option<Self> {
        match s {
            "example" => Some(Quantification::Example),
            "universal" => Some(Quantification::Universal),
            _ => None,
        }
    }

    pub fn name(self) -> &'static str {
        match self {
            Quantification::Example => "example",
            Quantification::Universal => "universal",
        }
    }
}

/// How evidence obtains its expected result. Oracle kinds are descriptive categories rather than
/// a strength ladder, but keeping the vocabulary closed prevents stale emitters from inventing a
/// category the model and its judges do not understand.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Oracle {
    Direct,
    Golden,
    Relational,
    Metamorphic,
    ModelBased,
    Contract,
}

impl Oracle {
    pub fn parse(s: &str) -> Option<Self> {
        match s {
            "direct" => Some(Oracle::Direct),
            "golden" => Some(Oracle::Golden),
            "relational" => Some(Oracle::Relational),
            "metamorphic" => Some(Oracle::Metamorphic),
            "model-based" => Some(Oracle::ModelBased),
            "contract" => Some(Oracle::Contract),
            _ => None,
        }
    }

    pub fn name(self) -> &'static str {
        match self {
            Oracle::Direct => "direct",
            Oracle::Golden => "golden",
            Oracle::Relational => "relational",
            Oracle::Metamorphic => "metamorphic",
            Oracle::ModelBased => "model-based",
            Oracle::Contract => "contract",
        }
    }
}

/// How far evidence reaches (D4.1). A ladder: stronger satisfies weaker.
///
/// `Proof` is deliberately narrower than the formal-methods sense — established by construction
/// over all executions, with no obligation discharged and no semantics checked. A unique index
/// counts because violation is unrepresentable, not because anything was proved (see the glossary).
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord)]
pub enum Strength {
    Detection,
    Demonstration,
    Proof,
}

impl Strength {
    pub fn parse(s: &str) -> Option<Self> {
        match s {
            "proof" => Some(Strength::Proof),
            "demonstration" => Some(Strength::Demonstration),
            "detection" => Some(Strength::Detection),
            _ => None,
        }
    }

    pub fn name(self) -> &'static str {
        match self {
            Strength::Proof => "proof",
            Strength::Demonstration => "demonstration",
            Strength::Detection => "detection",
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum StepKind {
    Given,
    When,
    Then,
    And,
}

impl StepKind {
    pub fn name(self) -> &'static str {
        match self {
            StepKind::Given => "given",
            StepKind::When => "when",
            StepKind::Then => "then",
            StepKind::And => "and",
        }
    }
}

#[derive(Debug, Clone)]
pub struct Step {
    pub kind: StepKind,
    pub text: String,
}

#[derive(Debug, Clone)]
pub struct Scenario {
    pub id: String,
    pub steps: Vec<Step>,
    pub line: usize,
}

/// What a claim ranges over (D13). The behavioural domain is implicit and never written; a second
/// domain arrived only when the demo produced evidence that the first could not carry it.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Domain {
    /// Executions of a behaviour — inputs matching the WHEN.
    Behaviour,
    /// A set of sites. Membership is derived from what the code built, so a new site joins the
    /// class without anyone declaring it.
    Sites,
}

#[derive(Debug, Clone)]
pub struct Requirement {
    pub id: String,
    /// `None` is the `unclassified` hole (D6.2), not a parse error: a missing *declaration* is a
    /// semantic gap, while an unrecognized *construct* fails the parse.
    pub criticality: Option<Criticality>,
    pub statement: String,
    pub scenarios: Vec<Scenario>,
    pub line: usize,
    pub domain: Domain,
    /// For `Domain::Sites`: the spec whose realizing sites form the class.
    pub over: Option<String>,
}

#[derive(Debug, Clone)]
pub struct Spec {
    pub id: String,
    pub path: String,
    pub requirements: Vec<Requirement>,
}

/// A tag site, from a manifest. `realizes` and `covers` differ only in the form fields, which are
/// absent on code: form is how a test checks, not a property of code.
#[derive(Debug, Clone)]
pub struct Site {
    pub spec: String,
    pub scenario: String,
    pub site: String,
    pub file: String,
    pub lang: String,
    /// Hash of the compiler-resolved source site. An empty value means the extractor could not
    /// isolate the site, so judgment freshness falls back to the whole file.
    pub source_fingerprint: String,
    /// Present for evidence imported from a system of record rather than extracted from code.
    pub evidence_kind: Option<String>,
    pub evidence_outcome: Option<String>,
    pub observed_at: Option<String>,
    pub expires_at: Option<u64>,
    pub scope: Option<Scope>,
    pub quantification: Option<Quantification>,
    pub oracle: Option<Oracle>,
}

/// A compiler-resolved site that implements a design-owned mechanism identity.
#[derive(Debug, Clone)]
pub struct MechanismImplementation {
    pub spec: String,
    pub mechanism: String,
    pub binding: String,
    pub file: String,
    pub lang: String,
    pub source_fingerprint: String,
}

/// Evidence about a mechanism's own contract. It is deliberately not claim evidence: whether a
/// mechanism establishes a particular business claim is a separate composition judgment.
#[derive(Debug, Clone)]
pub struct MechanismCover {
    pub spec: String,
    pub mechanism: String,
    pub site: String,
    pub file: String,
    pub lang: String,
    pub source_fingerprint: String,
    pub scope: Option<Scope>,
    pub quantification: Option<Quantification>,
    pub oracle: Option<Oracle>,
}

/// A member of a class, enumerated by the project's extractor from what the build produced —
/// a route table, a container, a manifest — rather than from a tag.
///
/// This exists because deriving membership from tags cannot see a site nobody tagged, which is the
/// failure D13.1 names: an enumerator that misses a member reports green over the gap. Identity is
/// the **file**: the member is the file, and a discharge anywhere in it discharges the member.
#[derive(Debug, Clone)]
pub struct ClassMember {
    pub class: String,
    pub site: String,
    pub file: String,
    pub lang: String,
}

/// Evidence that a class was enumerated from a system-produced source rather than reconstructed
/// from the declarations whose omissions the enumeration exists to find.
#[derive(Debug, Clone)]
pub struct Enumeration {
    pub class: String,
    pub kind: String,
    pub source: String,
    pub source_fingerprint: String,
}

/// A machine-addressable artifact emitted from a compiler or schema model. Optional properties
/// carry only facts the extractor can derive; semantic claims remain in the design prose.
#[derive(Debug, Clone)]
pub struct Artifact {
    pub id: String,
    pub kind: String,
    pub file: String,
    pub unique: Option<bool>,
    pub columns: Vec<String>,
    pub predicate: Option<String>,
}

#[derive(Debug, Default)]
pub struct Model {
    pub specs: Vec<Spec>,
    pub realizes: Vec<Site>,
    pub covers: Vec<Site>,
    pub mechanism_implementations: Vec<MechanismImplementation>,
    pub mechanism_covers: Vec<MechanismCover>,
    /// Class members enumerated by an extractor. Empty when no project emits them, in which case
    /// a class is only as wide as its tags.
    pub class_members: Vec<ClassMember>,
    pub enumerations: Vec<Enumeration>,
    pub artifacts: Vec<Artifact>,
    /// Absent until a standards file is read. Without it no evidence standard is known, so
    /// `wrong-form` cannot fire and `uncovered` falls back to "has any evidence at all".
    pub standards: Option<crate::plan::Standards>,
    pub plans: Vec<crate::plan::Plan>,
    pub designs: Vec<crate::design::Design>,
    pub judgments: Vec<crate::judgment::Judgments>,
}

/// The evidence standard for one claim: the project mapping, overridden by a plan entry.
#[derive(Debug, Clone, Copy)]
pub struct Required {
    /// `None` means no evidence is required — D6.5's `routine`.
    pub strength: Option<Strength>,
    pub quantification: Option<Quantification>,
    pub scope: Scope,
}

/// A scenario plus the context needed to report on it.
pub struct ClaimView<'a> {
    pub spec: &'a Spec,
    pub requirement: &'a Requirement,
    pub scenario: &'a Scenario,
}

impl<'a> ClaimView<'a> {
    pub fn id(&self) -> String {
        format!("{}#{}", self.spec.id, self.scenario.id)
    }
}

impl Model {
    pub fn claims(&self) -> impl Iterator<Item = ClaimView<'_>> {
        self.specs.iter().flat_map(|spec| {
            spec.requirements.iter().flat_map(move |requirement| {
                requirement.scenarios.iter().map(move |scenario| ClaimView {
                    spec,
                    requirement,
                    scenario,
                })
            })
        })
    }

    pub fn has_claim(&self, spec: &str, scenario: &str) -> bool {
        self.specs.iter().any(|s| {
            s.id == spec
                && s.requirements
                    .iter()
                    .any(|r| r.scenarios.iter().any(|sc| sc.id == scenario))
        })
    }

    pub fn scenario_count(&self) -> usize {
        self.claims().count()
    }

    pub fn find_claim(&self, spec: &str, scenario: &str) -> Option<ClaimView<'_>> {
        self.claims()
            .find(|c| c.spec.id == spec && c.scenario.id == scenario)
    }

    pub fn judgments_for(&self, spec: &str) -> Option<&crate::judgment::Judgments> {
        self.judgments.iter().find(|j| j.spec == spec)
    }

    /// Everything a judgment about this claim would have had to look at.
    pub fn evidence_files(&self, spec: &str, scenario: &str) -> Vec<String> {
        self.covers
            .iter()
            .filter(|s| s.spec == spec && s.scenario == scenario)
            .map(|s| s.file.clone())
            .collect()
    }

    /// Every source the agent-tier rubric requires for a verdict. Compiler-resolved evidence and
    /// realization sites carry their own fingerprints; prose and explicitly bound non-code
    /// mechanisms remain deliberately file-scoped.
    pub fn judgment_inputs(
        &self,
        spec: &str,
        scenario: &str,
    ) -> Vec<crate::judgment::FingerprintInput> {
        let mut inputs: Vec<crate::judgment::FingerprintInput> = self
            .realizes
            .iter()
            .filter(|site| site.spec == spec && site.scenario == scenario)
            .map(crate::judgment::FingerprintInput::realization)
            .collect();
        inputs.extend(
            self.covers
                .iter()
                .filter(|site| site.spec == spec && site.scenario == scenario)
                .map(crate::judgment::FingerprintInput::evidence),
        );
        let Some(claim) = self.find_claim(spec, scenario) else {
            return inputs;
        };
        if let Some(standards) = &self.standards {
            inputs.push(crate::judgment::FingerprintInput::file(&standards.path));
        }
        if let Some(plan) = self.plan_for(spec) {
            inputs.push(crate::judgment::FingerprintInput::file(&plan.path));
            if let Some(evidence) = plan
                .entry(scenario)
                .and_then(|entry| entry.evidence.as_ref())
            {
                for binding in evidence.bindings.iter().chain(&evidence.detector_bindings) {
                    if let Some(artifact) = self.artifacts.iter().find(|item| item.id == *binding) {
                        if !artifact.file.is_empty() {
                            inputs.push(crate::judgment::FingerprintInput::file(&artifact.file));
                        }
                    }
                }
            }
        }
        if let Some(design) = self.design_for(spec) {
            let entries = design
                .for_scenario(scenario)
                .into_iter()
                .chain(design.for_requirement(&claim.requirement.id));
            let mut has_entry = false;
            for entry in entries {
                has_entry = true;
                for mechanism in &entry.mechanisms {
                    if let Some(binding) = mechanism.binding.as_deref() {
                        if let Some(artifact) = self
                            .artifacts
                            .iter()
                            .find(|artifact| artifact.id == binding)
                        {
                            if !artifact.file.is_empty() {
                                inputs
                                    .push(crate::judgment::FingerprintInput::file(&artifact.file));
                            }
                        }
                    }
                    inputs.extend(
                        self.mechanism_implementations
                            .iter()
                            .filter(|implementation| {
                                implementation.spec == spec
                                    && implementation.mechanism == mechanism.id
                            })
                            .map(crate::judgment::FingerprintInput::mechanism),
                    );
                }
            }
            if has_entry {
                inputs.push(crate::judgment::FingerprintInput::file(&design.path));
            }
        }
        inputs.sort_by(|a, b| a.identity.cmp(&b.identity));
        inputs.dedup_by(|a, b| a.identity == b.identity);
        inputs
    }

    pub fn claim_text(&self, claim: &ClaimView<'_>) -> String {
        let steps: Vec<String> = claim
            .scenario
            .steps
            .iter()
            .map(|s| format!("{} {}", s.kind.name(), s.text))
            .collect();
        format!(
            "{}|{}|{}|{}",
            claim
                .requirement
                .criticality
                .map(|criticality| criticality.name())
                .unwrap_or("unclassified"),
            claim.requirement.statement,
            claim.scenario.id,
            steps.join("|")
        )
    }

    pub fn design_for(&self, spec: &str) -> Option<&crate::design::Design> {
        self.designs.iter().find(|d| d.spec == spec)
    }

    pub fn mechanism_bindings<'a>(
        &'a self,
        spec: &str,
        mechanism: &'a crate::design::Mechanism,
    ) -> Vec<&'a str> {
        let mut bindings = Vec::new();
        if let Some(binding) = mechanism.binding.as_deref() {
            bindings.push(binding);
        }
        bindings.extend(
            self.mechanism_implementations
                .iter()
                .filter(|implementation| {
                    implementation.spec == spec && implementation.mechanism == mechanism.id
                })
                .map(|implementation| implementation.binding.as_str()),
        );
        bindings
    }

    pub fn plan_for(&self, spec: &str) -> Option<&crate::plan::Plan> {
        self.plans.iter().find(|p| p.spec == spec)
    }

    /// Resolves the standard for a claim. Returns `None` when no standards file was read, or when
    /// the requirement declares no criticality — in both cases nothing is known to require.
    pub fn required_for(&self, claim: &ClaimView<'_>) -> Option<Required> {
        let standards = self.standards.as_ref()?;
        let level = standards.for_level(claim.requirement.criticality?)?;
        let entry = self
            .plan_for(&claim.spec.id)
            .and_then(|p| p.entry(&claim.scenario.id));
        Some(Required {
            strength: level.strength,
            quantification: entry
                .and_then(|e| e.quantification)
                .or(level.quantification),
            // D15: scope is not derived from criticality. Default, raised per claim where truth
            // depends on something real.
            scope: entry
                .and_then(|e| e.scope)
                .unwrap_or(standards.default_scope),
        })
    }

    /// D10: the export is the extension seam. Checks, dashboards and PR annotations are all
    /// consumers of this; nothing else re-parses specs.
    pub fn to_json(&self, holes: &[crate::check::Hole]) -> Json {
        let specs = self
            .specs
            .iter()
            .map(|spec| {
                let reqs = spec
                    .requirements
                    .iter()
                    .map(|r| {
                        let scenarios = r
                            .scenarios
                            .iter()
                            .map(|sc| {
                                let steps = sc
                                    .steps
                                    .iter()
                                    .map(|st| {
                                        Json::obj(vec![
                                            ("kind", Json::str(st.kind.name())),
                                            ("text", Json::str(&st.text)),
                                        ])
                                    })
                                    .collect();
                                Json::obj(vec![
                                    ("id", Json::str(&sc.id)),
                                    ("line", Json::Num(sc.line as f64)),
                                    ("steps", Json::Arr(steps)),
                                ])
                            })
                            .collect();
                        Json::obj(vec![
                            ("id", Json::str(&r.id)),
                            (
                                "criticality",
                                match r.criticality {
                                    Some(c) => Json::str(c.name()),
                                    None => Json::Null,
                                },
                            ),
                            ("statement", Json::str(&r.statement)),
                            ("line", Json::Num(r.line as f64)),
                            ("scenarios", Json::Arr(scenarios)),
                        ])
                    })
                    .collect();
                Json::obj(vec![
                    ("id", Json::str(&spec.id)),
                    ("path", Json::str(&spec.path)),
                    ("requirements", Json::Arr(reqs)),
                ])
            })
            .collect();

        Json::obj(vec![
            ("version", Json::Num(1.0)),
            ("specs", Json::Arr(specs)),
            (
                "realizes",
                Json::Arr(self.realizes.iter().map(site_json).collect()),
            ),
            (
                "covers",
                Json::Arr(self.covers.iter().map(site_json).collect()),
            ),
            (
                "mechanism_implementations",
                Json::Arr(
                    self.mechanism_implementations
                        .iter()
                        .map(mechanism_implementation_json)
                        .collect(),
                ),
            ),
            (
                "mechanism_covers",
                Json::Arr(
                    self.mechanism_covers
                        .iter()
                        .map(mechanism_cover_json)
                        .collect(),
                ),
            ),
            (
                "enumerations",
                Json::Arr(
                    self.enumerations
                        .iter()
                        .map(|e| {
                            Json::obj(vec![
                                ("class", Json::str(&e.class)),
                                ("kind", Json::str(&e.kind)),
                                ("source", Json::str(&e.source)),
                                ("source_fingerprint", Json::str(&e.source_fingerprint)),
                            ])
                        })
                        .collect(),
                ),
            ),
            (
                "artifacts",
                Json::Arr(
                    self.artifacts
                        .iter()
                        .map(|artifact| {
                            let mut fields = vec![
                                ("id", Json::str(&artifact.id)),
                                ("kind", Json::str(&artifact.kind)),
                                ("file", Json::str(&artifact.file)),
                            ];
                            if let Some(unique) = artifact.unique {
                                fields.push(("unique", Json::Bool(unique)));
                            }
                            if !artifact.columns.is_empty() {
                                fields.push((
                                    "columns",
                                    Json::Arr(artifact.columns.iter().map(Json::str).collect()),
                                ));
                            }
                            if let Some(predicate) = &artifact.predicate {
                                fields.push(("predicate", Json::str(predicate)));
                            }
                            Json::obj(fields)
                        })
                        .collect(),
                ),
            ),
            ("mechanisms", Json::Arr(self.mechanism_json())),
            (
                "holes",
                Json::Arr(holes.iter().map(|h| h.to_json()).collect()),
            ),
        ])
    }
}

impl Model {
    fn mechanism_json(&self) -> Vec<Json> {
        let mut out = Vec::new();
        for design in &self.designs {
            for entry in &design.entries {
                for m in &entry.mechanisms {
                    let bindings = self.mechanism_bindings(&design.spec, m);
                    out.push(Json::obj(vec![
                        ("spec", Json::str(&design.spec)),
                        (
                            "target_kind",
                            Json::str(match entry.target {
                                crate::design::Target::Requirement(_) => "requirement",
                                crate::design::Target::Scenario(_) => "scenario",
                            }),
                        ),
                        ("target", Json::str(entry.target.id())),
                        ("id", Json::str(&m.id)),
                        ("enforcement", Json::str(m.kind.name())),
                        ("rung", Json::Num(m.kind.rung() as f64)),
                        (
                            "binding",
                            (bindings.len() == 1)
                                .then(|| Json::str(bindings[0]))
                                .unwrap_or(Json::Null),
                        ),
                        (
                            "expected_unique",
                            m.expected_unique.map(Json::Bool).unwrap_or(Json::Null),
                        ),
                        (
                            "expected_columns",
                            Json::Arr(m.expected_columns.iter().map(Json::str).collect()),
                        ),
                        (
                            "expected_predicate",
                            m.expected_predicate
                                .as_ref()
                                .map(Json::str)
                                .unwrap_or(Json::Null),
                        ),
                    ]));
                }
            }
        }
        out
    }
}

fn site_json(s: &Site) -> Json {
    let mut pairs = vec![
        ("spec".to_string(), Json::str(&s.spec)),
        ("scenario".to_string(), Json::str(&s.scenario)),
        ("site".to_string(), Json::str(&s.site)),
        ("file".to_string(), Json::str(&s.file)),
        ("lang".to_string(), Json::str(&s.lang)),
    ];
    if !s.source_fingerprint.is_empty() {
        pairs.push((
            "source_fingerprint".to_string(),
            Json::str(&s.source_fingerprint),
        ));
    }
    if let Some(value) = &s.evidence_kind {
        pairs.push(("evidence_kind".to_string(), Json::str(value)));
    }
    if let Some(value) = &s.evidence_outcome {
        pairs.push(("evidence_outcome".to_string(), Json::str(value)));
    }
    if let Some(value) = &s.observed_at {
        pairs.push(("observed_at".to_string(), Json::str(value)));
    }
    if let Some(value) = s.expires_at {
        pairs.push(("expires_at".to_string(), Json::Num(value as f64)));
    }
    if let Some(scope) = s.scope {
        pairs.push(("scope".to_string(), Json::str(scope.name())));
    }
    if let Some(q) = s.quantification {
        pairs.push(("quantification".to_string(), Json::str(q.name())));
    }
    if let Some(o) = s.oracle {
        pairs.push(("oracle".to_string(), Json::str(o.name())));
    }
    Json::Obj(pairs)
}

fn mechanism_implementation_json(item: &MechanismImplementation) -> Json {
    Json::obj(vec![
        ("spec", Json::str(&item.spec)),
        ("mechanism", Json::str(&item.mechanism)),
        ("binding", Json::str(&item.binding)),
        ("file", Json::str(&item.file)),
        ("lang", Json::str(&item.lang)),
        ("source_fingerprint", Json::str(&item.source_fingerprint)),
    ])
}

fn mechanism_cover_json(item: &MechanismCover) -> Json {
    let mut fields = vec![
        ("spec".to_string(), Json::str(&item.spec)),
        ("mechanism".to_string(), Json::str(&item.mechanism)),
        ("site".to_string(), Json::str(&item.site)),
        ("file".to_string(), Json::str(&item.file)),
        ("lang".to_string(), Json::str(&item.lang)),
        (
            "source_fingerprint".to_string(),
            Json::str(&item.source_fingerprint),
        ),
    ];
    if let Some(scope) = item.scope {
        fields.push(("scope".to_string(), Json::str(scope.name())));
    }
    if let Some(quantification) = item.quantification {
        fields.push((
            "quantification".to_string(),
            Json::str(quantification.name()),
        ));
    }
    if let Some(oracle) = item.oracle {
        fields.push(("oracle".to_string(), Json::str(oracle.name())));
    }
    Json::Obj(fields)
}
