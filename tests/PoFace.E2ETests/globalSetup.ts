/**
 * globalSetup.ts — runs once before all Playwright tests.
 *
 * 1. Starts an Azurite container via Docker so the API can connect to
 *    Azure Storage emulation on the default ports.
 * 2. Publishes the Blazor WASM client into src/PoFace.Api/wwwroot so the
 *    API server (launched by webServer) can serve the SPA static files.
 */

import { execSync } from 'child_process';
import * as fs   from 'fs';
import * as os   from 'os';
import * as path from 'path';

const CONTAINER_NAME = 'poface-e2e-azurite';
const ROOT           = path.resolve(__dirname, '../..');

export default async function globalSetup(): Promise<void> {
    // ── 1. Azurite ─────────────────────────────────────────────────────────────
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

    // ── 2. Publish Blazor WASM ─────────────────────────────────────────────────
    const clientProject = path.join(ROOT, 'src', 'PoFace.Client');
    const tmpOut        = path.join(os.tmpdir(), 'poface-e2e-client-publish');
    const srcWwwroot    = path.join(tmpOut, 'wwwroot');
    const apiWwwroot    = path.join(ROOT, 'src', 'PoFace.Api', 'wwwroot');

    console.log('[e2e] Publishing Blazor WASM client...');
    execSync(
        `dotnet publish "${clientProject}" -o "${tmpOut}" --nologo -c Debug`,
        { stdio: 'inherit', cwd: ROOT }
    );

    // Copy the published web assets (wwwroot subfolder) into the API project's wwwroot.
    if (fs.existsSync(apiWwwroot)) fs.rmSync(apiWwwroot, { recursive: true, force: true });
    fs.cpSync(srcWwwroot, apiWwwroot, { recursive: true });
    console.log(`[e2e] Client assets copied to ${apiWwwroot}`);
}
