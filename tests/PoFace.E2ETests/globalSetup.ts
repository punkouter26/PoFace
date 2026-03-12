/**
 * globalSetup.ts — runs once before all Playwright tests.
 *
 * 1. Starts an Azurite container via Docker so the API can connect to
 *    Azure Storage emulation on the default ports.
 * 2. Leaves the hosted client build to the API project itself. The API now
 *    references PoFace.Client directly and serves it through static web assets,
 *    so copying published client files into src/PoFace.Api/wwwroot would create
 *    duplicate assets and break dotnet run.
 */

import { execSync } from 'child_process';

const CONTAINER_NAME = 'poface-e2e-azurite';

/** Returns true when something is already listening on the port. */
function isPortInUse(port: number): Promise<boolean> {
    return new Promise(resolve => {
        const { createConnection } = require('net');
        const socket = createConnection({ port, host: '127.0.0.1' });
        socket.once('connect', () => { socket.destroy(); resolve(true); });
        socket.once('error', () => { socket.destroy(); resolve(false); });
    });
}

export default async function globalSetup(): Promise<void> {
    // ── 1. Azurite ─────────────────────────────────────────────────────────────
    // If Azurite ports are already bound (e.g. a dev container is running) skip
    // starting a second instance — the existing one will serve E2E tests.
    if (await isPortInUse(10000)) {
        console.log('[e2e] Azurite already running on port 10000 — reusing existing instance.');
        return;
    }

    console.log('[e2e] Starting Azurite container...');
    // Silently remove any stale container from a previous failed run.
    try { execSync(`docker rm -f ${CONTAINER_NAME}`, { stdio: 'ignore' }); } catch { /* ok */ }

    execSync(
        `docker run -d --name ${CONTAINER_NAME} ` +
        `-p 10000:10000 -p 10001:10001 -p 10002:10002 ` +
        `mcr.microsoft.com/azure-storage/azurite`,
        { stdio: 'pipe' }  // capture output; don't print Docker pull noise
    );
    console.log('[e2e] Azurite running.');
}
