# Run under a normal Windows user account. Does not change execution policy or request elevation.
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)
$sdkVersion = '10.0.401'
$sdkUrl = 'https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/dotnet-sdk-10.0.401-win-x64.zip'
$sdkHash = '24b670ad3d923bfcf47df6c3b034152398b42f6dbc388e10d783aee1cfb5e5817d399fc0ae2a12cfa822a55e61d34830ccb15c50ef6efee437ab874bb7c79430'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$buildTools = Join-Path $env:LOCALAPPDATA 'EireTodoBuildTools'
$env:DOTNET_CLI_HOME = Join-Path $buildTools 'cli-home'
$env:NUGET_PACKAGES = Join-Path $buildTools 'packages'
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnetExe = if ($dotnetCommand) { $dotnetCommand.Source } else { $null }
if (!$dotnetExe -or !((& $dotnetExe --list-sdks) -match '^10\.0\.401 ')) {
    $sdkDirectory = Join-Path $buildTools 'dotnet'
    $dotnetExe = Join-Path $sdkDirectory 'dotnet.exe'
    if (!(Test-Path $dotnetExe) -or (& $dotnetExe --version) -ne $sdkVersion) {
        New-Item -ItemType Directory -Path $buildTools -Force | Out-Null
        $sdkArchive = Join-Path $buildTools 'dotnet-sdk.zip'
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing -Uri $sdkUrl -OutFile $sdkArchive
        if ((Get-FileHash $sdkArchive -Algorithm SHA512).Hash.ToLowerInvariant() -ne $sdkHash) {
            throw 'SDK checksum mismatch. Build stopped; the downloaded SDK will not be used.'
        }
        Expand-Archive -LiteralPath $sdkArchive -DestinationPath $sdkDirectory -Force
        Remove-Item -LiteralPath $sdkArchive
    }
    $env:DOTNET_ROOT = $sdkDirectory
}
function Run-Dotnet {
    & $script:dotnetExe @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" }
}
Run-Dotnet restore tests/EireTodo.Checks/EireTodo.Checks.csproj --locked-mode
Run-Dotnet run --project tests/EireTodo.Checks/EireTodo.Checks.csproj -c Release --no-restore
Run-Dotnet restore src/EireTodo.Windows/EireTodo.Windows.csproj --locked-mode
Run-Dotnet publish src/EireTodo.Windows/EireTodo.Windows.csproj -c Release --no-restore -o artifacts/windows-x64
# Zip only the self-contained executable and user documentation.
$staging = Join-Path 'artifacts' ('portable-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging -Force | Out-Null
try {
    Copy-Item artifacts/windows-x64/EireTodo.exe $staging
    Copy-Item QUICKSTART.txt $staging
    Copy-Item WINDOWS-VERIFICATION.md $staging
    Compress-Archive -Path "$staging/*" -DestinationPath artifacts/EireTodo-Windows-x64.zip -Force
} finally { Remove-Item -LiteralPath $staging -Recurse -Force }
Get-FileHash artifacts/EireTodo-Windows-x64.zip -Algorithm SHA256
Write-Host 'Portable app: artifacts/EireTodo-Windows-x64.zip'
