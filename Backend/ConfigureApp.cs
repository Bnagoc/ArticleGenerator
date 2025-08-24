using Serilog;

namespace ArticleGenerator
{
    public static class ConfigureApp
    {
        public static async Task Configure(this WebApplication app)
        {
            app.UseSerilogRequestLogging();
            app.UseRouting();
            app.UseCors();
            app.Use(async (context, next) =>
            {
                context.Request.EnableBuffering();
                await next();
            });
            app.MapEndpoints();
            await app.EnsureDatabaseCreated();
        }

        private static async Task EnsureDatabaseCreated(this WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
        }
    }
}
