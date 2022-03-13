namespace BlazorApp.Data;

public class EmailStates
{
    public Guid Id { get; set; }

    public string? Email { get; set; }

    public int MessagesCount { get; set; }

    public long Size { get; set; }
}