using GorbushkaBot.Model;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace GorbushkaBot.AppDbContext
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<UserApplication> UserApplications { get; set; }
        public DbSet<UserAccept> UserAccepts { get; set; }
        public DbSet<PinnedMessage> PinnedMessages { get; set; }
        
        public DbSet<BlacklistEntry> BlacklistEntries { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserApplication>().ToTable("user_applications");
            modelBuilder.Entity<UserAccept>().ToTable("user_accepts");
            modelBuilder.Entity<BlacklistEntry>().ToTable("black_list");
            modelBuilder.Entity<PinnedMessage>().ToTable("pinned_message");
            modelBuilder.Entity<BlacklistEntry>()
            .HasKey(b => b.Id);
        }
    }
}
