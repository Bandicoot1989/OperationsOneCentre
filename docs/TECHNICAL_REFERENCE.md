# 📚 Operations One Centre - Technical Reference

## Documentación Técnica Completa del Sistema

**Versión:** 4.4 - Multi-Agent Architecture (9 Agents) + Jira Monitoring + Feedback Loop  
**Última actualización:** 17 Febrero 2026  
**Autor:** IT Operations Team

---

## 📋 Índice

1. [Arquitectura General](#1-arquitectura-general)
2. [Flujo de Datos Principal](#2-flujo-de-datos-principal)
3. [Servicios (Services)](#3-servicios-services)
4. [Modelos (Models)](#4-modelos-models)
5. [Interfaces](#5-interfaces)
6. [Componentes Blazor](#6-componentes-blazor)
7. [Inyección de Dependencias](#7-inyección-de-dependencias)
8. [Flujos Detallados](#8-flujos-detallados)
9. [APIs y Endpoints](#9-apis-y-endpoints)
10. [Configuración](#10-configuración)

---

## 1. Arquitectura General

### 1.1 Stack Tecnológico

```
┌─────────────────────────────────────────────────────────────┐
│                    FRONTEND (Blazor Server)                  │
│  ┌─────────────────────────────────────────────────────────┐│
│  │ Components: KnowledgeChat, FeedbackAdmin, AgentContext  ││
│  └─────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    ROUTING LAYER                             │
│  ┌─────────────────────────────────────────────────────────┐│
│  │              AgentRouterService                          ││
│  │    Detecta tipo de consulta → Enruta al agente correcto ││
│  └─────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────┘
                              │
          ┌───────────────────┼───────────────────┐
          ▼                   ▼                   ▼
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│   SAP Agent     │  │  Network Agent  │  │  General Agent  │
│ (Specialist)    │  │  (Specialist)   │  │ (Knowledge)     │
└─────────────────┘  └─────────────────┘  └─────────────────┘
          │                   │                   │
          └───────────────────┼───────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    SEARCH LAYER                              │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐            │
│  │ Confluence │  │  Context   │  │ Knowledge  │            │
│  │  Service   │  │  Service   │  │   Base     │            │
│  └────────────┘  └────────────┘  └────────────┘            │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    STORAGE LAYER                             │
│  ┌────────────────────────────────────────────────────────┐ │
│  │              Azure Blob Storage                         │ │
│  │  Containers: agent-context, confluence-cache, scripts   │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    AI LAYER                                  │
│  ┌────────────────────────────────────────────────────────┐ │
│  │              Azure OpenAI                               │ │
│  │  gpt-4o-mini (chat) + text-embedding-3-small (vectors)  │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 Patrón de Diseño: Clean Architecture + Multi-Agent RAG

- **RAG (Retrieval Augmented Generation)**: Búsqueda + Generación con IA
- **Multi-Agent**: Router envía a agentes especializados
- **Enrichment Pattern**: Los agentes especializados ENRIQUECEN la búsqueda, no la reemplazan

---

## 2. Flujo de Datos Principal

### 2.1 Flujo de una Consulta de Usuario

```
Usuario escribe: "¿Cómo me conecto desde casa a Antolin?"
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│ 1. KnowledgeChat.razor                                       │
│    - Captura la pregunta                                     │
│    - Llama a IKnowledgeAgentService.AskAsync()              │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. AgentRouterService.AskAsync()                             │
│    - DetermineAgentAsync(): Detecta keywords "casa", "conectar"│
│    - Detecta: AgentType.Network                              │
│    - Llama a: KnowledgeAgentService.AskWithSpecialistAsync() │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. KnowledgeAgentService.AskWithSpecialistAsync()            │
│    a) DetectIntent(): QueryIntent.HowTo                      │
│    b) ExpandQueryWithSynonyms(): Añade "Zscaler VPN remote"  │
│    c) Parallel Search:                                       │
│       - SearchKnowledgeBase()                                │
│       - SearchContext() (Jira tickets)                       │
│       - SearchConfluence()                                   │
│    d) BuildContextWeighted(): Combina resultados             │
│    e) GetSpecialistSystemPrompt(): Network prompt            │
│    f) ChatClient.CompleteChatAsync(): Genera respuesta       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│ 4. Respuesta al Usuario                                      │
│    - Información sobre Zscaler                               │
│    - Link a documentación de Confluence                      │
│    - Link a ticket de soporte si necesario                   │
└─────────────────────────────────────────────────────────────┘
```

---

## 3. Servicios (Services)

### 3.1 AgentRouterService (PRINCIPAL)

**Archivo:** `Services/AgentRouterService.cs`  
**Propósito:** Router principal que determina qué agente debe manejar cada consulta.

```csharp
public class AgentRouterService : IKnowledgeAgentService
```

#### Métodos Principales:

| Método | Descripción |
|--------|-------------|
| `AskAsync(question, history)` | Punto de entrada. Determina agente y llama a `KnowledgeAgentService.AskWithSpecialistAsync()` |
| `AskWithSpecialistAsync(...)` | Delegación directa al generalAgent |
| `AskStreamingAsync(...)` | Streaming de respuestas |
| `DetermineAgentAsync(question)` | Determina si es SAP, Network o General |
| `GetSapSpecialistContextAsync(question)` | Obtiene datos de SAP Lookup para enriquecer contexto |

#### Lógica de Routing:

```
1. Verifica NetworkKeywords: "zscaler", "vpn", "conectar", "desde casa"...
2. Verifica SapKeywords: "sap", "transaccion", "rol sap", "fiori"...
3. Verifica SapPatterns: INCA01, MM01, SU01...
4. Verifica PlmKeywords: "windchill", "plm", "bom", "cad"...
5. Verifica EdiKeywords: "edi", "edifact", "as2", "seeburger"...
6. Verifica MesKeywords: "mes", "producción", "planta", "shopfloor"...
7. Verifica WorkplaceKeywords: "teams", "outlook", "office", "sharepoint"...
8. Verifica InfrastructureKeywords: "servidor", "backup", "vmware", "storage"...
9. Verifica CybersecurityKeywords: "seguridad", "phishing", "malware", "firewall"...
10. Si ambiguo → LLM Classification (GPT clasifica)
11. Default → General
```

#### Tipos de Agente (AgentType enum):

```csharp
public enum AgentType
{
    General,        // Consultas genéricas
    Sap,            // SAP ERP, transacciones, roles
    Network,        // Zscaler, VPN, conectividad
    Plm,            // Windchill, PLM, BOM, CAD
    Edi,            // EDI, EDIFACT, AS2, Seeburger
    Mes,            // MES, producción, planta
    Workplace,      // Teams, Outlook, Office 365
    Infrastructure, // Servidores, backup, VMware
    Cybersecurity   // Seguridad, phishing, malware
}
```

#### Keywords de Detección:

```csharp
// Network Keywords
"zscaler", "vpn", "remote", "remoto", "trabajo desde casa", "conectar",
"conecto", "conexion", "network", "red", "acceso remoto", "desde casa"...

// SAP Keywords  
"sap", "transaccion", "transacción", "t-code", "fiori", "sapgui",
"autorizacion", "rol sap", "posicion sap"...

// SAP Patterns (Regex)
"^[A-Z]{2}\d{2}$"      // SM35, MM01
"^[A-Z]{4}\d{2}$"      // INCA01, INGM01
```

---

### 3.2 KnowledgeAgentService (CORE)

**Archivo:** `Services/KnowledgeAgentService.cs`  
**Propósito:** Agente principal de RAG. Busca en todas las fuentes y genera respuestas.

```csharp
public class KnowledgeAgentService : IKnowledgeAgentService
```

#### Métodos Principales:

| Método | Descripción |
|--------|-------------|
| `AskAsync(question, history)` | Búsqueda completa + generación de respuesta |
| `AskWithSpecialistAsync(question, specialist, context, history)` | **NUEVO** - Búsqueda completa + prompt especializado |
| `AskStreamingAsync(question, history)` | Streaming de respuestas token por token |
| `DetectIntent(query)` | Detecta intención: HowTo, TicketRequest, Lookup, Troubleshooting |
| `ExpandQueryWithSynonyms(query)` | Expande query con sinónimos para mejor búsqueda |
| `DecomposeQuery(query)` | Divide query compleja en sub-queries |
| `SearchContextParallelAsync(...)` | Búsqueda paralela en Context Documents |
| `SearchConfluenceParallelAsync(...)` | Búsqueda paralela en Confluence |
| `BuildContextWeighted(...)` | Combina resultados con pesos según intent |
| `GetSpecialistSystemPrompt(specialist)` | Obtiene prompt según tipo de especialista |

#### Enum QueryIntent:

```csharp
public enum QueryIntent
{
    General,           // Consulta genérica
    HowTo,            // "¿Cómo hago...?" → Prioriza Confluence
    TicketRequest,    // "Necesito ticket" → Prioriza Jira forms
    Lookup,           // "¿Qué es X?" → Datos exactos
    Troubleshooting   // "No funciona..." → Soluciones + ticket
}
```

#### System Prompts Especializados:

```csharp
// NetworkSpecialistPrompt
"Eres el **Experto en Redes y Acceso Remoto**..."
// Incluye conocimiento sobre Zscaler

// SapSpecialistPrompt  
"Eres el **Experto en SAP**..."
// Incluye conocimiento sobre transacciones, roles, posiciones

// SystemPrompt (General)
"You are **Operations One Centre Bot**..."
// Prompt general para IT Operations
```

#### Flujo de AskWithSpecialistAsync:

```
1. DetectIntent() → HowTo/TicketRequest/etc
2. GetSearchWeights() → Pesos según intent
3. DecomposeQuery() → Sub-queries
4. ExpandQueryWithSynonyms() → Query expandida
5. Parallel Search:
   - KnowledgeSearchService.SearchArticlesAsync()
   - SearchContextParallelAsync()
   - SearchConfluenceParallelAsync()
6. BuildContextWeighted() → Combina resultados
7. GetSpecialistSystemPrompt() → Prompt según tipo
8. ChatClient.CompleteChatAsync() → Genera respuesta
```

---

### 3.3 ContextSearchService

**Archivo:** `Services/ContextSearchService.cs`  
**Propósito:** Búsqueda en documentos de contexto (Excel con tickets Jira, datos de referencia).

```csharp
public class ContextSearchService : IContextService
```

#### Métodos:

| Método | Descripción |
|--------|-------------|
| `InitializeAsync()` | Carga documentos desde Azure Blob Storage |
| `SearchAsync(query, topResults)` | Búsqueda híbrida (keyword + semantic) |
| `GetAllDocumentsAsync()` | Retorna todos los documentos |

#### Búsqueda Híbrida:

```csharp
// 1. Keyword Search
var keywordResults = documents.Where(d => 
    d.Name.Contains(query) || 
    d.Keywords.Contains(query) ||
    d.Description.Contains(query));

// 2. Semantic Search (Vector)
var embedding = await GenerateEmbedding(query);
var semanticResults = documents
    .Select(d => (Doc: d, Score: CosineSimilarity(embedding, d.Embedding)))
    .OrderByDescending(x => x.Score);

// 3. RRF (Reciprocal Rank Fusion)
var combined = ReciprocalRankFusion(keywordResults, semanticResults);
```

---

### 3.4 ConfluenceKnowledgeService

**Archivo:** `Services/ConfluenceKnowledgeService.cs`  
**Propósito:** Integración con Atlassian Confluence para documentación.

```csharp
public class ConfluenceKnowledgeService : IConfluenceService
```

#### Métodos:

| Método | Descripción |
|--------|-------------|
| `InitializeAsync()` | Carga caché de páginas desde Azure Blob |
| `SyncPagesAsync()` | Sincroniza páginas desde Confluence API |
| `SearchAsync(query, topResults)` | Búsqueda semántica en páginas |
| `GetAllPagesAsync()` | Retorna todas las páginas cacheadas |
| `GetCachedPageCount()` | Número de páginas en caché |

#### Cache en Azure Blob:

```
Container: confluence-cache
Blob: confluence-kb-cache.json
Contenido: Lista de ConfluencePage con embeddings pre-calculados
```

---

### 3.5 SapLookupService

**Archivo:** `Services/SapLookupService.cs`  
**Propósito:** Lookups O(1) de datos SAP (transacciones, roles, posiciones).

```csharp
public class SapLookupService
```

#### Métodos:

| Método | Descripción |
|--------|-------------|
| `InitializeAsync()` | Construye índices desde SapKnowledgeService |
| `GetTransaction(code)` | Lookup de transacción por código |
| `GetRole(roleId)` | Lookup de rol por ID |
| `GetPosition(positionId)` | Lookup de posición por ID |
| `GetTransactionsByPosition(positionId)` | Transacciones de una posición |
| `GetTransactionsByRole(roleId)` | Transacciones de un rol |
| `GetRolesForPosition(positionId)` | Roles asignados a una posición |

#### Índices (Dictionaries O(1)):

```csharp
_transactionsByCode      // "MM01" → SapTransaction
_transactionsByRole      // "Z_QM_01" → List<SapTransaction>
_transactionsByPosition  // "INCA01" → List<SapTransaction>
_rolesByCode            // "Z_QM_01" → SapRole
_positionsByCode        // "INCA01" → SapPosition
_rolesByPosition        // "INCA01" → List<string> (roleIds)
```

---

### 3.6 SapKnowledgeService

**Archivo:** `Services/SapKnowledgeService.cs`  
**Propósito:** Carga datos SAP desde Excel en Azure Blob Storage.

```csharp
public class SapKnowledgeService
```

#### Métodos:

| Método | Descripción |
|--------|-------------|
| `InitializeAsync()` | Carga Excel desde Azure Blob |
| `Transactions` | Lista de todas las transacciones |
| `Roles` | Lista de todos los roles |
| `Positions` | Lista de todas las posiciones |
| `Mappings` | Mapeos Position → Role → Transaction |

#### Archivos Excel:

```
Container: agent-context
Blobs:
  - Context_SAP_Transactions.xlsx
  - Context_SAP_Roles.xlsx
  - Context_SAP_Positions.xlsx
```

---

### 3.7 FeedbackService

**Archivo:** `Services/FeedbackService.cs`  
**Propósito:** Gestión de feedback de usuarios y auto-aprendizaje.

```csharp
public class FeedbackService
```

#### Métodos:

| Método | Descripción |
|--------|-------------|
| `InitializeAsync()` | Carga datos de feedback desde Azure Blob |
| `SubmitFeedbackAsync(...)` | Guarda feedback (👍/👎) |
| `CheckHealthAsync()` | Verifica conectividad con Azure |
| `GetAllFeedbackAsync()` | Retorna todo el feedback |
| `GetStatsAsync()` | Estadísticas de satisfacción |
| `GetCachedResponseAsync(query)` | Busca respuesta cacheada similar |
| `CacheSuccessfulResponseAsync(...)` | Guarda respuesta exitosa |
| `TrackFailurePatternAsync(...)` | Registra patrón de fallo |
| `TryAutoEnrichKeywordsAsync()` | Auto-enriquece keywords |

#### Auto-Learning Features:

1. **Cached Responses**: Guarda query→response exitosos con embedding
2. **Failure Patterns**: Detecta consultas que fallan repetidamente
3. **Auto-Enrichment**: Añade keywords automáticamente a documentos

#### Storage en Azure Blob:

```
Container: agent-context
Blobs:
  - chat-feedback.json          // Historial de feedback
  - successful-responses.json   // Respuestas cacheadas
  - failure-patterns.json       // Patrones de fallos
  - auto-learning-log.json      // Log de auto-aprendizaje
```

---

### 3.8 QueryCacheService

**Archivo:** `Services/QueryCacheService.cs`  
**Propósito:** Cache en memoria para respuestas frecuentes.

```csharp
public class QueryCacheService
```

#### Métodos:

| Método | Descripción |
|--------|-------------|
| `TryGetResponse(query)` | Busca respuesta en caché exacta |
| `CacheResponse(query, response, sources)` | Guarda en caché |
| `TryGetSemanticCacheAsync(query)` | Busca respuesta similar (semántica) |
| `AddToSemanticCacheAsync(...)` | Añade al caché semántico |

#### Tipos de Cache:

```csharp
// 1. String Cache (exacto)
MemoryCache con key = query.ToLowerInvariant()
Duración: 30 minutos

// 2. Semantic Cache (similar)
Lista de (query, embedding, response)
Threshold: 0.95 similaridad coseno
```

---

### 3.9 NetworkAgentService

**Archivo:** `Services/NetworkAgentService.cs`  
**Propósito:** Agente especializado en redes (Zscaler, VPN, conectividad).

> **NOTA:** En la arquitectura actual, este servicio NO se usa directamente.
> El `AgentRouterService` usa `KnowledgeAgentService.AskWithSpecialistAsync()` con `SpecialistType.Network`.

```csharp
public class NetworkAgentService
```

#### Métodos:

| Método | Descripción |
|--------|-------------|
| `AskNetworkAsync(question, history)` | Responde consultas de red |
| `GetConfluenceContextAsync(question)` | Busca en Confluence |
| `GetNetworkTicketsAsync(question)` | Busca tickets de red en contexto |

---

### 3.10 SapAgentService

**Archivo:** `Services/SapAgentService.cs`  
**Propósito:** Agente especializado en SAP.

> **NOTA:** Similar a NetworkAgentService, ahora se usa el flujo unificado.

```csharp
public class SapAgentService
```

#### Métodos:

| Método | Descripción |
|--------|-------------|
| `AskSapAsync(question, history)` | Responde consultas SAP |
| `LookupSapDataAsync(question)` | Busca en SapLookupService |

---

### 3.11 ContextStorageService

**Archivo:** `Services/ContextStorageService.cs`  
**Propósito:** Almacenamiento de documentos de contexto en Azure Blob.

```csharp
public class ContextStorageService
```

#### Métodos:

| Método | Descripción |
|--------|-------------|
| `InitializeAsync()` | Crea contenedor si no existe |
| `LoadDocumentsAsync()` | Carga documentos desde blob |
| `SaveDocumentsAsync(docs)` | Guarda documentos en blob |
| `ImportFromExcelAsync(file)` | Importa Excel a contexto |

---

### 3.12 KnowledgeStorageService

**Archivo:** `Services/KnowledgeStorageService.cs`  
**Propósito:** Almacenamiento de Knowledge Base (artículos internos).

```csharp
public class KnowledgeStorageService : IKnowledgeStorageService
```

#### Métodos:

| Método | Descripción |
|--------|-------------|
| `InitializeAsync()` | Inicializa contenedor |
| `GetAllArticlesAsync()` | Retorna todos los artículos |
| `SaveArticleAsync(article)` | Guarda artículo |
| `DeleteArticleAsync(id)` | Elimina artículo |

---

### 3.13 KnowledgeSearchService

**Archivo:** `Services/KnowledgeSearchService.cs`  
**Propósito:** Búsqueda semántica en Knowledge Base.

```csharp
public class KnowledgeSearchService : IKnowledgeService
```

#### Métodos:

| Método | Descripción |
|--------|-------------|
| `InitializeAsync()` | Carga artículos y genera embeddings |
| `SearchArticlesAsync(query, topResults)` | Búsqueda semántica |
| `GetAllArticlesAsync()` | Retorna todos los artículos |

---

### 3.14 AzureAuthService

**Archivo:** `Services/AzureAuthService.cs`  
**Propósito:** Autenticación con Azure Easy Auth.

```csharp
public class AzureAuthService : IAuthService
```

#### Métodos:

| Método | Descripción |
|--------|-------------|
| `GetCurrentUserAsync()` | Obtiene usuario actual desde headers |
| `IsAdminAsync()` | Verifica si es administrador |

#### Headers de Azure Easy Auth:

```
X-MS-CLIENT-PRINCIPAL-NAME: email@grupoantolin.com
X-MS-CLIENT-PRINCIPAL-ID: user-id
```

---

### 3.15 JiraMonitoringService

**Archivo:** `Services/JiraMonitoringService.cs`  
**Propósito:** Obtiene estadísticas y métricas de tickets Jira para el dashboard de Monitoring.

```csharp
public class JiraMonitoringService
```

#### Métodos:

| Método | Descripción |
|--------|-------------|
| `GetDashboardStatsAsync()` | Obtiene todas las estadísticas del dashboard |
| `IsConfigured` | Propiedad que indica si Jira está configurado |

#### Modelo JiraMonitoringStats:

```csharp
public class JiraMonitoringStats
{
    public int OpenTickets { get; set; }           // Tickets abiertos actualmente
    public int ClosedToday { get; set; }           // Resueltos hoy
    public int TotalThisMonth { get; set; }        // Total del mes actual
    public int CriticalTickets { get; set; }       // Prioridad Highest/High
    public List<WeeklyTrend> WeeklyTrends { get; set; }
    public List<JiraTicketSummary> RecentTickets { get; set; }
}
```

#### Modelo JiraTicketSummary:

```csharp
public class JiraTicketSummary
{
    public string Key { get; set; }                // MT-123
    public string Summary { get; set; }
    public string Status { get; set; }
    public string Priority { get; set; }
    public string? Assignee { get; set; }
    public string? Reporter { get; set; }          // Nuevo campo
    public DateTime Created { get; set; }
    public string Url { get; set; }                // Link directo a Jira
}
```

#### JQL Queries Utilizadas:

```jql
// Tickets abiertos
project IN (MT, MTT) AND status NOT IN (Resolved, Closed, Done)

// Cerrados hoy (zona horaria España)
project IN (MT, MTT) AND resolved >= "YYYY-MM-DD"

// Total del mes
project IN (MT, MTT) AND created >= startOfMonth()

// Críticos
project IN (MT, MTT) AND priority IN (Highest, High) 
    AND status NOT IN (Resolved, Closed, Done)

// Tickets recientes (25 últimos)
project IN (MT, MTT) ORDER BY created DESC
```

---

## 4. Modelos (Models)

### 4.1 ChatFeedback

```csharp
public class ChatFeedback
{
    public string Id { get; set; }
    public string Query { get; set; }
    public string Response { get; set; }
    public bool IsHelpful { get; set; }           // 👍 true, 👎 false
    public string? Comment { get; set; }
    public string AgentType { get; set; }          // "SAP", "Network", "General"
    public double BestSearchScore { get; set; }
    public bool WasLowConfidence { get; set; }
    public List<string> ExtractedKeywords { get; set; }
    public List<string> SuggestedKeywords { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsReviewed { get; set; }
    public bool IsApplied { get; set; }
}
```

### 4.2 SuccessfulResponse

```csharp
public class SuccessfulResponse
{
    public string Id { get; set; }
    public string Query { get; set; }
    public string Response { get; set; }
    public string AgentType { get; set; }
    public float[] QueryEmbedding { get; set; }    // Para búsqueda semántica
    public int UseCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUsedAt { get; set; }
}
```

### 4.3 FailurePattern

```csharp
public class FailurePattern
{
    public string Id { get; set; }
    public string PatternDescription { get; set; }  // Keywords combinadas
    public List<string> SampleQueries { get; set; }
    public int FailureCount { get; set; }
    public string? SuggestedAction { get; set; }
    public bool IsAlerted { get; set; }
    public DateTime FirstOccurrence { get; set; }
    public DateTime LastOccurrence { get; set; }
}
```

### 4.4 ContextDocument

```csharp
public class ContextDocument
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Keywords { get; set; }
    public string Link { get; set; }               // URL del ticket Jira
    public string Category { get; set; }
    public string SourceFile { get; set; }
    public float[] Embedding { get; set; }
    public double SearchScore { get; set; }        // Score de búsqueda
    public Dictionary<string, string> AdditionalData { get; set; }
}
```

### 4.5 ConfluencePage

```csharp
public class ConfluencePage
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string SpaceKey { get; set; }
    public string Content { get; set; }            // Texto limpio
    public string WebUrl { get; set; }             // URL de la página
    public float[] Embedding { get; set; }
    public DateTime LastModified { get; set; }
}
```

### 4.6 KnowledgeArticle

```csharp
public class KnowledgeArticle
{
    public string Id { get; set; }
    public string KBNumber { get; set; }           // KB0001, KB0002...
    public string Title { get; set; }
    public string ShortDescription { get; set; }
    public string Content { get; set; }
    public string Category { get; set; }
    public List<string> Tags { get; set; }
    public float[] Embedding { get; set; }
    public double SearchScore { get; set; }
}
```

### 4.7 SapModels

```csharp
public class SapTransaction
{
    public string Code { get; set; }               // MM01, SU01...
    public string Description { get; set; }
    public string RoleId { get; set; }
    public string PositionId { get; set; }
}

public class SapRole
{
    public string RoleId { get; set; }
    public string Description { get; set; }
}

public class SapPosition
{
    public string PositionId { get; set; }         // INCA01, INGM01...
    public string Name { get; set; }               // Quality Manager
}

public class SapPositionRoleMapping
{
    public string PositionId { get; set; }
    public string PositionName { get; set; }
    public string BRole { get; set; }
    public string BRoleName { get; set; }
    public string RoleId { get; set; }
    public string Transaction { get; set; }
    public string TransactionDescription { get; set; }
}
```

### 4.8 AgentResponse

```csharp
public class AgentResponse
{
    public string Answer { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string AgentType { get; set; }          // "SAP", "Network", "General"
    public bool LowConfidence { get; set; }
    public bool FromCache { get; set; }
    public List<ArticleReference> RelevantArticles { get; set; }
    public List<ConfluenceReference> ConfluenceSources { get; set; }
}
```

---

## 5. Interfaces

### 5.1 IKnowledgeAgentService

```csharp
public interface IKnowledgeAgentService
{
    Task<AgentResponse> AskAsync(string question, List<ChatMessage>? conversationHistory = null);
    Task<AgentResponse> AskWithSpecialistAsync(string question, SpecialistType specialist, 
        string? specialistContext = null, List<ChatMessage>? conversationHistory = null);
    IAsyncEnumerable<string> AskStreamingAsync(string question, List<ChatMessage>? conversationHistory = null);
}

public enum SpecialistType
{
    General,        // Consultas genéricas
    SAP,            // SAP ERP, transacciones, roles
    Network,        // Zscaler, VPN, conectividad
    Plm,            // Windchill, PLM, BOM, CAD
    Edi,            // EDI, EDIFACT, AS2, Seeburger
    Mes,            // MES, producción, planta
    Workplace,      // Teams, Outlook, Office 365
    Infrastructure, // Servidores, backup, VMware
    Cybersecurity   // Seguridad, phishing, malware
}
```

### 5.2 IContextService

```csharp
public interface IContextService
{
    Task InitializeAsync();
    Task<List<ContextDocument>> SearchAsync(string query, int topResults = 10);
    Task<List<ContextDocument>> GetAllDocumentsAsync();
}
```

### 5.3 IConfluenceService

```csharp
public interface IConfluenceService
{
    bool IsConfigured { get; }
    Task InitializeAsync();
    Task<List<ConfluencePage>> SearchAsync(string query, int topResults = 5);
    Task<int> SyncPagesAsync();
    int GetCachedPageCount();
}
```

---

## 6. Componentes Blazor

### 6.1 KnowledgeChat.razor

**Ruta:** `/chat` o como componente embebido  
**Propósito:** Chat interactivo con el bot.

#### Variables de Estado:

```csharp
private List<ChatMessage> messages = new();       // Historial de mensajes
private string currentMessage = "";                // Input actual
private bool isLoading = false;                    // Estado de carga
private string? selectedAssistantMessage;          // Para feedback
```

#### Métodos:

| Método | Descripción |
|--------|-------------|
| `SendMessage()` | Envía mensaje y obtiene respuesta |
| `SubmitFeedback(isHelpful)` | Envía 👍/👎 al FeedbackService |
| `ScrollToBottom()` | Scroll automático |

#### Flujo de Interacción:

```
1. Usuario escribe → currentMessage
2. Click Enviar → SendMessage()
3. Añade UserChatMessage a messages
4. Llama AgentService.AskAsync()
5. Añade AssistantChatMessage con respuesta
6. Usuario puede dar 👍/👎 → SubmitFeedback()
```

---

### 6.2 FeedbackAdmin.razor

**Ruta:** `/feedback-admin`  
**Propósito:** Panel de administración de feedback y training.

#### Secciones:

1. **Health Banner**: Estado de conexión Azure
2. **Stats Grid**: Métricas (positivo, negativo, satisfacción)
3. **Failure Alerts**: Patrones de fallos recurrentes
4. **Keyword Suggestions**: Keywords sugeridas para añadir
5. **Feedback List**: Lista de feedback con filtros

---

### 6.3 AgentContext.razor

**Ruta:** `/agent-context`  
**Propósito:** Administración de documentos de contexto.

#### Funciones:

- Importar Excel con tickets Jira
- Ver documentos actuales
- Sincronizar Confluence
- Ver estadísticas de búsqueda

---

### 6.4 Home.razor

**Ruta:** `/`  
**Propósito:** Página principal con tarjetas de navegación a módulos.

#### Tarjetas de Navegación:
- Scripts Repository
- Knowledge Base
- Agent Context
- Feedback Admin
- Monitoring (link a dashboard de Jira)

---

### 6.5 Monitoring.razor

**Ruta:** `/monitoring`  
**Propósito:** Dashboard de métricas de Jira en tiempo real.

#### Variables de Estado:

```csharp
private JiraMonitoringStats? stats;           // Datos del dashboard
private bool isLoading = true;                // Estado de carga
private string? errorMessage;                 // Mensaje de error

// Filtros de búsqueda
private string searchQuery = "";              // Búsqueda por texto
private string reporterFilter = "";           // Filtro por reporter
private string statusFilter = "";             // Filtro por status
private string priorityFilter = "";           // Filtro por prioridad
```

#### Métodos:

| Método | Descripción |
|--------|-------------|
| `LoadStatsAsync()` | Carga estadísticas desde JiraMonitoringService |
| `GetFilteredTickets()` | Filtra tickets según criterios de búsqueda |
| `GetUniqueReporters()` | Obtiene lista de reporters únicos |
| `GetUniqueStatuses()` | Obtiene lista de estados únicos |
| `GetUniquePriorities()` | Obtiene lista de prioridades únicas |

#### Componentes UI:

1. **KPI Cards**: 4 tarjetas con métricas principales
   - Tickets Abiertos (azul)
   - Cerrados Hoy (verde)
   - Total del Mes (naranja)
   - Críticos (rojo)

2. **Trend Chart**: Gráfico de tendencia semanal (SVG)
   - Línea azul: tickets abiertos
   - Línea verde: tickets resueltos

3. **Filter Controls**: Barra de filtros
   - Input de búsqueda
   - Dropdown de reporter
   - Dropdown de status
   - Dropdown de priority
   - Contador de resultados

4. **Tickets Table**: Tabla de 25 tickets recientes
   - Columnas: Key, Summary, Status, Priority, Reporter, Assignee, Created
   - Links directos a Jira

---

## 7. Inyección de Dependencias

### 7.1 Program.cs

```csharp
// Azure OpenAI
builder.Services.AddSingleton(azureClient);
builder.Services.AddSingleton(embeddingClient);

// Service Groups
builder.Services.AddStorageServices();      // Storage layer
builder.Services.AddConfluenceServices();   // Confluence integration
builder.Services.AddSearchServices();       // Vector search
builder.Services.AddCachingServices();      // Query cache
builder.Services.AddSapServices();          // SAP specialist
builder.Services.AddNetworkServices();      // Network specialist  
builder.Services.AddFeedbackServices();     // Feedback & learning
builder.Services.AddAgentServices();        // AI agents
builder.Services.AddAuthServices();         // Authentication
builder.Services.AddDocumentServices();     // Document processing
builder.Services.AddJiraSolutionServices(); // Jira integration
builder.Services.AddJiraMonitoringService(); // Jira monitoring dashboard
```

### 7.2 DependencyInjection.cs

Cada grupo de servicios tiene su método de extensión:

```csharp
public static IServiceCollection AddAgentServices(this IServiceCollection services)
{
    // Base agent
    services.AddSingleton<KnowledgeAgentService>();
    
    // Router as primary interface
    services.AddSingleton<AgentRouterService>();
    services.AddSingleton<IKnowledgeAgentService>(sp => sp.GetRequiredService<AgentRouterService>());
    
    return services;
}
```

---

## 8. Flujos Detallados

### 8.1 Flujo: Consulta SAP (Posición INCA01)

```
Usuario: "¿Qué posición es INCA01?"
          │
          ▼
┌─────────────────────────────────────────────────┐
│ AgentRouterService.DetermineAgentAsync()        │
│ 1. Detecta patrón "^[A-Z]{4}\d{2}$" → INCA01    │
│ 2. Return: AgentType.SAP                        │
└─────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────┐
│ GetSapSpecialistContextAsync("INCA01")          │
│ 1. SapLookup.GetPosition("INCA01")              │
│    → { PositionId: "INCA01", Name: "Quality Mgr"}│
│ 2. SapLookup.GetRolesForPosition("INCA01")      │
│    → ["Z_QM_01", "Z_QM_02", ...]                │
│ 3. SapLookup.GetTransactionsByPosition("INCA01")│
│    → [QM01, QM02, QM03, ...]                    │
│ 4. Construye string de contexto SAP             │
└─────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────┐
│ KnowledgeAgentService.AskWithSpecialistAsync()  │
│ specialist = SpecialistType.SAP                 │
│ specialistContext = "SAP Position INCA01..."    │
│                                                 │
│ 1. Búsqueda paralela (KB + Context + Confluence)│
│ 2. Usa SapSpecialistPrompt                      │
│ 3. Genera respuesta con datos de SAP            │
└─────────────────────────────────────────────────┘
          │
          ▼
Respuesta: "La posición INCA01 es Quality Manager.
           Tiene acceso a las siguientes transacciones:
           - QM01: Create Inspection Lot
           - QM02: Change Inspection Lot
           ..."
```

### 8.2 Flujo: Consulta Network (Zscaler)

```
Usuario: "¿Cómo me conecto desde casa?"
          │
          ▼
┌─────────────────────────────────────────────────┐
│ AgentRouterService.DetermineAgentAsync()        │
│ 1. Detecta keywords: "conecto", "desde casa"    │
│ 2. Return: AgentType.Network                    │
└─────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────┐
│ KnowledgeAgentService.AskWithSpecialistAsync()  │
│ specialist = SpecialistType.Network             │
│ specialistContext = null (no lookup especial)   │
│                                                 │
│ 1. ExpandQuery: "Zscaler VPN remote access..."  │
│ 2. Busca en Confluence: Guías de Zscaler        │
│ 3. Busca en Context: Tickets de VPN/Network     │
│ 4. Usa NetworkSpecialistPrompt                  │
│ 5. Genera respuesta con pasos de conexión       │
└─────────────────────────────────────────────────┘
          │
          ▼
Respuesta: "Para conectarte desde casa necesitas Zscaler.
           
           1. Asegúrate de tener Zscaler Client instalado
           2. Inicia sesión con tu cuenta corporativa
           3. Verifica que el icono esté verde
           
           📖 Más información: [Guía Zscaler](url)
           
           Si tienes problemas: [Abrir ticket](url)"
```

### 8.3 Flujo: Feedback Positivo (👍)

```
Usuario da 👍 a una respuesta
          │
          ▼
┌─────────────────────────────────────────────────┐
│ KnowledgeChat.SubmitFeedback(true)              │
│ Llama: FeedbackService.SubmitFeedbackAsync()    │
└─────────────────────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────────┐
│ FeedbackService.SubmitFeedbackAsync()           │
│ isHelpful = true                                │
│                                                 │
│ 1. Crea ChatFeedback con datos                  │
│ 2. CacheSuccessfulResponseAsync()               │
│    - Guarda query + response + embedding        │
│    - Para reusar en consultas similares         │
│ 3. Guarda feedback en Azure Blob                │
└─────────────────────────────────────────────────┘
```

### 8.4 Flujo: Feedback Negativo (👎)

```
Usuario da 👎 a una respuesta
          │
          ▼
┌─────────────────────────────────────────────────┐
│ FeedbackService.SubmitFeedbackAsync()           │
│ isHelpful = false                               │
│                                                 │
│ 1. Crea ChatFeedback                            │
│ 2. AnalyzeAndSuggestKeywordsAsync()             │
│    - Extrae keywords de la query                │
│    - Busca qué documentos deberían coincidir    │
│    - Sugiere keywords faltantes                 │
│ 3. TrackFailurePatternAsync()                   │
│    - Agrupa por patrón de keywords              │
│    - Si patrón llega a 5 fallos → Alerta        │
│ 4. TryAutoEnrichKeywordsAsync()                 │
│    - Si keyword tiene 3+ ocurrencias            │
│    - Añade automáticamente al documento         │
│ 5. Guarda en Azure Blob                         │
└─────────────────────────────────────────────────┘
```

---

## 9. APIs y Endpoints

### 9.1 Diagnóstico

| Endpoint | Método | Descripción |
|----------|--------|-------------|
| `/api/confluence-status` | GET | Estado de configuración de Confluence |
| `/api/confluence-sync` | GET | Forzar sincronización de Confluence |
| `/api/confluence-sync/{spaceKey}` | GET | Sincronizar un espacio específico |
| `/api/confluence-search?q={query}` | GET | Búsqueda de prueba en Confluence |

### 9.2 Ejemplo de Respuesta

```json
// GET /api/confluence-status
{
  "isConfigured": true,
  "pageCount": 245,
  "config": {
    "baseUrl": "https://antolin.atlassian.net",
    "email": "bot@antolin.com",
    "spaceKeys": "ITOPS,HELPDESK",
    "apiTokenBase64": "SET (64 chars)"
  }
}
```

---

## 10. Configuración

### 10.1 appsettings.json

```json
{
  "AzureStorage": {
    "ConnectionString": "SET_IN_AZURE_APP_SERVICE_CONFIGURATION",
    "ContainerName": "scripts",
    "KnowledgeContainerName": "knowledge"
  },
  "Confluence": {
    "BaseUrl": "https://antolin.atlassian.net",
    "Email": "bot@antolin.com",
    "ApiTokenBase64": "BASE64_ENCODED_TOKEN",
    "SpaceKeys": "ITOPS,HELPDESK"
  },
  "Authorization": {
    "AdminEmails": ["admin@antolin.com"]
  }
}
```

### 10.2 Variables de Entorno (Azure App Service)

| Variable | Descripción |
|----------|-------------|
| `AZURE_OPENAI_ENDPOINT` | Endpoint de Azure OpenAI |
| `AZURE_OPENAI_API_KEY` | API Key de Azure OpenAI |
| `AZURE_OPENAI_GPT_NAME` | Modelo de embeddings (text-embedding-3-small) |
| `AZURE_OPENAI_CHAT_NAME` | Modelo de chat (gpt-4o-mini) |
| `AzureStorage__ConnectionString` | Connection string de Azure Blob Storage |

### 10.3 Contenedores de Azure Blob Storage

| Contenedor | Propósito |
|------------|-----------|
| `agent-context` | Documentos de contexto, feedback, SAP data |
| `confluence-cache` | Caché de páginas de Confluence |
| `scripts` | Scripts de PowerShell |
| `knowledge` | Knowledge Base articles |

---

## Apéndice A: Diagrama de Clases Simplificado

```
┌─────────────────────┐
│ IKnowledgeAgentService │
└──────────┬──────────┘
           │ implements
           ▼
┌─────────────────────┐         ┌─────────────────────┐
│ AgentRouterService  │────────▶│ KnowledgeAgentService│
│                     │ uses    │                     │
│ + AskAsync()        │         │ + AskAsync()        │
│ + DetermineAgentAsync()│      │ + AskWithSpecialistAsync()│
│ + GetSapSpecialistContext()│  │ + SearchContextParallel()│
└──────────┬──────────┘         └──────────┬──────────┘
           │                               │
           │ uses                          │ uses
           ▼                               ▼
┌─────────────────────┐         ┌─────────────────────┐
│ SapLookupService    │         │ ContextSearchService │
│                     │         │                     │
│ + GetTransaction()  │         │ + SearchAsync()     │
│ + GetPosition()     │         │                     │
│ + GetTransactionsByPosition()││                     │
└─────────────────────┘         └─────────────────────┘
                                           │ uses
                                           ▼
                                ┌─────────────────────┐
                                │ ContextStorageService│
                                │                     │
                                │ + LoadDocumentsAsync()│
                                │ + SaveDocumentsAsync()│
                                └─────────────────────┘
```

---

## Apéndice B: Checklist de Debugging

### El bot no encuentra información:

1. ✅ Verificar `/api/confluence-status` → ¿PageCount > 0?
2. ✅ Verificar `/feedback-admin` → ¿Health check OK?
3. ✅ Revisar logs de AgentRouterService → ¿Routing correcto?
4. ✅ Revisar logs de KnowledgeAgentService → ¿Resultados de búsqueda?

### El feedback no se guarda:

1. ✅ Verificar FeedbackService config → AzureStorage:ConnectionString
2. ✅ Verificar `/feedback-admin` → Banner de salud
3. ✅ Revisar container `agent-context` → ¿Existe chat-feedback.json?

### SAP no devuelve transacciones:

1. ✅ Verificar SapKnowledgeService → ¿Excel cargado?
2. ✅ Verificar SapLookupService → ¿Índices construidos?
3. ✅ Revisar logs → GetSapSpecialistContextAsync

---

**Fin de la documentación técnica**
