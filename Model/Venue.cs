using System;

namespace NinetyNine.Model
{
    public class Venue
    {
        public Guid VenueId { get; set; }

        public string Name { get; set; } = String.Empty;
        public string Address { get; set; } = String.Empty;
        public string PhoneNumber { get; set; } = String.Empty;
    }
}