/**
 * health.spec.ts — preflight smoke test.
 *
 * Verifies the API server is reachable and the /api/health endpoint returns
 * a healthy response before any other tests run.  If this test fails, all
 * subsequent tests should be skipped (use `--stop-on-first-failure` in CI).
 */

import { test, expect } from '@playwright/test';

const BASE_URL = process.env.BASE_URL ?? 'http://localhost:5000';

test.describe('API health', () => {

    test('GET /api/health returns 200 with status ok', async ({ request }) => {
        const resp = await request.get(`${BASE_URL}/api/health`);
        expect(resp.status()).toBe(200);

        const body = await resp.json();
        expect(body.status).toBe('ok');
    });

    test('SPA home page returns 200 with HTML content', async ({ request }) => {
        const resp = await request.get(`${BASE_URL}/`);
        expect(resp.status()).toBe(200);

        const contentType = resp.headers()['content-type'] ?? '';
        expect(contentType).toContain('text/html');
    });

    test('unknown API route returns 404 not the SPA fallback', async ({ request }) => {
        const resp = await request.get(`${BASE_URL}/api/this-does-not-exist`);
        // API routes must not fall through to the SPA fallback.
        expect(resp.status()).toBe(404);
    });

});
