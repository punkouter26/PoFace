/**
 * recap.spec.ts — Phase 5 end-to-end tests for the public Recap page.
 *
 * Strategy
 * --------
 * • Sessions are completed via direct API calls (auth headers on the API only).
 * • The Recap page is navigated to WITHOUT auth cookies to prove it is publicly
 *   accessible (shareable link requirement FR-031).
 * • Assertions verify: round panels, emotion labels, score display, and expiry.
 */

import { test, expect } from '../fixtures/authFixture';
import { APIRequestContext } from '@playwright/test';

const BASE_URL = process.env.BASE_URL ?? 'http://localhost:5000';

// ── Helpers ────────────────────────────────────────────────────────────────────

async function startSession(
    request: APIRequestContext,
    userId: string,
    displayName: string
): Promise<string> {
    const resp = await request.post(`${BASE_URL}/api/sessions`, {
        headers: {
            'X-Test-User-Id':      userId,
            'X-Test-Display-Name': displayName,
        },
    });
    const body = await resp.json();
    return body.sessionId as string;
}

/** Smallest valid 1×1 JFIF JPEG — accepted by the stub FaceAnalysisService. */
function minimalJpeg(): Buffer {
    return Buffer.from(
        'ffd8ffe000104a46494600010100000100010000ffdb004300080606070605080707070909' +
        '0808090c140d0c0b0b0c1912130f141d1a1f1e1d1a1c1c20242e2720222c231c1c282' +
        '9373d3e3b31364d4a373e48393739353c3d3bffc0000b080001000101011100ffda000' +
        '801010000003f00fad4000000000000000000ffff000000000000000000000000000000' +
        '0000000000000000ffd9',
        'hex'
    );
}

async function scoreAllRounds(
    request: APIRequestContext,
    sessionId: string,
    userId: string,
    displayName: string
): Promise<void> {
    const jpeg = minimalJpeg();
    for (let round = 1; round <= 5; round++) {
        await request.post(
            `${BASE_URL}/api/sessions/${sessionId}/rounds/${round}/score`,
            {
                headers: {
                    'X-Test-User-Id':      userId,
                    'X-Test-Display-Name': displayName,
                },
                // Must be multipart — the endpoint binds from IFormFile, not raw body.
                multipart: {
                    image: {
                        name:     'frame.jpg',
                        mimeType: 'image/jpeg',
                        buffer:   jpeg,
                    },
                },
            }
        );
    }
}

async function completeSession(
    request: APIRequestContext,
    sessionId: string,
    userId: string,
    displayName: string
): Promise<{ recapUrl: string; isPersonalBest: boolean }> {
    const resp = await request.post(
        `${BASE_URL}/api/sessions/${sessionId}/complete`,
        {
            headers: {
                'X-Test-User-Id':      userId,
                'X-Test-Display-Name': displayName,
            },
        }
    );
    return resp.json();
}

// ── Tests ──────────────────────────────────────────────────────────────────────

test.describe('Recap Page', () => {

    test('public recap shows all 5 rounds without authentication cookies', async ({
        page,
        request,
    }) => {
        const userId      = `e2e-recap-${Date.now()}`;
        const displayName = 'RecapUser';

        const sessionId   = await startSession(request, userId, displayName);
        await scoreAllRounds(request, sessionId, userId, displayName);
        const { recapUrl } = await completeSession(request, sessionId, userId, displayName);

        // Navigate WITHOUT auth cookies — recap must be publicly accessible.
        await page.context().clearCookies();
        await page.goto(recapUrl);

        // Wait for the page to load (spinner disappears, player name visible).
        await expect(page.getByText(displayName)).toBeVisible({ timeout: 15_000 });

        // All 5 round panels must be present (each rendered by <RoundPanel>).
        for (let i = 1; i <= 5; i++) {
            await expect(page.getByText(`Round ${i}`)).toBeVisible();
        }

        // Score "/" display should appear 5 times (one per round panel).
        const scoreTexts = page.locator('.round-panel').filter({ hasText: '/ 10' });
        await expect(scoreTexts).toHaveCount(5);
    });

    test('recap page shows correct target emotion labels for each round', async ({
        page,
        request,
    }) => {
        const userId      = `e2e-emotions-${Date.now()}`;
        const displayName = 'EmotionUser';
        const emotions    = ['Happiness', 'Surprise', 'Anger', 'Sadness', 'Fear'];

        const sessionId   = await startSession(request, userId, displayName);
        await scoreAllRounds(request, sessionId, userId, displayName);
        const { recapUrl } = await completeSession(request, sessionId, userId, displayName);

        await page.context().clearCookies();
        await page.goto(recapUrl);
        await expect(page.getByText(displayName)).toBeVisible({ timeout: 15_000 });

        // All 5 canonical emotion names must appear in the recap.
        for (const emotion of emotions) {
            await expect(page.getByText(emotion).first()).toBeVisible();
        }
    });

    test('recap API returns 404 for unknown sessionId', async ({ request }) => {
        // 32 hex chars, valid format but no matching session.
        const fakeId = 'a'.repeat(32);
        const resp   = await request.get(`${BASE_URL}/api/recap/${fakeId}`);
        expect(resp.status()).toBe(404);
    });

    test('personal best recap has public cache-control header', async ({ request }) => {
        const userId      = `e2e-cache-${Date.now()}`;
        const displayName = 'CacheUser';

        const sessionId = await startSession(request, userId, displayName);
        await scoreAllRounds(request, sessionId, userId, displayName);
        const { recapUrl } = await completeSession(request, sessionId, userId, displayName);

        // recapUrl is the SPA route (/recap/{id}); the cache-control header is set by
        // the API endpoint, so we must call /api/recap/{id} directly.
        const resp = await request.get(`${BASE_URL}/api${recapUrl}`);
        expect(resp.status()).toBe(200);

        const cacheControl = resp.headers()['cache-control'] ?? '';
        expect(cacheControl).toContain('public');
        expect(cacheControl).toContain('max-age=3600');
    });
});
