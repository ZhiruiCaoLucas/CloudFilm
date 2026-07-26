import js from '@eslint/js';
import nextCoreWebVitals from 'eslint-config-next/core-web-vitals';
import tseslint from 'typescript-eslint';

/**
 * One flat config for the whole workspace.
 *
 * A single config means a rule is enabled everywhere or nowhere; there is no place
 * for an individual package to quietly lower the bar.
 */
export default tseslint.config(
  {
    ignores: [
      '**/node_modules/**',
      '**/.next/**',
      '**/dist/**',
      '**/bin/**',
      '**/obj/**',
      '**/next-env.d.ts',
    ],
  },
  js.configs.recommended,
  tseslint.configs.recommended,
  nextCoreWebVitals,
  {
    settings: {
      next: { rootDir: 'apps/portal' },
    },
    rules: {
      // Type-only import syntax is enforced by `verbatimModuleSyntax` in
      // tsconfig.base.json, so ESLint does not need type-aware linting here.
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],
      'no-console': ['error', { allow: ['info', 'warn', 'error'] }],
      eqeqeq: ['error', 'always'],
    },
  },
);
