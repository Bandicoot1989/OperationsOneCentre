# Employee Audit Dashboard

## Descripción General

El Employee Audit Dashboard es una herramienta interactiva para visualizar los resultados de auditoría de números de empleado (Employee Number) en Active Directory, procesando datos de **132 plantas** a nivel global.

### Componentes

| Archivo | Ruta | Descripción |
| --------- | ------ | ------------- |
| **index.html** (upload) | `RecipeSearchWeb/wwwroot/employee-audit/index.html` | Dashboard interactivo con upload de JSON |
| **Dashboard.html** (estático) | `RecipeSearchWeb/wwwroot/Dashboard.html` | Dashboard con datos embebidos (`window.__DATA__`) |
| **PS Script** | `tools/EmployeeAudit/ExportADUsersEmployeeNumberCheck.ps1` | Script PowerShell que genera el reporte JSON desde AD |

---

## Arquitectura de Datos

### Flujo de Datos

```text
Active Directory → PS Script → GlobalEmployeeNumberReport.json → Dashboard (upload) → Visualización
```

### Formato JSON del Script PowerShell (formato completo)

El script `ExportADUsersEmployeeNumberCheck.ps1` genera `GlobalEmployeeNumberReport.json` con esta estructura:

```json
{
  "reportMetadata": {
    "generatedAt": "2026-02-12T10:20:04",
    "totalPlantsProcessed": 132,
    "reportType": "Employee Number Validation"
  },
  "globalSummary": {
    "totalUsers": 8788,
    "totalMissingEmployeeNumbers": 585,
    "totalInvalidFormat": 647,
    "totalIssues": 1232,
    "issuePercentage": 14.02
  },
  "plants": [
    {
      "plant": "nombre_planta",
      "summary": {
        "totalUsers": 100,
        "missingCount": 5,
        "invalidFormatCount": 3,
        "totalIssues": 8
      },
      "usersWithMissingEmployeeNumber": [
        {
          "samAccountName": "user.name",
          "displayName": "User Name",
          "emailAddress": "user@antolin.com"
        }
      ],
      "usersWithInvalidFormat": [
        {
          "samAccountName": "user.name",
          "displayName": "User Name",
          "employeeNumber": "SHARED",
          "reason": "Contains invalid keyword: SHARED"
        }
      ]
    }
  ]
}
```

### Formato Compacto (usado internamente por el Dashboard)

El converter transforma el formato completo al formato compacto:

```json
{
  "meta": {
    "reportType": "Employee Number Validation",
    "generatedAt": "2026-02-12T10:20:04",
    "totalPlants": 132
  },
  "summary": {
    "totalUsers": 8788,
    "totalMissing": 585,
    "totalInvalid": 647,
    "totalIssues": 1232,
    "issuePercent": 14.02
  },
  "p": [
    {
      "n": "nombre_planta",
      "u": 100,
      "m": 5,
      "i": 3,
      "t": 8,
      "ml": [["User Name", "user@antolin.com", "user.name"]],
      "il": [["User Name", null, "user.name", "SHARED", "Contains invalid keyword: SHARED"]]
    }
  ]
}
```

**Clave de campos compactos:**

- `n` = name (nombre planta)
- `u` = users (total usuarios)
- `m` = missing (faltantes)
- `i` = invalid (formato inválido)
- `t` = total issues
- `ml` = missing list [displayName, email, samAccountName]
- `il` = invalid list [displayName, email, samAccountName, employeeNumber, reason]

---

## Converter: `convertFullToCompact()`

### Diseño

El converter usa dos técnicas clave:

#### 1. Función `pick()` en vez de `||`

```javascript
// ❌ MAL: || trata 0 como falsy
const users = s.totalUsers || s.TotalUsers || 0;  // Si totalUsers es 0, salta al siguiente

// ✅ BIEN: pick() preserva 0
function pick() {
  for (var i = 0; i < arguments.length; i++) {
    if (arguments[i] !== undefined && arguments[i] !== null) return arguments[i];
  }
  return 0;
}
const users = Number(pick(s.totalUsers, s.TotalUsers, 0));  // 0 se preserva correctamente
```

#### 2. Summary computado desde datos de plantas (no de `globalSummary`)

