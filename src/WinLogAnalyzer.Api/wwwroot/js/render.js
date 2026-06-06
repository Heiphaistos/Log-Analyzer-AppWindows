// render.js — generation du HTML a partir des donnees (pas de fetch ici).

import { escapeHtml, formatDate, severityClass } from "./format.js";

/** Cartes statistiques (total, critical, error, resolus). */
export function renderStats(list) {
  const total = list.length;
  const critical = list.filter(e => e.level === "Critical").length;
  const error = list.filter(e => e.level === "Error").length;
  const solved = list.filter(e => e.solution).length;

  return `
    <div class="stat-card"><div class="stat-card__value">${total}</div><div class="stat-card__label">Evenements</div></div>
    <div class="stat-card stat-card--critical"><div class="stat-card__value">${critical}</div><div class="stat-card__label">Critiques</div></div>
    <div class="stat-card stat-card--error"><div class="stat-card__value">${error}</div><div class="stat-card__label">Erreurs</div></div>
    <div class="stat-card stat-card--solved"><div class="stat-card__value">${solved}</div><div class="stat-card__label">Solutions connues</div></div>
  `;
}

/** Bloc solution (ou message si Event ID inconnu). */
function renderSolution(sol) {
  if (!sol) {
    return `<p class="no-solution">Aucune solution repertoriee pour cet Event ID. Ajoute-la dans data/solutions.json.</p>`;
  }
  const links = (sol.links || [])
    .map(u => `<a href="${escapeHtml(u)}" target="_blank" rel="noopener">Documentation ↗</a>`)
    .join("");

  return `
    <div class="solution">
      <div class="solution__title">💡 ${escapeHtml(sol.title)}</div>
      <div class="block">
        <div class="block__label">Explication</div>
        <div class="block__text">${escapeHtml(sol.explanation)}</div>
      </div>
      <div class="block">
        <div class="block__label">Remediation</div>
        <div class="solution__remediation">${escapeHtml(sol.remediation)}</div>
      </div>
      ${links ? `<div class="solution__links">${links}</div>` : ""}
    </div>
  `;
}

/** Une carte evenement depliable. */
function renderEvent(e, index) {
  const lvl = e.level.toLowerCase(); // critical | error
  const badge = severityClass(e.level);
  const title = e.solution ? e.solution.title : `${e.source}`;
  const pid = e.processId ? `PID ${e.processId} · ${escapeHtml(e.processName)}` : "PID inconnu";

  return `
    <article class="event event--${lvl}" data-index="${index}">
      <div class="event__head" data-toggle>
        <span class="event__id">${e.eventId}</span>
        <span class="badge ${badge}">${escapeHtml(e.level)}</span>
        <div class="event__main">
          <div class="event__title">${escapeHtml(title)}</div>
          <div class="event__meta">${escapeHtml(e.source)} · ${formatDate(e.timeCreated)} · ${pid}</div>
        </div>
        <span class="event__chevron">▶</span>
      </div>
      <div class="event__body">
        <div class="block">
          <div class="block__label">Message brut</div>
          <pre class="raw-msg">${escapeHtml(e.message)}</pre>
        </div>
        ${renderSolution(e.solution)}
      </div>
    </article>
  `;
}

/** Liste complete (ou etat vide). */
export function renderEvents(list) {
  if (!list.length) {
    return `<div class="empty">Aucune erreur critique trouvee. 🎉</div>`;
  }
  return list.map(renderEvent).join("");
}
