/**
 * 闲鱼 Windows 10 Mobile 仿真沙盒数据集 (Mock Data)
 * 涵盖推荐流、分类、搜索、商品详情与即时通讯数据
 */

const MOCK_DATA = {
  // 当前登录用户信息
  currentUser: {
    userId: "u_win10m_fan",
    nick: "Lumia极客",
    avatar: "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=100&auto=format&fit=crop&q=80",
    cookie: "cookie2=win10m_mock_session_token_12345",
    isLoggedIn: true
  },

  // 首页推荐商品列表
  feedItems: [
    {
      id: "item_1001",
      title: "【传家宝】微软 Lumia 950 XL 港版双卡 黑色 成色极新 已刷Win11 ARM",
      desc: "收藏级成色！屏幕完美无老化，原装电池待机正常。机器已经折腾刷入 Windows 11 on ARM，能开机进桌面跑 exe，附送 Continuum 扩展坞和原装充电头。退烧出给同好，不刀。",
      price: 499.00,
      originalPrice: 3999.00,
      transportFee: "包邮",
      images: [
        "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?w=600&auto=format&fit=crop&q=80",
        "https://images.unsplash.com/photo-1580910051074-3eb694886505?w=600&auto=format&fit=crop&q=80"
      ],
      city: "北京 · 海淀",
      category: "digital",
      browseCnt: 1829,
      collectCnt: 142,
      wantCnt: 38,
      isFav: true,
      seller: {
        userId: "seller_201",
        nick: "微粉老大哥",
        avatar: "https://images.unsplash.com/photo-1570295999919-56ceb5ecca61?w=100&auto=format&fit=crop&q=80",
        creditScore: "芝麻信用 极好",
        city: "北京",
        itemCount: 16,
        soldCount: 88,
        rating: "100% 好评"
      }
    },
    {
      id: "item_1002",
      title: "ThinkPad X1 Carbon 2021 经典商务本 i7-1165G7 16G 512G 4K屏",
      desc: "自用主力机，公司配新电脑故出。碳纤维A面质感无敌，小红点灵敏，键盘手感极佳。箱说全，带原装65W口红电源。支持当面试机，顺丰到付。",
      price: 3200.00,
      originalPrice: 11999.00,
      transportFee: "包邮",
      images: [
        "https://images.unsplash.com/photo-1588872657578-7efd1f1555ed?w=600&auto=format&fit=crop&q=80",
        "https://images.unsplash.com/photo-1496181133206-80ce9b88a853?w=600&auto=format&fit=crop&q=80"
      ],
      city: "上海 · 浦东",
      category: "digital",
      browseCnt: 3410,
      collectCnt: 215,
      wantCnt: 62,
      isFav: false,
      seller: {
        userId: "seller_202",
        nick: "陆家嘴打工人",
        avatar: "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=100&auto=format&fit=crop&q=80",
        creditScore: "芝麻信用 优秀",
        city: "上海",
        itemCount: 4,
        soldCount: 12,
        rating: "98% 好评"
      }
    },
    {
      id: "item_1003",
      title: "索尼 Sony A7M3 (ILCE-7M3) 单机身 快门1.2万 无磕碰功能完好",
      desc: "防潮箱长驻，成色95新左右。底壳无划伤，CMOS干干净净无霉无灰。赠送两块原厂FZ100电池和双槽充电器。爽快的包顺丰保价。",
      price: 6800.00,
      originalPrice: 13500.00,
      transportFee: "包邮",
      images: [
        "https://images.unsplash.com/photo-1516035069371-29a1b244cc32?w=600&auto=format&fit=crop&q=80",
        "https://images.unsplash.com/photo-1502920917128-1aa500764cbd?w=600&auto=format&fit=crop&q=80"
      ],
      city: "杭州 · 西湖",
      category: "digital",
      browseCnt: 5210,
      collectCnt: 489,
      wantCnt: 104,
      isFav: true,
      seller: {
        userId: "seller_203",
        nick: "风光摄影师阿杰",
        avatar: "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=100&auto=format&fit=crop&q=80",
        creditScore: "芝麻信用 极好",
        city: "杭州",
        itemCount: 9,
        soldCount: 42,
        rating: "100% 好评"
      }
    },
    {
      id: "item_1004",
      title: "Nintendo Switch 日版 OLED 白色限定款 99新 无暗病 带喷射3卡带",
      desc: "买了半年总共玩了不到50小时，一直贴钢化膜戴水晶壳。屏幕色彩艳丽无划痕，摇杆漂移已检测无问题。配件底座HDMI线全新未拆。",
      price: 1350.00,
      originalPrice: 2199.00,
      transportFee: "包邮",
      images: [
        "https://images.unsplash.com/photo-1578301978693-85fa9c0320b9?w=600&auto=format&fit=crop&q=80"
      ],
      city: "深圳 · 南山",
      category: "game",
      browseCnt: 1980,
      collectCnt: 167,
      wantCnt: 45,
      isFav: false,
      seller: {
        userId: "seller_204",
        nick: "任地狱小迷弟",
        avatar: "https://images.unsplash.com/photo-1492562080023-ab3db95bfbce?w=100&auto=format&fit=crop&q=80",
        creditScore: "芝麻信用 良好",
        city: "深圳",
        itemCount: 6,
        soldCount: 20,
        rating: "99% 好评"
      }
    },
    {
      id: "item_1005",
      title: "AirPods Pro 2代 Type-C 接口 国行正品 在保 带AC+ Care",
      desc: "苹果官网23年底购入，序列号可查支持直营店验货。充电盒微小日常使用细痕，耳塞均已酒精深度消毒并附赠原装未拆耳塞套。因换索尼降噪大耳闲置。",
      price: 1100.00,
      originalPrice: 1899.00,
      transportFee: "包邮",
      images: [
        "https://images.unsplash.com/photo-1600294037681-c80b4cb5b434?w=600&auto=format&fit=crop&q=80"
      ],
      city: "广州 · 天河",
      category: "digital",
      browseCnt: 2904,
      collectCnt: 310,
      wantCnt: 79,
      isFav: false,
      seller: {
        userId: "seller_205",
        nick: "数码发烧友小柯",
        avatar: "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=100&auto=format&fit=crop&q=80",
        creditScore: "芝麻信用 极好",
        city: "广州",
        itemCount: 15,
        soldCount: 95,
        rating: "100% 好评"
      }
    },
    {
      id: "item_1006",
      title: "ROG 玩家国度 RTX 4070 Ti 12G 猛禽 纯白限定 自用一手无挖矿",
      desc: "去年双十一京东旗舰店入手，电子发票齐全，箱说全对码。只用来打过黑神话和英雄联盟，跑分温度压在60度以内。换4090出，同城优先面交自提。",
      price: 4900.00,
      originalPrice: 6999.00,
      transportFee: "包邮",
      images: [
        "https://images.unsplash.com/photo-1587202372775-e229f172b9d7?w=600&auto=format&fit=crop&q=80"
      ],
      city: "成都 · 武侯",
      category: "digital",
      browseCnt: 4120,
      collectCnt: 390,
      wantCnt: 89,
      isFav: false,
      seller: {
        userId: "seller_206",
        nick: "纯白装机达人",
        avatar: "https://images.unsplash.com/photo-1519085360753-af0119f7cbe7?w=100&auto=format&fit=crop&q=80",
        creditScore: "芝麻信用 极好",
        city: "成都",
        itemCount: 7,
        soldCount: 31,
        rating: "100% 好评"
      }
    }
  ],

  // 消息与会话列表
  conversations: [
    {
      id: "conv_1",
      sellerId: "seller_201",
      sellerNick: "微粉老大哥",
      sellerAvatar: "https://images.unsplash.com/photo-1570295999919-56ceb5ecca61?w=100&auto=format&fit=crop&q=80",
      itemId: "item_1001",
      itemTitle: "微软 Lumia 950 XL 港版双卡 黑色",
      itemPrice: 499.00,
      itemThumb: "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?w=600&auto=format&fit=crop&q=80",
      lastMessage: "哥们，刷好的 Win11 ARM 可以直接运行经典桌面软件吗？",
      lastTime: "18:24",
      unreadCount: 1,
      messages: [
        { id: "m1", sender: "other", text: "你好！看上这款 Lumia 950 XL 了吗？机器状态非常好！", time: "18:20" },
        { id: "m2", sender: "me", text: "哥们，刷好的 Win11 ARM 可以直接运行经典桌面软件吗？", time: "18:24" },
        { id: "m3", sender: "other", text: "可以的！x86 仿真可以跑 7-Zip、Notepad++ 等小工具，插上 Continuum 扩展坞接显示器就成迷你主机了。", time: "18:26" }
      ]
    },
    {
      id: "conv_2",
      sellerId: "seller_203",
      sellerNick: "风光摄影师阿杰",
      sellerAvatar: "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=100&auto=format&fit=crop&q=80",
      itemId: "item_1003",
      itemTitle: "索尼 Sony A7M3 单机身 快门1.2万",
      itemPrice: 6800.00,
      itemThumb: "https://images.unsplash.com/photo-1516035069371-29a1b244cc32?w=600&auto=format&fit=crop&q=80",
      lastMessage: "快门数可以进工程模式确认，随时支持发货。",
      lastTime: "昨天",
      unreadCount: 0,
      messages: [
        { id: "m10", sender: "me", text: "老板，A7M3 机身有磕碰掉漆吗？", time: "昨天 14:10" },
        { id: "m11", sender: "other", text: "完全没有磕碰，底壳一直贴着保护膜，快门数可以进工程模式确认，随时支持发货。", time: "昨天 14:15" }
      ]
    }
  ]
};
