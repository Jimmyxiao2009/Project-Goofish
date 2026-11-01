/*
 * 原型数据与模拟接口。
 * 字段名与 UWP 工程里的 Models（ItemSummary / ItemDetail / Conversation / ChatMessage）一一对应，
 * MockApi 的方法签名也与 IGoofishDataSource 对齐，方便对照实现。
 */
(function () {
  "use strict";

  // 商品图：纯色底 + MDL2 图标，离线可用，也符合 Metro 的扁平风格
  function art(color, glyph, shade) {
    var svg =
      '<svg xmlns="http://www.w3.org/2000/svg" width="360" height="360" viewBox="0 0 360 360">' +
      '<rect width="360" height="360" fill="' + color + '"/>' +
      (shade ? '<rect y="250" width="360" height="110" fill="' + shade + '"/>' : "") +
      '<text x="180" y="210" text-anchor="middle" font-family="Segoe MDL2 Assets" font-size="120" fill="rgba(255,255,255,0.92)">' +
      glyph + "</text></svg>";
    return "data:image/svg+xml;charset=utf-8," + encodeURIComponent(svg);
  }

  var sellers = {
    s1: { userId: "2200731", nick: "柠檬汽水不加冰", city: "深圳", credit: "极好", sold: 86, reply: "98%", lastSeen: "刚刚来过", avatarColor: "#00ABA9" },
    s2: { userId: "2200845", nick: "老王的数码仓库", city: "杭州", credit: "优秀", sold: 412, reply: "92%", lastSeen: "1小时前来过", avatarColor: "#6A00FF" },
    s3: { userId: "2201137", nick: "Lumia 不死", city: "北京", credit: "极好", sold: 23, reply: "100%", lastSeen: "刚刚来过", avatarColor: "#E51400" },
    s4: { userId: "2201580", nick: "半夏微凉", city: "上海", credit: "良好", sold: 7, reply: "75%", lastSeen: "3天前来过", avatarColor: "#D80073" },
    s5: { userId: "2202016", nick: "阿杰搞机", city: "成都", credit: "极好", sold: 158, reply: "96%", lastSeen: "10分钟前来过", avatarColor: "#FA6800" }
  };

  var G = { phone: "\uE8EA", laptop: "\uE7F8", game: "\uE7FC", headset: "\uE95B", camera: "\uE722", book: "\uE82D",
            keyboard: "\uE765", tv: "\uE7F4", bike: "\uE806", watch: "\uE916", music: "\uE8D6", tablet: "\uE70A" };

  var items = [
    { itemId: "735020881256", title: "Lumia 950 XL 双卡 3+32G 黑色 带原装 Continuum Dock，屏幕无划痕", price: 680, originalPrice: 4999,
      sellerId: "s3", city: "北京", want: 37, browse: 1203, fav: 52, freeShip: true, condition: "95新", bargain: true,
      color: "#1BA1E2", glyph: G.phone, published: "3天前",
      desc: "自用备机，一直戴壳贴膜使用，屏幕完美。\n系统 Windows 10 Mobile 1709 (15254.603)，已重置。\n配件：原装 Dock、Type-C 线、充电头、原盒。\n电池健康，正常一天一充。\n情怀出，不刀太狠都可以聊。" },
    { itemId: "735020881311", title: "Surface Pro 4 i5 8G 256G 送键盘盖和触控笔", price: 1650, originalPrice: 8888,
      sellerId: "s2", city: "杭州", want: 64, browse: 2410, fav: 88, freeShip: true, condition: "9成新", bargain: false,
      color: "#647687", glyph: G.laptop, published: "5小时前",
      desc: "公司淘汰机器，屏幕无坏点无黄斑，电池损耗 18%。\n键盘盖边缘轻微磨损，笔正常使用。\n验货宝支持，支持当面交易。" },
    { itemId: "735020881402", title: "Xbox One S 1TB 白色 双手柄 + 极限竞速地平线 4 光盘", price: 999, originalPrice: 2599,
      sellerId: "s5", city: "成都", want: 21, browse: 860, fav: 30, freeShip: false, condition: "95新", bargain: true,
      color: "#60A917", glyph: G.game, published: "昨天",
      desc: "国行，没怎么玩，吃灰两年。手柄摇杆无漂移。运费到付。" },
    { itemId: "735020881577", title: "Sony WH-1000XM3 降噪耳机 黑色 国行在保", price: 860, originalPrice: 2899,
      sellerId: "s1", city: "深圳", want: 45, browse: 1522, fav: 61, freeShip: true, condition: "99新", bargain: false,
      color: "#76608A", glyph: G.headset, published: "2天前",
      desc: "买来通勤用，后来换了新款。耳罩无脱皮，盒子配件都在，保修到明年 3 月。" },
    { itemId: "735020881690", title: "Canon EF 50mm f/1.8 STM 小痰盂 无霉无雾", price: 420, originalPrice: 899,
      sellerId: "s4", city: "上海", want: 18, browse: 640, fav: 27, freeShip: true, condition: "9成新", bargain: true,
      color: "#A20025", glyph: G.camera, published: "1周前",
      desc: "入门定焦，对焦安静。前后盖都有，送一个 UV 镜。" },
    { itemId: "735020881744", title: "《深入理解计算机系统》第三版 中文 几乎全新", price: 58, originalPrice: 139,
      sellerId: "s4", city: "上海", want: 9, browse: 210, fav: 14, freeShip: false, condition: "全新", bargain: false,
      color: "#825A2C", glyph: G.book, published: "4天前",
      desc: "只翻过前两章，无笔记无折角。可以和其它几本一起打包。" },
    { itemId: "735020881839", title: "IKBC C87 机械键盘 樱桃红轴 87 键 有线", price: 199, originalPrice: 429,
      sellerId: "s5", city: "成都", want: 12, browse: 377, fav: 19, freeShip: true, condition: "95新", bargain: true,
      color: "#6D8764", glyph: G.keyboard, published: "6小时前",
      desc: "红轴手感很轻，打字游戏两用。键帽有轻微打油，可以送一套新键帽。" },
    { itemId: "735020881902", title: "Dell U2414H 24 寸 IPS 显示器 窄边框 可旋转升降", price: 520, originalPrice: 1699,
      sellerId: "s2", city: "杭州", want: 29, browse: 990, fav: 33, freeShip: false, condition: "9成新", bargain: true,
      color: "#0050EF", glyph: G.tv, published: "昨天",
      desc: "无亮点无坏点，支架完好。只限杭州自提，顺便可以看看其它闲置。" },
    { itemId: "735020882015", title: "迪卡侬 RC100 公路车 M 码 骑行不到 300 公里", price: 1100, originalPrice: 1999,
      sellerId: "s1", city: "深圳", want: 33, browse: 1104, fav: 40, freeShip: false, condition: "95新", bargain: true,
      color: "#008A00", glyph: G.bike, published: "2天前",
      desc: "搬家出，送码表支架和水壶架。仅限深圳南山自提。" },
    { itemId: "735020882188", title: "Microsoft Band 2 智能手环 M 码 带原装充电线", price: 150, originalPrice: 1688,
      sellerId: "s3", city: "北京", want: 15, browse: 498, fav: 22, freeShip: true, condition: "8成新", bargain: false,
      color: "#F0A30A", glyph: G.watch, published: "5天前",
      desc: "表带有一处小裂口，不影响佩戴。可以连接 Windows 10 Mobile 上的 Microsoft Health。" },
    { itemId: "735020882231", title: "Kindle Paperwhite 3 8G 白色 带保护套", price: 260, originalPrice: 958,
      sellerId: "s4", city: "上海", want: 51, browse: 1840, fav: 70, freeShip: true, condition: "9成新", bargain: true,
      color: "#87794E", glyph: G.tablet, published: "3小时前",
      desc: "屏幕无划痕，电池耐用。已注销账号。" },
    { itemId: "735020882390", title: "雅马哈 F310 民谣吉他 41 寸 送琴包变调夹", price: 380, originalPrice: 899,
      sellerId: "s5", city: "成都", want: 8, browse: 260, fav: 11, freeShip: false, condition: "9成新", bargain: true,
      color: "#D80073", glyph: G.music, published: "1周前",
      desc: "入门琴，音准正常，琴颈无弯曲。" }
  ];

  items.forEach(function (it) {
    it.seller = sellers[it.sellerId];
    it.images = [art(it.color, it.glyph), art(it.color, it.glyph, "rgba(0,0,0,0.25)"), art(it.color, "\uE8B9", "rgba(0,0,0,0.15)")];
    it.cover = it.images[0];
  });

  var byId = {};
  items.forEach(function (it) { byId[it.itemId] = it; });

  var conversations = [
    { cid: "48820017331", itemId: "735020881256", peer: sellers.s3, unread: 2, time: "20:02",
      messages: [
        { from: "me", text: "你好，Lumia 950 XL 还在吗？", time: "19:55" },
        { from: "peer", text: "在的，今天刚擦干净拍的照片", time: "19:58" },
        { from: "peer", text: "Dock 也是原装的，盒子说明书都在", time: "20:02" }
      ] },
    { cid: "48820017402", itemId: "735020881577", peer: sellers.s1, unread: 0, time: "昨天",
      messages: [
        { from: "me", text: "耳机支持验货吗？", time: "昨天 21:13" },
        { from: "peer", text: "可以走验货宝，运费我出", time: "昨天 21:20" },
        { from: "me", text: "好的，我考虑一下", time: "昨天 21:21" }
      ] },
    { cid: "48820017588", itemId: "735020881902", peer: sellers.s2, unread: 0, time: "周一",
      messages: [
        { from: "me", text: "显示器能便宜点吗", time: "周一 12:40" },
        { from: "peer", text: "最低 500，自提的话再送一根 DP 线", time: "周一 13:02" }
      ] }
  ];

  // 收藏：reduce 表示降价金额，invalid 表示已下架
  var favorites = [
    { itemId: "735020881311", reduce: 150, invalid: false, favTime: "10月2日" },
    { itemId: "735020882231", reduce: 0, invalid: false, favTime: "9月28日" },
    { itemId: "735020881744", reduce: 0, invalid: true, favTime: "9月20日" }
  ];

  var history = ["Lumia", "Surface", "Xbox 手柄"];
  var hot = ["Lumia 950", "Windows Phone", "Surface Pro", "Xbox", "机械键盘", "Kindle"];

  function delay(ms, value) {
    return new Promise(function (resolve) { setTimeout(function () { resolve(value); }, ms); });
  }

  function clone(o) { return JSON.parse(JSON.stringify(o)); }

  // 模拟分页：每页 6 条，共 3 页，后两页对原数据换序并重新编号
  function page(list, pageNumber, size) {
    var out = [];
    for (var i = 0; i < size; i++) {
      var idx = (pageNumber - 1) * size + i;
      if (idx >= list.length * 2) break;
      var src = list[idx % list.length];
      out.push(src);
    }
    return out;
  }

  var replies = [
    "在的，诚心要可以小刀",
    "包邮的，今天拍下今天发顺丰",
    "可以视频看实物，你方便的时候说一声",
    "成色描述都是真实的，放心",
    "最低价了哈，已经很便宜了"
  ];

  window.MockApi = {
    hotKeywords: hot,
    searchHistory: history,

    getHomeFeed: function (pageNumber) {
      var size = 6;
      var list = page(items, pageNumber, size);
      return delay(700, { items: list, nextPage: pageNumber < 3 });
    },

    search: function (keyword, sort, pageNumber) {
      var kw = (keyword || "").toLowerCase().trim();
      var list = items.filter(function (it) {
        return !kw || it.title.toLowerCase().indexOf(kw) >= 0 || it.city.indexOf(kw) >= 0 ||
          it.seller.nick.toLowerCase().indexOf(kw) >= 0;
      });
      if (!list.length && kw) list = items.slice(0, 4); // 模拟模糊匹配
      if (sort === "price_asc") list.sort(function (a, b) { return a.price - b.price; });
      if (sort === "price_desc") list.sort(function (a, b) { return b.price - a.price; });
      if (sort === "newest") list.sort(function (a, b) { return a.itemId < b.itemId ? 1 : -1; });
      return delay(600, { items: list, total: list.length, nextPage: false });
    },

    getItemDetail: function (itemId) {
      return delay(450, byId[itemId]);
    },

    getFavorites: function (type) {
      var list = favorites.map(function (f) {
        var it = byId[f.itemId];
        return Object.assign({}, it, { reduce: f.reduce, invalid: f.invalid, favTime: f.favTime });
      }).filter(function (f) {
        if (type === "REDUCE") return f.reduce > 0;
        if (type === "VALID") return !f.invalid;
        if (type === "INVALID") return f.invalid;
        return true;
      });
      return delay(400, list);
    },

    isFavorite: function (itemId) {
      return favorites.some(function (f) { return f.itemId === itemId; });
    },

    setFavorite: function (itemId, on) {
      favorites = favorites.filter(function (f) { return f.itemId !== itemId; });
      if (on) favorites.unshift({ itemId: itemId, reduce: 0, invalid: false, favTime: "刚刚" });
      return delay(300, on);
    },

    getConversations: function () {
      return delay(400, conversations.map(function (c) {
        var last = c.messages[c.messages.length - 1];
        return { cid: c.cid, item: byId[c.itemId], peer: c.peer, unread: c.unread, time: c.time,
                 lastMessage: (last.from === "me" ? "我: " : "") + last.text };
      }));
    },

    unreadCount: function () {
      return conversations.reduce(function (n, c) { return n + c.unread; }, 0);
    },

    openConversation: function (cid) {
      var c = conversations.filter(function (x) { return x.cid === cid; })[0];
      c.unread = 0;
      return delay(350, { cid: c.cid, item: byId[c.itemId], peer: c.peer, messages: clone(c.messages) });
    },

    // 对应 IM 的 /r/SingleChatConversation/create：同一商品 + 卖家复用已有会话
    createConversation: function (itemId) {
      var it = byId[itemId];
      var c = conversations.filter(function (x) { return x.itemId === itemId; })[0];
      if (!c) {
        c = { cid: "4882" + Date.now().toString().slice(-7), itemId: itemId, peer: it.seller, unread: 0, time: "刚刚", messages: [] };
        conversations.unshift(c);
      }
      return delay(300, c.cid);
    },

    // 对应 /r/MessageSend/sendByReceiverScope；onReply 模拟 /s/sync 下行推送
    sendMessage: function (cid, text, onReply) {
      var c = conversations.filter(function (x) { return x.cid === cid; })[0];
      var now = new Date();
      var t = ("0" + now.getHours()).slice(-2) + ":" + ("0" + now.getMinutes()).slice(-2);
      c.messages.push({ from: "me", text: text, time: t });
      c.time = t;
      setTimeout(function () {
        var reply = { from: "peer", text: replies[Math.floor(Math.random() * replies.length)], time: t };
        c.messages.push(reply);
        if (onReply) onReply(c, reply);
      }, 2200);
      return delay(350, { from: "me", text: text, time: t });
    },

    markUnread: function (cid) {
      var c = conversations.filter(function (x) { return x.cid === cid; })[0];
      if (c) c.unread += 1;
    }
  };
})();
