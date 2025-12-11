# 🎨 Design System - Operations One Centre

## Guía de Estilos y Componentes UI

**Última actualización:** 11 Diciembre 2025  
**Versión:** 1.0  
**Framework:** Blazor Server .NET 10 + CSS puro (Material Design 3 inspirado)

---

## 📋 Índice

1. [Filosofía de Diseño](#1-filosofía-de-diseño)
2. [Paleta de Colores](#2-paleta-de-colores)
3. [Tipografía](#3-tipografía)
4. [Espaciado y Layout](#4-espaciado-y-layout)
5. [Componentes UI](#5-componentes-ui)
6. [Patrones de Diseño](#6-patrones-de-diseño)
7. [Animaciones](#7-animaciones)
8. [Responsive Design](#8-responsive-design)
9. [Iconografía](#9-iconografía)
10. [Código de Referencia](#10-código-de-referencia)

---

## 1. Filosofía de Diseño

### Principios

- **Dark Mode First**: Diseño optimizado para modo oscuro, reduce fatiga visual
- **Material Design 3**: Inspirado en Google Material You con toques corporativos
- **Consistencia**: Mismos patrones visuales en toda la aplicación
- **Accesibilidad**: Contrastes adecuados y estados claramente diferenciados
- **Minimalismo Funcional**: Solo los elementos necesarios, sin ruido visual

### Enfoque Visual

```
┌──────────────────────────────────────────────────────────────┐
│  DARK MODE CORPORATE                                          │
│                                                                │
│  • Fondo oscuro principal (#121212)                           │
│  • Superficies elevadas (#1E1F21, #2D2E30)                   │
│  • Bordes sutiles (#3F4042)                                   │
│  • Acento verde corporativo (#00C897 - Antolin Green)        │
│  • Texto blanco con opacidades variables                      │
└──────────────────────────────────────────────────────────────┘
```

---

## 2. Paleta de Colores

### Colores Principales

| Nombre | Hex | Uso |
|--------|-----|-----|
| **Background** | `#121212` | Fondo principal de la aplicación |
| **Surface 1** | `#1E1F21` | Tarjetas, modales, contenedores |
| **Surface 2** | `#2D2E30` | Inputs, dropdowns, elementos elevados |
| **Border** | `#3F4042` | Bordes de elementos |
| **Primary (Antolin Green)** | `#00C897` | Acento principal, CTAs, links |
| **Primary Dark** | `#00A87E` | Hover del primary |
| **Corporate Blue** | `#002A50` | Gradientes hero, elementos destacados |

### Colores de Estado

| Estado | Hex | Uso |
|--------|-----|-----|
| **Success** | `#4CAF50` | Operaciones exitosas, estados positivos |
| **Success Light** | `#66BB6A` | Variante más clara |
| **Warning** | `#FF9800` | Alertas, precaución |
| **Warning Light** | `#FFA726` | Variante más clara |
| **Error** | `#EF5350` | Errores, estados críticos |
| **Info** | `#42A5F5` | Información, estados neutros |
| **Purple** | `#BB86FC` | Estados especiales (learning, AI) |
| **Purple Alt** | `#AB47BC` | Variante alternativa |

### Opacidades de Texto

```css
/* Texto sobre fondo oscuro */
--text-high:    rgba(255, 255, 255, 0.95);  /* Títulos, texto principal */
--text-medium:  rgba(255, 255, 255, 0.70);  /* Subtítulos, texto secundario */
--text-low:     rgba(255, 255, 255, 0.50);  /* Hints, placeholders */
--text-disabled: rgba(255, 255, 255, 0.30); /* Texto deshabilitado */
```

### Uso de Color en Fondos

```css
/* Para badges y fondos de color */
background: rgba(0, 200, 151, 0.15);   /* Primary - 15% opacidad */
background: rgba(76, 175, 80, 0.15);   /* Success - 15% opacidad */
background: rgba(255, 152, 0, 0.15);   /* Warning - 15% opacidad */
background: rgba(239, 83, 80, 0.15);   /* Error - 15% opacidad */
background: rgba(66, 165, 245, 0.15);  /* Info - 15% opacidad */
```

---

## 3. Tipografía

### Fuentes

```css
/* Principal */
font-family: 'Roboto', 'Helvetica Neue', Helvetica, Arial, sans-serif;

/* Código */
font-family: 'Roboto Mono', 'Consolas', monospace;
```

### Escala Tipográfica

| Elemento | Tamaño | Peso | Line Height |
|----------|--------|------|-------------|
| Hero Title | `3rem` (48px) | 400 | 1.2 |
| Page Title | `2rem` (32px) | 400 | 1.3 |
| Section Title | `1.25rem` (20px) | 500 | 1.4 |
| Card Title | `1.125rem` (18px) | 500 | 1.4 |
| Body | `1rem` (16px) | 400 | 1.5 |
| Body Small | `0.875rem` (14px) | 400 | 1.5 |
| Caption | `0.75rem` (12px) | 400/500 | 1.4 |

### Estilos de Texto

```css
/* Títulos de página */
.md-page-title {
    font-size: 2rem;
    font-weight: 400;
    color: rgba(255, 255, 255, 0.95);
}

/* Subtítulos */
.md-page-subtitle {
    font-size: 1rem;
    color: rgba(255, 255, 255, 0.70);
}

/* Labels uppercase */
.md-label-uppercase {
    font-size: 0.75rem;
    font-weight: 500;
    text-transform: uppercase;
    letter-spacing: 0.5px;
    color: rgba(255, 255, 255, 0.70);
}
```

---

## 4. Espaciado y Layout

### Sistema de Espaciado (8px base)

| Token | Valor | Uso |
|-------|-------|-----|
| `xs` | 4px | Gaps mínimos |
| `sm` | 8px | Gaps pequeños, padding chips |
| `md` | 12px | Gaps medios |
| `lg` | 16px | Padding cards, gaps grid |
| `xl` | 20px | Padding secciones |
| `2xl` | 24px | Gaps entre secciones |
| `3xl` | 32px | Margin entre módulos |
| `4xl` | 48px | Padding hero sections |

### Border Radius

| Elemento | Radio |
|----------|-------|
| Chips/Pills | `9999px` (full) |
| Cards grandes | `24px` |
| Cards medianas | `16px` |
| Cards pequeñas | `12px` |
| Inputs | `8px` |
| Badges | `4px` |
| Botones icon | `50%` |

### Grid System

```css
/* Grid auto-responsive */
.md-card-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
    gap: 24px;
}

/* Grid fijo 4 columnas */
.md-card-grid-4 {
    grid-template-columns: repeat(4, 1fr);
}

/* Grid fijo 3 columnas */
.md-card-grid-3 {
    grid-template-columns: repeat(3, 1fr);
}

/* Dos columnas asimétricas */
.monitoring-grid-2col {
    display: grid;
    grid-template-columns: 1.5fr 1fr;
    gap: 24px;
}
```

### Contenedor Principal

```css
.md-page {
    max-width: 1400px;
    margin: 0 auto;
    padding: 0;
}
```

---

## 5. Componentes UI

### 5.1 Cards

#### Card Básica

```html
<div class="md-card">
    <div class="md-card-icon">
        <span class="material-symbols-outlined">dashboard</span>
    </div>
    <h3 class="md-card-title">Título</h3>
    <p class="md-card-subtitle">Descripción breve</p>
    <span class="md-card-action">Ver más →</span>
</div>
```

```css
.md-card {
    background: #1E1F21;
    border-radius: 16px;
    padding: 24px;
    border: 1px solid #3F4042;
    transition: all 200ms cubic-bezier(0.4, 0, 0.2, 1);
}

.md-card-clickable:hover {
    border-color: #00C897;
    box-shadow: 0 0 0 1px #00C897;
}
```

#### Card con Gradiente (Primary)

```css
.md-card-primary {
    background: linear-gradient(135deg, #002A50 0%, #1E1F21 100%);
    border-color: #00C897;
}
```

#### Card de Icono

```css
.md-card-icon {
    width: 56px;
    height: 56px;
    border-radius: 16px;
    background: rgba(0, 200, 151, 0.15);
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 24px;
    color: #00C897;
    margin-bottom: 16px;
}
```

### 5.2 KPI Cards (Métricas)

```html
<div class="monitoring-kpi-card kpi-created">
    <div class="kpi-icon">
        <span class="material-symbols-outlined">confirmation_number</span>
    </div>
    <div class="kpi-content">
        <span class="kpi-value">42</span>
        <span class="kpi-label">Tickets Abiertos</span>
    </div>
</div>
```

```css
.monitoring-kpi-card {
    background: #1E1F21;
    border: 1px solid #3F4042;
    border-radius: 20px;
    padding: 24px;
    display: flex;
    align-items: center;
    gap: 16px;
    transition: all 0.2s ease;
}

.monitoring-kpi-card:hover {
    border-color: #00C897;
    transform: translateY(-2px);
}

/* Variantes de color por tipo */
.kpi-created .kpi-icon { background: rgba(66, 165, 245, 0.15); color: #42A5F5; }
.kpi-resolved .kpi-icon { background: rgba(102, 187, 106, 0.15); color: #66BB6A; }
.kpi-open .kpi-icon { background: rgba(255, 167, 38, 0.15); color: #FFA726; }
.kpi-progress .kpi-icon { background: rgba(171, 71, 188, 0.15); color: #AB47BC; }
```

### 5.3 Botones

#### Botón Primary (Pill)

```css
.btn-primary {
    color: #002A50;
    background-color: #00C897;
    border-color: #00C897;
    border-radius: 9999px;
    font-weight: 500;
}

.btn-primary:hover {
    background-color: #00A87E;
    border-color: #00A87E;
}
```

#### Botón Outlined

```css
.md-btn-outlined {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    padding: 10px 20px;
    background: transparent;
    border: 1px solid #3F4042;
    border-radius: 9999px;
    color: rgba(255, 255, 255, 0.9);
    font-size: 0.875rem;
    font-weight: 500;
    cursor: pointer;
    transition: all 0.2s ease;
}

.md-btn-outlined:hover:not(:disabled) {
    background: rgba(0, 200, 151, 0.1);
    border-color: #00C897;
    color: #00C897;
}
```

#### Botón Icon

```css
.md-btn-icon {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 40px;
    height: 40px;
    background: transparent;
    border: 1px solid #3F4042;
    border-radius: 12px;
    color: rgba(255, 255, 255, 0.7);
    cursor: pointer;
    transition: all 0.2s ease;
}

.md-btn-icon:hover {
    background: rgba(0, 200, 151, 0.1);
    border-color: #00C897;
    color: #00C897;
}
```

### 5.4 Inputs y Forms

#### Search Box (Pill)

```css
.md-search-box {
    display: flex;
    align-items: center;
    gap: 12px;
    background: #2D2E30;
    border: 1px solid #3F4042;
    border-radius: 9999px;
    padding: 0 20px;
    height: 56px;
    transition: all 200ms ease;
}

.md-search-box:focus-within {
    background: #1E1F21;
    border-color: #00C897;
    box-shadow: 0 0 0 1px #00C897;
}

.md-search-input {
    flex: 1;
    border: none;
    background: transparent;
    font-size: 1rem;
    color: rgba(255, 255, 255, 0.95);
    outline: none;
}

.md-search-input::placeholder {
    color: rgba(255, 255, 255, 0.50);
}
```

#### Input Standard

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
```

#### Select/Dropdown

```css
.filter-group select {
    background: #1E1F21;
    border: 1px solid #3F4042;
    border-radius: 8px;
    color: rgba(255, 255, 255, 0.9);
    font-size: 0.8rem;
    padding: 8px 12px;
    cursor: pointer;
    min-width: 120px;
}

.filter-group select:hover {
    border-color: #00C897;
}

.filter-group select:focus {
    outline: none;
    border-color: #00C897;
}
```

### 5.5 Badges y Tags

#### Status Badge (Pill)

```css
.status-badge {
    display: inline-block;
    padding: 4px 10px;
    border-radius: 9999px;
    font-size: 0.75rem;
    font-weight: 500;
}

.status-done { background: rgba(102, 187, 106, 0.15); color: #66BB6A; }
.status-progress { background: rgba(66, 165, 245, 0.15); color: #42A5F5; }
.status-waiting { background: rgba(255, 167, 38, 0.15); color: #FFA726; }
.status-open { background: rgba(239, 83, 80, 0.15); color: #EF5350; }
```

#### Priority Badge

```css
.priority-badge {
    display: inline-block;
    padding: 3px 8px;
    border-radius: 4px;
    font-size: 0.7rem;
    font-weight: 500;
}

.priority-critical { background: rgba(239, 83, 80, 0.2); color: #EF5350; }
.priority-high { background: rgba(255, 167, 38, 0.2); color: #FFA726; }
.priority-medium { background: rgba(66, 165, 245, 0.2); color: #42A5F5; }
.priority-low { background: rgba(102, 187, 106, 0.2); color: #66BB6A; }
```

#### Tag/Chip

```css
.md-tag {
    font-size: 0.75rem;
    font-weight: 500;
    color: rgba(255, 255, 255, 0.70);
    background: #2D2E30;
    border: 1px solid #3F4042;
    padding: 4px 12px;
    border-radius: 9999px;
    transition: all 150ms ease;
}

.md-tag:hover {
    border-color: #00C897;
    color: #00C897;
}

.md-tag-primary {
    color: #00C897;
    background: rgba(0, 200, 151, 0.15);
    border-color: transparent;
}
```

### 5.6 Tabs

```css
.md-tabs {
    display: flex;
    gap: 8px;
    margin-bottom: 24px;
    padding-bottom: 16px;
    border-bottom: 1px solid #3F4042;
}

.md-tab {
    padding: 10px 24px;
    font-size: 0.875rem;
    font-weight: 500;
    color: rgba(255, 255, 255, 0.70);
    background: transparent;
    border: none;
    border-radius: 9999px;
    cursor: pointer;
    transition: all 150ms ease;
}

.md-tab:hover {
    background: #2D2E30;
    color: rgba(255, 255, 255, 0.95);
}

.md-tab.active {
    background: rgba(0, 200, 151, 0.15);
    color: #00C897;
}
```

### 5.7 Tables

```css
.md-table-container {
    background: #1E1F21;
    border-radius: 12px;
    overflow: hidden;
    border: 1px solid #3F4042;
}

.md-table {
    width: 100%;
    border-collapse: collapse;
}

.md-table th {
    text-align: left;
    padding: 16px 20px;
    font-size: 0.75rem;
    font-weight: 500;
    color: rgba(255, 255, 255, 0.70);
    text-transform: uppercase;
    letter-spacing: 0.5px;
    background: #2D2E30;
    border-bottom: 1px solid #3F4042;
}

.md-table td {
    padding: 16px 20px;
    font-size: 0.875rem;
    color: rgba(255, 255, 255, 0.95);
    border-bottom: 1px solid #3F4042;
}

.md-table tr:last-child td {
    border-bottom: none;
}

.md-table tr:hover td {
    background: #2D2E30;
}
```

### 5.8 Modals

```css
.md-modal-backdrop {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.6);
    backdrop-filter: blur(2px);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 1000;
    padding: 24px;
}

.md-modal {
    background: #1E1F21;
    border-radius: 24px;
    border: 1px solid #3F4042;
    max-width: 800px;
    width: 100%;
    max-height: calc(100vh - 48px);
    overflow: hidden;
    animation: modalIn 200ms ease-out;
}

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

### 5.9 Alerts/Banners

```css
.md-banner {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 16px 20px;
    border-radius: 12px;
    margin-bottom: 24px;
    font-size: 0.875rem;
    border: 1px solid;
}

.md-banner-success {
    background: rgba(76, 175, 80, 0.1);
    border-color: #4CAF50;
    color: #4CAF50;
}

.md-banner-error {
    background: rgba(239, 83, 80, 0.1);
    border-color: #EF5350;
    color: #EF5350;
}

.md-banner-warning {
    background: rgba(255, 152, 0, 0.1);
    border-color: #FF9800;
    color: #FF9800;
}

.md-banner-info {
    background: rgba(0, 200, 151, 0.1);
    border-color: #00C897;
    color: #00C897;
}
```

### 5.10 Empty States

```css
.md-empty-state {
    text-align: center;
    padding: 64px 32px;
}

.md-empty-icon {
    font-size: 64px;
    display: block;
    margin-bottom: 24px;
    opacity: 0.3;
    color: rgba(255, 255, 255, 0.50);
}

.md-empty-title {
    font-size: 1.25rem;
    font-weight: 500;
    color: rgba(255, 255, 255, 0.95);
    margin: 0 0 8px 0;
}

.md-empty-text {
    font-size: 0.875rem;
    color: rgba(255, 255, 255, 0.70);
    margin: 0;
}
```

### 5.11 Loading States

```css
.md-loading {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 64px;
    gap: 16px;
}

.md-spinner {
    width: 48px;
    height: 48px;
    border: 4px solid #3F4042;
    border-top-color: #00C897;
    border-radius: 50%;
    animation: spin 1s linear infinite;
}

@keyframes spin {
    to { transform: rotate(360deg); }
}
```

---

## 6. Patrones de Diseño

### 6.1 Hero Section

```css
.md-hero {
    background: linear-gradient(135deg, #002A50 0%, #1E1F21 100%);
    border-radius: 24px;
    padding: 48px;
    margin-bottom: 32px;
    position: relative;
    overflow: hidden;
    border: 1px solid #3F4042;
}

/* Efecto decorativo */
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

### 6.2 Page Header con Acciones

```css
.md-page-header {
    display: flex;
    align-items: center;
    gap: 16px;
    margin-bottom: 32px;
}

.md-page-actions {
    margin-left: auto;
}
```

### 6.3 Filter Bar

```css
.monitoring-filters {
    margin-bottom: 20px;
    padding: 16px;
    background: rgba(255, 255, 255, 0.03);
    border-radius: 12px;
}

.filter-row {
    display: flex;
    flex-wrap: wrap;
    gap: 16px;
    align-items: center;
}
```

---

## 7. Animaciones

### Transiciones Standard

```css
/* Transición rápida para hover */
transition: all 150ms ease;

/* Transición media para cambios de estado */
transition: all 200ms ease;

/* Transición suave Material */
transition: all 200ms cubic-bezier(0.4, 0, 0.2, 1);

/* Transición para charts/datos */
transition: height 0.3s ease;
```

### Animaciones Keyframe

```css
/* Spinner de carga */
@keyframes spin {
    from { transform: rotate(0deg); }
    to { transform: rotate(360deg); }
}

/* Entrada de modal */
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

## 8. Responsive Design

### Breakpoints

| Nombre | Ancho | Uso |
|--------|-------|-----|
| Desktop Large | > 1200px | Grid 4 columnas |
| Desktop | 1024px - 1200px | Grid 3 columnas |
| Tablet | 768px - 1024px | Grid 2 columnas |
| Mobile | 600px - 768px | Grid 1-2 columnas |
| Mobile Small | < 600px | Grid 1 columna |

### Media Queries

```css
@media (max-width: 1200px) {
    .md-card-grid-4 {
        grid-template-columns: repeat(2, 1fr);
    }
}

@media (max-width: 1024px) {
    .monitoring-kpi-grid {
        grid-template-columns: repeat(2, 1fr);
    }
    
    .monitoring-grid-2col {
        grid-template-columns: 1fr;
    }
}

@media (max-width: 768px) {
    .md-card-grid,
    .md-card-grid-3,
    .md-card-grid-4 {
        grid-template-columns: 1fr;
    }
    
    .md-hero {
        padding: 32px 24px;
    }
    
    .md-hero-title {
        font-size: 2rem;
    }
}

@media (max-width: 600px) {
    .monitoring-kpi-grid {
        grid-template-columns: 1fr;
    }
    
    .md-card {
        padding: 16px;
    }
}
```

---

## 9. Iconografía

### Librería de Iconos

```html
<!-- Google Material Symbols (Outlined) -->
<link href="https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@20..48,100..700,0..1,-50..200" rel="stylesheet" />
```

### Uso

```html
<span class="material-symbols-outlined">dashboard</span>
<span class="material-symbols-outlined">confirmation_number</span>
<span class="material-symbols-outlined">trending_up</span>
```

### Tamaños de Icono

| Contexto | Tamaño |
|----------|--------|
| Icono en card | 24px |
| Icono en KPI | 28px |
| Icono en botón | 20px |
| Icono en input | 20px |
| Icono en tabla | 18px |
| Icono empty state | 48-64px |

---

## 10. Código de Referencia

### Archivos CSS

```
wwwroot/
├── app.css                  # Estilos globales, reset, variables base
├── css/
│   ├── pages-material.css   # Componentes UI (cards, tables, forms, etc.)
│   └── chatbot.css          # Estilos del chatbot (si aplica)
```

### Clases de Nomenclatura

- **Prefijo `md-`**: Material Design components
- **Prefijo `monitoring-`**: Componentes específicos del dashboard
- **Modificadores**: `.md-card-primary`, `.md-card-clickable`, `.status-done`

### Ejemplo de Página Completa

```html
<div class="md-page">
    <!-- Header -->
    <div class="md-page-header">
        <div>
            <h1 class="md-page-title">
                <span class="material-symbols-outlined">dashboard</span>
                Título de Página
            </h1>
            <p class="md-page-subtitle">Descripción breve</p>
        </div>
        <div class="md-page-actions">
            <button class="md-btn-outlined">
                <span class="material-symbols-outlined">refresh</span>
                Actualizar
            </button>
        </div>
    </div>

    <!-- KPI Grid -->
    <div class="monitoring-kpi-grid">
        <div class="monitoring-kpi-card kpi-created">
            <div class="kpi-icon">
                <span class="material-symbols-outlined">confirmation_number</span>
            </div>
            <div class="kpi-content">
                <span class="kpi-value">42</span>
                <span class="kpi-label">Tickets Abiertos</span>
            </div>
        </div>
        <!-- Más KPIs... -->
    </div>

    <!-- Content Cards -->
    <div class="md-card">
        <div class="md-card-header">
            <h3>
                <span class="material-symbols-outlined">list</span>
                Título de Sección
            </h3>
        </div>
        <!-- Contenido -->
    </div>
</div>
```

---

## 📚 Recursos Adicionales

- [Google Material Design 3](https://m3.material.io/)
- [Material Symbols](https://fonts.google.com/icons)
- [Roboto Font](https://fonts.google.com/specimen/Roboto)

---

**Nota**: Este documento debe actualizarse cuando se añadan nuevos componentes o se modifiquen los estilos existentes.

*Última actualización: 11 Diciembre 2025*
