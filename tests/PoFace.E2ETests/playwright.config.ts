import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  testIgnore: ['./fixtures/**'],
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',

  // ── One-time setup / teardown ───────────────────────────────────────────────
  // globalSetup publishes the Blazor WASM client and starts Azurite.
  // globalTeardown removes the Azurite container.
  globalSetup:    require.resolve('./globalSetup'),
  globalTeardown: require.resolve('./globalTeardown'),

  // ── Server under test ───────────────────────────────────────────────────────
  // Playwright starts the API server before tests and polls /api/health until
  // it responds.  reuseExistingServer lets developers keep a server running
  // manually during local iteration (avoids a slow restart each run).
  webServer: {
    command: [
      'dotnet', 'run',
      '--project', '../../src/PoFace.Api',
      '--urls', 'http://localhost:5000',
      '--no-launch-profile',
    ].join(' '),
    url:                 'http://localhost:5000/',
    reuseExistingServer: !process.env.CI,
    timeout:             120_000,
    env: {
      ASPNETCORE_ENVIRONMENT: 'Development',
      POFACE_ENABLE_KEYVAULT: 'false',
      // Blank out the tenant ID so the API takes the PoTestAuth branch instead
      // of the Entra ID branch, allowing X-Test-User-Id headers to authenticate.
      AzureAd__TenantId: '',
    },
  },

  use: {
    baseURL: process.env.BASE_URL ?? 'http://localhost:5000',
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
