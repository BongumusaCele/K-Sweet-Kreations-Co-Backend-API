// Disposable HTTP integration server. Never connects to the application's SQL Server.
import { spawn, spawnSync } from 'node:child_process'
import { mkdtempSync, mkdirSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { randomBytes } from 'node:crypto'

const project = resolve(fileURLToPath(new URL('../K-Sweet-Kreations-Co-Backend-API', import.meta.url)))
const build = spawnSync('dotnet', ['build', project, '--no-restore', '--verbosity', 'quiet'], { stdio: 'inherit', windowsHide: true })
if (build.status !== 0) process.exit(build.status || 1)
const directory = mkdtempSync(join(tmpdir(), 'ksk-integration-'))
mkdirSync(join(directory, 'wwwroot'))
const child = spawn('dotnet', [join(project, 'bin/Debug/net10.0/K-Sweet-Kreations-Co-Backend-API.dll'), '--urls', 'http://127.0.0.1:5089'], {
  cwd: project, stdio: 'inherit', windowsHide: true,
  env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Testing',
    ConnectionStrings__DefaultConnection: `Data Source=${join(directory, 'test.db')}`,
    Jwt__Key: randomBytes(64).toString('hex'), SeedAdmin__Enabled: 'true',
    SeedAdmin__Email: 'admin@integration.test', SeedAdmin__Password: 'IntegrationOnly123!',
    Storage__PrivateRoot: join(directory, 'uploads'), Storage__BlobConnectionString: '', Website__RedirectOrigin: '', ASPNETCORE_WEBROOT: join(directory, 'wwwroot'), RateLimits__RequestsPerMinute: '10000', RateLimits__IdentityPerMinute: '1000',
    Logging__LogLevel__Default: 'Warning', Logging__LogLevel__Microsoft: 'Warning',
  },
})
let stopping = false
function stop() { if (!stopping) { stopping = true; child.kill() } }
process.on('SIGTERM', stop)
process.on('SIGINT', stop)
child.on('exit', code => { try { rmSync(directory, { recursive: true, force: true }) } catch { /* OS may still hold SQLite handles. */ } process.exit(code || 0) })
