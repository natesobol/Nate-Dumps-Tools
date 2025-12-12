(function () {
  function getBasePath() {
    const pathSegments = window.location.pathname.split('/').filter(Boolean);
    const repoIndex = pathSegments.indexOf('Nates-Free-Tools');
    return repoIndex !== -1 ? `/${pathSegments.slice(0, repoIndex + 1).join('/')}` : '';
  }

  const basePath = getBasePath();

  function ensureStyles() {
    const head = document.head;
    const stylesheets = ['/css/webapps-unified.css', '/css/styles.css', '/css/webapp-theme.css'];

    const existingLinks = Array.from(head.querySelectorAll('link[rel="stylesheet"]'));

    existingLinks.forEach(link => {
      const href = link.getAttribute('href') || '';
      if (basePath && href.startsWith('/css/')) {
        link.href = `${basePath}${href}`;
      }
    });

    stylesheets.forEach(href => {
      const alreadyLoaded = existingLinks.some(link => (link.getAttribute('href') || '').includes(href));

      if (!alreadyLoaded) {
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = `${basePath}${href}`;
        head.appendChild(link);
      }
    });
  }

  function buildHeader() {
    const header = document.createElement('header');
    header.className = 'site-header';
    header.innerHTML = `
      <div class="header-top">
        <a class="brand" href="${basePath || '/'}">
          <img src="${basePath}/public/logo.png" alt="Monetize Hub" class="brand-logo" />
          <div class="brand-text">
            <span class="brand-name">Monetize Hub</span>
            <span class="brand-tagline">Launch, host, and grow your webapps</span>
          </div>
        </a>
        <details class="nav-dropdown user-dropdown" open>
          <summary class="nav-link nav-dropdown-toggle">
            <span class="user-label">Account</span>
            <span aria-hidden="true">▾</span>
          </summary>
          <div class="nav-dropdown-menu">
            <a href="${basePath}/login" class="dropdown-link">Login</a>
            <a href="${basePath}/register" class="dropdown-link">Create Account</a>
            <a href="${basePath}/admin" class="dropdown-link">Admin Dashboard</a>
          </div>
        </details>
      </div>
      <nav class="main-nav">
        <a href="${basePath || '/'}" class="nav-link">Home</a>
        <a href="${basePath}/about" class="nav-link">About</a>
        <details class="nav-dropdown" open>
          <summary class="nav-link nav-dropdown-toggle">Webapps <span aria-hidden="true">▾</span></summary>
          <div class="nav-dropdown-menu">
            <a href="${basePath}/excel-to-json" class="dropdown-link">Excel → JSON</a>
            <a href="${basePath}/json-to-excel" class="dropdown-link">JSON → Excel</a>
            <a href="${basePath}/xml-json-translator" class="dropdown-link">XML ⇄ JSON Translator</a>
            <a href="${basePath}/yaml-json-converter" class="dropdown-link">YAML ↔ JSON Converter</a>
            <a href="${basePath}/json-combiner" class="dropdown-link">JSON Combiner</a>
            <a href="${basePath}/find-and-replace" class="dropdown-link">Find &amp; Replace</a>
            <a href="${basePath}/csv-xml-converter" class="dropdown-link">CSV/XML Converter</a>
            <a href="${basePath}/list-comparison" class="dropdown-link">List Comparison / Diff Checker</a>
            <a href="${basePath}/html-metadata-extractor" class="dropdown-link">HTML Metadata Extractor</a>
            <a href="${basePath}/html-tag-cleaner" class="dropdown-link">HTML Tag Cleaner</a>
            <a href="${basePath}/tabify-untabify" class="dropdown-link">Tabify or Untabify</a>
            <a href="${basePath}/extract-text-inside-quotes" class="dropdown-link">Extract Text Inside Quotes</a>
            <a href="${basePath}/pdf-splitter" class="dropdown-link">PDF Splitter</a>
            <a href="${basePath}/bullet-list-extractor" class="dropdown-link">Bullet List Extractor</a>
            <a href="${basePath}/powerpoint-to-pdf" class="dropdown-link">PowerPoint → PDF</a>
            <a href="${basePath}/powerpoint-image-extractor" class="dropdown-link">PowerPoint Image Extractor</a>
          </div>
        </details>
      </nav>
    `;
    return header;
  }

  function injectHeader() {
    if (!document.querySelector('.site-header')) {
      document.body.prepend(buildHeader());
    }
  }

  document.addEventListener('DOMContentLoaded', () => {
    ensureStyles();
    injectHeader();
  });
})();
