[CmdletBinding()]
param(
    [ValidateSet('Prepare', 'Validate', 'Deploy', 'Update', 'Migrate')][string]$Mode = 'Prepare',
    [string]$SubscriptionId = 'e81cac15-7128-4116-a416-bc57e51fdbb8',
    [string]$ResourceGroup = 'RSG-RCGPON-ST10127074-SANORTH',
    [string]$Location = 'southafricanorth',
    [string]$AppName = 'k-sweet-creations-co',
    [string]$PlanName = 'asp-ksweetkreations-basic',
    [string]$SqlServerName = 'ksweetkreations-sql-4d7f',
    [string]$DatabaseName = 'KSweetKreations',
    [string]$AdminEmail = 'ST10127074@rcconnect.edu.za'
)
$ErrorActionPreference = 'Stop'
$deployRoot = $PSScriptRoot
$repository = Split-Path $deployRoot -Parent
$workspace = Split-Path $repository -Parent
$frontend = Join-Path $workspace 'K-Sweet-Kreations-Co-React-Website'
$project = Join-Path $repository 'K-Sweet-Kreations-Co-Backend-API'
$artifacts = Join-Path $deployRoot 'artifacts'
$credentialsPath = Join-Path $deployRoot 'credentials.local.json'
$parametersPath = Join-Path $deployRoot 'parameters.local.json'
$settingsPath = Join-Path $deployRoot 'settings.local.json'
$statePath = Join-Path $deployRoot 'deployment.local.json'

function Invoke-Azure([string[]]$Arguments) {
    $result = & az @Arguments --only-show-errors
    if ($LASTEXITCODE -ne 0) { throw "Azure command failed: $($Arguments[0..([Math]::Min(2,$Arguments.Length-1))] -join ' ')" }
    return $result
}
function Secret { return 'Zr9!' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)) }
function Save-PrivateJson($Value, [string]$Path) {
    $alreadyExists = Test-Path -LiteralPath $Path
    $Value | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $Path -Encoding utf8
    # Updating a protected file preserves its existing DACL. Reapplying a fresh
    # security descriptor on Windows can request an unnecessary SACL privilege.
    if ($alreadyExists) { return }
    if ($IsWindows) {
        $acl = [Security.AccessControl.FileSecurity]::new()
        $acl.SetAccessRuleProtection($true, $false)
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent().Name
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($identity, 'FullControl', 'Allow'))
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new('NT AUTHORITY\SYSTEM', 'FullControl', 'Allow'))
        Set-Acl -LiteralPath $Path -AclObject $acl
    } else { [IO.File]::SetUnixFileMode($Path, [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite) }
}
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
if (Test-Path -LiteralPath $credentialsPath) {
    $credentials = Get-Content -LiteralPath $credentialsPath -Raw | ConvertFrom-Json
    if ($credentials.adminEmail -ne $AdminEmail) { throw 'Existing deployment credentials use a different Admin email. Review them before continuing.' }
} else {
    $credentials = [ordered]@{ adminEmail = $AdminEmail; adminPassword = (Secret); sqlAdmin = 'ksk_deployer'; sqlPassword = (Secret); applicationPassword = (Secret); jwtKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(64)) }
    Save-PrivateJson $credentials $credentialsPath
}
$parameters = @{ '$schema' = 'https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#'; contentVersion = '1.0.0.0'; parameters = @{} }
foreach ($entry in @{ location=$Location; appName=$AppName; planName=$PlanName; sqlServerName=$SqlServerName; databaseName=$DatabaseName; sqlAdmin=$credentials.sqlAdmin; sqlPassword=$credentials.sqlPassword }.GetEnumerator()) {
    $parameters.parameters[$entry.Key] = @{ value = $entry.Value }
}
Save-PrivateJson $parameters $parametersPath

