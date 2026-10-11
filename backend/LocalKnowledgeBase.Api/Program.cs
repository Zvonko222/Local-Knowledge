using LocalKnowledgeBase.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Get or create the data directory
var dataDir = KnowledgeBaseService.GetUserDataDirectory();
if (string.IsNullOrEmpty(dataDir))
{
    // Default to a folder under the app directory
    dataDir = Path.Combine(AppContext.BaseDirectory, "data");
    Directory.CreateDirectory(dataDir);
}

var kbService = new KnowledgeBaseService(dataDir);
builder.Services.AddSingleton(kbService);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
