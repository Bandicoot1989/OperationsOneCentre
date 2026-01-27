# Feedback Loop - Fase 3: Diagnóstico Conversacional

## 📋 Resumen

Sistema de **triaje inteligente** que evalúa la claridad de las queries del usuario **antes** de ejecutar búsquedas RAG costosas. Cuando detecta queries ambiguas o demasiado vagas, solicita aclaraciones de forma contextual en lugar de ofrecer respuestas prematuras.

---

## 🎯 Objetivos

1. **Prevenir búsquedas RAG innecesarias** en queries vagas (ej: "ayuda", "problema", "error")
2. **Guiar al usuario** hacia formulaciones más específicas mediante mensajes de clarificación contextuales
3. **Priorizar soluciones probadas** del Jira Harvester y correcciones de usuario sobre documentación estática
4. **Mejorar experiencia del usuario** al no ofrecer respuestas genéricas a preguntas imprecisas

---

## 🔍 Flujo de Decisión

```
┌─────────────────────────────────────────────────────────────────────┐
│  Usuario: "help"  /  "ayuda"  /  "error"                            │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
                    ┌───────────────────────┐
                    │ IsQueryAmbiguous()?   │
                    └───────────┬───────────┘
                                │
                    ┌───────────┴────────────┐
                    │                        │
                YES │                        │ NO
                    │                        │
                    ▼                        ▼
    ┌──────────────────────────┐  ┌────────────────────────────┐
    │ GenerateClarification    │  │ Proceder con RAG Search:   │
    │ Response()               │  │ 1. KB Articles             │
    │                          │  │ 2. Context Documents       │
    │ Respuesta contextual     │  │ 3. Confluence Pages        │
    │ inmediata SIN búsqueda   │  │ 4. 🏆 Jira Solutions      │
    │                          │  │    (PRIORIDAD ALTA)        │
    └──────────────────────────┘  └────────────────────────────┘
                │                             │
                │                             ▼
                │                  ┌─────────────────────────┐
                │                  │ SystemPrompt + Context  │
                │                  │ → Azure OpenAI GPT-4o   │
                │                  └─────────────────────────┘
                │                             │
                └─────────────┬───────────────┘
                              ▼
                   ┌──────────────────────────┐
                   │ Respuesta al Usuario     │
                   └──────────────────────────┘
```

---

## 📏 Reglas de Ambigüedad

### `IsQueryAmbiguous()` - Criterios de Detección

El método evalúa la query del usuario con los siguientes criterios:

| Criterio | Umbral | Ejemplo |
|----------|--------|---------|
| **Longitud total** | < 15 caracteres | "help", "ayuda", "sap error" |
| **Cantidad de palabras** | 1-2 palabras | "error", "no funciona" |
| **Ausencia de verbos específicos** | No contiene verbos de acción | "problema red" vs "**no puedo acceder** a SAP" |

#### Keywords que NO se consideran ambiguos (suficientemente específicos):

```csharp
var specificKeywords = new[] 
{ 
    "acceso", "access", "contraseña", "password", 
    "error", "locked", "bloqueado", "reset", 
    "ticket", "jira", "confluence", "correo", "email" 
};
```

### Código Implementado

```csharp
private bool IsQueryAmbiguous(string query)
{
    if (query.Length < 15)
        return true;
    
    var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    
    if (words.Length <= 2)
    {
        var specificKeywords = new[] { "acceso", "access", "contraseña", "password", ... };
        if (!words.Any(w => specificKeywords.Contains(w.ToLowerInvariant())))
            return true;
    }
    
    return false;
}
```

---

## 💬 Mensajes de Clarificación Contextuales

### `GenerateClarificationResponse()` - Estrategia de Respuestas

El sistema **analiza keywords** en la query vaga para ofrecer mensajes de clarificación relevantes:

| Keywords Detectados | Respuesta Contextual |
|---------------------|----------------------|
| `sap`, `s/4`, `hana` | "Entiendo que tienes un problema con **SAP**. ¿Podrías especificar...?" |
| `red`, `network`, `vpn`, `conexión` | "Veo que mencionas un problema de **conectividad**. ¿Es un problema de VPN...?" |
| `acceso`, `access`, `contraseña`, `password` | "Parece un problema de **acceso**. ¿Es un problema de cuenta bloqueada...?" |
| `correo`, `email`, `outlook` | "Tienes un problema con **correo electrónico**. ¿Es sobre acceso a tu buzón...?" |
| **Ninguno (genérico)** | "¿Podrías darme más detalles sobre tu consulta? Por ejemplo..." |

### Ejemplo de Implementación

