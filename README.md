# Project Goofish for Windows 10 Mobile (第三方闲鱼 UWP 客户端)

> **致敬经典 Lumia 与 Windows 10 Mobile**：这是一个面向 Windows 10 Mobile / Windows 10 PC (UWP) 平台打造的高还原度第三方闲鱼客户端方案。
> 专注于核心的**二手浏览、推荐流、搜索、商品详情、收藏夹、以及与卖家的即时通讯 (私聊)**，彻底剥离冗余的支付与金融链路。

---

## 目录结构

```text
Project-Goofish/
├── prototype/                     # 完整的交互式 HTML/CSS/JS 原型 (可直接双击运行)
│   ├── index.html                 # Windows 10 Mobile 仿真视口容器 (Lumia 950 外壳/全屏切换)
│   ├── css/
│   │   ├── w10m-core.css          # MDL2 调色板、Segoe UI 字体层级、AMOLED 纯黑暗色模式
│   │   └── w10m-controls.css      # Pivot 横滑、CommandBar 底栏、双列磁贴卡片、私聊气泡
│   └── js/
│       ├── mockData.js            # 真实的数码模玩闲置商品、卖家信誉、聊天会话数据集
│       ├── api.js                 # 纯原生无依赖 MD5 散列实现 + MTOP 自动握手请求管线
│       └── app.js                 # 原型交互 Code-Behind (页面栈导航路由、ViewModel 与事件绑定)
│
├── src/                           # 完整的 C# / XAML UWP Code-Behind 项目源代码
│   ├── Models/
│   │   ├── ItemModels.cs          # 商品详情、Feed 卡片、卖家信用画像数据实体
│   │   ├── ChatModels.cs          # 钉钉/闲鱼 WebSocket 会话条目与消息实体
│   │   └── MtopResponse.cs        # MTOP 统一返回体与错误解析
│   ├── Services/
│   │   ├── MtopSigner.cs          # 基于 Windows CryptographicBuffer 的原生 MD5 签名生成器
│   │   ├── GoofishApiService.cs   # Windows.Web.Http 原生网络服务 (内置沙盒模式与 MTOP 对接)
│   │   └── GoofishImWebSocket.cs  # 基于 UWP MessageWebSocket 的长连接私聊客户端
│   ├── ViewModels/
│   │   ├── ViewModelBase.cs       # INotifyPropertyChanged 基础通知类
│   │   ├── MainViewModel.cs       # 首页推荐流与分类 Pivot 数据绑定
│   │   ├── ItemDetailViewModel.cs # 商品详情与收藏状态切换
│   │   ├── SearchViewModel.cs     # 关键词模糊搜索与多维度排序
│   │   └── ChatViewModel.cs       # 消息列表与专属 1 对 1 聊天会话
│   └── Views/
│       ├── MainPage.xaml / .cs    # 经典 Pivot 首页与 CommandBar
│       ├── ItemDetailPage.xaml/.cs# 详情画廊与联系卖家动作
│       ├── SearchPage.xaml / .cs  # AutoSuggestBox 智能搜索
│       └── ChatPage.xaml / .cs    # Windows 10 消息中心风格气泡对话页
│
└── docs/
    └── Xianyu_API_Documentation.md # 闲鱼 MTOP 协议与 WebSocket IM 机制全景逆向报告
```

---

## 一、闲鱼 API 核心调研结论与可行性

闲鱼（Goofish）未对外公开商业 OpenAPI，但在 Web 端与 H5 端有极其成熟且可逆向对接的通信链路：

1. **MTOP 网关请求机制**：
   - 接口格式：`https://h5api.m.goofish.com/h5/{api}/{version}/`
   - 签名公式：`sign = MD5(token + "&" + timestamp + "&" + appKey + "&" + data)`
   - 客户端 AppKey：`12574478` (Web 公共标识)。
   - **空令牌自动握手**：客户端首次发起请求若缺少 `_m_h5_tk` Cookie，网关会通过 HTTP 400/200 下发 `FAIL_SYS_TOKEN_EMPTY` 并在 `Set-Cookie` 中附带 Token。客户端提取后重新计算签名即可正常获取数据。
