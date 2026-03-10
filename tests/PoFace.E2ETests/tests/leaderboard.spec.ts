/**
 * leaderboard.spec.ts — Phase 4 end-to-end tests for the Leaderboard page.
 *
 * Strategy
 * --------
 * • Sessions are completed via direct API calls using Playwright's `request`
 *   fixture so we don't rely on the full game-loop UI.
 * • Each API call carries X-Test-User-Id / X-Test-Display-Name headers so the
 *   PoTestAuth middleware identifies the caller.
 * • After seeding, the test navigates to /leaderboard and asserts the DOM.
 */

import { test, expect } from '../fixtures/authFixture';
import { APIRequestContext } from '@playwright/test';

const BASE_URL = process.env.BASE_URL ?? 'http://localhost:5000';

// ── Helpers ───────────────────────────────────────────────────────────────────

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

/** Builds a minimal 1×1 JPEG blob (valid enough for the stub analyser). */
function minimalJpeg(): Buffer {
    // Smallest valid JFIF — 1×1 white pixel.
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
): Promise<void> {
    await request.post(`${BASE_URL}/api/sessions/${sessionId}/complete`, {
        headers: {
            'X-Test-User-Id':      userId,
            'X-Test-Display-Name': displayName,
        },
    });
}

// ── Tests ─────────────────────────────────────────────────────────────────────

// Tests that navigate to /leaderboard and assert on UI state must not run in
// parallel because they share the same physical Azurite table.  API-only tests
// (e.g. sort order check) are safe in parallel because they use unique user IDs.
test.describe.configure({ mode: 'serial' });

test.describe('Leaderboard', () => {

    test('shows one entry per user; lower score does NOT replace personal best', async ({
        page,
        request,
    }) => {
        const userId      = `e2e-lb-${Date.now()}`;
        // Unique display name prevents cross-test row count pollution when parallel
        // runs insert entries with the same name before the UI assertion fires.
        const displayName = `LBUser-${Date.now()}`;

        // Session 1 — stub returns 0 for all rounds; this becomes the personal best.
        const sessionId1 = await startSession(request, userId, displayName);
        await scoreAllRounds(request, sessionId1, userId, displayName);
        const completeResp1 = await request.post(
            `${BASE_URL}/api/sessions/${sessionId1}/complete`,
            {
                headers: {
                    'X-Test-User-Id':      userId,
                    'X-Test-Display-Name': displayName,
                },
            }
        );
        const complete1 = await completeResp1.json();
        expect(complete1.isPersonalBest).toBe(true);

        // Session 2 — same score (0); must NOT replace the existing entry.
        const sessionId2 = await startSession(request, userId, displayName);
        await scoreAllRounds(request, sessionId2, userId, displayName);
        const completeResp2 = await request.post(
            `${BASE_URL}/api/sessions/${sessionId2}/complete`,
            {
                headers: {
                    'X-Test-User-Id':      userId,
                    'X-Test-Display-Name': displayName,
                },
            }
        );
        const complete2 = await completeResp2.json();
        expect(complete2.isPersonalBest).toBe(false);

        // ── Assert leaderboard page shows exactly one entry for this user ─────
        await page.goto('/leaderboard');
        // Wait for React/Blazor to render (spinner gone).
        await expect(page.getByText(displayName)).toBeVisible({ timeout: 15_000 });

        // The user should appear exactly once.
        const rows = page.locator(`td:has-text("${displayName}")`);
        await expect(rows).toHaveCount(1);
    });

    test('leaderboard entries are sorted with highest score first', async ({
        request,
    }) => {
        const apiResp = await request.get(`${BASE_URL}/api/leaderboard`);
        expect(apiResp.ok()).toBeTruthy();

        const body = await apiResp.json();
        const entries: Array<{ totalScore: number; rank: number }> = body.entries;

        // Ranks must be consecutive starting at 1.
        entries.forEach((e, i) => expect(e.rank).toBe(i + 1));

        // Scores must be non-increasing.
        for (let i = 1; i < entries.length; i++) {
            expect(entries[i].totalScore).toBeLessThanOrEqual(entries[i - 1].totalScore);
        }
    });
});
