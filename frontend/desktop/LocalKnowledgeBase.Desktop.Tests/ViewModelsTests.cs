using System.IO;
using LocalKnowledgeBase.Desktop.Models;
using LocalKnowledgeBase.Desktop.Services;
using LocalKnowledgeBase.Desktop.Tests.Fakes;
using LocalKnowledgeBase.Desktop.ViewModels;
using Xunit;

namespace LocalKnowledgeBase.Desktop.Tests;

public sealed class DocumentsViewModelTests
{
    private static DocumentsViewModel CreateViewModel(FakeKnowledgeApiClient api, FakeDialogService dialogs) =>
        new(api, dialogs, new StatusReporter());

    [Fact]
    public async Task Load_PopulatesDocumentsFromApi()
    {
        var api = new FakeKnowledgeApiClient();
        await api.AddDocumentAsync("架构笔记", "notes.md", [1, 2, 3]);
        var viewModel = CreateViewModel(api, new FakeDialogService());

        await viewModel.LoadAsync();

        Assert.Single(viewModel.Documents);
        Assert.Equal("架构笔记", viewModel.Documents[0].Title);
    }

    [Fact]
    public async Task Filter_NarrowsDocumentsByTitle()
    {
        var api = new FakeKnowledgeApiClient();
        api.Documents.Add(new Document { Id = "1", Title = "架构笔记" });
        api.Documents.Add(new Document { Id = "2", Title = "会议记录" });
        var viewModel = CreateViewModel(api, new FakeDialogService());
        await viewModel.LoadAsync(); // 先加载数据（对应页面进入时 OnNavigatedTo）

        viewModel.FilterText = "架构";

        Assert.Single(viewModel.Documents);
        Assert.Equal("1", viewModel.Documents[0].Id);
    }

    [Fact]
    public async Task Filter_ClearingShowsAllDocumentsAgain()
    {
        var api = new FakeKnowledgeApiClient();
        api.Documents.Add(new Document { Id = "1", Title = "架构笔记" });
        api.Documents.Add(new Document { Id = "2", Title = "会议记录" });
        var viewModel = CreateViewModel(api, new FakeDialogService());
        await viewModel.LoadAsync();

        viewModel.FilterText = "会议";
        Assert.Single(viewModel.Documents);

        viewModel.FilterText = string.Empty;
        Assert.Equal(2, viewModel.Documents.Count);
    }

    [Fact]
    public async Task Save_WithEmptyTitle_DoesNotCallApi()
    {
        var api = new FakeKnowledgeApiClient();
        var created = await api.AddDocumentAsync("原标题", "a.md", [1]);
        var viewModel = CreateViewModel(api, new FakeDialogService());
        await viewModel.LoadAsync();

        viewModel.SelectedDocument = viewModel.Documents[0];
        viewModel.DetailTitle = "   ";
        await viewModel.SaveAsync();

        Assert.Equal("原标题", api.Documents[0].Title);
        Assert.Contains("标题不能为空", viewModel.StatusText);
        Assert.NotNull(created);
    }

