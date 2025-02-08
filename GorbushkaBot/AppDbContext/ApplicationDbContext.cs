using GorbushkaBot.Model;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace GorbushkaBot.AppDbContext
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<UserApplication> UserApplications { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserApplication>().ToTable("user_applications");
        }
    }
}
