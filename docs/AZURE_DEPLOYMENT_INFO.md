# 🌐 Azure Deployment Information

## Operations One Centre - Información de Despliegue en Azure

**Documento creado:** 10 Diciembre 2025  
**Última actualización:** 11 Diciembre 2025

---

## 📋 Datos del Recurso en Azure

| Propiedad | Valor |
|-----------|-------|
| **Suscripción Azure** | Grupo Antolin ITHQ PoCs |
| **Grupo de Recursos** | `rg-hq-helpdeskai-poc-001` |
| **Nombre de Web App** | `powershell-scripts-helpdesk` |
| **URL de la aplicación** | https://powershell-scripts-helpdesk.germanywestcentral-01.azurewebsites.net |
| **Región** | Germany West Central |
| **Runtime** | .NET 10 |
| **Tipo de App Service** | Blazor Server (InteractiveServer) |

---

## 🚀 Despliegue con Azure CLI

### Prerrequisitos

1. Tener instalado [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli)
2. Tener permisos de despliegue en la suscripción "Grupo Antolin ITHQ PoCs"

### Pasos de Despliegue

#### 1. Iniciar sesión en Azure CLI

```powershell
# Iniciar sesión interactivo
az login

# Verificar suscripción activa
az account show --query "{Subscription:name, TenantId:tenantId}" --output table

# Si es necesario, cambiar a la suscripción correcta
az account set --subscription "Grupo Antolin ITHQ PoCs"
```

#### 2. Publicar la aplicación localmente

```powershell
# Navegar al proyecto
cd c:\Users\osmany.fajardo\repos\.NET_AI_Vector_Search_App\RecipeSearchWeb

# Limpiar y publicar
dotnet clean
dotnet publish -c Release -o ../publish
```

#### 3. Desplegar desde la carpeta publish

```powershell
# Navegar a la carpeta publish
cd c:\Users\osmany.fajardo\repos\.NET_AI_Vector_Search_App\publish

# Comprimir el contenido para despliegue
Compress-Archive -Path .\* -DestinationPath ..\app.zip -Force

# Desplegar usando zip deploy
az webapp deploy `
  --resource-group rg-hq-helpdeskai-poc-001 `
  --name powershell-scripts-helpdesk `
  --src-path ..\app.zip `
  --type zip

# O usar el comando de deployment directo
az webapp deployment source config-zip `
  --resource-group rg-hq-helpdeskai-poc-001 `
  --name powershell-scripts-helpdesk `
  --src ..\app.zip
```

#### 4. Reiniciar la aplicación (opcional)

```powershell
az webapp restart `
  --resource-group rg-hq-helpdeskai-poc-001 `
  --name powershell-scripts-helpdesk
```

---

## ⚙️ Configuración Importante

### WebSockets (Requerido para Blazor Server)

WebSockets **DEBE** estar habilitado para que Blazor Server funcione correctamente:

```powershell
# Verificar estado de WebSockets
az webapp config show `
  --resource-group rg-hq-helpdeskai-poc-001 `
  --name powershell-scripts-helpdesk `
  --query "webSocketsEnabled"

# Habilitar WebSockets si está deshabilitado
az webapp config set `
  --resource-group rg-hq-helpdeskai-poc-001 `
  --name powershell-scripts-helpdesk `
  --web-sockets-enabled true
```

### Variables de Configuración (App Settings)

Las siguientes variables de entorno deben estar configuradas en Azure App Service:

| Variable | Descripción |
|----------|-------------|
| `AZURE_OPENAI_ENDPOINT` | Endpoint de Azure OpenAI |
| `AZURE_OPENAI_API_KEY` | API Key de Azure OpenAI |
| `AZURE_OPENAI_GPT_NAME` | Nombre del deployment GPT (gpt-4o-mini) |
| `AZURE_OPENAI_EMBEDDING_NAME` | Nombre del deployment de embeddings |
| `AzureStorage__ConnectionString` | Connection string de Azure Blob Storage |
| `Confluence__BaseUrl` | URL de Confluence (Atlassian) |
| `Confluence__Username` | Usuario de Confluence |
| `Confluence__ApiToken` | API Token de Confluence |
| `Jira__BaseUrl` | URL de Jira (antolin.atlassian.net) |
| `Jira__Email` | Email para autenticación Jira |
| `Jira__ApiToken` | API Token de Jira |
| `Jira__ProjectKeys` | Proyectos de Jira para monitoreo (MT, MTT) |

```powershell
# Ver configuración actual
az webapp config appsettings list `
  --resource-group rg-hq-helpdeskai-poc-001 `
  --name powershell-scripts-helpdesk `
  --output table
```