```csharp
private AgentResponse GenerateClarificationResponse(string query)
{
    var lowerQuery = query.ToLowerInvariant();
    
    if (lowerQuery.Contains("sap") || lowerQuery.Contains("s/4") || lowerQuery.Contains("hana"))
    {
        return new AgentResponse
        {
            Answer = "Entiendo que tienes un problema con **SAP**. ¿Podrías especificar:\n" +
                     "- ¿Qué sistema? (Producción, Desarrollo, QA)\n" +
                     "- ¿Qué mensaje de error ves?\n" +
                     "- ¿Qué operación intentabas realizar?\n\n" +
                     "Ejemplo: *\"No puedo acceder a SAP Producción, me dice usuario bloqueado\"*",
            ConfidenceScore = 1.0,
            Sources = new List<string>()
        };
    }
    
    // ... más condiciones para red, acceso, email ...
    
    // Fallback genérico
    return new AgentResponse { Answer = "¿Podrías darme más detalles?" };
}
```

---

## 🏆 Priorización de Fuentes en el Contexto

### Jerarquía de Conocimiento

El sistema de Feedback Loop implementa una **jerarquía de prioridad** en el contexto enviado al LLM:

```
┌─────────────────────────────────────────────────────────────┐
│ PRIORIDAD 1: 🏆 JIRA SOLUTIONS                              │
│ (Soluciones probadas de tickets reales resueltos)          │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ PRIORIDAD 2: 📝 USER CORRECTIONS                            │
│ (Correcciones enviadas por usuarios vía feedback negativo) │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ PRIORIDAD 3: 📚 CONTEXT DOCUMENTS                           │
│ (Documentación enriquecida manualmente)                     │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ PRIORIDAD 4: 🌐 CONFLUENCE PAGES                            │
│ (Wikis y procedimientos corporativos)                       │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ PRIORIDAD 5: 📖 KNOWLEDGE BASE ARTICLES                     │
│ (Base de conocimientos estática)                            │
└─────────────────────────────────────────────────────────────┘
```

### Implementación: Prepend con Header Visual

Las **Jira Solutions** se anteponen al contexto con un header destacado:

```csharp
if (!string.IsNullOrWhiteSpace(jiraSolutionsContext))
{
    var prioritizedContext = new StringBuilder();
    prioritizedContext.AppendLine("=== 🏆 PROVEN SOLUTIONS FROM SIMILAR TICKETS ===");
    prioritizedContext.AppendLine("PRIORITY: These are VALIDATED solutions from real resolved incidents.");
    prioritizedContext.AppendLine("Use these FIRST before other documentation when applicable.");
    prioritizedContext.AppendLine();
    prioritizedContext.AppendLine(jiraSolutionsContext);
    prioritizedContext.AppendLine();
    prioritizedContext.Append(context); // Resto de fuentes después
    context = prioritizedContext.ToString();
}
```

### ¿Por qué el orden importa?

Los LLMs (especialmente GPT-4) tienen **primacy bias**: Prestan más atención a la información que aparece **al inicio** del contexto. Por eso:

1. **Jira Solutions** se colocan **antes** que la documentación estática
2. **User Corrections** también se priorizan visualmente con headers
3. La documentación estática (KB, Confluence) aparece **después**

---

## 🧪 Instrucciones de Prueba

### Escenarios de Triaje (Ambigüedad Detectada)

Estas queries **deben disparar** el mensaje de clarificación **SIN búsqueda RAG**:

| Input | Razón | Respuesta Esperada |
|-------|-------|-------------------|
| `"ayuda"` | 1 palabra, 5 caracteres | Mensaje genérico de clarificación |
| `"help"` | 1 palabra, 4 caracteres | Mensaje genérico de clarificación |
| `"problema"` | 1 palabra, 8 caracteres | Mensaje genérico de clarificación |
| `"error sap"` | 2 palabras, 9 caracteres | Mensaje contextual sobre **SAP** |
| `"red"` | 1 palabra, 3 caracteres | Mensaje contextual sobre **conectividad** |
| `"no funciona"` | 2 palabras, no específicas | Mensaje genérico de clarificación |

### Escenarios de Búsqueda RAG (Query Clara)

Estas queries **deben proceder** con la búsqueda RAG normal:

| Input | Razón | Fuentes Consultadas |
|-------|-------|---------------------|
| `"No puedo acceder a SAP producción"` | >3 palabras, verbo específico | KB + Confluence + **🏆 Jira Solutions** |
| `"¿Cómo reseteo mi contraseña de Active Directory?"` | Query completa con verbo | KB + Confluence + Context Docs |
| `"Error al subir archivos a SharePoint sitio RRHH"` | Detalles específicos | Confluence + Jira Solutions |
| `"Mi usuario está bloqueado en CyberArk"` | Sistema específico + problema | Jira Solutions + KB |

### Prueba Manual: Caso de Uso Completo

#### 1️⃣ Usuario envía query vaga:

```
Usuario: "error"
```

