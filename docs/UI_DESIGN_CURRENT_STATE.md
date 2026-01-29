# 🎨 UI Design - Estado Actual de la Aplicación

## Operations One Centre - Documentación de Diseño Visual

**Última actualización:** 29 Enero 2026  
**Versión:** 3.1 (Gemini UI Refactor)  
**Stack Tecnológico:** Blazor Server .NET 10 + CSS puro (sin frameworks)

---

## 📋 Índice

1. [Stack y Herramientas](#1-stack-y-herramientas)
2. [Sistema de Colores - Material You](#2-sistema-de-colores---material-you)
3. [Tipografía](#3-tipografía)
4. [Componentes Principales](#4-componentes-principales)
5. [Sistema de Animaciones](#5-sistema-de-animaciones)
6. [Layout y Estructura](#6-layout-y-estructura)
7. [Responsive Design](#7-responsive-design)
8. [Iconografía](#8-iconografía)
9. [Temas Visuales](#9-temas-visuales)

---

## 1. Stack y Herramientas

### Framework y Tecnologías

| Tecnología | Versión | Uso |
|------------|---------|-----|
| **Blazor Server** | .NET 10 | Framework principal de la aplicación |
| **CSS Puro** | CSS3 | Estilos personalizados sin frameworks externos |
| **SignalR** | Integrado en Blazor | Comunicación en tiempo real (WebSockets) |
| **Material Symbols** | Google Fonts | Iconografía |
| **Roboto** | Google Fonts | Tipografía principal |

### Estructura de Estilos

```
wwwroot/
├── app.css                    # Reset CSS, variables globales, estilos base
├── css/
│   └── material-design.css    # Sistema MD3 con Surface Tints
│   └── pages-material.css     # Componentes UI Material Design inspired
└── Components/
    └── KnowledgeChat.razor    # Estilos inline del chatbot Gemini-style
```

### Enfoque de Diseño

- **CSS Vanilla**: No se usan frameworks como Bootstrap, Tailwind o Material Components Web
- **Mobile-First**: Diseño responsive con enfoque mobile primero
- **Dark Mode Only**: Optimizado para modo oscuro con Surface Tints
- **Material Design 3 / Material You**: Sistema de colores con tintes del color primario
- **Performance**: CSS modular, sin dependencias externas pesadas

---

## 2. Sistema de Colores - Material You

### 🎨 Concepto: Surface Tints

Material You introduce "Surface Tints": los fondos oscuros ya no son grises neutros, sino que tienen una **mezcla sutil del color primario** (#00C897 Antolin Teal). Esto crea una apariencia más cálida y conectada con la marca.

### Source Colors (Paleta Base)

```css
/* Color Primario: Antolin Teal */
--md-source-primary: #00C897;
--md-source-primary-rgb: 0, 200, 151;

/* Color Secundario: Corporate Blue */
--md-source-secondary: #4DA8DA;
--md-source-secondary-rgb: 77, 168, 218;

/* Color Terciario: Brand Deep Blue */
--md-source-tertiary: #002A50;
```

### MD3 Surface Hierarchy (Tinted Backgrounds)

Los fondos ahora tienen un tinte verde sutil derivado del color primario:

```css
/* === SUPERFICIES CON TINTE === */

/* Fondo absoluto (más oscuro) */
--md-sys-color-surface: #0F1412;

/* Base ligeramente elevada */
--md-sys-color-surface-dim: #131916;

/* Para áreas enfatizadas */
--md-sys-color-surface-bright: #1A211E;

/* === SURFACE CONTAINERS === */

/* Elementos hundidos, fondos secundarios */
--md-sys-color-surface-container-lowest: #0B0F0D;

/* Cards por defecto */
--md-sys-color-surface-container-low: #151C19;

/* Cards estándar y componentes */
--md-sys-color-surface-container: #1A221F;

/* Cards elevadas, dropdowns, sheets */
--md-sys-color-surface-container-high: #1F2824;

/* Modales, diálogos, popovers (más elevado) */
--md-sys-color-surface-container-highest: #252E2A;
```

### MD3 Elevation System (Dark Mode)

En modo oscuro, la elevación se representa con **opacidades de blanco** sobre el fondo base, no con sombras:

```css
/* Overlays de elevación (blancos) */
--md-sys-elevation-level0: transparent;
--md-sys-elevation-level1: rgba(255, 255, 255, 0.05);
--md-sys-elevation-level2: rgba(255, 255, 255, 0.08);
--md-sys-elevation-level3: rgba(255, 255, 255, 0.11);
--md-sys-elevation-level4: rgba(255, 255, 255, 0.12);
--md-sys-elevation-level5: rgba(255, 255, 255, 0.14);

/* Overlays tintados (con primary color) */
--md-sys-elevation-tint-1: rgba(0, 200, 151, 0.05);
--md-sys-elevation-tint-2: rgba(0, 200, 151, 0.08);
--md-sys-elevation-tint-3: rgba(0, 200, 151, 0.11);
--md-sys-elevation-tint-4: rgba(0, 200, 151, 0.12);
--md-sys-elevation-tint-5: rgba(0, 200, 151, 0.14);
```

### Color Roles

```css
/* Primary */
--md-primary: #00C897;
--md-primary-hover: #33D4AC;
--md-primary-pressed: #00A87E;
--md-primary-container: rgba(0, 200, 151, 0.24);
--md-on-primary: #00201A;
--md-on-primary-container: #00C897;

/* Secondary */
--md-secondary: #4DA8DA;
--md-secondary-hover: #70B9E2;
--md-secondary-container: rgba(77, 168, 218, 0.18);
```

### Text Colors (Tinted for Harmony)

```css
/* Texto principal (con tinte verde sutil) */
--md-on-surface: #E6F2EE;
--md-text-primary: #E6F2EE;

/* Texto secundario */
--md-on-surface-variant: #B8C9C3;
--md-text-secondary: #B8C9C3;

/* Estados */
--md-text-disabled: rgba(230, 242, 238, 0.38);
--md-text-hint: rgba(230, 242, 238, 0.50);
```

### State Colors

```css
--md-error: #FFB4AB;
--md-error-container: rgba(255, 180, 171, 0.16);

--md-success: #81C995;
--md-success-container: rgba(129, 201, 149, 0.16);

--md-warning: #FFD95A;
--md-warning-container: rgba(255, 217, 90, 0.16);

--md-info: #8AB4F8;
--md-info-container: rgba(138, 180, 248, 0.16);
```

### Interactive States

```css
--md-state-hover: rgba(255, 255, 255, 0.08);
--md-state-focus: rgba(255, 255, 255, 0.12);
--md-state-pressed: rgba(255, 255, 255, 0.16);

/* Con tinte primario */
--md-state-primary-hover: rgba(0, 200, 151, 0.08);
--md-state-primary-focus: rgba(0, 200, 151, 0.12);
--md-state-primary-pressed: rgba(0, 200, 151, 0.16);
```

#### Fondos de Color con Transparencia

Usados en badges, estados y highlights:

```css
background: rgba(0, 200, 151, 0.15);   /* Primary con 15% opacidad */
background: rgba(76, 175, 80, 0.15);   /* Success con 15% opacidad */
background: rgba(255, 152, 0, 0.15);   /* Warning con 15% opacidad */
background: rgba(239, 83, 80, 0.15);   /* Error con 15% opacidad */
background: rgba(66, 165, 245, 0.15);  /* Info con 15% opacidad */
```

---

## 3. Tipografía (Enhanced Legibility)

### Familia de Fuentes

```css
--md-font-family: 'Roboto', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
--md-font-display: 'Google Sans', 'Roboto', sans-serif;
```

**Fuente CDN:**
```html
<link href="https://fonts.googleapis.com/css2?family=Roboto:wght@300;400;500;700&display=swap" rel="stylesheet">
```

### Type Scale (Material Design 3)

#### Display & Headlines

```css
/* Display (para héroes y landing) */
--md-sys-typescale-display-large: 400 3.5625rem/4rem var(--md-font-display);
--md-sys-typescale-display-medium: 400 2.8125rem/3.25rem var(--md-font-display);
--md-sys-typescale-display-small: 400 2.25rem/2.75rem var(--md-font-display);

/* Headline */
--md-sys-typescale-headline-large: 400 2rem/2.5rem var(--md-font-family);
--md-sys-typescale-headline-medium: 400 1.75rem/2.25rem var(--md-font-family);
--md-sys-typescale-headline-small: 500 1.5rem/2rem var(--md-font-family);
```

#### Titles (Con letter-spacing mejorado)

```css
/* Títulos con spacing optimizado para legibilidad */
--md-sys-typescale-title-large: 500 1.375rem/1.75rem var(--md-font-family);
--md-sys-typescale-title-medium: 500 1rem/1.5rem var(--md-font-family);
--md-sys-typescale-title-small: 500 0.875rem/1.25rem var(--md-font-family);

/* Letter Spacing para Títulos Pequeños */
--md-sys-typescale-title-small-tracking: 0.025em;
--md-sys-typescale-title-medium-tracking: 0.015em;
--md-sys-typescale-label-tracking: 0.05em;
```

#### Body (Con line-height aumentado)

```css
/* Body con mayor line-height para legibilidad */
--md-sys-typescale-body-large: 400 1rem/1.75rem var(--md-font-family);
--md-sys-typescale-body-medium: 400 0.875rem/1.5rem var(--md-font-family);
--md-sys-typescale-body-small: 400 0.75rem/1.25rem var(--md-font-family);
```

#### Labels

```css
--md-sys-typescale-label-large: 500 0.875rem/1.25rem var(--md-font-family);
--md-sys-typescale-label-medium: 500 0.75rem/1rem var(--md-font-family);
--md-sys-typescale-label-small: 500 0.6875rem/1rem var(--md-font-family);
```

### Jerarquía de Encabezados (Con letter-spacing)

```css
h1 { 
    font-size: 2.5rem; 
    font-weight: 400; 
    letter-spacing: -0.5px;
    line-height: 1.2;
}

h2 { 
    font-size: 2rem; 
    font-weight: 400;
    letter-spacing: -0.25px;
    line-height: 1.25;
}

h3 { 
    font-size: 1.5rem; 
    font-weight: 500;
    letter-spacing: 0;
    line-height: 1.3;
}

/* Títulos pequeños: mayor letter-spacing mejora legibilidad */
h4 { 
    font-size: 1.25rem; 
    font-weight: 500;
    letter-spacing: 0.015em;  /* ✅ Mejora */
    line-height: 1.35;
}

h5 { 
    font-size: 1rem; 
    font-weight: 500;
    letter-spacing: 0.025em;  /* ✅ Mejora */
    line-height: 1.4;
}

h6 { 
    font-size: 0.875rem; 
    font-weight: 500; 
    text-transform: uppercase; 
    letter-spacing: 0.05em;   /* ✅ Mejora */
    line-height: 1.4;
}
```

### Body Text (Line-height aumentado)

```css
p {
    color: var(--md-text-secondary);
    margin-bottom: var(--md-spacing-md);
    line-height: 1.75;  /* ✅ Aumentado de 1.5 a 1.75 */
}
```

### Clases Utilitarias de Tipografía

```css
/* Body */
.md-body-large { font: var(--md-sys-typescale-body-large); letter-spacing: 0.015em; }
.md-body-medium { font: var(--md-sys-typescale-body-medium); letter-spacing: 0.025em; }
.md-body-small { font: var(--md-sys-typescale-body-small); letter-spacing: 0.03em; }

/* Title */
.md-title-large { font: var(--md-sys-typescale-title-large); letter-spacing: 0.015em; }
.md-title-medium { font: var(--md-sys-typescale-title-medium); letter-spacing: 0.015em; }
.md-title-small { font: var(--md-sys-typescale-title-small); letter-spacing: 0.025em; }

/* Label */
.md-label-large { font: var(--md-sys-typescale-label-large); letter-spacing: 0.05em; }
.md-label-medium { font: var(--md-sys-typescale-label-medium); letter-spacing: 0.05em; }
.md-label-small { font: var(--md-sys-typescale-label-small); letter-spacing: 0.05em; }
```

---

## 4. Componentes Principales

### 4.1 Chatbot Gemini-Style (Knowledge Chat)

Componente estrella de la aplicación con diseño inspirado en Google Gemini.

#### Vista Inicial (Welcome Screen)

```css
.gemini-welcome-view {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    min-height: 80vh;
    max-width: 700px;
    margin: 0 auto;
    padding: 2rem;
}

.gemini-title {
    font-size: 3rem;
    font-weight: 300;
    color: rgba(255, 255, 255, 0.95);
    margin: 2rem 0 3rem 0;
    text-align: center;
}

.gemini-logo-img {
    width: 80px;
    height: 80px;
    border-radius: 50%;
}
```

#### Suggestion Chips

```css
.gemini-suggestion-chips {
    display: flex;
    flex-wrap: wrap;
    gap: 1rem;
    justify-content: center;
    margin-top: 3rem;
}

.gemini-chip {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    background: #1E1F21;
    border: 1px solid #3F4042;
    border-radius: 24px;
    padding: 1rem 1.5rem;
    cursor: pointer;
    transition: all 200ms cubic-bezier(0.4, 0, 0.2, 1);
}

.gemini-chip:hover {
    background: #2D2E30;
    border-color: #00C897;
    transform: translateY(-2px);
    box-shadow: 0 4px 12px rgba(0, 200, 151, 0.2);
}

.chip-icon {
    font-size: 1.25rem;
}

.chip-text {
    font-size: 0.9375rem;
    color: rgba(255, 255, 255, 0.9);
}
```

#### Input de Chat - Gemini Floating Style

```css
/* Input flotante con backdrop-filter blur */
.gemini-input-wrapper {
    position: relative;
    display: flex;
    align-items: flex-end;
    background: rgba(30, 31, 33, 0.85);
    backdrop-filter: blur(12px);
    -webkit-backdrop-filter: blur(12px);
    border: 1px solid rgba(255, 255, 255, 0.1);
    border-radius: 28px;
    padding: 0.875rem 1rem 0.875rem 1.25rem;
    transition: all 250ms cubic-bezier(0.2, 0, 0, 1);
    box-shadow: 
        0 4px 24px rgba(0, 0, 0, 0.4),
        inset 0 1px 0 rgba(255, 255, 255, 0.05);
}

.gemini-input-wrapper:hover {
    border-color: rgba(255, 255, 255, 0.2);
    background: rgba(38, 42, 40, 0.9);
}

.gemini-input-wrapper:focus-within {
    border-color: rgba(0, 200, 151, 0.5);
    box-shadow: 
        0 4px 24px rgba(0, 0, 0, 0.4),
        0 0 0 2px rgba(0, 200, 151, 0.15),
        inset 0 1px 0 rgba(255, 255, 255, 0.05);
}

/* Fixed Input Area with Blur */
.gemini-input-fixed {
    background: rgba(18, 18, 18, 0.75);
    backdrop-filter: blur(16px);
    -webkit-backdrop-filter: blur(16px);
    border-top: 1px solid rgba(255, 255, 255, 0.08);
    padding: 1.25rem 1.5rem;
    position: sticky;
    bottom: 0;
    transition: all 300ms ease;
}

/* Sparkle Effect - Thinking State */
.gemini-input-fixed.is-thinking {
    border-image: linear-gradient(90deg, 
        transparent 0%, 
        rgba(0, 200, 151, 0.6) 25%, 
        rgba(77, 168, 218, 0.6) 50%, 
        rgba(0, 200, 151, 0.6) 75%, 
        transparent 100%) 1;
    animation: sparkleShimmer 2s ease-in-out infinite;
}
```

#### Mensajes de Chat - Gemini Style

```css
.gemini-message {
    display: flex;
    gap: 1rem;
    margin-bottom: 2rem;
    animation: messageIn 300ms cubic-bezier(0.4, 0, 0.2, 1);
}

@keyframes messageIn {
    from {
        opacity: 0;
        transform: translateY(10px);
    }
    to {
        opacity: 1;
        transform: translateY(0);
    }
}

/* User Message: Surface Container High + Sharp Corner */
.gemini-message.user {
    flex-direction: row-reverse;
}

.gemini-message.user .message-text {
    background: #313633;
    border: none;
    border-radius: 20px 20px 4px 20px; /* Sharp bottom-right corner */
}

/* Bot Message: Transparent, no box, just text + icon */
.gemini-message.assistant .message-text {
    background: transparent;
    padding: 0.5rem 0;
    line-height: 1.7;
    animation: botFadeIn 400ms ease-out;
}

@keyframes botFadeIn {
    from {
        opacity: 0;
        transform: translateY(8px);
    }
    to {
        opacity: 1;
        transform: translateY(0);
    }
}

/* Gemini-style Bot Avatar */
.assistant-avatar {
    width: 32px;
    height: 32px;
    border-radius: 50%;
    background: linear-gradient(135deg, rgba(0, 200, 151, 0.2), rgba(77, 168, 218, 0.2));
    padding: 5px;
    object-fit: contain;
    flex-shrink: 0;
    transition: all 300ms ease;
}

/* Avatar glow when bot is responding */
.gemini-message.assistant.is-typing .assistant-avatar {
    animation: avatarPulse 1.5s ease-in-out infinite;
    box-shadow: 
        0 0 12px rgba(0, 200, 151, 0.4),
        0 0 24px rgba(0, 200, 151, 0.2);
}

/* Typing Indicator - Gemini Style */
.typing-indicator {
    display: flex;
    gap: 6px;
    padding: 0.5rem 0;
    align-items: center;
}

.typing-indicator span {
    width: 6px;
    height: 6px;
    background: linear-gradient(135deg, #00C897, #4DA8DA);
    border-radius: 50%;
    animation: geminiPulse 1.4s infinite ease-in-out;
}
```
}

.message-text {
    font-size: 0.9375rem;
    line-height: 1.6;
    color: rgba(255, 255, 255, 0.9);
}
```

#### Botones de Feedback (Thumbs Up/Down)

```css
.feedback-buttons {
    display: flex;
    gap: 0.5rem;
    opacity: 0.5;
    transition: opacity 200ms ease;
}

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
    display: flex;
    align-items: center;
    justify-content: center;
}

.feedback-btn:hover {
    border-color: #00C897;
    color: #00C897;
    background: rgba(0, 200, 151, 0.1);
    transform: scale(1.1);
}
```

#### Panel de Corrección

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
    font-size: 0.875rem;
    resize: vertical;
    transition: all 200ms ease;
}

.correction-textarea:focus {
    outline: none;
    border-color: #00C897;
    box-shadow: 0 0 0 2px rgba(0, 200, 151, 0.2);
}
```

### 4.2 Cards Material

```css
.md-card {
    background: #1E1F21;
    border: 1px solid #3F4042;
    border-radius: 16px;
    padding: 24px;
    transition: all 200ms ease;
}

.md-card-clickable {
    cursor: pointer;
}

.md-card-clickable:hover {
    background: #2D2E30;
    border-color: #5F6062;
    transform: translateY(-2px);
    box-shadow: 0 8px 16px rgba(0, 0, 0, 0.3);
}

.md-card-header {
    display: flex;
    align-items: center;
    gap: 12px;
    margin-bottom: 16px;
}

.md-card-title {
    font-size: 1.125rem;
    font-weight: 500;
    color: rgba(255, 255, 255, 0.95);
    margin: 0;
}
```

### 4.3 Botones - Material Design 3

Sistema de botones modernizado con micro-interacciones **Scale & Glow**.

#### Variables de Botones

```css
:root {
    /* Shape: 20px intermedio (moderno), 9999px para FABs */
    --md-btn-shape: 20px;
    --md-btn-shape-full: 9999px;
    
    /* Sizing */
    --md-btn-height: 40px;
    --md-btn-height-sm: 32px;
    --md-btn-height-lg: 48px;
    
    /* Motion: Snappy easing */
    --md-btn-easing: cubic-bezier(0.2, 0.0, 0, 1.0);
    --md-btn-duration: 200ms;
    
    /* Glow Effects */
    --md-btn-glow-primary: 0 0 16px rgba(0, 200, 151, 0.4);
}
```

#### `.btn-filled` - Primary Action

```css
.btn-filled {
    background-color: var(--md-primary);    /* #00C897 */
    color: var(--md-on-primary);             /* #00201A */
    border: none;
    border-radius: 20px;                     /* Shape intermedio */
    height: 40px;
    padding: 0 24px;
    font-weight: 500;
    transition: all 200ms cubic-bezier(0.2, 0.0, 0, 1.0);
}

.btn-filled:hover {
    background-color: var(--md-primary-hover);
    transform: translateY(-1px) scale(1.01);
}

/* 🔥 MICRO-INTERACTION: Scale & Glow */
.btn-filled:active {
    transform: scale(0.98);
    box-shadow: 0 0 16px rgba(0, 200, 151, 0.4);
    transition-duration: 50ms;
}
```

#### `.btn-tonal` - Secondary Action (Crucial)

Fondo primario con 18% opacidad. Ideal para acciones secundarias.

```css
.btn-tonal {
    background-color: rgba(0, 200, 151, 0.18);
    color: var(--md-primary);
    border: none;
    border-radius: 20px;
}

.btn-tonal:hover {
    background-color: rgba(0, 200, 151, 0.28);
    transform: translateY(-1px) scale(1.01);
}

/* 🔥 MICRO-INTERACTION: Scale & Glow */
.btn-tonal:active {
    transform: scale(0.98);
    box-shadow: 0 0 16px rgba(0, 200, 151, 0.4);
    background-color: rgba(0, 200, 151, 0.35);
    transition-duration: 50ms;
}
```

#### `.btn-outlined` - Subtle Action

```css
.btn-outlined {
    background-color: transparent;
    color: var(--md-primary);
    border: 1px solid var(--md-outline);     /* #3D4844 */
    border-radius: 20px;
}

.btn-outlined:hover {
    background-color: var(--md-state-primary-hover);
    border-color: var(--md-primary);
    transform: translateY(-1px) scale(1.01);
}

/* 🔥 MICRO-INTERACTION: Scale & Glow */
.btn-outlined:active {
    transform: scale(0.98);
    box-shadow: 0 0 12px rgba(0, 200, 151, 0.25);
    transition-duration: 50ms;
}
```

#### Size Variants

```css
.btn-sm { height: 32px; padding: 0 16px; font-size: 0.8125rem; border-radius: 16px; }
.btn-lg { height: 48px; padding: 0 32px; font-size: 1rem; border-radius: 24px; }
```

#### Uso Recomendado

| Clase | Uso | Ejemplo |
|-------|-----|---------|
| `.btn-filled` | Acción primaria de la página | "Enviar", "Guardar", "Confirmar" |
| `.btn-tonal` | Acciones secundarias importantes | "Cancelar", "Editar", "Ver más" |
| `.btn-outlined` | Acciones terciarias sutiles | "Limpiar", "Restablecer" |
| `.btn-pill` | Añadir a cualquiera para shape 9999px | FABs, chips seleccionables |

### 4.4 Tables

```css
.md-table-wrapper {
    overflow-x: auto;
    border-radius: 12px;
    border: 1px solid #3F4042;
}

.md-table {
    width: 100%;
    border-collapse: collapse;
    background: #1E1F21;
}

.md-table thead {
    background: #2D2E30;
    border-bottom: 1px solid #3F4042;
}

.md-table th {
    text-align: left;
    padding: 16px;
    font-weight: 500;
    font-size: 0.875rem;
    color: rgba(255, 255, 255, 0.7);
    text-transform: uppercase;
    letter-spacing: 0.5px;
}

.md-table td {
    padding: 16px;
    font-size: 0.875rem;
    color: rgba(255, 255, 255, 0.9);
    border-bottom: 1px solid #3F4042;
}

.md-table tbody tr:hover {
    background: rgba(255, 255, 255, 0.03);
}
```

### 4.5 KPI Cards (Dashboard)

```css
.monitoring-kpi-card {
    background: linear-gradient(135deg, #1E1F21 0%, #2D2E30 100%);
    border: 1px solid #3F4042;
    border-radius: 16px;
    padding: 24px;
    display: flex;
    align-items: center;
    gap: 16px;
    transition: all 200ms ease;
}

.monitoring-kpi-card:hover {
    transform: translateY(-4px);
    box-shadow: 0 8px 24px rgba(0, 0, 0, 0.3);
}

.kpi-icon {
    width: 56px;
    height: 56px;
    border-radius: 12px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 28px;
}

.kpi-created .kpi-icon {
    background: rgba(66, 165, 245, 0.15);
    color: #42A5F5;
}

.kpi-done .kpi-icon {
    background: rgba(102, 187, 106, 0.15);
    color: #66BB6A;
}

.kpi-value {
    display: block;
    font-size: 2rem;
    font-weight: 700;
    color: rgba(255, 255, 255, 0.95);
}

.kpi-label {
    display: block;
    font-size: 0.875rem;
    color: rgba(255, 255, 255, 0.6);
    margin-top: 4px;
}
```

### 4.6 Badges de Estado

```css
.status-badge {
    display: inline-block;
    padding: 4px 10px;
    border-radius: 9999px;
    font-size: 0.75rem;
    font-weight: 500;
}

.status-done {
    background: rgba(102, 187, 106, 0.15);
    color: #66BB6A;
}

.status-progress {
    background: rgba(66, 165, 245, 0.15);
    color: #42A5F5;
}

.status-waiting {
    background: rgba(255, 167, 38, 0.15);
    color: #FFA726;
}

.status-open {
    background: rgba(239, 83, 80, 0.15);
    color: #EF5350;
}
```

### 4.7 Forms

```css
.md-form-input {
    width: 100%;
    height: 56px;
    padding: 16px;
    font-size: 1rem;
    color: rgba(255, 255, 255, 0.95);
    background: #2D2E30;
    border: 1px solid #3F4042;
    border-radius: 8px;
    transition: all 150ms ease;
}

.md-form-input:focus {
    outline: none;
    border-color: #00C897;
    box-shadow: 0 0 0 1px #00C897;
    background: #1E1F21;
}

.md-form-input::placeholder {
    color: rgba(255, 255, 255, 0.50);
}
```

---

## 5. Sistema de Animaciones

### Variables de Animación

```css
:root {
    /* Spring-like easing con rebote natural */
    --md-ease-spring: cubic-bezier(0.175, 0.885, 0.32, 1.275);
    --md-ease-spring-soft: cubic-bezier(0.34, 1.56, 0.64, 1);
    --md-ease-out-expo: cubic-bezier(0.19, 1, 0.22, 1);
    
    /* Duraciones */
    --md-anim-duration-fast: 300ms;
    --md-anim-duration-normal: 400ms;
    --md-anim-duration-slow: 500ms;
    
    /* Delay base para stagger */
    --md-stagger-delay: 50ms;
}
```

### 🔥 Animación Principal: fade-slide-up-spring

Animación de entrada con efecto de resorte natural:

```css
@keyframes fade-slide-up-spring {
    0% {
        opacity: 0;
        transform: translateY(24px) scale(0.96);
    }
    100% {
        opacity: 1;
        transform: translateY(0) scale(1);
    }
}

.md-fade-slide-up {
    animation: fade-slide-up-spring 400ms cubic-bezier(0.175, 0.885, 0.32, 1.275) both;
}
```

### Stagger Animation (Cascada)

Cards y mensajes entran en cascada con nth-child:

```css
/* Base para cards */
.md-card {
    animation: fade-slide-up-spring 400ms var(--md-ease-spring) both;
}

/* Delays escalonados (hasta 10 elementos) */
.md-card:nth-child(1) { animation-delay: 0ms; }
.md-card:nth-child(2) { animation-delay: 50ms; }
.md-card:nth-child(3) { animation-delay: 100ms; }
/* ... hasta nth-child(10) */

/* Mensajes del chat */
.gemini-message {
    animation: fade-slide-up-spring 300ms var(--md-ease-spring) both;
}
```

### View Transitions API (Blazor Ready)

Preparación para transiciones de página suaves:

```css
/* Elementos principales con view-transition-name */
header, .md-header { view-transition-name: app-header; }
main, .md-main     { view-transition-name: app-main; }
aside, .md-sidebar { view-transition-name: app-sidebar; }
nav, .md-nav       { view-transition-name: app-nav; }
.page-content      { view-transition-name: page-content; }

/* Transiciones de página */
::view-transition-old(page-content) {
    animation: fade-slide-up-spring 250ms var(--md-ease-spring) reverse both;
}

::view-transition-new(page-content) {
    animation: fade-slide-up-spring 300ms var(--md-ease-spring) both;
}

/* Header estático durante transiciones */
::view-transition-old(app-header),
::view-transition-new(app-header) {
    animation: none;
}
```

### Respeto a prefers-reduced-motion

```css
@media (prefers-reduced-motion: reduce) {
    .md-card, .gemini-message, .md-fade-slide-up {
        animation: none;
    }
}
```

### Velocidades de Transición Legacy

```css
/* Transición rápida - Hover states */
transition: all 150ms ease;

/* Transición estándar - Cambios de estado */
transition: all 200ms ease;

/* Transición Material - Interacciones complejas */
transition: all 200ms cubic-bezier(0.4, 0, 0.2, 1);
```

### Animaciones Keyframe Legacy

#### Loading Spinner

```css
@keyframes spin {
    from { transform: rotate(0deg); }
    to { transform: rotate(360deg); }
}

.md-spinner {
    animation: spin 1s linear infinite;
}
```

#### Typing Indicator

```css
@keyframes bounce {
    0%, 80%, 100% { transform: scale(0); }
    40% { transform: scale(1); }
}

.typing-indicator span {
    animation: bounce 1.4s infinite ease-in-out both;
}
.typing-indicator span:nth-child(1) { animation-delay: -0.32s; }
.typing-indicator span:nth-child(2) { animation-delay: -0.16s; }
```

#### Modal Entrance

```css
@keyframes modalIn {
    from {
        opacity: 0;
        transform: scale(0.95) translateY(10px);
    }
    to {
        opacity: 1;
        transform: scale(1) translateY(0);
    }
}
```

---

## 6. Layout y Estructura

### Grid System

```css
.md-card-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
    gap: 24px;
}

.md-card-grid-2 {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 24px;
}

.md-card-grid-3 {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 24px;
}

.monitoring-kpi-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
    gap: 20px;
}
```

### Espaciado

```css
/* Espaciado entre secciones */
margin-bottom: 32px;    /* Secciones principales */
margin-bottom: 24px;    /* Sub-secciones */
margin-bottom: 16px;    /* Elementos dentro de secciones */

/* Padding interno */
padding: 24px;          /* Cards estándar */
padding: 16px;          /* Cards compactas */
padding: 48px;          /* Hero sections */
```

### Page Container

```css
.md-page {
    max-width: 1400px;
    margin: 0 auto;
    padding: 2rem;
}

.md-page-header {
    display: flex;
    align-items: center;
    gap: 16px;
    margin-bottom: 32px;
}

.md-page-title {
    display: flex;
    align-items: center;
    gap: 12px;
    font-size: 2rem;
    font-weight: 500;
    color: rgba(255, 255, 255, 0.95);
    margin: 0;
}

.md-page-subtitle {
    font-size: 0.875rem;
    color: rgba(255, 255, 255, 0.70);
    margin: 4px 0 0 0;
}

.md-page-actions {
    margin-left: auto;
}
```

---

## 7. Responsive Design

### Breakpoints

| Breakpoint | Ancho | Uso |
|------------|-------|-----|
| **Desktop XL** | >1400px | Grid 4 columnas, espaciado generoso |
| **Desktop L** | 1200px - 1400px | Grid 3-4 columnas |
| **Desktop** | 1024px - 1200px | Grid 2-3 columnas |
| **Tablet** | 768px - 1024px | Grid 1-2 columnas |
| **Mobile L** | 600px - 768px | Grid 1 columna |
| **Mobile M** | 480px - 600px | Layout compacto |
| **Mobile S** | <480px | Layout mínimo |

### Media Queries

```css
/* Desktop Large */
@media (max-width: 1400px) {
    .md-page {
        max-width: 1200px;
    }
}

/* Desktop */
@media (max-width: 1200px) {
    .md-card-grid-4 {
        grid-template-columns: repeat(2, 1fr);
    }
}

/* Tablet */
@media (max-width: 1024px) {
    .monitoring-kpi-grid {
        grid-template-columns: repeat(2, 1fr);
    }
    
    .md-card-grid-3 {
        grid-template-columns: repeat(2, 1fr);
    }
}

/* Tablet Small */
@media (max-width: 768px) {
    .md-card-grid,
    .md-card-grid-2,
    .md-card-grid-3,
    .md-card-grid-4 {
        grid-template-columns: 1fr;
    }
    
    .gemini-title {
        font-size: 2.5rem;
    }
    
    .md-page {
        padding: 1rem;
    }
}

/* Mobile */
@media (max-width: 600px) {
    .monitoring-kpi-grid {
        grid-template-columns: 1fr;
    }
    
    .md-card {
        padding: 16px;
    }
    
    .md-page-title {
        font-size: 1.5rem;
    }
}

/* Mobile Small */
@media (max-width: 480px) {
    .gemini-title {
        font-size: 2rem;
    }
    
    .gemini-logo-img {
        width: 60px;
        height: 60px;
    }
    
    .message-text {
        font-size: 0.875rem;
    }
}
```

---

## 8. Iconografía - Material Symbols Rounded

### Librería

**Google Material Symbols (Rounded variant with Font Variations)**

```html
<link href="https://fonts.googleapis.com/css2?family=Material+Symbols+Rounded:opsz,wght,FILL,GRAD@20..48,100..700,0..1,-50..200" rel="stylesheet" />
```

### Font Variation Settings

Material Symbols soporta variaciones de fuente para control preciso:

```css
.material-symbols-rounded {
    font-variation-settings:
        'FILL' 0,    /* 0 = outlined, 1 = filled */
        'wght' 400,  /* 100-700 (weight) */
        'GRAD' 0,    /* -50 to 200 (grade/emphasis) */
        'opsz' 24;   /* 20, 24, 40, 48 (optical size) */
    
    /* Transición suave para animación de FILL */
    transition: font-variation-settings 200ms cubic-bezier(0.2, 0, 0, 1);
}
```

### 🔥 Transición de FILL en Hover/Active

El icono se llena automáticamente cuando el padre tiene `:hover` o `.active`:

```css
/* Parent hover → Icon fills */
button:hover > .material-symbols-rounded,
.md-btn:hover .material-symbols-rounded,
.md-nav-item:hover .material-symbols-rounded {
    font-variation-settings: 'FILL' 1, 'wght' 400, 'GRAD' 0, 'opsz' 24;
}

/* Parent .active → Icon filled */
.active .material-symbols-rounded,
.md-nav-item.active .material-symbols-rounded,
[aria-selected="true"] .material-symbols-rounded {
    font-variation-settings: 'FILL' 1, 'wght' 500, 'GRAD' 0, 'opsz' 24;
}
```

### Clases de Variantes

| Clase | FILL | WGHT | Uso |
|-------|------|------|-----|
| `.icon-light` | 0 | 300 | Iconos sutiles |
| `.icon-regular` | 0 | 400 | Default |
| `.icon-medium` | 0 | 500 | Énfasis medio |
| `.icon-bold` | 0 | 700 | Énfasis fuerte |
| `.icon-filled` | 1 | 400 | Siempre filled |

### Tamaños con Optical Size

```css
.icon-sm  { font-size: 18px; opsz: 20; }
.icon-md  { font-size: 24px; opsz: 24; }  /* Default */
.icon-lg  { font-size: 32px; opsz: 40; }
.icon-xl  { font-size: 48px; opsz: 48; }
```

### Uso

```html
<!-- Icono básico -->
<span class="material-symbols-rounded">dashboard</span>

<!-- Icono que se llena en hover del botón -->
<button class="btn-filled">
    <span class="material-symbols-rounded">send</span>
    Enviar
</button>

<!-- Icono siempre filled -->
<span class="material-symbols-rounded icon-filled">favorite</span>

<!-- Prevenir transición de fill -->
<span class="material-symbols-rounded icon-no-fill">info</span>
```

### Tamaños Contextuales

| Contexto | Tamaño | Optical Size | Uso |
|----------|--------|--------------|-----|
| Hero Icon | 64px | 48 | Estados vacíos grandes |
| KPI Icon | 28px | 24 | Tarjetas de métricas |
| Card Icon | 24px | 24 | Encabezados de cards |
| Button Icon | 20px | 24 | Botones con icono |
| Badge Icon | 14px | 20 | Status badges |

---

## 9. Temas Visuales

### Hero Gradients

```css
/* Gradiente corporativo azul-oscuro */
background: linear-gradient(135deg, #002A50 0%, #1E1F21 100%);

/* Con efecto decorativo circular */
.md-hero::before {
    content: '';
    position: absolute;
    top: -50%;
    right: -20%;
    width: 400px;
    height: 400px;
    background: radial-gradient(circle, rgba(0, 200, 151, 0.15) 0%, transparent 70%);
    border-radius: 50%;
}
```

### Elevación y Sombras

```css
/* Shadow pequeña - Cards en reposo */
box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);

/* Shadow media - Cards en hover */
box-shadow: 0 8px 16px rgba(0, 0, 0, 0.3);

/* Shadow grande - Modales, dropdowns */
box-shadow: 0 12px 24px rgba(0, 0, 0, 0.4);

/* Glow effect - Elementos activos con primary */
box-shadow: 0 4px 12px rgba(0, 200, 151, 0.3);

/* Focus ring - Inputs en foco */
box-shadow: 0 0 0 3px rgba(0, 200, 151, 0.15);
```

### Scrollbar Custom

```css
.gemini-messages-area::-webkit-scrollbar {
    width: 8px;
}

.gemini-messages-area::-webkit-scrollbar-track {
    background: transparent;
}

.gemini-messages-area::-webkit-scrollbar-thumb {
    background: #3F4042;
    border-radius: 4px;
}

.gemini-messages-area::-webkit-scrollbar-thumb:hover {
    background: #5F6062;
}
```

### Glassmorphism (Subtle)

```css
.monitoring-filters {
    background: rgba(255, 255, 255, 0.03);
    backdrop-filter: blur(10px);
    border-radius: 12px;
}
```

---

## 📊 Guía de Implementación

### Clase Naming Conventions

```
Prefijo        Uso
---------      ------------------------------------------
.md-           Material Design components
.gemini-       Chatbot Gemini-style components
.monitoring-   Dashboard/monitoring specific
.kpi-          KPI cards and metrics
.status-       Status badges
.feedback-     Feedback UI elements
.correction-   Correction panel elements
```

### Checklist de Nuevo Componente

- [ ] Usa la paleta de colores definida
- [ ] Respeta la jerarquía tipográfica
- [ ] Incluye estados hover/focus/active/disabled
- [ ] Tiene transiciones suaves (150-300ms)
- [ ] Es responsive (mobile-first)
- [ ] Usa Material Symbols para iconos
- [ ] Mantiene espaciado consistente (múltiplos de 4px)
- [ ] Accesibilidad: contraste adecuado (WCAG AA mínimo)

---

## 🔗 Referencias

- **Material Design 3**: https://m3.material.io/
- **Material Symbols**: https://fonts.google.com/icons
- **Roboto Font**: https://fonts.google.com/specimen/Roboto
- **Color Contrast Checker**: https://webaim.org/resources/contrastchecker/
- **CSS Easing Functions**: https://easings.net/

---

**Nota Importante:** Este documento refleja el estado actual de la UI al 29 de enero de 2026. Cualquier cambio en el diseño debe actualizarse aquí para mantener la documentación sincronizada con el código.

**Contacto Design System:** Arquitecto de Software Senior  
**Última revisión:** 29/01/2026
