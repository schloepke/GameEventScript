// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

// Tabbed C#/Swift examples. Without JavaScript every panel stays visible with its label.
const storageKey = 'ges-language';
const groups = [...document.querySelectorAll<HTMLElement>('[data-ges-tabs]')];

function select(language: string) {
  for (const group of groups) {
    const panels = [...group.querySelectorAll<HTMLElement>(':scope > [data-ges-language]')];
    if (!panels.some((panel) => panel.dataset.gesLanguage === language)) continue;
    for (const panel of panels) panel.hidden = panel.dataset.gesLanguage !== language;
    for (const tab of group.querySelectorAll<HTMLButtonElement>(':scope > [role="tablist"] > [role="tab"]')) {
      const selected = tab.dataset.gesLanguage === language;
      tab.setAttribute('aria-selected', String(selected));
      tab.tabIndex = selected ? 0 : -1;
    }
  }
}

function stored() {
  try { return localStorage.getItem(storageKey); } catch { return null; }
}

groups.forEach((group, groupIndex) => {
  const panels = [...group.querySelectorAll<HTMLElement>(':scope > [data-ges-language]')];
  const list = document.createElement('div');
  list.setAttribute('role', 'tablist');
  list.setAttribute('aria-label', 'Programming language');
  panels.forEach((panel, panelIndex) => {
    const tab = document.createElement('button');
    tab.type = 'button';
    tab.setAttribute('role', 'tab');
    tab.id = `ges-tab-${groupIndex}-${panelIndex}`;
    panel.id = `ges-panel-${groupIndex}-${panelIndex}`;
    tab.setAttribute('aria-controls', panel.id);
    panel.setAttribute('role', 'tabpanel');
    panel.setAttribute('aria-labelledby', tab.id);
    tab.dataset.gesLanguage = panel.dataset.gesLanguage;
    tab.textContent = panel.dataset.gesLabel ?? '';
    tab.addEventListener('click', () => {
      const language = panel.dataset.gesLanguage!;
      // Keep the clicked group in place while groups above it change height.
      const top = group.getBoundingClientRect().top;
      select(language);
      window.scrollBy(0, group.getBoundingClientRect().top - top);
      try { localStorage.setItem(storageKey, language); } catch { /* storage unavailable */ }
    });
    tab.addEventListener('keydown', (event) => {
      if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
      const tabs = [...list.querySelectorAll<HTMLButtonElement>('[role="tab"]')];
      const next = tabs[(tabs.indexOf(tab) + (event.key === 'ArrowRight' ? 1 : tabs.length - 1)) % tabs.length];
      next.click();
      next.focus();
    });
    list.append(tab);
  });
  group.prepend(list);
  group.classList.add('ges-tabs-ready');
});
if (groups.length) select(stored() ?? groups[0].querySelector<HTMLElement>('[data-ges-language]')!.dataset.gesLanguage!);
