using Microsoft.EntityFrameworkCore;

namespace NinetyNine.Repository
{
    public class LocalContext : NinetyNineContext
    {
        public LocalContext() : base("LocalDatabase") { }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite(@"Data Source=NinetyNine.db");
    }
}
