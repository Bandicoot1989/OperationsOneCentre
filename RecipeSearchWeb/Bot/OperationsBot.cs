using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using RecipeSearchWeb.Services;
using System.Text.RegularExpressions;

namespace RecipeSearchWeb.Bot;

/// <summary>
/// Operations One Centre Bot for Microsoft Teams
/// Uses KnowledgeAgentService to answer IT-related questions
/// </summary>
public class OperationsBot : ActivityHandler
{
    private readonly KnowledgeAgentService _agentService;
    private readonly ILogger<OperationsBot> _logger;

    public OperationsBot(
        KnowledgeAgentService agentService,
        ILogger<OperationsBot> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== BOT RECEIVED MESSAGE ===");
        
        var userMessage = turnContext.Activity.Text;
        _logger.LogInformation("Raw message: {Message}", userMessage);
        
        // Remove bot mention from message (Teams includes @mention)
        userMessage = RemoveBotMention(userMessage, turnContext.Activity);
        _logger.LogInformation("Cleaned message: {Message}", userMessage);
        
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            _logger.LogInformation("Empty message, sending greeting");
            await turnContext.SendActivityAsync(
                MessageFactory.Text("¡Hola! Soy el **Operations One Centre Bot**. ¿En qué puedo ayudarte hoy?"),
                cancellationToken);
            return;
        }

        _logger.LogInformation("Teams Bot received message: {Message}", userMessage);

        try
        {
            // Show typing indicator
            _logger.LogInformation("Sending typing indicator");
            await turnContext.SendActivitiesAsync(new Activity[] { new Activity { Type = ActivityTypes.Typing } }, cancellationToken);

            _logger.LogInformation("Calling KnowledgeAgentService");
            // Get response from Knowledge Agent
            var response = await _agentService.AskAsync(userMessage);
            _logger.LogInformation("KnowledgeAgentService response: Success={Success}", response.Success);

            if (response.Success)
            {
                // Convert markdown to Teams-compatible format
                var teamsMessage = ConvertToTeamsFormat(response.Answer);
                
                await turnContext.SendActivityAsync(
                    MessageFactory.Text(teamsMessage),
                    cancellationToken);
            }
            else
            {
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("Lo siento, no pude procesar tu consulta. Por favor intenta de nuevo o contacta al IT Help Desk."),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Teams message");
            await turnContext.SendActivityAsync(
                MessageFactory.Text("Ha ocurrido un error. Por favor intenta de nuevo más tarde."),
                cancellationToken);
        }
    }

    protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
    {
        foreach (var member in membersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                var welcomeMessage = @"¡Hola! 👋 Soy el **Operations One Centre Bot**, tu asistente de IT.

Puedo ayudarte con:
• Información sobre **portales de clientes** (BMW, VW, Ford, etc.)
• Problemas de **acceso remoto y Zscaler**
• Consultas sobre **SAP, Teamcenter, PLM**
• **Tickets de soporte** y procedimientos IT

¿En qué puedo ayudarte hoy?";

                await turnContext.SendActivityAsync(
                    MessageFactory.Text(welcomeMessage),
                    cancellationToken);
            }
        }
    }

    /// <summary>
    /// Remove @mention from the message text
    /// </summary>
    private string RemoveBotMention(string text, IMessageActivity activity)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Remove mentions from Teams
        if (activity.Entities != null)
        {
            foreach (var entity in activity.Entities)
            {
                if (entity.Type == "mention")
                {
                    var mention = entity.GetAs<Mention>();
                    if (mention != null && !string.IsNullOrEmpty(mention.Text))
                    {
                        text = text.Replace(mention.Text, "").Trim();
                    }
                }
            }
        }

        // Also try regex to remove <at>...</at> tags
        text = Regex.Replace(text, @"<at>.*?</at>", "", RegexOptions.IgnoreCase).Trim();
        
        return text;
    }

    /// <summary>
    /// Convert standard markdown to Teams-compatible format
    /// </summary>
    private string ConvertToTeamsFormat(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return markdown;

        // Teams uses similar markdown, but some adjustments may be needed
        // Convert standard links to Teams format
        // Most markdown should work as-is in Teams
        
        return markdown;
    }
}