    [Fact]
    public async Task Import_AddsDocumentAndReloads()
    {
        var api = new FakeKnowledgeApiClient();
        var dialogs = new FakeDialogService();
        var viewModel = CreateViewModel(api, dialogs);

        var tempFile = Path.Combine(Path.GetTempPath(), $"lkb-test-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(tempFile, "# hello");
        try
        {
            await viewModel.ImportFilesAsync(new[] { tempFile });

            Assert.Equal(1, api.AddDocumentCallCount);
            Assert.Single(viewModel.Documents);
            Assert.Contains("导入成功", viewModel.StatusText);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Import_WithMultipleFiles_ImportsAllAndReportsCount()
    {
        var api = new FakeKnowledgeApiClient();
        var dialogs = new FakeDialogService();
        var viewModel = CreateViewModel(api, dialogs);

        var files = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"lkb-batch-{Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(tempFile, $"内容 {i}");
            files.Add(tempFile);
        }
        try
        {
            await viewModel.ImportFilesAsync(files);

            Assert.Equal(3, api.AddDocumentCallCount);
            Assert.Equal(3, viewModel.Documents.Count);
            Assert.Contains("成功 3 / 3", viewModel.StatusText);
        }
        finally
        {
            files.ForEach(File.Delete);
        }
    }

    [Fact]
    public async Task Delete_RequiresConfirmation()
    {
        var api = new FakeKnowledgeApiClient();
        await api.AddDocumentAsync("待删除", "a.md", [1]);
        var dialogs = new FakeDialogService { NextConfirmResult = false };
        var viewModel = CreateViewModel(api, dialogs);
        await viewModel.LoadAsync();

        viewModel.SelectedDocument = viewModel.Documents[0];
        await viewModel.DeleteAsync();

        Assert.Equal(1, dialogs.ConfirmCallCount);
        Assert.Single(viewModel.Documents); // 用户取消，未删除
    }
}

public sealed class TagFilterTests
{
    private static List<Document> SeedDocs() => new()
    {
        new Document { Id = "1", Title = "架构笔记", Tags = "架构,部署" },
        new Document { Id = "2", Title = "部署指南", Tags = "部署" },
        new Document { Id = "3", Title = "会议记录", Tags = null }
    };

    private static DocumentsViewModel CreateLoaded(FakeKnowledgeApiClient api)
    {
        var viewModel = new DocumentsViewModel(api, new FakeDialogService(), new StatusReporter());
        viewModel.LoadAsync().GetAwaiter().GetResult();
        return viewModel;
    }

    [Fact]
    public void Load_BuildsTagChipsWithCounts()
    {
        var api = new FakeKnowledgeApiClient();
        foreach (var doc in SeedDocs()) api.Documents.Add(doc);
        var viewModel = CreateLoaded(api);

        Assert.Equal(4, viewModel.TagFilters.Count); // 全部 + 架构 + 部署 + 未分类
        Assert.Equal("全部 (3)", viewModel.TagFilters[0].Label);
        Assert.Contains(viewModel.TagFilters, f => f.Label == "架构 (1)");
        Assert.Contains(viewModel.TagFilters, f => f.Label == "部署 (2)");
        Assert.Contains(viewModel.TagFilters, f => f.Label == "未分类 (1)");
    }

    [Fact]
    public void SelectingTagChip_FiltersDocuments()
    {
        var api = new FakeKnowledgeApiClient();
        foreach (var doc in SeedDocs()) api.Documents.Add(doc);
        var viewModel = CreateLoaded(api);

        viewModel.SelectedTagFilter = viewModel.TagFilters.First(f => f.Tag == "架构");

        Assert.Single(viewModel.Documents);
        // 分类芯片与关键词筛选叠加（架构笔记命中标题关键词）
        viewModel.FilterText = "笔记";
        Assert.Single(viewModel.Documents);
    }

    [Fact]
    public void SelectingUntaggedChip_ShowsOnlyUntaggedDocuments()
    {
        var api = new FakeKnowledgeApiClient();
        foreach (var doc in SeedDocs()) api.Documents.Add(doc);
        var viewModel = CreateLoaded(api);

        viewModel.SelectedTagFilter = viewModel.TagFilters.First(f => f.Tag == string.Empty);

        Assert.Single(viewModel.Documents);
        Assert.Equal("3", viewModel.Documents[0].Id);
    }

    [Fact]
    public void Reload_KeepsSelection_WhenChipStillExists()
    {
        var api = new FakeKnowledgeApiClient();
        foreach (var doc in SeedDocs()) api.Documents.Add(doc);
        var viewModel = CreateLoaded(api);
        viewModel.SelectedTagFilter = viewModel.TagFilters.First(f => f.Tag == "架构");

        viewModel.LoadAsync().GetAwaiter().GetResult();

        Assert.Equal("架构", viewModel.SelectedTagFilter?.Tag);
        Assert.Single(viewModel.Documents);
    }
}

public sealed class MemoryViewModelTests
{
    private static MemoryViewModel CreateViewModel(FakeKnowledgeApiClient api) =>
        new(api, new FakeDialogService(), new StatusReporter());

    [Fact]
    public async Task Save_NewEntry_CallsApiAndReloads()
    {
        var api = new FakeKnowledgeApiClient();
        var viewModel = CreateViewModel(api);

        viewModel.Title = "使用偏好";
        viewModel.Content = "回复保持简洁";
        await viewModel.SaveAsync();

        Assert.Equal(1, api.AddMemoryCallCount);
        Assert.Single(viewModel.Entries);
    }

    [Fact]
    public async Task Save_WithoutContent_DoesNotCallApi()
    {
        var api = new FakeKnowledgeApiClient();
        var viewModel = CreateViewModel(api);

        viewModel.Title = "只有标题";
        viewModel.Content = "  ";
        await viewModel.SaveAsync();

        Assert.Equal(0, api.AddMemoryCallCount);
        Assert.Contains("请填写标题和内容", viewModel.StatusText);
    }

    [Fact]
    public async Task SelectingEntry_SwitchesFormToEditMode()
    {
        var api = new FakeKnowledgeApiClient();
        var added = await api.AddMemoryEntryAsync("决策", "记录", "架构");
        var viewModel = CreateViewModel(api);
        await viewModel.LoadAsync();

        viewModel.SelectedEntry = viewModel.Entries[0];

        Assert.True(viewModel.IsEditing);
        Assert.Equal("更新", viewModel.SaveButtonText);
        Assert.Equal("决策", viewModel.Title);
        Assert.NotNull(added);
    }
}

public sealed class SearchViewModelTests
{
    [Fact]
    public async Task Search_WithEmptyQuery_DoesNotCallApi()
    {
        var api = new FakeKnowledgeApiClient();
        var viewModel = new SearchViewModel(api, new StatusReporter());

        viewModel.Query = "  ";
        await viewModel.SearchAsync();

        Assert.False(viewModel.HasSearched);
        Assert.Contains("请输入搜索关键词", viewModel.StatusText);
    }

    [Fact]
    public async Task Search_PopulatesResultsAndCount()
    {
        var api = new FakeKnowledgeApiClient();
        await api.AddDocumentAsync("架构笔记", "a.md", [1]);
        var viewModel = new SearchViewModel(api, new StatusReporter());

        viewModel.Query = "架构";
        await viewModel.SearchAsync();

        Assert.True(viewModel.HasSearched);
        Assert.True(viewModel.HasResults);
        Assert.Equal("搜索结果 (1)", viewModel.ResultCountText);
        Assert.Contains("找到 1 个结果", viewModel.StatusText);
    }
}
