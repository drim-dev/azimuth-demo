#!/usr/bin/env node
/**
 * azimuth-emit-ts --output <path> [--root <dir>] <dir-or-file>...
 *
 * Writes the language-neutral manifest the core reads.
 */

import * as fs from 'node:fs';
import * as path from 'node:path';
import { emit } from './emitter';

const USAGE = `usage: azimuth-emit-ts --output <path> [--root <dir>] <dir-or-file>...
  --output  where the manifest is written
  --root    paths in the manifest are made relative to this (default: cwd)`;

function main(argv: string[]): number {
  let output: string | undefined;
  let root = process.cwd();
  const roots: string[] = [];

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === '--output' || arg === '-o') {
      output = argv[++i];
    } else if (arg === '--root') {
      root = path.resolve(argv[++i] ?? '.');
    } else if (arg.startsWith('-')) {
      console.error(`azimuth-emit: unknown option \`${arg}\`\n${USAGE}`);
      return 2;
    } else {
      roots.push(arg);
    }
  }

  if (!output || roots.length === 0) {
    console.error(USAGE);
    return 2;
  }

  for (const target of roots) {
    if (!fs.existsSync(target)) {
      console.error(`azimuth-emit: not found: ${target}`);
      return 2;
    }
  }

  const { manifest, warnings } = emit(roots, root);
  for (const warning of warnings) {
    console.error(`warning: ${warning.file}:${warning.line}: ${warning.message}`);
  }

  fs.mkdirSync(path.dirname(path.resolve(output)), { recursive: true });
  fs.writeFileSync(output, `${JSON.stringify(manifest, null, 2)}\n`);
  console.error(
    `${manifest.realizes.length} realizes, ${manifest.covers.length} covers, ` +
      `${manifest.untraced_tests.length} untraced → ${output}`,
  );
  return 0;
}

process.exit(main(process.argv.slice(2)));
