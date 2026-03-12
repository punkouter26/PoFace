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

import { test, expect } from '../fixtures/authFixture';

test.describe('Arena game loop', () => {
    // Serial execution prevents multiple game loops from running concurrently,
    // which would overload the shared server/Azurite instance and cause timeouts
    // in unrelated tests. Each game loop makes 7 API calls and takes ~1.4 min.
    test.describe.configure({ mode: 'serial' });

    test.beforeEach(async ({ page }) => {
        // ── Stub the webcamInterop JS layer so the game loop runs in headless CI ───
        //
        // webcam.js runs `window.webcamInterop = {...}` at module load time.
        // By using Object.defineProperty with a no-op setter BEFORE the script
        // loads, the assignment is silently discarded and our stub stays in place.
        //
        // captureFrame returns a valid 1-frame JPEG so the scoring endpoint
        // accepts the multipart upload. The server-side StubFaceAnalysisService
        // returns score=0 for every round, keeping the game deterministic.
        await page.addInitScript(() => {
            // Build minimal JPEG data URL (same 1×1 JFIF bytes used in recap tests).
            const _hex = 'ffd8ffe000104a46494600010100000100010000ffdb00430008060607060508070707090908080' +
                         '90c140d0c0b0b0c1912130f141d1a1f1e1d1a1c1c20242e2720222c231c1c2829373d3e3b31364d4' +
                         'a373e48393739353c3d3bffc0000b080001000101011100ffda000801010000003f00fad4000000000' +
                         '000000000ffff0000000000000000000000000000000000000000000000ffd9';
            const _bytes = new Uint8Array(_hex.length / 2);
            for (let i = 0; i < _bytes.length; i++) _bytes[i] = parseInt(_hex.substr(i * 2, 2), 16);
            let _bin = '';
            _bytes.forEach(b => _bin += String.fromCharCode(b));
            const FAKE_FRAME = 'data:image/jpeg;base64,' + btoa(_bin);

            const _stub = {
                getCameraPermissionState: () => Promise.resolve('granted'),
                initCamera:   (_id: string)  => Promise.resolve('ok'),
                captureFrame: (_id: string)  => Promise.resolve(FAKE_FRAME),
                releaseCamera: ()            => {},
                flashShutter: (_id: string)  => Promise.resolve(),
            };

            // Prevent webcam.js from overwriting this stub.
            Object.defineProperty(window, 'webcamInterop', {
                configurable: false,
                enumerable:   true,
                get: ()  => _stub,
                set: ()  => { /* ignore webcam.js assignment */ },
            });
        });
    });

    test('plays all 5 rounds and shows total score ≤ 50', async ({ page, consoleErrors }) => {
        test.setTimeout(120_000); // 5 rounds × ~8 s each = ~45 s minimum run time
        await page.goto('/arena');

        // Wait for the arcade page to initialise (GET READY appears for round 1).
        // Allow 30 s to account for server load from concurrent game-loop tests.
        await expect(page.getByText('ROUND 1')).toBeVisible({ timeout: 30_000 });

        // Wait for the full game-over screen (all 5 rounds × ~8 s each = up to 60 s).
        await expect(page.getByText('GAME OVER')).toBeVisible({ timeout: 90_000 });

        // Extract score from the DOM and verify it is in the valid range 0–50.
        const scoreText = await page.locator('.total-score').innerText();
        const match     = scoreText.match(/(\d+)\s*\/\s*50/);
        expect(match).not.toBeNull();
        const score = parseInt(match![1], 10);
        expect(score).toBeGreaterThanOrEqual(0);
        expect(score).toBeLessThanOrEqual(50);

        // No JS errors should occur during gameplay.
        expect(consoleErrors).toHaveLength(0);
    });

    test('shows Play Again button after game over', async ({ page, consoleErrors }) => {
        test.setTimeout(120_000); // full game loop needed
        await page.goto('/arena');
        await expect(page.getByText('GAME OVER')).toBeVisible({ timeout: 90_000 });
        await expect(page.getByRole('button', { name: 'Play Again' })).toBeVisible();
        expect(consoleErrors).toHaveLength(0);
    });

    // Aesthetics check that lives here (not in aesthetics.spec.ts) so that it runs
    // serially with the other game-loop tests and never creates a second concurrent
    // game loop, which would saturate Azurite and cause unrelated test timeouts.
    test('no audio files are requested during one full arena session', async ({ page }) => {
        test.setTimeout(120_000);
        const blockedAudioRequests: string[] = [];
        page.on('request', req => {
            const url = req.url().toLowerCase();
            if (url.endsWith('.mp3') || url.endsWith('.wav') || url.endsWith('.ogg') || url.endsWith('.flac')) {
                blockedAudioRequests.push(url);
            }
        });
        // webcamInterop stub already installed by beforeEach.
        await page.goto('/arena');
        await expect(page.getByText('GAME OVER')).toBeVisible({ timeout: 90_000 });
        expect(blockedAudioRequests).toHaveLength(0);
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
        test.setTimeout(120_000); // slow-network page load can take 30-60 s
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
        await expect(page.getByText('ROUND 1')).toBeVisible({ timeout: 60_000 }); // allow for slow-network page load

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
