(function () {
  function ensureStyles() {
    const head = document.head;
    const stylesheets = ['/css/styles.css', '/css/webapp-theme.css'];

    stylesheets.forEach(href => {
      const alreadyLoaded = Array.from(head.querySelectorAll('link[rel="stylesheet"]')).some(link =>
        (link.getAttribute('href') || '').includes(href)
      );

      if (!alreadyLoaded) {
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = href;
        head.appendChild(link);
      }
    });
  }

  function buildHeader() {
    const header = document.createElement('header');
    header.className = 'site-header';
    header.innerHTML = `
      <div class="header-top">
        <a class="brand" href="/">
          <img src="/logo.png" alt="Monetize Hub" class="brand-logo" />
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
            <a href="/login" class="dropdown-link">Login</a>
            <a href="/register" class="dropdown-link">Create Account</a>
            <a href="/admin" class="dropdown-link">Admin Dashboard</a>
          </div>
        </details>
      </div>
      <nav class="main-nav">
        <a href="/" class="nav-link">Home</a>
        <a href="/about" class="nav-link">About</a>
        <details class="nav-dropdown" open>
          <summary class="nav-link nav-dropdown-toggle">Webapps <span aria-hidden="true">▾</span></summary>
          <div class="nav-dropdown-menu">
            <a href="/excel-to-json" class="dropdown-link">Excel → JSON</a>
            <a href="/json-to-excel" class="dropdown-link">JSON → Excel</a>
            <a href="/xml-json-translator" class="dropdown-link">XML ⇄ JSON Translator</a>
            <a href="/yaml-json-converter" class="dropdown-link">YAML ↔ JSON Converter</a>
            <a href="/json-combiner" class="dropdown-link">JSON Combiner</a>
            <a href="/find-and-replace" class="dropdown-link">Find &amp; Replace</a>
            <a href="/csv-xml-converter" class="dropdown-link">CSV/XML Converter</a>
            <a href="/list-comparison" class="dropdown-link">List Comparison / Diff Checker</a>
            <a href="/html-tag-cleaner" class="dropdown-link">HTML Tag Cleaner</a>
            <a href="/tabify-untabify" class="dropdown-link">Tabify or Untabify</a>
            <a href="/extract-text-inside-quotes" class="dropdown-link">Extract Text Inside Quotes</a>
            <a href="/pdf-splitter" class="dropdown-link">PDF Splitter</a>
            <a href="/bullet-list-extractor" class="dropdown-link">Bullet List Extractor</a>
            <a href="/powerpoint-to-pdf" class="dropdown-link">PowerPoint → PDF</a>
            <a href="/powerpoint-image-extractor" class="dropdown-link">PowerPoint Image Extractor</a>
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
