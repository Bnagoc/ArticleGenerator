using Serilog;

namespace ArticleGenerator
{
    public static class ConfigureServices
    {
        public static void AddServices(this WebApplicationBuilder builder)
        {
            builder.AddSizeLimit();
            builder.AddSerilog();
            builder.AddDatabase();
            builder.AddCors();
        }

        private static void AddSizeLimit(this WebApplicationBuilder builder)
        {
            builder.WebHost.ConfigureKestrel((context, configuration) =>
            {
                configuration.Limits.MaxRequestBodySize = 100_000_000;
            });
        }

        private static void AddSerilog(this WebApplicationBuilder builder)
        {
            builder.Host.UseSerilog((context, configuration) =>
            {
                configuration.ReadFrom.Configuration(context.Configuration);
            });
        }

        private static void AddDatabase(this WebApplicationBuilder builder)
        {
            builder.Services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(builder.Configuration.GetConnectionString("Default"));
            });
        }

        private static void AddCors(this WebApplicationBuilder builder)
        {
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins("http://localhost:5173");
                    policy.AllowAnyHeader();
                    policy.AllowAnyMethod();
                    policy.AllowCredentials();
                });
            });
        }
    }
}
