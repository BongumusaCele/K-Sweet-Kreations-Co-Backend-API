[CmdletBinding()]
param([ValidateSet('Configure','Cutover')][string]$Mode = 'Configure')
$ErrorActionPreference = 'Stop'
$group = 'RSG-RCGPON-ST10127074-SANORTH'
$subscription = 'e81cac15-7128-4116-a416-bc57e51fdbb8'
$oldApp = 'React-Bakery'
$newApp = 'k-sweet-creations-co'
$storage = 'ksweetmedia4d7f'
$url = 'https://k-sweet-creations-co.azurewebsites.net'
function Azure([string[]]$Arguments) {
    $result = & az @Arguments --subscription $subscription --only-show-errors
    if ($LASTEXITCODE -ne 0) { throw "Azure operation failed: $($Arguments[0..2] -join ' ')" }
    return $result
}
function PrivateJson($value, $path) {
    if (!(Test-Path -LiteralPath $path)) {
        New-Item -ItemType File -Path $path | Out-Null
        $acl = [Security.AccessControl.FileSecurity]::new()
        $acl.SetAccessRuleProtection($true,$false)
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.WindowsIdentity]::GetCurrent().Name,'FullControl','Allow'))
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new('NT AUTHORITY\SYSTEM','FullControl','Allow'))
        Set-Acl -LiteralPath $path -AclObject $acl
    }
    $value | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $path -Encoding utf8
}
$settingsPath = Join-Path $PSScriptRoot 'move-settings.local.json'
if ($Mode -eq 'Configure') {
    $settings = @{}
    $existing = Azure @('webapp','config','appsettings','list','--resource-group',$group,'--name',$oldApp,'--output','json') | ConvertFrom-Json
    foreach ($setting in $existing) { $settings[$setting.name] = $setting.value }
    $connection = (Azure @('storage','account','show-connection-string','--resource-group',$group,'--name',$storage,'--query','connectionString','--output','tsv')).Trim()
    $settings['Storage__BlobConnectionString'] = $connection
    $settings.Remove('Website__RedirectOrigin')
    PrivateJson $settings $settingsPath
    foreach ($app in @($oldApp, $newApp)) {
        Azure @('webapp','config','appsettings','set','--resource-group',$group,'--name',$app,'--settings',"@$settingsPath",'--output','none') | Out-Null
    }
    Azure @('webapp','config','set','--resource-group',$group,'--name',$newApp,'--startup-file','dotnet K-Sweet-Kreations-Co-Backend-API.dll','--always-on','true','--min-tls-version','1.2','--ftps-state','Disabled','--output','none') | Out-Null
    # Both sites share the same existing database; only add missing outbound IP rules.
    $outbound = (Azure @('webapp','show','--resource-group',$group,'--name',$newApp,'--query','possibleOutboundIpAddresses','--output','tsv')).Trim() -split ','
    $existingRules = Azure @('sql','server','firewall-rule','list','--resource-group',$group,'--server','ksweetkreations-sql-4d7f','--output','json') | ConvertFrom-Json
    foreach ($address in ($outbound | Sort-Object -Unique)) {
        if ($address -and $address -notin $existingRules.startIpAddress) {
            Azure @('sql','server','firewall-rule','create','--resource-group',$group,'--server','ksweetkreations-sql-4d7f','--name',('Website-' + $address.Replace('.','-')),'--start-ip-address',$address,'--end-ip-address',$address,'--output','none') | Out-Null
        }
    }
    $credentialsPath = Join-Path $PSScriptRoot 'credentials.local.json'
    $credentials = Get-Content -LiteralPath $credentialsPath -Raw | ConvertFrom-Json
    $credentials | Add-Member -NotePropertyName storageConnectionString -NotePropertyValue $connection -Force
    PrivateJson $credentials $credentialsPath
    Write-Output 'Configured both apps with the existing database and private Blob Storage. Deploy to the old app first to migrate existing files, then deploy to the new app.'
} else {
    if ((Invoke-RestMethod "$url/api/health" -TimeoutSec 30).status -ne 'healthy') { throw 'The new site is not healthy. No cutover performed.' }
    # The former site only redirects; it no longer needs to access SQL or Blob Storage.
    PrivateJson @{ Website__RedirectOrigin=$url; Storage__BlobConnectionString='' } $settingsPath
    Azure @('webapp','config','appsettings','set','--resource-group',$group,'--name',$oldApp,'--settings',"@$settingsPath",'--output','none') | Out-Null
    $redirectPackage = Join-Path $PSScriptRoot ('artifacts/redirect-' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmss') + '.zip')
    Compress-Archive -LiteralPath (Join-Path $PSScriptRoot 'redirect/server.cjs') -DestinationPath $redirectPackage
    $configPath = Join-Path $PSScriptRoot 'redirect-config.local.json'
    PrivateJson @{ properties=@{ linuxFxVersion='NODE|24-lts'; appCommandLine='node server.cjs'; alwaysOn=$true; minTlsVersion='1.2'; ftpsState='Disabled' } } $configPath
    Azure @('rest','--method','patch','--url',"https://management.azure.com/subscriptions/$subscription/resourceGroups/$group/providers/Microsoft.Web/sites/$oldApp/config/web?api-version=2023-12-01",'--body',"@$configPath",'--output','none') | Out-Null
    Azure @('webapp','deploy','--resource-group',$group,'--name',$oldApp,'--src-path',$redirectPackage,'--type','zip','--track-status','false','--output','none') | Out-Null
    Azure @('webapp','start','--resource-group',$group,'--name',$oldApp,'--output','none') | Out-Null
    $credentials = Get-Content (Join-Path $PSScriptRoot 'credentials.local.json') -Raw | ConvertFrom-Json
    PrivateJson @{ websiteUrl=$url; email=$credentials.adminEmail; password=$credentials.adminPassword } (Join-Path $PSScriptRoot 'admin-login.local.json')
    Write-Output "Canonical website: $url. The old site redirects visitors; database and original files are retained."
}
