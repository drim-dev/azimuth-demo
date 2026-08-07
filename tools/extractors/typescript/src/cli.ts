#!/usr/bin/env node
/**
 * azimuth-emit-ts --output <path> [--root <dir>] <dir-or-file>...
 *
 * Writes the language-neutral manifest the core reads.
 */

import * as fs from 'node:fs';
import * as path from 'node:path';
import { emit, nextRoutes } from './emitter';

const USAGE = `usage: azimuth-emit-ts --output <path> [--root <dir>] [--next-app <class>=<dir>] <dir-or-file>...
  --output    where the manifest is written
  --root      paths in the manifest are made relative to this (default: cwd)
  --next-app  enumerate a built Next.js app's routes as members of <class>, repeatable.
              Membership comes from the build, so an untagged route is still a member.`;

function main(argv: string[]): number {
  let output: string | undefined;
  let root = process.cwd();
  const roots: string[] = [];
  const apps: { classId: string; dir: string }[] = [];

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === '--output' || arg === '-o') {
      output = argv[++i];
    } else if (arg === '--root') {
      root = path.resolve(argv[++i] ?? '.');
    } else if (arg === '--next-app') {
      const value = argv[++i] ?? '';
      const split = value.indexOf('=');
      if (split <= 0) {
        console.error(`azimuth-emit: --next-app wants <class>=<dir>, got \`${value}\`\n${USAGE}`);
        return 2;
      }
      apps.push({ classId: value.slice(0, split), dir: path.resolve(value.slice(split + 1)) });
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

  for (const app of apps) {
    const routes = nextRoutes(app.classId, app.dir, root);
    manifest.class_members.push(...routes.members);
    warnings.push(...routes.warnings);
  }

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
