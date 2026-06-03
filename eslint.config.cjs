// ESLint flat config for the Velucid Nx workspace.
// Apps/web extends this with its own Astro/React rules; libs and other apps inherit it.
const nxEslintPlugin = require('@nx/eslint-plugin');

module.exports = [
  // Ignore build artifacts
  {
    ignores: [
      '**/node_modules/**',
      '**/dist/**',
      '**/.nx/**',
      '**/coverage/**',
      '**/build/**',
      'apps/web/dist/**',
      'apps/web/.astro/**',
    ],
  },
  // Base rules for all TypeScript projects
  {
    files: ['**/*.ts', '**/*.tsx', '**/*.js', '**/*.mjs', '**/*.cjs'],
    languageOptions: {
      ecmaVersion: 2022,
      sourceType: 'module',
    },
    rules: {
      'no-unused-vars': 'off', // TypeScript handles this
    },
  },
  // Module boundary enforcement: libs cannot import from apps.
  // Apps can import from libs and other apps. This is the tag rule
  // Epic 4 relies on to keep the shared-projection invariant.
  {
    files: ['libs/**/*.ts', 'apps/**/*.ts'],
    plugins: {
      '@nx': nxEslintPlugin,
    },
    rules: {
      '@nx/enforce-module-boundaries': [
        'error',
        {
          enforceBuildableLibDependency: true,
          allowCircularSelfDependency: false,
          banTransitiveDependencies: true,
          checkDynamicDependenciesExceptions: ['^@velucid/.+$'],
          depConstraints: [
            {
              sourceTag: 'type:lib',
              onlyDependOnLibsWithTags: ['type:lib'],
            },
            {
              sourceTag: 'type:app',
              onlyDependOnLibsWithTags: ['type:lib', 'type:app'],
            },
          ],
        },
      ],
    },
  },
];
