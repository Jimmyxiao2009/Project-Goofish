# 闲鱼 for Windows 10 Mobile · v2

第三方闲鱼 UWP 客户端：看推荐、搜索宝贝、看详情、收藏、查看订单、和卖家私信。不做付款与下单。

v2 是对旧版的重做，旧版 `prototype/`、`src/` 保持不动。主要变化：

- 界面按 Windows 10 Mobile 原生控件重新设计，不再是网页风格的卡片与圆角按钮。
- 接口按网页版的真实请求重写。旧文档里的 appKey `12574478` 和搜索接口名都不对，详见 `docs/API.md`。
- `GoofishMobile/` 是可以直接编译、部署到手机的完整工程，旧版缺少 csproj、App、清单等文件。
- 登录改成电脑版扫码（纯 HTTP），不再依赖手机上过老的 WebView 内核，详见 `docs/API.md` 第 4 节。
- 私信能显示图片、播放视频；新增订单查看（我买到的 / 我卖出的 / 订单详情）。
- `docs/Capabilities.md` 记录了「网页端有、客户端还没有」的能力清单。

## 目录

```text
v2/
├── prototype/                 HTML 原型，双击 index.html 打开
│   └── screenshots/           原型各界面截图
├── GoofishMobile.sln          VS2017 解决方案
├── GoofishMobile/
│   ├── Themes/Metro.xaml      样式、列表模板、私信气泡、订单项
│   ├── Views/                 MainPage(Pivot) / Search / ItemDetail / UserProfile / Chat / Orders / OrderDetail / Login / Settings + Shell
│   ├── ViewModels/
│   ├── Services/              MtopClient、GoofishService、GoofishImClient、GoofishQrLogin、QrEncoder、演示数据、账户
│   ├── Models/  Common/
│   └── screenshots/           真机构建在 PC 上运行的截图
└── docs/
    ├── API.md                 接口参考（含来源与可信度）
    └── Capabilities.md        与网页端的能力差距清单
```

## 设计要点（Windows 10 Mobile 规范）

- 首页是 **Pivot**：小号应用标题“闲鱼”，下面是“推荐 / 消息 / 收藏”，可以横滑切换，选中项始终在最左。
- 底部 **CommandBar**：只显示图标，点“…”展开文字标签和次要命令。
- 强调色、深浅色都跟随系统，设置里可以单独指定深色或浅色。
- 加载状态显示在**系统托盘**（`StatusBar.ProgressIndicator`），桌面上没有托盘，改为顶部细进度条。
- 返回走硬件 / 导航栏返回键；页面切换用系统的 Entrance 动画；列表靠 ListViewItem 自带的按压倾斜反馈。
- ComboBox 在手机上会以全屏选择器打开，ContentDialog 贴顶显示，这些都是系统控件的原生行为。
- 私信页参照 W10M“消息”应用：自己的气泡用强调色，对方的用灰色，带直角小尾巴，输入框在 CommandBar 上方。
- 新私信用系统 Toast 提醒，点击后直接打开对应会话。

## 运行原型

双击 `prototype/index.html`（建议用 Edge 或 Chrome）。右侧面板可以跳转到各个界面、切换系统深浅色和强调色。底部的“←”软键相当于手机的返回键。

## 编译与部署

需要 Visual Studio 2017，安装“通用 Windows 平台开发”工作负载、Windows 10 SDK 15063 和 Windows Mobile Extensions。

1. 打开 `GoofishMobile.sln`，第一次会还原 NuGet 包（UWP 5.4.7、Newtonsoft.Json 13.0.3）。
2. 部署到手机：平台选 **ARM**，目标选 **Device**，手机开启开发者模式后用 USB 连接，按 F5。
3. 在电脑上调试：平台选 **x86**，目标选 **Local Machine**。应用会以手机尺寸的窄窗口启动。
4. 发布 / 侧载：项目 → 应用商店 → 创建应用包。工程已开启签名（`AppxPackageSigningEnabled=true`），用的是仓库里的测试证书 `GoofishMobile_TemporaryKey.pfx`。该证书主题为 `CN=ProjectGoofish`，必须与 `Package.appxmanifest` 里的 `Publisher` 一致，否则打包会报 `0x800B0109` 之类的错误。证书有效期到 2029-10-05（SHA1 指纹 `C54F7C60C8272EB5DEB7C07880E1D3DC836DD26F`），过期后重新生成并同步更新 csproj 里的 `PackageCertificateThumbprint`。

命令行编译：

```powershell
$msb = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe'
& $msb GoofishMobile.sln /t:Restore
& $msb GoofishMobile.sln /p:Configuration=Debug /p:Platform=ARM
```

生成签名后的 ARM 侧载包（Release 走 .NET Native，产物约 2.5MB）：

```powershell
& $msb GoofishMobile\GoofishMobile.csproj /t:Build /p:Configuration=Release /p:Platform=ARM `
  /p:AppxBundle=Never /p:UapAppxPackageBuildMode=SideloadOnly
