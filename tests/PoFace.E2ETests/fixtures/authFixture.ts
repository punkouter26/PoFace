import { test as base, Page, BrowserContext } from '@playwright/test';

/**
 * Options accepted by the auth fixture.
 */
export type AuthOptions = {
    testUserId:      string;
    testDisplayName: string;
};

/**
 * Playwright fixture that injects the test-auth bypass headers on every
 * request so the PoTestAuth middleware in the API server identifies the
 * request as coming from a known test user without a real MSAL flow.
 *
 * Also exposes a `consoleErrors` array that collects browser-console errors
 * emitted during the test.  Assertions can use it to detect unexpected 404s,
 * JS exceptions, or CSP violations:
 *
 *   expect(consoleErrors.filter(m => m.includes('404'))).toHaveLength(0);
 *
 * Usage:
 *   import { test } from '../fixtures/authFixture';
 *   test('my test', async ({ page, consoleErrors }) => { ... });
 */
export const test = base.extend<{
    authOptions:   AuthOptions;
    consoleErrors: string[];
}>({
    authOptions: [
        {
            testUserId:      'test-e2e-001',
            testDisplayName: 'E2E User',
        },
        { option: true },
    ],

    // Collects all browser console-error messages emitted during the test.
    // Populated automatically; read it in your test to make assertions.
    consoleErrors: async ({ page }, use) => {
        const errors: string[] = [];
        page.on('console', msg => {
            if (msg.type() === 'error') errors.push(msg.text());
        });
        page.on('pageerror', err => errors.push(err.message));
        await use(errors);
    },

    // Override the default `page` fixture to always add the auth headers.
    // Uses page.route() — not extraHTTPHeaders — so headers are injected ONLY for
    // same-origin /api/* requests and never sent to external CDNs (e.g. Google Fonts),
    // which would trigger CORS preflight failures and console error noise.
    page: async ({ browser, authOptions }, use) => {
        const context: BrowserContext = await browser.newContext();
        const page: Page = await context.newPage();

        await page.route('**/api/**', async route => {
            await route.continue({
                headers: {
                    ...route.request().headers(),
                    'X-Test-User-Id':      authOptions.testUserId,
                    'X-Test-Display-Name': authOptions.testDisplayName,
                },
            });
        });

        await use(page);
        await context.close();
    },
});

export { expect } from '@playwright/test';
