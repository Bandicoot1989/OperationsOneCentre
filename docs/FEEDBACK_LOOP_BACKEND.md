# Feedback Loop - Backend Implementation

## 📋 Resumen

Sistema de feedback implementado para capturar correcciones de usuario y enriquecer automáticamente la base de conocimientos mediante Azure Blob Storage.

---

## 🏗️ Arquitectura de Almacenamiento

### Estrategia: **Single JSON File with Atomic Updates**

**Decisión**: Usamos un **único archivo JSON** (`chat-feedback.json`) con actualizaciones atómicas mediante el patrón "descarga → modificar → sobrescribir".

### ¿Por qué esta estrategia?

#### ✅ Ventajas:
1. **Simplicidad**: Un solo archivo JSON es fácil de gestionar y respaldar
2. **Atomicidad en Azure Blob**: `UploadAsync(overwrite: true)` reemplaza el blob completo, evitando escrituras parciales corruptas
3. **Búsqueda eficiente**: Todo el historial está en memoria después de `InitializeAsync()`
4. **Costo reducido**: Menos transacciones de blob = menor costo
5. **Compatible con la arquitectura existente**: Otros servicios (`KnowledgeStorageService`, `ContextStorageService`) usan el mismo patrón

#### ⚠️ Consideraciones de Concurrencia:
- **Escenario actual**: Blazor Server con usuarios limitados (operaciones IT internas)
- **Lock en memoria**: `SemaphoreSlim _initLock` previene condiciones de carrera en el mismo proceso
- **Conflictos entre instancias**: En multi-instance Azure App Service, existe un pequeño riesgo de "last-write-wins"

### Alternativas descartadas:

| Estrategia | Ventaja | Desventaja | Decisión |
|------------|---------|------------|----------|
| **Archivos individuales por feedback** | Sin conflictos | Miles de archivos pequeños, alto costo de transacciones | ❌ Descartada |
| **Append Blobs** | Optimizado para append | Más complejo, requiere parsing línea por línea | ❌ Innecesaria para el volumen actual |
| **Archivos por día/mes** | Reduce tamaño | Búsqueda en múltiples archivos | ❌ Complejidad innecesaria |
| **Azure Table Storage** | Búsqueda rápida | Requiere cambiar arquitectura completa | ❌ Inconsistente con proyecto |
| **Single JSON (actual)** | Simple, consistente con proyecto | Potencial last-write-wins en multi-instance | ✅ **ELEGIDA** |

---

## 📦 Contenedores de Azure Blob Storage

| Contenedor | Archivo | Propósito |
|------------|---------|-----------|
| `agent-context` | `chat-feedback.json` | Todos los feedbacks (positivos + negativos) |
| `agent-context` | `successful-responses.json` | Respuestas cacheadas (👍) para few-shot learning |
| `agent-context` | `failure-patterns.json` | Patrones de fallos detectados |
| `agent-context` | `auto-learning-log.json` | Log de enriquecimientos automáticos |
| `agent-context` | `context-documents/` | Documentos enriquecidos desde correcciones de usuario |

---

## 🔄 Flujo de Datos

### Feedback Positivo (👍):
```
Usuario → KnowledgeChat.razor → FeedbackService.SubmitFeedbackAsync()
   ↓
Cache en memoria + Guardado en chat-feedback.json
   ↓
Almacenado en successful-responses.json (cache few-shot)
```

### Feedback Negativo con Corrección (👎):
```
Usuario → KnowledgeChat.razor → FeedbackService.SubmitFeedbackWithCorrectionAsync()
   ↓
1. Crear ChatFeedback con UserCorrection
   ↓
2. EnrichContextFromCorrectionAsync():
   - Crear nuevo ContextDocument
   - Almacenar en Azure Blob (ContextStorageService)
   - Generar embeddings (búsqueda semántica)
   ↓
3. Guardar feedback en chat-feedback.json
   ↓
4. Refrescar ContextSearchService (nuevo documento indexado)
```

---

## 🛠️ Componentes Implementados

