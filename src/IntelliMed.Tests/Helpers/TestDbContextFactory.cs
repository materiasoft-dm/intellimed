using IntelliMed.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliMed.Tests.Helpers;

public static class TestDbContextFactory
{
    public static AppDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        // The InMemory provider only applies OnModelCreating's HasData seed rows (e.g. HealthFunds)
        // once the database is explicitly created — unlike relational providers, it won't seed
        // implicitly on first query/SaveChanges.
        context.Database.EnsureCreated();
        return context;
    }
}