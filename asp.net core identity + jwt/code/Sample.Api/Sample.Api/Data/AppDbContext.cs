using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Sample.Api.Data
{
    public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<int>, int>
    {
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<AppUser>(b => { b.ToTable("AppUsers"); });

            builder.Entity<IdentityUserClaim<int>>(b => { b.ToTable("AppUserClaims"); });

            builder.Entity<IdentityUserLogin<int>>(b => { b.ToTable("AppUserLogins"); });

            builder.Entity<IdentityUserToken<int>>(b => { b.ToTable("AppUserTokens"); });

            builder.Entity<IdentityRole<int>>(b => { b.ToTable("AppRoles"); });

            builder.Entity<IdentityRoleClaim<int>>(b => { b.ToTable("AppRoleClaims"); });

            builder.Entity<IdentityUserRole<int>>(b => { b.ToTable("AppUserRoles"); });
        }
    }
}