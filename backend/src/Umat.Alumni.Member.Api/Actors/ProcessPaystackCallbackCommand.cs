namespace Umat.Alumni.Member.Api.Actors;

public sealed class ProcessPaystackCallbackCommand
{
    public string Reference { get; }
    public string RawBody { get; }

    public ProcessPaystackCallbackCommand(string reference, string rawBody)
    {
        Reference = reference;
        RawBody = rawBody;
    }
}