**Respuesta esperada:**
```
¿Podrías darme más detalles sobre tu consulta? Por ejemplo:
- ¿Qué sistema estás usando? (SAP, Outlook, SharePoint, etc.)
- ¿Qué acción estabas realizando?
- ¿Qué mensaje de error exacto ves?

Ejemplo: "No puedo acceder a SAP Producción, me dice usuario bloqueado"
```

#### 2️⃣ Usuario reformula con más detalle:

```
Usuario: "No puedo acceder a SAP producción me dice usuario bloqueado"
```

**Flujo esperado:**
1. ✅ `IsQueryAmbiguous()` → `false` (>15 caracteres, verbo implícito)
2. 🔍 Búsqueda RAG en KB, Context Docs, Confluence
3. 🏆 **Jira Solutions prioritized** - Si existe un ticket similar resuelto, aparece PRIMERO
4. 🤖 GPT-4o genera respuesta con:
   - Solución del Jira ticket similar (si existe)
   - Procedimiento de desbloqueo desde KB
   - Contacto de soporte desde Confluence

#### 3️⃣ Usuario da feedback:

- **👍 Positivo**: Solución se almacena en `successful-responses.json`
- **👎 Negativo + Corrección**: Se crea un `ContextDocument` con la corrección del usuario, que tendrá **PRIORIDAD 2** en futuras búsquedas

---

## 📊 Métricas de Éxito

### KPIs del Sistema de Triaje

| Métrica | Descripción | Objetivo |
|---------|-------------|----------|
| **Tasa de Clarificación** | % de queries que disparan triaje | 10-15% (no saturar) |
| **Tasa de Reformulación** | % de usuarios que reformulan después del triaje | >80% |
| **Reducción de Búsquedas RAG** | Búsquedas evitadas por triaje | +20% reducción de costos |
| **Uso de Jira Solutions** | % de respuestas que usan soluciones del harvester | >40% en queries de SAP/Jira |
| **Feedback Positivo** | % de 👍 después de respuestas con Jira Solutions | >70% |

### Monitoreo en Logs

Buscar en los logs de Azure App Service:

```
"Query ambiguous, requesting clarification"
"Specialist agent: Added Jira solutions context with HIGH PRIORITY"
"Added Jira solutions context with HIGH PRIORITY"
```

---

## 🔧 Configuración y Ajustes

### Ajustar Umbral de Ambigüedad

Si el sistema dispara **demasiadas** clarificaciones:

```csharp
// En IsQueryAmbiguous()
if (query.Length < 15)  // Cambiar a < 10 para ser menos restrictivo
    return true;
```

### Personalizar Mensajes de Clarificación

Editar `GenerateClarificationResponse()` en `KnowledgeAgentService.cs` para añadir más contextos:

```csharp
if (lowerQuery.Contains("sharepoint") || lowerQuery.Contains("onedrive"))
{
    return new AgentResponse
    {
        Answer = "Tienes un problema con **SharePoint/OneDrive**. ¿Podrías especificar:\n" +
                 "- ¿Es un problema de acceso o de permisos?\n" +
                 "- ¿Qué sitio o carpeta?\n" +
                 "- ¿Intentas subir, descargar o compartir archivos?\n\n" +
                 "Ejemplo: *\"No puedo subir archivos al sitio de RRHH en SharePoint\"*",
        ConfidenceScore = 1.0,
        Sources = new List<string>()
    };
}
```

### Deshabilitar Triaje (Solo para Debugging)

Comentar la llamada en `AskAsync()`:

```csharp
// if (IsQueryAmbiguous(query))
// {
//     return GenerateClarificationResponse(query);
// }
```

---

## 🚀 Próximos Pasos

### Mejoras Futuras

1. **Machine Learning para Detección de Ambigüedad**  
   Entrenar un modelo clasificador (Binary: Ambiguo / No Ambiguo) con el historial de queries.

2. **Feedback Loop en las Clarificaciones**  
   Permitir que el usuario marque las clarificaciones como útiles o molestas.

3. **Historial de Conversación**  
   Considerar el contexto de mensajes previos antes de pedir clarificación.

4. **A/B Testing**  
   Medir si el triaje realmente mejora la satisfacción del usuario vs responder siempre.

---

## 📚 Referencias

- [FEEDBACK_LOOP_BACKEND.md](./FEEDBACK_LOOP_BACKEND.md) - Arquitectura de almacenamiento
- [FEEDBACK_LOOP_FRONTEND.md](./FEEDBACK_LOOP_FRONTEND.md) - UI de feedback con correcciones
- [JIRA_SOLUTION_HARVESTER.md](./JIRA_SOLUTION_HARVESTER.md) - Extracción de soluciones probadas
- [BOT_SEARCH_FLOW.md](./BOT_SEARCH_FLOW.md) - Flujo completo de búsqueda RAG

---

**Última actualización:** 27 de enero de 2026  
**Versión:** 1.0  
**Autor:** Senior Software Architect Team
