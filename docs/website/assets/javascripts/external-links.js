function updateExternalLinks(root = document) {
  const links = root.querySelectorAll('a[href]');

  for (const link of links) {
    const href = link.getAttribute('href');
    if (!href) {
      continue;
    }

    if (
      href.startsWith('#') ||
      href.startsWith('/') ||
      href.startsWith('./') ||
      href.startsWith('../') ||
      href.startsWith('mailto:') ||
      href.startsWith('tel:')
    ) {
      continue;
    }

    let url;
    try {
      url = new URL(href, window.location.href);
    } catch {
      continue;
    }

    if (url.origin === window.location.origin) {
      continue;
    }

    link.setAttribute('target', '_blank');
    link.setAttribute('rel', 'noopener noreferrer');
  }
}

updateExternalLinks();

if (typeof document$ !== 'undefined') {
  document$.subscribe(() => updateExternalLinks());
}
