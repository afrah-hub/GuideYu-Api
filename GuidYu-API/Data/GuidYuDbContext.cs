using GuidYu_API.Models;
using Microsoft.EntityFrameworkCore;

namespace GuidYu_API.Data;

public class GuidYuDbContext : DbContext
{
    public GuidYuDbContext(DbContextOptions<GuidYuDbContext> options) : base(options)
    {
    }

    public DbSet<Resume> Resumes { get; set; }
    


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Resume>(entity =>
        {
            entity.ToTable("Resume"); // Use the requested table name
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETDATE()");
        });


    }
}
