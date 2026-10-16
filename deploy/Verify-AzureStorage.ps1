param([string]$AssemblyPath)
$ErrorActionPreference = 'Stop'
$credentials = Get-Content (Join-Path $PSScriptRoot 'credentials.local.json') -Raw | ConvertFrom-Json
$state = Get-Content (Join-Path $PSScriptRoot 'deployment.local.json') -Raw | ConvertFrom-Json
if (!$AssemblyPath) { $AssemblyPath = Join-Path $state.publish 'K-Sweet-Kreations-Co-Backend-API.dll' }
$temporary = Join-Path $PSScriptRoot ('artifacts/storage-check-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temporary | Out-Null
$settings = @{
    Jwt__Key=$credentials.jwtKey; Storage__BlobConnectionString=$credentials.storageConnectionString;
    Storage__PrivateRoot=(Join-Path $temporary 'private'); Storage__PublicRoot=(Join-Path $temporary 'products');
    SeedAdmin__Enabled='false'; ASPNETCORE_ENVIRONMENT='Production'; Website__RedirectOrigin=''
}
$previous = @{}
try {
    foreach ($entry in $settings.GetEnumerator()) {
        $previous[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
        [Environment]::SetEnvironmentVariable($entry.Key,$entry.Value,'Process')
    }
    & dotnet $AssemblyPath --verify-media-storage
    if ($LASTEXITCODE -ne 0) { throw 'Azure Blob Storage roundtrip failed.' }
} finally {
    foreach ($entry in $previous.GetEnumerator()) { [Environment]::SetEnvironmentVariable($entry.Key,$entry.Value,'Process') }
}
