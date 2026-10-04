// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/** Keep the chooser in the top layer so rotated seats and scrolling zones cannot clip it. */
export function createCardActionPicker(onAction) {
  const popup = document.createElement('div');
  popup.id = 'card-action-picker';
  popup.className = 'card-action-picker';
  popup.setAttribute('popover', 'auto');
  popup.setAttribute('role', 'dialog');
  popup.setAttribute('aria-label', 'Choose card action');
  document.body.append(popup);
  let anchor = null;

  function close() {
    popup.hidePopover();
    anchor?.setAttribute('aria-expanded', 'false');
    anchor = null;
  }

  popup.addEventListener('toggle', (event) => {
    if (event.newState === 'closed' && !popup.matches(':popover-open')) {
      anchor?.setAttribute('aria-expanded', 'false');
      anchor = null;
    }
  });
  window.addEventListener('resize', close);
  document.addEventListener('scroll', (event) => {
    if (!popup.contains(event.target)) close();
  }, true);

  return {
    close,

    open(card, offers) {
      close();
      anchor = card;
      popup.replaceChildren();
      for (const offer of offers) {
        const button = document.createElement('button');
        button.type = 'button';
        button.textContent = offer.label + (offer.remaining > 1 ? ` (${offer.remaining} remaining)` : '');
        button.onclick = () => {
          close();
          card.focus({ preventScroll: true });
          onAction(offer);
        };
        popup.append(button);
      }
      card.setAttribute('aria-controls', popup.id);
      card.setAttribute('aria-expanded', 'true');
      popup.showPopover();
      const rect = card.getBoundingClientRect();
      const gap = 8;
      popup.style.left = `${Math.max(gap, Math.min(rect.left + (rect.width - popup.offsetWidth) / 2, window.innerWidth - popup.offsetWidth - gap))}px`;
      popup.style.top = `${Math.max(gap, Math.min(rect.top, window.innerHeight - popup.offsetHeight - gap))}px`;
      popup.querySelector('button')?.focus({ preventScroll: true });
    },
  };
}