---

## 📊 Monitorización y Logs

### Ver logs en tiempo real

```powershell
az webapp log tail `
  --resource-group rg-hq-helpdeskai-poc-001 `
  --name powershell-scripts-helpdesk
```

### Habilitar logging detallado

```powershell
az webapp log config `
  --resource-group rg-hq-helpdeskai-poc-001 `
  --name powershell-scripts-helpdesk `
  --application-logging filesystem `
  --detailed-error-messages true `
  --failed-request-tracing true `
  --web-server-logging filesystem
```

### Descargar logs

```powershell
az webapp log download `
  --resource-group rg-hq-helpdeskai-poc-001 `
  --name powershell-scripts-helpdesk `
  --log-file webapp_logs.zip
```

---

## 🔄 Script Completo de Despliegue

```powershell
# =========================================
# Script de Despliegue Completo
# Operations One Centre → Azure App Service
# =========================================

# 1. Variables
$resourceGroup = "rg-hq-helpdeskai-poc-001"
$appName = "powershell-scripts-helpdesk"
$projectPath = "c:\Users\osmany.fajardo\repos\.NET_AI_Vector_Search_App"

# 2. Login en Azure
Write-Host "🔐 Iniciando sesión en Azure..." -ForegroundColor Cyan
az login

# 3. Verificar suscripción
Write-Host "📋 Verificando suscripción..." -ForegroundColor Cyan
az account show --query "{Subscription:name}" --output table

# 4. Compilar y publicar
Write-Host "🔨 Compilando aplicación..." -ForegroundColor Cyan
Set-Location "$projectPath\RecipeSearchWeb"
dotnet clean
dotnet publish -c Release -o "$projectPath\publish"

# 5. Crear ZIP
Write-Host "📦 Creando paquete de despliegue..." -ForegroundColor Cyan
Set-Location "$projectPath\publish"
if (Test-Path "$projectPath\app.zip") { Remove-Item "$projectPath\app.zip" -Force }
Compress-Archive -Path .\* -DestinationPath "$projectPath\app.zip" -Force

# 6. Desplegar
Write-Host "🚀 Desplegando a Azure..." -ForegroundColor Cyan
az webapp deployment source config-zip `
  --resource-group $resourceGroup `
  --name $appName `
  --src "$projectPath\app.zip"

# 7. Verificar WebSockets
Write-Host "⚙️ Verificando WebSockets..." -ForegroundColor Cyan
$webSockets = az webapp config show --resource-group $resourceGroup --name $appName --query "webSocketsEnabled" -o tsv
if ($webSockets -ne "True") {
    Write-Host "  → Habilitando WebSockets..." -ForegroundColor Yellow
    az webapp config set --resource-group $resourceGroup --name $appName --web-sockets-enabled true
}

# 8. Reiniciar
Write-Host "🔄 Reiniciando aplicación..." -ForegroundColor Cyan
az webapp restart --resource-group $resourceGroup --name $appName

Write-Host "✅ Despliegue completado!" -ForegroundColor Green
Write-Host "🌐 URL: https://$appName.germanywestcentral-01.azurewebsites.net" -ForegroundColor Cyan
```

---

## 🆘 Troubleshooting

### El chatbot no responde a clicks
→ WebSockets está deshabilitado. Ver sección [WebSockets](#websockets-requerido-para-blazor-server)

### Error 500 al cargar la página
→ Revisar logs con `az webapp log tail`. Posibles causas:
- Variables de entorno faltantes
- Error en la compilación

### La aplicación tarda en cargar
→ El primer request después de inactividad despierta el App Service (cold start). Esto es normal en planes gratuitos/básicos.

### SignalR connection failed
→ Verificar que WebSockets esté habilitado y que no haya un proxy/firewall bloqueando conexiones WebSocket.

---

## 📚 Documentación Relacionada

- [PROJECT_DOCUMENTATION.md](./PROJECT_DOCUMENTATION.md) - Documentación general del proyecto
- [TECHNICAL_REFERENCE.md](./TECHNICAL_REFERENCE.md) - Referencia técnica completa
- [TROUBLESHOOTING.md](./TROUBLESHOOTING.md) - Guía de resolución de problemas
- [AI_CONTEXT.md](./AI_CONTEXT.md) - Contexto para asistentes IA

---

## 📞 Contacto

Para problemas de despliegue o acceso a Azure, contactar al equipo de IT Operations.