### 1. Modelo de Datos (`Models/ChatFeedback.cs`)

```csharp
public class ChatFeedback
{
    public string Id { get; set; }
    public string Query { get; set; }
    public string Response { get; set; }
    public bool IsHelpful { get; set; }
    
    // ⭐ NUEVOS CAMPOS:
    public string? UserCorrection { get; set; }          // Corrección del usuario
    public List<string> SourcesUsed { get; set; }        // KB-001, Confluence:12345, MT-67890
    
    // Metadatos existentes:
    public string AgentType { get; set; }
    public double BestSearchScore { get; set; }
    public bool WasLowConfidence { get; set; }
    public List<string> ExtractedKeywords { get; set; }
    public DateTime Timestamp { get; set; }
    // ... (otros campos)
}
```

### 2. Interfaz (`Interfaces/IFeedbackService.cs`)

Define el contrato completo del servicio:

```csharp
public interface IFeedbackService
{
    Task InitializeAsync();
    Task<ChatFeedback> SubmitFeedbackAsync(...);
    
    // ⭐ NUEVO MÉTODO:
    Task<ChatFeedback> SubmitFeedbackWithCorrectionAsync(
        string query,
        string response,
        string userCorrection,      // Texto de corrección
        List<string> sourcesUsed,   // Fuentes que se usaron
        string? userId,
        string agentType,
        double bestScore,
        bool wasLowConfidence);
    
    // Métodos de consulta y gestión...
}
```

### 3. Servicio (`Services/FeedbackService.cs`)

**Método clave implementado**:

```csharp
public async Task<ChatFeedback> SubmitFeedbackWithCorrectionAsync(...)
{
    // 1. Crear feedback con corrección
    var feedback = new ChatFeedback { 
        UserCorrection = userCorrection,
        SourcesUsed = sourcesUsed,
        IsHelpful = false
    };
    
    // 2. CRÍTICO: Enriquecer contexto
    await EnrichContextFromCorrectionAsync(feedback);
    
    // 3. Guardar feedback
    _feedbackCache.Add(feedback);
    await SaveFeedbackAsync();
    
    return feedback;
}

// Método privado que hace la magia:
private async Task EnrichContextFromCorrectionAsync(ChatFeedback feedback)
{
    // Crear documento de contexto
    var contextDoc = new ContextDocument {
        Name = $"User Correction - {feedback.Query.Substring(0, 50)}",
        Description = feedback.UserCorrection,  // ← La corrección del usuario
        Keywords = string.Join(", ", feedback.ExtractedKeywords),
        DocumentType = "UserFeedback",          // ← Nuevo tipo
        Category = feedback.AgentType
    };
    
    // Almacenar y generar embeddings
    await _contextStorage.AddDocumentAsync(contextDoc);
    
    // Refrescar búsqueda semántica
    await _contextService.InitializeAsync();
    
    _logger.LogInformation("✅ Context enriched from user correction");
}
```

### 4. Registro DI (`Extensions/DependencyInjection.cs`)

```csharp
public static IServiceCollection AddFeedbackServices(this IServiceCollection services)
{
    // Registro dual: concreto + interfaz
    services.AddSingleton<FeedbackService>();
    services.AddSingleton<IFeedbackService>(sp => sp.GetRequiredService<FeedbackService>());
    
    return services;
}
```

**En `Program.cs`** (ya existente):
```csharp
builder.Services.AddFeedbackServices();    // ← Ya está configurado
```

---

## 🔒 Manejo de Errores y Logging

### Try-Catch Estratégico:
```csharp
try
{
    await EnrichContextFromCorrectionAsync(feedback);
}
catch (Exception ex)
{
    _logger.LogError(ex, "❌ Error enriching context from user correction");
    throw; // Re-lanzar para que el usuario vea el error
}
```

### Logging Detallado:
- `LogInformation` → Operaciones exitosas (✅)
- `LogWarning` → Situaciones recuperables (⚠️)
- `LogError` → Errores críticos (❌)

