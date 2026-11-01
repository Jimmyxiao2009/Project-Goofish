namespace Goofish.Services
{
    /// <summary>应用级服务的唯一入口。页面与 ViewModel 都从这里取依赖。</summary>
    public static class AppServices
    {
        static AppServices()
        {
            Settings = new AppSettings();
            Mtop = new MtopClient();
            Favorites = new FavoritesStore();
            Account = new AccountService(Mtop, Settings);
            Live = new GoofishService(Mtop, Favorites);
            Im = new GoofishImClient(Mtop, Account);
            Demo = new DemoDataSource(Favorites);
        }

        public static AppSettings Settings { get; }
        public static MtopClient Mtop { get; }
        public static FavoritesStore Favorites { get; }
        public static AccountService Account { get; }
        public static GoofishService Live { get; }
        public static GoofishImClient Im { get; }
        public static DemoDataSource Demo { get; }

        /// <summary>未登录且开启了演示数据时使用样例，其余情况都访问闲鱼。</summary>
        public static bool UseDemo => !Account.IsSignedIn && Settings.DemoMode;

        public static IGoofishDataSource Data => UseDemo ? (IGoofishDataSource)Demo : Live;

        public static IChatService Chat => UseDemo ? (IChatService)Demo : Im;

        /// <summary>当前在前台查看的会话，收到这个会话的消息时不弹 Toast。</summary>
        public static string ActiveConversationId { get; set; }
    }
}
