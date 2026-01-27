# Feedback Loop - Frontend Implementation

## 📋 Resumen

Sistema de interfaz de usuario implementado para capturar feedback positivo/negativo y correcciones de usuario, conectado al backend de Azure Blob Storage.

---

## 🎨 UI/UX Design

### Filosofía de Diseño
- **Material Design 3** con dark mode
- **Animaciones suaves** para transiciones (300ms cubic-bezier)
- **Estados visuales claros** (hover, active, disabled)
- **Accesibilidad** con tooltips y contraste adecuado

### Paleta de Colores (Design System)
```css
--primary: #00C897          /* Antolin Green - Positive feedback */
--primary-dark: #00A87E     /* Hover state */
--error: #EF5350            /* Negative feedback */
--bg-surface: #1E1F21       /* Panels, cards */
--bg-input: #2D2E30         /* Inputs, textareas */
--border: #3F4042           /* Borders */
--text-primary: rgba(255, 255, 255, 0.95)
--text-secondary: rgba(255, 255, 255, 0.70)
--text-hint: rgba(255, 255, 255, 0.50)
```

---

## 🏗️ Arquitectura Frontend

### Componentes Modificados

#### 1. **AgentResponse.cs** (Models)
```csharp
public class AgentResponse
{
    // ... existing properties ...
    
    /// <summary>
    /// NEW: List of source IDs used to generate this response
    /// Format: "KB-001", "Confluence:Page Title", "MT-12345"
    /// </summary>
    public List<string> UsedSources { get; set; } = new();
}
```

**Propósito**: Tracking de fuentes para feedback detallado.

---

#### 2. **KnowledgeAgentService.cs** (Services)

**Modificación Principal**:
```csharp
// Build UsedSources list for feedback tracking
var usedSources = new List<string>();
usedSources.AddRange(articleRefs.Select(a => a.KBNumber));
usedSources.AddRange(confluenceRefs.Select(c => $"Confluence:{c.Title}"));
usedSources.AddRange(contextDocs.Take(3).Select(d => 
    !string.IsNullOrWhiteSpace(d.Link) && d.Link.Contains("atlassian") ? 
        Regex.Match(d.Link, @"(MT|MTT|IT|HELP)-\d+")?.Value ?? d.Name 
        : d.Name));

return new AgentResponse
{
    // ... other properties ...
    UsedSources = usedSources.Distinct().ToList()
};
```

**Lógica**:
1. Extrae KB numbers de artículos relevantes
2. Extrae títulos de páginas Confluence (prefijo "Confluence:")
3. Extrae ticket IDs de Context Documents (regex para MT-12345)
4. Elimina duplicados con `Distinct()`

---

#### 3. **KnowledgeChat.razor** (Components)

##### 3.1. Inyección de Dependencias
```razor
@inject IFeedbackService FeedbackService  // Cambió de FeedbackService a IFeedbackService
```

##### 3.2. Clase ChatMessage (Actualizada)
```csharp
private class ChatMessage
{
    // Existing
    public string Text { get; set; }
    public bool IsUser { get; set; }
    public DateTime Timestamp { get; set; }
    public List<ArticleReference>? References { get; set; }
    
    // ⭐ NEW: Source tracking
    public List<string> Sources { get; set; } = new();
    
    // ⭐ UPDATED: Feedback state
    public bool HasFeedback { get; set; } = false;          // Reemplaza FeedbackSubmitted
    public bool? IsPositive { get; set; }                   // Reemplaza FeedbackIsPositive
    public bool ShowCorrectionInput { get; set; } = false;  // NEW
    public string CorrectionText { get; set; } = string.Empty; // NEW
    
    // Metadata (existing)
    public string? OriginalQuery { get; set; }
    public string AgentType { get; set; } = "General";
    public double BestSearchScore { get; set; }
    public bool WasLowConfidence { get; set; }
}
```

##### 3.3. Captura de Sources
```csharp
// En SendMessage(), al agregar respuesta del bot:
messages.Add(new ChatMessage
{
    // ... existing properties ...
    Sources = response.UsedSources,  // ⭐ NEW
});
```

##### 3.4. Métodos de Feedback (Nuevos/Actualizados)

