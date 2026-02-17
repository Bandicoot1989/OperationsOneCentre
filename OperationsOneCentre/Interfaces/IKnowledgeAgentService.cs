using OpenAI.Chat;
using OperationsOneCentre.Services;

namespace OperationsOneCentre.Interfaces;

/// <summary>
/// Specialist type for routing
/// </summary>
public enum SpecialistType
{
    General,
    SAP,
    Network,
    PLM,           // Teamcenter, CATIA, CAD, Siemens NX
    EDI,           // EDI, B2B Portals, Supplier, BeOne, BuyOne
    MES,           // MES, Production, BLADE, Plant, Manufacturing
    Workplace,     // End User: Outlook, Teams, Printer, Laptop, Office
    Infrastructure,// Servers, Azure, VMware, Backup, AD, DNS
    Cybersecurity  // Password, MFA, Security, Phishing
}

/// <summary>
/// Interface for the AI Knowledge Agent (RAG-based Q&A)
/// </summary>
public interface IKnowledgeAgentService
{
    Task<AgentResponse> AskAsync(string question, List<ChatMessage>? conversationHistory = null);
    Task<AgentResponse> AskWithSpecialistAsync(string question, SpecialistType specialist, string? specialistContext = null, List<ChatMessage>? conversationHistory = null);
    IAsyncEnumerable<string> AskStreamingAsync(string question, List<ChatMessage>? conversationHistory = null);
    Task<StreamingAgentResponse> AskStreamingFullAsync(string question, List<ChatMessage>? conversationHistory = null);
}