2. **核心业务接口覆盖**：
   - 首页 Feed 推荐：`mtop.taobao.idlehome.home.webpc.feed` (v1.0)
   - 商品搜索：`mtop.taobao.idle.web.item.search` (v1.0)
   - 商品详情：`mtop.taobao.idle.pc.detail` (v1.0)
   - 收藏列表与收藏操作：`mtop.taobao.idle.web.favor.item.list` / `mtop.taobao.idle.favor.item.add`
3. **即时通讯 (IM) 对话**：
   - 先通过 `mtop.taobao.idlemessage.pc.login.token` 换取临时 IM 凭证。
   - 建立 WebSocket 长连接至阿里钉钉网关：`wss://wss-goofish.dingtalk.com/`。
   - 通过 `/reg` 注册设备，监听 `/s/sync` 下行消息推送，通过 `/r/MessageSend/sendByReceiverScope` 发送文本。
4. **付款功能剥离可行性**：
   - 闲鱼付款涉及支付宝深度 SDK 唤起、设备指纹与风控验证；剔除付款功能后，整个系统退化为纯粹的**二手商品搜索引擎与买卖双方沟通桥梁**，极大降低了安全风控风险，完全适合打造精简轻快的第三方客户端！

---

## 二、Windows 10 Mobile 原生设计规范还原 (MDL2)

本项目在 UI 表现上严格遵循微软当年制定的 **Microsoft Design Language 2 (MDL2)** 移动端规范：

1. **经典 Pivot 横滑控件**：
   - 页面顶部巨大的轻量级标题栏，横滑切换「推荐」「数码外设」「潮玩二次元」。活动项高亮显示强调色下划线，未选定项半透明压暗。
2. **底部 CommandBar (Application Bar)**：
   - 高度规范为 48px，停靠在屏幕最底端（单手拇指热区）。
   - 包含圆形按钮（40px 触控区、MDL2 图标、10px 描述文字）。
   - 右侧包含经典的 `...`（省略号）按钮，点击顺滑上拉弹出次级操作菜单。
3. **纯黑 AMOLED 调色与系统强调色**：
   - 默认采用 `#000000` 纯黑背景（极致省电且契合当年 Lumia OLED 屏幕表现），搭配闲鱼经典金黄色（`#FFB900`）或 Lumia 经典蓝（`#0078D7`）。
   - 内置浅色模式与 Windows 强调色实时切换功能。
4. **磁贴与双列网格布局**：
   - 商品卡片采用无圆角的轻量扁平卡片（Flat Tiles），价格醒目加粗，底部展示卖家芝麻信用标签与头像。
5. **Windows 10 Messaging 私聊对话气泡**：
   - 发送者使用右侧高亮气泡（Accent Color），卖家回复采用左侧深灰色气泡，顶部悬浮正在咨询的宝贝摘要条。

---

## 三、如何运行交互式 HTML 原型

直接双击打开项目内的 `prototype/index.html`：

1. **多端体验**：
   - 默认采用精致的 **Lumia 950 手机外壳视图**（包含状态栏 4G/WiFi/电池/时钟）。
   - 点击顶部「切换真机外壳 / 全屏」按钮，可一键切换为移动端全屏响应式视图。
2. **完整交互流程体验**：
   - **浏览推荐**：切换 Pivot 标签查看数码、二次元等商品。
   - **全局搜索**：点击顶部放大镜或底部搜索按钮，支持关键词即时过滤与价格排序。
   - **查看详情**：点击任意卡片进入详情页，查看大图轮播、卖家芝麻信用分、想要人数。
   - **一键收藏**：在详情页点击「收藏」，实时同步至底部「收藏」夹。
   - **与卖家对话**：在详情页点击「聊一聊 / 联系卖家」，自动生成商品对话流，输入文本并发送，**模拟卖家将在 1.2 秒后智能秒回**！
   - **系统个性化**：点击底部 CommandBar 的 `...` 展开菜单，可切换深/浅色模式以及更换 6 种 Windows 经典强调色。
