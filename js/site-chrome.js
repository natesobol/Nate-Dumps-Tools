(() => {
  if (document.querySelector('script[data-proxied-site-chrome="true"]')) {
    return;
  }

  const pathSegments = window.location.pathname.split('/').filter(Boolean);
  const repoIndex = pathSegments.indexOf('Nates-Free-Tools');
  const basePath = repoIndex !== -1 ? `/${pathSegments.slice(0, repoIndex + 1).join('/')}` : '';

  const script = document.createElement('script');
  script.src = `${basePath}/public/js/site-chrome.js`;
  script.dataset.proxiedSiteChrome = 'true';
  document.head.appendChild(script);
})();
