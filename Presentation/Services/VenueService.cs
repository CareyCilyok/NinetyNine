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
using System.Linq;
using System.Threading.Tasks;
using NinetyNine.Model;

namespace NinetyNine.Presentation.Services
{
    /// <summary>
    /// Service for managing venues and location data
    /// </summary>
    public class VenueService : IVenueService
    {
        private readonly List<Venue> _venues;
        private readonly Random _random;

        public VenueService()
        {
            _random = new Random();
            _venues = GenerateMockVenues();
        }

        /// <summary>
        /// Gets all venues
        /// </summary>
        public async Task<List<Venue>> GetAllVenuesAsync()
        {
            return await Task.FromResult(_venues.ToList());
        }

        /// <summary>
        /// Gets a venue by ID
        /// </summary>
        public async Task<Venue?> GetVenueByIdAsync(Guid venueId)
        {
            var venue = _venues.FirstOrDefault(v => v.VenueId == venueId);
            return await Task.FromResult(venue);
        }

        /// <summary>
        /// Creates a new venue
        /// </summary>
        public async Task<Venue> CreateVenueAsync(Venue venue)
        {
            venue.VenueId = Guid.NewGuid();
            _venues.Add(venue);
            return await Task.FromResult(venue);
        }

        /// <summary>
        /// Updates an existing venue
        /// </summary>
        public async Task<Venue> UpdateVenueAsync(Venue venue)
        {
            var existingVenue = _venues.FirstOrDefault(v => v.VenueId == venue.VenueId);
            if (existingVenue != null)
            {
                var index = _venues.IndexOf(existingVenue);
                _venues[index] = venue;
            }
            return await Task.FromResult(venue);
        }

        /// <summary>
        /// Deletes a venue
        /// </summary>
        public async Task<bool> DeleteVenueAsync(Guid venueId)
        {
            var venue = _venues.FirstOrDefault(v => v.VenueId == venueId);
            if (venue != null)
            {
                _venues.Remove(venue);
                return await Task.FromResult(true);
            }
            return await Task.FromResult(false);
        }

        /// <summary>
        /// Gets venue statistics
        /// </summary>
        public async Task<VenueStatistics> GetVenueStatisticsAsync(Guid venueId)
        {
            var venue = _venues.FirstOrDefault(v => v.VenueId == venueId);
            if (venue == null)
            {
                return await Task.FromResult(new VenueStatistics());
            }

            // Mock statistics
            var stats = new VenueStatistics
            {
                TotalGamesPlayed = _random.Next(20, 100),
                AverageScore = _random.Next(60, 85) + _random.NextDouble(),
                UniquePlayersCount = _random.Next(5, 25),
                FirstGameDate = DateTime.Now.AddDays(-_random.Next(30, 365)),
                LastGameDate = DateTime.Now.AddDays(-_random.Next(0, 7))
            };

            // Mock table usage
            stats.TableUsage[TableSize.SevenFoot] = _random.Next(0, 20);
            stats.TableUsage[TableSize.NineFoot] = _random.Next(10, 40);
            stats.TableUsage[TableSize.TenFoot] = _random.Next(0, 15);

            return await Task.FromResult(stats);
        }

        /// <summary>
        /// Gets popular venues by game count
        /// </summary>
        public async Task<List<PopularVenue>> GetPopularVenuesAsync(int limit = 10)
        {
            var popularVenues = _venues.Take(limit).Select(venue => new PopularVenue
            {
                VenueId = venue.VenueId,
                Name = venue.Name,
                Address = venue.Address ?? "Address not available",
                TotalGames = _random.Next(20, 100),
                AverageScore = _random.Next(60, 85) + _random.NextDouble(),
                UniquePlayersCount = _random.Next(5, 25),
                LastGameDate = DateTime.Now.AddDays(-_random.Next(0, 30))
            }).OrderByDescending(v => v.TotalGames).ToList();

            return await Task.FromResult(popularVenues);
        }

        /// <summary>
        /// Searches venues by name or location
        /// </summary>
        public async Task<List<Venue>> SearchVenuesAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return await GetAllVenuesAsync();
            }

            var results = _venues.Where(v => 
                v.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                (v.Address?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();

            return await Task.FromResult(results);
        }

        #region Private Methods

        private List<Venue> GenerateMockVenues()
        {
            return new List<Venue>
            {
                new Venue
                {
                    VenueId = Guid.NewGuid(),
                    Name = "Home Table",
                    Address = "123 Main Street, Springfield, IL 62701",
                    PhoneNumber = "+1 (555) 123-4567",
                    Private = true
                },
                new Venue
                {
                    VenueId = Guid.NewGuid(),
                    Name = "Downtown Pool Hall",
                    Address = "456 Oak Avenue, Springfield, IL 62702",
                    PhoneNumber = "+1 (555) 987-6543",
                    Private = false
                },
                new Venue
                {
                    VenueId = Guid.NewGuid(),
                    Name = "Sports Bar & Grill",
                    Address = "789 Elm Street, Springfield, IL 62703",
                    PhoneNumber = "+1 (555) 555-0123",
                    Private = false
                },
                new Venue
                {
                    VenueId = Guid.NewGuid(),
                    Name = "Billiards Palace",
                    Address = "321 Pine Road, Springfield, IL 62704",
                    PhoneNumber = "+1 (555) 111-2222",
                    Private = false
                },
                new Venue
                {
                    VenueId = Guid.NewGuid(),
                    Name = "Corner Pocket",
                    Address = "654 Maple Drive, Springfield, IL 62705",
                    PhoneNumber = "+1 (555) 333-4444",
                    Private = false
                },
                new Venue
                {
                    VenueId = Guid.NewGuid(),
                    Name = "Championship Billiards",
                    Address = "987 Cedar Lane, Springfield, IL 62706",
                    PhoneNumber = "+1 (555) 777-8888",
                    Private = false
                }
            };
        }

        #endregion
    }
}