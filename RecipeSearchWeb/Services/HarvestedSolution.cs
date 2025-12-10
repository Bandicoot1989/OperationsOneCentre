using System;

namespace RecipeSearchWeb.Services
{
    public class HarvestedSolution
    {
        public string Id { get; set; } = string.Empty;
        public string TicketKey { get; set; } = string.Empty;
        public string Problem { get; set; } = string.Empty;
        public string Context { get; set; } = string.Empty;
        public string Solution { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string[] Tags { get; set; } = Array.Empty<string>();
        public DateTime ExtractedAt { get; set; }
        public string SourceUrl { get; set; } = string.Empty;
    }
}
