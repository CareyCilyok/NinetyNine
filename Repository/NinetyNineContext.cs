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

        public DbSet<Player> Players { get; set; }
        public DbSet<Venue> Venues { get; set; }
        public DbSet<Game> Games { get; set; }
    }
}
