/**
 * Azimuth linkage tags for TypeScript.
 *
 * The front end is functions — route handlers, server components, hooks — not classes, so the tags
 * are typed no-op *function calls* rather than decorators, which are class-member-only. They exist
 * to be type-checked at author time and found statically by the emitter, which resolves each call's
 * enclosing named symbol as the site. At runtime they do nothing.
 */

export type Scope = 'unit' | 'component' | 'e2e';
export type Quantification = 'example' | 'universal';
export type Oracle = 'direct' | 'golden' | 'metamorphic' | 'model-based' | 'contract';

/**
 * Marks a production-code site as being on a claim's path, keyed by the stable
 * `(spec, scenario)` pair.
 *
 * The pair, not a triple: scenario ids are unique per spec, so a requirement id would be redundant
 * information that can go stale. Dropping it is what makes splitting or merging a requirement free.
 *
 * Carries no form — form is how a *test* checks, not a property of code.
 */
export function realizes(spec: string, scenario: string): void {
  void spec;
  void scenario;
}

/**
 * Marks a test as verifying a claim, at the form the test *actually* has.
 *
 * What the form must *be* lives in the verification plan; this declares what it is, and the
 * comparison between the two is `wrong-form`.
 */
export function covers(
  spec: string,
  scenario: string,
  scope: Scope,
  quantification: Quantification,
  oracle?: Oracle,
): void {
  void spec;
  void scenario;
  void scope;
  void quantification;
  void oracle;
}

/**
 * Opts a test out of tracing: it legitimately covers no claim — setup, infrastructure, a smoke
 * check. A deliberate, attributable, reviewable exemption is fine anywhere; a silent absence is
 * not, and the reason is what makes it the former.
 */
export function untraced(reason: string): void {
  void reason;
}
