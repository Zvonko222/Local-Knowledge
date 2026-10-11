using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LocalKnowledgeBase.Desktop.ViewModels;

namespace LocalKnowledgeBase.Desktop.Views;

/// <summary>问答页面。DataContext 是 ChatViewModel；这里只负责新消息到达时滚动到底部。</summary>
public partial class ChatPage : UserControl
{
    public ChatPage()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => HookMessages(DataContext as ChatViewModel);
    }

    private ChatViewModel? _hookedViewModel;

    private void HookMessages(ChatViewModel? viewModel)
    {
        if (_hookedViewModel is not null)
            _hookedViewModel.Messages.CollectionChanged -= Messages_CollectionChanged;

        _hookedViewModel = viewModel;
        if (viewModel is not null)
            viewModel.Messages.CollectionChanged += Messages_CollectionChanged;
    }

    private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<INotifyPropertyChanged>())
                item.PropertyChanged += Message_PropertyChanged;
        }

        // 不能在 CollectionChanged 里同步滚动（会重入 ItemsControl 的容器生成，
        // 触发 "ItemsControl is inconsistent with its items source"），延迟到布局之后
        Dispatcher.BeginInvoke(new Action(ScrollToBottom), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void Message_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatMessageViewModel.Text))
            Dispatcher.BeginInvoke(new Action(ScrollToBottom), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void ScrollToBottom()
    {
        if (DataContext is ChatViewModel { Messages.Count: > 0 } viewModel)
            MessagesList.ScrollIntoView(viewModel.Messages[^1]);
    }
}