if ($Mode -eq 'Prepare') {
    Push-Location $frontend
    try {
        $oldBase = $env:VITE_BASE_PATH; $oldApi = $env:VITE_API_URL
        $env:VITE_BASE_PATH = '/'; $env:VITE_API_URL = '/api'
        & npm run build
        if ($LASTEXITCODE -ne 0) { throw 'Frontend production build failed.' }
    } finally { $env:VITE_BASE_PATH=$oldBase; $env:VITE_API_URL=$oldApi; Pop-Location }
    # A fresh output directory prevents stale deployment files without deleting prior packages.
    $publish = Join-Path $artifacts ('publish-' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))
    & dotnet publish $project --configuration Release --output $publish --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'API publish failed.' }
    $webRoot = Join-Path $publish 'wwwroot'
    New-Item -ItemType Directory -Path $webRoot -Force | Out-Null
    Copy-Item -Path (Join-Path $frontend 'dist/*') -Destination $webRoot -Recurse -Force
    $zip = Join-Path $artifacts ('website-' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmss') + '.zip')
    Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip
    Save-PrivateJson @{ appName=$AppName; resourceGroup=$ResourceGroup; subscriptionId=$SubscriptionId; location=$Location; sqlServerName=$SqlServerName; databaseName=$DatabaseName; zip=$zip; publish=$publish; appServiceTier='Basic B1'; sqlTier='Basic (5 DTU, 2 GB)' } $statePath
    Write-Output 'Prepared React/API deployment package and private local credentials.'
    Write-Output "Package: $zip"
    return
}

