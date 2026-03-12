/**
 * globalTeardown.ts — runs once after all Playwright tests.
 *
 * Stops and removes the Azurite Docker container that was started in globalSetup,
 * but only if the container is named 'poface-e2e-azurite' (i.e. we started it).
 * When a pre-existing dev container was reused, this is a no-op.
 */

import { execSync } from 'child_process';

const CONTAINER_NAME = 'poface-e2e-azurite';

export default async function globalTeardown(): Promise<void> {
    // Only stop the container if globalSetup created it under our name.
    let containerExists = false;
    try {
        const out = execSync(`docker inspect --format "{{.Name}}" ${CONTAINER_NAME}`, { stdio: 'pipe' }).toString();
        containerExists = out.includes(CONTAINER_NAME);
    } catch { /* container not found */ }

    if (!containerExists) {
        console.log('[e2e] No E2E-owned Azurite container to stop — skipping.');
        return;
    }

    console.log('[e2e] Stopping Azurite container...');
    try {
        execSync(`docker rm -f ${CONTAINER_NAME}`, { stdio: 'ignore' });
        console.log('[e2e] Azurite stopped.');
    } catch {
        // If Docker is unavailable or the container already exited, ignore.
    }
}
