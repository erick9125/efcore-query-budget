using EfCoreQueryBudget;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=query_budget_sample;Username=postgres;Password=postgres";

builder.Services.AddEfCoreQueryBudget(options =>
{
    // Capture is a test and diagnostics concern. Registering the interceptor everywhere but
    // leaving it inert in production keeps a single composition root.
    options.Enabled = !builder.Environment.IsProduction();
});

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    options
        .UseNpgsql(connectionString)
        .AddInterceptors(
            serviceProvider.GetRequiredService<QueryBudgetCommandInterceptor>());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    if (!await db.Authors.AnyAsync())
    {
        var alice = new Author { Name = "Alice" };
        var bob = new Author { Name = "Bob" };
        db.Authors.AddRange(alice, bob);
        db.Posts.AddRange(
            new Post { Title = "One", Author = alice },
            new Post { Title = "Two", Author = alice },
            new Post { Title = "Three", Author = bob });
        await db.SaveChangesAsync();
    }
}

app.MapGet("/api/posts/problematic", async (AppDbContext db) =>
{
    var posts = await db.Posts.ToListAsync();
    foreach (var post in posts)
    {
        post.Author = await db.Authors.SingleAsync(x => x.Id == post.AuthorId);
    }

    return posts.Select(p => new { p.Id, p.Title, Author = p.Author!.Name });
});

app.MapGet("/api/posts/optimized", async (AppDbContext db) =>
{
    var posts = await db.Posts
        .Include(x => x.Author)
        .ToListAsync();

    return posts.Select(p => new { p.Id, p.Title, Author = p.Author!.Name });
});

app.Run();

public partial class Program;

public sealed class Author
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Post> Posts { get; set; } = [];
}

public sealed class Post
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int AuthorId { get; set; }
    public Author? Author { get; set; }
}

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Author> Authors => Set<Author>();
    public DbSet<Post> Posts => Set<Post>();
}
