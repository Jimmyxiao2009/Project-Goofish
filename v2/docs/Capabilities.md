# 闲鱼网页端能力 vs 本客户端 · 差距清单

对比基线：v2 客户端已实现的能力（见 `API.md` 与 `GoofishMobile/Services/`）。
只写有出处的结论；查不到接口名的能力标「仅界面可见、接口未知」，不编造接口名。
来源项目与 `API.md` 一致（11273/goofish-client、fancyboi999/goofish-cli、timefunnel/xianyu-monitor、shaxiu/XianyuAutoAgent）。

可信度：**已验证** = 至少两个独立来源一致；**单一来源** = 只有一个项目记录；**未验证** = 推测，需抓包核对。

## 已实现（本次补齐的用 ★ 标出）

| 能力 | 接口 / 机制 | 状态 |
| --- | --- | --- |
| ★ 扫码登录（电脑版，纯 HTTP） | `passport.goofish.com` 的 `newlogin/qrcode/generate.do` → `query.do` → `login_token/login.do` | 已实现，替代原 WebView 登录 |
| 首页推荐流 | `mtop.taobao.idlehome.home.webpc.feed` / 1.0 | 已实现 |
| 搜索 | `mtop.taobao.idlemtopsearch.pc.search` / 1.0，3 种排序，无筛选 | 部分 |
| 商品详情 | `mtop.taobao.idle.pc.detail` / 1.0 | 已实现 |
| 收藏列表 | `mtop.taobao.idle.web.favor.item.list` / 1.0 | 已实现 |
| 收藏 / 取消收藏 | 接口名未验证，失败只存本机 | 部分 |
| 会话列表 | `mtop.taobao.idlemessage.pc.session.sync` / 3.0 | 已实现 |
| 当前用户信息 | `mtop.idle.web.user.page.nav` / 1.0，只取昵称/头像 | 部分 |
| ★ 卖家 / 个人主页 | `mtop.idle.web.user.page.head` / 1.0（昵称、头像、信用、粉丝、在售、评价）；在售列表按昵称搜索兜底 | 部分 |
| 私信（文字） | 钉钉 IM WebSocket | 已实现 |
| ★ 私信里的图片与视频 | `contentType` 1/2/3/4；图片 `image.pics[].url`、视频 `video.url` | 已实现（可看、可播放） |
| ★ 私信里的卡片消息 | `contentType` 5–11；按标题/描述/图片/链接/价格还原成卡片 | 已实现（字段名靠递归兜底） |
| ★ 我买到的订单 | `mtop.idle.web.trade.bought.list` / 1.0 | 已实现 |
| ★ 我卖出的订单 | `mtop.taobao.idle.trade.merchant.sold.get` / 1.0 | 已实现 |
| ★ 订单详情 | `mtop.idle.web.trade.order.detail` / 1.0 | 已实现 |

## 仍缺失（按对浏览与沟通的价值排序）

| # | 能力 | 网页端入口 | 接口（若查到） | 可信度 |
| --- | --- | --- | --- | --- |
| 1 | 搜索多维筛选（价格区间、发布天数、地区、个人闲置/包邮/全新/验货宝等） | `www.goofish.com/search` | 复用 `idlemtopsearch.pc.search`：`propValueStr.searchFilter`、`extraFilterValue` | 已验证 |
| 2 | 更多排序（信用、距离、最近活跃、新降价、修改时间） | 搜索页排序 | 同搜索接口：`sortField=credit/pos/reduce/modify` | 已验证 |
| 3 | 账号概览计数（待付款、购买/卖出次数、收藏数） | 我的闲鱼 | 已在用的 `user.page.nav` 里就有，只是没取 | 单一来源 |
| 4 | 卖家在售 / 已卖出商品列表 | 卖家主页 | 自己在售 `mtop.idle.web.xyh.item.list`；他人列表未知 | 单一来源 / 仅界面可见 |
| 5 | 想要的（want）动作 | 详情页 | 接口未知（计数可见） | 仅界面可见 |
| 6 | 消息中心分类（真人 / 系统 / 互动 / 通知） | 消息页 | 已在用的 `session.sync` 返回 `sessionType`，界面未分类 | 已验证 |
| 7 | 会话列表分页 | 消息左栏滚动 | WebSocket `/r/Conversation/listNewestPagination` | 单一来源 |
| 8 | 发送图片消息 | 聊天页 | 同发文字接口，content 换成图片结构 | 单一来源 |
| 9 | 评价明细与信用、闲气值 | 卖家主页「评价」 | `user.page.head` 有汇总；明细未知 | 部分 |
| 10 | 关注卖家 / 粉丝 | 卖家主页 | 读 `user.page.head`；写接口未知 | 读单一来源 |
| 11 | 分类导航 / 兴趣频道 | 首页顶部 | 未知 | 仅界面可见 |
| 12 | 搜索热词 / 联想建议 | 搜索框下拉 | 未知 | 仅界面可见 |
| 13 | 详情页相似宝贝 | 详情页底部 | 未知 | 仅界面可见 |
| 14 | 浏览历史 | 我的闲鱼 | 未知 | 仅界面可见 |
| 15 | 发布 / 编辑 / 下架 / 删除闲置 | 发闲置 | `mtop.idle.pc.idleitem.publish`、`com.taobao.idle.item.delete` 等 | 单一来源（写操作，不在本项目定位内） |
| 16 | 擦亮 | 我发布的 | 未知 | 仅界面可见 |
| 17 | 退款 / 售后动作 | 订单列表 | 列表可查；动作接口未知 | 仅界面可见 |
| 18 | 验货宝 / 免费送 / 举报 / 分享 | 筛选栏、详情页 | 筛选维度可查，其余未知 | 部分 |
| 19 | 直播 / 短视频 | 首页、详情 | 未知 | 仅界面可见 |
| 20 | 鱼塘 / 圈子 | 兴趣频道 | 未知 | 仅界面可见 |

## 备注

- **性价比最高的补齐点**：搜索筛选与排序（#1、#2）只需扩展现有搜索请求体，不用新增接口。
- **写操作**（发布、下架、删除、擦亮、关注）不在本项目定位内；若要引入需要额外的风控护栏（goofish-cli 用 1 写/分钟令牌桶 + RGV587 熔断）。
- 所有接口共用同一 mtop 网关（appKey `34839810`，签名同 `API.md`），新增调用同样受 `RGV587` 限流约束，`MtopClient.MinGap`（600ms）串行限速照旧适用。
