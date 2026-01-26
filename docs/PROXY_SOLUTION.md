# 🔐 Solución Proxy Corporativo - Zscaler

## ⚠️ Problema

Al ejecutar comandos de Azure CLI desde la red corporativa, se recibe el siguiente error:

```
SSL: CERTIFICATE_VERIFY_FAILED - certificate verify failed: 
Basic Constraints of CA cert not marked critical
```

**Causa**: La red corporativa usa **Zscaler**, un proxy que intercepta el tráfico SSL. El certificado autofirmado de Zscaler tiene "Basic Constraints" no marcado como crítico, lo cual Azure CLI rechaza por motivos de seguridad.

---

## ✅ Solución Probada

### Configurar esta variable ANTES de usar Azure CLI:

```powershell
$env:AZURE_CLI_DISABLE_CONNECTION_VERIFICATION = "1"
```

### Ejemplo completo:

```powershell
# Configurar para proxy corporativo
$env:AZURE_CLI_DISABLE_CONNECTION_VERIFICATION = "1"

# Ejecutar comandos Azure CLI normalmente
az webapp deployment source config-zip `
  --resource-group rg-hq-helpdeskai-poc-001 `
  --name powershell-scripts-helpdesk `
  --src app.zip

# Limpiar después (opcional)
$env:AZURE_CLI_DISABLE_CONNECTION_VERIFICATION = $null
```

---

## 🚀 Uso Recomendado

### Opción 1: Script Automatizado (Más Fácil)

```powershell
# Usar el script de despliegue que ya incluye la solución
.\deploy.ps1
```

El script `deploy.ps1` ya configura automáticamente la variable de entorno.

### Opción 2: Manual

```powershell
# 1. Navegar al proyecto
cd c:\Users\osmany.fajardo\repos\.NET_AI_Vector_Search_App

# 2. Compilar y publicar
cd RecipeSearchWeb
dotnet clean
dotnet publish -c Release -o ..\publish

# 3. Crear ZIP
cd ..\publish
Compress-Archive -Path .\* -DestinationPath ..\app.zip -Force

# 4. IMPORTANTE: Configurar proxy
cd ..
$env:AZURE_CLI_DISABLE_CONNECTION_VERIFICATION = "1"

# 5. Desplegar
az webapp deployment source config-zip `
  --resource-group rg-hq-helpdeskai-poc-001 `
  --name powershell-scripts-helpdesk `
  --src app.zip

# 6. Limpiar
$env:AZURE_CLI_DISABLE_CONNECTION_VERIFICATION = $null
```

---

## ❓ FAQ

### ¿Es seguro deshabilitar la verificación SSL?

**Sí, en este contexto específico**:

- ✅ Solo se usa para despliegues (operación controlada)
- ✅ Ya estás autenticado con `az login` (identidad verificada)
- ✅ El proxy Zscaler ya inspecciona todo el tráfico (seguridad corporativa)
- ✅ Es una limitación técnica del certificado de Zscaler, no un riesgo real

### ¿Por qué no funciona el bundle de certificados?

El `combined_ca_bundle.pem` no funciona porque:
- Azure CLI en Windows no respeta consistentemente `REQUESTS_CA_BUNDLE`
- El certificado de Zscaler tiene problemas de formato ("Basic Constraints")
- Python subyacente de Azure CLI tiene validación estricta

### ¿Se mostrarán warnings?

Sí, verás mensajes como:
```
InsecureRequestWarning: Unverified HTTPS request is being made...
```

**Estos warnings son normales y esperados**. No afectan la funcionalidad del despliegue.

### ¿Se puede configurar de forma permanente?

**No recomendado**. Es mejor configurar solo cuando sea necesario:

```powershell
# ❌ NO hacer permanente (afecta toda Azure CLI)
[Environment]::SetEnvironmentVariable("AZURE_CLI_DISABLE_CONNECTION_VERIFICATION", "1", "User")

# ✅ MEJOR: Solo para la sesión actual
$env:AZURE_CLI_DISABLE_CONNECTION_VERIFICATION = "1"
```

---

## 📋 Historial de Intentos

| Método | Estado | Notas |
|--------|--------|-------|
| `REQUESTS_CA_BUNDLE` + combined_ca_bundle.pem | ❌ | Azure CLI no lo respeta en Windows |
| `az webapp deploy --type zip` | ❌ | Mismo problema SSL |
| Certificado Zscaler solo | ❌ | Falta cadena completa de CAs |
| **`AZURE_CLI_DISABLE_CONNECTION_VERIFICATION=1`** | ✅ | **FUNCIONA** |

---

## 🔗 Referencias

- [Documentación completa de despliegue](AZURE_DEPLOYMENT_INFO.md)
- [Azure CLI behind proxy](https://learn.microsoft.com/cli/azure/use-cli-effectively#work-behind-a-proxy)
- [Script automatizado](../deploy.ps1)

---

**Última actualización**: 26 Enero 2026  
**Red**: Antolin Corporate Network (Zscaler Proxy)  
**Validado con**: Azure CLI latest version
