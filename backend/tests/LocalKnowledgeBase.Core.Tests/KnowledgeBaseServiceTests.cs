using LocalKnowledgeBase.Core;
using LocalKnowledgeBase.Core.Services;

namespace LocalKnowledgeBase.Core.Tests;

public sealed class KnowledgeBaseServiceTests : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(
        Path.GetTempPath(),
        "LocalKnowledgeBase.Core.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void DocumentCrud_PersistsChangesAndDeletesRawFile()
    {
        var rawDirectory = Path.Combine(_dataDirectory, "raw_files");
        Directory.CreateDirectory(rawDirectory);

        var document = new Document
        {
            Title = "Initial title",
            FileType = ".md",
            OriginalFileName = "notes.md",
            StoredFileName = Guid.NewGuid().ToString("N") + ".md",
            Content = "Initial content"
        };
        File.WriteAllText(Path.Combine(rawDirectory, document.StoredFileName), document.Content);

        using (var service = new KnowledgeBaseService(_dataDirectory))
        {
            service.AddDocument(document);

            var updated = service.UpdateDocument(document.Id, "Updated title", "Updated content", "docs, local");
            Assert.NotNull(updated);
            Assert.Equal("Updated title", updated.Title);
            Assert.Equal("Updated content", updated.Content);
            Assert.Equal("docs, local", updated.Tags);
            Assert.Equal(15, updated.FileSize);
        }

        using (var reloadedService = new KnowledgeBaseService(_dataDirectory))
        {
            var reloaded = reloadedService.GetDocument(document.Id);
            Assert.NotNull(reloaded);
            Assert.Equal("Updated title", reloaded.Title);

            Assert.True(reloadedService.DeleteDocument(document.Id));
            Assert.False(File.Exists(Path.Combine(rawDirectory, document.StoredFileName)));
        }
    }

    [Fact]
    public void MemoryCrud_PersistsUpdates()
    {
        var entry = new MemoryEntry
        {
            Title = "Decision",
            Content = "Keep storage local"
        };

        using (var service = new KnowledgeBaseService(_dataDirectory))
        {
            service.AddMemoryEntry(entry);

            var updated = service.UpdateMemoryEntry(entry.Id, "Updated decision", "Keep JSON storage local", "architecture, storage");
            Assert.NotNull(updated);
            Assert.Equal("architecture, storage", updated.Tags);
        }

        using (var reloadedService = new KnowledgeBaseService(_dataDirectory))
        {
            var reloaded = reloadedService.GetMemoryEntry(entry.Id);
            Assert.NotNull(reloaded);
            Assert.Equal("Updated decision", reloaded.Title);
            Assert.True(reloadedService.DeleteMemoryEntry(entry.Id));
        }
    }

    [Fact]
    public void Search_RespectsMaximumResultCount()
    {
        using var service = new KnowledgeBaseService(_dataDirectory);
        service.AddDocument(new Document { Title = "One", Content = "shared text" });
        service.AddDocument(new Document { Title = "Two", Content = "shared text" });

        var results = service.SearchDocuments("shared", 1);

        Assert.Single(results);
    }

    [Fact]
    public void Reconfigure_LoadsDataFromNewDirectory()
    {
        var secondDirectory = Path.Combine(_dataDirectory, "second");
        using var service = new KnowledgeBaseService(_dataDirectory);
        service.AddDocument(new Document { Title = "First", Content = "first" });

        service.Reconfigure(secondDirectory);

        Assert.Empty(service.ListDocuments());
        Assert.Equal(Path.GetFullPath(secondDirectory), service.DataDirectory);
    }

    [Fact]
    public void ChunkText_UsesRequestedSizeAndOverlap()
    {
        var chunks = KnowledgeBaseService.ChunkText(new string('a', 120), chunkSize: 50, overlap: 10);

        Assert.Equal(3, chunks.Count);
        Assert.Equal(50, chunks[0].Length);
        Assert.Equal(50, chunks[1].Length);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dataDirectory))
            Directory.Delete(_dataDirectory, true);
    }
}