**Ejemplos**:
```csharp
_logger.LogInformation("📝 Enriching context from user correction...");
_logger.LogInformation("✅ Context document created: '{DocName}'", contextDoc.Name);
_logger.LogError(ex, "❌ Error processing feedback with correction");
```

---

## 🧪 Estrategia de Almacenamiento: Detalles Técnicos

### Operación de Guardado:

```csharp
private async Task SaveFeedbackAsync()
{
    if (_containerClient == null) return;
    
    try
    {
        var blobClient = _containerClient.GetBlobClient(FeedbackBlobName);
        
        // Serializar todo el feedback en memoria
        var json = JsonSerializer.Serialize(_feedbackCache, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        
        // Sobrescribir blob completo (operación atómica en Azure)
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await blobClient.UploadAsync(stream, overwrite: true);
        
        _logger.LogDebug("Feedback saved to blob: {Count} entries", _feedbackCache.Count);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to save feedback to blob storage");
        throw;
    }
}
```

### ¿Qué pasa con concurrencia en multi-instance?

**Escenario**: 2 instancias de Azure App Service, ambas reciben feedback al mismo tiempo.

1. **Instance A** descarga `chat-feedback.json` (tiene 100 entradas)
2. **Instance B** descarga `chat-feedback.json` (tiene 100 entradas)
3. **Instance A** agrega entrada #101 y sube el archivo
4. **Instance B** agrega entrada #102 y sube el archivo → **SOBRESCRIBE** el archivo de Instance A

**Resultado**: Se pierde la entrada #101 (last-write-wins).

**¿Es un problema?**:
- ❌ En sistemas de alta concurrencia (millones de usuarios)
- ✅ En este proyecto (operaciones IT internas, ~10-50 usuarios)
- **Probabilidad**: Extremadamente baja (requiere feedback simultáneo en el mismo segundo)

**Mitigación si escala**:
- Usar Azure Table Storage con PartitionKey/RowKey
- Implementar Append Blobs con parsing línea por línea
- Implementar retry con exponential backoff
- Usar Azure Queue Storage como buffer

---

## 📊 Métricas de Almacenamiento

### Tamaño estimado por entrada:
- Feedback mínimo: ~500 bytes
- Feedback con corrección: ~1-2 KB

### Proyección:
- **100 feedback/día** → ~150 KB/día → 4.5 MB/mes
- **Azure Blob Storage**: $0.018 per GB/mes → **Costo insignificante**

### Operaciones:
- **1 UploadAsync** por feedback → ~100 transacciones/día
- **Azure Blob**: Primeras 10,000 transacciones gratis → **Sin costo adicional**

---

## 🚀 Próximos Pasos (Frontend - Fase 2)

1. Modificar `KnowledgeChat.razor` para capturar `UserCorrection`
2. Mostrar textarea cuando el usuario haga clic en 👎
3. Enviar corrección usando `IFeedbackService.SubmitFeedbackWithCorrectionAsync()`
4. Capturar `SourcesUsed` desde `AgentResponse.RelevantArticles`

---

## 📝 Notas Técnicas

### ¿Por qué no usar Entity Framework Core?
- **Consistencia**: Todo el proyecto usa Azure Blob Storage
- **Simplicidad**: No requiere migrations, solo JSON
- **Costo**: Sin necesidad de Azure SQL Database

### ¿Por qué Singleton en DI?
- **Cache en memoria**: Evita cargar JSON en cada request
- **Performance**: Búsqueda instantánea en listas en memoria
- **Consistencia**: Todos los servicios de storage son Singleton

### ¿Cómo se regeneran los embeddings?
- `ContextStorageService.AddDocumentAsync()` llama internamente a `EmbeddingClient`
- El nuevo documento se indexa automáticamente en la búsqueda semántica
- `ContextSearchService.InitializeAsync()` recarga todos los documentos y embeddings

---

**Última actualización**: 27 Enero 2026  
**Estado**: ✅ Backend implementado y listo para Frontend (Fase 2)
