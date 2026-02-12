# Employee Number Audit Tool

Herramienta de auditoria para validar Employee Numbers en Active Directory por planta.

## Componentes

### 1. Script PowerShell - `ExportADUsersEmployeeNumberCheck.ps1`

Escanea todas las OUs (plantas) en Active Directory y detecta:
- **Employee Numbers faltantes** (vacios, null)
- **Formatos invalidos** (SHARED, EXTERNAL, SERVICE, non-numeric, etc.)

**Requisitos:**
- PowerShell 5.1+
- Modulo ActiveDirectory (`Import-Module ActiveDirectory`)
- Acceso de lectura al dominio `grupoantolin.com`

**Uso:**
```powershell
.\ExportADUsersEmployeeNumberCheck.ps1
```

**Salida:** `GlobalEmployeeNumberReport.json` en la carpeta de resultados.

### 2. Dashboard Web - Employee Audit Dashboard

Dashboard interactivo React que visualiza los resultados del script.

**Acceso:**
- Dentro de la app: Menu lateral > **Employee Audit**
- Directo: `/employee-audit/index.html`

**Uso:**
1. Ejecute el script PowerShell
2. Arrastre el archivo `GlobalEmployeeNumberReport.json` al dashboard
3. Explore los resultados con filtros, busqueda y detalle por planta

**Funcionalidades:**
- 4 KPIs globales (usuarios, incidencias, faltantes, invalidos)
- Top 10 plantas con mas problemas
- 6 filtros (Todas, Con/Sin Incidencias, Faltantes, Invalidos, SHARED)
- Columnas ordenables
- Busqueda global de usuarios entre todas las plantas
- Modal de detalle por planta con tabs (Faltantes/Invalidos)
- Exportar resultados filtrados a CSV
- Tags de riesgo (SHARED, NON-NUM)
- Barras de riesgo visual por planta

### 3. Plants.txt

Lista de 132 plantas/OUs a escanear. Una por linea.