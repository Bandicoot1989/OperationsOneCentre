# Troubleshooting Guide - Operations One Centre

Este documento recopila problemas encontrados y sus soluciones durante el desarrollo y despliegue de la aplicación.

---

## 📋 Índice

1. [Chatbot No Responde a Clicks](#1-chatbot-no-responde-a-clicks)
2. [Error 500 al Usar @rendermode en MainLayout](#2-error-500-al-usar-rendermode-en-mainlayout)
3. [Error de Deserialización de Fechas Jira](#3-error-de-deserialización-de-fechas-jira)
4. [IJiraClient No Registrado en DI](#4-ijiraclient-no-registrado-en-di)
5. [Error BLAZOR106 - Archivo JS Huérfano](#5-error-blazor106---archivo-js-huérfano)
6. [Conflicto de SDK Workloads .NET 9 vs .NET 10](#6-conflicto-de-sdk-workloads-net-9-vs-net-10)
7. [Error 500.30 - ASP.NET Core Failed to Start (Servicio No Registrado en DI)](#7-error-50030---aspnet-core-failed-to-start-servicio-no-registrado-en-di)

---

## 1. Chatbot No Responde a Clicks

### Síntomas
- El icono del chatbot es visible en la esquina inferior derecha
- Al hacer click no pasa nada
- No hay errores visibles en la consola del navegador

### Causa Raíz
**WebSockets estaba DESHABILITADO** en Azure App Service. Blazor Server requiere SignalR que usa WebSockets para la comunicación en tiempo real.

### Solución
```bash
# Habilitar WebSockets en Azure App Service
az webapp config set \
  --resource-group rg-hq-helpdeskai-poc-001 \
  --name ops-one-centre-ai \
  --web-sockets-enabled true

# Reiniciar la aplicación
az webapp restart \
  --resource-group rg-hq-helpdeskai-poc-001 \
  --name ops-one-centre-ai
```

### Verificación
```bash
# Comprobar que WebSockets está habilitado
az webapp config show \
  --resource-group rg-hq-helpdeskai-poc-001 \
  --name ops-one-centre-ai \
  --query "webSocketsEnabled"
```

---

## 2. Error 500 al Usar @rendermode en MainLayout

### Síntomas
- Página muestra "Something went wrong"
- HTTP 500 Internal Server Error
- Request ID visible en la página de error

### Causa Raíz
En Blazor .NET 8+, **NO se puede usar `@rendermode` directamente en un `LayoutComponentBase`**. Los layouts son componentes especiales que no soportan esta directiva directamente.

### Código Incorrecto ❌
```razor
@inherits LayoutComponentBase
@using static Microsoft.AspNetCore.Components.Web.RenderMode
@rendermode InteractiveServer  <!-- ESTO CAUSA 500 -->

<div class="md-app">
```

### Código Correcto ✅
```razor
@inherits LayoutComponentBase

<div class="md-app">
    <!-- Layout content -->
</div>

@* Componentes hijos SÍ pueden usar @rendermode *@
<KnowledgeChat @rendermode="InteractiveServer" />
```

### Solución
- Usar `@rendermode` solo en **componentes hijos**, no en el layout
- Los componentes interactivos como `KnowledgeChat` especifican su rendermode donde se instancian

---

## 3. Error de Deserialización de Fechas Jira

### Síntomas
```
System.Text.Json.JsonException: The JSON value could not be converted to System.DateTime.
```

### Causa Raíz
Jira devuelve fechas en formato ISO 8601 con timezone: `2024-01-15T10:30:00.000+0100`
`System.Text.Json` no parsea este formato automáticamente a `DateTime`.

### Solución
Cambiar los campos de `DateTime` a `string` y parsear manualmente:

```csharp
// En JiraClient.cs - Modelo de respuesta
public class JiraFieldsResponsePublic
{
    // ANTES (causaba error):
    // public DateTime Created { get; set; }
    
    // DESPUÉS (funciona):
    public string? Created { get; set; }
    public string? Resolutiondate { get; set; }
}

// Helper methods para parsear
private static DateTime ParseJiraDate(string? dateString)
{
    if (string.IsNullOrEmpty(dateString))
        return DateTime.MinValue;
    
    if (DateTime.TryParse(dateString, out var result))
        return result;
    
    return DateTime.MinValue;
}
```

---

## 4. IJiraClient No Registrado en DI

### Síntomas
```
System.InvalidOperationException: Unable to resolve service for type 
'OperationsOneCentre.Interfaces.IJiraClient' while attempting to activate 
'OperationsOneCentre.Services.JiraSolutionStorageService'.
```

### Causa Raíz
La interfaz `IJiraClient` no estaba registrada en el contenedor de inyección de dependencias.

### Solución
Consolidar todos los servicios de Jira en un método de extensión:

```csharp
// En DependencyInjection.cs
public static IServiceCollection AddJiraSolutionServices(
    this IServiceCollection services, 
    IConfiguration configuration)
{
    // Registrar el cliente HTTP de Jira
    services.AddHttpClient<JiraClient>();
    
    // Registrar la interfaz CON implementación
    services.AddScoped<IJiraClient, JiraClient>();
    
    // Registrar otros servicios
    services.AddScoped<IJiraSolutionStorageService, JiraSolutionStorageService>();
    services.AddScoped<IJiraSolutionSearchService, JiraSolutionSearchService>();
    services.AddHostedService<JiraSolutionHarvesterService>();
    
    return services;
}
```

---

## 5. Error BLAZOR106 - Archivo JS Huérfano

### Síntomas
```
error BLAZOR106: The JS module file '...\publish\wwwroot\Components\Layout\ReconnectModal.razor.js' 
was defined but no associated razor component or view was found for it.
```

### Causa Raíz
Archivo JavaScript de un componente Razor quedó en la carpeta `publish/` después de que el componente fue movido o renombrado.

### Solución
Limpiar la carpeta publish y reconstruir:

```powershell
# Eliminar carpeta publish
Remove-Item -Path publish -Recurse -Force

# Limpiar y reconstruir
dotnet clean
dotnet build
```

---

## 🔧 Comandos Útiles de Diagnóstico

### Ver logs de Azure en tiempo real
```bash
az webapp log tail \
  --resource-group rg-hq-helpdeskai-poc-001 \
  --name ops-one-centre-ai
```

### Descargar logs de Azure
```bash
az webapp log download \
  --resource-group rg-hq-helpdeskai-poc-001 \
  --name ops-one-centre-ai \
  --log-file webapp-logs.zip
```

### Verificar que la app responde
```powershell
(Invoke-WebRequest -Uri "https://ops-one-centre-ai-f0h8h6ekcsb5amhn.germanywestcentral-01.azurewebsites.net/" -UseBasicParsing).StatusCode
```

### Verificar endpoint SignalR/Blazor
```powershell
$response = Invoke-WebRequest -Uri "https://ops-one-centre-ai-f0h8h6ekcsb5amhn.germanywestcentral-01.azurewebsites.net/_blazor/negotiate?negotiateVersion=1" -Method POST -UseBasicParsing
$response.StatusCode  # Debe ser 200
```

---

## 📅 Historial de Actualizaciones

| Fecha | Problema | Solución |
|-------|----------|----------|
| 2025-12-10 | Chatbot no responde | Habilitar WebSockets en Azure |
| 2025-12-10 | Error 500 con @rendermode en Layout | Quitar @rendermode del MainLayout |
| 2025-12-10 | Error deserialización fechas Jira | Cambiar DateTime a string |
| 2025-12-10 | IJiraClient no registrado | Consolidar DI en AddJiraSolutionServices |
| 2025-12-11 | Conflicto SDK Workloads .NET 9/10 | Renombrar manifiesto conflictivo |
| 2026-01-27 | Error 500.30 - ITicketLookupService no registrado | Limpieza completa de publish y rebuild |

---

## 7. Error 500.30 - ASP.NET Core Failed to Start (Servicio No Registrado en DI)

### Síntomas
- HTTP Error 500.30 - ASP.NET Core app failed to start
- Página de error de Azure con mensaje genérico
- En logs: `System.InvalidOperationException: Unable to resolve service for type 'OperationsOneCentre.Interfaces.ITicketLookupService'`

### Causa Raíz
**Despliegue incremental corrupto**: La carpeta `publish` no se regeneró completamente con los últimos cambios del código. Al hacer múltiples despliegues consecutivos, algunos archivos DLL pueden quedar con versiones mezcladas (algunos con código viejo, otros con código nuevo), causando que:

1. Un servicio antiguo intente inyectar una dependencia nueva que no existe en su versión
2. O viceversa: código nuevo referencia servicios que no están registrados en el `Program.cs` viejo

**En este caso específico:**
- `KnowledgeAgentService` actualizado esperaba `ITicketLookupService` en el constructor
- Pero el `Program.cs` desplegado NO tenía `builder.Services.AddTicketLookupServices()`
- Resultado: DI no puede resolver la dependencia → 500.30

### Solución: Despliegue Limpio Completo

```powershell
# 1. Eliminar completamente la carpeta publish
cd c:\Users\osmany.fajardo\repos\OperationsOneCentre
Remove-Item -Path publish -Recurse -Force -ErrorAction SilentlyContinue

# 2. Limpiar artefactos de compilación
cd OperationsOneCentre
dotnet clean

# 3. Publicar desde cero
dotnet publish -c Release -o ../publish

# 4. Comprimir
cd ../publish
Compress-Archive -Path .\* -DestinationPath ..\app.zip -Force

# 5. Desplegar
$env:AZURE_CLI_DISABLE_CONNECTION_VERIFICATION = "1"
az webapp deployment source config-zip `
  --resource-group rg-hq-helpdeskai-poc-001 `
  --name ops-one-centre-ai `
  --src ..\app.zip
```

### Cómo Diagnosticar Este Error

#### 1. Revisar logs de Azure
```powershell
# Descargar logs
az webapp log download `
  --resource-group rg-hq-helpdeskai-poc-001 `
  --name ops-one-centre-ai `
  --log-file c:\temp\webapp-logs.zip

# Extraer y buscar el error específico
Expand-Archive c:\temp\webapp-logs.zip -DestinationPath c:\temp\webapp-logs
Select-String -Path "c:\temp\webapp-logs\LogFiles\eventlog.xml" `
  -Pattern "Unable to resolve service|InvalidOperationException" `
  -Context 2,5
```

#### 2. Verificar registro de servicios en código local
```bash
# Buscar si el servicio faltante está registrado
grep -r "AddTicketLookupServices" OperationsOneCentre/

# Verificar que el método se llama en Program.cs
grep "AddTicketLookupServices" OperationsOneCentre/Program.cs
```

### Prevención

**SIEMPRE hacer despliegue limpio cuando:**
- Agregas nuevos servicios al DI (`Program.cs` o `DependencyInjection.cs`)
- Cambias constructores de servicios (nuevos parámetros)
- Actualizas interfaces registradas
- Después de varios despliegues incrementales consecutivos

**Script recomendado para despliegue seguro:**
```powershell
# deploy-clean.ps1
param(
    [string]$ResourceGroup = "rg-hq-helpdeskai-poc-001",
    [string]$AppName = "ops-one-centre-ai"
)

Write-Host "🧹 Cleaning previous build..." -ForegroundColor Yellow
Remove-Item -Path publish -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "🔨 Building from scratch..." -ForegroundColor Yellow
cd OperationsOneCentre
dotnet clean
dotnet publish -c Release -o ../publish

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "📦 Compressing..." -ForegroundColor Yellow
cd ../publish
Compress-Archive -Path .\* -DestinationPath ..\app.zip -Force

Write-Host "🚀 Deploying to Azure..." -ForegroundColor Yellow
$env:AZURE_CLI_DISABLE_CONNECTION_VERIFICATION = "1"
az webapp deployment source config-zip `
  --resource-group $ResourceGroup `
  --name $AppName `
  --src ..\app.zip

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Deployment successful!" -ForegroundColor Green
} else {
    Write-Host "❌ Deployment failed!" -ForegroundColor Red
}
```

---

## 6. Conflicto de SDK Workloads .NET 9 vs .NET 10

### Síntomas
```
error MSB4242: Paquete de carga de trabajo 'Microsoft.NET.Runtime.MonoAOTCompiler.Task.net9' 
en el manifiesto 'microsoft.net.workload.mono.toolchain.net9' entra en conflicto con el 
manifiesto 'microsoft.net.workload.mono.toolchain.current'
```

### Causa Raíz
Cuando tienes instalados SDKs de .NET 9 y .NET 10 simultáneamente, los manifiestos de workloads pueden entrar en conflicto. Los manifiestos están en:
- `C:\Program Files\dotnet\sdk-manifests\10.0.100\microsoft.net.workload.mono.toolchain.net9`
- `C:\Program Files\dotnet\sdk-manifests\9.0.100\microsoft.net.workload.mono.toolchain.current`

### Solución
Renombrar o eliminar el manifiesto conflictivo de .NET 9 (requiere permisos de administrador):

```powershell
# Ejecutar como Administrador
Rename-Item "C:\Program Files\dotnet\sdk-manifests\9.0.100\microsoft.net.workload.mono.toolchain.current" `
  "microsoft.net.workload.mono.toolchain.current.bak"
```

O mediante PowerShell elevado:
```powershell
Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -Command `"Rename-Item 'C:\Program Files\dotnet\sdk-manifests\9.0.100\microsoft.net.workload.mono.toolchain.current' 'microsoft.net.workload.mono.toolchain.current.bak' -Force`""
```

### Verificación
```powershell
# Verificar que el manifiesto fue renombrado
Test-Path "C:\Program Files\dotnet\sdk-manifests\9.0.100\microsoft.net.workload.mono.toolchain.current"
# Debe retornar: False

# Intentar compilar
dotnet build -c Release
```