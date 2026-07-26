import type { NextConfig } from 'next';

const config: NextConfig = {
  reactStrictMode: true,

  // Emits a self contained server bundle so the runtime image needs neither
  // node_modules nor pnpm - which is what keeps the portal image small and the
  // hospital deployment reproducible.
  output: 'standalone',
  outputFileTracingRoot: new URL('../../', import.meta.url).pathname,

  // Workspace packages ship TypeScript source rather than a build artefact.
  // Next compiles them with the app, so there is no build ordering to maintain.
  transpilePackages: ['@cloudfilm/shared', '@cloudfilm/core', '@cloudfilm/ui'],

  poweredByHeader: false,
};

export default config;
