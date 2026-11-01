# 闲鱼网页版接口参考（v2）

闲鱼没有公开 API。本文整理的是闲鱼网页版 `www.goofish.com` 实际发出的请求，来源于下列开源项目的实测代码与文档（2026 年 9 月仍在维护）：

| 来源 | 用到的内容 |
| --- | --- |
| [shaxiu/XianyuAutoAgent](https://github.com/shaxiu/XianyuAutoAgent) | mtop 签名、IM 令牌、`/reg`、发消息、心跳、MessagePack 推送解码 |
| [fancyboi999/goofish-cli](https://github.com/fancyboi999/goofish-cli) | 会话列表、历史消息、建会话、`/s/vulcan` 就绪信号、ack 规则 |
| [timefunnel/xianyu-monitor](https://github.com/timefunnel/xianyu-monitor) | 搜索请求体、错误分类（RGV587 / deny / TOKEN）、扫码登录流程 |
| [11273/goofish-client 文档](https://11273.github.io/goofish-client/) | 首页 Feed、详情、收藏列表、用户信息的字段结构 |

> 这些都是非官方接口，可能随时变化，频繁请求会触发风控。应用只做浏览、收藏、聊天，不涉及下单与付款。

可信度标记：**已验证** = 至少两个独立项目的代码一致；**单一来源** = 只有一个项目记录；**未验证** = 推测，需要抓包核对。

---

## 1. mtop 网关

```
POST https://h5api.m.goofish.com/h5/{api}/{v}/?jsv=2.7.2&appKey=34839810&t={t}&sign={sign}
     &v={v}&type=originaljson&accountSite=xianyu&dataType=json&timeout=20000
     &api={api}&sessionOption=AutoLoginOnly
Content-Type: application/x-www-form-urlencoded
Referer: https://www.goofish.com/
Origin:  https://www.goofish.com

data={业务 JSON}
```

- **appKey 是 `34839810`**（已验证）。旧版文档里的 `12574478` 是淘宝 H5 的 appKey，用在闲鱼网关上签名对不上。
- 签名（已验证）：`sign = md5(token + "&" + t + "&" + appKey + "&" + data)`，小写十六进制。
  - `token` 是 Cookie `_m_h5_tk` 第一个 `_` 之前的部分；`t` 是毫秒时间戳；`data` 必须与表单里发送的字符串逐字节相同。
- 令牌握手（已验证）：没有或过期的 `_m_h5_tk` 会得到 `FAIL_SYS_TOKEN_EMPTY` / `FAIL_SYS_TOKEN_EXOIRED`（服务端确实把 EXPIRED 拼错了），新令牌已通过 `Set-Cookie` 下发，重签一次即可。实现见 `MtopClient.CallRawAsync`。
- 返回：`{ "ret": ["SUCCESS::调用成功"], "data": {...} }`。

### 错误分类（`MtopException.FromRet`）

| ret / data.url | 含义 | 处理 |
| --- | --- | --- |
| `FAIL_SYS_TOKEN*` | 令牌握手 | 重签一次 |
| `FAIL_SYS_SESSION_EXPIRED`、`data.url` 指向 passport | 登录失效 | 提示重新登录 |
| `RGV587_ERROR::SM::哎哟喂,被挤爆啦` | 限流或需要过滑块 | 提示稍后再试，不自动重试 |
| `data.url` 含 `/punish/` 且 `action=deny` | 风控直接拒绝 | 提示去网页版完成验证 |

客户端把所有 mtop 调用串行化，并保持至少 600ms 间隔（`MtopClient.MinGap`）。实测短时间连发 3 次搜索就可能触发 RGV587。

---

## 2. 业务接口

| 功能 | api / v | data | 可信度 |
| --- | --- | --- | --- |
| 首页推荐 | `mtop.taobao.idlehome.home.webpc.feed` / 1.0 | `{"itemId":"","pageSize":30,"pageNumber":1,"machId":""}` | 已验证 |
| 搜索 | `mtop.taobao.idlemtopsearch.pc.search` / 1.0 | 见下 | 已验证 |
| 商品详情 | `mtop.taobao.idle.pc.detail` / 1.0 | `{"itemId":"..."}` | 已验证 |
| 收藏列表 | `mtop.taobao.idle.web.favor.item.list` / 1.0 | `{"pageNumber":1,"rowsPerPage":20,"type":"DEFAULT"}`，type 可选 `REDUCE` / `VALID` / `INVALID` | 单一来源 |
| 会话列表 | WebSocket `/r/Conversation/listNewestPagination`（`session.sync` 仅作补充） | `[9007199254740991, 50]` | 已验证 |
| 当前用户 | `mtop.idle.web.user.page.nav` / 1.0 | `{}` | 单一来源 |
| 卖家 / 个人主页 | `mtop.idle.web.user.page.head` / 1.0 | `{"userId":"..."}` | 单一来源 |
| IM 令牌 | `mtop.taobao.idlemessage.pc.login.token` / 1.0 | `{"appKey":"444e9908a51d1cb236a27862abc769c9","deviceId":"..."}` | 已验证 |
| 添加收藏 | `mtop.taobao.idle.collect.item` / 1.0 | `{"itemId":"..."}` | **未验证** |
| 取消收藏 | `mtop.taobao.idle.collect.item.cancel` / 1.0 | `{"itemId":"..."}` | **未验证** |

收藏增删接口在公开资料里没有可靠记录。应用里这两个名字集中写在 `GoofishService.FavoriteAddApi / FavoriteRemoveApi`：调用失败时收藏只保存在本机（`FavoritesStore`），并告诉用户没有同步。在 goofish.com 点一次“收藏”，用开发者工具抓到真实接口名后改这两个常量即可。

### 搜索请求体（与网页版一致）

```json
{
  "pageNumber": 1, "keyword": "Lumia", "fromFilter": false, "rowsPerPage": 30,
  "sortValue": "", "sortField": "", "customDistance": "", "gps": "",
  "propValueStr": { "searchFilter": "" }, "customGps": "",
  "searchReqFromPage": "pcSearch", "extraFilterValue": "{}", "userPositionJson": "{}"
}
```

排序：最新 `sortField=create, sortValue=desc`；价格 `sortField=price, sortValue=asc|desc`。价格区间、发布天数写进 `propValueStr.searchFilter`，如 `priceRange:500,700;publishDays:3;`。

### 关键返回字段（`GoofishParser`）

- 推荐：`data.cardList[].cardData`：`itemId`、`titleSummary.text`、`priceInfo.price / oriPrice`、`city`、`user.userNick / avatar`、`mainPicInfo.url`、`fishTags`；`data.nextPage`。
- 搜索：`data.resultList[].data.item.main.exContent`：`itemId`、`title`、`price`（富文本片段数组，如 `[{"text":"¥"},{"text":"161"}]`）、`picUrl`、`area`、`userNickName`、`fishTags.*.tagList[].data.content`（如“卖家信用极好”“包邮”）。
- 详情：`data.itemDO`：`title`、`desc`、`soldPrice`、`originalPrice`、`transportFee`、`browseCnt`、`collectCnt`、`wantCnt`、`imageInfos[].url`、`gmtCreate`、`itemLabelExtList`（成色等）；`data.sellerDO`：`sellerId`、`nick`、`portraitUrl`、`city`、`hasSoldNumInteger`、`replyRatio24h`、`lastVisitTime`。
- 收藏：`data.items[]`：`id`、`title`、`price`、`reducePrice`、`picUrl`、`userNick`、`userId`、`city`、`itemStatus`（负数为失效）；`data.nextPage`、`data.totalCount`。
- 会话：`data.sessions[]`：`session.sessionId`、`session.sessionType`（1 单聊，3/6/23 为系统通知）、`session.userInfo.{userId,nick,fishNick,logo}`、`message.summary.{summary,ts,unread}`。

图片地址常为 `//img.alicdn.com/...`，需要补 `https:`。阿里 CDN 支持在地址后追加 `_400x400.jpg` 取缩略图，应用的列表与省流量模式都用这个办法。

---

## 3. 私信（钉钉 IM，LWP 协议）

`wss://wss-goofish.dingtalk.com/`，帧是 JSON：`{ "lwp": "/路径", "headers": { "mid": "..." }, "body": ... }`。`mid` 的格式是 `随机0~999 + 毫秒时间戳 + " 0"`，响应帧带相同 `mid` 和 `code`。

1. 用 mtop 换 `accessToken`（见上表）。`deviceId` 形如 `UUID-用户ID`，按用户持久化。
2. 连接时带 `Origin: https://www.goofish.com` 和登录 Cookie。
3. 发送 `/reg`：

   ```json
   { "lwp": "/reg", "headers": { "cache-header": "app-key token ua wv",
     "app-key": "444e9908a51d1cb236a27862abc769c9", "token": "{accessToken}",
     "ua": "{浏览器 UA} DingTalk(2.1.5) OS(Windows/10) Browser(Chrome/...) DingWeb/2.1.5 IMPaaS DingWeb/2.1.5",
     "dt": "j", "wv": "im:3,au:3,sy:6", "sync": "0,0;0;0;", "did": "{deviceId}", "mid": "..." } }
   ```

   接着发 `/r/SyncStatus/ackDiff`。它实测总是返回 400，但不影响后续流程。
4. **等服务端推送 `/s/vulcan` 之后再发 `/r/` 请求**，否则会被 400 拒绝（goofish-cli #33）。
5. 每 15 秒发一次 `{"lwp":"/!"}` 心跳。服务端每个下行推送都要回 `{"code":200,"headers":{"mid":..,"sid":..}}`，否则会重复推送。

| 操作 | lwp | body |
| --- | --- | --- |
| 建会话（基于宝贝） | `/r/SingleChatConversation/create` | `[{"pairFirst":"卖家ID@goofish","pairSecond":"我的ID@goofish","bizType":"1","extension":{"itemId":"..."},"ctx":{"appVersion":"1.0","platform":"web"}}]` → `body.singleChatConversation.cid` |
| 历史消息 | `/r/MessageManager/listUserMessages` | `["cid@goofish", false, 9007199254740991, 30, false]` → `body.userMessageModels[].message` |
| 发文本 | `/r/MessageSend/sendByReceiverScope` | `[{uuid, cid:"cid@goofish", conversationType:1, content:{contentType:101, custom:{type:1, data: base64({"contentType":1,"text":{"text":"..."}})}}, ...}, {"actualReceivers":["对方@goofish","我@goofish"]}]` |

新消息推送在 `/s/sync` 等帧的 `body.syncPushPackage.data[].data` 里，有三种编码，需要依次尝试：明文 JSON、base64(JSON)、base64(MessagePack)。MessagePack 解出来的是以 `"1"`、`"2"` 为键的结构，正文在 `["1"]["10"].reminderContent`，发送者在 `senderUserId`，会话在 `["1"]["2"]`。`contentType=8` 是会话激活心跳，`/s/para` 是“对方正在输入”，都不算消息。实现见 `GoofishImClient` 与 `MessagePackReader`。

### 消息内容类型（已验证）

`content.custom.data` 是 base64 的 JSON，解出来按 `contentType` 区分。取值与字段结构取自闲鱼 Web IM 自身打包的枚举（`goofish_js_origin_version_2.js`）：

| contentType | 类型 | 内容字段 |
| --- | --- | --- |
| 1 | 文本 | `text.text` |
| 2 | 图片 | `image.pics[].url` / `.width` / `.height` |
| 3 | 语音 | `audio`（客户端显示为“[语音]”） |
| 4 | 视频 | `video.url` / `.width` / `.height`（`video.url` 是可直接播放的直链） |
| 5–11 | 位置 / 卡片 / 链接 / @ / 文件 / 引用 / 合并转发 | 渲染成卡片：标题 `title`、描述 `desc`/`summary`、图片 `picUrl`/`imageUrl`、链接 `url`/`jumpUrl`、价格 `price`；字段名不确定，解析用递归按键名兜底（`JsonExtensions.Find`），一个字段都取不到才退回 `[类型]` 占位 |

历史消息（`/r/MessageManager/listUserMessages`）与实时推送的信封不同，但载荷结构一致，客户端统一走同一套解析（`GoofishImClient.ParseContent`）。

### 会话列表（已验证）

左栏会话列表要用 WebSocket 的 `/r/Conversation/listNewestPagination`，请求体 `[起始时间戳, 条数]`，首页起始时间戳传 JS 的 `MAX_SAFE_INTEGER`（`9007199254740991`），响应在 `body.userConvs[]`：

- 会话：`singleChatUserConversation.singleChatConversation`：`cid`、`pairFirst`/`pairSecond`（`<userId>@goofish`，对方是其中不等于自己的一方）、`extension.{itemId,itemTitle,itemMainPic}`
- 最后一条消息：`singleChatUserConversation.lastMessage.message`：`createAt`（毫秒）、`unreadCount`、`content.custom.summary` / `content.searchableContent.summary` / `extension.reminderContent`
- 会话修改时间：`singleChatUserConversation.modifyTime`；未读也可以用 `redPoint`

**不要用 mtop 的 `session.sync` 当会话列表**：goofish-cli 明确标注它是阉割版，只返回活跃 Top N，会漏掉最新会话（客户端表现为列表停在很久以前）。它仍然有用——响应里带对方昵称、头像、未读，正好补 `listNewestPagination` 缺的这些字段。客户端的做法是：以 `listNewestPagination` 的结果和顺序为准，用 `session.sync` 按 cid 补昵称/头像，并把 `session.sync` 里多出来的会话也保留，最后按时间倒序。见 `GoofishImClient.GetConversationsAsync` 与 `GoofishParser.ParseNewestConversations`。

**昵称/头像的兜底**：`session.sync` 属于 `mtop.taobao.idlemessage.*` 命名空间，和换 IM 令牌同族，可能被风控单独拦掉。一旦它失败，整列会话就会没有昵称和头像。客户端对此的处理是：`session.sync` 解析不到昵称时**不填默认值**，由 `MainViewModel.EnrichPeersAsync` 对仍缺昵称/头像的会话逐个调 `mtop.idle.web.user.page.head`（普通接口，不在 IM 命名空间）补上，补到一个刷新一个；`GoofishService` 按 userId 缓存，同一个对方只请求一次，每次加载最多补 20 个，避免触发风控。解析见 `GoofishParser.ParseSessions`。

**单聊里谁是谁**（实测）：`session.ownerInfo` 是**对方**（卖家），`session.userInfo` 是**自己**——`userInfo.userId` 等于当前账号的 `unb`。所以取对方要按 `selfUserId` 排除自己、优先 `ownerInfo`（`GoofishParser.PickPeer`），否则会把我的昵称当成卖家的。昵称优先 `fishNick`，其次 `nick`（`nick` 是打码形式，如 `x***6`）。系统会话的 `pairFirst`/`pairSecond` 里会出现 `0@goofish`、`-1@goofish` 这类占位，不是真实用户，要当作没有对方（界面显示“闲鱼官方”）。

**本地缓存**：会话列表（含昵称、头像、商品信息）缓存在 `LocalState\conversations.json`，按账号区分。打开或切回消息页先用缓存显示、再后台刷新，避免每次都空等同步；刷新时按 cid 把缓存里的昵称/头像合到新结果上，界面不会闪回“闲鱼用户”。见 `ConversationsCache`、`MainViewModel.RefreshConversationsAsync` / `MergePeers`。

---

## 4. 登录（电脑版扫码，纯 HTTP）

原来的做法是在 WebView 里打开官方登录页，但 W10M 的 Edge 内核太老，登录页里那套人机校验会直接报错。现在改成走闲鱼**电脑版**的扫码链路，全程纯 HTTP，不启动浏览器，因此也不受 WebView 内核版本影响：

```
1. GET  https://passport.goofish.com/mini_login.htm            → 下发 XSRF-TOKEN、cookie2 等基础 Cookie
2. GET  https://passport.goofish.com/newlogin/qrcode/generate.do → codeContent / t / ck
3. POST https://passport.goofish.com/newlogin/qrcode/query.do    → qrCodeStatus，轮询到 CONFIRMED
4. POST https://passport.goofish.com/login_token/login.do        → 真正建立会话，Set-Cookie 才带 unb / cookie2
```

要点（都实测过）：

- 请求要带电脑版 UA，`isMobile=false`、`site=77`、`appName=xianyu`、`fromSite=77`。
- `codeContent` 形如 `https://passport.goofish.com/qrcodeCheck.htm?lgToken=...&_from=havana`，把它编码成二维码给用户扫。**二维码在本地生成**（`QrEncoder`），不调任何在线二维码接口——内容等同于登录令牌，不能外传。显示尺寸按屏幕可用宽度自适应（最大 200 DIP），小屏上不会顶满或溢出。
- 轮询的 `t` 必须用 `generate.do` 返回的那个，用当前时间戳会被判成 `EXPIRED`；`query.do` 必须带 `ck`，否则直接 HTTP 500。
- `qrCodeStatus` 的值在各实现间连拼写都不统一（`SCANNED` / `SCANED`），客户端只认 `CONFIRMED`、`EXPIRED`、`CANCELED`，其余一律当“继续等”。
- 只有 `unb` 出现才算真的登录成功；`cookie2` 在登录页那一步就会给，单独拿到它等于一份看起来正常、其实没登录的凭据。
- 服务端把会话 Cookie 下在 `.goofish.com` 上，客户端按同样的域搬进 `MtopClient` 的容器（`GoofishQrLogin.ImportSessionCookies`），之后 `MtopClient` 直接可用。

备用方案仍是粘贴浏览器里复制的 Cookie 串（`AccountService.ImportCookies`）。实现见 `GoofishQrLogin`、`QrEncoder`、`LoginPage`。

## 5. 订单

| 功能 | api / v | data | 可信度 |
| --- | --- | --- | --- |
| 我买到的 | `mtop.idle.web.trade.bought.list` / 1.0 | `{"pageNumber":1,"orderStatus":"ALL"}` | 单一来源 |
| 我卖出的 | `mtop.taobao.idle.trade.merchant.sold.get` / 1.0 | `{"pageNumber":1,"rowsPerPage":30,"orderIds":"","queryCode":"ALL","orderSearchParam":"{}"}` | 已验证 |
| 订单详情（买卖共用） | `mtop.idle.web.trade.order.detail` / 1.0 | `{"tid":"订单号"}` | 已验证 |

- 买家列表状态枚举：`ALL / NOT_PAY / NOT_SHIP / SHIPPED / NOT_RATE / REFUND`；卖家列表 `queryCode`：`ALL / NOT_SHIP`。
- 买家与卖家列表是**两个不同接口**（一个在 `www.goofish.com`，一个在 `seller.goofish.com`），靠参数区分不了。卖家接口对请求头敏感：必须带 `Origin: https://seller.goofish.com`、`Referer: https://seller.goofish.com/`、`idle_site_biz_code: COMMONPRO`，且 `type` 用 `originaljson`，否则会报 `PERMISSION_EXCEPTION`。实现里通过 `MtopClient.CallAsync` 的可选 `extraHeaders` 覆盖默认的 Origin/Referer。
- 详情返回是**组件数组**（`data.components[]`，按 `render` 分 `orderInfoVO` / `addressInfoVO` / `orderStatusVO`），不同订单组合会变，解析必须容错；`data.utArgs.orderStatus` 是数字状态码。见 `OrderParser`。
