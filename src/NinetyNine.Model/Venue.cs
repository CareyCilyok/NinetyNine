namespace NinetyNine.Model;

/// <summary>
/// A pool hall or other venue where games are played.
/// </summary>
public class Venue
{
    public Guid VenueId { get; set; } = Guid.NewGuid();

    /// <summary>When true, the venue is only visible to its creator.</summary>
    public bool Private { get; set; }

    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
}
