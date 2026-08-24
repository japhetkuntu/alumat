namespace ReservEase.Alumni.Member.Api.Models;

/// <summary>Provide either CategoryId (institution-wide thread) or CommunityId (community-scoped thread) — not both.</summary>
public record CreateThreadRequest(string? CategoryId, string Title, string Content, string? CommunityId = null);
public record CreateForumPostRequest(string ThreadId, string Content);
