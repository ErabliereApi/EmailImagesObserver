using System.ComponentModel.DataAnnotations;

/// <summary>
/// Mapping between a key and a value for an incoming email image
/// </summary>
public class Mapping
{
    public Guid Id { get; set; }

    /// <summary>
    /// Name of the mapping
    /// </summary>
    [MaxLength(255)]
    public string? Name { get; set; }

    /// <summary>
    /// Filter to apply to match the mapping. This is the email address of the sender
    /// </summary>
    [MaxLength(1024)]
    public string? Filter { get; set; }

    /// <summary>
    /// SubFilter to apply to match the mapping. This is a text to search in the email content
    /// </summary>
    [MaxLength(1024)]
    public string? SubFilter { get; set; }

    /// <summary>
    /// Key to match the mapping. This is a text to search in the subject of the email
    /// </summary>
    [MaxLength(1024)]
    public string? Key { get; set; }

    /// <summary>
    /// The ExternalOwnerId to set when the mapping is matched.
    /// In the context of ErabliereAPI, this is the id of the erabliere.
    /// </summary>
    public Guid? Value { get; set; }

    /// <summary>
    /// The ExternalOwnerSubId to set when the mapping is matched.
    /// In the context of ErabliereAPI, this is the subId of the ImageSensor.
    /// </summary>
    public Guid? SubValue { get; set; }
}