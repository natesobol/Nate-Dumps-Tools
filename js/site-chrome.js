(() => {
  function getBasePath() {
    const pathSegments = window.location.pathname.split('/').filter(Boolean);
    const repoIndex = pathSegments.indexOf('Nates-Free-Tools');
    return repoIndex !== -1 ? `/${pathSegments.slice(0, repoIndex + 1).join('/')}` : '';
  }

  const basePath = getBasePath();

  const resolvePath = path => {
    const normalized = path.startsWith('/') ? path : `/${path}`;
    const withHtml = /\.html?$/.test(normalized) ? normalized : `${normalized}.html`;
    return `${basePath}${withHtml}`.replace(/\/{2,}/g, '/');
  };

  const hasMenuBuilder = () =>
    typeof window.NDTWebappsMenu?.buildMenu === 'function' &&
    typeof window.NDTWebappsMenu?.populateMenus === 'function';

  function ensureStyles() {
    const stylesheets = ['/css/webapps-unified.css', '/css/styles.css', '/css/webapp-theme.css'];
    const head = document.head;
    const existing = Array.from(head.querySelectorAll('link[rel="stylesheet"]'));

    existing.forEach(link => {
      const href = link.getAttribute('href') || '';
      if (basePath && href.startsWith('/css/')) {
        link.href = `${basePath}${href}`;
      }
    });

    stylesheets.forEach(href => {
      const alreadyPresent = existing.some(link => (link.getAttribute('href') || '').includes(href));
      if (!alreadyPresent) {
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = `${basePath}${href}`;
        head.appendChild(link);
      }
    });
  }

  function ensureMenuScript() {
    return new Promise(resolve => {
      if (hasMenuBuilder()) return resolve();

      const existing = document.querySelector('script[data-webapps-menu]');
      if (existing) {
        existing.addEventListener('load', resolve, { once: true });
        existing.addEventListener('error', resolve, { once: true });
        return;
      }

      const script = document.createElement('script');
      script.defer = true;
      script.src = `${basePath}/public/js/webapps-menu.js`;
      script.setAttribute('data-webapps-menu', 'true');
      script.onload = resolve;
      script.onerror = resolve;
      document.head.appendChild(script);
    });
  }

  function buildHeader() {
    const menuHtml = hasMenuBuilder() ? window.NDTWebappsMenu.buildMenu(basePath) : '';
    const header = document.createElement('header');
    header.className = 'site-header';
    header.innerHTML = `
      <div class="header-top">
        <a class="brand" href="${resolvePath('/index')}">
          <img src="${resolvePath('/public/logo.png')}" alt="Monetize Hub" class="brand-logo" />
          <div class="brand-text">
            <span class="brand-name">Monetize Hub</span>
            <span class="brand-tagline">Launch, host, and grow your webapps</span>
          </div>
        </a>
        <details class="nav-dropdown user-dropdown">
          <summary class="nav-link nav-dropdown-toggle">
            <span class="user-label">Account</span>
            <span aria-hidden="true">▾</span>
          </summary>
          <div class="nav-dropdown-menu">
            <a href="${resolvePath('/login')}" class="dropdown-link">Login</a>
            <a href="${resolvePath('/register')}" class="dropdown-link">Create Account</a>
            <a href="${resolvePath('/admin')}" class="dropdown-link">Admin Dashboard</a>
          </div>
        </details>
      </div>
      <nav class="main-nav">
        <a href="${resolvePath('/index')}" class="nav-link">Home</a>
        <a href="${resolvePath('/about')}" class="nav-link">About</a>
        <details class="nav-dropdown">
          <summary class="nav-link nav-dropdown-toggle">Webapps <span aria-hidden="true">▾</span></summary>
          <div class="nav-dropdown-menu mega-menu" data-webapps-menu>${menuHtml}</div>
        </details>
      </nav>
    `;
    return header;
  }

  function setupHoverDropdowns(scope = document) {
    scope.querySelectorAll('.nav-dropdown').forEach(details => {
      let closeTimer;

      const open = () => {
        clearTimeout(closeTimer);
        details.setAttribute('open', '');
      };

      const scheduleClose = () => {
        clearTimeout(closeTimer);
        closeTimer = setTimeout(() => details.removeAttribute('open'), 120);
      };

      details.addEventListener('mouseenter', open);
      details.addEventListener('mouseleave', scheduleClose);
      details.addEventListener('focusin', open);
      details.addEventListener('focusout', event => {
        if (!details.contains(event.relatedTarget)) {
          scheduleClose();
        }
      });
    });
  }

  async function injectHeader() {
    let header = document.querySelector('.site-header');
    const newHeader = buildHeader();

    if (header) {
      header.replaceWith(newHeader);
      header = newHeader;
    } else {
      document.body.prepend(newHeader);
      header = newHeader;
    }

    if (hasMenuBuilder()) {
      window.NDTWebappsMenu.populateMenus();
    }

    setupHoverDropdowns(header);
  }

  document.addEventListener('DOMContentLoaded', async () => {
    ensureStyles();
    await ensureMenuScript();
    injectHeader();
  });
})();
