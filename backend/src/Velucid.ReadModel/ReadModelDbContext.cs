using Microsoft.EntityFrameworkCore;
using Velucid.ReadModel.Entities;

namespace Velucid.ReadModel;

public sealed class ReadModelDbContext : DbContext
{
    public ReadModelDbContext(DbContextOptions<ReadModelDbContext> options) : base(options) { }

    public DbSet<UserProjection> UserProjections => Set<UserProjection>();
    public DbSet<UserIdentity> UserIdentities => Set<UserIdentity>();
    public DbSet<OrgProjection> OrgProjections => Set<OrgProjection>();
    public DbSet<OrgMemberProjection> OrgMemberProjections => Set<OrgMemberProjection>();
    public DbSet<OrgInvitationProjection> OrgInvitationProjections => Set<OrgInvitationProjection>();
    public DbSet<UserOrgProjection> UserOrgProjections => Set<UserOrgProjection>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureUserProjection(modelBuilder);
        ConfigureUserIdentity(modelBuilder);
        ConfigureOrgProjection(modelBuilder);
        ConfigureOrgMemberProjection(modelBuilder);
        ConfigureOrgInvitationProjection(modelBuilder);
        ConfigureUserOrgProjection(modelBuilder);

        modelBuilder.HasDefaultSchema("public");
        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureUserProjection(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserProjection>(e =>
        {
            e.ToTable("user_projection");
            e.HasKey(x => x.UserId);
            e.Property(x => x.DisplayName).IsRequired();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");
        });
    }

    private static void ConfigureUserIdentity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserIdentity>(e =>
        {
            e.ToTable("user_identity");
            e.HasKey(x => new { x.UserId, x.Sub });
            e.Property(x => x.Sub).IsRequired();
            e.Property(x => x.ProviderName).IsRequired();
            e.Property(x => x.LinkedAt).HasDefaultValueSql("NOW()");

            e.HasOne(x => x.User)
                .WithMany(x => x.Identities)
                .HasForeignKey(x => x.UserId);

            e.HasIndex(x => x.Sub).IsUnique();
            e.HasIndex(x => x.Email).HasFilter("email IS NOT NULL");
        });
    }

    private static void ConfigureOrgProjection(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrgProjection>(e =>
        {
            e.ToTable("org_projection");
            e.HasKey(x => x.OrgId);
            e.Property(x => x.Name).IsRequired();
            e.Property(x => x.IsDeleted).HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("NOW()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("NOW()");
        });
    }

    private static void ConfigureOrgMemberProjection(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrgMemberProjection>(e =>
        {
            e.ToTable("org_member_projection");
            e.HasKey(x => new { x.OrgId, x.UserId });
            e.Property(x => x.Role).IsRequired();
            e.Property(x => x.JoinedAt).HasDefaultValueSql("NOW()");

            e.HasOne<UserProjection>().WithMany()
                .HasForeignKey(x => x.UserId);
            e.HasOne<OrgProjection>().WithMany()
                .HasForeignKey(x => x.OrgId);

            e.HasIndex(x => x.UserId);
            e.ToTable(t => t.HasCheckConstraint("ck_org_member_role", "role IN ('Owner', 'Member')"));
        });
    }

    private static void ConfigureOrgInvitationProjection(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrgInvitationProjection>(e =>
        {
            e.ToTable("org_invitation_projection");
            e.HasKey(x => new { x.OrgId, x.Email });
            e.Property(x => x.Email).IsRequired();
            e.Property(x => x.Role).IsRequired();
            e.Property(x => x.Status).IsRequired();

            e.HasOne<OrgProjection>().WithMany()
                .HasForeignKey(x => x.OrgId);

            e.HasIndex(x => new { x.Email, x.Status });
            e.ToTable(t => t.HasCheckConstraint("ck_invitation_role", "role IN ('Owner', 'Member')"));
            e.ToTable(t => t.HasCheckConstraint("ck_invitation_status", "status IN ('Pending', 'Accepted', 'Declined')"));
        });
    }

    private static void ConfigureUserOrgProjection(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserOrgProjection>(e =>
        {
            e.ToTable("user_org_projection");
            e.HasKey(x => new { x.UserId, x.OrgId });
            e.Property(x => x.Role).IsRequired();

            e.HasOne<UserProjection>().WithMany()
                .HasForeignKey(x => x.UserId);
            e.HasOne<OrgProjection>().WithMany()
                .HasForeignKey(x => x.OrgId);
        });
    }
}