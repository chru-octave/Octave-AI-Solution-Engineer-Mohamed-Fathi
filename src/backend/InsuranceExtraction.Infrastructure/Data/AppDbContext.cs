using InsuranceExtraction.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InsuranceExtraction.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<Insured> Insureds => Set<Insured>();
    public DbSet<Broker> Brokers => Set<Broker>();
    public DbSet<CoverageLine> CoverageLines => Set<CoverageLine>();
    public DbSet<Exposure> Exposures => Set<Exposure>();
    public DbSet<LossHistory> LossHistory => Set<LossHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Submission>(e =>
        {
            e.HasKey(s => s.SubmissionId);
            e.HasOne(s => s.Insured).WithMany(i => i.Submissions).HasForeignKey(s => s.InsuredId);
            e.HasOne(s => s.Broker).WithMany(b => b.Submissions).HasForeignKey(s => s.BrokerId);
        });

        modelBuilder.Entity<CoverageLine>(e =>
        {
            e.HasKey(c => c.CoverageId);
            e.HasOne(c => c.Submission).WithMany(s => s.CoverageLines).HasForeignKey(c => c.SubmissionId);
        });

        modelBuilder.Entity<Exposure>(e =>
        {
            e.HasKey(ex => ex.ExposureId);
            e.HasOne(ex => ex.Submission).WithMany(s => s.Exposures).HasForeignKey(ex => ex.SubmissionId);
        });

        modelBuilder.Entity<LossHistory>(e =>
        {
            e.HasKey(l => l.LossId);
            e.HasOne(l => l.Submission).WithMany(s => s.Losses).HasForeignKey(l => l.SubmissionId);
        });
    }
}
