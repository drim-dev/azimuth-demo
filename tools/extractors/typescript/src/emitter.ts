/**
 * Static-scan emitter for TypeScript.
 *
 * Reads sources, finds `realizes(...)` / `covers(...)` / `untraced(...)` calls, resolves each
 * call's enclosing named symbol as the site, and writes the language-neutral manifest the core
 * reads. Each ecosystem emits the manifest natively; the core only ever reads manifests, which is
 * why adding a language is a day's work rather than a fork of the core.
 *
 * D17 constrains the core, not the extractors: AST work belongs here, where the compiler API is
 * already present and idiomatic.
 */

import * as fs from 'node:fs';
import * as path from 'node:path';
import * as ts from 'typescript';

const LANG = 'typescript';

const SCOPES = ['unit', 'component', 'e2e'] as const;
const QUANTIFICATIONS = ['example', 'invariant'] as const;
const ORACLES = ['direct', 'golden', 'metamorphic', 'model-based', 'contract'] as const;

const TEST_CALLS = new Set(['test', 'it']);

export interface Entry {
  spec: string;
  scenario: string;
  site: string;
  file: string;
  lang: string;
  scope?: string;
  quantification?: string;
  oracle?: string;
}

export interface UntracedTest {
  site: string;
  file: string;
  lang: string;
}

export interface Manifest {
  realizes: Entry[];
  covers: Entry[];
  untraced_tests: UntracedTest[];
}

export interface Warning {
  file: string;
  line: number;
  message: string;
}

export interface ScanResult {
  realizes: Entry[];
  covers: Entry[];
  untraced_tests: UntracedTest[];
  warnings: Warning[];
}

/**
 * A file is *tracing* when it carries at least one `covers`. Only there does a bare test count as
 * a finding — holding every test in a repo to this would be noise, and partial adoption is what
 * makes the ratchet work.
 */
export function scanText(text: string, file: string): ScanResult {
  const result: ScanResult = { realizes: [], covers: [], untraced_tests: [], warnings: [] };
  const source = ts.createSourceFile(file, text, ts.ScriptTarget.Latest, true, scriptKind(file));

  let tracing = false;
  visit(source, (node) => {
    if (isMarkerCall(node, 'covers')) {
      tracing = true;
    }
  });

  visit(source, (node) => {
    if (isMarkerCall(node, 'realizes')) {
      const args = stringArgs(node);
      if (args.length < 2) {
        result.warnings.push(warn(node, source, file, 'realizes needs a spec and a scenario id'));
        return;
      }
      result.realizes.push({
        spec: args[0],
        scenario: args[1],
        site: resolveSite(node),
        file,
        lang: LANG,
      });
      return;
    }

    if (isMarkerCall(node, 'covers')) {
      const args = stringArgs(node);
      if (args.length < 4) {
        result.warnings.push(
          warn(node, source, file, 'covers needs a spec, a scenario id, a scope and a quantification'),
        );
        return;
      }
      const [spec, scenario, scope, quantification, oracle] = args;
      if (!member(scope, SCOPES)) {
        result.warnings.push(warn(node, source, file, `unknown scope \`${scope}\``));
        return;
      }
      if (!member(quantification, QUANTIFICATIONS)) {
        result.warnings.push(
          warn(node, source, file, `unknown quantification \`${quantification}\``),
        );
        return;
      }
      if (oracle !== undefined && !member(oracle, ORACLES)) {
        result.warnings.push(warn(node, source, file, `unknown oracle \`${oracle}\``));
        return;
      }
      result.covers.push({
        spec,
        scenario,
        site: resolveSite(node),
        file,
        lang: LANG,
        scope,
        quantification,
        ...(oracle === undefined ? {} : { oracle }),
      });
    }
  });

  if (tracing) {
    visit(source, (node) => {
      if (!isTestCall(node)) return;
      if (subtreeHasMarker(node, ['covers', 'untraced'])) return;
      result.untraced_tests.push({ site: testName(node), file, lang: LANG });
    });
  }

  return result;
}

function visit(node: ts.Node, fn: (node: ts.Node) => void): void {
  fn(node);
  ts.forEachChild(node, (child) => visit(child, fn));
}

function isMarkerCall(node: ts.Node, name: string): node is ts.CallExpression {
  return (
    ts.isCallExpression(node) &&
    ts.isIdentifier(node.expression) &&
    node.expression.text === name
  );
}

