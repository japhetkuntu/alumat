namespace Umat.Alumni.Member.Api.Models;

public record CreateThreadRequest(string CategoryId, string Title, string Content);
public record CreateForumPostRequest(string ThreadId, string Content);