**HandlePositiveFeedback** (👍):
```csharp
private async Task HandlePositiveFeedback(ChatMessage msg)
{
    if (msg.HasFeedback) return;
    
    await FeedbackService.SubmitFeedbackAsync(
        query: msg.OriginalQuery ?? "",
        response: msg.Text,
        isHelpful: true,
        agentType: msg.AgentType,
        bestScore: msg.BestSearchScore,
        wasLowConfidence: msg.WasLowConfidence,
        userId: currentUser?.Email
    );
    
    msg.HasFeedback = true;
    msg.IsPositive = true;
    StateHasChanged();
}
```

**HandleNegativeFeedback** (👎):
```csharp
private void HandleNegativeFeedback(ChatMessage msg)
{
    if (msg.HasFeedback) return;
    
    // Solo muestra el panel de corrección
    msg.ShowCorrectionInput = true;
    StateHasChanged();
}
```

**SubmitCorrection**:
```csharp
private async Task SubmitCorrection(ChatMessage msg)
{
    if (string.IsNullOrWhiteSpace(msg.CorrectionText)) return;
    
    await FeedbackService.SubmitFeedbackWithCorrectionAsync(
        query: msg.OriginalQuery ?? "",
        response: msg.Text,
        userCorrection: msg.CorrectionText,
        sourcesUsed: msg.Sources ?? new List<string>(),  // ⭐ Usa Sources capturadas
        userId: currentUser?.Email,
        agentType: msg.AgentType,
        bestScore: msg.BestSearchScore,
        wasLowConfidence: msg.WasLowConfidence
    );
    
    msg.HasFeedback = true;
    msg.IsPositive = false;
    msg.ShowCorrectionInput = false;
    StateHasChanged();
}
```

**CancelCorrection**:
```csharp
private void CancelCorrection(ChatMessage msg)
{
    msg.ShowCorrectionInput = false;
    msg.CorrectionText = string.Empty;
    StateHasChanged();
}
```

---

## 🎭 Estados UI del Feedback

### Estado 1: Sin Feedback
```
┌────────────────────────────────────┐
│ [Respuesta del bot...]             │
│                                    │
│ 📚 Sources: KB-045, MT-12345       │
│                                    │
│ [👍] [👎]  ← Botones sutiles      │
│         (opacity 0.5, hover → 1.0)│
└────────────────────────────────────┘
```

### Estado 2A: Feedback Positivo
```
┌────────────────────────────────────┐
│ [Respuesta del bot...]             │
│                                    │
│ ✅ Gracias por tu feedback         │
│    ← Color: #00C897                │
└────────────────────────────────────┘
```

### Estado 2B: Feedback Negativo (Panel Abierto)
```
┌────────────────────────────────────┐
│ [Respuesta del bot...]             │
│                                    │
│ ┌──────────────────────────────┐  │
│ │ Por favor, describe la       │  │
│ │ respuesta correcta...        │  │
│ │                              │  │
│ │ ┌────────────────────────┐  │  │
│ │ │ [Textarea]             │  │  │
│ │ │                        │  │  │
│ │ └────────────────────────┘  │  │
│ │                              │  │
│ │ [✉ Enviar] [Cancelar]       │  │
│ └──────────────────────────────┘  │
└────────────────────────────────────┘
```

### Estado 3: Corrección Enviada
```
┌────────────────────────────────────┐
│ [Respuesta del bot...]             │
│                                    │
│ ❌ Gracias por ayudarnos a mejorar │
│    ← Color: #EF5350                │
└────────────────────────────────────┘
```

---

## 🎨 Estilos CSS Implementados

### Botones de Feedback
```css
.feedback-buttons {
    display: flex;
    gap: 0.5rem;
    opacity: 0.5;  /* Sutiles por defecto */
    transition: opacity 200ms ease;
}

/* Aparecen al hover del mensaje */
.gemini-message.assistant:hover .feedback-buttons {
    opacity: 1;
}

.feedback-btn {
    background: transparent;
    border: 1px solid #3F4042;
    width: 34px;
    height: 34px;
    border-radius: 50%;
    cursor: pointer;
    color: rgba(255, 255, 255, 0.5);
    transition: all 200ms cubic-bezier(0.4, 0, 0.2, 1);
}

.feedback-btn:hover {
    border-color: #00C897;
    color: #00C897;
    background: rgba(0, 200, 151, 0.1);
    transform: scale(1.1);  /* Micro-interacción */
}
```

