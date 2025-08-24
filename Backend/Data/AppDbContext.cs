namespace ArticleGenerator.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<Product> Products { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureUsersTable(modelBuilder);
            base.OnModelCreating(modelBuilder);
        }

        private static void ConfigureUsersTable(ModelBuilder modelBuilder)
        {
            var builder = modelBuilder.Entity<Product>();

            builder.HasIndex(x => x.Id)
                .IsUnique();
        }

    }
}
