using System.ComponentModel.DataAnnotations;

namespace BlazorApp.Data;

public class EmailStates
{
    public Guid Id { get; set; }

    [MaxLength(1024)]
    public string? Email { get; set; }

    public int MessagesCount { get; set; }

    public long Size { get; set; }
}