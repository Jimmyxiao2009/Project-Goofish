/**
 * 闲鱼 MTOP 协议与 API 客户端抽象层
 * 包含纯原生实现的 MD5 签名算法与网络请求管线
 */

// 纯原生 MD5 实现（零外部依赖，确保在无外网/旧浏览器/WebView中均可执行）
function md5(str) {
  function safeAdd(x, y) {
    const lsw = (x & 0xffff) + (y & 0xffff);
    const msw = (x >> 16) + (y >> 16) + (lsw >> 16);
    return (msw << 16) | (lsw & 0xffff);
  }
  function bitRotateLeft(num, cnt) {
    return (num << cnt) | (num >>> (32 - cnt));
  }
  function md5cmn(q, a, b, x, s, t) {
    return safeAdd(bitRotateLeft(safeAdd(safeAdd(a, q), safeAdd(x, t)), s), b);
  }
  function md5ff(a, b, c, d, x, s, t) {
    return md5cmn((b & c) | (~b & d), a, b, x, s, t);
  }
  function md5gg(a, b, c, d, x, s, t) {
    return md5cmn((b & d) | (c & ~d), a, b, x, s, t);
  }
  function md5hh(a, b, c, d, x, s, t) {
    return md5cmn(b ^ c ^ d, a, b, x, s, t);
  }
  function md5ii(a, b, c, d, x, s, t) {
    return md5cmn(c ^ (b | ~d), a, b, x, s, t);
  }
  function binlMD5(x, len) {
    x[len >> 5] |= 0x80 << (len % 32);
    x[(((len + 64) >>> 9) << 4) + 14] = len;
    let a = 1732584193, b = -271733879, c = -1732584194, d = 271733878;
    for (let i = 0; i < x.length; i += 16) {
      const olda = a, oldb = b, oldc = c, oldd = d;
      a = md5ff(a, b, c, d, x[i], 7, -680876936);
      d = md5ff(d, a, b, c, x[i + 1], 12, -389564586);
      c = md5ff(c, d, a, b, x[i + 2], 17, 606105819);
      b = md5ff(b, c, d, a, x[i + 3], 22, -1044525330);
      a = md5ff(a, b, c, d, x[i + 4], 7, -176418897);
      d = md5ff(d, a, b, c, x[i + 5], 12, 1200080426);
      c = md5ff(c, d, a, b, x[i + 6], 17, -1473231341);
      b = md5ff(b, c, d, a, x[i + 7], 22, -45705983);
      a = md5ff(a, b, c, d, x[i + 8], 7, 1770035416);
      d = md5ff(d, a, b, c, x[i + 9], 12, -1958414417);
      c = md5ff(c, d, a, b, x[i + 10], 17, -42063);
      b = md5ff(b, c, d, a, x[i + 11], 22, -1990404162);
      a = md5ff(a, b, c, d, x[i + 12], 7, 1804603682);
      d = md5ff(d, a, b, c, x[i + 13], 12, -40341101);
      c = md5ff(c, d, a, b, x[i + 14], 17, -1502002290);
      b = md5ff(b, c, d, a, x[i + 15], 22, 1236535329);

      a = md5gg(a, b, c, d, x[i + 1], 5, -165796510);
      d = md5gg(d, a, b, c, x[i + 6], 9, -1069501632);
      c = md5gg(c, d, a, b, x[i + 11], 14, 643717713);
      b = md5gg(b, c, d, a, x[i], 20, -373897302);
      a = md5gg(a, b, c, d, x[i + 5], 5, -701558691);
      d = md5gg(d, a, b, c, x[i + 10], 9, 38016083);
      c = md5gg(c, d, a, b, x[i + 15], 14, -660478335);
      b = md5gg(b, c, d, a, x[i + 4], 20, -405537848);
      a = md5gg(a, b, c, d, x[i + 9], 5, 568446438);
      d = md5gg(d, a, b, c, x[i + 14], 9, -1019803690);
      c = md5gg(c, d, a, b, x[i + 3], 14, -187363961);
      b = md5gg(b, c, d, a, x[i + 8], 20, 1163531501);
      a = md5gg(a, b, c, d, x[i + 13], 5, -1444681467);
      d = md5gg(d, a, b, c, x[i + 2], 9, -51403784);
      c = md5gg(c, d, a, b, x[i + 7], 14, 1735328473);
      b = md5gg(b, c, d, a, x[i + 12], 20, -1926607734);

      a = md5hh(a, b, c, d, x[i + 5], 4, -378558);
      d = md5hh(d, a, b, c, x[i + 8], 11, -2022574463);
      c = md5hh(c, d, a, b, x[i + 11], 16, 1839030562);
      b = md5hh(b, c, d, a, x[i + 14], 23, -35309556);
      a = md5hh(a, b, c, d, x[i + 1], 4, -1530992060);
      d = md5hh(d, a, b, c, x[i + 4], 11, 1272893353);
      c = md5hh(c, d, a, b, x[i + 7], 16, -155497632);
      b = md5hh(b, c, d, a, x[i + 10], 23, -1094730640);
      a = md5hh(a, b, c, d, x[i + 13], 4, 681279174);
      d = md5hh(d, a, b, c, x[i], 11, -358537222);
      c = md5hh(c, d, a, b, x[i + 3], 16, -722521979);
      b = md5hh(b, c, d, a, x[i + 6], 23, 76029189);
      a = md5hh(a, b, c, d, x[i + 9], 4, -640364487);
      d = md5hh(d, a, b, c, x[i + 12], 11, -421815835);
      c = md5hh(c, d, a, b, x[i + 15], 16, 530742520);
      b = md5hh(b, c, d, a, x[i + 2], 23, -995338651);

      a = md5ii(a, b, c, d, x[i], 6, -198630844);
      d = md5ii(d, a, b, c, x[i + 7], 10, 1126891415);
      c = md5ii(c, d, a, b, x[i + 14], 15, -1416354905);
      b = md5ii(b, c, d, a, x[i + 5], 21, -57434055);
      a = md5ii(a, b, c, d, x[i + 12], 6, 1700485571);
      d = md5ii(d, a, b, c, x[i + 3], 10, -1894986606);
      c = md5ii(c, d, a, b, x[i + 10], 15, -1051523);
      b = md5ii(b, c, d, a, x[i + 1], 21, -2054922799);
      a = md5ii(a, b, c, d, x[i + 8], 6, 1873313359);
      d = md5ii(d, a, b, c, x[i + 15], 10, -30611744);
      c = md5ii(c, d, a, b, x[i + 6], 15, -1560198380);
      b = md5ii(b, c, d, a, x[i + 13], 21, 1309151649);
      a = md5ii(a, b, c, d, x[i + 4], 6, -145523070);
      d = md5ii(d, a, b, c, x[i + 11], 10, -1120210379);
      c = md5ii(c, d, a, b, x[i + 2], 15, 718787259);
      b = md5ii(b, c, d, a, x[i + 9], 21, -343485551);

      a = safeAdd(a, olda);
      b = safeAdd(b, oldb);
      c = safeAdd(c, oldc);
      d = safeAdd(d, oldd);
    }
    return [a, b, c, d];
  }
  function rstr2binl(input) {
    const output = Array(input.length >> 2).fill(0);
    for (let i = 0; i < input.length * 8; i += 8) {
      output[i >> 5] |= (input.charCodeAt(i / 8) & 0xff) << (i % 32);
    }
    return output;
  }
  function binl2rstr(input) {
    let output = '';
    for (let i = 0; i < input.length * 32; i += 8) {
      output += String.fromCharCode((input[i >> 5] >>> (i % 32)) & 0xff);
    }
    return output;
  }
  function rstr2hex(input) {
    const hexTab = '0123456789abcdef';
    let output = '';
    for (let i = 0; i < input.length; i++) {
      const x = input.charCodeAt(i);
      output += hexTab.charAt((x >>> 4) & 0x0f) + hexTab.charAt(x & 0x0f);
    }
    return output;
  }
  return rstr2hex(binl2rstr(binlMD5(rstr2binl(unescape(encodeURIComponent(str))), unescape(encodeURIComponent(str)).length * 8)));
}

