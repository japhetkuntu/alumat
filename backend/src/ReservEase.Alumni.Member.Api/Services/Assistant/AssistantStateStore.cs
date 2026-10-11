using ReservEase.Alumni.PaymentCallbacks.Sdk.Options;
using ReservEase.Alumni.Redis.Sdk.Services;

namespace ReservEase.Alumni.Member.Api.Services.Assistant;

/// <summary>Conversation state in Redis for thirty minutes. A Redis outage reads as "new conversation": the user sees the menu again.</summary>
public class AssistantStateStore(IRedisService<MemberRedisConfig> redis) : IAssistantStateStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    private static string Key(string phone) => $"wa:state:{phone}";

    public Task<AssistantState?> GetAsync(string phone) => redis.GetAsync<AssistantState>(Key(phone));
    public Task SetAsync(string phone, AssistantState state) => redis.SetAsync(Key(phone), state, Lifetime);
}
