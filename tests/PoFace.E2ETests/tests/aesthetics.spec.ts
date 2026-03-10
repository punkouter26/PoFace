import { test, expect } from '../fixtures/authFixture';

const pages = ['/', '/arena', '/leaderboard', '/diagnostics', `/recap/${'a'.repeat(32)}`];

test.describe('Terminal aesthetic baseline', () => {
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

    test('no audio files are requested during one full arena session', async ({ page }) => {
        const blockedAudioRequests: string[] = [];

        page.on('request', req => {
            const url = req.url().toLowerCase();
            if (url.endsWith('.mp3') || url.endsWith('.wav') || url.endsWith('.ogg') || url.endsWith('.flac')) {
                blockedAudioRequests.push(url);
            }
        });

        await page.goto('/arena');
        await expect(page.getByText('GAME OVER')).toBeVisible({ timeout: 90_000 });
        expect(blockedAudioRequests).toHaveLength(0);
    });
});
