namespace OperationsOneCentre.Domain.Common;

/// <summary>
/// Shared text analysis utilities used across multiple services.
/// Eliminates duplication of stop words, system detection, and text normalization.
/// </summary>
public static class TextAnalysis
{
    /// <summary>
    /// Bilingual stop words (Spanish + English) used for keyword-based search filtering.
    /// </summary>
    public static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Spanish
        "que", "es", "el", "la", "los", "las", "un", "una", "de", "del", "en", "por", "para",
        "como", "cual", "donde", "cuando", "quien", "qué", "cuál", "dónde", "cuándo", "quién",
        "me", "te", "se", "nos", "mi", "tu", "su", "este", "esta", "ese", "esa", "centro",
        "con", "sin", "sobre", "entre", "hasta", "pero", "más", "muy", "ya", "no", "si",
        "todo", "todos", "toda", "todas", "otro", "otra", "otros", "otras",
        // English
        "what", "is", "the", "a", "an", "of", "in", "for", "to", "how", "which", "where",
        "when", "who", "it", "its", "this", "that", "these", "those", "are", "was", "were",
        "be", "been", "being", "have", "has", "had", "do", "does", "did", "will", "would",
        "can", "could", "should", "may", "might", "must", "shall",
        "and", "or", "but", "not", "with", "from", "by", "at", "on", "about",
        // Domain-specific low-value words
        "plant", "planta"
    };

    /// <summary>
    /// Detect the IT system/domain from text content.
    /// Used by JiraSolutionHarvesterService and TicketLookupService.
    /// </summary>
    public static string DetectSystem(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "General";

        var lower = text.ToLowerInvariant();

        // Order matters: more specific matches first
        if (ContainsAny(lower, "sap", "fiori", "t-code", "tcode", "transaccion", "transacción",
            "authorization", "autorización", "sapgui", "sap gui", "abap", "bapi", "idoc sap"))
            return "SAP";

        if (ContainsAny(lower, "teamcenter", "plm", "catia", "siemens nx", "windchill",
            "cad", "bom", "bill of materials", "drawing", "design"))
            return "PLM";

        if (ContainsAny(lower, "edi", "edifact", "as2", "seeburger", "b2b",
            "beone", "buyone", "web-edi", "supplier portal"))
            return "EDI";

        if (ContainsAny(lower, "mes", "blade", "scada", "plc", "opc",
            "produccion", "producción", "manufacturing", "shop floor"))
            return "MES";

        if (ContainsAny(lower, "zscaler", "vpn", "remote access", "acceso remoto",
            "conectividad", "connectivity", "firewall", "proxy"))
            return "Network";

        if (ContainsAny(lower, "outlook", "teams", "office 365", "o365", "onedrive",
            "sharepoint", "printer", "impresora", "laptop", "email", "correo"))
            return "Workplace";

        if (ContainsAny(lower, "server", "servidor", "vmware", "azure", "backup",
            "active directory", "dns", "dhcp", "hyper-v", "datacenter"))
            return "Infrastructure";

        if (ContainsAny(lower, "password", "contraseña", "mfa", "phishing", "malware",
            "security", "seguridad", "encryption", "cifrado", "bitlocker"))
            return "Cybersecurity";

        return "General";
    }

    /// <summary>
    /// Normalize text by removing accents and lowercasing.
    /// </summary>
    public static string NormalizeForSearch(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return text.ToLowerInvariant()
            .Replace("á", "a").Replace("é", "e").Replace("í", "i")
            .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n")
            .Replace("ü", "u");
    }

    /// <summary>
    /// Split text into search terms, filtering stop words and short tokens.
    /// </summary>
    public static List<string> ExtractSearchTerms(string query, int minLength = 2)
    {
        return query
            .Split(new[] { ' ', '?', '¿', '!', '¡', ',', '.', ':', ';', '"', '\'', '(', ')' },
                StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= minLength)
            .Select(t => t.ToLowerInvariant())
            .Where(t => !StopWords.Contains(t))
            .Distinct()
            .ToList();
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
