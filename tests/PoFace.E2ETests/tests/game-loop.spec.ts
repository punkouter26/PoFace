/**
 * game-loop.spec.ts — Phase 3 end-to-end tests for the Arena game loop.
 *
 * Strategy
 * --------
 * • The browser's getUserMedia API is replaced with a fake MediaStream that
 *   supplies an all-black 640×480 canvas-based stream. This avoids needing a
 *   physical camera in CI.
 * • The API is called over the real test-server HTTP stack (testcontainers in
 *   the ASP.NET pipeline, BASE_URL set in playwright.config.ts).
 * • Gameplay is anonymous by default, so no auth bootstrap is required.
 */

import { test, expect } from '@playwright/test';

test.describe('Arena game loop', () => {

    test.beforeEach(async ({ page }) => {
        // ── Replace camera with a synthetic black-frame MediaStream ──────────
        await page.addInitScript(() => {
            const canvas = document.createElement('canvas');
            canvas.width  = 640;
            canvas.height = 480;
            const ctx = canvas.getContext('2d')!;
            ctx.fillStyle = '#000';
            ctx.fillRect(0, 0, 640, 480);

            // Keep drawing so the stream is "live".
            const keepAlive = () => {
                ctx.fillRect(0, 0, 1, 1);
                requestAnimationFrame(keepAlive);
            };
            requestAnimationFrame(keepAlive);

            const fakeStream = (canvas as any).captureStream(10);
            const original   = navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices);
            (navigator.mediaDevices as any).getUserMedia = async (constraints: MediaStreamConstraints) => {
                if (constraints.video) return fakeStream;
                return original(constraints);
            };
        });
    });

    test('plays all 5 rounds and shows total score ≤ 50', async ({ page }) => {
        await page.goto('/arena');

        // Wait for the arcade page to initialise (GET READY appears for round 1).
        await expect(page.getByText('ROUND 1')).toBeVisible({ timeout: 10_000 });

        // Wait for the full game-over screen (all 5 rounds × ~8 s each = up to 60 s).
        await expect(page.getByText('GAME OVER')).toBeVisible({ timeout: 90_000 });

        // Extract score from the DOM and verify it is in the valid range 0–50.
        const scoreText = await page.locator('.total-score').innerText();
        const match     = scoreText.match(/(\d+)\s*\/\s*50/);
        expect(match).not.toBeNull();
        const score = parseInt(match![1], 10);
        expect(score).toBeGreaterThanOrEqual(0);
        expect(score).toBeLessThanOrEqual(50);
    });

    test('shows Play Again button after game over', async ({ page }) => {
        await page.goto('/arena');
        await expect(page.getByText('GAME OVER')).toBeVisible({ timeout: 90_000 });
        await expect(page.getByRole('button', { name: 'Play Again' })).toBeVisible();
    });

    test('score endpoint returns 413 when payload too large', async ({ request }) => {
        // Direct API check — no browser required.
        const bytes     = new Uint8Array(600 * 1024); // 600 KB
        bytes[0] = 0xFF; bytes[1] = 0xD8; // JPEG magic

        const response = await request.post('/api/sessions/e2e-oversized/rounds/1/score', {
            multipart: {
                image: {
                    name:     'frame.jpg',
                    mimeType: 'image/jpeg',
                    buffer:   Buffer.from(bytes),
                },
            },
            headers: {
                'X-Test-User-Id': 'test-e2e-001',
            },
        });

        expect(response.status()).toBe(413);
    });

    test('on slow network frozen frame appears within 3s and payload is under 40KB', async ({ page }) => {
        const cdp = await page.context().newCDPSession(page);
        await cdp.send('Network.enable');
        await cdp.send('Network.emulateNetworkConditions', {
            offline: false,
            latency: 400,
            downloadThroughput: 375000,
            uploadThroughput: 125000,
        });

        let firstScorePayloadBytes = -1;
        page.on('request', req => {
            if (req.url().includes('/api/sessions/') && req.url().includes('/score')) {
                const body = req.postDataBuffer();
                if (body && firstScorePayloadBytes < 0) {
                    firstScorePayloadBytes = body.byteLength;
                }
            }
        });

        await page.goto('/arena');
        await expect(page.getByText('ROUND 1')).toBeVisible({ timeout: 12_000 });

        const start = Date.now();
        await expect(page.locator('#frozen-frame-overlay')).toBeVisible({ timeout: 3_000 });
        const elapsed = Date.now() - start;
        expect(elapsed).toBeLessThanOrEqual(3000);

        await expect.poll(() => firstScorePayloadBytes, {
            timeout: 20_000,
            message: 'Expected first score request payload to be captured',
        }).toBeGreaterThan(0);

        expect(firstScorePayloadBytes).toBeLessThan(40 * 1024);
    });
});
