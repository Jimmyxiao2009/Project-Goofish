# 闲鱼 (Goofish) API 接口深度分析与 Windows 10 Mobile 架构设计

> 本文档由逆向工程与开放社区协议分析整理，旨在为开发第三方 Windows 10 Mobile (UWP) 客户端提供完整的协议参考与技术可行性论证。

---

## 一、闲鱼接口核心机制概览

闲鱼 (Goofish) 属于阿里巴巴集团旗下二手交易平台，其核心网络接口沿用阿里系的 **MTOP (Mobile Taobao Open Platform)** 网关协议，即时通讯模块则依托阿里自研的 **钉钉 (DingTalk) 云端 WebSocket 协议**。

### 1. MTOP 网关架构
* **Web/H5 网关地址**：`https://h5api.m.goofish.com/h5/{api}/{version}/`
* **备用/Taobao 通道**：`https://h5api.m.taobao.com/h5/{api}/{version}/`
* **传输协议**：HTTPS POST / GET（通常使用 `application/x-www-form-urlencoded`）
* **公共参数**：
  | 参数名 | 典型值 | 说明 |
  | :--- | :--- | :--- |
  | `jsv` | `2.7.2` | MTOP JS-SDK 版本 |
  | `appKey` | `12574478` (Web) / `34534403` (PC) | 闲鱼客户端公开 AppKey |
  | `t` | 当前毫秒时间戳 (如 `1712345678901`) | 请求时间戳，用于防重放 |
  | `sign` | 32位小写 MD5 散列值 | 动态防篡改签名 |
  | `api` | 如 `mtop.taobao.idlehome.home.webpc.feed` | 目标接口名称 |
  | `v` | 如 `1.0` | 接口版本号 |
  | `type` | `originaljson` | 返回格式（避免 JSONP 包裹） |
  | `dataType` | `json` | 数据类型 |
  | `data` | JSON 格式字符串 | 业务参数对象序列化字符串 |

---

## 二、MTOP 签名 (Sign) 与 Token 握手机制

MTOP 的 Web/H5 签名机制相对清晰且可在第三方客户端中完全自主实现：

### 1. 签名算法公式
```text
sign = MD5(token + "&" + t + "&" + appKey + "&" + data)
```
* **`token`**：从 Cookie 中的 `_m_h5_tk` 字段获取。例如 Cookie 为 `_m_h5_tk=abcdef1234567890_1712349999999;`，截取前半段 `abcdef1234567890` 作为 `token`。
* **`t`**：13位毫秒级时间戳字符串。
* **`appKey`**：`12574478`。
* **`data`**：请求接口的具体业务 JSON 字符串（例如 `{"pageNumber":1,"pageSize":30}`）。

### 2. 空 Token 自动握手流程
1. 客户端首次发起请求时，本地没有 `_m_h5_tk` Cookie，此时 `token` 为空字符串。
2. 发起请求后，MTOP 服务器返回：
   ```json
   { "ret": ["FAIL_SYS_TOKEN_EMPTY::令牌为空"], "data": {} }
   ```
3. 同时服务器在 HTTP Response Headers 的 `Set-Cookie` 中下发 `_m_h5_tk` 和 `_m_h5_tk_enc`。
4. 客户端捕获并保存该 Cookie，提取 `token`，重新按公式计算 `sign`，再次发起请求即可成功。

---

## 三、核心功能接口规范

### 1. 首页推荐 Feed
* **API 路径**：`mtop.taobao.idlehome.home.webpc.feed`
* **版本**：`1.0`
* **请求数据 (`data`)**：
  ```json
  {
    "pageNumber": 1,
    "pageSize": 30,
    "itemId": ""
  }
  ```
* **核心返回字段**：
  - `cardList[]`: 商品卡片列表
  - `cardList[i].cardData.itemId`: 商品唯一ID
  - `cardList[i].cardData.titleSummary.title`: 商品标题
  - `cardList[i].cardData.priceInfo.price`: 当前价格
  - `cardList[i].cardData.city`: 卖家所在城市
  - `cardList[i].cardData.user.userNick`: 卖家昵称
  - `cardList[i].cardData.user.avatar`: 卖家头像
  - `cardList[i].cardData.images[]`: 商品图片列表