### Estado de Feedback Enviado
```css
.feedback-submitted {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    background: rgba(255, 255, 255, 0.05);
    border-radius: 16px;
    padding: 0.5rem 0.75rem;
}

.feedback-icon.positive {
    background: rgba(0, 200, 151, 0.2);
    color: #00C897;
}

.feedback-icon.negative {
    background: rgba(239, 83, 80, 0.2);
    color: #EF5350;
}
```

### Panel de Corrección
```css
.correction-panel {
    margin-top: 1rem;
    padding: 1rem;
    background: #1E1F21;
    border: 1px solid #3F4042;
    border-radius: 12px;
    animation: slideDown 300ms cubic-bezier(0.4, 0, 0.2, 1);
}

@keyframes slideDown {
    from {
        opacity: 0;
        transform: translateY(-10px);
    }
    to {
        opacity: 1;
        transform: translateY(0);
    }
}

.correction-textarea {
    width: 100%;
    min-height: 100px;
    background: #2D2E30;
    border: 1px solid #3F4042;
    border-radius: 8px;
    padding: 0.75rem;
    color: rgba(255, 255, 255, 0.95);
    resize: vertical;
    transition: all 200ms ease;
}

.correction-textarea:focus {
    border-color: #00C897;
    box-shadow: 0 0 0 2px rgba(0, 200, 151, 0.2);
}
```

### Botón de Enviar Corrección
```css
.btn-submit-correction {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    background: #00C897;  /* Antolin Green */
    color: #121212;       /* Dark text on green */
    border: none;
    padding: 0.625rem 1.25rem;
    border-radius: 24px;
    font-weight: 500;
    cursor: pointer;
    transition: all 200ms cubic-bezier(0.4, 0, 0.2, 1);
}

.btn-submit-correction:hover:not(:disabled) {
    background: #00A87E;
    transform: translateY(-2px);
    box-shadow: 0 4px 12px rgba(0, 200, 151, 0.3);
}

.btn-submit-correction:disabled {
    opacity: 0.5;
    cursor: not-allowed;
}
```

---

## 🔄 Flujo Completo de Datos

```
┌──────────────────────────────────────────────────────────────────┐
│                   USER INTERACTION                                │
└──────────────────┬───────────────────────────────────────────────┘
                   │
                   ▼
   Usuario hace pregunta: "¿Cómo creo usuario SAP?"
                   │
                   ▼
┌──────────────────────────────────────────────────────────────────┐
│             KnowledgeAgentService.AskAsync()                      │
├───────────────────────────────────────────────────────────────────┤
│ 1. Busca en KB, Confluence, Context Documents                    │
│ 2. Construye respuesta con GPT-4o                                │
│ 3. Captura fuentes usadas:                                       │
│    - KB-045 (SAP User Management)                                │
│    - Confluence:SAP Administration Guide                         │
│    - MT-12345 (Similar ticket)                                   │
└──────────────────┬───────────────────────────────────────────────┘
                   │
                   ▼ AgentResponse { UsedSources = ["KB-045", "Confluence:...", "MT-12345"] }
                   │
                   ▼
┌──────────────────────────────────────────────────────────────────┐
│              KnowledgeChat.razor (SendMessage)                    │
├───────────────────────────────────────────────────────────────────┤
│ messages.Add(new ChatMessage {                                   │
│     Text = response.Answer,                                      │
│     Sources = response.UsedSources,  ← Captura aquí             │
│     OriginalQuery = "¿Cómo creo usuario SAP?",                  │
│     AgentType = "SAP"                                            │
│ });                                                              │
└──────────────────┬───────────────────────────────────────────────┘
                   │
                   ▼ Mensaje renderizado con botones 👍 👎
                   │
         ┌─────────┴─────────┐
         │                   │
         ▼ 👍                ▼ 👎
┌──────────────────┐  ┌──────────────────────────────────────────┐
│ Feedback Positivo│  │      Feedback Negativo                    │
├──────────────────┤  ├───────────────────────────────────────────┤
│ FeedbackService  │  │ 1. Muestra panel con textarea             │
│ .SubmitFeedback  │  │ 2. Usuario escribe corrección:            │
│ Async(...)       │  │    "Se usa transacción SU01..."          │
│                  │  │ 3. Click "Enviar Corrección"              │
│ → Guarda en      │  │                                           │
│   chat-feedback  │  │ FeedbackService                           │
│   .json          │  │ .SubmitFeedbackWithCorrectionAsync(       │
│                  │  │   userCorrection = "Se usa SU01...",      │
│                  │  │   sourcesUsed = ["KB-045", "MT-12345"]    │
│                  │  │ )                                         │
│                  │  │                                           │
│                  │  │ → EnrichContextFromCorrectionAsync():     │
│                  │  │   - Crea ContextDocument                  │
│                  │  │   - Genera embeddings                     │
│                  │  │   - Almacena en Azure Blob                │
└──────────────────┘  └───────────────────────────────────────────┘
```

