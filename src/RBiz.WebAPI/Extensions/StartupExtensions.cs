using Microsoft.EntityFrameworkCore;
using RBiz.Infrastructure.Persistence;

namespace RBiz.WebAPI.Extensions;

/// <summary>
/// Optional startup work against the database: applying pending migrations and inserting
/// the first tenant and administrator.
/// </summary>
internal static class StartupExtensions
{
    /// <summary>
    /// Runs migrations and seeding according to the "Database" configuration section.
    ///
    /// Both are off by default. Migrating from inside the application is convenient on a
    /// developer machine but a poor idea in production, where two instances starting at
    /// once would race and a schema change would be applied with no review step - there,
    /// run "dotnet ef database update" as a deployment step instead.
    /// </summary>
    public static async Task InitialiseDatabaseAsync(this WebApplication app)
    {
        var section = app.Configuration.GetSection("Database");

        var applyMigrations = section.GetValue("ApplyMigrationsOnStartup", false);
        var seed = section.GetValue("SeedOnStartup", false);

        if (!applyMigrations && !seed)
        {
            return;
        }

        // A scope of its own: the scoped DbContext cannot be resolved from the root provider.
        await using var scope = app.Services.CreateAsyncScope();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        if (applyMigrations)
        {
            logger.LogInformation("Applying pending database migrations.");

            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.MigrateAsync();
        }

        if (seed)
        {
            var seedSection = section.GetSection("Seed");

            var fullName = seedSection.GetValue<string>("FullName");
            var mobile = seedSection.GetValue<string>("Mobile");
            var password = seedSection.GetValue<string>("Password");
            var email = seedSection.GetValue<string>("Email");

            // Rather than invent a default password, refuse to seed without one. A
            // well-known fallback credential is how a demo account reaches production.
            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(mobile) ||
                string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning(
                    "Database:SeedOnStartup is enabled but the Database:Seed values are " +
                    "incomplete, so seeding was skipped.");

                return;
            }

            var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();

            await seeder.SeedAsync(fullName, mobile, password, email);
        }
    }
}