### 2. 商品搜索
* **API 路径**：`mtop.taobao.idle.web.item.search`
* **版本**：`1.0`
* **请求数据 (`data`)**：
  ```json
  {
    "keyword": "ThinkPad",
    "pageNumber": 1,
    "rowsPerPage": 20,
    "sortField": "",
    "sortValue": ""
  }
  ```
* **排序参数**：
  - 默认综合排序：`sortField: "", sortValue: ""`
  - 价格升序：`sortField: "price", sortValue: "asc"`
  - 价格降序：`sortField: "price", sortValue: "desc"`
  - 发布时间：`sortField: "publishTime", sortValue: "desc"`

### 3. 商品详情
* **API 路径**：`mtop.taobao.idle.pc.detail`
* **版本**：`1.0`
* **请求数据 (`data`)**：
  ```json
  {
    "itemId": "761234567890"
  }
  ```
* **核心返回字段**：
  - `itemDO.title`: 商品标题
  - `itemDO.desc`: 商品完整图文描述
  - `itemDO.soldPrice`: 售卖价格
  - `itemDO.originalPrice`: 初始买入价格
  - `itemDO.transportFee`: 运费 (如 `0.00` 免运费)
  - `itemDO.imageInfos[]`: 高清大图列表
  - `itemDO.browseCnt`: 浏览热度
  - `itemDO.collectCnt`: 收藏人数
  - `sellerDO.nick`: 卖家昵称
  - `sellerDO.userId`: 卖家用户ID
  - `sellerDO.avatar`: 卖家头像
  - `sellerDO.creditScore`: 芝麻信用评级 (如 "信用极好")

### 4. 我的收藏列表 & 收藏操作
* **获取收藏列表**：`mtop.taobao.idle.web.favor.item.list`
  ```json
  {
    "pageNumber": 1,
    "rowsPerPage": 20,
    "type": "DEFAULT"
  }
  ```
  *(type 可选：`DEFAULT` 全部, `REDUCE` 降价, `VALID` 有效, `INVALID` 失效)*
* **添加收藏**：`mtop.taobao.idle.favor.item.add`
  ```json
  { "itemId": "761234567890" }
  ```
* **取消收藏**：`mtop.taobao.idle.favor.item.remove`
  ```json
  { "itemId": "761234567890" }
  ```

---

## 四、IM 即时通讯（与卖家对话）架构

闲鱼的私聊基于 WebSocket 实时长连接系统：

### 1. 认证流程
1. **获取 IM Token**：
   调用 MTOP 接口 `mtop.taobao.idlemessage.pc.login.token` 获取连接所需的临时通信 Token 与设备凭证。
2. **连接 WebSocket**：
   连接至网关：`wss://wss-goofish.dingtalk.com/`。
3. **注册与同步 (`/reg`)**：
   通过 WebSocket 发送注册包，带上 token、deviceId、appKey，握手成功后服务进入实时心跳（10秒一次）。

### 2. 会话管理与收发消息
* **获取会话列表**：向 WebSocket 发送 `/r/Conversation/listNewestPagination` 请求，返回与各卖家的最近聊天列表、最后一条消息和未读数。
* **发起单聊**：向 WebSocket 发送 `/r/SingleChatConversation/create`，传入 `itemId` 与 `targetUserId`，建立特定商品的沟通通道。
* **发送消息**：向 `/r/MessageSend/sendByReceiverScope` 发送文本或图片消息。
* **接收推送**：监听来自 `/s/sync` 的下行数据包，触发 Windows 10 Mobile 系统 Toast 通知与 UI 刷新。

---

## 五、Windows 10 Mobile 架构设计建议

在 Windows 10 Mobile 操作系统上，我们采用经典 **UWP (Universal Windows Platform)** 架构开发：
* **UI 范式**：遵循微软 Metro / MDL2 (Microsoft Design Language 2) 移动端规范。
* **网络层**：`Windows.Web.Http.HttpClient`（自带系统级 CookieJar 管理）配合 C# `MD5` 散列算法。
* **长连接层**：`Windows.Networking.Sockets.MessageWebSocket`，极度节省 Windows 10 Mobile 电池与后台资源。
* **离线/沙盒模式**：内置 Mock 演示数据引擎，即使在没有淘宝/闲鱼 Cookie 或处于网络受限环境时，也能丝滑浏览与体验所有页面。
