/*
 * 原型交互层。结构刻意贴近 UWP 工程：
 *   - pages.xxx 对应 Views/XxxPage.xaml(.cs)，show() ≈ OnNavigatedTo
 *   - appbar() ≈ Page.BottomAppBar
 *   - nav.go / nav.back ≈ Frame.Navigate / SystemNavigationManager.BackRequested
 */
(function () {
  "use strict";

  var api = window.MockApi;
  var $ = function (sel, root) { return (root || document).querySelector(sel); };
  var $$ = function (sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); };

  function esc(s) {
    return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
    });
  }
  function money(n) { return Number(n).toLocaleString("zh-CN", { maximumFractionDigits: 2 }); }
  function initial(name) { return esc((name || "?").slice(0, 1)); }

  var state = {
    theme: "dark",
    accent: "#0078D7",
    signedIn: true,
    demoMode: true,
    dataSaver: false,
    notify: true,
    pivot: 0,
    favFilter: "DEFAULT",
    search: { keyword: "", sort: "default" },
    chatCid: null
  };

  var screen = $("#screen");

  /* ================= 系统托盘进度（StatusBar.ProgressIndicator） ================= */
  var busyCount = 0;
  var tray = {
    busy: function (text) {
      busyCount++;
      $("#tray-text").textContent = text || "正在加载...";
      $("#tray").classList.add("busy");
    },
    idle: function () {
      busyCount = Math.max(0, busyCount - 1);
      if (!busyCount) $("#tray").classList.remove("busy");
    }
  };
  function withBusy(text, promise) {
    tray.busy(text);
    return promise.then(function (v) { tray.idle(); return v; }, function (e) { tray.idle(); throw e; });
  }

  function tickClock() {
    var d = new Date();
    $("#clock").textContent = d.getHours() + ":" + ("0" + d.getMinutes()).slice(-2);
  }
  tickClock();
  setInterval(tickClock, 15000);

  /* ================= CommandBar ================= */
  var appbarEl = $("#appbar");
  var appbar = {
    set: function (cfg) {
      appbar.close();
      if (!cfg) { appbarEl.classList.add("hidden"); return; }
      appbarEl.classList.remove("hidden");
      var primary = $("#appbar-primary");
      primary.innerHTML = "";
      (cfg.primary || []).forEach(function (b) {
        var btn = document.createElement("button");
        btn.className = "appbar-btn" + (b.checked ? " checked" : "");
        btn.disabled = !!b.disabled;
        btn.innerHTML = '<span class="mdl2">' + b.icon + '</span><span class="label">' + esc(b.label) + "</span>";
        btn.title = b.label;
        if (b.id) btn.id = b.id;
        btn.addEventListener("click", function (ev) { appbar.close(); b.action(ev, btn); });
        primary.appendChild(btn);
      });
      var sec = $("#appbar-secondary");
      sec.innerHTML = "";
      (cfg.secondary || []).forEach(function (s) {
        var btn = document.createElement("button");
        btn.textContent = s.label;
        btn.addEventListener("click", function () { appbar.close(); s.action(); });
        sec.appendChild(btn);
      });
      $("#appbar-more").style.visibility = (cfg.secondary && cfg.secondary.length) || (cfg.primary && cfg.primary.length) ? "visible" : "hidden";
    },
    toggle: function () {
      var open = !appbarEl.classList.contains("open");
      appbarEl.classList.toggle("open", open);
      $("#appbar-scrim").classList.toggle("show", open);
    },
    close: function () {
      appbarEl.classList.remove("open");
      $("#appbar-scrim").classList.remove("show");
    },
    isOpen: function () { return appbarEl.classList.contains("open"); }
  };
  $("#appbar-more").addEventListener("click", appbar.toggle);
  $("#appbar-scrim").addEventListener("click", appbar.close);

  /* ================= 浮层 ================= */
  var overlay = { picker: null, dialog: null, menu: false };

  // ComboBox 在手机上的全屏选择器
  function showPicker(title, options, selected) {
    return new Promise(function (resolve) {
      var el = $("#picker");
      el.innerHTML = '<div class="picker-header">' + esc(title) + "</div>" + options.map(function (o) {
        return '<div class="picker-item tilt' + (o.value === selected ? " selected" : "") + '" data-v="' + esc(o.value) + '">' + esc(o.label) + "</div>";
      }).join("");
      el.classList.add("show");
      overlay.picker = function (v) { el.classList.remove("show"); overlay.picker = null; resolve(v); };
      $$(".picker-item", el).forEach(function (n) {
        n.addEventListener("click", function () { overlay.picker(n.getAttribute("data-v")); });
      });
    });
  }

  function showDialog(opts) {
    return new Promise(function (resolve) {
      var el = $("#dialog");
      var box = $(".dialog", el);
      box.innerHTML =
        (opts.title ? '<div class="dialog-title">' + esc(opts.title) + "</div>" : "") +
        '<div class="dialog-body">' + (opts.html || "") + "</div>" +
        '<div class="dialog-actions">' +
        (opts.primary ? '<button class="btn accent" data-r="primary">' + esc(opts.primary) + "</button>" : "") +
        (opts.secondary ? '<button class="btn" data-r="secondary">' + esc(opts.secondary) + "</button>" : "") +
        "</div>";
      el.classList.add("show");
      overlay.dialog = function (r) { el.classList.remove("show"); overlay.dialog = null; resolve(r); };
      $$("[data-r]", box).forEach(function (b) {
        b.addEventListener("click", function () { overlay.dialog(b.getAttribute("data-r")); });
      });
      if (opts.onOpen) opts.onOpen(box);
    });
  }

  function showMenu(anchorEvent, items) {
    var menu = $("#menu");
    var rect = screen.getBoundingClientRect();
    menu.innerHTML = "";
    items.forEach(function (it) {
      var b = document.createElement("button");
      b.textContent = it.label;
      b.addEventListener("click", function () { closeMenu(); it.action(); });
      menu.appendChild(b);
    });
    menu.classList.add("show");
    var x = Math.min(anchorEvent.clientX - rect.left, 360 - menu.offsetWidth - 8);
    var y = Math.min(anchorEvent.clientY - rect.top, 560 - menu.offsetHeight);
    menu.style.left = Math.max(8, x) + "px";
    menu.style.top = Math.max(32, y) + "px";
    $("#menu-scrim").classList.add("show");
    overlay.menu = true;
  }
  function closeMenu() {
    $("#menu").classList.remove("show");
    $("#menu-scrim").classList.remove("show");
    overlay.menu = false;
  }
  $("#menu-scrim").addEventListener("click", closeMenu);

  var toastTimer;
  function toast(title, body, onClick) {
    var t = $("#toast");
    $("#toast-title").textContent = title;
    $("#toast-body").textContent = body;
    t.onclick = function () { t.classList.remove("show"); if (onClick) onClick(); };
    t.classList.add("show");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { t.classList.remove("show"); }, 4000);
  }

  // 长按（触屏）与右键（鼠标）都触发上下文菜单，与 UWP 的 RightTapped/Holding 行为一致
  function onHold(el, handler) {
    var timer, sx, sy, fired = false;
    el.addEventListener("contextmenu", function (e) { e.preventDefault(); handler(e); });
    el.addEventListener("pointerdown", function (e) {
      if (e.pointerType === "mouse") return;
      fired = false; sx = e.clientX; sy = e.clientY;
      timer = setTimeout(function () { fired = true; handler(e); }, 550);
    });
    el.addEventListener("pointermove", function (e) {
      if (Math.abs(e.clientX - sx) > 8 || Math.abs(e.clientY - sy) > 8) clearTimeout(timer);
    });
    el.addEventListener("pointerup", function () { clearTimeout(timer); });
    el.addEventListener("click", function (e) { if (fired) { e.stopImmediatePropagation(); e.preventDefault(); } }, true);
  }

  /* ================= 导航 ================= */
  var stack = [];
  var pages = {};

  var nav = {
    go: function (name, params, opts) {
      stack.push({ name: name, params: params || {} });
      activate(name, "enter");
      pages[name].show(params || {});
      appbar.set(pages[name].appbar ? pages[name].appbar() : null);
      if (opts && opts.replaceRoot) stack = [stack[stack.length - 1]];
    },
    back: function () {
      if (overlay.picker) { overlay.picker(null); return true; }
      if (overlay.dialog) { overlay.dialog(null); return true; }
      if (overlay.menu) { closeMenu(); return true; }
      if (appbar.isOpen()) { appbar.close(); return true; }
      if (pages.search.closeSuggestions && pages.search.closeSuggestions()) return true;
      if (stack.length <= 1) {
        toast("闲鱼", "已在首页，再按返回键将退出应用（原型中不退出）");
        return false;
      }
      stack.pop();
      var top = stack[stack.length - 1];
      activate(top.name, "enter-back");
      if (pages[top.name].resume) pages[top.name].resume(top.params);
      appbar.set(pages[top.name].appbar ? pages[top.name].appbar() : null);
      return true;
    },
    current: function () { return stack.length ? stack[stack.length - 1].name : null; },
    reset: function (name, params) {
      stack = [];
      nav.go(name, params);
    }
  };

  function activate(name, anim) {
    $$(".page").forEach(function (p) {
      var on = p.getAttribute("data-page") === name;
      p.classList.remove("enter", "enter-back");
      p.classList.toggle("active", on);
      if (on) { void p.offsetWidth; p.classList.add(anim); }
    });
  }

  $("#nav-back").addEventListener("click", nav.back);
  $("#nav-search").addEventListener("click", function () { if (nav.current() !== "search") nav.go("search"); });
  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape") nav.back();
  });

  function requireSignIn(reason) {
    if (state.signedIn) return Promise.resolve(true);
    return showDialog({
      title: "需要登录",
      html: "<p>" + esc(reason) + "需要登录闲鱼账号。</p><p>登录在内置浏览器中完成，本应用不会看到你的密码。</p>",
      primary: "登录",
      secondary: "取消"
    }).then(function (r) {
      if (r === "primary") nav.go("login");
      return false;
    });
  }

  /* ================= 公共渲染片段 ================= */
  function tileHtml(it) {
    return '<div class="tile tilt" data-id="' + it.itemId + '">' +
      '<div class="tile-img" style="background-image:url(\'' + it.cover + '\')">' +
      (it.freeShip ? '<span class="badge">包邮</span>' : "") + "</div>" +
      '<div class="tile-title">' + esc(it.title) + "</div>" +
      '<div class="tile-price"><small>¥</small>' + money(it.price) + "</div>" +
      '<div class="tile-meta">' + esc(it.want) + "人想要 · " + esc(it.city) + "</div></div>";
  }

  function rowHtml(it, extra) {
    extra = extra || {};
    return '<div class="row tilt' + (it.invalid ? " invalid" : "") + '" data-id="' + it.itemId + '">' +
      '<div class="row-thumb" style="background-image:url(\'' + it.cover + '\')"></div>' +
      '<div class="row-body">' +
      '<div class="row-title">' + esc(it.title) + "</div>" +
      '<div class="row-price">¥' + money(it.price) + (it.originalPrice ? "<s>¥" + money(it.originalPrice) + "</s>" : "") + "</div>" +
      '<div class="row-meta">' + (extra.tags || "") + esc(it.city) + " · " + esc(it.seller.nick) + "</div>" +
      "</div></div>";
  }

  /* ================= 首页：Pivot（推荐 / 消息 / 收藏） ================= */
  pages.main = (function () {
    var el = $('[data-page="main"]');
    var headers = ["推荐", "消息", "收藏"];
    var feed = { page: 0, loading: false, more: true };
    var built = false;

    function build() {
      el.innerHTML =
        '<div class="pivot">' +
        '<div class="pivot-title">闲鱼</div>' +
        '<div class="pivot-headers"></div>' +
        '<div class="pivot-body">' +
        '<div class="pivot-item" data-i="0"><div class="scroll" id="feed-scroll"><div class="tile-grid" id="feed-grid"></div><div class="list-footer" id="feed-footer"></div></div></div>' +
        '<div class="pivot-item" data-i="1"><div class="scroll" id="conv-scroll"></div></div>' +
        '<div class="pivot-item" data-i="2"><div style="padding:4px 12px 8px"><button class="combo" id="fav-filter"><span></span><span class="mdl2">&#xE70D;</span></button></div><div class="scroll" id="fav-scroll"></div></div>' +
        "</div></div>";

      $("#feed-scroll").addEventListener("scroll", function (e) {
        var s = e.target;
        if (s.scrollTop + s.clientHeight > s.scrollHeight - 120) loadFeed();
      });
      $("#feed-grid").addEventListener("click", function (e) {
        var t = e.target.closest(".tile");
        if (t) nav.go("detail", { itemId: t.getAttribute("data-id") });
      });
      $("#fav-filter").addEventListener("click", function () {
        showPicker("筛选收藏", favFilters, state.favFilter).then(function (v) {
          if (v) { state.favFilter = v; loadFavorites(); }
        });
      });

      // Pivot 横向滑动
      var body = $(".pivot-body", el), sx = null, sy = 0;
      body.addEventListener("pointerdown", function (e) { sx = e.clientX; sy = e.clientY; });
      body.addEventListener("pointerup", function (e) {
        if (sx === null) return;
        var dx = e.clientX - sx, dy = e.clientY - sy;
        sx = null;
        if (Math.abs(dx) > 60 && Math.abs(dx) > Math.abs(dy) * 1.5) select(state.pivot + (dx < 0 ? 1 : -1), dx < 0 ? "left" : "right");
      });
      built = true;
    }

    var favFilters = [
      { value: "DEFAULT", label: "全部宝贝" },
      { value: "REDUCE", label: "降价宝贝" },
      { value: "VALID", label: "有效宝贝" },
      { value: "INVALID", label: "已失效" }
    ];

    // W10M Pivot：选中项总在最左侧，其余标题依次轮转排列
    function renderHeaders() {
      var box = $(".pivot-headers", el);
      var unread = api.unreadCount();
      var html = "";
      for (var k = 0; k < headers.length; k++) {
        var i = (state.pivot + k) % headers.length;
        html += '<div class="pivot-header' + (k === 0 ? " selected" : "") + '" data-i="' + i + '">' + headers[i] +
          (i === 1 && unread && state.signedIn ? '<span class="count">' + unread + "</span>" : "") + "</div>";
      }
      box.innerHTML = html;
      $$(".pivot-header", box).forEach(function (h) {
        h.addEventListener("click", function () { select(+h.getAttribute("data-i"), "left"); });
      });
    }

    function select(i, dir) {
      i = (i + headers.length) % headers.length;
      if (i === state.pivot && $(".pivot-item.active", el)) return;
      state.pivot = i;
      renderHeaders();
      $$(".pivot-item", el).forEach(function (p) {
        var on = +p.getAttribute("data-i") === i;
        p.classList.remove("slide-left", "slide-right");
        p.classList.toggle("active", on);
        if (on && dir) { void p.offsetWidth; p.classList.add("slide-" + dir); }
      });
      if (i === 0 && feed.page === 0) loadFeed();
      if (i === 1) loadConversations();
      if (i === 2) loadFavorites();
      appbar.set(appbarCfg());
    }

    function loadFeed(reset) {
      if (reset) { feed = { page: 0, loading: false, more: true }; $("#feed-grid").innerHTML = ""; $("#feed-scroll").scrollTop = 0; }
      if (feed.loading || !feed.more) return;
      feed.loading = true;
      $("#feed-footer").textContent = "正在加载...";
      withBusy("正在获取推荐...", api.getHomeFeed(feed.page + 1)).then(function (r) {
        feed.page++;
        feed.more = r.nextPage;
        feed.loading = false;
        $("#feed-grid").insertAdjacentHTML("beforeend", r.items.map(tileHtml).join(""));
        $("#feed-footer").textContent = feed.more ? "" : "没有更多推荐了";
      });
    }

    function loadConversations() {
      var box = $("#conv-scroll");
      if (!state.signedIn) {
        box.innerHTML = '<div class="empty"><span class="mdl2">&#xE8BD;</span>登录后可以和卖家聊天<br><br><button class="btn accent" id="conv-login">登录</button></div>';
        $("#conv-login").addEventListener("click", function () { nav.go("login"); });
        return;
      }
      withBusy("正在同步消息...", api.getConversations()).then(function (list) {
        if (!list.length) { box.innerHTML = '<div class="empty"><span class="mdl2">&#xE8BD;</span>还没有会话</div>'; return; }
        box.innerHTML = list.map(function (c) {
          return '<div class="conv tilt' + (c.unread ? " unread" : "") + '" data-cid="' + c.cid + '">' +
            '<div class="avatar" style="background:' + c.peer.avatarColor + '">' + initial(c.peer.nick) + "</div>" +
            '<div class="conv-body"><div class="conv-top"><div class="conv-name">' + esc(c.peer.nick) + '</div><div class="conv-time">' + esc(c.time) + "</div></div>" +
            '<div class="conv-msg">' + esc(c.lastMessage) + "</div>" +
            '<div class="conv-item">' + esc(c.item.title) + "</div></div></div>";
        }).join("");
        $$(".conv", box).forEach(function (n) {
          n.addEventListener("click", function () { nav.go("chat", { cid: n.getAttribute("data-cid") }); });
          onHold(n, function (e) {
            showMenu(e, [
              { label: "标为未读", action: function () { api.markUnread(n.getAttribute("data-cid")); loadConversations(); renderHeaders(); } },
              { label: "查看商品", action: function () { nav.go("detail", { itemId: list.filter(function (c) { return c.cid === n.getAttribute("data-cid"); })[0].item.itemId }); } }
            ]);
          });
        });
        renderHeaders();
      });
    }

    function loadFavorites() {
      var label = favFilters.filter(function (f) { return f.value === state.favFilter; })[0].label;
      $("#fav-filter span").textContent = label;
      var box = $("#fav-scroll");
      if (!state.signedIn) {
        box.innerHTML = '<div class="empty"><span class="mdl2">&#xE734;</span>登录后同步你的闲鱼收藏<br><br><button class="btn accent" id="fav-login">登录</button></div>';
        $("#fav-login").addEventListener("click", function () { nav.go("login"); });
        return;
      }
      withBusy("正在获取收藏...", api.getFavorites(state.favFilter)).then(function (list) {
        if (!list.length) { box.innerHTML = '<div class="empty"><span class="mdl2">&#xE734;</span>这里还没有宝贝</div>'; return; }
        box.innerHTML = list.map(function (it) {
          var tags = (it.reduce ? '<span class="tag">降 ¥' + it.reduce + "</span>" : "") + (it.invalid ? '<span class="tag">已失效</span>' : "");
          return rowHtml(it, { tags: tags });
        }).join("") + '<div class="list-footer">长按宝贝可取消收藏</div>';
        $$(".row", box).forEach(function (n) {
          var id = n.getAttribute("data-id");
          n.addEventListener("click", function () { nav.go("detail", { itemId: id }); });
          onHold(n, function (e) {
            showMenu(e, [
              { label: "取消收藏", action: function () { withBusy("正在取消收藏...", api.setFavorite(id, false)).then(loadFavorites); } },
              { label: "联系卖家", action: function () { startChat(id); } }
            ]);
          });
        });
      });
    }

    function appbarCfg() {
      var secondary = [
        { label: "设置", action: function () { nav.go("settings"); } },
        { label: state.signedIn ? "账户" : "登录", action: function () { nav.go(state.signedIn ? "settings" : "login"); } },
        { label: "关于", action: about }
      ];
      if (state.pivot === 0) {
        return {
          primary: [
            { icon: "&#xE721;", label: "搜索", action: function () { nav.go("search"); } },
            { icon: "&#xE72C;", label: "刷新", action: function () { loadFeed(true); } }
          ],
          secondary: secondary
        };
      }
      return {
        primary: [
          { icon: "&#xE721;", label: "搜索", action: function () { nav.go("search"); } },
          { icon: "&#xE72C;", label: "刷新", action: function () { state.pivot === 1 ? loadConversations() : loadFavorites(); } }
        ],
        secondary: secondary
      };
    }

    return {
      show: function (params) {
        if (!built) build();
        var target = params && params.pivot != null ? params.pivot : state.pivot;
        state.pivot = -1;
        select(target, null);
      },
      resume: function () {
        renderHeaders();
        if (state.pivot === 1) loadConversations();
        if (state.pivot === 2) loadFavorites();
      },
      refreshHeaders: function () { if (built) renderHeaders(); },
      appbar: appbarCfg
    };
  })();

  function about() {
    showDialog({
      title: "关于",
      html: "<p>闲鱼 for Windows 10 Mobile（第三方）<br>版本 2.0.0 原型</p>" +
        "<p>本应用通过闲鱼网页版使用的 mtop 接口与 IM 长连接工作，不提供任何付款功能，与阿里巴巴无关。</p>",
      primary: "确定"
    });
  }

  function startChat(itemId) {
    requireSignIn("联系卖家").then(function (ok) {
      if (!ok) return;
      withBusy("正在打开会话...", api.createConversation(itemId)).then(function (cid) {
        nav.go("chat", { cid: cid });
      });
    });
  }

  /* ================= 搜索 ================= */
  pages.search = (function () {
    var el = $('[data-page="search"]');
    var built = false;
    var sorts = [
      { value: "default", label: "综合排序" },
      { value: "newest", label: "最新发布" },
      { value: "price_asc", label: "价格从低到高" },
      { value: "price_desc", label: "价格从高到低" }
    ];

    function build() {
      el.innerHTML =
        '<div class="page-header">搜索</div>' +
        '<div style="padding:0 12px 8px"><div class="asb">' +
        '<input id="q" class="textbox" placeholder="搜索闲置宝贝" autocomplete="off">' +
        '<button class="asb-btn mdl2" id="q-go">&#xE721;</button>' +
        '<div class="asb-list" id="q-suggest"></div></div></div>' +
        '<div id="sort-row" style="padding:0 12px 6px;display:none"><button class="combo" id="sort"><span></span><span class="mdl2">&#xE70D;</span></button></div>' +
        '<div class="scroll" id="search-body"></div>';

      var q = $("#q");
      q.addEventListener("input", suggest);
      q.addEventListener("focus", suggest);
      q.addEventListener("keydown", function (e) { if (e.key === "Enter") submit(q.value); });
      $("#q-go").addEventListener("click", function () { submit(q.value); });
      $("#sort").addEventListener("click", function () {
        showPicker("排序方式", sorts, state.search.sort).then(function (v) {
          if (v) { state.search.sort = v; run(); }
        });
      });
      built = true;
    }

    function suggest() {
      var v = $("#q").value.trim();
      var box = $("#q-suggest");
      if (!v) { box.classList.remove("show"); return; }
      var words = api.hotKeywords.concat(api.searchHistory).filter(function (w, i, a) {
        return a.indexOf(w) === i && w.toLowerCase().indexOf(v.toLowerCase()) >= 0;
      }).slice(0, 6);
      if (!words.length) { box.classList.remove("show"); return; }
      box.innerHTML = words.map(function (w) {
        return '<div data-w="' + esc(w) + '"><span class="mdl2">&#xE721;</span>' + esc(w) + "</div>";
      }).join("");
      box.classList.add("show");
      $$("div", box).forEach(function (d) {
        d.addEventListener("mousedown", function (e) { e.preventDefault(); submit(d.getAttribute("data-w")); });
      });
    }

    function closeSuggestions() {
      var box = $("#q-suggest");
      if (box && box.classList.contains("show")) { box.classList.remove("show"); return true; }
      return false;
    }

    function submit(kw) {
      kw = (kw || "").trim();
      if (!kw) return;
      $("#q").value = kw;
      $("#q").blur();
      closeSuggestions();
      var h = api.searchHistory;
      var i = h.indexOf(kw);
      if (i >= 0) h.splice(i, 1);
      h.unshift(kw);
      if (h.length > 10) h.pop();
      state.search.keyword = kw;
      run();
    }

    function home() {
      $("#sort-row").style.display = "none";
      var body = $("#search-body");
      var h = api.searchHistory;
      body.innerHTML =
        (h.length ? '<div class="section-label">最近搜索</div>' + h.map(function (w) {
          return '<div class="conv tilt" data-w="' + esc(w) + '" style="padding:10px 12px"><span class="mdl2" style="font-size:16px;color:var(--base-medium);padding-top:2px">&#xE81C;</span><div style="font-size:17px">' + esc(w) + "</div></div>";
        }).join("") : "") +
        '<div class="section-label">大家都在搜</div><div style="display:flex;flex-wrap:wrap;gap:8px;padding:4px 12px">' +
        api.hotKeywords.map(function (w) { return '<button class="btn" style="min-width:0" data-w="' + esc(w) + '">' + esc(w) + "</button>"; }).join("") +
        "</div>";
      $$("[data-w]", body).forEach(function (n) {
        n.addEventListener("click", function () { submit(n.getAttribute("data-w")); });
      });
    }

    function run() {
      $("#sort-row").style.display = "block";
      $("#sort span").textContent = sorts.filter(function (s) { return s.value === state.search.sort; })[0].label;
      var body = $("#search-body");
      body.innerHTML = "";
      withBusy("正在搜索“" + state.search.keyword + "”...", api.search(state.search.keyword, state.search.sort, 1)).then(function (r) {
        if (!r.items.length) { body.innerHTML = '<div class="empty"><span class="mdl2">&#xE721;</span>没有找到相关宝贝</div>'; return; }
        body.innerHTML = '<div class="hint" style="padding:4px 12px 0">找到 ' + r.total + " 件宝贝</div>" +
          r.items.map(function (it) {
            return rowHtml(it, { tags: (it.freeShip ? '<span class="tag">包邮</span>' : "") + '<span class="tag">' + esc(it.condition) + "</span>" });
          }).join("") + '<div class="list-footer">已经到底了</div>';
        $$(".row", body).forEach(function (n) {
          n.addEventListener("click", function () { nav.go("detail", { itemId: n.getAttribute("data-id") }); });
        });
      });
    }

    return {
      show: function (params) {
        if (!built) build();
        if (params && params.keyword) { submit(params.keyword); return; }
        $("#q").value = state.search.keyword;
        if (state.search.keyword) run(); else home();
        setTimeout(function () { if (!state.search.keyword) $("#q").focus(); }, 350);
      },
      closeSuggestions: closeSuggestions,
      appbar: function () {
        return {
          primary: [],
          secondary: [{ label: "清除搜索历史", action: function () { api.searchHistory.length = 0; if (!state.search.keyword) home(); } }]
        };
      }
    };
  })();

  /* ================= 商品详情 ================= */
  pages.detail = (function () {
    var el = $('[data-page="detail"]');
    var item = null, fav = false, index = 0;

    function render() {
      var it = item;
      el.innerHTML =
        '<div class="scroll">' +
        '<div class="flip"><div class="flip-track">' +
        it.images.map(function (src) { return '<div class="flip-slide" style="background-image:url(\'' + src + '\')"></div>'; }).join("") +
        '</div><button class="flip-nav prev mdl2">&#xE76B;</button><button class="flip-nav next mdl2">&#xE76C;</button>' +
        '<div class="flip-count"></div></div>' +
        '<div class="detail-pad">' +
        '<div class="detail-price"><span class="now">¥' + money(it.price) + "</span>" +
        (it.originalPrice ? "<s>原价 ¥" + money(it.originalPrice) + "</s>" : "") +
        '<span class="ship">' + (it.freeShip ? "包邮" : "运费另计") + "</span></div>" +
        '<div class="detail-title">' + esc(it.title) + "</div>" +
        '<div class="detail-stats">' + it.want + " 人想要 · " + it.browse + " 次浏览 · " + it.fav + " 人收藏</div>" +
        '<div class="divider"></div>' +
        '<div class="seller tilt" id="seller">' +
        '<div class="avatar" style="background:' + it.seller.avatarColor + '">' + initial(it.seller.nick) + "</div>" +
        '<div><div class="seller-name">' + esc(it.seller.nick) + "</div>" +
        '<div class="seller-sub">卖家信用' + esc(it.seller.credit) + " · 已卖出 " + it.seller.sold + " 件</div>" +
        '<div class="seller-sub">' + esc(it.seller.lastSeen) + " · 24 小时回复率 " + esc(it.seller.reply) + "</div></div>" +
        '<span class="mdl2 chev">&#xE76C;</span></div>' +
        '<div class="divider"></div>' +
        '<div class="kv"><span>成色</span><span>' + esc(it.condition) + "</span></div>" +
        '<div class="kv"><span>议价</span><span>' + (it.bargain ? "可小刀" : "不议价") + "</span></div>" +
        '<div class="kv"><span>发货地</span><span>' + esc(it.city) + "</span></div>" +
        '<div class="kv"><span>发布</span><span>' + esc(it.published) + "</span></div>" +
        '<div class="divider"></div>' +
        '<div class="detail-desc">' + esc(it.desc) + "</div>" +
        '<div class="list-footer">闲鱼商品 ID ' + it.itemId + "</div>" +
        "</div></div>";

      index = 0;
      updateFlip();
      $(".flip-nav.prev", el).addEventListener("click", function () { flip(-1); });
      $(".flip-nav.next", el).addEventListener("click", function () { flip(1); });
      var f = $(".flip", el), sx = null;
      f.addEventListener("pointerdown", function (e) { sx = e.clientX; });
      f.addEventListener("pointerup", function (e) {
        if (sx !== null && Math.abs(e.clientX - sx) > 40) flip(e.clientX < sx ? 1 : -1);
        sx = null;
      });
      $("#seller").addEventListener("click", function () {
        nav.go("search", { keyword: it.seller.nick });
      });
    }

    function flip(d) {
      index = Math.max(0, Math.min(item.images.length - 1, index + d));
      updateFlip();
    }
    function updateFlip() {
      $(".flip-track", el).style.transform = "translateX(" + (-100 * index) + "%)";
      $(".flip-count", el).textContent = (index + 1) + " / " + item.images.length;
      $(".flip-nav.prev", el).style.display = index > 0 ? "" : "none";
      $(".flip-nav.next", el).style.display = index < item.images.length - 1 ? "" : "none";
    }

    function toggleFav() {
      requireSignIn("收藏宝贝").then(function (ok) {
        if (!ok) return;
        var next = !fav;
        withBusy(next ? "正在收藏..." : "正在取消收藏...", api.setFavorite(item.itemId, next)).then(function () {
          fav = next;
          item.fav += next ? 1 : -1;
          appbar.set(cfg());
          $(".detail-stats", el).textContent = item.want + " 人想要 · " + item.browse + " 次浏览 · " + item.fav + " 人收藏";
        });
      });
    }

    function share() {
      showPicker("共享“" + item.title.slice(0, 12) + "…”", [
        { value: "messaging", label: "短信" },
        { value: "mail", label: "邮件" },
        { value: "onenote", label: "OneNote" },
        { value: "copy", label: "复制链接" }
      ], null).then(function (v) {
        if (v) toast("共享", v === "copy" ? "链接已复制到剪贴板" : "已通过系统共享发送");
      });
    }

    function cfg() {
      if (!item) return { primary: [], secondary: [] };
      return {
        primary: [
          { icon: fav ? "&#xE735;" : "&#xE734;", label: fav ? "取消收藏" : "收藏", action: toggleFav },
          { icon: "&#xE8BD;", label: "聊一聊", action: function () { startChat(item.itemId); } },
          { icon: "&#xE72D;", label: "共享", action: share }
        ],
        secondary: [
          { label: "在浏览器中打开", action: function () { toast("浏览器", "goofish.com/item?id=" + item.itemId); } },
          { label: "复制链接", action: function () { toast("闲鱼", "链接已复制到剪贴板"); } }
        ]
      };
    }

    return {
      show: function (params) {
        item = null;
        el.innerHTML = '<div class="empty" style="padding-top:120px">正在加载宝贝...</div>';
        var id = params.itemId || "735020881256";
        withBusy("正在加载宝贝...", api.getItemDetail(id)).then(function (it) {
          item = it;
          fav = api.isFavorite(it.itemId);
          render();
          if (nav.current() === "detail") appbar.set(cfg());
        });
      },
      appbar: cfg
    };
  })();

  /* ================= 私信 ================= */
  pages.chat = (function () {
    var el = $('[data-page="chat"]');
    var conv = null;

    function bubble(m) {
      return '<div class="bubble ' + (m.from === "me" ? "out" : "in") + (m.sending ? " sending" : "") + '">' + esc(m.text) +
        '<span class="time">' + esc(m.sending ? "正在发送..." : m.time) + "</span></div>";
    }

    function render() {
      el.innerHTML =
        '<div class="chat-head"><div class="avatar" style="background:' + conv.peer.avatarColor + '">' + initial(conv.peer.nick) + '</div><div class="name">' + esc(conv.peer.nick) + "</div></div>" +
        '<div class="chat-item tilt" id="chat-item"><div class="thumb" style="background-image:url(\'' + conv.item.cover + '\')"></div>' +
        '<div><div class="t">' + esc(conv.item.title) + '</div><div class="p">¥' + money(conv.item.price) + '</div></div><span class="mdl2">&#xE76C;</span></div>' +
        '<div class="scroll" id="chat-scroll"><div class="chat-list" id="chat-list">' +
        (conv.messages.length ? "" : '<div class="chat-sys">你正在咨询这件宝贝，交易请认准闲鱼官方流程</div>') +
        conv.messages.map(bubble).join("") +
        "</div></div>" +
        '<div class="chat-input"><textarea id="chat-text" class="textbox" rows="1" placeholder="输入消息"></textarea></div>';

      $("#chat-item").addEventListener("click", function () { nav.go("detail", { itemId: conv.item.itemId }); });
      var ta = $("#chat-text");
      ta.addEventListener("input", function () {
        ta.style.height = "auto";
        ta.style.height = Math.min(96, ta.scrollHeight + 4) + "px";
        var send = $("#btn-send");
        if (send) send.disabled = !ta.value.trim();
      });
      ta.addEventListener("keydown", function (e) {
        if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); send(); }
      });
      scrollEnd();
    }

    function scrollEnd() {
      var s = $("#chat-scroll");
      if (s) s.scrollTop = s.scrollHeight;
    }

    function send(text) {
      var ta = $("#chat-text");
      text = (text || ta.value).trim();
      if (!text) return;
      ta.value = "";
      ta.style.height = "auto";
      appbar.set(cfg());
      var list = $("#chat-list");
      var sys = $(".chat-sys", list);
      if (sys) sys.remove();
      list.insertAdjacentHTML("beforeend", bubble({ from: "me", text: text, sending: true }));
      var pending = list.lastElementChild;
      scrollEnd();
      var cid = conv.cid;
      api.sendMessage(cid, text, onReply).then(function (m) {
        pending.outerHTML = bubble(m);
        if (state.chatCid === cid && nav.current() === "chat") {
          setTimeout(function () {
            if (state.chatCid !== cid || nav.current() !== "chat") return;
            list.insertAdjacentHTML("beforeend", '<div class="typing" id="typing">' + esc(conv.peer.nick) + " 正在输入...</div>");
            scrollEnd();
          }, 900);
        }
      });
    }

    // 模拟 /s/sync 推送：在当前会话内直接追加，否则弹出系统 Toast
    function onReply(c, m) {
      if (state.chatCid === c.cid && nav.current() === "chat") {
        var t = $("#typing");
        if (t) t.remove();
        $("#chat-list").insertAdjacentHTML("beforeend", bubble(m));
        scrollEnd();
      } else {
        api.markUnread(c.cid);
        pages.main.refreshHeaders();
        if (state.notify) toast(c.peer.nick, m.text, function () { nav.go("chat", { cid: c.cid }); });
      }
    }

    function quick(ev) {
      showMenu(ev, ["还在吗？", "可以小刀吗？", "包邮吗？", "成色怎么样，有没有划痕？", "可以走验货宝吗？"].map(function (t) {
        return { label: t, action: function () { send(t); } };
      }));
    }

    function cfg() {
      var ta = $("#chat-text");
      return {
        primary: [
          { icon: "&#xE8F2;", label: "常用语", action: quick },
          { id: "btn-send", icon: "&#xE724;", label: "发送", disabled: !(ta && ta.value.trim()), action: function () { send(); } }
        ],
        secondary: [
          { label: "查看宝贝", action: function () { if (conv) nav.go("detail", { itemId: conv.item.itemId }); } },
          { label: "在浏览器中查看会话", action: function () { toast("浏览器", "goofish.com/im"); } }
        ]
      };
    }

    return {
      show: function (params) {
        conv = null;
        el.innerHTML = '<div class="empty" style="padding-top:120px">正在连接...</div>';
        var cid = params.cid;
        if (!cid) { api.getConversations().then(function (l) { pages.chat.show({ cid: l[0].cid }); }); return; }
        state.chatCid = cid;
        withBusy("正在连接消息服务...", api.openConversation(cid)).then(function (c) {
          conv = c;
          render();
          pages.main.refreshHeaders();
          if (nav.current() === "chat") appbar.set(cfg());
        });
      },
      resume: function () { if (conv) state.chatCid = conv.cid; },
      appbar: cfg
    };
  })();

  // 离开私信页后不再把推送视为“正在查看”
  var _back = nav.back, _go = nav.go;
  nav.back = function () {
    var r = _back();
    if (nav.current() !== "chat") state.chatCid = null;
    return r;
  };
  nav.go = function (name, params, opts) {
    if (name !== "chat") state.chatCid = null;
    _go(name, params, opts);
  };

  /* ================= 登录（WebView） ================= */
  pages.login = (function () {
    var el = $('[data-page="login"]');

    function render() {
      el.innerHTML =
        '<div class="page-header">账户</div>' +
        '<div class="scroll">' +
        '<div class="page-title">登录闲鱼</div>' +
        '<p class="hint" style="padding:0 12px">下方是闲鱼官方登录页（WebView）。登录完成后应用会读取登录 Cookie，用来浏览推荐、收藏和聊天。</p>' +
        '<div class="webview">' +
        '<div class="webview-bar"><span class="mdl2">&#xE72E;</span>passport.goofish.com</div>' +
        '<div class="webview-body"><div class="logo"><b>闲</b>鱼</div>' +
        '<div class="webview-tabs"><span class="on" data-t="sms">短信登录</span><span data-t="qr">扫码登录</span></div>' +
        '<div id="wv-sms"><input placeholder="请输入手机号" value="138****6688"><input placeholder="请输入验证码" value="246810"><button class="go" id="wv-go">登录</button></div>' +
        '<div id="wv-qr" style="display:none;text-align:center;color:#666"><div class="qr"></div>用另一台手机的闲鱼 App 扫码</div>' +
        "</div></div>" +
        '<div style="padding:16px 12px">' +
        '<p><a class="link" id="cookie-login">用 Cookie 登录（高级）</a></p>' +
        '<p><a class="link" id="skip-login">暂不登录，先逛逛</a></p>' +
        "</div></div>";

      $$(".webview-tabs span", el).forEach(function (t) {
        t.addEventListener("click", function () {
          $$(".webview-tabs span", el).forEach(function (x) { x.classList.toggle("on", x === t); });
          $("#wv-sms").style.display = t.getAttribute("data-t") === "sms" ? "" : "none";
          $("#wv-qr").style.display = t.getAttribute("data-t") === "qr" ? "" : "none";
        });
      });
      $("#wv-go").addEventListener("click", finish);
      $("#cookie-login").addEventListener("click", function () {
        showDialog({
          title: "用 Cookie 登录",
          html: "<p>在电脑浏览器登录 goofish.com，复制请求中的 Cookie 粘贴到这里。至少需要 unb、cookie2、_m_h5_tk。</p>" +
            '<textarea class="textbox" rows="4" id="cookie-text" placeholder="unb=...; cookie2=...; _m_h5_tk=..."></textarea>',
          primary: "登录",
          secondary: "取消"
        }).then(function (r) { if (r === "primary") finish(); });
      });
      $("#skip-login").addEventListener("click", function () { nav.back(); });
    }

    function finish() {
      withBusy("正在验证登录状态...", new Promise(function (r) { setTimeout(r, 900); })).then(function () {
        state.signedIn = true;
        nav.back();
        toast("闲鱼", "登录成功，欢迎回来");
      });
    }

    return { show: render, appbar: function () { return null; } };
  })();

  /* ================= 设置 ================= */
  pages.settings = (function () {
    var el = $('[data-page="settings"]');

    function toggle(id, label, on) {
      return '<label class="toggle"><input type="checkbox" id="' + id + '"' + (on ? " checked" : "") + '><span class="track"></span><span class="state">' + (on ? "开" : "关") + "</span></label>" +
        (label ? '<p style="margin-top:6px">' + label + "</p>" : "");
    }

    function render() {
      el.innerHTML =
        '<div class="page-header">设置</div>' +
        '<div class="scroll"><div class="settings">' +
        "<h3>账户</h3>" +
        (state.signedIn
          ? '<div class="account"><div class="avatar" style="background:var(--accent);color:var(--accent-fg)">鱼</div><div><div class="account-name">闲鱼用户_7788</div><div class="account-sub">已通过网页登录 · Cookie 有效</div></div></div><button class="btn" id="sign-out">注销</button>'
          : '<p>未登录。登录后可以收藏宝贝、同步收藏夹并和卖家聊天。</p><button class="btn accent" id="sign-in">登录</button>') +

        "<h3>主题</h3>" +
        ["dark:深色", "light:浅色", "system:使用 Windows 设置"].map(function (p) {
          var v = p.split(":")[0];
          return '<label class="radio"><input type="radio" name="theme" value="' + v + '"' + ((state.themeChoice || "system") === v ? " checked" : "") + '><span class="ring"></span>' + p.split(":")[1] + "</label>";
        }).join("") +
        "<p>强调色跟随 Windows 设置 → 个性化 → 颜色。</p>" +

        "<h3>演示数据</h3>" +
        toggle("t-demo", "不登录也能浏览示例宝贝。登录后自动使用真实数据。", state.demoMode) +

        "<h3>省流量</h3>" +
        toggle("t-saver", "使用移动数据时加载缩略图而不是原图。", state.dataSaver) +

        "<h3>通知</h3>" +
        toggle("t-notify", "收到新私信时显示横幅通知。", state.notify) +

        "<h3>搜索历史</h3>" +
        '<button class="btn" id="clear-history">清除搜索历史</button>' +

        "<h3>关于</h3>" +
        "<p>闲鱼 for Windows 10 Mobile（第三方客户端）<br>版本 2.0.0.0</p>" +
        "<p>不支持付款与下单。数据来自闲鱼网页版接口，接口可能随时变化。本应用与阿里巴巴及闲鱼无关。</p>" +
        "</div></div>";

      var so = $("#sign-out"), si = $("#sign-in");
      if (so) so.addEventListener("click", function () {
        showDialog({ title: "注销闲鱼账号？", html: "<p>将清除本机保存的登录 Cookie。收藏和聊天记录保存在闲鱼服务器上，不会丢失。</p>", primary: "注销", secondary: "取消" })
          .then(function (r) { if (r === "primary") { state.signedIn = false; render(); } });
      });
      if (si) si.addEventListener("click", function () { nav.go("login"); });
      $$('input[name="theme"]', el).forEach(function (r) {
        r.addEventListener("change", function () {
          state.themeChoice = r.value;
          applyTheme(r.value === "system" ? systemTheme : r.value);
        });
      });
      bindToggle("t-demo", "demoMode");
      bindToggle("t-saver", "dataSaver");
      bindToggle("t-notify", "notify");
      $("#clear-history").addEventListener("click", function () {
        api.searchHistory.length = 0;
        toast("闲鱼", "搜索历史已清除");
      });
    }

    function bindToggle(id, key) {
      var input = $("#" + id);
      input.addEventListener("change", function () {
        state[key] = input.checked;
        input.parentNode.querySelector(".state").textContent = input.checked ? "开" : "关";
      });
    }

    return { show: render, resume: render, appbar: function () { return null; } };
  })();

  /* ================= 主题与强调色 ================= */
  var systemTheme = "dark";

  function applyTheme(t) {
    screen.classList.toggle("theme-dark", t === "dark");
    screen.classList.toggle("theme-light", t === "light");
  }

  function applyAccent(hex) {
    state.accent = hex;
    var r = parseInt(hex.substr(1, 2), 16), g = parseInt(hex.substr(3, 2), 16), b = parseInt(hex.substr(5, 2), 16);
    var lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
    document.documentElement.style.setProperty("--accent", hex);
    document.documentElement.style.setProperty("--accent-fg", lum > 0.62 ? "#000000" : "#FFFFFF");
    $$(".swatch").forEach(function (s) { s.classList.toggle("selected", s.getAttribute("data-c") === hex); });
  }

  // Windows Phone / Windows 10 Mobile 经典强调色
  var accents = ["#0078D7", "#A4C400", "#60A917", "#008A00", "#00ABA9", "#1BA1E2", "#0050EF", "#6A00FF", "#AA00FF", "#F472D0",
                 "#D80073", "#A20025", "#E51400", "#FA6800", "#F0A30A", "#E3C800", "#825A2C", "#6D8764", "#647687", "#87794E"];
  $("#review-accent").innerHTML = accents.map(function (c) {
    return '<div class="swatch" title="' + c + '" data-c="' + c + '" style="background:' + c + '"></div>';
  }).join("");
  $$(".swatch").forEach(function (s) {
    s.addEventListener("click", function () { applyAccent(s.getAttribute("data-c")); });
  });
  $$("#review-theme button").forEach(function (b) {
    b.addEventListener("click", function () {
      systemTheme = b.getAttribute("data-theme");
      $$("#review-theme button").forEach(function (x) { x.classList.toggle("on", x === b); });
      if (!state.themeChoice || state.themeChoice === "system") applyTheme(systemTheme);
    });
  });
  $("#review-frame").addEventListener("change", function (e) {
    document.body.classList.toggle("frameless", !e.target.checked);
  });
  $$("[data-jump]").forEach(function (b) {
    b.addEventListener("click", function () {
      var parts = b.getAttribute("data-jump").split(":");
      var name = parts[0];
      if (name === "main") { nav.reset("main", { pivot: +parts[1] }); return; }
      if (stack.length === 0 || stack[0].name !== "main") nav.reset("main");
      if (name === "detail") nav.go("detail", { itemId: "735020881256" });
      else if (name === "chat") nav.go("chat", { cid: "48820017331" });
      else nav.go(name);
    });
  });

  applyAccent(state.accent);
  applyTheme("dark");
  nav.go("main", { pivot: 0 });
})();
