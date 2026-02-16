# =========================================
# Script de Despliegue Rápido
# Operations One Centre → Azure App Service
# =========================================
# 
# Este script incluye la solución para proxy corporativo (Zscaler)
# Ejecutar: .\deploy.ps1
#

param(
    [switch]$SkipBuild = $false,
    [switch]$SkipRestart = $false
)

# Configuración
$resourceGroup = "rg-hq-helpdeskai-poc-001"
$appName = "ops-one-centre-ai"
$projectPath = $PSScriptRoot
$appUrl = "https://ops-one-centre-ai.azurewebsites.net"

Write-Host "`n╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     🚀 DESPLIEGUE - OPERATIONS ONE CENTRE                 ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝`n" -ForegroundColor Cyan

# 1. Verificar autenticación Azure
Write-Host "🔐 Verificando autenticación Azure..." -ForegroundColor Cyan
try {
    $account = az account show 2>$null | ConvertFrom-Json
    Write-Host "   ✅ Autenticado como: $($account.user.name)" -ForegroundColor Green
} catch {
    Write-Host "   ⚠️  No autenticado. Iniciando sesión..." -ForegroundColor Yellow
    az login
}

# 2. Compilar y publicar (si no se omite)
if (-not $SkipBuild) {
    Write-Host "`n🔨 Compilando aplicación..." -ForegroundColor Cyan
    Set-Location "$projectPath\OperationsOneCentre"
    
    Write-Host "   → Limpiando..." -ForegroundColor Gray
    dotnet clean --verbosity quiet
    
    Write-Host "   → Publicando (Release)..." -ForegroundColor Gray
    $publishOutput = dotnet publish -c Release -o "$projectPath\publish" --verbosity quiet 2>&1
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "   ❌ Error en compilación" -ForegroundColor Red
        Write-Host $publishOutput
        exit 1
    }
    Write-Host "   ✅ Compilación exitosa" -ForegroundColor Green
} else {
    Write-Host "`n⏭️  Omitiendo compilación (usando publish existente)" -ForegroundColor Yellow
}

# 3. Crear paquete ZIP
Write-Host "`n📦 Creando paquete de despliegue..." -ForegroundColor Cyan
Set-Location "$projectPath\publish"

if (Test-Path "$projectPath\app.zip") { 
    Remove-Item "$projectPath\app.zip" -Force 
}

Compress-Archive -Path .\* -DestinationPath "$projectPath\app.zip" -Force
$zipSize = [math]::Round((Get-Item "$projectPath\app.zip").Length / 1MB, 2)
Write-Host "   ✅ app.zip creado ($zipSize MB)" -ForegroundColor Green

# 4. IMPORTANTE: Configurar para proxy corporativo (Zscaler)
Write-Host "`n🔐 Configurando para proxy corporativo Zscaler..." -ForegroundColor Yellow
$env:AZURE_CLI_DISABLE_CONNECTION_VERIFICATION = "1"
Write-Host "   ✅ Variable AZURE_CLI_DISABLE_CONNECTION_VERIFICATION configurada" -ForegroundColor Green

# 5. Desplegar a Azure
Write-Host "`n🚀 Desplegando a Azure App Service..." -ForegroundColor Cyan
Write-Host "   → Resource Group: $resourceGroup" -ForegroundColor Gray
Write-Host "   → App Name: $appName" -ForegroundColor Gray

Set-Location $projectPath

$deployStart = Get-Date
az webapp deployment source config-zip `
  --resource-group $resourceGroup `
  --name $appName `
  --src "$projectPath\app.zip" `
  --output json > $null 2>&1

if ($LASTEXITCODE -eq 0) {
    $deployTime = [math]::Round(((Get-Date) - $deployStart).TotalSeconds, 1)
    Write-Host "   ✅ Despliegue exitoso ($deployTime segundos)" -ForegroundColor Green
} else {
    Write-Host "   ❌ Error en despliegue" -ForegroundColor Red
    $env:AZURE_CLI_DISABLE_CONNECTION_VERIFICATION = $null
    exit 1
}

# 6. Verificar WebSockets
Write-Host "`n⚙️  Verificando configuración..." -ForegroundColor Cyan
$webSockets = az webapp config show `
  --resource-group $resourceGroup `
  --name $appName `
  --query "webSocketsEnabled" `
  -o tsv 2>$null

if ($webSockets -ne "true") {
    Write-Host "   → Habilitando WebSockets..." -ForegroundColor Yellow
    az webapp config set `
      --resource-group $resourceGroup `
      --name $appName `
      --web-sockets-enabled true `
      --output none 2>$null
    Write-Host "   ✅ WebSockets habilitado" -ForegroundColor Green
} else {
    Write-Host "   ✅ WebSockets ya habilitado" -ForegroundColor Green
}

# 7. Reiniciar aplicación (si no se omite)
if (-not $SkipRestart) {
    Write-Host "`n🔄 Reiniciando aplicación..." -ForegroundColor Cyan
    az webapp restart `
      --resource-group $resourceGroup `
      --name $appName `
      --output none 2>$null
    
    Write-Host "   ✅ Aplicación reiniciada" -ForegroundColor Green
    Write-Host "   ⏳ Esperando 5 segundos para que inicie..." -ForegroundColor Gray
    Start-Sleep -Seconds 5
} else {
    Write-Host "`n⏭️  Omitiendo reinicio" -ForegroundColor Yellow
}

# 8. Limpiar variable de entorno
$env:AZURE_CLI_DISABLE_CONNECTION_VERIFICATION = $null

# 9. Resumen final
Write-Host "`n╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║          ✅ DESPLIEGUE COMPLETADO EXITOSAMENTE            ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Green

Write-Host "`n📋 Información del despliegue:" -ForegroundColor Cyan
Write-Host "   🌐 URL: $appUrl" -ForegroundColor White
Write-Host "   📍 Región: Germany West Central" -ForegroundColor Gray
Write-Host "   ⚙️  WebSockets: Habilitado" -ForegroundColor Gray
Write-Host "   🎨 UI: Diseño Gemini (centrado)" -ForegroundColor Gray

Write-Host "`n💡 Comandos útiles:" -ForegroundColor Cyan
Write-Host "   Ver logs:    az webapp log tail -g $resourceGroup -n $appName" -ForegroundColor Gray
Write-Host "   Abrir app:   Start-Process '$appUrl'" -ForegroundColor Gray
Write-Host "   Portal:      Start-Process 'https://portal.azure.com'" -ForegroundColor Gray

Write-Host ""
