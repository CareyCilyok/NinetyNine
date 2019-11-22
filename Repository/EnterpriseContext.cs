using System;
using System.Configuration;
using Microsoft.EntityFrameworkCore;

namespace NinetyNine.Repository
{
    public class EnterpriseContext : NinetyNineContext
    {
        public EnterpriseContext() : base("EnterpriseDatabase") { }

        protected override void OnConfiguring(DbContextOptionsBuilder options) =>
            options.UseSqlServer(@"Data Source=.\sqlexpress; Initial Catalog = NinetyNine; Integrated Security = True; MultipleActiveResultSets=True");
    }
}