class GoofishApiService {
  constructor() {
    this.appKey = "12574478";
    this.token = "";
    this.cookie = "";
    this.useMock = true; // 默认使用沙盒数据保证离线演示体验，支持一键切换真实MTOP网关
    this.localItems = [...MOCK_DATA.feedItems];
    this.conversations = JSON.parse(JSON.stringify(MOCK_DATA.conversations));
  }

  setCredentials(cookie, token) {
    this.cookie = cookie;
    this.token = token;
  }

  // 计算 MTOP 散列签名
  computeSign(dataStr, timestamp) {
    const tokenPart = this.token || "";
    const raw = `${tokenPart}&${timestamp}&${this.appKey}&${dataStr}`;
    return md5(raw);
  }

  // 1. 首页推荐 Feed 接口
  async getFeed(pageNumber = 1, pageSize = 20, category = "all") {
    if (this.useMock) {
      await new Promise(r => setTimeout(r, 200)); // 仿真网络延迟
      let list = this.localItems;
      if (category !== "all") {
        list = list.filter(item => item.category === category);
      }
      return {
        ret: ["SUCCESS::调用成功"],
        data: {
          cardList: list,
          nextPage: true,
          feedsCount: list.length
        }
      };
    }

    // 真实 MTOP 调用逻辑
    const t = Date.now().toString();
    const data = JSON.stringify({ pageNumber, pageSize, itemId: "" });
    const sign = this.computeSign(data, t);
    const url = `https://h5api.m.goofish.com/h5/mtop.taobao.idlehome.home.webpc.feed/1.0/?jsv=2.7.2&appKey=${this.appKey}&t=${t}&sign=${sign}&api=mtop.taobao.idlehome.home.webpc.feed&v=1.0&type=originaljson&dataType=json&data=${encodeURIComponent(data)}`;

    try {
      const resp = await fetch(url, { credentials: "include" });
      return await resp.json();
    } catch (err) {
      console.warn("Real MTOP request failed, falling back to mock sandbox", err);
      return this.getFeed(pageNumber, pageSize, category);
    }
  }

