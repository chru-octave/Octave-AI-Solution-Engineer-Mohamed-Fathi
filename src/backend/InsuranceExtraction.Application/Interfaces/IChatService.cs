using InsuranceExtraction.Application.Models;

namespace InsuranceExtraction.Application.Interfaces;

public interface IChatService
{
    Task<ChatResponse> ChatAsync(List<ChatMessage> messages);
}
