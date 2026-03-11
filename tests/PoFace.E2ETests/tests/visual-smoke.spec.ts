import { test, expect } from '@playwright/test';

type VisualRoute = {
    name: string;
    route: string;
    assertReady: (page: import('@playwright/test').Page) => Promise<void>;
};

const routes: VisualRoute[] = [
    {
        name: 'home',
        route: '/',
        assertReady: page => expect(page.getByRole('link', { name: 'Start Game' })).toBeVisible({ timeout: 15_000 }),
    },
    {
        name: 'leaderboard',
        route: '/leaderboard',
        assertReady: page => expect(page.getByRole('heading', { name: 'Leaderboard' })).toBeVisible({ timeout: 15_000 }),
    },
];

test.describe('Visual smoke', () => {
    for (const route of routes) {
        test(`${route.name} renders without Blazor error UI`, async ({ page }, testInfo) => {
            const consoleProblems: string[] = [];
            page.on('console', msg => {
                if (msg.type() === 'error') {
                    consoleProblems.push(msg.text());
                }
            });
            page.on('pageerror', err => consoleProblems.push(err.message));

            await page.goto(route.route);
            await route.assertReady(page);

            await expect(page.locator('#blazor-error-ui')).toBeHidden();
            expect(consoleProblems).toEqual([]);

            await testInfo.attach(`${route.name}-screenshot`, {
                body: await page.screenshot({ fullPage: true }),
                contentType: 'image/png',
            });
        });
    }

    test('arena shows a retryable camera message when camera permission is denied', async ({ browser }, testInfo) => {
        const consoleErrors: string[] = [];
        const context = await browser.newContext();
        const page = await context.newPage();

        page.on('console', msg => {
            if (msg.type() === 'error') {
                consoleErrors.push(msg.text());
            }
        });
        page.on('pageerror', err => consoleErrors.push(err.message));

        await page.addInitScript(() => {
            if (!navigator.mediaDevices) {
                Object.defineProperty(navigator, 'mediaDevices', {
                    value: {},
                    configurable: true,
                });
            }

            (navigator.mediaDevices as MediaDevices & { getUserMedia: typeof navigator.mediaDevices.getUserMedia }).getUserMedia = async () => {
                throw new DOMException('Permission denied', 'NotAllowedError');
            };
        });

        await page.goto('/arena');

        await expect(page.getByRole('heading', { name: 'Camera Access Required' })).toBeVisible({ timeout: 15_000 });
        await expect(page.getByText('Camera access was blocked. Allow camera permission in the browser and retry.')).toBeVisible();
        await expect(page.locator('#blazor-error-ui')).toBeHidden();
        expect(consoleErrors).toEqual([]);

        await testInfo.attach('arena-permission-denied', {
            body: await page.screenshot({ fullPage: true }),
            contentType: 'image/png',
        });

        await context.close();
    });
});