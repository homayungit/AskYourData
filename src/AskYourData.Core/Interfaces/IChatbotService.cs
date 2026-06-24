using AskYourData.Core.Models;

namespace AskYourData.Core.Interfaces;

public interface IChatbotService
{
    Task<ChatResponse> AskAsync(ChatRequest request, CancellationToken cancellationToken = default);
}
