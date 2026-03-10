/**
 * globalTeardown.ts — runs once after all Playwright tests.
 *
 * Stops and removes the Azurite Docker container that was started in globalSetup.
 */

import { execSync } from 'child_process';

const CONTAINER_NAME = 'poface-e2e-azurite';

export default async function globalTeardown(): Promise<void> {
    console.log('[e2e] Stopping Azurite container...');
    try {
        execSync(`docker rm -f ${CONTAINER_NAME}`, { stdio: 'ignore' });
        console.log('[e2e] Azurite stopped.');
    } catch {
        // If Docker is unavailable or the container already exited, ignore.
    }
}
