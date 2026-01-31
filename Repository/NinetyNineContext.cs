/// Copyright (c) 2020
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
using System.Text;
using Microsoft.EntityFrameworkCore;
using NinetyNine.Model;

namespace NinetyNine.Repository
{
    public class NinetyNineContext : DbContext
    {
        public NinetyNineContext(string dbConnectionString)
        {
            //this.Database.GetDbConnection().ConnectionString = dbConnectionString;
        }

        public NinetyNineContext(DbContextOptions options) : base(options)
        {

        }

        public DbSet<Player> Players { get; set; }
        public DbSet<Venue> Venues { get; set; }
        public DbSet<Game> Games { get; set; }
        public DbSet<Frame> Frames { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Game entity
            modelBuilder.Entity<Game>(entity =>
            {
                entity.HasKey(g => g.GameId);

                // Configure Game -> Frames relationship with cascade delete
                entity.HasMany(g => g.Frames)
                      .WithOne(f => f.Game)
                      .HasForeignKey(f => f.GameId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Configure Game -> Player relationship as optional
                entity.HasOne(g => g.Player)
                      .WithMany(p => p.Games)
                      .HasForeignKey(g => g.PlayerId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.SetNull);

                // Configure Game -> Venue relationship as optional
                entity.HasOne(g => g.LocationPlayed)
                      .WithMany()
                      .HasForeignKey(g => g.VenueId)
                      .IsRequired(false)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure Frame entity
            modelBuilder.Entity<Frame>(entity =>
            {
                entity.HasKey(f => f.FrameId);
            });

            // Configure Player entity
            modelBuilder.Entity<Player>(entity =>
            {
                entity.HasKey(p => p.PlayerId);
                entity.Ignore(p => p.Name); // Computed property
            });

            // Configure Venue entity
            modelBuilder.Entity<Venue>(entity =>
            {
                entity.HasKey(v => v.VenueId);
            });
        }
    }
}
