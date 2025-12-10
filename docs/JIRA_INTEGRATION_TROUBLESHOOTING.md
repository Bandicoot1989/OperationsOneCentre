# Jira Integration - Troubleshooting Guide

## Problemas Resueltos Durante Implementación (Diciembre 2025)

### 1. API Jira Deprecada - GET /search ya no funciona

**Síntomas:**
- Llamadas a `GET /rest/api/3/search?jql=...` retornan 404 o error
- La documentación antigua de Jira muestra este endpoint

**Causa Raíz:**
Atlassian deprecó el endpoint GET `/rest/api/3/search` en 2024 y lo reemplazó con POST `/rest/api/3/search/jql`.

**Solución:**
```csharp
// ANTES (no funciona)
var response = await _httpClient.GetAsync($"/rest/api/3/search?jql={jql}");

// DESPUÉS (correcto)
var requestBody = new Dictionary<string, object>
{
    ["jql"] = jql,
    ["maxResults"] = 50,
    ["fields"] = new[] { "key", "summary", "description", "status", "resolution", ... }
};
var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
var response = await _httpClient.PostAsync("/rest/api/3/search/jql", jsonContent);
```

**Referencia:**
- [Atlassian Changelog 2024](https://developer.atlassian.com/cloud/jira/platform/changelog/)

---

### 2. Deserialización Silenciosa Falla - Tickets Count = 0

**Síntomas:**
- API Jira retorna datos correctamente (verificado con `/raw` endpoint)
- `JsonSerializer.Deserialize<JiraSearchResponse>()` retorna lista vacía
- No hay excepciones visibles

**Causa Raíz:**
La estructura de respuesta de Jira Cloud cambió:
- Campo `total` ya no existe en la nueva API
- Nuevo campo `isLast` para paginación
- Campo `nextPageToken` en lugar de `startAt`

**Solución:**
Crear clases de deserialización específicas para la nueva API:

```csharp
public class JiraSearchResponsePublic
{
    public bool IsLast { get; set; }
    public string? NextPageToken { get; set; }
    public List<JiraIssueResponsePublic>? Issues { get; set; }
}

public class JiraFieldsResponsePublic
{
    public string? Summary { get; set; }
    public object? Description { get; set; } // Puede ser ADF o string
    public JiraStatusResponsePublic? Status { get; set; }
    public JiraResolutionResponsePublic? Resolution { get; set; }
    // ... más campos
}
```

**Verificación:**
```bash
# Endpoint de prueba que muestra deserialización
GET /api/jiratest/deserialize-test
```

---

### 3. .NET 10 SDK Workload Manifest Error

**Síntomas:**
```
error NETSDK1060: Microsoft.NET.Workload.Emscripten.Current 10.0.100 is lower than 10.0.101 required
```

**Causa Raíz:**
El manifest `microsoft.net.workload.mono.toolchain.current` versión 10.0.101 tenía dependencia en Emscripten 10.0.101, pero solo estaba instalado 10.0.100.

**Solución:**
1. Ejecutar como Administrador PowerShell:
```powershell
Remove-Item -Path "C:\Program Files\dotnet\sdk-manifests\10.0.100\microsoft.net.workload.mono.toolchain.current\10.0.101" -Recurse -Force
```

2. Crear `global.json` para fijar SDK:
```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestMinor"
  }
}
```

---

### 4. Autenticación Azure AD Bloquea Endpoints API

**Síntomas:**
- Endpoints `/api/jiratest/*` redirigen a login de Microsoft
- `Invoke-RestMethod` retorna HTML de página de login

**Causa Raíz:**
Azure App Service tiene Easy Auth configurado para toda la aplicación.

**Solución Temporal:**
Autenticarse manualmente en el navegador antes de probar endpoints.

**Solución Permanente (pendiente):**
Configurar exclusiones en App Service Authentication:
```json
{
  "routes": [
    {
      "match": { "path_prefix": "/api/jiratest" },
      "action": { "allow_unauthenticated": true }
    }
  ]
}
```

---

### 5. Description en Formato ADF (Atlassian Document Format)

**Síntomas:**
- Campo `description` contiene JSON complejo en lugar de texto plano
- Estructura anidada con nodos `type`, `content`, `text`

**Causa Raíz:**
Jira Cloud usa ADF (Atlassian Document Format) para campos de texto enriquecido.

**Solución:**
Implementar extractor de texto recursivo:

```csharp
private string ExtractTextFromAdf(object? adfContent)
{
    if (adfContent == null) return "";
    
    try
    {
        var json = adfContent is JsonElement element 
            ? element.GetRawText() 
            : JsonSerializer.Serialize(adfContent);
        
        var doc = JsonSerializer.Deserialize<AdfDocument>(json);
        if (doc?.Content == null) return json;
        
        var sb = new StringBuilder();
        ExtractTextRecursive(doc.Content, sb);
        return sb.ToString().Trim();
    }
    catch
    {
        return adfContent?.ToString() ?? "";
    }
}

private void ExtractTextRecursive(List<AdfNode>? nodes, StringBuilder sb)
{
    if (nodes == null) return;
    
    foreach (var node in nodes)
    {
        if (node.Type == "text" && !string.IsNullOrEmpty(node.Text))
            sb.Append(node.Text);
        else if (node.Type == "hardBreak" || node.Type == "paragraph")
            sb.AppendLine();
        
        ExtractTextRecursive(node.Content, sb);
    }
}
```

---

## Endpoints de Diagnóstico

| Endpoint | Propósito |
|----------|-----------|
| `/api/jiratest/config` | Verificar configuración de credenciales |
| `/api/jiratest/connection` | Probar conectividad con Jira API |
| `/api/jiratest/tickets?days=7&maxResults=5` | Obtener tickets resueltos |
| `/api/jiratest/raw` | Ver respuesta cruda de Jira (debug) |
| `/api/jiratest/deserialize-test` | Verificar deserialización funciona |
| `/api/jiratest/ticket/{key}` | Obtener ticket específico |

---

## Configuración Requerida

### App Settings (Azure / appsettings.json)

```json
{
  "Jira": {
    "BaseUrl": "https://YOUR-INSTANCE.atlassian.net",
    "Email": "your.email@company.com",
    "ApiToken": "YOUR_API_TOKEN_FROM_ATLASSIAN"
  }
}
```

### Obtener API Token

1. Ir a [https://id.atlassian.com/manage/api-tokens](https://id.atlassian.com/manage/api-tokens)
2. Click "Create API token"
3. Guardar token de forma segura (solo se muestra una vez)

---

## Logs Útiles

```csharp
// Habilitar logging detallado
_logger.LogInformation("Jira request body: {Body}", jsonBody);
_logger.LogInformation("Jira API response length: {Length}", content.Length);
_logger.LogInformation("Deserialized: Issues={Count}, IsLast={IsLast}", 
    searchResult?.Issues?.Count ?? -1, searchResult?.IsLast);
```

---

## Fecha de Documentación
- **Creado:** 10 Diciembre 2025
- **Autor:** Copilot + Osmany Fajardo
- **Versión Jira API:** REST API v3 (POST /search/jql)
- **SDK .NET:** 10.0.100