$baseArguments = @('--subscription', $SubscriptionId, '--resource-group', $ResourceGroup, '--template-file', (Join-Path $deployRoot 'azure.json'), '--parameters', "@$parametersPath")
if ($Mode -eq 'Validate') {
    Invoke-Azure (@('deployment','group','validate') + $baseArguments + @('--query','properties.provisioningState','--output','tsv'))
    return
}
if (!(Test-Path -LiteralPath $statePath)) { throw 'Run -Mode Prepare first.' }
$state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
if ($state.appName -ne $AppName -or $state.location -ne $Location -or $state.sqlServerName -ne $SqlServerName) { throw 'Prepared package targets different infrastructure. Run Prepare again.' }
if ($Mode -eq 'Migrate') {
    # Additive, reviewed EF migrations only; do not provision resources or reset users.
    $migrationIp = (Invoke-RestMethod -Uri 'https://api.ipify.org' -TimeoutSec 20).Trim()
    if ([Net.IPAddress]::Parse($migrationIp).AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork) { throw 'An IPv4 migration address is required.' }
    $ruleName = 'TemporarySchemaUpdate-' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
    $serverTarget = @('--subscription',$SubscriptionId,'--resource-group',$ResourceGroup,'--server',$SqlServerName)
    $migrationEnvironment = @{
        ASPNETCORE_ENVIRONMENT='Production'; Jwt__Key=$credentials.jwtKey; SeedAdmin__Enabled='false';
        Deployment__ApplicationPassword=''; Storage__BlobConnectionString=''; Website__RedirectOrigin='';
        ConnectionStrings__DefaultConnection="Server=tcp:$SqlServerName.database.windows.net,1433;Initial Catalog=$DatabaseName;User ID=$($credentials.sqlAdmin);Password=$($credentials.sqlPassword);Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"
    }
    $previous = @{}
    try {
        Invoke-Azure (@('sql','server','firewall-rule','create') + $serverTarget + @('--name',$ruleName,'--start-ip-address',$migrationIp,'--end-ip-address',$migrationIp,'--output','none')) | Out-Null
        foreach ($entry in $migrationEnvironment.GetEnumerator()) {
            $previous[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key,'Process')
            [Environment]::SetEnvironmentVariable($entry.Key,$entry.Value,'Process')
        }
        & dotnet (Join-Path $state.publish 'K-Sweet-Kreations-Co-Backend-API.dll') --initialize-database
        if ($LASTEXITCODE -ne 0) { throw 'Database migration failed; the website package has not been deployed.' }
    } finally {
        foreach ($entry in $previous.GetEnumerator()) { [Environment]::SetEnvironmentVariable($entry.Key,$entry.Value,'Process') }
        Invoke-Azure (@('sql','server','firewall-rule','delete') + $serverTarget + @('--name',$ruleName,'--output','none')) | Out-Null
    }
    Write-Output 'Applied pending database migrations. Existing records, credentials and resource tiers preserved.'
    return
}
if ($Mode -eq 'Update') {
    # Code-only update: preserve existing accounts, SQL schema, secrets and resources.
    $target = @('--subscription',$SubscriptionId,'--resource-group',$ResourceGroup,'--name',$AppName)
    $hostname = (Invoke-Azure (@('webapp','show') + $target + @('--query','defaultHostName','--output','tsv'))).Trim()
    $websiteUrl = "https://$hostname"
    Invoke-Azure (@('webapp','deploy') + $target + @('--src-path',$state.zip,'--type','zip','--timeout','600000','--track-status','false','--output','none')) | Out-Null
    # Basic has one worker: stop the outgoing process before starting the new
    # package so overlapping .NET warmups do not exhaust its CPU and memory.
    Invoke-Azure (@('webapp','stop') + $target + @('--output','none')) | Out-Null
    Invoke-Azure (@('webapp','start') + $target + @('--output','none')) | Out-Null
    $expectedScript = [regex]::Match((Get-Content (Join-Path $state.publish 'wwwroot/index.html') -Raw), 'src="([^"]+\.js)"').Groups[1].Value
    if (!$expectedScript) { throw 'The prepared React entry script could not be identified.' }
    $healthy = $false
    for ($attempt = 1; $attempt -le 24; $attempt++) {
        try {
            if ((Invoke-RestMethod -Uri "$websiteUrl/api/health" -TimeoutSec 20).status -eq 'healthy' -and
                (Invoke-WebRequest -Uri "$websiteUrl/" -TimeoutSec 20).Content.Contains($expectedScript)) { $healthy=$true; break }
        } catch { if ($attempt -eq 24) { throw 'Code update uploaded, but the new package did not pass the live checks.' } }
        Start-Sleep -Seconds 5
    }
    if (!$healthy) { throw 'The updated website is not healthy.' }
    $state | Add-Member -NotePropertyName websiteUrl -NotePropertyValue $websiteUrl -Force
    Save-PrivateJson $state $statePath
    Write-Output "Updated website/API; existing database and accounts preserved: $websiteUrl"
    return
}
Write-Output 'Provisioning Basic B1 App Service and Basic Azure SQL Database in South Africa North.'
$outputs = (Invoke-Azure (@('deployment','group','create','--name','ksweetkreations-basic') + $baseArguments + @('--query','properties.outputs','--output','json'))) | ConvertFrom-Json
$websiteUrl = $outputs.websiteUrl.value
$sqlHost = $outputs.sqlHost.value
$appArguments = @('--subscription',$SubscriptionId,'--resource-group',$ResourceGroup,'--name',$AppName)
$serverArguments = @('--subscription',$SubscriptionId,'--resource-group',$ResourceGroup,'--server',$SqlServerName)

# Restrict SQL access to App Service's outbound addresses and a temporary migration IP.
$outbound = (Invoke-Azure (@('webapp','show') + $appArguments + @('--query','possibleOutboundIpAddresses','--output','tsv'))).Trim()
if (!$outbound) { throw 'App Service outbound addresses are unavailable. SQL access has not been opened.' }
$number = 0
foreach ($address in ($outbound -split ',' | Sort-Object -Unique)) {
    $number++
    Invoke-Azure (@('sql','server','firewall-rule','create') + $serverArguments + @('--name',"AppService-$number",'--start-ip-address',$address,'--end-ip-address',$address,'--output','none')) | Out-Null
}
$migrationIp = (Invoke-RestMethod -Uri 'https://api.ipify.org' -TimeoutSec 20).Trim()
if (!([System.Net.IPAddress]::Parse($migrationIp).AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork)) { throw 'An IPv4 migration address is required.' }
Invoke-Azure (@('sql','server','firewall-rule','create') + $serverArguments + @('--name','TemporaryDeployment','--start-ip-address',$migrationIp,'--end-ip-address',$migrationIp,'--output','none')) | Out-Null

