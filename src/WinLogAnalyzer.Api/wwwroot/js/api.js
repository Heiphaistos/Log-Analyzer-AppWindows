// api.js — appels reseau vers l'agent local.

/**
 * Recupere les erreurs critiques du journal.
 * @param {string} log - System | Application | Security
 * @param {number} max - 1..1000
 * @returns {Promise<Array>} liste d'EventEntry
 */
export async function fetchEvents(log, max) {
  const url = `/api/events?log=${encodeURIComponent(log)}&max=${encodeURIComponent(max)}`;
  const res = await fetch(url);

  if (!res.ok) {
    let detail = `Erreur HTTP ${res.status}`;
    try {
      const body = await res.json();
      detail = body.error || body.detail || detail;
    } catch { /* corps non-JSON : on garde le message par defaut */ }
    throw new Error(detail);
  }
  return res.json();
}

/** Statut de l'agent + taille du dictionnaire de solutions. */
export async function fetchHealth() {
  const res = await fetch("/api/health");
  if (!res.ok) throw new Error(`Health HTTP ${res.status}`);
  return res.json();
}
