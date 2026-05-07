namespace Umat.Alumni.Mailtrap.Sdk.Models;

public class MailtrapResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
}