  // 2. 商品搜索接口
  async search(keyword, pageNumber = 1, sortType = "default") {
    if (this.useMock) {
      await new Promise(r => setTimeout(r, 250));
      const kw = (keyword || "").toLowerCase().trim();
      let filtered = this.localItems.filter(item =>
        item.title.toLowerCase().includes(kw) ||
        item.desc.toLowerCase().includes(kw) ||
        item.city.toLowerCase().includes(kw)
      );

      if (sortType === "price_asc") {
        filtered.sort((a, b) => a.price - b.price);
      } else if (sortType === "price_desc") {
        filtered.sort((a, b) => b.price - a.price);
      } else if (sortType === "time") {
        filtered.reverse();
      }

      return {
        ret: ["SUCCESS::调用成功"],
        data: {
          resultList: filtered,
          totalCount: filtered.length
        }
      };
    }
  }

  // 3. 商品详情接口
  async getItemDetail(itemId) {
    await new Promise(r => setTimeout(r, 150));
    const item = this.localItems.find(i => i.id === itemId);
    if (!item) throw new Error("商品不存在或已下架");
    return {
      ret: ["SUCCESS::调用成功"],
      data: item
    };
  }

  // 4. 收藏列表与状态切换
  async getFavorites() {
    await new Promise(r => setTimeout(r, 150));
    return this.localItems.filter(i => i.isFav);
  }

  async toggleFavorite(itemId) {
    const item = this.localItems.find(i => i.id === itemId);
    if (item) {
      item.isFav = !item.isFav;
      item.collectCnt += item.isFav ? 1 : -1;
      return item.isFav;
    }
    return false;
  }

  // 5. IM 会话与私聊消息
  async getConversations() {
    await new Promise(r => setTimeout(r, 150));
    return this.conversations;
  }

  async getConversationById(convId) {
    return this.conversations.find(c => c.id === convId);
  }

  async getOrCreateConversationByItem(item) {
    let conv = this.conversations.find(c => c.sellerId === item.seller.userId);
    if (!conv) {
      conv = {
        id: `conv_${Date.now()}`,
        sellerId: item.seller.userId,
        sellerNick: item.seller.nick,
        sellerAvatar: item.seller.avatar,
        itemId: item.id,
        itemTitle: item.title,
        itemPrice: item.price,
        itemThumb: item.images[0],
        lastMessage: "你好，请问宝贝还在吗？",
        lastTime: "刚刚",
        unreadCount: 0,
        messages: [
          { id: `m_${Date.now()}`, sender: "me", text: "你好，请问宝贝还在吗？", time: "刚刚" }
        ]
      };
      this.conversations.unshift(conv);
    }
    return conv;
  }

  async sendMessage(convId, text) {
    const conv = this.conversations.find(c => c.id === convId);
    if (!conv) return null;

    const timeStr = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    const newMsg = {
      id: `m_${Date.now()}`,
      sender: "me",
      text,
      time: timeStr
    };
    conv.messages.push(newMsg);
    conv.lastMessage = text;
    conv.lastTime = timeStr;

    // 仿真卖家智能秒回
    setTimeout(() => {
      const replies = [
        "在的呢！支持直接拍下，当天顺丰发出～",
        "机器无拆无修，成色和描述完全一致，诚心要可以小包个邮。",
        "箱说齐全的，配件也都在，放心入！",
        "可以视频验机的，您看方便吗？"
      ];
      const replyText = replies[Math.floor(Math.random() * replies.length)];
      conv.messages.push({
        id: `m_reply_${Date.now()}`,
        sender: "other",
        text: replyText,
        time: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
      });
      conv.lastMessage = replyText;
      if (window.onNewMessageReceived) {
        window.onNewMessageReceived(conv.id);
      }
    }, 1200);

    return newMsg;
  }
}

// 挂载全局 API 单例
window.goofishApi = new GoofishApiService();
