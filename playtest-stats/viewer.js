const fileInput = document.querySelector("#file");
const workspace = document.querySelector("#workspace");
const panel = document.querySelector("#report");
const status = document.querySelector("#status");
const tabs = [...document.querySelectorAll('[role="tab"]')];
const number = new Intl.NumberFormat("en");
const signed = new Intl.NumberFormat("en", { signDisplay: "exceptZero" });
const oreFields = [
  ["Produced", "produced"], ["Retrieved", "retrieved"], ["Salvaged", "salvaged"],
  ["Lost", "lost"], ["Credited", "credited"],
];
let report;
let selected = 0;
const openDays = new Set();

// Validate the Unity report before any of its rows reach a renderer.
function validate(value) {
  const record = item => item !== null && typeof item === "object" && !Array.isArray(item);
  const rows = (items, valid) => Array.isArray(items) && items.every(item => record(item) && valid(item));
  const optionalNumber = field => field === undefined || Number.isFinite(field);
  const optionalText = field => field == null || typeof field === "string";
  const diveFields = [
    ...oreFields.map(([, key]) => key), "carriedNow", "carriedAtReturn", "activeSeconds",
    "machinesPacked", "stimulantsPacked", "scans", "spotsRevealed", "machinesDeployed",
    "machinesAbandoned", "deliveriesCollected", "deliveriesMissed",
  ];
  if (!record(value) || !Number.isInteger(value.schemaVersion)) {
    throw new Error("Choose a JSON report exported by the game.");
  }
  if (value.schemaVersion !== 1) throw new Error(`Report version ${value.schemaVersion} is unsupported.`);
  const validDay = day => Number.isInteger(day.number) && Number.isFinite(day.openingBalance) &&
    Number.isFinite(day.balance) && rows(day.attempts, dive => typeof dive.outcome === "string" &&
      optionalText(dive.tank) && diveFields.every(key => optionalNumber(dive[key]))) &&
    rows(day.transactions, entry => typeof entry.kind === "string" && Number.isFinite(entry.amount) &&
      optionalText(entry.item) && optionalNumber(entry.samples));
  if (!Number.isInteger(value.currentDay) || !rows(value.days, validDay) ||
      !rows(value.quest, entry => Number.isInteger(entry.day) && typeof entry.kind === "string" &&
        optionalText(entry.subject) && optionalText(entry.detail))) {
    throw new Error("This report has missing or invalid day, excursion, or quest data.");
  }
  return value;
}

// Build text nodes directly so a report can never inject markup.
function element(tag, content, className) {
  const node = document.createElement(tag);
  if (content !== undefined) node.textContent = content;
  if (className) node.className = className;
  return node;
}

function section(parent, heading, level = "h2") {
  const block = element("section", undefined, "block");
  block.append(element(level, heading));
  parent.append(block);
  return block;
}

function metrics(parent, values) {
  const grid = element("dl", undefined, "grid");
  for (const [label, value] of values) {
    const metric = element("div", undefined, "metric");
    metric.append(element("dt", label), element("dd", value));
    grid.append(metric);
  }
  parent.append(grid);
}

function questTimeline(parent, events) {
  if (!events.length) {
    parent.append(element("p", "No quest activity recorded yet.", "detail"));
    return;
  }
  const list = element("ol", undefined, "lines");
  for (const event of events) {
    const entry = element("li");
    entry.append(element("span", `Day ${event.day}`, "muted"), element("strong", event.kind));
    if (event.subject || event.detail) {
      entry.append(element("span", [event.subject, event.detail].filter(Boolean).join(" · "), "muted"));
    }
    list.append(entry);
  }
  parent.append(list);
}

function showSummary() {
  const dives = report.days.flatMap(day => day.attempts);
  const transactions = report.days.flatMap(day => day.transactions);
  const creditTotal = kind => transactions.filter(entry => entry.kind === kind)
    .reduce((total, entry) => total + entry.amount, 0);
  const run = section(panel, "Run");
  metrics(run, [
    ["Day reached", report.currentDay], ["Status", report.completed ? "Completed" : "In progress"],
    ["Credits", report.days.length ? number.format(report.days.at(-1).balance) : "—"],
    ...(typeof report.buildVersion === "string" && report.buildVersion ? [["Game build", report.buildVersion]] : []),
  ]);
  if (report.debuggerUsed) run.append(element("p", "This run was modified with the editor debugger.", "detail"));

  const excursions = section(panel, "Excursions");
  const outcomes = ["Returned", "Incapacitated", "Tutorial retry", "In progress"];
  const interrupted = dives.filter(dive => dive.outcome === "Interrupted by debugger").length;
  const counts = outcomes.map(outcome => [outcome, dives.filter(dive => dive.outcome === outcome).length]);
  if (interrupted) counts.push(["Interrupted", interrupted]);
  metrics(excursions, counts);
  const totals = oreFields.map(([label, key]) => [label, dives.reduce((sum, dive) => sum + (dive[key] ?? 0), 0)]);
  const max = Math.max(1, ...totals.map(([, total]) => total));
  const bars = element("div", undefined, "bars");
  for (const [label, total] of totals) {
    const row = element("div", undefined, "bar-row");
    const bar = element("meter");
    bar.max = max;
    bar.value = total;
    bar.setAttribute("aria-label", `Tellurium samples ${label.toLowerCase()}`);
    row.append(element("span", label), bar, element("strong", number.format(total)));
    bars.append(row);
  }
  excursions.append(bars);

  const credits = section(panel, "Credits");
  const bonus = creditTotal("Credit bonus");
  metrics(credits, [
    ["From excursions", creditTotal("Excursion mining income")], ["From Cole", creditTotal("Cole's mining income")],
    ["Shop spending", -creditTotal("Purchase") || 0], ["Daily expenses", -creditTotal("Daily expenses") || 0],
    ...(bonus ? [["Bonus credits", bonus]] : []),
  ].map(([label, total]) => [label, number.format(total)]));
  questTimeline(section(panel, "Recent quest activity"), report.quest.slice(-4).reverse());
}