---

## 📊 Tracking de Fuentes: Ejemplos

### Ejemplo 1: Query sobre SAP
```json
{
  "Query": "¿Cómo creo un usuario en SAP?",
  "Response": "Para crear un usuario...",
  "UsedSources": [
    "KB-045",
    "Confluence:SAP Administration Guide",
    "MT-12345"
  ],
  "UserCorrection": "Se usa la transacción SU01, no SU10",
  "Timestamp": "2026-01-27T14:30:00Z"
}
```

### Ejemplo 2: Query sobre Zscaler
```json
{
  "Query": "How do I configure Zscaler VPN?",
  "Response": "Zscaler setup steps...",
  "UsedSources": [
    "KB-089",
    "Confluence:Network Access Guide",
    "Zscaler Installation Manual"
  ],
  "UserCorrection": null,
  "IsHelpful": true,
  "Timestamp": "2026-01-27T15:45:00Z"
}
```

---

## 🧪 Testing Checklist

### Funcional
- [ ] Botones 👍/👎 aparecen solo en mensajes del bot
- [ ] Click en 👍 → Muestra confirmación verde
- [ ] Click en 👎 → Abre panel de corrección
- [ ] Textarea acepta texto multilinea
- [ ] Botón "Enviar" deshabilitado si textarea vacío
- [ ] Botón "Cancelar" cierra panel sin enviar
- [ ] Feedback se envía correctamente al backend
- [ ] No se puede votar dos veces en el mismo mensaje

### Visual (Design System)
- [ ] Colores correctos (#00C897 positivo, #EF5350 negativo)
- [ ] Animaciones suaves (300ms)
- [ ] Hover states funcionan
- [ ] Responsive en móvil
- [ ] Contraste adecuado (WCAG AA)

### Backend
- [ ] `UsedSources` se captura correctamente
- [ ] Corrección crea `ContextDocument`
- [ ] Embeddings se generan automáticamente
- [ ] `ContextSearchService` se refresca
- [ ] Feedback persiste en Azure Blob

---

## 🚀 Próximos Pasos (Fase 3 - Opcional)

1. **Admin Panel para Feedback**:
   - Vista de todos los feedbacks negativos
   - Botón para aplicar correcciones manualmente
   - Estadísticas de satisfacción

2. **Analytics Dashboard**:
   - Tasa de satisfacción por agente (SAP, Network, etc.)
   - Palabras clave más frecuentes en correcciones
   - Tendencias temporales

3. **Auto-Learning Avanzado**:
   - Re-entrenamiento automático de embeddings
   - Detección de patrones en correcciones
   - Alertas cuando múltiples usuarios corrigen lo mismo

---

**Última actualización**: 27 Enero 2026  
**Estado**: ✅ Frontend implementado y conectado al backend  
**Archivos modificados**:
- `Services/KnowledgeAgentService.cs` (AgentResponse + UsedSources tracking)
- `Components/KnowledgeChat.razor` (UI completa + métodos de feedback)