```javascript
// El summary se CALCULA sumando los datos de cada planta convertida
const totalUsers = compactPlants.reduce((sum, p) => sum + p.u, 0);
const totalMissing = compactPlants.reduce((sum, p) => sum + p.m, 0);
const totalInvalid = compactPlants.reduce((sum, p) => sum + p.i, 0);
const totalIssues = totalMissing + totalInvalid;
const issuePercent = totalUsers > 0 ? parseFloat(((totalIssues / totalUsers) * 100).toFixed(2)) : 0;
```

Esto es más robusto que depender de las keys de `globalSummary`, ya que diferentes versiones del script PS pueden usar nombres de campo distintos.

---

## Deployment

### Archivos estáticos y compresión ASP.NET

ASP.NET genera automáticamente versiones pre-comprimidas de archivos estáticos durante el build:

```text
wwwroot/employee-audit/index.html      ← archivo fuente
wwwroot/employee-audit/index.html.br   ← comprimido Brotli (generado por build)
wwwroot/employee-audit/index.html.gz   ← comprimido gzip (generado por build)
```

El archivo `RecipeSearchWeb.staticwebassets.endpoints.json` registra todos los endpoints estáticos, incluyendo versiones con fingerprint hash.

### ⚠️ IMPORTANTE: Siempre usar Full Build para cambios en archivos estáticos

```powershell
# ✅ CORRECTO: Full build regenera .br/.gz desde el source actualizado
.\deploy.ps1

# ❌ INCORRECTO: -SkipBuild reutiliza .br/.gz viejos del publish anterior
.\deploy.ps1 -SkipBuild
```

**¿Por qué?** Azure App Service sirve los archivos `.br` (Brotli) con prioridad sobre el `.html` plano. Si solo se copia el `.html` nuevo sin regenerar los comprimidos, el servidor sigue sirviendo la versión vieja comprimida.

### Proceso de deploy correcto

1. Editar `RecipeSearchWeb/wwwroot/employee-audit/index.html`
2. Ejecutar `.\deploy.ps1` (sin `-SkipBuild`)
3. El build regenera `publish/wwwroot/employee-audit/index.html.br` y `.gz`
4. El zip incluye los 3 archivos con contenido actualizado
5. Deploy a Azure App Service

---

## Funcionalidades del Dashboard

- **Upload de JSON**: Arrastra/suelta o selecciona `GlobalEmployeeNumberReport.json`
- **Auto-detección de formato**: Detecta formato completo o compacto automáticamente
- **KPI Cards**: Total Usuarios, Incidencias, Faltantes, Formato Inválido
- **Top 10 Plantas**: Las plantas con más incidencias
- **Búsqueda Global**: Buscar usuarios en TODAS las plantas
- **Filtros**: Todas, Con Incidencias, Sin Incidencias, Con Faltantes, Con Inválidos, Con SHARED
- **Tabla ordenable**: Por nombre, usuarios, faltantes, inválidos, total, riesgo
- **Modal de detalle**: Ver usuarios faltantes/inválidos por planta
- **Exportar CSV**: Descargar datos en formato CSV
- **Tags**: NON-NUM (formato no numérico), SHARED (cuentas compartidas)

---

## Historial de Bugs y Fixes

### Bug: Nombres de plantas no aparecían + KPIs en 0 (Feb 2026)

**Síntomas:**

- Columna PLANTA vacía en la tabla
- KPIs "Total Usuarios" y "Total Incidencias" mostraban 0
- "Nums Faltantes" mostraba 585 (calculado desde user lists, no desde summary)
- Top 10 plantas sin nombres

**Causa raíz (3 problemas):**

1. **Nombre de campo del PS script**: El script usa `plant` como key del nombre, pero el converter buscaba `plantName` primero. Con `||`, al no encontrar `plantName`, caía a `''`.

2. **`||` con valores numéricos**: El operador `||` en JS trata `0` como falsy. Si `totalUsers` era `0` para una planta, el chain `s.totalUsers || s.TotalUsers || 0` saltaba incorrectamente.

3. **Deploy con `-SkipBuild`**: Los archivos `.br`/`.gz` pre-comprimidos del build anterior contenían el código viejo. Azure sirve `.br` con prioridad, ignorando el `.html` actualizado. Al borrar los `.br`/`.gz` sin rebuild, el endpoint config (`staticwebassets.endpoints.json`) aún los referenciaba, causando una página en blanco.

**Solución:**

- Función `pick()` que usa `!== null && !== undefined` en vez de `||`
- Summary computado desde datos de plantas (reduce sobre los arrays)
- Full build (`.\deploy.ps1`) para regenerar archivos comprimidos