```

产物在 `GoofishMobile\AppPackages\GoofishMobile_2.0.0.8_ARM_Test\`，包含 `GoofishMobile_2.0.0.8_ARM.appx`、依赖包（.NET Native 1.7 的 ARM framework / runtime）和公钥证书 `GoofishMobile_2.0.0.8_ARM.cer`。侧载到手机前需要先把 `.cer` 装进设备的“受信任人”存储：用 `Add-AppDevPackage.ps1` 会自动处理，手动安装则把 `.cer` 拷到手机双击导入。

最低版本 10586（Lumia 950 / 950 XL 出厂系统），目标版本 15063。`Microsoft.NETCore.UniversalWindowsPlatform` 不能升级到 6.x，6.x 起最低要求 16299，W10M 装不上。

### 无线部署（设备门户 REST）

手机开启“设备门户”后，不用 USB 也能装：`https://<手机IP>/`。实测踩过的坑：

- 端点不是 `/api/appx/packagemanager/install`，而是 **`POST /api/app/packagemanager/package?package=<appx 文件名>`**（手机版 WDP 的路径与桌面版不同）。
- POST 必须带 **`X-CSRF-Token`**：先 `GET /`，从 Set-Cookie 里取 `CSRF-Token`，再把同值放进 `X-CSRF-Token` 头，否则 403 `CSRF Token Invalid`。
- multipart 的**字段名必须等于 appx 文件名**（网页版 JS 里是 `u.append(n.appFile.name, n.appFile)`）。
- 没有 body 的 POST 要显式发 `Content-Length: 0`（curl 加 `--data ""`），否则返回 411。
- 升级要**提高 `Package.appxmanifest` 里的 `Version`**，同版本重装无法确认是否真的换了包。
- 状态查询：`GET /api/app/packagemanager/state`（204 = 正在装）；启动应用：`POST /api/taskmanager/app?appid=<base64(PackageRelativeId)>`，`PackageRelativeId` 形如 `ProjectGoofish.GoofishMobile_xxxx!App`。

### 关于 Newtonsoft.Json

工程用的是 **12.0.3 的可移植版（PCL）**，不是 NuGet 默认给 UAP 选的 netstandard 版，`csproj` 里有注释说明。原因：这个工程在桌面上调试时进程跑在 .NET Framework 上（加载 `clr.dll`），netstandard 版 Newtonsoft 在那套运行时上会抛 `FieldAccessException`（`JsonSerializer..ctor` 访问 `JsonSerializerSettings.DefaultContext`）；而 net45 版又引用了 UWP 里没有的 `mscorlib` 类型、编译不过。PCL 版两边都认。

### 在电脑上跑

用 VS 按 F5（平台 x86、目标 Local Machine）最省事：VS 会把 `bin\x86\Debug` 注册成开发包并挂调试器。手动注册（`Add-AppxPackage -Register bin\x86\Debug\AppxManifest.xml`）也能启动，但那个 `AppxManifest.xml` 是构建产物、缺少打包清单里的框架依赖，应用可能落到 .NET Framework 上运行，行为与手机上不一致。要发布给手机时用上面的 Release/ARM 打包命令。

## 使用

- 不登录时默认显示**演示数据**：可以浏览、搜索、收藏（存在本机）、查看订单，私信会模拟卖家回复，演示会话里带一张图片和一段视频。设置里可以关掉。
- 登录：底部“…” → 登录。页面显示一个二维码（按屏幕宽度自适应，最大 200 DIP），用手机上的闲鱼 App 扫一扫并在手机上确认即可（电脑版扫码链路，纯 HTTP）。也可以粘贴从电脑浏览器复制的 Cookie。
- 订单：底部“…” → 订单。可切换「我买到的 / 我卖出的」，买家侧支持按状态筛选，点进订单看详情。
- 个人主页：在宝贝详情页点卖家一行，或在私信页点顶部对方昵称，进入 TA 的主页（昵称、头像、信用、粉丝、在售数、评价），可以直接「和 TA 聊聊」。在售列表是按昵称搜索得到的，未必全是 TA 的宝贝。
- 崩溃时原因写在应用的 `LocalState\crash.log`，可以通过 Windows Device Portal 的文件浏览器取回。

## 已知限制

- 所有接口都是闲鱼网页版的非官方接口，可能随时变化；请求太频繁会被风控（RGV587），客户端已经串行化并限速。
- **收藏 / 取消收藏的接口名未经验证**（见 `docs/API.md`）。调用失败时只保存在本机，并提示没有同步。
- 私信能显示图片、播放视频，卡片消息（宝贝/订单/链接/文件等）渲染成卡片，语音消息显示为“[语音]”；**发送**仍只支持文字。
- 消息列表（含对方昵称、头像）缓存在本机（`LocalState\conversations.json`，按账号区分）：打开或切回消息页先用缓存立刻显示，再在后台刷新，不会每次都空等一次同步。缓存里已有昵称/头像时不会闪回“闲鱼用户”。
- 订单：买家列表与订单详情目前只有单一来源，字段名以实测为准；卖家接口对 `Origin` 等请求头敏感，可能返回权限错误。订单页与详情页都做了容错，取不到的字段留空。
- 应用挂起时会断开 IM 长连接，不在前台时收不到新消息提醒（W10M 的后台推送需要 WNS，闲鱼不提供）。
- 真实账号下的推荐、搜索、私信、订单流程还没有在真机上跑过，字段解析按开源项目记录的结构写，并做了多路径容错。
