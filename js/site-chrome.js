(() => {
  const pathSegments = window.location.pathname.split('/').filter(Boolean);
  const repoIndex = pathSegments.indexOf('Nates-Free-Tools');
  const basePath = repoIndex !== -1 ? `/${pathSegments.slice(0, repoIndex + 1).join('/')}` : '';

  const resolvePath = path => {
    const normalized = path.startsWith('/') ? path : `/${path}`;
    return `${basePath}${normalized}`.replace(/\/{2,}/g, '/');
  };

  const loadScript = (src, datasetName) => {
    const attribute = `data-${datasetName}`;
    const filename = src.split('/').pop();
    const alreadyLoaded =
      document.querySelector(`script[${attribute}]`) ||
      Array.from(document.scripts).some(script => (script.getAttribute('src') || '').includes(filename));

    if (alreadyLoaded) {
      return;
    }

    const script = document.createElement('script');
    script.src = src;
    script.setAttribute(attribute, 'true');
    document.head.appendChild(script);
  };

  const ensureStyles = () => {
    const styles = [
      '/public/css/styles.css',
      '/public/css/webapps-unified.css',
      '/public/css/webapp-theme.css',
    ];

    const head = document.head;
    const existing = Array.from(head.querySelectorAll('link[rel="stylesheet"]'));

    existing.forEach(link => {
      const href = link.getAttribute('href') || '';
      if (basePath && href.startsWith('/public/css/')) {
        link.href = `${basePath}${href}`;
      }
    });

    styles.forEach(href => {
      const alreadyPresent = existing.some(link => (link.getAttribute('href') || '').includes(href));
      if (!alreadyPresent) {
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = `${basePath}${href}`;
        head.appendChild(link);
      }
    });
  };

  loadScript(`${basePath}/public/js/webapps-menu.js`, 'proxied-webapps-menu');

  function buildHeader() {
    const header = document.querySelector('.site-header');
    if (!header) return;

    const navLinks = [
      { key: 'home', href: resolvePath('/index.html'), label: 'Home' },
      { key: 'about', href: resolvePath('/about.html'), label: 'About' },
    ];

    header.innerHTML = `
      <div class="header-top">
        <a class="brand" href="${resolvePath('/index.html')}">
          <img src="${resolvePath('/public/logo.png')}" alt="Nate Dumps Tools" class="brand-logo" />
          <div class="brand-text">
            <span class="brand-name">Nate Dumps Tools</span>
            <span class="brand-tagline">Launch, host, and grow your webapps</span>
          </div>
        </a>
        <details class="nav-dropdown user-dropdown">
          <summary class="nav-link nav-dropdown-toggle">
            <span class="user-label">Account</span>
            <span aria-hidden="true">▾</span>
          </summary>
          <div class="nav-dropdown-menu">
            <a href="${resolvePath('/login.html')}" class="dropdown-link">Login</a>
            <a href="${resolvePath('/register.html')}" class="dropdown-link">Create Account</a>
            <a href="${resolvePath('/admin.html')}" class="dropdown-link">Admin Dashboard</a>
          </div>
        </details>
      </div>
      <nav class="main-nav">
        ${navLinks
          .map(link => `<a class="nav-link" data-nav-key="${link.key}" href="${link.href}">${link.label}</a>`)
          .join('')}
        <details class="nav-dropdown">
          <summary class="nav-link nav-dropdown-toggle">Webapps <span aria-hidden="true">▾</span></summary>
          <div class="nav-dropdown-menu mega-menu" data-webapps-menu></div>
        </details>
      </nav>
    `;

    const pathname = window.location.pathname.replace(/\/index\.html$/, '/');
    navLinks.forEach(({ key, href }) => {
      const link = header.querySelector(`[data-nav-key="${key}"]`);
      if (!link) return;
      const normalizedHref = href.replace(/\/index\.html$/, '/');
      if (pathname === normalizedHref || pathname.endsWith(`${key}.html`)) {
        link.classList.add('active');
      }
    });

    setupHoverDropdowns(header);
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

  document.addEventListener('DOMContentLoaded', () => {
    ensureStyles();
    buildHeader();
    if (window.NDTWebappsMenu?.populateMenus) {
      window.NDTWebappsMenu.populateMenus();
    }
    setupHoverDropdowns();
  });
})();
