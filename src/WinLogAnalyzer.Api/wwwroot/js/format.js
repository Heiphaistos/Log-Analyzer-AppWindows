// format.js — helpers de formatage purs (aucun effet de bord).

/** Echappe le HTML pour eviter toute injection depuis les messages de log. */
export function escapeHtml(value) {
  if (value == null) return "";
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

/** Date ISO -> format FR lisible (jj/mm/aaaa hh:mm:ss). */
export function formatDate(iso) {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "(date inconnue)";
  return d.toLocaleString("fr-FR", {
    day: "2-digit", month: "2-digit", year: "numeric",
    hour: "2-digit", minute: "2-digit", second: "2-digit"
  });
}

/** Severite (solution) ou niveau (event) -> classe CSS de badge. */
export function severityClass(severity) {
  switch ((severity || "").toLowerCase()) {
    case "critical": return "badge--critical";
    case "error":    return "badge--error";
    case "warning":  return "badge--warning";
    default:         return "badge--info";
  }
}
