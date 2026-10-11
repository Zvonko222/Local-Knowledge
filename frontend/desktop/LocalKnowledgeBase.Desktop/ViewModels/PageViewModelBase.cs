namespace LocalKnowledgeBase.Desktop.ViewModels;

/// <summary>
/// 所有页面 ViewModel 的基类。Shell 在切换到页面时调用 <see cref="OnNavigatedTo"/>，
/// 页面在该时机刷新自己的数据。
/// </summary>
public abstract class PageViewModelBase : ViewModelBase
{
    /// <summary>每次通过侧边栏进入页面时调用。</summary>
    public virtual void OnNavigatedTo()
    {
    }
}
