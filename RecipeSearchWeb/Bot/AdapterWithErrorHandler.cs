using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;

namespace RecipeSearchWeb.Bot;

/// <summary>
/// Bot adapter with error handling
/// </summary>
public class AdapterWithErrorHandler : CloudAdapter
{
    public AdapterWithErrorHandler(
        BotFrameworkAuthentication auth,
        ILogger<IBotFrameworkHttpAdapter> logger)
        : base(auth, logger)
    {
        logger.LogInformation("AdapterWithErrorHandler initialized");
        
        OnTurnError = async (turnContext, exception) =>
        {
            // Log the error
            logger.LogError(exception, "[OnTurnError] Unhandled error: {Message}", exception.Message);

            // Send error message to user
            var errorMessage = "Lo siento, ha ocurrido un error procesando tu solicitud. Por favor intenta de nuevo o contacta al IT Help Desk.";
            
            try
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(errorMessage, errorMessage));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send error message to user");
            }
        };
    }
}
