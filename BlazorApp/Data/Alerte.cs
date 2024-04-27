using System.ComponentModel.DataAnnotations;

public class Alerte
{
    /// <summary>
    /// Id of the alert
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Title of the alert
    /// </summary>
    [MaxLength(255)]
    public string Title { get; set; } = "";

    /// <summary>
    /// Description of the alert
    /// </summary>
    [MaxLength(1024)]
    public string Description { get; set; } = "";

    /// <summary>
    /// List of emails separated by ; to send the alert to
    /// </summary>
    [MaxLength(1024)]
    public string SendTo { get; set; } = "";

    /// <summary>
    /// List of phone numbers separated by ; to send the alert to
    /// </summary>
    [MaxLength(1024)]
    public string TextTo { get; set; } = "";

    /// <summary>
    /// List of keywords separated by ;
    /// </summary>
    [MaxLength(1024)]
    public string Keywords { get; set; } = "";

    /// <summary>
    /// List of keywords separated by ; that will be removed before looking for keywords in the text
    /// </summary>
    [MaxLength(1024)]
    public string? RemoveKeywords { get; set; }

    /// <summary>
    /// Id of the owner of the alert. In the context of ErabliereAPI, this is the id of the erabliere.
    /// </summary>
    [MaxLength(100)]
    public string? ExternalOwnerId { get; set; }

    /// <summary>
    /// SubId of the owner of the alert. In the context of ErabliereAPI, this is the subId of the ImageSensor.
    /// </summary>
    [MaxLength(100)]
    public string? ExternalOwnerSubId { get; set; }

    /// <summary>
    /// Create that of the alert
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Update date of the alert
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Name of the user who create the alert
    /// </summary>
    [MaxLength(255)]
    public string CreateBy { get; set; } = "";

    /// <summary>
    /// Name of the user who update the alert
    /// </summary>
    [MaxLength(255)]
    public string UpdateBy { get; set; } = "";
}