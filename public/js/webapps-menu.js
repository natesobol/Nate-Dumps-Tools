(function () {
  const categories = [
    {
      title: 'Convert & Combine',
      icon: '🔄',
      apps: [
        { slug: 'excel-to-json', label: 'Excel → JSON' },
        { slug: 'json-to-excel', label: 'JSON → Excel' },
        { slug: 'csv-xml-converter', label: 'CSV/XML Converter' },
        { slug: 'yaml-json-converter', label: 'YAML ↔ JSON Converter' },
        { slug: 'xml-json-translator', label: 'XML ⇄ JSON Translator' },
        { slug: 'json-combiner', label: 'JSON Combiner' },
        { slug: 'multi-csv-column-merger', label: 'Multi CSV Column Merger' },
        { slug: 'list-comparison', label: 'List Comparison / Diff Checker' },
        { slug: 'pdf-splitter', label: 'PDF Splitter' },
        { slug: 'powerpoint-to-pdf', label: 'PowerPoint → PDF' },
      ],
    },
    {
      title: 'Cleanup & Editing',
      icon: '🧹',
      apps: [
        { slug: 'find-and-replace', label: 'Find & Replace' },
        { slug: 'tabify-untabify', label: 'Tabify / Untabify' },
        { slug: 'batch-file-renamer', label: 'Batch File Renamer' },
        { slug: 'formatted-text-extractor', label: 'Formatted Text Extractor' },
        { slug: 'document-comment-extractor', label: 'Document Comment Extractor' },
        { slug: 'html-tag-cleaner', label: 'HTML Tag Cleaner' },
      ],
    },
    {
      title: 'Text Extraction',
      icon: '✂️',
      apps: [
        { slug: 'batch-pdf-text-extractor', label: 'Batch PDF Text Extractor' },
        { slug: 'capitalized-phrase-extractor', label: 'Capitalized Phrase Extractor' },
        { slug: 'citation-style-extractor', label: 'Citation Style Extractor' },
        { slug: 'extract-text-inside-quotes', label: 'Extract Text Inside Quotes' },
        { slug: 'highlighted-text-extractor', label: 'Highlighted Text Extractor' },
        { slug: 'pattern-text-extractor', label: 'Pattern Text Extractor' },
        { slug: 'repetitive-phrase-extractor', label: 'Repetitive Phrase Extractor' },
        { slug: 'sentence-keyword-extractor', label: 'Sentence Keyword Extractor' },
        { slug: 'long-word-extractor', label: 'Long Word Extractor' },
        { slug: 'presentation-heading-extractor', label: 'Presentation Heading Extractor' },
        { slug: 'currency-percent-sentence-extractor', label: 'Currency & Percent Sentence Extractor' },
        { slug: 'date-line-extractor', label: 'Date Line Extractor' },
      ],
    },
    {
      title: 'Lists & Tables',
      icon: '📋',
      apps: [
        { slug: 'bullet-list-extractor', label: 'Bullet List Extractor' },
        { slug: 'delimited-text-extractor', label: 'Delimited Text Extractor' },
        { slug: 'list-item-extractor', label: 'List Item Extractor' },
        { slug: 'list-structure-extractor', label: 'List Structure Extractor' },
        { slug: 'table-data-extractor', label: 'Table Data Extractor' },
        { slug: 'table-of-contents-extractor', label: 'Table of Contents Extractor' },
        { slug: 'excel-highlighted-row-extractor', label: 'Excel Highlighted Row Extractor' },
      ],
    },
    {
      title: 'Entities & Records',
      icon: '🗂️',
      apps: [
        { slug: 'named-entity-extractor', label: 'Named Entity Extractor' },
        { slug: 'number-extractor', label: 'Number Extractor' },
        { slug: 'number-row-extractor', label: 'Number Row Extractor' },
        { slug: 'resume-contact-extractor', label: 'Resume Contact Extractor' },
        { slug: 'product-sku-extractor', label: 'Product SKU Extractor' },
        { slug: 'question-extractor', label: 'Question Extractor' },
        { slug: 'phone-number-extractor', label: 'Phone Number Extractor' },
        { slug: 'timestamp-extractor', label: 'Timestamp Extractor' },
        { slug: 'email-thread-extractor', label: 'Email Thread Extractor' },
        { slug: 'email-signature-extractor', label: 'Email Signature Extractor' },
      ],
    },
    {
      title: 'Links, Code & Metadata',
      icon: '🔗',
      apps: [
        { slug: 'color-extractor', label: 'Color Extractor' },
        { slug: 'file-path-extractor', label: 'File Path Extractor' },
        { slug: 'image-path-extractor', label: 'Image Path Extractor' },
        { slug: 'html-metadata-extractor', label: 'HTML Metadata Extractor' },
        { slug: 'domain-name-extractor', label: 'Domain Name Extractor' },
        { slug: 'ip-port-extractor', label: 'IP / Port Extractor' },
        { slug: 'pdf-link-extractor', label: 'PDF Link Extractor' },
        { slug: 'text-url-extractor', label: 'Text URL Extractor' },
        { slug: 'hashtag-mention-extractor', label: 'Hashtag Mention Extractor' },
        { slug: 'code-block-extractor', label: 'Code Block Extractor' },
        { slug: 'code-comment-extractor', label: 'Code Comment Extractor' },
        { slug: 'hyperlink-text-extractor', label: 'Hyperlink Text Extractor' },
      ],
    },
    {
      title: 'Downloads & Media',
      icon: '📥',
      apps: [
        { slug: 'audio-only-extractor', label: 'Audio Only Extractor' },
        { slug: 'podcast-episode-downloader', label: 'Podcast Episode Downloader' },
        { slug: 'youtube-video-downloader', label: 'YouTube Video Downloader' },
        { slug: 'youtube-playlist-downloader', label: 'YouTube Playlist Downloader' },
        { slug: 'VimeoVideoDownloader', label: 'Vimeo Video Downloader' },
        { slug: 'facebook-reel-downloader', label: 'Facebook Reel Downloader' },
        { slug: 'powerpoint-image-extractor', label: 'PowerPoint Image Extractor' },
        { slug: 'powerpoint-slide-exporter', label: 'PowerPoint Slide Exporter' },
      ],
    },
  ];

  const rootIndexSlugs = new Set([
    'csv-xml-converter',
    'excel-to-json',
    'highlighted-text-extractor',
    'xml-json-translator',
    'yaml-json-converter',
  ]);

  const customPaths = {};

  function getBasePath() {
    const segments = window.location.pathname.split('/').filter(Boolean);
    const repoIndex = segments.indexOf('Nates-Free-Tools');
    return repoIndex !== -1 ? `/${segments.slice(0, repoIndex + 1).join('/')}` : '.';
  }

  function getAppPath(slug) {
    if (customPaths[slug]) return customPaths[slug];
    const folder = rootIndexSlugs.has(slug) ? 'index.html' : 'wwwroot/index.html';
    return `apps/${slug}/${folder}`;
  }

  function buildLink(basePath, slug) {
    const normalizedBase = basePath.endsWith('/') ? basePath.slice(0, -1) : basePath;
    const appPath = getAppPath(slug);
    const normalizedPath = appPath.startsWith('/') ? appPath.slice(1) : appPath;
    return `${normalizedBase}/${normalizedPath}`;
  }

  function buildMenu(basePath = getBasePath()) {
    return categories
      .map(category => {
        const links = category.apps
          .map(app => `<a class="dropdown-link" href="${buildLink(basePath, app.slug)}">${app.label}</a>`)
          .join('');

        return `
<section class="dropdown-group">
  <div class="dropdown-heading"><span class="dropdown-icon" aria-hidden="true">${category.icon || ''}</span>${category.title}</div>
  <div class="dropdown-links-grid">${links}</div>
</section>`;
      })
      .join('');
  }

  function populateMenus() {
    const html = buildMenu();
    document.querySelectorAll('[data-webapps-menu]').forEach(menu => {
      menu.classList.add('mega-menu');
      menu.innerHTML = html;
    });
  }

  window.NDTWebappsMenu = { buildMenu, populateMenus, getBasePath };

  document.addEventListener('DOMContentLoaded', populateMenus);
})();