function attemptDetails(parent, dive, index) {
  const attempt = section(parent, `Attempt ${index + 1} · ${dive.outcome}`, "h3");
  const seconds = Math.max(0, Math.floor(dive.activeSeconds ?? 0));
  const duration = `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, "0")}`;
  const loadout = [
    dive.tank, `${dive.machinesPacked ?? 0} machines`, `${dive.stimulantsPacked ?? 0} stimulants`,
    `Camera ${dive.camera ? "yes" : "no"}`, `Flippers ${dive.flippers ? "yes" : "no"}`, `${duration} active`,
  ].filter(Boolean);
  attempt.append(element("p", loadout.join(" · "), "detail"));
  const counts = [
    [...oreFields.slice(0, 3), ["Carried", dive.carriedNow === undefined ? "carriedAtReturn" : "carriedNow"], ...oreFields.slice(3)],
    [["Scans", "scans"], ["New spots", "spotsRevealed"], ["Machines deployed", "machinesDeployed"],
      ["Abandoned", "machinesAbandoned"], ["Packages collected", "deliveriesCollected"], ["Missed", "deliveriesMissed"]],
  ];
  for (const group of counts) {
    attempt.append(element("p", group.map(([label, key]) => `${number.format(dive[key] ?? 0)} ${label.toLowerCase()}`).join(" · "), "detail"));
  }
}

function showDays() {
  panel.append(element("h2", "Day Log"));
  if (!report.days.length) panel.append(element("p", "No days recorded yet.", "detail"));
  report.days.forEach((day, index) => {
    const current = index === report.days.length - 1 && !report.completed;
    const state = day.settled ? "Settled" : current ? "Current day" : "Unfinished";
    const details = element("details", undefined, "day");
    const summary = element("summary", `Day ${day.number}`);
    summary.append(element("span", `${state} · ${number.format(day.balance)} credits`, "day-meta"));
    const body = element("div", undefined, "day-body");
    body.append(element("p", `Opening balance ${number.format(day.openingBalance)} · ${day.settled ? "Closing" : "Recorded"} balance ${number.format(day.balance)}`, "detail"));
    if (day.transactions.length) {
      const ledger = element("ul", undefined, "lines ledger");
      for (const transaction of day.transactions) {
        let label = transaction.kind === "Purchase" && transaction.item ? transaction.item : transaction.kind;
        if (transaction.samples > 0) label += ` (${transaction.samples} samples)`;
        const row = element("li");
        row.append(element("span", label), element("strong", `${signed.format(transaction.amount)} credits`));
        ledger.append(row);
      }
      section(body, "Transactions", "h3").append(ledger);
    }
    day.attempts.forEach((dive, attempt) => attemptDetails(body, dive, attempt));
    details.append(summary, body);
    details.open = openDays.has(index);
    details.addEventListener("toggle", () => {
      if (!details.isConnected) return;
      if (details.open) openDays.add(index);
      else openDays.delete(index);
    });
    panel.append(details);
  });
}

function selectTab(index) {
  selected = index;
  for (const [position, tab] of tabs.entries()) {
    tab.setAttribute("aria-selected", String(position === selected));
    tab.tabIndex = position === selected ? 0 : -1;
  }
  panel.setAttribute("aria-labelledby", tabs[selected].id);
  panel.replaceChildren();
  if (selected === 0) showSummary();
  if (selected === 1) showDays();
  if (selected === 2) questTimeline(section(panel, "Quest & Choices"), report.quest);
}

let importRequest = 0;
async function openFile(file) {
  if (!file) return;
  const request = ++importRequest;
  status.classList.remove("error");
  status.textContent = `Opening ${file.name}…`;
  try {
    const text = await file.text();
    if (request !== importRequest) return;
    report = validate(JSON.parse(text.replace(/^\uFEFF/, "")));
    openDays.clear();
    tabs.forEach(tab => { tab.disabled = false; });
    selectTab(0);
    status.textContent = `Loaded ${file.name}`;
  } catch (error) {
    if (request !== importRequest) return;
    status.classList.add("error");
    status.textContent = error instanceof SyntaxError ? "This file is not valid JSON." : error.message;
  }
}

tabs.forEach((tab, index) => {
  tab.addEventListener("click", () => selectTab(index));
  tab.addEventListener("keydown", event => {
    const next = { ArrowLeft: (index + tabs.length - 1) % tabs.length, ArrowRight: (index + 1) % tabs.length,
      Home: 0, End: tabs.length - 1 }[event.key];
    if (next === undefined) return;
    event.preventDefault();
    selectTab(next);
    tabs[next].focus();
  });
});
fileInput.addEventListener("change", () => {
  const file = fileInput.files[0];
  fileInput.value = "";
  openFile(file);
});
workspace.addEventListener("dragover", event => {
  event.preventDefault();
  workspace.classList.add("dragging");
});
workspace.addEventListener("dragleave", event => {
  if (!workspace.contains(event.relatedTarget)) workspace.classList.remove("dragging");
});
workspace.addEventListener("drop", event => {
  event.preventDefault();
  workspace.classList.remove("dragging");
  openFile(event.dataTransfer.files[0]);
});
