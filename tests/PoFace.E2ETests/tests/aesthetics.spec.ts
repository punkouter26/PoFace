import { test, expect } from '../fixtures/authFixture';

const pages = ['/', '/arena', '/leaderboard', '/diagnostics', `/recap/${'a'.repeat(32)}`];

test.describe('Terminal aesthetic baseline', () => {
    // Serial so the responsive-layout test always runs before the no-audio game loop
    // (moved to game-loop.spec.ts) and so palette tests never compete for workers.
    test.describe.configure({ mode: 'serial' });

    for (const route of pages) {
        test(`page ${route} uses terminal palette and scanline`, async ({ page }) => {
            await page.goto(route);

            const colors = await page.evaluate(() => {
                const body = window.getComputedStyle(document.body);
                return {
                    bg: body.backgroundColor,
                    color: body.color,
                };
            });

            expect(colors.bg).toBe('rgb(10, 10, 10)');
            expect(colors.color).toBe('rgb(0, 255, 0)');

            const monospaceFound = await page.evaluate(() => {
                const nodes = Array.from(document.querySelectorAll<HTMLElement>('body, body *'));
                return nodes.some(n => window.getComputedStyle(n).fontFamily.toLowerCase().includes('monospace')
                    || window.getComputedStyle(n).fontFamily.toLowerCase().includes('courier'));
            });
            expect(monospaceFound).toBeTruthy();

            await expect(page.locator('.crt-scanline')).toHaveCount(1);
        });
    }

    test('responsive layout has no horizontal overflow and touch targets are >= 44px', async ({ page }) => {
        // 5 pages × ~4 s each under mild server load; increase from default 30 s to be safe.
        test.setTimeout(60_000);
        const targets = ['/', '/arena', '/leaderboard', '/diagnostics', `/recap/${'a'.repeat(32)}`];

        for (const route of targets) {
            await page.setViewportSize({ width: 390, height: 844 });
            await page.goto(route);

            const noOverflow = await page.evaluate(() => document.body.scrollWidth <= 390);
            expect(noOverflow).toBeTruthy();

            const buttonsOk = await page.evaluate(() => {
                const buttons = Array.from(document.querySelectorAll<HTMLElement>('button'));
                return buttons.every(button => button.offsetHeight >= 44 && button.offsetWidth >= 44);
            });
            expect(buttonsOk).toBeTruthy();
        }
    });

});
