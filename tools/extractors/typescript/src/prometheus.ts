import * as fs from 'node:fs';
import * as path from 'node:path';
import { Artifact } from './emitter';

/**
 * Enumerates names only after promtool has validated these files in the repository check. The
 * strict line form avoids treating comments or annotation prose as executable rules.
 */
export function prometheusArtifacts(
  rulesFile: string,
  testsFile: string,
  repoRoot: string,
): Artifact[] {
  const alerts = names(fs.readFileSync(rulesFile, 'utf8'), /^\s*-\s+alert:\s+([A-Za-z][A-Za-z0-9_]*)\s*$/gm);
  const tests = names(
    fs.readFileSync(testsFile, 'utf8'),
    /^\s*(?:-\s+)?alertname:\s+([A-Za-z][A-Za-z0-9_]*)\s*$/gm,
  );
  if (alerts.length === 0) throw new Error(`${rulesFile} contains no alert rules`);
  if (tests.length === 0) throw new Error(`${testsFile} contains no alert rule tests`);

  return [
    ...alerts.map((name) => ({
      id: `prometheus-alert:${name}`,
      kind: 'prometheus-alert',
      file: relative(repoRoot, rulesFile),
    })),
    ...tests.map((name) => ({
      id: `prometheus-rule-test:${name}`,
      kind: 'prometheus-rule-test',
      file: relative(repoRoot, testsFile),
    })),
  ];
}

function names(source: string, pattern: RegExp): string[] {
  return [...source.matchAll(pattern)]
    .map((match) => match[1])
    .filter((value, index, all) => all.indexOf(value) === index)
    .sort((a, b) => a.localeCompare(b));
}

function relative(root: string, file: string): string {
  return path.relative(root, file).split(path.sep).join('/');
}
