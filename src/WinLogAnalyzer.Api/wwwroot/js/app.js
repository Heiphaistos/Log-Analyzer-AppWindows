// app.js — orchestration : binding UI, etats, filtrage.

import { fetchEvents, fetchHealth } from "./api.js";
import { renderEvents, renderStats } from "./render.js";

const $ = (id) => document.getElementById(id);
let cache = []; // dernier jeu d'evenements charge (pour filtre client)

/** Met a jour l'indicateur de sante en topbar. */
async function refreshHealth() {
  const dot = document.querySelector(".dot");
  try {
    const h = await fetchHealth();
    dot.className = "dot dot--ok";
    $("health-text").textContent = `Agent OK · ${h.solutions} solutions`;
  } catch {
    dot.className = "dot dot--err";
    $("health-text").textContent = "Agent injoignable";
  }
}

/** Applique le filtre texte sur le cache et re-rend. */
function applyFilter() {
  const q = $("search").value.trim().toLowerCase();
  const filtered = !q ? cache : cache.filter(e =>
    String(e.eventId).includes(q) ||
    e.source.toLowerCase().includes(q) ||
    e.message.toLowerCase().includes(q) ||
    (e.solution?.title.toLowerCase().includes(q) ?? false)
  );
  $("stats").innerHTML = renderStats(filtered);
  $("results").innerHTML = renderEvents(filtered);
}

/** Lance une analyse complete. */
async function analyze() {
  const log = $("log").value;
  const max = Number($("max").value) || 100;

  $("stats").innerHTML = "";
  $("results").innerHTML = `<div class="spinner"></div>`;

  try {
    cache = await fetchEvents(log, max);
    applyFilter();
  } catch (err) {
    cache = [];
    $("results").innerHTML = `<div class="error-box">❌ ${err.message}</div>`;
  }
}

/** Delegation : depli/repli d'une carte au clic. */
function onResultsClick(ev) {
  const head = ev.target.closest("[data-toggle]");
  if (!head) return;
  head.parentElement.classList.toggle("open");
}

function init() {
  $("refresh").addEventListener("click", analyze);
  $("search").addEventListener("input", applyFilter);
  $("results").addEventListener("click", onResultsClick);
  refreshHealth();
  analyze();
}

init();
