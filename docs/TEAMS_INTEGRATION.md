# Integración con Microsoft Teams

## Opción 1: Azure Bot Service (Recomendada)

Esta es la integración más completa que permite conversaciones naturales en Teams.

### Paso 1: Crear Azure Bot Service

```powershell
# Variables
$resourceGroup = "rg-hq-helpdeskai-poc-001"
$botName = "operations-one-centre-bot"
$location = "germanywestcentral"

# Crear Azure Bot
az bot create \
  --resource-group $resourceGroup \
  --name $botName \
  --kind webapp \
  --sku F0 \
  --location $location \
  --endpoint "https://powershell-scripts-helpdesk-f0h8h6ekcsb5amhn.germanywestcentral-01.azurewebsites.net/api/messages"
```

### Paso 2: Registrar App en Azure AD

1. Ve a [Azure Portal](https://portal.azure.com) → Azure Active Directory → App registrations
2. Click "New registration"
3. Nombre: `Operations One Centre Bot`
4. Supported account types: "Accounts in this organizational directory only"
5. Redirect URI: Web → `https://token.botframework.com/.auth/web/redirect`
6. Guarda el **Application (client) ID** y crea un **Client Secret**

### Paso 3: Configurar App Settings

Añade estas variables de entorno en tu App Service:

```
MicrosoftAppId=<tu-app-id>
MicrosoftAppPassword=<tu-client-secret>
```

### Paso 4: Habilitar Canal de Teams

1. Ve a Azure Portal → Tu Bot Service
2. Channels → Add Teams channel
3. Configura las opciones y guarda

### Paso 5: Instalar en Teams

1. En Azure Portal → Bot Service → Channels → Teams → "Open in Teams"
2. O crear un App Package para distribuir en tu organización

---

## Opción 2: Outgoing Webhook (Más Simple)

Si solo necesitas que el bot responda cuando lo mencionan en un canal:

### Crear Webhook en Teams

1. Ve al canal de Teams donde quieres el bot
2. Click en "..." → "Connectors"
3. Busca "Outgoing Webhook"
4. Configura:
   - Name: `Operations Bot`
   - Callback URL: `https://powershell-scripts-helpdesk-f0h8h6ekcsb5amhn.germanywestcentral-01.azurewebsites.net/api/teams-webhook`

---

## Archivos Necesarios para Azure Bot Service

Los siguientes archivos ya han sido añadidos al proyecto:

- `Controllers/BotController.cs` - Endpoint para recibir mensajes de Teams
- `Bot/OperationsBot.cs` - Lógica del bot que usa KnowledgeAgentService
- `Bot/AdapterWithErrorHandler.cs` - Manejo de errores

## Paquetes NuGet Requeridos

```xml
<PackageReference Include="Microsoft.Bot.Builder" Version="4.22.0" />
<PackageReference Include="Microsoft.Bot.Builder.Integration.AspNet.Core" Version="4.22.0" />
```