function isTestCall(node: ts.Node): node is ts.CallExpression {
  return (
    ts.isCallExpression(node) &&
    ts.isIdentifier(node.expression) &&
    TEST_CALLS.has(node.expression.text) &&
    node.arguments.length > 0 &&
    ts.isStringLiteralLike(node.arguments[0])
  );
}

function subtreeHasMarker(node: ts.Node, names: readonly string[]): boolean {
  let found = false;
  visit(node, (child) => {
    if (names.some((name) => isMarkerCall(child, name))) found = true;
  });
  return found;
}

function stringArgs(call: ts.CallExpression): string[] {
  const out: string[] = [];
  for (const argument of call.arguments) {
    if (!ts.isStringLiteralLike(argument)) break;
    out.push(argument.text);
  }
  return out;
}

/**
 * Walks outward to the nearest thing a human would name: a test's own title, then a named
 * function or method, then a named binding an arrow was assigned to. A `covers` inside
 * `test('…', () => …)` therefore names the test, while a `realizes` in `export function GET` names
 * the handler.
 */
function resolveSite(call: ts.CallExpression): string {
  let node: ts.Node | undefined = call.parent;
  while (node) {
    if (isTestCall(node)) {
      return testName(node);
    }
    if ((ts.isFunctionDeclaration(node) || ts.isMethodDeclaration(node)) && node.name) {
      return node.name.getText();
    }
    if (ts.isVariableDeclaration(node) && ts.isIdentifier(node.name)) {
      return node.name.text;
    }
    if (ts.isClassDeclaration(node) && node.name) {
      return node.name.text;
    }
    node = node.parent;
  }
  return '<module>';
}

function testName(call: ts.CallExpression): string {
  const first = call.arguments[0];
  return ts.isStringLiteralLike(first) ? first.text : '<test>';
}

function warn(node: ts.Node, source: ts.SourceFile, file: string, message: string): Warning {
  const { line } = source.getLineAndCharacterOfPosition(node.getStart(source));
  return { file, line: line + 1, message };
}

function member<T extends string>(value: string | undefined, values: readonly T[]): value is T {
  return value !== undefined && (values as readonly string[]).includes(value);
}

function scriptKind(file: string): ts.ScriptKind {
  if (file.endsWith('.tsx')) return ts.ScriptKind.TSX;
  if (file.endsWith('.jsx')) return ts.ScriptKind.JSX;
  if (file.endsWith('.js') || file.endsWith('.mjs') || file.endsWith('.cjs')) return ts.ScriptKind.JS;
  return ts.ScriptKind.TS;
}

const SOURCE = /\.(ts|tsx|js|jsx|mjs|cjs)$/;
const SKIP = new Set(['node_modules', 'dist', 'build', '.git', 'target']);

export function walk(dir: string, out: string[] = []): string[] {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (SKIP.has(entry.name)) continue;
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full, out);
    else if (SOURCE.test(entry.name) && !entry.name.endsWith('.d.ts')) out.push(full);
  }
  return out;
}

export function emit(roots: string[], repoRoot: string): { manifest: Manifest; warnings: Warning[] } {
  const manifest: Manifest = { realizes: [], covers: [], untraced_tests: [] };
  const warnings: Warning[] = [];

  const files: string[] = [];
  for (const root of roots) {
    const stat = fs.statSync(root);
    if (stat.isDirectory()) walk(root, files);
    else files.push(root);
  }
  files.sort();

  for (const file of files) {
    const relative = path.relative(repoRoot, file).split(path.sep).join('/');
    const result = scanText(fs.readFileSync(file, 'utf8'), relative);
    manifest.realizes.push(...result.realizes);
    manifest.covers.push(...result.covers);
    manifest.untraced_tests.push(...result.untraced_tests);
    warnings.push(...result.warnings);
  }

  manifest.realizes.sort(compare);
  manifest.covers.sort(compare);
  manifest.untraced_tests.sort((a, b) => a.site.localeCompare(b.site));
  return { manifest, warnings };
}

function compare(a: Entry, b: Entry): number {
  return (
    a.spec.localeCompare(b.spec) ||
    a.scenario.localeCompare(b.scenario) ||
    a.site.localeCompare(b.site)
  );
}
