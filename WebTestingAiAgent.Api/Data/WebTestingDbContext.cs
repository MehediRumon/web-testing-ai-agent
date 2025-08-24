using Microsoft.EntityFrameworkCore;
using WebTestingAiAgent.Core.Models;
using System.Text.Json;

namespace WebTestingAiAgent.Api.Data;

public class WebTestingDbContext : DbContext
{
    public WebTestingDbContext(DbContextOptions<WebTestingDbContext> options) : base(options)
    {
    }

    public DbSet<TestCase> TestCases { get; set; } = null!;
    public DbSet<RecordedStep> RecordedSteps { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure TestCase entity
        modelBuilder.Entity<TestCase>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).IsRequired();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.BaseUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            entity.Property(e => e.Format).IsRequired();
            
            // Convert Tags list to JSON string for storage
            entity.Property(e => e.Tags)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                    v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>());

            // Configure relationship with RecordedSteps
            entity.HasMany(e => e.Steps)
                .WithOne()
                .HasForeignKey("TestCaseId")
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure RecordedStep entity
        modelBuilder.Entity<RecordedStep>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).IsRequired();
            entity.Property(e => e.Order).IsRequired();
            entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ElementSelector).HasMaxLength(1000);
            entity.Property(e => e.Value).HasMaxLength(2000);
            entity.Property(e => e.Url).HasMaxLength(500);
            entity.Property(e => e.Timestamp).IsRequired();
            
            // Convert Metadata dictionary to JSON string for storage
            entity.Property(e => e.Metadata)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, JsonSerializerOptions.Default) ?? new Dictionary<string, object>());

            // Add foreign key for TestCase relationship
            entity.Property<string>("TestCaseId");
            entity.HasIndex("TestCaseId");
        });
    }
}