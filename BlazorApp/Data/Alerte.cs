public class Alerte
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string SendTo { get; set; } = "";
    public string TextTo { get; set; } = "";

    /// <summary>
    /// List of keywords separated by ;
    /// </summary>
    public string Keywords { get; set; } = "";

    public string? ExternalOwnerId { get; set; }

    public string? ExternalOwnerSubId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string CreateBy { get; set; } = "";

    public string UpdateBy { get; set; } = "";
}