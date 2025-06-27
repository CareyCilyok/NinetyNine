using Microsoft.EntityFrameworkCore;
using NinetyNine.Model;

namespace Services;

public class NinetyNineContext : DbContext
{
    public NinetyNineContext(DbContextOptions<NinetyNineContext> options) :
        base(options)
    {}

    public DbSet<Game> Games { get; set; }
    
    public DbSet<Player> Players { get; set; }
    
    public DbSet<Venue> Venues { get; set; }
}