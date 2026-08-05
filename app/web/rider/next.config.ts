import path from 'node:path';
import type { NextConfig } from 'next';

// The annotations package is a workspace sibling reached by symlink, so the bundler's root has to
// be the repo rather than this app — otherwise it refuses to follow the link out of the app dir.
const repoRoot = path.join(__dirname, '..', '..', '..');

const config: NextConfig = {
  turbopack: { root: repoRoot },
  outputFileTracingRoot: repoRoot,
};

export default config;
