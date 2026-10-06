namespace ReservEase.Alumni.PostgresDb.Sdk.Entities.Alumni;

/// <summary>Validation shared by every place a community's channels can be edited (member leaders and institution admins).</summary>
public static class CommunityChannelRules
{
    public const int MaxChannels = 5;

    /// <summary>
    /// Turns submitted channels into the stored list. Returns null (and an error) when anything is invalid; an empty
    /// list means "disconnect everything". Keeps the original connected date and connector for unchanged entries.
    /// </summary>
    public static List<CommunityChannel>? Parse(
        IEnumerable<(string? Type, string? DisplayName, string? InviteUrl)>? submitted,
        List<CommunityChannel>? existing, string actorId, out string? error)
    {
        error = null;
        var result = new List<CommunityChannel>();
        foreach (var (type, name, url) in submitted ?? [])
        {
            var channelType = string.IsNullOrWhiteSpace(type) ? CommunityChannel.WhatsApp : type.Trim();
            if (channelType != CommunityChannel.WhatsApp) { error = "Only WhatsApp groups can be connected for now."; return null; }
            var display = name?.Trim();
            if (string.IsNullOrEmpty(display)) { error = "Give the group a name."; return null; }
            if (display.Length > 100) { error = "The group name is too long."; return null; }
            var invite = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
            if (!CommunityChannel.IsValidInviteUrl(invite)) { error = "Use the group's WhatsApp invite link (it starts with https://chat.whatsapp.com/)."; return null; }
            var before = existing?.FirstOrDefault(c => c.Type == channelType && c.DisplayName == display && c.InviteUrl == invite);
            result.Add(before ?? new CommunityChannel { Type = channelType, DisplayName = display, InviteUrl = invite, ConnectedBy = actorId, ConnectedAt = DateTime.UtcNow });
        }
        if (result.Count > MaxChannels) { error = $"You can connect up to {MaxChannels} groups."; return null; }
        return result;
    }

    public static List<CommunityChannel>? Parse(IEnumerable<CommunityChannelSubmission>? submitted, List<CommunityChannel>? existing, string actorId, out string? error)
        => Parse(submitted?.Select(s => (s.Type, s.DisplayName, s.InviteUrl)), existing, actorId, out error);
}

public record CommunityChannelSubmission(string? Type, string? DisplayName, string? InviteUrl);
