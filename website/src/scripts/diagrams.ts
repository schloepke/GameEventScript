// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Keep the authored text usable without JavaScript or when rendering fails.
const figures = [...document.querySelectorAll<HTMLElement>('.ges-diagram')];
if (figures.length) {
  const { default: mermaid } = await import('mermaid');
  let generation = 0;
  let pending = false;
  let rendering = false;

  async function render() {
    pending = true;
    if (rendering) return;
    rendering = true;
    try {
      while (pending) {
        pending = false;
        const dark = document.documentElement.dataset.theme === 'dark';
        mermaid.initialize({
          startOnLoad: false,
          securityLevel: 'strict',
          theme: 'base',
          fontFamily: 'system-ui, sans-serif',
          flowchart: { htmlLabels: false, useMaxWidth: true },
          themeVariables: {
            darkMode: dark,
            primaryColor: dark ? '#23262f' : '#edf1ff',
            primaryTextColor: dark ? '#edf3fc' : '#10203c',
            primaryBorderColor: dark ? '#a9c3ff' : '#4564d6',
            secondaryColor: dark ? '#2d3038' : '#e5f4f1',
            secondaryTextColor: dark ? '#edf3fc' : '#10203c',
            secondaryBorderColor: dark ? '#91ded3' : '#247f8b',
            tertiaryColor: dark ? '#23262f' : '#f3f5fa',
            tertiaryTextColor: dark ? '#edf3fc' : '#10203c',
            lineColor: dark ? '#a9bad5' : '#53627b',
            textColor: dark ? '#edf3fc' : '#10203c',
            edgeLabelBackground: dark ? '#17181c' : '#ffffff',
          },
        });
        for (const [index, figure] of figures.entries()) {
          const code = figure.querySelector('code')!;
          const source = figure.querySelector<HTMLDetailsElement>('details')!;
          const target = figure.querySelector<HTMLElement>('.ges-diagram-image')!;
          const id = `ges-diagram-${generation++}-${index}`;
          try {
            const { svg } = await mermaid.render(id, code.textContent || '');
            target.innerHTML = svg;
            target.hidden = false;
            target.tabIndex = 0;
            target.setAttribute('role', 'region');
            target.setAttribute('aria-label', target.querySelector('svg title')?.textContent || 'Diagram');
            if (!figure.dataset.rendered) source.open = false;
            figure.dataset.rendered = 'true';
          } catch (error) {
            target.replaceChildren();
            target.hidden = true;
            source.open = true;
            document.getElementById(`d${id}`)?.remove();
            console.error('Unable to render documentation diagram.', error);
          }
        }
      }
    } finally {
      rendering = false;
    }
  }
  await render();
  new MutationObserver(() => void render()).observe(document.documentElement, {
    attributes: true, attributeFilter: ['data-theme'],
  });
}
