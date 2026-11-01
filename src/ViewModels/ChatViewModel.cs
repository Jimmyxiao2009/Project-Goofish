using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ProjectGoofish.Models;
using ProjectGoofish.Services;

namespace ProjectGoofish.ViewModels
{
    /// <summary>
    /// 即时通讯与私聊会话视图模型
    /// </summary>
    public class ChatViewModel : ViewModelBase
    {
        private Conversation _activeConversation;
        public Conversation ActiveConversation
        {
            get => _activeConversation;
            set => SetProperty(ref _activeConversation, value);
        }

        public ObservableCollection<Conversation> Conversations { get; } = new ObservableCollection<Conversation>();
        public ObservableCollection<ChatMessage> CurrentMessages { get; } = new ObservableCollection<ChatMessage>();

        public async Task LoadConversationsAsync()
        {
            var list = await GoofishApiService.Instance.GetConversationsAsync();
            Conversations.Clear();
            foreach (var conv in list)
            {
                Conversations.Add(conv);
            }
        }

        public void SetActiveConversation(Conversation conv)
        {
            ActiveConversation = conv;
            CurrentMessages.Clear();
            if (conv != null && conv.Messages != null)
            {
                foreach (var msg in conv.Messages)
                {
                    CurrentMessages.Add(msg);
                }
            }
        }

        public async Task SendMessageAsync(string text)
        {
            if (ActiveConversation == null || string.IsNullOrWhiteSpace(text)) return;

            var msg = await GoofishApiService.Instance.SendChatMessageAsync(ActiveConversation.Id, text.Trim());
            if (msg != null)
            {
                CurrentMessages.Add(msg);
            }
        }
    }
}
