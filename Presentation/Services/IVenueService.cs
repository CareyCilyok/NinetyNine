/// Copyright (c) 2020-2022
///
/// Permission is hereby granted, free of charge, to any person obtaining a copy
/// of this software and associated documentation files (the "Software"), to deal
/// in the Software without restriction, including without limitation the rights
/// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
/// copies of the Software, and to permit persons to whom the Software is
/// furnished to do so, subject to the following conditions:
/// 
/// The above copyright notice and this permission notice shall be included in all
/// copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
/// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
/// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
/// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
/// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
/// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NinetyNine.Model;

namespace NinetyNine.Presentation.Services
{
    /// <summary>
    /// Service for managing venues and location data
    /// </summary>
    public interface IVenueService
    {
        /// <summary>
        /// Gets all venues
        /// </summary>
        /// <returns>List of venues</returns>
        Task<List<Venue>> GetAllVenuesAsync();

        /// <summary>
        /// Gets a venue by ID
        /// </summary>
        /// <param name="venueId">Venue ID</param>
        /// <returns>Venue or null if not found</returns>
        Task<Venue?> GetVenueByIdAsync(Guid venueId);

        /// <summary>
        /// Creates a new venue
        /// </summary>
        /// <param name="venue">Venue to create</param>
        /// <returns>Created venue with ID</returns>
        Task<Venue> CreateVenueAsync(Venue venue);

        /// <summary>
        /// Updates an existing venue
        /// </summary>
        /// <param name="venue">Venue to update</param>
        /// <returns>Updated venue</returns>
        Task<Venue> UpdateVenueAsync(Venue venue);

        /// <summary>
        /// Deletes a venue
        /// </summary>
        /// <param name="venueId">Venue ID to delete</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteVenueAsync(Guid venueId);

        /// <summary>
        /// Gets venue statistics
        /// </summary>
        /// <param name="venueId">Venue ID</param>
        /// <returns>Venue statistics</returns>
        Task<VenueStatistics> GetVenueStatisticsAsync(Guid venueId);

        /// <summary>
        /// Gets popular venues by game count
        /// </summary>
        /// <param name="limit">Maximum number of venues to return</param>
        /// <returns>List of popular venues</returns>
        Task<List<PopularVenue>> GetPopularVenuesAsync(int limit = 10);

        /// <summary>
        /// Searches venues by name or location
        /// </summary>
        /// <param name="searchTerm">Search term</param>
        /// <returns>List of matching venues</returns>
        Task<List<Venue>> SearchVenuesAsync(string searchTerm);
    }

    /// <summary>
    /// Popular venue summary for display
    /// </summary>
    public class PopularVenue
    {
        public Guid VenueId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int TotalGames { get; set; }
        public double AverageScore { get; set; }
        public int UniquePlayersCount { get; set; }
        public DateTime LastGameDate { get; set; }
        public string LastGameText => $"Last game: {LastGameDate:MMM dd}";
        public string PopularityText => $"{TotalGames} games • {UniquePlayersCount} players";
    }
}