$adminConnection = "Server=tcp:$sqlHost,1433;Initial Catalog=$DatabaseName;User ID=$($credentials.sqlAdmin);Password=$($credentials.sqlPassword);Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"
$runtimeConnection = "Server=tcp:$sqlHost,1433;Initial Catalog=$DatabaseName;User ID=ksk_app;Password=$($credentials.applicationPassword);Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;"
$temporaryEnvironment = @{
    ASPNETCORE_ENVIRONMENT='Production'; ConnectionStrings__DefaultConnection=$adminConnection; Jwt__Key=$credentials.jwtKey;
    SeedAdmin__Enabled='true'; SeedAdmin__Email=$AdminEmail; SeedAdmin__Password=$credentials.adminPassword;
    Deployment__ApplicationPassword=$credentials.applicationPassword
}
$previous = @{}
try {
    foreach ($entry in $temporaryEnvironment.GetEnumerator()) {
        $previous[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
    Push-Location $project
    try {
        & dotnet (Join-Path $state.publish 'K-Sweet-Kreations-Co-Backend-API.dll') --initialize-database
        if ($LASTEXITCODE -ne 0) { throw 'Database initialization failed; website package was not deployed.' }
    } finally { Pop-Location }
} finally {
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
    Invoke-Azure (@('sql','server','firewall-rule','delete') + $serverArguments + @('--name','TemporaryDeployment','--output','none')) | Out-Null
}
$settings = @{
    ASPNETCORE_ENVIRONMENT='Production'; ASPNETCORE_FORWARDEDHEADERS_ENABLED='true';
    ConnectionStrings__DefaultConnection=$runtimeConnection; Jwt__Key=$credentials.jwtKey;
    SeedAdmin__Enabled='false'; Storage__PrivateRoot='/home/data/ksweet/private'; Storage__PublicRoot='/home/data/ksweet/products';
    WEBSITES_ENABLE_APP_SERVICE_STORAGE='true'; WEBSITE_RUN_FROM_PACKAGE='1'; SCM_DO_BUILD_DURING_DEPLOYMENT='false'
}
Save-PrivateJson $settings $settingsPath
$storageConnection = (Invoke-Azure @('storage','account','show-connection-string','--subscription',$SubscriptionId,'--resource-group',$ResourceGroup,'--name','ksweetmedia4d7f','--query','connectionString','--output','tsv')).Trim()
$settings['Storage__BlobConnectionString'] = $storageConnection
Save-PrivateJson $settings $settingsPath
Invoke-Azure (@('webapp','config','appsettings','set') + $appArguments + @('--settings',"@$settingsPath",'--output','none')) | Out-Null
Invoke-Azure (@('webapp','deploy') + $appArguments + @('--src-path',$state.zip,'--type','zip','--timeout','600000','--track-status','false','--output','none')) | Out-Null
$healthy = $false
for ($attempt = 1; $attempt -le 12; $attempt++) {
    try {
        $health = Invoke-RestMethod -Uri "$websiteUrl/api/health" -TimeoutSec 30
        if ($health.status -eq 'healthy') { $healthy = $true; break }
    } catch { if ($attempt -eq 12) { throw 'Azure deployment finished, but the API/database health check failed.' } }
    Start-Sleep -Seconds 5
}
if (!$healthy) { throw 'The deployed API is not healthy.' }
$homePage = Invoke-WebRequest -Uri $websiteUrl -TimeoutSec 30
if ($homePage.StatusCode -ne 200 -or $homePage.Content -notmatch 'id="root"') { throw 'The deployed React entry page could not be verified.' }
$state | Add-Member -NotePropertyName websiteUrl -NotePropertyValue $websiteUrl -Force
Save-PrivateJson $state $statePath
Save-PrivateJson @{ websiteUrl=$websiteUrl; email=$AdminEmail; password=$credentials.adminPassword } (Join-Path $deployRoot 'admin-login.local.json')
Write-Output "Deployed website and API: $websiteUrl"
Write-Output "Database: $sqlHost / $DatabaseName"
Write-Output "Admin email: $AdminEmail"
Write-Output "Admin login is saved privately in: $(Join-Path $deployRoot 'admin-login.local.json')"
