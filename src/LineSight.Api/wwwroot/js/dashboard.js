// LineSight browser dashboard.
// Polls the same REST API the WPF dashboard uses and renders it - no framework, no build step.
'use strict';

const REFRESH_MS = 3000;
const HISTORY_POINTS = 40;   // sparkline length per station (client-side, resets on reload)

const state = {
  windowMinutes: 15,
  stations: [],          // StationStatusResponse[] in line order
  oee: new Map(),        // stationId -> OeeResponse
  history: new Map(),    // stationId -> [{ t: Date, oee: number }]
};

// ---------------------------------------------------------------- helpers

const $ = (id) => document.getElementById(id);

function esc(value) {
  return String(value ?? '').replace(/[&<>"']/g, (c) =>
    ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
}

const pct = (ratio, digits = 1) => `${(ratio * 100).toFixed(digits)}%`;

function duration(seconds) {
  const s = Math.round(seconds);
  if (s < 60) return `${s}s`;
  const m = Math.floor(s / 60);
  if (m < 60) return `${m}m ${s % 60}s`;
  return `${Math.floor(m / 60)}h ${m % 60}m`;
}

function ago(iso) {
  if (!iso) return '–';
  const s = Math.max(0, Math.round((Date.now() - new Date(iso)) / 1000));
  if (s < 60) return `${s}s ago`;
  if (s < 3600) return `${Math.floor(s / 60)}m ago`;
  return new Date(iso).toLocaleString();
}

const time = (iso) => new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
const number = (n) => n.toLocaleString();
const stateClass = (s) => (s ? s.toLowerCase() : 'unknown');

/** GET JSON from the API. Returns null on 404, throws on other errors. */
async function api(path) {
  const response = await fetch(path, { headers: { Accept: 'application/json' } });
  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  return response.json();
}

// ---------------------------------------------------------------- tooltip

const tooltip = $('tooltip');

function showTooltip(html, event) {
  tooltip.innerHTML = html;
  tooltip.hidden = false;
  const pad = 14;
  const { innerWidth: w, innerHeight: h } = window;
  const rect = tooltip.getBoundingClientRect();
  let x = event.clientX + pad;
  let y = event.clientY + pad;
  if (x + rect.width > w - 8) x = event.clientX - rect.width - pad;
  if (y + rect.height > h - 8) y = event.clientY - rect.height - pad;
  tooltip.style.left = `${x}px`;
  tooltip.style.top = `${y}px`;
}

const hideTooltip = () => { tooltip.hidden = true; };

// ---------------------------------------------------------------- data refresh

async function refresh() {
  const to = new Date();
  const from = new Date(to.getTime() - state.windowMinutes * 60_000);
  const range = `from=${encodeURIComponent(from.toISOString())}&to=${encodeURIComponent(to.toISOString())}`;

  try {
    const stations = await api('/api/stations');
    const [oees, recent] = await Promise.all([
      Promise.all(stations.map((s) => api(`/api/oee/${s.id}?${range}`))),
      api('/api/modules?take=10'),
    ]);

    state.stations = stations;
    stations.forEach((s, i) => {
      if (!oees[i]) return;
      state.oee.set(s.id, oees[i]);
      const points = state.history.get(s.id) ?? [];
      points.push({ t: to, oee: oees[i].oee });
      state.history.set(s.id, points.slice(-HISTORY_POINTS));
    });

    hideTooltip();
    renderKpis();
    renderLine();
    renderStationTable();
    renderPareto();
    renderScrap();
    renderRecent(recent ?? []);
    setConnection(true, `Live · updated ${to.toLocaleTimeString()}`);
  } catch (error) {
    setConnection(false, 'API unreachable – retrying');
    console.warn('Refresh failed', error);
  } finally {
    // Chain the next poll only after this one finishes, so requests never pile up.
    setTimeout(refresh, REFRESH_MS);
  }
}

function setConnection(live, text) {
  const el = $('connection');
  el.classList.toggle('live', live);
  el.classList.toggle('offline', !live);
  el.querySelector('.label').textContent = text;
}

// ---------------------------------------------------------------- KPIs

function renderKpis() {
  const results = state.stations.map((s) => state.oee.get(s.id)).filter(Boolean);
  if (results.length === 0) return;

  const avg = results.reduce((sum, r) => sum + r.oee, 0) / results.length;
  const first = state.oee.get(state.stations[0].id);
  const last = state.oee.get(state.stations[state.stations.length - 1].id);
  const scrap = results.reduce((sum, r) => sum + r.scrapCount, 0);
  const started = first?.totalCount ?? 0;
  const downtimeSeconds = results.flatMap((r) => r.downtime).reduce((sum, d) => sum + d.durationSeconds, 0);
  const stops = results.flatMap((r) => r.downtime).reduce((sum, d) => sum + d.occurrences, 0);
  const downNow = state.stations.filter((s) => s.currentState === 'Down').length;
  const worst = results.reduce((a, b) => (b.oee < a.oee ? b : a));

  $('kpi-oee').textContent = pct(avg);
  $('kpi-oee-note').textContent = `Lowest: ${worst.stationName} ${pct(worst.oee)} · last ${windowLabel()}`;
  $('kpi-completed').textContent = number(last?.goodCount ?? 0);
  $('kpi-completed-note').textContent = `${number(started)} started at ${state.stations[0].name}`;
  $('kpi-scrap').textContent = number(scrap);
  $('kpi-scrap-note').textContent = started > 0 ? `${pct(scrap / started)} of modules started` : 'No modules started yet';
  $('kpi-downtime').textContent = duration(downtimeSeconds);
  $('kpi-downtime-note').textContent =
    `${stops} stop${stops === 1 ? '' : 's'} · ${downNow} station${downNow === 1 ? '' : 's'} down now`;
}

const windowLabel = () => (state.windowMinutes < 60 ? `${state.windowMinutes} min` : `${state.windowMinutes / 60} h`);

// ---------------------------------------------------------------- station cards

function renderLine() {
  $('line').innerHTML = state.stations.map(stationCard).join('');

  // Wire sparkline hover (crosshair + tooltip) for each card.
  document.querySelectorAll('.spark').forEach((svg) => {
    const id = Number(svg.dataset.station);
    svg.addEventListener('mousemove', (e) => sparkHover(svg, id, e));
    svg.addEventListener('mouseleave', () => { svg.querySelector('.hover')?.remove(); hideTooltip(); });
  });
}

function stationCard(station) {
  const oee = state.oee.get(station.id);
  const cls = stateClass(station.currentState);
  const reason = station.currentState === 'Down'
    ? `<strong>${esc(station.downtimeReason)}</strong> · since ${esc(time(station.lastEventUtc))}`
    : station.lastEventUtc ? `Last report ${esc(ago(station.lastEventUtc))}` : 'No data yet';

  const factor = (label, value) => `
    <div class="factor">
      <span class="factor-label">${label}</span>
      <span class="meter" role="meter" aria-valuemin="0" aria-valuemax="100" aria-valuenow="${Math.round(value * 100)}" aria-label="${label}">
        <span style="width:${Math.min(100, value * 100).toFixed(1)}%"></span>
      </span>
      <span class="factor-value">${pct(value, 0)}</span>
    </div>`;

  return `
    <article class="station ${cls}" aria-label="${esc(station.name)}">
      <div class="station-head">
        <span class="station-name">${esc(station.name)}</span>
        <span class="station-seq">#${station.sequence}</span>
      </div>
      <span class="state-chip ${cls}"><span class="state-icon"></span>${esc(station.currentState ?? 'No data')}</span>
      <div class="reason">${reason}</div>
      <div class="oee-row">
        <div>
          <div class="oee-value">${oee ? pct(oee.oee) : '–'}</div>
          <div class="oee-caption">OEE</div>
        </div>
        ${sparkline(station.id)}
      </div>
      ${oee ? `<div class="factors">
        ${factor('Availability', oee.availability)}
        ${factor('Performance', oee.performance)}
        ${factor('Quality', oee.quality)}
      </div>
      <div class="counts"><span>Good <strong>${number(oee.goodCount)}</strong></span><span>Scrap <strong>${number(oee.scrapCount)}</strong></span></div>
      ${oee.warning ? `<div class="flag" role="note">${esc(oee.warning)}</div>` : ''}` : ''}
    </article>`;
}

// Sparkline geometry: fixed 0-100% scale so stations are comparable at a glance.
const SPARK = { w: 92, h: 34, pad: 3 };

function sparkPoints(id) {
  const points = state.history.get(id) ?? [];
  const max = Math.max(1, ...points.map((p) => p.oee));
  const step = points.length > 1 ? (SPARK.w - SPARK.pad * 2) / (points.length - 1) : 0;
  return points.map((p, i) => ({
    ...p,
    x: SPARK.pad + i * step,
    y: SPARK.h - SPARK.pad - (p.oee / max) * (SPARK.h - SPARK.pad * 2),
  }));
}

function sparkline(id) {
  const pts = sparkPoints(id);
  if (pts.length === 0) return '';
  const path = pts.map((p, i) => `${i ? 'L' : 'M'}${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' ');
  const last = pts[pts.length - 1];
  return `
    <svg class="spark" data-station="${id}" viewBox="0 0 ${SPARK.w} ${SPARK.h}" role="img" aria-label="OEE trend">
      <rect x="0" y="0" width="${SPARK.w}" height="${SPARK.h}" fill="transparent"/>
      <path class="line-path" d="${path}"/>
      <circle class="last" cx="${last.x.toFixed(1)}" cy="${last.y.toFixed(1)}" r="4"/>
    </svg>`;
}

function sparkHover(svg, id, event) {
  const pts = sparkPoints(id);
  if (pts.length === 0) return;
  const rect = svg.getBoundingClientRect();
  const x = ((event.clientX - rect.left) / rect.width) * SPARK.w;
  const nearest = pts.reduce((a, b) => (Math.abs(b.x - x) < Math.abs(a.x - x) ? b : a));

  svg.querySelector('.hover')?.remove();
  svg.insertAdjacentHTML('beforeend', `
    <g class="hover">
      <line class="hover-line" x1="${nearest.x}" x2="${nearest.x}" y1="0" y2="${SPARK.h}"/>
      <circle class="hover-dot" cx="${nearest.x}" cy="${nearest.y}" r="4"/>
    </g>`);
  showTooltip(`<strong>OEE ${pct(nearest.oee)}</strong>${nearest.t.toLocaleTimeString()}`, event);
}

// Accessible table view of the same numbers.
function renderStationTable() {
  const rows = state.stations.map((s) => {
    const o = state.oee.get(s.id);
    return `<tr>
      <td>${s.sequence}. ${esc(s.name)}</td>
      <td>${esc(s.currentState ?? '–')}</td>
      <td>${esc(s.downtimeReason ?? '')}</td>
      <td class="num">${o ? pct(o.oee) : '–'}</td>
      <td class="num">${o ? pct(o.availability) : '–'}</td>
      <td class="num">${o ? pct(o.performance) : '–'}</td>
      <td class="num">${o ? pct(o.quality) : '–'}</td>
      <td class="num">${o ? number(o.goodCount) : '–'}</td>
      <td class="num">${o ? number(o.scrapCount) : '–'}</td>
    </tr>`;
  }).join('');
  $('station-table').innerHTML = `
    <thead><tr><th>Station</th><th>State</th><th>Reason</th><th class="num">OEE</th><th class="num">Avail.</th>
      <th class="num">Perf.</th><th class="num">Quality</th><th class="num">Good</th><th class="num">Scrap</th></tr></thead>
    <tbody>${rows}</tbody>`;
}

// ---------------------------------------------------------------- downtime Pareto

function renderPareto() {
  $('pareto-window').textContent = `Last ${windowLabel()} · all stations`;

  // Sum each reason across stations, remembering the per-station split for the tooltip.
  const byReason = new Map();
  for (const station of state.stations) {
    for (const d of state.oee.get(station.id)?.downtime ?? []) {
      const entry = byReason.get(d.reason) ?? { reason: d.reason, seconds: 0, stops: 0, stations: [] };
      entry.seconds += d.durationSeconds;
      entry.stops += d.occurrences;
      entry.stations.push({ name: station.name, seconds: d.durationSeconds });
      byReason.set(d.reason, entry);
    }
  }

  const reasons = [...byReason.values()].sort((a, b) => b.seconds - a.seconds);
  if (reasons.length === 0) {
    $('pareto').innerHTML = '<p class="muted">No downtime in this window.</p>';
    return;
  }

  const max = reasons[0].seconds;
  $('pareto').innerHTML = `<div class="pareto">${reasons.map((r, i) => `
    <div class="pareto-row">
      <span class="pareto-label">${esc(r.reason)}</span>
      <span class="pareto-track" data-index="${i}">
        <span class="pareto-bar" style="width:${Math.max(1, (r.seconds / max) * 78).toFixed(1)}%"></span>
        <span class="pareto-value">${duration(r.seconds)} · ${r.stops} stop${r.stops === 1 ? '' : 's'}</span>
      </span>
    </div>`).join('')}</div>`;

  $('pareto').querySelectorAll('.pareto-track').forEach((el) => {
    const r = reasons[Number(el.dataset.index)];
    const detail = r.stations.sort((a, b) => b.seconds - a.seconds)
      .map((s) => `${esc(s.name)}: ${duration(s.seconds)}`).join('<br>');
    el.addEventListener('mousemove', (e) => showTooltip(`<strong>${esc(r.reason)}</strong>${detail}`, e));
    el.addEventListener('mouseleave', hideTooltip);
  });
}

// ---------------------------------------------------------------- scrap by station

function renderScrap() {
  const rows = state.stations.map((s) => ({ station: s, oee: state.oee.get(s.id) })).filter((r) => r.oee);
  const max = Math.max(...rows.map((r) => r.oee.scrapCount));
  if (rows.length === 0 || max === 0) {
    $('scrap').innerHTML = '<p class="muted">No scrap in this window.</p>';
    return;
  }

  $('scrap').innerHTML = `<div class="pareto">${rows.map((r, i) => {
    const rate = r.oee.totalCount > 0 ? r.oee.scrapCount / r.oee.totalCount : 0;
    return `
      <div class="pareto-row">
        <span class="pareto-label">${esc(r.station.name)}</span>
        <span class="pareto-track" data-index="${i}">
          <span class="pareto-bar" style="width:${Math.max(1, (r.oee.scrapCount / max) * 78).toFixed(1)}%"></span>
          <span class="pareto-value">${number(r.oee.scrapCount)} · ${pct(rate)}</span>
        </span>
      </div>`;
  }).join('')}</div>`;

  $('scrap').querySelectorAll('.pareto-track').forEach((el) => {
    const { station, oee } = rows[Number(el.dataset.index)];
    el.addEventListener('mousemove', (e) => showTooltip(
      `<strong>${esc(station.name)}</strong>Good ${number(oee.goodCount)} · Scrap ${number(oee.scrapCount)}<br>Quality ${pct(oee.quality)}`, e));
    el.addEventListener('mouseleave', hideTooltip);
  });
}

// ---------------------------------------------------------------- recent modules

function renderRecent(modules) {
  if (modules.length === 0) {
    $('recent').innerHTML = '<tbody><tr><td class="muted">No modules yet – start the simulator.</td></tr></tbody>';
    return;
  }
  $('recent').innerHTML = `
    <thead><tr><th>Serial</th><th>Status</th><th>Last station</th><th class="num">Updated</th></tr></thead>
    <tbody>${modules.map((m) => `
      <tr tabindex="0" data-serial="${esc(m.serialNumber)}">
        <td class="mono">${esc(m.serialNumber)}</td>
        <td><span class="badge ${esc(m.status)}">${esc(statusText(m.status))}</span></td>
        <td>${esc(m.lastStation ?? '–')}</td>
        <td class="num muted">${esc(ago(m.lastStepUtc))}</td>
      </tr>`).join('')}
    </tbody>`;

  $('recent').querySelectorAll('tbody tr').forEach((row) => {
    const open = () => { $('search-input').value = row.dataset.serial; trace(row.dataset.serial); };
    row.addEventListener('click', open);
    row.addEventListener('keydown', (e) => { if (e.key === 'Enter') open(); });
  });
}

const statusText = (status) => (status === 'InProgress' ? 'In progress' : status);

// ---------------------------------------------------------------- genealogy

async function trace(serial) {
  const target = $('genealogy');
  serial = serial.trim();
  if (!serial) return;

  target.innerHTML = '<p class="muted">Loading…</p>';
  try {
    const module = await api(`/api/modules/${encodeURIComponent(serial)}`);
    // The timeline is drawn against the full station list (to show pending stations too).
    if (state.stations.length === 0) state.stations = await api('/api/stations');
    if (!module) {
      target.innerHTML = `<p class="error">No module found with serial <span class="mono">${esc(serial)}</span>.</p>`;
      return;
    }
    target.innerHTML = genealogyView(module);
  } catch (error) {
    target.innerHTML = '<p class="error">Could not reach the API.</p>';
  }
}

function genealogyView(module) {
  const steps = new Map(module.steps.map((s) => [s.stationId, s]));
  const total = module.steps.length > 1
    ? (new Date(module.steps.at(-1).timestampUtc) - new Date(module.steps[0].timestampUtc)) / 1000
    : 0;

  let previous = null;
  const timeline = state.stations.map((station) => {
    const step = steps.get(station.id);
    if (!step) {
      const why = module.status === 'Scrapped' ? 'Not reached (scrapped)' : 'Pending';
      return `<div class="step"><div class="step-marker">${station.sequence}</div>
        <div class="step-name">${esc(station.name)}</div><div class="step-meta">${why}</div></div>`;
    }
    const delta = previous ? ` · +${duration((new Date(step.timestampUtc) - new Date(previous)) / 1000)}` : '';
    previous = step.timestampUtc;
    const pass = step.result === 'Pass';
    return `<div class="step ${pass ? 'pass' : 'scrap'}">
      <div class="step-marker" aria-label="${pass ? 'Passed' : 'Scrapped'}">${pass ? '✓' : '✕'}</div>
      <div class="step-name">${esc(station.name)}</div>
      <div class="step-meta"><strong>${pass ? 'Pass' : 'Scrap'}</strong> · ${esc(time(step.timestampUtc))}${delta}</div>
    </div>`;
  }).join('');

  return `
    <div class="module-head">
      <span class="module-serial mono">${esc(module.serialNumber)}</span>
      <span class="badge ${esc(module.status)}">${esc(statusText(module.status))}</span>
      <span class="muted small">Started ${esc(new Date(module.createdUtc).toLocaleString())}
        ${total ? ` · ${duration(total)} on the line` : ''}</span>
    </div>
    <div class="timeline">${timeline}</div>`;
}

// ---------------------------------------------------------------- controls

$('search-form').addEventListener('submit', (e) => {
  e.preventDefault();
  trace($('search-input').value);
});

$('window-picker').addEventListener('click', (e) => {
  const button = e.target.closest('button[data-minutes]');
  if (!button) return;
  document.querySelectorAll('#window-picker button').forEach((b) => b.classList.toggle('active', b === button));
  state.windowMinutes = Number(button.dataset.minutes);
  state.history.clear();   // trend points from a different window aren't comparable
});

// Theme: follows the OS by default; the toggle pins light or dark (remembered per browser).
function applyStoredTheme() {
  try {
    const saved = localStorage.getItem('linesight-theme');
    if (saved) document.documentElement.dataset.theme = saved;
  } catch { /* storage unavailable: just follow the OS */ }
}

$('theme-toggle').addEventListener('click', () => {
  const current = document.documentElement.dataset.theme
    ?? (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
  const next = current === 'dark' ? 'light' : 'dark';
  document.documentElement.dataset.theme = next;
  try { localStorage.setItem('linesight-theme', next); } catch { /* ignore */ }
});

applyStoredTheme();
refresh();

// Deep link: /?serial=FS-20261022-00001 opens that module's genealogy straight away.
const linkedSerial = new URLSearchParams(location.search).get('serial');
if (linkedSerial) {
  $('search-input').value = linkedSerial;
  trace(linkedSerial);
}
