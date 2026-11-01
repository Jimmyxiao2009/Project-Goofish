/**
 * Windows 10 Mobile 闲鱼客户端前端交互与 Code-Behind 逻辑
 * MVVM 架构设计：NavigationManager, ViewModels, UI DataBinding
 */

document.addEventListener("DOMContentLoaded", () => {
  // =========================================================================
  // 1. 系统时钟与状态栏更新
  // =========================================================================
  function updateStatusBarClock() {
    const clockEl = document.getElementById("status-clock");
    if (!clockEl) return;
    const now = new Date();
    const hours = String(now.getHours()).padStart(2, "0");
    const minutes = String(now.getMinutes()).padStart(2, "0");
    clockEl.textContent = `${hours}:${minutes}`;
  }
  updateStatusBarClock();
  setInterval(updateStatusBarClock, 10000);

  // =========================================================================
  // 2. 导航管理器 (Windows Page Navigation Stack)
  // =========================================================================
  class NavigationManager {
    constructor() {
      this.stack = ["page-home"];
      this.currentPage = "page-home";
    }

    navigateTo(pageId, param = null, isBack = false) {
      if (this.currentPage === pageId && !param) return;

      const currentEl = document.getElementById(this.currentPage);
      const nextEl = document.getElementById(pageId);
      if (!nextEl) return;

      if (currentEl) {
        currentEl.classList.remove("active", "page-enter-forward", "page-enter-backward");
      }

      nextEl.classList.add("active");
      nextEl.classList.add(isBack ? "page-enter-backward" : "page-enter-forward");

      if (!isBack) {
        this.stack.push(pageId);
      }
      this.currentPage = pageId;

      // 触发页面专属的 Activated 生命周期钩子
      if (window.pageControllers[pageId]?.onActivated) {
        window.pageControllers[pageId].onActivated(param);
      }

      this.updateHeaderAndAppbarState(pageId);
    }

    goBack() {
      if (this.stack.length > 1) {
        this.stack.pop();
        const prevPage = this.stack[this.stack.length - 1];
        this.navigateTo(prevPage, null, true);
      }
    }

    updateHeaderAndAppbarState(pageId) {
      const backBtn = document.getElementById("header-back-btn");
      const titleEl = document.getElementById("header-title");
      const commandBar = document.getElementById("w10m-commandbar");

      // 顶部返回键可见性
      if (this.stack.length > 1 && pageId !== "page-home") {
        backBtn.style.display = "flex";
      } else {
        backBtn.style.display = "none";
      }

      // 顶部标题设置
      const titleMap = {
        "page-home": "闲鱼",
        "page-search": "搜索发现",
        "page-detail": "宝贝详情",
        "page-chat-list": "消息中心",
        "page-chat-dialog": "联系卖家",
        "page-favorites": "我的收藏"
      };
      titleEl.textContent = titleMap[pageId] || "闲鱼";

      // 底部 CommandBar 活动图标高亮
      document.querySelectorAll(".w10m-appbar-btn").forEach(btn => {
        btn.classList.remove("active");
        if (btn.getAttribute("data-page") === pageId) {
          btn.classList.add("active");
        }
      });

      // 聊天会话页隐藏通用 CommandBar，启用专属输入栏
      if (pageId === "page-chat-dialog") {
        commandBar.style.display = "none";
      } else {
        commandBar.style.display = "flex";
      }
    }
  }

  window.navManager = new NavigationManager();

  // =========================================================================
  // 3. 页面控制器集合 (Page Controllers / ViewModels)
  // =========================================================================
  window.pageControllers = {
    // -----------------------------------------------------------------------
    // A. 首页控制器 (MainPage / Feed)
    // -----------------------------------------------------------------------
    "page-home": {
      currentCategory: "all",

      init() {
        // Pivot 分页切换监听
        document.querySelectorAll("#home-pivot .w10m-pivot-item").forEach(item => {
          item.addEventListener("click", (e) => {
            document.querySelectorAll("#home-pivot .w10m-pivot-item").forEach(p => p.classList.remove("active"));
            item.classList.add("active");
            this.currentCategory = item.getAttribute("data-cat");
            this.loadFeed();
          });
        });

        this.loadFeed();
      },

      onActivated() {
        this.loadFeed();
      },

      async loadFeed() {
        const grid = document.getElementById("home-grid");
        grid.innerHTML = `<div style="grid-column: span 2; text-align: center; padding: 40px; color: var(--text-tertiary); font-size: 13px;">正在同步闲置好物...</div>`;

        const resp = await window.goofishApi.getFeed(1, 20, this.currentCategory);
        grid.innerHTML = "";

        resp.data.cardList.forEach(item => {
          const card = document.createElement("div");
          card.className = "w10m-tile-card";
          card.innerHTML = `
            <div class="w10m-tile-thumb-wrap">
              <img class="w10m-tile-thumb" src="${item.images[0]}" alt="${item.title}" loading="lazy" />
              <div class="w10m-tile-tag">${item.city.split(" · ")[0]}</div>
            </div>
            <div class="w10m-tile-content">
              <div class="w10m-tile-title">${item.title}</div>
              <div class="w10m-tile-price-row">
                <div class="w10m-tile-price"><span class="currency">¥</span>${item.price.toFixed(2)}</div>
                <div class="w10m-tile-original-price">¥${item.originalPrice.toFixed(0)}</div>
              </div>
              <div class="w10m-tile-meta-row">
                <div class="w10m-tile-seller">
                  <img class="w10m-avatar-small" src="${item.seller.avatar}" />
                  <span>${item.seller.nick}</span>
                </div>
                <div class="w10m-tile-city">${item.wantCnt}人想要</div>
              </div>
            </div>
          `;
          card.addEventListener("click", () => {
            window.navManager.navigateTo("page-detail", item.id);
          });
          grid.appendChild(card);
        });
      }
    },

    // -----------------------------------------------------------------------
    // B. 商品详情页控制器 (ItemDetailPage)
    // -----------------------------------------------------------------------
    "page-detail": {
      currentItem: null,

      async onActivated(itemId) {
        if (!itemId) return;
        const container = document.getElementById("detail-content");
        container.innerHTML = `<div style="text-align: center; padding: 60px; color: var(--text-tertiary);">加载中...</div>`;

        const resp = await window.goofishApi.getItemDetail(itemId);
        this.currentItem = resp.data;
        this.render();
      },

      render() {
        const item = this.currentItem;
        const container = document.getElementById("detail-content");
        container.innerHTML = `
          <div class="w10m-detail-container">
            <div class="w10m-gallery-slider">
              <img class="w10m-gallery-image" src="${item.images[0]}" alt="${item.title}" />
              <div class="w10m-gallery-badge">1 / ${item.images.length}</div>
            </div>

            <div class="w10m-detail-info">
              <div class="w10m-detail-price-row">
                <div class="w10m-detail-price">¥${item.price.toFixed(2)}</div>
                <div class="w10m-detail-stats">
                  <span>${item.browseCnt} 次浏览</span>
                  <span>${item.wantCnt} 人想要</span>
                </div>
              </div>
              <div class="w10m-detail-title">${item.title}</div>
              <div class="w10m-detail-desc">${item.desc}</div>
              <div style="font-size: 11px; color: var(--text-tertiary); display:flex; gap: 16px;">
                <span>运费：${item.transportFee}</span>
                <span>地点：${item.city}</span>
              </div>
            </div>

            <div class="w10m-seller-card">
              <div class="w10m-seller-left">
                <img class="w10m-seller-avatar" src="${item.seller.avatar}" />
                <div>
                  <div class="w10m-seller-nick">${item.seller.nick}</div>
                  <div class="w10m-seller-tags">
                    <span class="w10m-credit-tag">${item.seller.creditScore}</span>
                    <span style="font-size: 10px; color: var(--text-secondary);">${item.seller.rating}</span>
                  </div>
                </div>
              </div>
              <div style="font-size: 11px; color: var(--text-secondary); text-align: right;">
                <div>在售 ${item.seller.itemCount}</div>
                <div>已售 ${item.seller.soldCount}</div>
              </div>
            </div>

            <div style="display: flex; gap: 10px; padding: 16px;">
              <button id="btn-fav-toggle" class="w10m-btn" style="flex: 1; display: flex; align-items: center; justify-content: center; gap: 6px;">
                <span class="mdl2">${item.isFav ? '&#xE006;' : '&#xEB51;'}</span>
                <span>${item.isFav ? '已收藏' : '收藏'}</span>
              </button>
              <button id="btn-chat-seller" class="w10m-btn primary" style="flex: 2; display: flex; align-items: center; justify-content: center; gap: 6px;">
                <span class="mdl2">&#xE8BD;</span>
                <span>聊一聊 / 联系卖家</span>
              </button>
            </div>
          </div>
        `;

        // 绑定收藏按钮
        const favBtn = document.getElementById("btn-fav-toggle");
        favBtn.addEventListener("click", async () => {
          const isFav = await window.goofishApi.toggleFavorite(item.id);
          favBtn.querySelector(".mdl2").innerHTML = isFav ? "&#xE006;" : "&#xEB51;";
          favBtn.querySelector("span:last-child").textContent = isFav ? "已收藏" : "收藏";
        });

        // 绑定联系卖家按钮
        const chatBtn = document.getElementById("btn-chat-seller");
        chatBtn.addEventListener("click", async () => {
          const conv = await window.goofishApi.getOrCreateConversationByItem(item);
          window.navManager.navigateTo("page-chat-dialog", conv.id);
        });
      }
    },

    // -----------------------------------------------------------------------
    // C. 搜索页控制器 (SearchPage)
    // -----------------------------------------------------------------------
    "page-search": {
      sortType: "default",

      init() {
        const input = document.getElementById("search-input");
        input.addEventListener("keyup", (e) => {
          if (e.key === "Enter") {
            this.doSearch(input.value);
          }
        });

        document.querySelectorAll("#search-filter-chips .w10m-chip").forEach(chip => {
          chip.addEventListener("click", () => {
            document.querySelectorAll("#search-filter-chips .w10m-chip").forEach(c => c.classList.remove("active"));
            chip.classList.add("active");
            this.sortType = chip.getAttribute("data-sort");
            this.doSearch(input.value);
          });
        });
      },

      onActivated() {
        const input = document.getElementById("search-input");
        if (input.value) {
          this.doSearch(input.value);
        }
      },

      async doSearch(kw) {
        const grid = document.getElementById("search-results-grid");
        if (!kw || !kw.trim()) {
          grid.innerHTML = `<div style="grid-column: span 2; text-align: center; padding: 40px; color: var(--text-tertiary);">输入关键词并按回车搜索</div>`;
          return;
        }

        grid.innerHTML = `<div style="grid-column: span 2; text-align: center; padding: 40px; color: var(--text-tertiary);">全网搜索中...</div>`;
        const resp = await window.goofishApi.search(kw, 1, this.sortType);

        grid.innerHTML = "";
        if (resp.data.resultList.length === 0) {
          grid.innerHTML = `<div style="grid-column: span 2; text-align: center; padding: 40px; color: var(--text-tertiary);">没有找到相关闲置好物</div>`;
          return;
        }

        resp.data.resultList.forEach(item => {
          const card = document.createElement("div");
          card.className = "w10m-tile-card";
          card.innerHTML = `
            <div class="w10m-tile-thumb-wrap">
              <img class="w10m-tile-thumb" src="${item.images[0]}" alt="${item.title}" />
              <div class="w10m-tile-tag">${item.city.split(" · ")[0]}</div>
            </div>
            <div class="w10m-tile-content">
              <div class="w10m-tile-title">${item.title}</div>
              <div class="w10m-tile-price-row">
                <div class="w10m-tile-price"><span class="currency">¥</span>${item.price.toFixed(2)}</div>
              </div>
              <div class="w10m-tile-meta-row">
                <div class="w10m-tile-seller">${item.seller.nick}</div>
                <div class="w10m-tile-city">${item.wantCnt}人想要</div>
              </div>
            </div>
          `;
          card.addEventListener("click", () => {
            window.navManager.navigateTo("page-detail", item.id);
          });
          grid.appendChild(card);
        });
      }
    },

    // -----------------------------------------------------------------------
    // D. 消息列表控制器 (ChatListPage)
    // -----------------------------------------------------------------------
    "page-chat-list": {
      async onActivated() {
        const container = document.getElementById("chat-conversations-list");
        container.innerHTML = `<div style="text-align: center; padding: 40px; color: var(--text-tertiary);">拉取会话中...</div>`;

        const convs = await window.goofishApi.getConversations();
        container.innerHTML = "";

        if (convs.length === 0) {
          container.innerHTML = `<div style="text-align: center; padding: 40px; color: var(--text-tertiary);">暂无私信记录</div>`;
          return;
        }

        convs.forEach(c => {
          const row = document.createElement("div");
          row.className = "w10m-list-row";
          row.innerHTML = `
            <img class="w10m-list-thumb" style="border-radius: 50%;" src="${c.sellerAvatar}" />
            <div class="w10m-list-info">
              <div class="w10m-list-title" style="display:flex; justify-content:space-between;">
                <span>${c.sellerNick}</span>
                <span style="font-size: 10px; color: var(--text-tertiary); font-weight: normal;">${c.lastTime}</span>
              </div>
              <div class="w10m-list-sub">
                <span style="white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 80%;">${c.lastMessage}</span>
                ${c.unreadCount > 0 ? `<span class="w10m-accent-badge" style="border-radius: 8px;">${c.unreadCount}</span>` : ''}
              </div>
            </div>
          `;
          row.addEventListener("click", () => {
            window.navManager.navigateTo("page-chat-dialog", c.id);
          });
          container.appendChild(row);
        });
      }
    },

    // -----------------------------------------------------------------------
    // E. 专属聊天对话控制器 (ChatDialoguePage)
    // -----------------------------------------------------------------------
    "page-chat-dialog": {
      activeConvId: null,

      init() {
        const sendBtn = document.getElementById("chat-send-btn");
        const input = document.getElementById("chat-msg-input");

        const send = async () => {
          const text = input.value.trim();
          if (!text || !this.activeConvId) return;
          input.value = "";
          await window.goofishApi.sendMessage(this.activeConvId, text);
          this.renderMessages();
        };

        sendBtn.addEventListener("click", send);
        input.addEventListener("keyup", (e) => {
          if (e.key === "Enter") send();
        });
      },

      async onActivated(convId) {
        this.activeConvId = convId;
        const conv = await window.goofishApi.getConversationById(convId);
        if (!conv) return;

        // 渲染商品预览条
        const banner = document.getElementById("chat-item-banner");
        banner.innerHTML = `
          <img class="w10m-chat-item-thumb" src="${c_thumb(conv.itemThumb)}" />
          <div class="w10m-chat-item-info">
            <div class="w10m-chat-item-title">${conv.itemTitle}</div>
            <div class="w10m-chat-item-price">¥${conv.itemPrice.toFixed(2)}</div>
          </div>
          <div style="font-size: 11px; color: var(--text-secondary); text-decoration: underline;">查看宝贝</div>
        `;
        banner.onclick = () => {
          window.navManager.navigateTo("page-detail", conv.itemId);
        };

        this.renderMessages();
      },

      async renderMessages() {
        const conv = await window.goofishApi.getConversationById(this.activeConvId);
        if (!conv) return;

        const container = document.getElementById("chat-msg-container");
        container.innerHTML = "";

        conv.messages.forEach(m => {
          const bubble = document.createElement("div");
          bubble.className = `w10m-bubble ${m.sender === 'me' ? 'outgoing' : 'incoming'}`;
          bubble.innerHTML = `
            <div>${m.text}</div>
            <div class="w10m-bubble-time">${m.time}</div>
          `;
          container.appendChild(bubble);
        });

        // 滚动到底部
        container.scrollTop = container.scrollHeight;
      }
    },

    // -----------------------------------------------------------------------
    // F. 收藏夹控制器 (FavoritesPage)
    // -----------------------------------------------------------------------
    "page-favorites": {
      async onActivated() {
        const container = document.getElementById("favorites-list");
        container.innerHTML = `<div style="text-align: center; padding: 40px; color: var(--text-tertiary);">同步收藏宝贝中...</div>`;

        const favs = await window.goofishApi.getFavorites();
        container.innerHTML = "";

        if (favs.length === 0) {
          container.innerHTML = `<div style="text-align: center; padding: 60px; color: var(--text-tertiary);"><span class="mdl2" style="font-size: 32px; display:block; margin-bottom: 12px;">&#xEB51;</span>暂无收藏宝贝，快去发现好物吧</div>`;
          return;
        }

        favs.forEach(item => {
          const row = document.createElement("div");
          row.className = "w10m-list-row";
          row.innerHTML = `
            <img class="w10m-list-thumb" src="${item.images[0]}" />
            <div class="w10m-list-info">
              <div class="w10m-list-title">${item.title}</div>
              <div class="w10m-list-sub">
                <span style="color: var(--accent-color); font-weight: bold; font-size: 13px;">¥${item.price.toFixed(2)}</span>
                <span>${item.city}</span>
              </div>
            </div>
            <button class="w10m-nav-btn" title="取消收藏" style="color: var(--accent-color);">
              <span class="mdl2">&#xE006;</span>
            </button>
          `;
          row.querySelector(".w10m-list-info").addEventListener("click", () => {
            window.navManager.navigateTo("page-detail", item.id);
          });
          row.querySelector("button").addEventListener("click", async (e) => {
            e.stopPropagation();
            await window.goofishApi.toggleFavorite(item.id);
            this.onActivated();
          });
          container.appendChild(row);
        });
      }
    }
  };

  function c_thumb(url) {
    return url || "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?w=200";
  }

  // 监听即时推送新消息
  window.onNewMessageReceived = (convId) => {
    if (window.navManager.currentPage === "page-chat-dialog" && window.pageControllers["page-chat-dialog"].activeConvId === convId) {
      window.pageControllers["page-chat-dialog"].renderMessages();
    }
  };

  // =========================================================================
  // 4. 初始化应用与事件委托绑定
  // =========================================================================
  // 初始化各页面组件
  Object.values(window.pageControllers).forEach(ctrl => {
    if (ctrl.init) ctrl.init();
  });

  // 顶部返回键
  document.getElementById("header-back-btn").addEventListener("click", () => {
    window.navManager.goBack();
  });

  // 底部 CommandBar 导航按钮
  document.querySelectorAll(".w10m-appbar-btn").forEach(btn => {
    btn.addEventListener("click", () => {
      const targetPage = btn.getAttribute("data-page");
      if (targetPage) {
        window.navManager.navigateTo(targetPage);
      }
    });
  });

  // 底部更多菜单 (Ellipsis ...) 展开与折叠
  const moreBtn = document.getElementById("appbar-more-btn");
  const flyoutMenu = document.getElementById("w10m-flyout-menu");
  moreBtn.addEventListener("click", (e) => {
    e.stopPropagation();
    flyoutMenu.classList.toggle("open");
  });

  document.addEventListener("click", (e) => {
    if (!flyoutMenu.contains(e.target) && e.target !== moreBtn) {
      flyoutMenu.classList.remove("open");
    }
  });

  // =========================================================================
  // 5. Windows 10 Mobile 主题与真机外观定制
  // =========================================================================
  // 主题切换（深色/浅色）
  document.getElementById("menu-toggle-theme").addEventListener("click", () => {
    document.body.classList.toggle("light-theme");
    flyoutMenu.classList.remove("open");
  });

  // 全屏视图切换
  document.getElementById("btn-toggle-fullscreen").addEventListener("click", () => {
    document.body.classList.toggle("fullscreen-mode");
  });

  // 强调色选择弹窗
  const colorDialog = document.getElementById("color-dialog");
  document.getElementById("menu-accent-color").addEventListener("click", () => {
    flyoutMenu.classList.remove("open");
    colorDialog.classList.add("open");
  });

  document.querySelectorAll(".color-swatch").forEach(swatch => {
    swatch.addEventListener("click", () => {
      const color = swatch.getAttribute("data-color");
      const contrast = swatch.getAttribute("data-contrast") || "#000";
      document.documentElement.style.setProperty("--accent-color", color);
      document.documentElement.style.setProperty("--accent-contrast", contrast);
      colorDialog.classList.remove("open");
    });
  });

  document.getElementById("color-dialog-close").addEventListener("click", () => {
    colorDialog.classList.remove("open");
  });

  // 关于对话框
  const aboutDialog = document.getElementById("about-dialog");
  document.getElementById("menu-about").addEventListener("click", () => {
    flyoutMenu.classList.remove("open");
    aboutDialog.classList.add("open");
  });
  document.getElementById("about-dialog-close").addEventListener("click", () => {
    aboutDialog.classList.remove("open");
  });

  // 初始进入首页
  window.navManager.navigateTo("page-home");
});
