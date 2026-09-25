(function () {
  "use strict";

  var app = document.getElementById("app");
  var errorbar = document.getElementById("errorbar");
  var errorbarText = errorbar ? errorbar.querySelector(".errorbar-text") : null;
  var help = document.getElementById("help");
  var crumbs = document.querySelector("nav.crumbs");
  var pollTimer = null;
  var runFilter = "";
  var runSelection = [];

  function el(tag, attrs, children) {
    var node = document.createElement(tag);
    if (attrs) {
      for (var key in attrs) {
        if (!Object.prototype.hasOwnProperty.call(attrs, key)) continue;
        var value = attrs[key];
        if (value === null || value === undefined) continue;
        if (key === "class") node.className = value;
        else if (key === "text") node.textContent = value;
        else if (key.slice(0, 2) === "on" && typeof value === "function") node.addEventListener(key.slice(2), value);
        else node.setAttribute(key, value);
      }
    }
    (children || []).forEach(function (child) {
      if (child === null || child === undefined || child === false) return;
      node.appendChild(typeof child === "string" || typeof child === "number" ? document.createTextNode(String(child)) : child);
    });
    return node;
  }

  function showError(message) {
    if (!errorbar || !errorbarText) return;
    errorbarText.textContent = message || "Request failed";
    errorbar.hidden = false;
  }

  function api(path, options) {
    options = options || {};
    var method = options.method || "GET";
    var headers = {};
    var init = { method: method, headers: headers };
    if (method === "PUT" || method === "DELETE") {
      headers["X-ChangeLens-Review"] = "1";
      headers["Content-Type"] = "application/json";
      if (options.body !== undefined) init.body = JSON.stringify(options.body);
    }
    return fetch(path, init).then(function (response) {
      return response.json().catch(function () { return null; }).then(function (data) {
        if (!response.ok) {
          var message = data && data.error ? data.error : ("Request failed (" + response.status + ")");
          showError(message);
          var error = new Error(message);
          error.status = response.status;
          throw error;
        }
        return data;
      });
    }).catch(function (error) {
      if (error && !error.status) showError(error.message || "Network error");
      throw error;
    });
  }

  function fmtBytes(bytes) {
    if (bytes === null || bytes === undefined) return "—";
    var value = Number(bytes);
    if (!isFinite(value)) return "—";
    if (value < 1024) return value + " B";
    var units = ["KB", "MB", "GB", "TB"];
    var index = 0;
    value = value / 1024;
    while (value >= 1024 && index < units.length - 1) { value = value / 1024; index++; }
    var rounded = value >= 10 ? Math.round(value) : Math.round(value * 10) / 10;
    return rounded + " " + units[index];
  }

  function fmtDuration(ms) {
    if (ms === null || ms === undefined) return "—";
    var total = Math.round(Number(ms) / 1000);
    if (!isFinite(total)) return "—";
    if (total < 60) return total + " s";
    var minutes = Math.floor(total / 60);
    var seconds = total % 60;
    if (minutes < 60) return minutes + " m " + seconds + " s";
    var hours = Math.floor(minutes / 60);
    return hours + " h " + (minutes % 60) + " m";
  }

  function pad2(value) {
    return value < 10 ? "0" + value : String(value);
  }

  function fmtLocalTime(iso) {
    if (!iso) return "—";
    var date = new Date(iso);
    if (isNaN(date.getTime())) return "—";
    return date.getFullYear() + "-" + pad2(date.getMonth() + 1) + "-" + pad2(date.getDate()) +
      " " + pad2(date.getHours()) + ":" + pad2(date.getMinutes());
  }

  function fmtCost(cost) {
    if (cost === null || cost === undefined) return "—";
    var value = Number(cost);
    if (!isFinite(value)) return "—";
    return "$" + value.toFixed(4);
  }

  function fmtInt(value) {
    if (value === null || value === undefined) return "—";
    var number = Number(value);
    if (!isFinite(number)) return "—";
    return number.toLocaleString("en-US");
  }

  function fmtMs(ms) {
    if (ms === null || ms === undefined) return "—";
    var value = Number(ms);
    if (!isFinite(value)) return "—";
    return fmtDuration(value);
  }

  function chip(status) {
    if (status === null || status === undefined || status === "") {
      return el("span", { class: "chip chip-todo", text: "—" });
    }
    return el("span", { class: "chip chip-" + status, text: String(status) });
  }

  function verdictChip(verdict) {
    var name = verdict && typeof verdict === "object" ? verdict.verdict : verdict;
    if (!name) return el("span", { class: "chip chip-todo", text: "to judge", "data-tip": GLOSSARY.toJudge.tip });
    return el("span", { class: "chip chip-" + name, text: String(name) });
  }

  function tallyEl(passed, scored) {
    var modifier = " tally-none";
    if (scored > 0 && passed >= scored) modifier = " tally-all";
    else if (passed > 0) modifier = " tally-some";
    return el("span", { class: "tally" + modifier, text: passed + "/" + scored });
  }

  var GLOSSARY = {
    run: {
      term: "Run",
      tip: "One execution of a review plan: every case in the plan, run against a fresh engine build.",
      body: "A run executes every case of one plan against an engine built from the working tree. It gets its own folder, named after the time it started and the plan id, for example 20260924T142524Z-quality-benchmark."
    },
    plan: {
      term: "Plan",
      tip: "A Markdown file listing the cases to run and what to check in each.",
      body: "A plan lives in docs/evaluation/review-plans/. It lists cases, and for each case the repository to review, the steps to send to the engine, and the checks to apply. Its prose also describes what to look for in each case; the case screen shows that text as plan notes."
    },
    case: {
      term: "Case",
      tip: "One code change the engine is asked to explain, with its own checks.",
      body: "A case names one change (a fixture repository, or a public repository cloned at a base and head commit), the steps to run, its hard checks and its soft checks. Its results are in cases/<case>/."
    },
    repeat: {
      term: "Repeat",
      tip: "The same case run again from scratch. The AI's wording varies, so a case can run several times.",
      body: "The model's explanation changes from one attempt to the next, so a case can say repeat: 3. Each repeat gets its own repository copy, engine and recorded AI answers, under cases/<case>/repeats/<n>/. Judge the case by looking across its repeats."
    },
    status: {
      term: "Status",
      tip: "pass, fail, error or skipped. It comes only from the hard checks.",
      body: "pass: every hard check held. fail: at least one hard check did not hold (for a repeated case, in any repeat). error: the review tool itself broke, for example a build or a fixture failed. skipped: the case did not run. Soft checks never change the status."
    },
    hardCheck: {
      term: "Hard check",
      tip: "A must-hold expectation (the plan's expect list). If one fails, the case fails.",
      body: "Hard checks are the expect list of a case: facts that must be true, such as \"the analysis completes\", \"every citation points at real evidence\" or \"the repository is left untouched\". They decide the case status."
    },
    softCheck: {
      term: "Soft check",
      tip: "A quality hint (the plan's judge list). It is counted, but never fails the case.",
      body: "Soft checks are the judge list of a case. They look for signs of a good explanation: should_link (two files are explained in the same section), should_flag_related (a file is mentioned as related), should_not_flag (a decoy file is left out). Each repeat passes or misses them; the result is a tally."
    },
    tally: {
      term: "Tally",
      tip: "How many repeats passed a soft check, out of those that scored it, e.g. 2/3.",
      body: "2/3 means two of three repeats passed that soft check. Green: all passed. Red: none passed. Amber: some did."
    },
    verdict: {
      term: "Verdict",
      tip: "Your own judgement of the explanation: good, weak or wrong.",
      body: "good: true, and a reviewer would understand the change. weak: nothing false, but it misses something or is clumsy. wrong: at least one statement is false. The verdict is saved as cases/<case>/verdict.json, beside the result, which is never changed."
    },
    toJudge: {
      term: "To judge",
      tip: "No verdict has been recorded for this case yet.",
      body: "A case shows to judge until you save a verdict for it. Judged counts the cases that have one."
    },
    judged: {
      term: "Judged",
      tip: "Cases with a saved verdict, out of all cases in the run.",
      body: "4/9 means you have recorded a verdict for four of the run's nine cases."
    },
    explanation: {
      term: "Explanation",
      tip: "What ChangeLens published about the change: a thesis, areas, and the evidence behind them.",
      body: "The engine publishes a reading model: a one-paragraph thesis, then areas that group related parts of the change. Every sentence can cite evidence: exact line ranges of old or new code."
    },
    thesis: {
      term: "Thesis",
      tip: "The one-paragraph summary of what the change does.",
      body: "The thesis is the first thing a reviewer reads. It should be true and name the change's real purpose."
    },
    area: {
      term: "Area",
      tip: "A section of the explanation that groups related parts of the change.",
      body: "Each area has a title, a summary and, depending on its shape, numbered steps, participants (the code elements involved), relationships between them, and purposes."
    },
    claim: {
      term: "Claim and sources",
      tip: "Click a sentence to highlight the code lines it cites in the diff.",
      body: "Each sentence that cites evidence is a button. ▸ 3 sources means it cites three line ranges. Clicking it highlights them in yellow in the diff and lists each source: old code or new code, file and lines, and the quoted text."
    },
    limitation: {
      term: "Could not read",
      tip: "Files or facts ChangeLens says it did not look at, so it cannot vouch for them.",
      body: "Limitations list what the engine could not read, such as a file larger than the configured bound or binary content. A thesis built around a file it never read deserves suspicion."
    },
    assurance: {
      term: "Assurances",
      tip: "Things the engine did not do, stated plainly, such as \"claim checking was not run\".",
      body: "Assurances are the engine's own caveats about how far to trust the explanation, for example that tests or the build were not executed."
    },
    removal: {
      term: "Removed by validation",
      tip: "Statements the engine dropped before publishing because their evidence did not hold up.",
      body: "Before publishing, the engine validates every statement against its evidence. Statements that fail are removed and listed here with the reason."
    },
    interruption: {
      term: "Interrupted",
      tip: "The repeat stopped early, for example at its deadline or because the engine exited.",
      body: "An interrupted repeat is shown in red. It usually published no explanation; the reason is in its tooltip and on the case screen."
    },
    heavy: {
      term: "Heavy output",
      tip: "Fixtures, databases and build output of a run. Safe to delete; results stay.",
      body: "heavy/ holds large working files. It is deleted after a run unless --keep was used. Clearing it keeps every result, verdict and log."
    },
    size: {
      term: "Size",
      tip: "Disk space the run folder uses, heavy output included.",
      body: "Deleting a run removes its whole folder, including its verdicts. Clearing heavy output only removes heavy/."
    },
    incomplete: {
      term: "Incomplete",
      tip: "The run has not finished: it is still running, or it was stopped.",
      body: "A run is complete once every case has a result. An incomplete run's screen reloads every 10 seconds, so you can watch it."
    },
    calls: {
      term: "Calls",
      tip: "How many requests the engine sent to the AI provider.",
      body: "Counted over all cases and repeats. Replay runs answer from a recording instead of calling the provider."
    },
    tokens: {
      term: "Tokens",
      tip: "The size of the AI requests and answers, as the provider reported it.",
      body: "Prompt plus completion tokens over all calls. When a provider leaves a count out, the tool estimates one token per four characters."
    },
    cost: {
      term: "Cost",
      tip: "What the AI provider charged, in US dollars. Empty when no call reported a cost.",
      body: "Only costs the provider reported are counted."
    },
    latency: {
      term: "Latency",
      tip: "Time spent waiting for the AI provider's answers.",
      body: "The sum of the provider response times of a repeat's calls."
    },
    stages: {
      term: "Stages",
      tip: "How long each engine stage took, in order: capturing, discovering, collecting.",
      body: "The engine works through its stages in order: capturing, then discovering, then collecting. Collecting is usually the longest, because it includes waiting for the AI's answers."
    },
    engine: {
      term: "Engine commit",
      tip: "The ChangeLens commit the engine was built from. dirty: it had uncommitted changes.",
      body: "Every run builds the engine from the working tree once and records the commit. dirty means uncommitted changes were included, so the commit alone does not reproduce the run."
    },
    compare: {
      term: "Compare",
      tip: "Line up two runs case by case: status, checks, cost, timings and verdicts.",
      body: "Tick exactly two runs on the runs screen. The comparison reads A -> B, for example fail -> pass."
    },
    planNotes: {
      term: "Plan notes",
      tip: "What the plan says to look for in this case.",
      body: "Taken from the case's bullet in the plan's prose. Use it to know what a good explanation should get right."
    },
    diff: {
      term: "Diff",
      tip: "The real code change: removed lines in red, added lines in green.",
      body: "Big or generated files (lock files, bundles, minified files, more than 400 changed lines) start collapsed. Files the explanation never cites can be hidden with Cited files only."
    },
    notRead: {
      term: "Not read by ChangeLens",
      tip: "The engine did not read this file, so nothing it says about it is backed by the file's text.",
      body: "Usually the file is larger than the configured bound, or it is binary."
    },
    rawFiles: {
      term: "engine.log and protocol.ndjson",
      tip: "The engine's own log, and every request and response between the tool and the engine.",
      body: "Open them when a repeat misbehaves: engine.log for errors, protocol.ndjson for exactly what was asked and answered."
    }
  };

  function tip(key) {
    var entry = GLOSSARY[key];
    if (!entry) return null;
    return el("a", {
      class: "tip",
      href: "#/info?term=" + encodeURIComponent(key),
      "data-tip": entry.tip,
      "aria-label": entry.term + ": " + entry.tip
    }, ["?"]);
  }

  function withTip(text, key) {
    return el("span", { class: "with-tip" }, [text, tip(key)]);
  }

  var tooltipBox = null;

  function showTooltip(target) {
    if (!tooltipBox) {
      tooltipBox = el("div", { class: "tooltip", role: "tooltip", hidden: "" });
      document.body.appendChild(tooltipBox);
    }
    tooltipBox.textContent = target.getAttribute("data-tip") || "";
    tooltipBox.hidden = false;
    var box = target.getBoundingClientRect();
    var width = tooltipBox.offsetWidth;
    var height = tooltipBox.offsetHeight;
    var left = Math.min(Math.max(8, box.left + box.width / 2 - width / 2), window.innerWidth - width - 8);
    var top = box.bottom + 6;
    if (top + height > window.innerHeight - 8) top = box.top - height - 6;
    tooltipBox.style.left = left + "px";
    tooltipBox.style.top = Math.max(8, top) + "px";
  }

  function hideTooltip() {
    if (tooltipBox) tooltipBox.hidden = true;
  }

  function tipTarget(event) {
    var node = event.target;
    return node && node.closest ? node.closest("[data-tip]") : null;
  }

  document.addEventListener("mouseover", function (event) {
    var target = tipTarget(event);
    if (target) showTooltip(target);
  });
  document.addEventListener("mouseout", function (event) {
    if (tipTarget(event)) hideTooltip();
  });
  document.addEventListener("focusin", function (event) {
    var target = tipTarget(event);
    if (target) showTooltip(target);
    else hideTooltip();
  });
  document.addEventListener("focusout", hideTooltip);
  window.addEventListener("scroll", hideTooltip, true);
  window.addEventListener("hashchange", hideTooltip);

  function decodeSegment(segment) {
    try { return decodeURIComponent(segment); } catch (error) { return segment; }
  }

  function parseQuery(query) {
    var out = {};
    if (!query) return out;
    query.split("&").forEach(function (pair) {
      if (!pair) return;
      var index = pair.indexOf("=");
      var key = index < 0 ? pair : pair.slice(0, index);
      var raw = index < 0 ? "" : pair.slice(index + 1);
      try { out[key] = decodeURIComponent(raw.replace(/\+/g, " ")); } catch (error) { out[key] = raw; }
    });
    return out;
  }

  function parseRoute() {
    var hash = location.hash || "";
    if (hash.charAt(0) === "#") hash = hash.slice(1);
    var query = "";
    var question = hash.indexOf("?");
    if (question >= 0) { query = hash.slice(question + 1); hash = hash.slice(0, question); }
    var parts = hash.split("/").filter(function (segment) { return segment !== ""; });
    if (parts[0] === "info") return { screen: "info", term: parseQuery(query).term || null };
    if (parts[0] === "compare") {
      var params = parseQuery(query);
      if (params.a && params.b) return { screen: "compare", a: params.a, b: params.b };
      return { screen: "runs" };
    }
    if (parts[0] !== "runs") return { screen: "runs" };
    if (parts.length === 1) return { screen: "runs" };
    if (parts.length === 2) return { screen: "run", run: decodeSegment(parts[1]) };
    var repeatParam = parseQuery(query).repeat;
    var repeat = repeatParam !== undefined && repeatParam !== "" ? Number(repeatParam) : null;
    return { screen: "case", run: decodeSegment(parts[1]), case: decodeSegment(parts[2]), repeat: repeat };
  }

  function runHref(runId) {
    return "#/runs/" + encodeURIComponent(runId);
  }

  function caseHref(runId, caseId) {
    return "#/runs/" + encodeURIComponent(runId) + "/" + encodeURIComponent(caseId);
  }

  function compareHref(a, b) {
    return "#/compare?a=" + encodeURIComponent(a) + "&b=" + encodeURIComponent(b);
  }

  function setCrumbs(parts) {
    if (!crumbs) return;
    crumbs.textContent = "";
    (parts || []).forEach(function (part, index) {
      if (index > 0) crumbs.appendChild(el("span", { class: "sep", text: "/" }));
      if (part[1]) crumbs.appendChild(el("a", { href: part[1], text: part[0] }));
      else crumbs.appendChild(el("span", { text: part[0] }));
    });
  }

  function setProgress(text) {
    var progress = document.querySelector(".topbar-progress");
    if (progress) progress.textContent = text || "";
  }

  function clearPoll() {
    if (pollTimer !== null) { clearTimeout(pollTimer); pollTimer = null; }
  }

  function schedulePoll(callback) {
    clearPoll();
    pollTimer = setTimeout(callback, 10000);
  }

  function dispatch() {
    clearPoll();
    setProgress("");
    var route = parseRoute();
    if (route.screen === "run") renderRun(route);
    else if (route.screen === "case") renderCase(route);
    else if (route.screen === "compare") renderCompare(route);
    else if (route.screen === "info") renderInfo(route);
    else renderRuns();
  }

  function statusChips(counts) {
    var order = ["pass", "fail", "error", "skipped"];
    var box = el("span", { class: "chips" });
    var any = false;
    order.forEach(function (name) {
      var count = counts && counts[name];
      if (count === null || count === undefined || count === 0) return;
      any = true;
      box.appendChild(el("span", { class: "chip chip-" + name, text: count + " " + name }));
    });
    if (!any) box.appendChild(el("span", { class: "muted", text: "—" }));
    return box;
  }

  function runsHrefForSelection() {
    if (runSelection.length !== 2) return null;
    return compareHref(runSelection[0], runSelection[1]);
  }

  function toggleSelection(runId, checked) {
    var index = runSelection.indexOf(runId);
    if (checked && index < 0) runSelection.push(runId);
    else if (!checked && index >= 0) runSelection.splice(index, 1);
  }

  function clearHeavy(run, rerender) {
    var message = "Clear heavy files for " + run.runId + " (" + fmtBytes(run.sizeBytes) + ")?";
    if (!window.confirm(message)) return;
    api("/api/runs/" + encodeURIComponent(run.runId) + "/heavy", { method: "DELETE" }).then(function () {
      rerender();
    }).catch(function () {});
  }

  function deleteRun(run, rerender) {
    var message = "Delete run " + run.runId + " (" + fmtBytes(run.sizeBytes) + ")? This cannot be undone.";
    if (run.complete !== true) message += " This run is incomplete — it may still be running.";
    if (!window.confirm(message)) return;
    api("/api/runs/" + encodeURIComponent(run.runId), { method: "DELETE" }).then(function () {
      var index = runSelection.indexOf(run.runId);
      if (index >= 0) runSelection.splice(index, 1);
      rerender();
    }).catch(function () {});
  }

  var RUNS_SORT_KEY = "changelens-review:runs-sort";

  var RUN_COLUMNS = [
    { key: "runId", label: "Run", type: "text", tipKey: "run" },
    { key: "planId", label: "Plan", type: "text", tipKey: "plan" },
    { key: "startedAt", label: "Started", type: "date" },
    { key: "durationMs", label: "Duration", type: "number" },
    { key: "calls", label: "Calls", type: "number", align: "right", tipKey: "calls" },
    { key: "tokens", label: "Tokens", type: "number", align: "right", tipKey: "tokens" },
    { key: "cost", label: "Cost", type: "number", align: "right", tipKey: "cost" },
    { key: "judgedRatio", label: "Judged", type: "number", align: "right", tipKey: "judged" },
    { key: "sizeBytes", label: "Size", type: "number", align: "right", tipKey: "size" }
  ];

  var runSort = null;

  function findRunColumn(key) {
    for (var i = 0; i < RUN_COLUMNS.length; i++) {
      if (RUN_COLUMNS[i].key === key) return RUN_COLUMNS[i];
    }
    return null;
  }

  function readRunSort() {
    if (runSort) return runSort;
    var fallback = { key: "startedAt", dir: "desc" };
    try {
      var raw = localStorage.getItem(RUNS_SORT_KEY);
      if (raw) {
        var parsed = JSON.parse(raw);
        if (parsed && typeof parsed === "object" && typeof parsed.key === "string" &&
            (parsed.dir === "asc" || parsed.dir === "desc") && findRunColumn(parsed.key)) {
          runSort = { key: parsed.key, dir: parsed.dir };
          return runSort;
        }
      }
    } catch (error) {}
    runSort = fallback;
    return runSort;
  }

  function writeRunSort(value) {
    runSort = value;
    try { localStorage.setItem(RUNS_SORT_KEY, JSON.stringify(value)); } catch (error) {}
  }

  function runDurationMs(run) {
    if (!run || !run.finishedAt || !run.startedAt) return null;
    var value = new Date(run.finishedAt).getTime() - new Date(run.startedAt).getTime();
    return isFinite(value) ? value : null;
  }

  function runSortValue(run, key) {
    var provider = run.provider || {};
    if (key === "runId") return run.runId || null;
    if (key === "planId") return run.planId || null;
    if (key === "startedAt") {
      if (!run.startedAt) return null;
      var started = new Date(run.startedAt).getTime();
      return isFinite(started) ? started : null;
    }
    if (key === "durationMs") return runDurationMs(run);
    if (key === "calls") return provider.calls === null || provider.calls === undefined ? null : Number(provider.calls);
    if (key === "tokens") return provider.totalTokens === null || provider.totalTokens === undefined ? null : Number(provider.totalTokens);
    if (key === "cost") return provider.cost === null || provider.cost === undefined ? null : Number(provider.cost);
    if (key === "sizeBytes") return run.sizeBytes === null || run.sizeBytes === undefined ? null : Number(run.sizeBytes);
    if (key === "judgedRatio") {
      if (run.cases === null || run.cases === undefined || run.judged === null || run.judged === undefined) return null;
      var total = Number(run.cases);
      if (!isFinite(total) || total === 0) return null;
      return Number(run.judged) / total;
    }
    return null;
  }

  function compareRunValues(a, b, type) {
    if (type === "text") {
      var left = String(a).toLowerCase();
      var right = String(b).toLowerCase();
      if (left < right) return -1;
      if (left > right) return 1;
      return 0;
    }
    if (a < b) return -1;
    if (a > b) return 1;
    return 0;
  }

  function sortRuns(runs) {
    var sort = readRunSort();
    var column = findRunColumn(sort.key) || RUN_COLUMNS[2];
    var direction = sort.dir === "asc" ? 1 : -1;
    var decorated = runs.map(function (run, index) {
      return { run: run, index: index, value: runSortValue(run, column.key) };
    });
    decorated.sort(function (x, y) {
      var xNull = x.value === null;
      var yNull = y.value === null;
      if (xNull || yNull) {
        if (xNull && yNull) return x.index - y.index;
        return xNull ? 1 : -1;
      }
      var base = compareRunValues(x.value, y.value, column.type);
      if (base !== 0) return base * direction;
      return x.index - y.index;
    });
    return decorated.map(function (entry) { return entry.run; });
  }

  function runSortHeader(column, sort, onSort) {
    var button = el("button", { class: "sort", type: "button" });
    button.appendChild(document.createTextNode(column.label));
    if (sort.key === column.key) {
      button.appendChild(el("span", { class: "sort-dir", text: sort.dir === "asc" ? "▲" : "▼" }));
    }
    button.addEventListener("click", function () { onSort(column.key); });
    var attrs = column.align === "right" ? { class: "right" } : null;
    var th = el("th", attrs, [button, column.tipKey ? tip(column.tipKey) : null]);
    if (sort.key === column.key) th.setAttribute("aria-sort", sort.dir === "asc" ? "ascending" : "descending");
    return th;
  }

  function paintRuns(runs) {
    app.textContent = "";
    var rerender = renderRuns;

    var filter = el("input", { class: "filter", type: "search", placeholder: "Filter by plan" });
    filter.value = runFilter;
    filter.addEventListener("input", function () { runFilter = filter.value; paintRunsTable(runs, rerender); });

    var compareBtn = el("button", { class: "btn btn-primary", id: "compare-btn", text: "Compare" });
    compareBtn.disabled = runSelection.length !== 2;
    compareBtn.addEventListener("click", function () {
      var href = runsHrefForSelection();
      if (href) location.hash = href;
    });

    app.appendChild(el("div", { class: "toolbar" }, [filter, compareBtn]));
    app.appendChild(el("div", { id: "runs-table" }));
    paintRunsTable(runs, rerender);
  }

  function paintRunsTable(runs, rerender) {
    var holder = document.getElementById("runs-table");
    var compareBtn = document.getElementById("compare-btn");
    if (!holder) return;
    holder.textContent = "";

    var needle = runFilter.trim().toLowerCase();
    var visible = runs.filter(function (run) {
      if (!needle) return true;
      return String(run.planId || "").toLowerCase().indexOf(needle) >= 0;
    });

    function onSort(key) {
      var column = findRunColumn(key);
      if (!column) return;
      var current = readRunSort();
      var dir;
      if (current.key === key) dir = current.dir === "asc" ? "desc" : "asc";
      else dir = column.type === "text" ? "asc" : "desc";
      writeRunSort({ key: key, dir: dir });
      paintRunsTable(runs, rerender);
    }

    var sort = readRunSort();
    var head = el("thead", null, [el("tr", null, [
      el("th", null, [""]),
      runSortHeader(findRunColumn("runId"), sort, onSort),
      runSortHeader(findRunColumn("planId"), sort, onSort),
      runSortHeader(findRunColumn("startedAt"), sort, onSort),
      runSortHeader(findRunColumn("durationMs"), sort, onSort),
      el("th", null, ["Status", tip("status")]),
      runSortHeader(findRunColumn("calls"), sort, onSort),
      runSortHeader(findRunColumn("tokens"), sort, onSort),
      runSortHeader(findRunColumn("cost"), sort, onSort),
      runSortHeader(findRunColumn("judgedRatio"), sort, onSort),
      runSortHeader(findRunColumn("sizeBytes"), sort, onSort),
      el("th", null, ["Actions", tip("heavy")])
    ])]);

    var body = el("tbody");
    sortRuns(visible).forEach(function (run) {
      var tick = el("input", { type: "checkbox" });
      tick.checked = runSelection.indexOf(run.runId) >= 0;
      tick.setAttribute("aria-label", "Select " + run.runId);
      tick.addEventListener("change", function () {
        toggleSelection(run.runId, tick.checked);
        if (compareBtn) compareBtn.disabled = runSelection.length !== 2;
      });

      var statusCell = el("td", { class: "chips" }, [statusChips(run.counts)]);
      if (run.complete !== true) statusCell.appendChild(el("span", { class: "chip chip-incomplete", text: "incomplete", "data-tip": GLOSSARY.incomplete.tip }));

      var actions = el("td", { class: "actions" });
      if (run.hasHeavy) {
        actions.appendChild(el("button", { class: "btn", text: "Clear heavy", onclick: function () { clearHeavy(run, rerender); } }));
      }
      actions.appendChild(el("button", { class: "btn btn-danger", text: "Delete", onclick: function () { deleteRun(run, rerender); } }));

      var provider = run.provider || {};
      var duration = runDurationMs(run);
      body.appendChild(el("tr", null, [
        el("td", null, [tick]),
        el("td", null, [el("a", { href: runHref(run.runId), text: run.runId })]),
        el("td", { text: run.planId || "—" }),
        el("td", { text: fmtLocalTime(run.startedAt) }),
        el("td", { class: "num", text: duration === null ? "—" : fmtDuration(duration) }),
        statusCell,
        el("td", { class: "right num", text: fmtInt(provider.calls) }),
        el("td", { class: "right num", text: fmtInt(provider.totalTokens) }),
        el("td", { class: "right num", text: fmtCost(provider.cost) }),
        el("td", { class: "right num", text: fmtInt(run.judged) + "/" + fmtInt(run.cases) }),
        el("td", { class: "right num", text: fmtBytes(run.sizeBytes) }),
        actions
      ]));
    });

    var table = el("table", { class: "grid runs" }, [head, body]);
    holder.appendChild(el("div", { class: "table-wrap" }, [table]));

    var total = visible.reduce(function (sum, run) {
      return sum + (run.sizeBytes === null || run.sizeBytes === undefined ? 0 : Number(run.sizeBytes));
    }, 0);
    holder.appendChild(el("p", { class: "footer", text: "Total on disk: " + fmtBytes(total) }));
  }

  function renderRuns() {
    setCrumbs([["Runs", "#/runs"]]);
    app.replaceChildren(el("p", { class: "empty", text: "Loading runs…" }));
    api("/api/runs").then(function (runs) {
      paintRuns(runs || []);
    }).catch(function () {});
  }

  function factPair(list, label, value) {
    list.appendChild(el("dt", null, [label]));
    list.appendChild(el("dd", null, value instanceof Node ? [value] : [String(value)]));
  }

  function shortCommit(commit) {
    if (!commit) return "—";
    return String(commit).slice(0, 8);
  }

  function changeSummary(change) {
    if (!change) return "—";
    var files = fmtInt(change.files);
    var added = change.linesAdded === null || change.linesAdded === undefined ? "—" : "+" + fmtInt(change.linesAdded);
    var removed = change.linesDeleted === null || change.linesDeleted === undefined ? "—" : "−" + fmtInt(change.linesDeleted);
    return files + " files, " + added + " " + removed;
  }

  function repeatCellDiv(attempt) {
    if (!attempt) return el("div", { class: "repeat-cell empty" });
    var interrupted = !!attempt.interruption;
    var bad = interrupted || attempt.status === "fail" || attempt.status === "error";
    var cell = el("div", { class: "repeat-cell" + (interrupted ? " interrupted" : "") });
    cell.appendChild(el("div", { class: "repeat-line" }, [
      el("span", { class: "dot " + (bad ? "dot-bad" : "dot-ok") }),
      el("span", { class: "repeat-status", text: attempt.status || "—" })
    ]));
    var meta = interrupted ? "interrupted" : (fmtMs(attempt.latencyMs) + " · " + fmtCost(attempt.cost));
    cell.appendChild(el("span", { class: "repeat-meta muted", text: meta }));
    if (interrupted) cell.setAttribute("data-tip", "Interrupted: " + attempt.interruption);
    return cell;
  }

  function repeatCells(attempts) {
    return (attempts || []).map(function (attempt) { return repeatCellDiv(attempt); });
  }

  function tallyCells(judgeTally) {
    return (judgeTally || []).map(function (tally) {
      return el("li", null, [
        tallyEl(tally.passed, tally.scored),
        el("span", { class: "check-label", text: caseSoftLabel(tally) })
      ]);
    });
  }

  function caseCost(item) {
    var attempts = item.attempts || [];
    var total = null;
    attempts.forEach(function (attempt) {
      if (!attempt) return;
      var value = attempt.cost;
      if (value === null || value === undefined) return;
      var number = Number(value);
      if (!isFinite(number)) return;
      total = (total === null ? 0 : total) + number;
    });
    return total;
  }

  function renderRun(route, polling) {
    var runId = route.run;
    setCrumbs([["Runs", "#/runs"], [runId, null]]);
    if (!polling) app.replaceChildren(el("p", { class: "empty", text: "Loading run…" }));

    api("/api/runs/" + encodeURIComponent(runId)).then(function (run) {
      app.textContent = "";

      var titleRow = el("div", { class: "run-title" }, [el("h2", { text: run.planId || run.runId })]);
      if (run.complete !== true) {
        var incomplete = el("span", { class: "chip chip-incomplete", text: "incomplete" });
        incomplete.setAttribute("data-tip", GLOSSARY.incomplete.tip);
        titleRow.appendChild(incomplete);
      }
      var header = el("section", { class: "panel run-header" }, [titleRow]);
      var facts = el("dl", { class: "facts" });
      factPair(facts, "Run", run.runId);
      factPair(facts, "Started", fmtLocalTime(run.startedAt));
      factPair(facts, "Finished", fmtLocalTime(run.finishedAt));
      var engine = run.engine || {};
      factPair(facts, withTip("Engine", "engine"), shortCommit(engine.commit) + (engine.dirty ? " (dirty)" : ""));
      factPair(facts, "Tool", run.toolVersion || "—");
      var totals = run.totals || {};
      var caseCount = totals.cases === null || totals.cases === undefined ? (run.cases || []).length : totals.cases;
      factPair(facts, withTip("Cases", "case"), fmtInt(caseCount));
      factPair(facts, withTip("Calls", "calls"), fmtInt(totals.calls));
      factPair(facts, withTip("Tokens", "tokens"), fmtInt(totals.totalTokens));
      factPair(facts, withTip("Cost", "cost"), fmtCost(totals.cost));
      factPair(facts, withTip("Judged", "judged"), fmtInt(run.judged) + " of " + fmtInt(caseCount));
      factPair(facts, "Change", changeSummary(totals.change));

      var firstUnjudged = (run.cases || []).filter(function (item) { return !item.verdict; })[0];
      var judgeBtn = el("button", { class: "btn btn-primary", id: "judge-next", text: "Judge next" });
      if (!firstUnjudged) judgeBtn.disabled = true;
      judgeBtn.addEventListener("click", function () {
        if (firstUnjudged) location.hash = caseHref(runId, firstUnjudged.caseId);
      });

      header.appendChild(facts);
      header.appendChild(judgeBtn);
      app.appendChild(header);

      var maxAttempts = 0;
      var anyRepeat = false;
      (run.cases || []).forEach(function (item) {
        var attempts = item.attempts || [];
        if (attempts.length > maxAttempts) maxAttempts = attempts.length;
        attempts.forEach(function (attempt) {
          if (attempt && attempt.repeat !== null && attempt.repeat !== undefined) anyRepeat = true;
        });
      });
      var repeatColumns = [];
      if (anyRepeat) {
        for (var index = 0; index < maxAttempts; index++) repeatColumns.push(index);
      } else {
        repeatColumns.push(0);
      }

      var headCells = [
        el("th", null, ["Case"]),
        el("th", null, ["Status", tip("status")]),
        el("th", null, ["Verdict", tip("verdict")]),
        el("th", null, ["Soft checks", tip("softCheck")])
      ];
      repeatColumns.forEach(function (columnIndex, position) {
        var label = anyRepeat ? "Repeat " + (columnIndex + 1) : "Result";
        headCells.push(el("th", null, [label, position === 0 ? tip("repeat") : null]));
      });
      headCells.push(el("th", { class: "right" }, ["Cost", tip("cost")]));

      var head = el("thead", null, [el("tr", null, headCells)]);
      var body = el("tbody");
      (run.cases || []).forEach(function (item) {
        var checks = tallyCells(item.judgeTally);
        var softCell = el("td", null, [
          checks.length ? el("ul", { class: "check-list" }, checks) : el("span", { class: "muted", text: "—" })
        ]);
        var row = el("tr", null, [
          el("td", { class: "case-id" }, [el("a", { href: caseHref(runId, item.caseId), text: item.caseId })]),
          el("td", null, [chip(item.status)]),
          el("td", null, [verdictChip(item.verdict)]),
          softCell
        ]);
        var cellDivs = repeatCells(item.attempts);
        repeatColumns.forEach(function (columnIndex) {
          row.appendChild(el("td", { class: "repeat-col" }, [cellDivs[columnIndex]]));
        });
        var cost = caseCost(item);
        row.appendChild(el("td", { class: "right num", text: cost === null ? "—" : fmtCost(cost) }));
        body.appendChild(row);
      });

      app.appendChild(el("div", { class: "table-wrap" }, [
        el("table", { class: "grid cases" }, [head, body])
      ]));

      if (run.complete !== true) schedulePoll(function () { renderRun(route, true); });
    }).catch(function () {});
  }


  function findCasePrefix(text, caseIds) {
    var match = null;
    caseIds.forEach(function (caseId) {
      if (match) return;
      if (!caseId) return;
      if (text === caseId) { match = caseId; return; }
      if (text.indexOf(caseId) === 0 && !/[A-Za-z0-9_-]/.test(text.charAt(caseId.length))) match = caseId;
    });
    return match;
  }

  function compareLines(lines, caseIds, runB) {
    var pre = el("pre", { class: "compare-lines" });
    (lines || []).forEach(function (line, index) {
      var trimmed = String(line).replace(/^\s+/, "");
      var indent = String(line).slice(0, String(line).length - trimmed.length);
      if (indent) pre.appendChild(document.createTextNode(indent));
      var lead = trimmed.indexOf("case ") === 0 ? "case " : "";
      if (lead) { pre.appendChild(document.createTextNode(lead)); trimmed = trimmed.slice(lead.length); }
      var match = findCasePrefix(trimmed, caseIds);
      if (match) {
        pre.appendChild(el("a", { href: caseHref(runB, match), text: match }));
        pre.appendChild(document.createTextNode(trimmed.slice(match.length)));
      } else {
        pre.appendChild(document.createTextNode(trimmed));
      }
      if (index < lines.length - 1) pre.appendChild(document.createTextNode("\n"));
    });
    return pre;
  }

  function renderCompare(route) {
    var a = route.a;
    var b = route.b;
    setCrumbs([["Runs", "#/runs"], ["Compare", null]]);
    app.replaceChildren(el("p", { class: "empty", text: "Loading comparison…" }));

    Promise.all([
      api("/api/compare?a=" + encodeURIComponent(a) + "&b=" + encodeURIComponent(b)),
      api("/api/runs/" + encodeURIComponent(b))
    ]).then(function (results) {
      var data = results[0] || {};
      var runB = results[1] || {};
      var caseIds = (runB.cases || []).map(function (item) { return item.caseId; });
      app.textContent = "";
      app.appendChild(el("section", { class: "panel compare-head" }, [
        el("span", { class: "label", text: "A" }),
        el("span", { class: "num", text: a }),
        el("span", { class: "label", text: "B" }),
        el("span", { class: "num", text: b })
      ]));
      app.appendChild(el("section", { class: "panel" }, [
        compareLines(data.lines || [], caseIds, b)
      ]));
    }).catch(function () {});
  }

  function parsePatch(text) {
    var files = [];
    var current = null;
    var oldNo = 0;
    var newNo = 0;
    var oldLeft = 0;
    var newLeft = 0;
    var lines = String(text == null ? "" : text).split("\n");

    for (var i = 0; i < lines.length; i++) {
      var line = lines[i];
      if (line.charAt(line.length - 1) === "\r") line = line.slice(0, -1);

      if (line.indexOf("diff --git ") === 0) {
        var m = /^diff --git a\/(.+) b\/(.+)$/.exec(line);
        var oldPath = m ? m[1] : null;
        var newPath = m ? m[2] : null;
        current = {
          oldPath: oldPath,
          newPath: newPath,
          path: newPath || oldPath || line,
          adds: 0,
          dels: 0,
          tags: [],
          binary: false,
          hunks: []
        };
        files.push(current);
        oldNo = 0;
        newNo = 0;
        continue;
      }

      if (!current) continue;

      if (oldLeft > 0 || newLeft > 0) {
        var bodyMarker = line.charAt(0);
        var bodyText = line.slice(1);
        var bodyHunk = current.hunks[current.hunks.length - 1];
        if (bodyMarker === "+") {
          bodyHunk.lines.push({ kind: "add", oldNo: null, newNo: newNo, text: bodyText });
          newNo++; newLeft--; current.adds++;
          continue;
        }
        if (bodyMarker === "-") {
          bodyHunk.lines.push({ kind: "del", oldNo: oldNo, newNo: null, text: bodyText });
          oldNo++; oldLeft--; current.dels++;
          continue;
        }
        if (bodyMarker === " " || line === "") {
          bodyHunk.lines.push({ kind: "ctx", oldNo: oldNo, newNo: newNo, text: bodyText });
          oldNo++; newNo++; oldLeft--; newLeft--;
          continue;
        }
      }

      if (line.indexOf("\\ No newline at end of file") === 0) continue;

      if (line.indexOf("@@") === 0) {
        var h = /^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@/.exec(line);
        if (h) {
          oldNo = parseInt(h[1], 10);
          newNo = parseInt(h[3], 10);
          oldLeft = h[2] === undefined ? 1 : parseInt(h[2], 10);
          newLeft = h[4] === undefined ? 1 : parseInt(h[4], 10);
        }
        current.hunks.push({ header: line, lines: [] });
        continue;
      }

      if (line.indexOf("new file mode") === 0) {
        current.tags.push("new");
        current.oldPath = null;
        continue;
      }
      if (line.indexOf("deleted file mode") === 0) {
        current.tags.push("deleted");
        current.newPath = null;
        continue;
      }
      if (line.indexOf("old mode") === 0 || line.indexOf("new mode") === 0) {
        if (current.tags.indexOf("mode change") < 0) current.tags.push("mode change");
        continue;
      }
      if (line.indexOf("rename from ") === 0) {
        current.oldPath = line.slice(12);
        current.tags.push("renamed from " + line.slice(12));
        continue;
      }
      if (line.indexOf("rename to ") === 0) {
        current.newPath = line.slice(10);
        continue;
      }
      if (line.indexOf("similarity index ") === 0 || line.indexOf("dissimilarity index ") === 0 || line.indexOf("index ") === 0) continue;

      if (line.indexOf("--- ") === 0) {
        var before = line.slice(4);
        if (before === "/dev/null") current.oldPath = null;
        else if (before.indexOf("a/") === 0) current.oldPath = before.slice(2);
        continue;
      }
      if (line.indexOf("+++ ") === 0) {
        var after = line.slice(4);
        if (after === "/dev/null") current.newPath = null;
        else if (after.indexOf("b/") === 0) current.newPath = after.slice(2);
        continue;
      }
      if (line.indexOf("Binary files ") === 0 || line.indexOf("GIT binary patch") === 0) {
        current.binary = true;
        if (current.tags.indexOf("binary") < 0) current.tags.push("binary");
        continue;
      }

      if (!current.hunks.length) continue;

      var marker = line.charAt(0);
      var body = line.slice(1);
      var hunk = current.hunks[current.hunks.length - 1];
      if (marker === "+") {
        hunk.lines.push({ kind: "add", oldNo: null, newNo: newNo, text: body });
        newNo++;
        current.adds++;
      } else if (marker === "-") {
        hunk.lines.push({ kind: "del", oldNo: oldNo, newNo: null, text: body });
        oldNo++;
        current.dels++;
      } else if (marker === " ") {
        hunk.lines.push({ kind: "ctx", oldNo: oldNo, newNo: newNo, text: body });
        oldNo++;
        newNo++;
      }
    }

    for (var j = 0; j < files.length; j++) {
      if (files[j].newPath != null) files[j].path = files[j].newPath;
      else if (files[j].oldPath != null) files[j].path = files[j].oldPath;
    }

    return files;
  }

  function isCollapsedByDefault(path, adds, dels) {
    if (adds + dels > 400) return true;
    if (/(^|\/)dist\//.test(path)) return true;
    if (/\.lock$/.test(path)) return true;
    if (/-lock\.json$/.test(path)) return true;
    if (/\.min\.js$/.test(path)) return true;
    if (/\.min\.css$/.test(path)) return true;
    if (/bundle[^/]*\.js$/.test(path)) return true;
    if (/\.snap$/.test(path)) return true;
    return false;
  }

  function createDiffView(container, files, options) {
    var opts = options || {};
    var notRead = opts.notRead || new Map();
    var citeCounts = opts.citeCounts || new Map();
    var entries = [];

    function notReadReason(path) {
      return notRead.has(path) ? notRead.get(path) : null;
    }
    function citeCount(path) {
      return citeCounts.get(path) || 0;
    }

    container.textContent = "";

    var index = el("div", { class: "file-index" });
    var checkbox = el("input", { type: "checkbox" });
    var citedOnly = el("label", { class: "cited-only" }, [checkbox, " Cited files only", tip("diff")]);
    checkbox.addEventListener("change", function () { setCitedOnly(checkbox.checked); });
    index.appendChild(citedOnly);

    var indexTable = el("table", { class: "grid file-index-table" });
    indexTable.appendChild(el("thead", null, [el("tr", null, [
      el("th", { text: "File" }),
      el("th", { class: "right", text: "+/−" }),
      el("th", { class: "right", text: "Cited" })
    ])]));
    var indexBody = el("tbody");
    indexTable.appendChild(indexBody);
    index.appendChild(indexTable);
    container.appendChild(index);

    files.forEach(function (file) {
      var defaultOpen = !isCollapsedByDefault(file.path, file.adds, file.dels);
      var entry = { file: file, defaultOpen: defaultOpen, built: false, rows: [], details: null, indexRow: null };

      var pathButton = el("button", { class: "linklike", text: file.path });
      pathButton.addEventListener("click", function () { jumpTo(entry); });

      var pathCell = el("td", null, [pathButton]);
      if (!defaultOpen) pathCell.appendChild(el("span", { class: "tag tag-collapsed", text: "collapsed", "data-tip": GLOSSARY.diff.tip }));
      var indexRow = el("tr", { "data-path": file.path }, [
        pathCell,
        el("td", { class: "right num" }, [
          el("span", { class: "adds", text: "+" + file.adds }),
          " ",
          el("span", { class: "dels", text: "−" + file.dels })
        ]),
        el("td", { class: "right num", text: citeCount(file.path) ? String(citeCount(file.path)) : "" })
      ]);
      entry.indexRow = indexRow;
      indexBody.appendChild(indexRow);

      var summary = el("summary", { class: "file-head" }, [
        el("span", { class: "file-path", text: file.path }),
        el("span", { class: "file-stats" }, [
          el("span", { class: "adds", text: "+" + file.adds }),
          el("span", { class: "dels", text: "−" + file.dels })
        ])
      ]);
      file.tags.forEach(function (tag) {
        summary.appendChild(el("span", { class: "tag", text: tag }));
      });
      var reason = notReadReason(file.path);
      if (reason !== null) {
        summary.appendChild(el("span", { class: "tag tag-notread", text: "not read by ChangeLens — " + reason, "data-tip": GLOSSARY.notRead.tip }));
      }

      var details = el("details", { class: "file", "data-path": file.path }, [summary]);
      entry.details = details;
      details.addEventListener("toggle", function () {
        if (details.open) buildRows(entry);
      });
      if (defaultOpen) details.open = true;
      container.appendChild(details);
      entries.push(entry);
    });

    function fileNote(file) {
      var parts = [];
      if (file.binary) parts.push("Binary content changed; there is no text diff to show.");
      if (file.tags.indexOf("mode change") >= 0) parts.push("Only the file's permissions changed; there are no line changes.");
      if (file.oldPath != null && file.newPath != null && file.oldPath !== file.newPath) {
        parts.push("Only the file's name changed; there are no line changes.");
      }
      if (!parts.length) parts.push("This file has no line changes to show.");
      return parts.join(" ");
    }

    function codeCell(line) {
      var text = line.text || "";
      var cut = text.length > 400;
      var cell = el("td", { class: "code" }, [cut ? text.slice(0, 400) : text]);
      if (cut) cell.appendChild(el("span", { class: "cut-marker", text: "… (cut at 400 chars)" }));
      return cell;
    }

    function buildLineRow(line) {
      var tr = el("tr", { class: line.kind });
      tr.setAttribute("data-old", line.oldNo == null ? "" : String(line.oldNo));
      tr.setAttribute("data-new", line.newNo == null ? "" : String(line.newNo));
      tr.appendChild(el("td", { class: "ln", text: line.oldNo == null ? "" : String(line.oldNo) }));
      tr.appendChild(el("td", { class: "ln", text: line.newNo == null ? "" : String(line.newNo) }));
      tr.appendChild(el("td", { class: "mk", text: line.kind === "add" ? "+" : line.kind === "del" ? "−" : " " }));
      tr.appendChild(codeCell(line));
      return { tr: tr, oldNo: line.oldNo, newNo: line.newNo };
    }

    function buildRows(entry) {
      if (entry.built) return;
      entry.built = true;
      var file = entry.file;

      if (file.binary || !file.hunks.length) {
        entry.details.appendChild(el("p", { class: "file-note", text: fileNote(file) }));
        return;
      }

      var table = el("table", { class: "hunk-table" });
      var body = el("tbody");
      file.hunks.forEach(function (hunk) {
        body.appendChild(el("tr", { class: "hunk" }, [
          el("td", { class: "hunk-head", colspan: "4", text: hunk.header })
        ]));
        hunk.lines.forEach(function (line) {
          var row = buildLineRow(line);
          body.appendChild(row.tr);
          entry.rows.push(row);
        });
      });
      table.appendChild(body);
      entry.details.appendChild(el("div", { class: "file-scroll" }, [table]));
    }

    function jumpTo(entry) {
      buildRows(entry);
      entry.details.open = true;
      entry.details.scrollIntoView({ block: "center" });
    }

    function clear() {
      var cited = container.querySelectorAll("tr.cited");
      for (var k = 0; k < cited.length; k++) cited[k].classList.remove("cited");
    }

    function setCitedOnly(flag) {
      var on = !!flag;
      entries.forEach(function (entry) {
        var hide = on && citeCount(entry.file.path) === 0;
        entry.details.hidden = hide;
        if (entry.indexRow) entry.indexRow.hidden = hide;
      });
      if (checkbox.checked !== on) checkbox.checked = on;
    }

    function highlight(evidence) {
      clear();
      var results = [];
      var first = null;
      var nodes = evidence || [];

      nodes.forEach(function (node) {
        var entry = null;
        for (var n = 0; n < entries.length; n++) {
          var file = entries[n].file;
          var match = node.side === "before" ? file.oldPath === node.path : file.newPath === node.path;
          if (match) { entry = entries[n]; break; }
        }
        if (!entry) {
          results.push({ nodeId: node.nodeId, note: "file did not change" });
          return;
        }

        buildRows(entry);
        entry.details.open = true;

        var hit = null;
        entry.rows.forEach(function (row) {
          var no = node.side === "before" ? row.oldNo : row.newNo;
          if (no == null) return;
          if (no >= node.startLine && no <= node.endLine) {
            row.tr.classList.add("cited");
            if (!hit) hit = row.tr;
          }
        });

        if (!hit) {
          results.push({ nodeId: node.nodeId, note: "outside the diff hunks" });
          return;
        }
        if (!first) first = hit;
        results.push({ nodeId: node.nodeId, note: null });
      });

      if (first) {
        var reduced = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
        first.scrollIntoView({ behavior: reduced ? "auto" : "smooth", block: "center" });
      }
      return results;
    }

    return {
      highlight: highlight,
      clear: clear,
      setCitedOnly: setCitedOnly
    };
  }

  var CASE_DRAFT_PREFIX = "changelens-review:draft:";

  var caseScreen = null;
  var caseDraft = { verdict: null, note: "" };
  var caseKeyBound = false;

  var CASE_SOFT_LABEL = {
    should_link: function (e) {
      var x = e || {};
      return "Puts " + (x.from || "?") + " and " + (x.to || "?") + " in the same section";
    },
    should_flag_related: function (e) {
      return "Mentions " + [].concat(e == null ? [] : e).join(", ") + " as related";
    },
    should_not_flag: function (e) {
      return "Leaves " + [].concat(e == null ? [] : e).join(", ") + " out";
    }
  };

  var CASE_CHOICES = [
    ["good", "Good", "True, and a reviewer would understand the change."],
    ["weak", "Weak", "Nothing false, but misses something or is clumsy."],
    ["wrong", "Wrong", "At least one statement is false."]
  ];

  function enc(s) {
    return encodeURIComponent(s);
  }

  function caseSoftLabel(t) {
    var fn = CASE_SOFT_LABEL[t && t.name];
    return fn ? fn(t.expected) : ((t && t.name) || "check");
  }

  function caseVerdictChip(v) {
    return verdictChip(v);
  }

  function caseDraftKey(run, caseId) {
    return CASE_DRAFT_PREFIX + run + ":" + caseId;
  }

  function caseReadDraft(run, caseId) {
    try {
      var raw = localStorage.getItem(caseDraftKey(run, caseId));
      if (raw) {
        var d = JSON.parse(raw);
        if (d && typeof d === "object") return { verdict: d.verdict || null, note: d.note || "" };
      }
    } catch (e) {}
    return null;
  }

  function caseWriteDraft(run, caseId, d) {
    try {
      localStorage.setItem(caseDraftKey(run, caseId), JSON.stringify({ verdict: d.verdict || null, note: d.note || "" }));
    } catch (e) {}
  }

  function caseClearDraft(run, caseId) {
    try { localStorage.removeItem(caseDraftKey(run, caseId)); } catch (e) {}
  }

  function caseEvidenceById() {
    var map = {};
    var model = caseScreen && caseScreen.attempt && caseScreen.attempt.readingModel;
    if (model && model.evidence) model.evidence.forEach(function (n) { if (n && n.nodeId) map[n.nodeId] = n; });
    return map;
  }

  function caseClaimNodes(ids) {
    var map = caseEvidenceById();
    var out = [];
    (ids || []).forEach(function (id) { if (map[id]) out.push(map[id]); });
    return out;
  }

  function caseCloseClaim() {
    var open = caseScreen && caseScreen.openClaim;
    if (!open) return;
    open.btn.setAttribute("aria-expanded", "false");
    open.box.hidden = true;
    caseScreen.openClaim = null;
    if (caseScreen.diffView) caseScreen.diffView.clear();
  }

  function casePaintEvidence(claim) {
    claim.box.textContent = "";
    var notes = {};
    if (caseScreen.diffView) {
      var results = caseScreen.diffView.highlight(claim.nodes) || [];
      results.forEach(function (r) { if (r && r.nodeId) notes[r.nodeId] = r.note; });
    }
    if (!claim.nodes.length) {
      claim.box.appendChild(el("div", { class: "evidence-item" }, [
        el("span", { class: "evidence-note", text: "No sources cited for this sentence." })
      ]));
      return;
    }
    claim.nodes.forEach(function (n) {
      var item = el("div", { class: "evidence-item" });
      item.appendChild(el("span", { class: "evidence-side", text: n.side === "before" ? "old code" : "new code" }));
      item.appendChild(el("span", { class: "evidence-loc", text: n.path + ":" + n.startLine + "\u2013" + n.endLine }));
      if (notes[n.nodeId]) item.appendChild(el("span", { class: "evidence-note", text: notes[n.nodeId] }));
      item.appendChild(el("pre", { class: "evidence-quote", text: n.text || "(no text)" }));
      claim.box.appendChild(item);
    });
  }

  function caseToggleClaim(claim) {
    if (caseScreen.openClaim === claim) {
      caseCloseClaim();
      return;
    }
    caseCloseClaim();
    caseScreen.openClaim = claim;
    claim.btn.setAttribute("aria-expanded", "true");
    claim.box.hidden = false;
    casePaintEvidence(claim);
  }

  function caseClaimParts(content, ids, cls) {
    var btn = el("button", { class: "claim" + (cls ? " " + cls : ""), "aria-expanded": "false" });
    if (typeof content === "string") btn.appendChild(document.createTextNode(content));
    else if (content) btn.appendChild(content);
    var list = ids || [];
    if (list.length) btn.appendChild(el("span", { class: "claim-hint", text: "\u25B8 " + list.length + " source" + (list.length === 1 ? "" : "s") }));
    var box = el("div", { class: "evidence", hidden: "" });
    var claim = { btn: btn, box: box, nodes: caseClaimNodes(list) };
    btn.addEventListener("click", function () { caseToggleClaim(claim); });
    return claim;
  }

  function caseClaim(content, ids, cls) {
    var c = caseClaimParts(content, ids, cls);
    return el("div", { class: "claim-wrap" }, [c.btn, c.box]);
  }

  function caseCollectClaims(model) {
    var out = [];
    function add(ids) { if (ids && ids.length) out.push(ids); }
    if (!model) return out;
    if (model.thesis) add(model.thesis.evidenceNodeIds);
    (model.areas || []).forEach(function (a) {
      if (a.summary) add(a.summary.evidenceNodeIds);
      (a.orderedSteps || []).forEach(function (s) { add(s.evidenceNodeIds); });
      (a.participants || []).forEach(function (p) { add(p.evidenceNodeIds); });
      (a.relationships || []).forEach(function (r) { add(r.evidenceNodeIds); });
      (a.purposes || []).forEach(function (p) { add(p.evidenceNodeIds); });
    });
    return out;
  }

  function caseCiteCounts(model) {
    var counts = new Map();
    var map = {};
    if (model && model.evidence) model.evidence.forEach(function (n) { if (n && n.nodeId) map[n.nodeId] = n; });
    caseCollectClaims(model).forEach(function (ids) {
      ids.forEach(function (id) {
        var n = map[id];
        if (n && n.path) counts.set(n.path, (counts.get(n.path) || 0) + 1);
      });
    });
    return counts;
  }

  function caseNotRead(model) {
    var map = new Map();
    if (model && model.limitations) {
      model.limitations.forEach(function (l) {
        if (l && l.kind === "fileNotRead" && l.path) map.set(l.path, l.detail || "not read");
      });
    }
    return map;
  }

  function caseBackticks(target, text) {
    var parts = String(text).split("`");
    parts.forEach(function (part, i) {
      if (!part) return;
      if (i % 2 === 1) target.appendChild(el("code", { text: part }));
      else target.appendChild(document.createTextNode(part));
    });
  }

  function caseRepoLine(detail) {
    var repo = detail.repository;
    if (!repo) return el("p", { class: "muted", text: "No repository recorded for this case." });
    var pin = (repo.base || "").slice(0, 8) + "\u2026" + (repo.head || "").slice(0, 8);
    return el("p", { class: "source" }, [
      document.createTextNode((repo.clone || "repository") + " "),
      el("code", { text: pin })
    ]);
  }

  function caseRenderInfo(detail) {
    var sec = el("section", { class: "panel case-info" });
    sec.appendChild(el("h2", { text: detail.caseId || caseScreen.caseId }));
    sec.appendChild(caseRepoLine(detail));
    if (detail.planNotes) {
      sec.appendChild(el("p", { class: "label" }, [withTip("Plan notes", "planNotes")]));
      var notes = el("p", { class: "plan-notes" });
      caseBackticks(notes, detail.planNotes);
      sec.appendChild(notes);
    }
    return sec;
  }

  function caseRenderChecks(detail) {
    var sec = el("section", { class: "panel case-checks" }, [
      el("p", { class: "panel-title" }, [withTip("Soft checks", "softCheck")])
    ]);
    var list = detail.judgeTally || [];
    if (!list.length) {
      sec.appendChild(el("p", { class: "empty", text: "No soft checks recorded." }));
      return sec;
    }
    var ul = el("ul", { class: "checks" });
    list.forEach(function (t) {
      ul.appendChild(el("li", null, [
        tallyEl(t.passed, t.scored),
        document.createTextNode(" " + caseSoftLabel(t))
      ]));
    });
    sec.appendChild(ul);
    return sec;
  }

  function caseAttemptLabel(attempt) {
    return attempt.repeat == null ? "Result" : "Repeat " + attempt.repeat;
  }

  function caseAttemptBad(attempt) {
    return !!(attempt && (attempt.interruption || attempt.status === "fail" || attempt.status === "error"));
  }

  function caseRawFileUrl(run, caseId, attempt, name) {
    var rep = attempt.repeat == null ? "-" : String(attempt.repeat);
    return "/api/runs/" + enc(run) + "/cases/" + enc(caseId) + "/files/" + rep + "/" + enc(name);
  }

  function caseRenderTabs() {
    var tabs = el("div", { class: "repeat-tabs", role: "group", "aria-label": "Repeat" });
    (caseScreen.detail.attempts || []).forEach(function (attempt, i) {
      var dot = el("span", { class: "dot" + (caseAttemptBad(attempt) ? " dot-bad" : " dot-ok") });
      var btn = el("button", { "aria-pressed": i === caseScreen.index ? "true" : "false" }, [
        dot,
        document.createTextNode(caseAttemptLabel(attempt))
      ]);
      if (attempt.interruption) btn.setAttribute("data-tip", "Interrupted: " + attempt.interruption);
      btn.addEventListener("click", function () { caseGoRepeat(i); });
      tabs.appendChild(btn);
    });
    tabs.appendChild(tip("repeat"));
    return tabs;
  }

  function caseRenderMeta() {
    var attempt = caseScreen.attempt;
    var meta = el("div", { class: "attempt-meta" });
    var bits = [];
    if (attempt.latencyMs != null) bits.push(fmtMs(attempt.latencyMs));
    if (attempt.tokens != null) bits.push(fmtInt(attempt.tokens) + " tokens");
    if (attempt.cost != null) bits.push(fmtCost(attempt.cost));
    if (attempt.stages) {
      Object.keys(attempt.stages).forEach(function (k) {
        if (attempt.stages[k] != null) bits.push(k + " " + fmtMs(attempt.stages[k]));
      });
    }
    var missed = (attempt.judge || []).filter(function (j) { return j && j.passed === false; }).length;
    if (missed) bits.push("missed " + missed + " soft check" + (missed === 1 ? "" : "s"));
    meta.appendChild(document.createTextNode(bits.join(" \u00b7 ") + " "));
    meta.appendChild(tip("stages"));
    (attempt.files || []).forEach(function (name) {
      meta.appendChild(document.createTextNode(" \u00b7 "));
      meta.appendChild(el("a", {
        href: caseRawFileUrl(caseScreen.run, caseScreen.caseId, attempt, name),
        target: "_blank", rel: "noopener", text: name
      }));
    });
    if ((attempt.files || []).length) {
      meta.appendChild(document.createTextNode(" "));
      meta.appendChild(tip("rawFiles"));
    }
    return meta;
  }

  function caseRenderPurpose(p) {
    return p.text || p.purpose || (p.summary && p.summary.text) || "";
  }

  function caseRenderExplanation(attempt) {
    var box = el("div");
    var model = attempt.readingModel;
    if (!model) {
      box.appendChild(el("p", {
        class: "empty",
        text: "This repeat published no explanation" + (attempt.interruption ? " \u2014 " + attempt.interruption : "")
      }));
      return box;
    }
    if (model.thesis) {
      var parts = caseClaimParts(model.thesis.text || "", model.thesis.evidenceNodeIds, "thesis");
      box.appendChild(el("p", { class: "thesis" }, [parts.btn]));
      box.appendChild(parts.box);
    }
    (model.areas || []).forEach(function (a) {
      var sec = el("section", { class: "area" });
      var head = el("h3", { class: "area-title", text: a.title || "Area" });
      if (a.shape) {
        head.appendChild(document.createTextNode(" "));
        head.appendChild(el("span", { class: "area-shape", text: a.shape }));
      }
      sec.appendChild(head);
      if (a.summary) sec.appendChild(caseClaim(a.summary.text || "", a.summary.evidenceNodeIds, null));
      if ((a.orderedSteps || []).length) {
        var ol = el("ol", { class: "steps" });
        a.orderedSteps.forEach(function (s) {
          ol.appendChild(el("li", null, [caseClaim(s.text || "", s.evidenceNodeIds, null)]));
        });
        sec.appendChild(ol);
      }
      if ((a.participants || []).length) {
        var ulp = el("ul", { class: "participants" });
        a.participants.forEach(function (p) {
          var content = el("span", null, [
            el("code", { text: p.name || "" }),
            p.changed ? el("span", { class: "tag tag-changed", text: "changed" }) : null,
            document.createTextNode(" " + (p.role || ""))
          ]);
          ulp.appendChild(el("li", { class: "participant" }, [caseClaim(content, p.evidenceNodeIds, null)]));
        });
        sec.appendChild(ulp);
      }
      if ((a.relationships || []).length) {
        var ulr = el("ul", { class: "relationships" });
        a.relationships.forEach(function (r) {
          var text = r.explanation || ((r.fromParticipantId || r.from || "") + " " + (r.kind || "") + " " + (r.toParticipantId || r.to || ""));
          ulr.appendChild(el("li", null, [caseClaim(text, r.evidenceNodeIds, null)]));
        });
        sec.appendChild(ulr);
      }
      if ((a.purposes || []).length) {
        var ulu = el("ul", { class: "purposes" });
        a.purposes.forEach(function (p) {
          ulu.appendChild(el("li", null, [caseClaim(caseRenderPurpose(p), p.evidenceNodeIds, null)]));
        });
        sec.appendChild(ulu);
      }
      box.appendChild(sec);
    });
    var limits = model.limitations || [];
    if (limits.length) {
      var ls = el("section", { class: "limits" }, [
        el("h3", { class: "panel-title" }, [withTip("What ChangeLens says it could not read", "limitation")])
      ]);
      var lul = el("ul");
      limits.forEach(function (l) {
        lul.appendChild(el("li", null, [
          el("code", { text: l.path || "" }),
          document.createTextNode(" \u2014 " + (l.detail || l.kind || ""))
        ]));
      });
      ls.appendChild(lul);
      box.appendChild(ls);
    }
    if ((model.assurances || []).length) {
      box.appendChild(el("p", { class: "label" }, [withTip("Assurances", "assurance")]));
      var aul = el("ul", { class: "assurances" });
      model.assurances.forEach(function (a) {
        aul.appendChild(el("li", { text: typeof a === "string" ? a : (a.detail || a.text || a.kind || "") }));
      });
      box.appendChild(aul);
    }
    if ((attempt.removals || []).length) {
      var rs = el("section", { class: "removals" }, [
        el("h3", { class: "panel-title" }, [withTip("Removed by validation", "removal")])
      ]);
      var rul = el("ul");
      attempt.removals.forEach(function (r) {
        var text = typeof r === "string" ? r : (r.text || [r.scope, r.id].filter(Boolean).join(" ") || "");
        rul.appendChild(el("li", null, [document.createTextNode(text + (r && r.reason ? " \u2014 " + r.reason : ""))]));
      });
      rs.appendChild(rul);
      box.appendChild(rs);
    }
    return box;
  }

  function caseRenderSidebar(runDetail, run, caseId) {
    var nav = el("nav", { class: "case-nav", "aria-label": "Cases" });
    (runDetail.cases || []).forEach(function (c) {
      var a = el("a", { class: "case-link", href: "#/runs/" + enc(run) + "/" + enc(c.caseId) }, [
        el("span", { class: "case-link-name", text: c.caseId }),
        caseVerdictChip(c.verdict)
      ]);
      if (c.caseId === caseId) a.setAttribute("aria-current", "page");
      nav.appendChild(a);
    });
    return nav;
  }

  function caseRenderVerdict() {
    var panel = el("section", { class: "panel verdict-panel" }, [
      el("p", { class: "panel-title" }, [withTip("Your verdict", "verdict")])
    ]);
    var choices = el("div", { class: "choices" });
    var note = el("input", { class: "note", type: "text", placeholder: "One line: why?" });
    note.value = caseDraft.note || "";
    var save = el("button", { class: "btn btn-primary", id: "save-verdict", text: "Save" });
    var status = el("span", { class: "save-status" });
    var remove = el("button", { class: "linklike remove-verdict", text: "Remove verdict" });
    var recorded = el("span", { class: "recorded-at" });
    var buttons = {};
    CASE_CHOICES.forEach(function (o) {
      var b = el("button", {
        class: "choice " + o[0],
        "aria-pressed": caseDraft.verdict === o[0] ? "true" : "false"
      }, [
        el("strong", { text: o[1] }),
        el("span", { class: "rubric", text: o[2] })
      ]);
      b.addEventListener("click", function () { casePick(o[0]); });
      buttons[o[0]] = b;
      choices.appendChild(b);
    });
    note.addEventListener("input", function () {
      caseDraft.note = note.value;
      caseWriteDraft(caseScreen.run, caseScreen.caseId, caseDraft);
    });
    save.addEventListener("click", function () { caseSaveVerdict(); });
    remove.addEventListener("click", function () { caseRemoveVerdict(); });
    panel.appendChild(choices);
    panel.appendChild(el("div", { class: "verdict-row" }, [note, save, status, remove, recorded]));
    caseScreen.els.choices = buttons;
    caseScreen.els.note = note;
    caseScreen.els.save = save;
    caseScreen.els.status = status;
    caseScreen.els.remove = remove;
    caseScreen.els.recorded = recorded;
    caseRefreshVerdict();
    return panel;
  }

  function caseRefreshVerdict() {
    var els = caseScreen.els;
    Object.keys(els.choices || {}).forEach(function (k) {
      els.choices[k].setAttribute("aria-pressed", caseDraft.verdict === k ? "true" : "false");
    });
    var v = caseScreen.verdict;
    if (v && v.recordedAt) els.recorded.textContent = "recorded " + fmtLocalTime(v.recordedAt);
    else els.recorded.textContent = "";
    els.remove.hidden = !v;
    els.save.disabled = !caseDraft.verdict;
  }

  function casePick(v) {
    caseDraft.verdict = v;
    caseWriteDraft(caseScreen.run, caseScreen.caseId, caseDraft);
    caseRefreshVerdict();
  }

  function caseVerdictUrl() {
    return "/api/runs/" + enc(caseScreen.run) + "/cases/" + enc(caseScreen.caseId) + "/verdict";
  }

  function caseApplyVerdict(v) {
    (caseScreen.runDetail.cases || []).forEach(function (c) {
      if (c.caseId === caseScreen.caseId) c.verdict = v;
    });
    var nav = caseScreen.els.nav;
    if (nav && nav.parentNode) {
      var fresh = caseRenderSidebar(caseScreen.runDetail, caseScreen.run, caseScreen.caseId);
      nav.parentNode.replaceChild(fresh, nav);
      caseScreen.els.nav = fresh;
    }
    caseUpdateProgress();
    caseRefreshVerdict();
  }

  function caseUpdateProgress() {
    var list = caseScreen.runDetail.cases || [];
    var done = list.filter(function (c) { return c.verdict; }).length;
    setProgress(done + " of " + list.length + " judged");
  }

  function caseSaveVerdict() {
    if (!caseDraft.verdict) return;
    var els = caseScreen.els;
    els.save.disabled = true;
    els.status.textContent = "Saving\u2026";
    api(caseVerdictUrl(), {
      method: "PUT",
      body: { verdict: caseDraft.verdict, note: caseDraft.note || "" }
    }).then(function (saved) {
      caseScreen.verdict = saved || {
        verdict: caseDraft.verdict, note: caseDraft.note || "", recordedAt: new Date().toISOString()
      };
      caseClearDraft(caseScreen.run, caseScreen.caseId);
      els.status.textContent = "Saved";
      caseApplyVerdict(caseScreen.verdict);
      setTimeout(function () { caseGoNextUnjudged(); }, 600);
    }).catch(function () {
      els.status.textContent = "";
      els.save.disabled = false;
    });
  }

  function caseRemoveVerdict() {
    var els = caseScreen.els;
    els.status.textContent = "Removing\u2026";
    api(caseVerdictUrl(), { method: "DELETE" }).then(function () {
      caseScreen.verdict = null;
      caseClearDraft(caseScreen.run, caseScreen.caseId);
      caseDraft = { verdict: null, note: "" };
      els.note.value = "";
      els.status.textContent = "";
      caseApplyVerdict(null);
    }).catch(function () { els.status.textContent = ""; });
  }

  function caseGoNextUnjudged() {
    var list = caseScreen.runDetail.cases || [];
    var start = -1;
    list.forEach(function (c, i) { if (c.caseId === caseScreen.caseId) start = i; });
    for (var step = 1; step <= list.length; step++) {
      var c = list[(start + step) % list.length];
      if (c && !c.verdict) {
        location.hash = "#/runs/" + enc(caseScreen.run) + "/" + enc(c.caseId);
        return;
      }
    }
    caseScreen.els.status.textContent = "All cases judged";
  }

  function caseMoveCase(delta) {
    var list = caseScreen.runDetail.cases || [];
    var start = -1;
    list.forEach(function (c, i) { if (c.caseId === caseScreen.caseId) start = i; });
    if (start < 0 || !list.length) return;
    var target = list[(start + delta + list.length) % list.length];
    if (target) location.hash = "#/runs/" + enc(caseScreen.run) + "/" + enc(target.caseId);
  }

  function caseGoRepeat(index) {
    var attempts = caseScreen.detail.attempts || [];
    if (index < 0 || index >= attempts.length || index === caseScreen.index) return;
    caseScreen.index = index;
    caseScreen.attempt = attempts[index];
    caseScreen.openClaim = null;
    var q = attempts[index].repeat == null ? "" : "?repeat=" + attempts[index].repeat;
    try { history.replaceState(null, "", "#/runs/" + enc(caseScreen.run) + "/" + enc(caseScreen.caseId) + q); } catch (e) {}
    casePaint();
  }

  function caseStepRepeat(delta) {
    var attempts = caseScreen.detail.attempts || [];
    if (attempts.length < 2) return;
    var next = caseScreen.index + delta;
    if (next < 0 || next >= attempts.length) return;
    caseGoRepeat(next);
  }

  function casePaint() {
    var attempt = caseScreen.attempt;
    var explain = caseScreen.els.explain;
    explain.textContent = "";
    explain.appendChild(el("p", { class: "panel-title" }, [withTip("What ChangeLens said", "explanation")]));
    explain.appendChild(caseRenderTabs());
    explain.appendChild(caseRenderMeta());
    if (attempt.readingModel) {
      explain.appendChild(el("p", { class: "muted claim-help" }, [withTip("Click a sentence to see the code it cites.", "claim")]));
    }
    explain.appendChild(caseRenderExplanation(attempt));
    var diff = caseScreen.els.diff;
    diff.textContent = "";
    var model = attempt.readingModel;
    caseScreen.diffView = createDiffView(diff, caseScreen.files, {
      notRead: caseNotRead(model),
      citeCounts: caseCiteCounts(model)
    });
  }

  function caseOnKey(e) {
    if (!document.querySelector(".case-shell")) return;
    if (e.ctrlKey || e.metaKey || e.altKey) return;
    var t = e.target;
    var typing = t && (t.tagName === "INPUT" || t.tagName === "TEXTAREA");
    if (typing) {
      if (e.key === "Enter") { e.preventDefault(); caseSaveVerdict(); }
      else if (e.key === "Escape") { caseCloseClaim(); }
      return;
    }
    if (e.key === "ArrowLeft") { e.preventDefault(); caseStepRepeat(-1); }
    else if (e.key === "ArrowRight") { e.preventDefault(); caseStepRepeat(1); }
    else if (e.key === "j") caseMoveCase(1);
    else if (e.key === "k") caseMoveCase(-1);
    else if (e.key === "g") casePick("good");
    else if (e.key === "w") casePick("weak");
    else if (e.key === "x") casePick("wrong");
    else if (e.key === "n") caseGoNextUnjudged();
    else if (e.key === "Escape") caseCloseClaim();
  }

  function caseBindKeys() {
    if (caseKeyBound) return;
    caseKeyBound = true;
    document.addEventListener("keydown", caseOnKey);
  }

  async function renderCase(route) {
    var run = route && route.run;
    var caseId = route && route.case;
    if (!run || !caseId) return;
    setCrumbs([["Runs", "#/runs"], [run, "#/runs/" + enc(run)], [caseId, null]]);
    var results;
    try {
      results = await Promise.all([
        api("/api/runs/" + enc(run)),
        api("/api/runs/" + enc(run) + "/cases/" + enc(caseId))
      ]);
    } catch (e) {
      return;
    }
    var current = parseRoute();
    if (current.screen !== "case" || current.run !== run || current.case !== caseId) return;
    var runDetail = results[0] || {};
    var detail = results[1] || {};
    var attempts = detail.attempts || [];
    var index = 0;
    if (route.repeat != null) {
      attempts.forEach(function (a, i) { if (a.repeat === route.repeat) index = i; });
    }
    caseDraft = caseReadDraft(run, caseId) || {
      verdict: detail.verdict ? detail.verdict.verdict : null,
      note: detail.verdict ? (detail.verdict.note || "") : ""
    };
    caseScreen = {
      run: run,
      caseId: caseId,
      runDetail: runDetail,
      detail: detail,
      files: parsePatch(detail.patch || ""),
      index: index,
      attempt: attempts[index] || { status: null, readingModel: null },
      diffView: null,
      openClaim: null,
      verdict: detail.verdict || null,
      els: {}
    };

    app.textContent = "";
    var shell = el("div", { class: "case-shell" });
    var nav = caseRenderSidebar(runDetail, run, caseId);
    shell.appendChild(nav);
    var body = el("div", { class: "case-body" });
    var top = el("div", { class: "case-top" });
    top.appendChild(caseRenderInfo(detail));
    top.appendChild(caseRenderChecks(detail));
    body.appendChild(top);
    var work = el("div", { class: "work" });
    var explain = el("section", { class: "panel explain" });
    var diff = el("section", { class: "panel diff" });
    work.appendChild(explain);
    work.appendChild(diff);
    body.appendChild(work);
    body.appendChild(caseRenderVerdict());
    shell.appendChild(body);
    app.appendChild(shell);

    caseScreen.els.nav = nav;
    caseScreen.els.explain = explain;
    caseScreen.els.diff = diff;
    casePaint();
    caseUpdateProgress();
    caseBindKeys();
    window.scrollTo(0, 0);
  }


  function infoSvg(tag, attrs, children) {
    var node = document.createElementNS("http://www.w3.org/2000/svg", tag);
    if (attrs) {
      for (var key in attrs) {
        if (!Object.prototype.hasOwnProperty.call(attrs, key)) continue;
        var value = attrs[key];
        if (value === null || value === undefined) continue;
        node.setAttribute(key, value);
      }
    }
    (children || []).forEach(function (child) {
      if (child === null || child === undefined || child === false) return;
      node.appendChild(typeof child === "string" ? document.createTextNode(child) : child);
    });
    return node;
  }

  function infoScrollTo(id) {
    var node = document.getElementById(id);
    if (node) node.scrollIntoView({ block: "start" });
  }

  function infoLink(label, id) {
    var button = el("button", { class: "linklike", type: "button", text: label });
    button.addEventListener("click", function () { infoScrollTo(id); });
    return button;
  }

  function infoBackticks(target, text) {
    String(text).split("`").forEach(function (part, index) {
      if (!part) return;
      if (index % 2 === 1) target.appendChild(el("code", { text: part }));
      else target.appendChild(document.createTextNode(part));
    });
  }

  function infoP(text) {
    var p = el("p");
    infoBackticks(p, text);
    return p;
  }

  function infoSection(id, title, children) {
    var section = el("section", { class: "panel info-section" });
    section.id = id;
    section.appendChild(el("h2", { text: title }));
    (children || []).forEach(function (child) { section.appendChild(child); });
    return section;
  }

  function infoLeadList(items) {
    var list = el("ul", { class: "info-list" });
    items.forEach(function (item) {
      var li = el("li");
      var strong = el("strong");
      if (item[1]) strong.appendChild(withTip(item[0], item[1]));
      else strong.appendChild(document.createTextNode(item[0]));
      li.appendChild(strong);
      if (item[2]) {
        var rest = el("span");
        infoBackticks(rest, " " + item[2]);
        li.appendChild(rest);
      }
      list.appendChild(li);
    });
    return list;
  }

  function infoToc() {
    var nav = el("nav", { class: "info-toc", "aria-label": "On this page" });
    var entries = [
      ["What this is", "info-what"],
      ["How it fits together", "info-flow"],
      ["Hard checks, soft checks and your verdict", "info-checks"],
      ["Judging a case", "info-judging"],
      ["Where things are stored", "info-files"],
      ["Housekeeping", "info-housekeeping"],
      ["Same thing from the terminal", "info-cli"],
      ["Glossary", "info-glossary"]
    ];
    entries.forEach(function (entry, index) {
      if (index) nav.appendChild(el("span", { class: "sep", text: "·" }));
      nav.appendChild(infoLink(entry[0], entry[1]));
    });
    return nav;
  }

  function infoVisualFlow() {
    var boxes = [
      { title: "Plan", lines: ["which changes", "to test"] },
      { title: "Run", lines: ["one execution", "of the plan"] },
      { title: "Case", lines: ["one change", "to explain"] },
      { title: "Repeats", lines: ["the same case,", "run again"] },
      { title: "Explanation", lines: ["what ChangeLens", "wrote"] },
      { title: "Checks", lines: ["hard: pass / fail ·", "soft: 2/3"] },
      { title: "Your verdict", lines: ["good · weak · wrong"], accent: true }
    ];
    var boxWidth = 150;
    var boxHeight = 104;
    var gap = 18;
    var top = 12;
    var total = boxes.length * boxWidth + (boxes.length - 1) * gap;
    var children = [
      infoSvg("defs", null, [
        infoSvg("marker", {
          id: "info-flow-arrow",
          viewBox: "0 0 8 8",
          refX: "7",
          refY: "4",
          markerWidth: "7",
          markerHeight: "7",
          orient: "auto-start-reverse"
        }, [infoSvg("path", { class: "flow-arrow-head", d: "M0,0 L8,4 L0,8 z" })])
      ])
    ];
    boxes.forEach(function (box, index) {
      var x = index * (boxWidth + gap);
      children.push(infoSvg("rect", {
        class: "flow-box" + (box.accent ? " flow-box-accent" : ""),
        x: x,
        y: top,
        width: boxWidth,
        height: boxHeight,
        rx: "8"
      }));
      children.push(infoSvg("text", {
        class: "flow-title",
        x: x + boxWidth / 2,
        y: top + 36,
        "text-anchor": "middle"
      }, [box.title]));
      box.lines.forEach(function (line, lineIndex) {
        children.push(infoSvg("text", {
          class: "flow-sub",
          x: x + boxWidth / 2,
          y: top + 60 + lineIndex * 15,
          "text-anchor": "middle"
        }, [line]));
      });
      if (index < boxes.length - 1) {
        children.push(infoSvg("line", {
          class: "flow-arrow",
          x1: x + boxWidth + 2,
          y1: top + boxHeight / 2,
          x2: x + boxWidth + gap - 2,
          y2: top + boxHeight / 2,
          "marker-end": "url(#info-flow-arrow)"
        }));
      }
    });
    return infoSvg("svg", {
      class: "info-svg info-flow-svg",
      viewBox: "0 0 " + total + " " + (boxHeight + top * 2),
      width: "100%",
      role: "img",
      "aria-label": "A plan becomes a run, each case repeats, can be checked, and gets your verdict."
    }, children);
  }

  function infoFlowLinks() {
    var row = el("p", { class: "info-flow-links" });
    var links = [
      ["Plan", "term-plan"],
      ["Run", "term-run"],
      ["Case", "term-case"],
      ["Repeat", "term-repeat"],
      ["Explanation", "term-explanation"],
      ["Hard check", "term-hardCheck"],
      ["Verdict", "term-verdict"]
    ];
    links.forEach(function (entry, index) {
      if (index) row.appendChild(el("span", { class: "sep", text: "·" }));
      row.appendChild(infoLink(entry[0], entry[1]));
    });
    return row;
  }

  function infoWhat() {
    return [
      infoP("ChangeLens reads a code change and writes an explanation of it. This tool checks how good those explanations are."),
      infoP("It runs the real ChangeLens engine over a list of example changes, records everything that happened, and lets you judge each explanation against the actual code."),
      infoP("You do three things here:"),
      el("ul", { class: "info-list" }, [
        el("li", { text: "Browse runs and see what they cost." }),
        el("li", { text: "Judge each case: read the explanation next to the diff, then mark it good, weak or wrong." }),
        el("li", { text: "Tidy up: compare two runs, or delete runs you no longer need." })
      ])
    ];
  }

  function infoFlow() {
    return [
      infoP("A plan becomes a run. Each case in it is run one or more times, checked automatically, and then judged by you."),
      infoVisualFlow(),
      infoFlowLinks(),
      infoLeadList([
        ["Plan.", "plan", "A Markdown file in docs/evaluation/review-plans/ that lists cases and what to check in each."],
        ["Run.", "run", "`review run <plan>` builds the engine once, then runs every case. Each run gets a folder named after its start time and plan, like 20260924T142524Z-quality-benchmark."],
        ["Case.", "case", "One code change: a small fixture repository, or a real project cloned at two commits."],
        ["Repeats.", "repeat", "The AI's wording changes from one attempt to the next, so a case can run several times. Judge the case by looking across its repeats."],
        ["Checks.", "softCheck", "Hard checks must hold, or the case fails. Soft checks are hints about quality: they are counted, never fail the case."],
        ["Your verdict.", "verdict", "The final word on quality. Only you can tell whether an explanation is true and useful."]
      ])
    ];
  }

  function infoTd(text) {
    var td = el("td");
    infoBackticks(td, text);
    return td;
  }

  function infoChecks() {
    var head = el("thead", null, [el("tr", null, [
      el("th", { text: "" }),
      el("th", { text: "Hard check" }),
      el("th", { text: "Soft check" }),
      el("th", { text: "Your verdict" })
    ])]);
    var rows = [
      ["Written by", "the plan's `expect:` list", "the plan's `judge:` list", "you, on the case screen"],
      ["Example", "every citation points at real code", "`should_link`: two files are explained together", "\"weak — misses why the guard was added\""],
      ["Result", "pass or fail per repeat", "passed / scored, e.g. 2/3", "good, weak or wrong"],
      ["Changes the case status?", "yes", "no", "no"],
      ["Where it is stored", "result.json", "result.json (judge_tally)", "verdict.json"]
    ];
    var body = el("tbody");
    rows.forEach(function (row) {
      var tr = el("tr");
      row.forEach(function (cell) { tr.appendChild(infoTd(cell)); });
      body.appendChild(tr);
    });
    var note = el("p", { class: "info-note" });
    note.appendChild(document.createTextNode("The three soft checks: "));
    var softs = [
      ["should_link", "two files are explained in the same section"],
      ["should_flag_related", "a file is mentioned as related"],
      ["should_not_flag", "a decoy file is left out"]
    ];
    softs.forEach(function (soft, index) {
      if (index) note.appendChild(document.createTextNode(" "));
      note.appendChild(el("strong", { text: soft[0] }));
      note.appendChild(document.createTextNode(" — " + soft[1] + "."));
    });
    return [
      el("div", { class: "table-wrap" }, [el("table", { class: "grid info-compare" }, [head, body])]),
      note
    ];
  }

  function infoVisualCase() {
    var children = [];
    function wireRect(cls, x, y, width, height) {
      return infoSvg("rect", { class: cls, x: x, y: y, width: width, height: height, rx: "8" });
    }
    function wireText(cls, x, y, lines, anchor) {
      lines.forEach(function (line, index) {
        children.push(infoSvg("text", {
          class: cls,
          x: x,
          y: y + index * 17,
          "text-anchor": anchor || "start"
        }, [line]));
      });
    }
    function wireNumber(number, x, y) {
      children.push(infoSvg("circle", { class: "wire-num", cx: x, cy: y, r: "12" }));
      children.push(infoSvg("text", { class: "wire-num-text", x: x, y: y + 4, "text-anchor": "middle" }, [String(number)]));
    }

    children.push(wireRect("wire-col", 8, 8, 104, 430));
    wireText("wire-title", 60, 218, ["Cases", "sidebar"], "middle");

    children.push(wireRect("wire-box", 124, 8, 372, 64));
    wireText("wire-title", 140, 38, ["Case, repository,", "plan notes"]);
    wireNumber(1, 474, 30);

    children.push(wireRect("wire-box", 508, 8, 384, 64));
    wireText("wire-title", 524, 44, ["Soft checks"]);

    children.push(wireRect("wire-strip", 124, 80, 768, 30));
    wireText("wire-sub", 140, 100, ["Repeats: 1  2  3    ← →"]);
    wireNumber(4, 874, 95);

    children.push(wireRect("wire-box", 124, 118, 372, 250));
    wireText("wire-title", 140, 150, ["Explanation", "(click a sentence)"]);
    wireNumber(2, 474, 140);

    children.push(wireRect("wire-box", 508, 118, 384, 250));
    wireText("wire-title", 524, 150, ["Diff", "(cited lines turn yellow)"]);
    wireNumber(3, 870, 140);

    children.push(wireRect("wire-box", 124, 378, 768, 60));
    wireText("wire-title", 140, 412, ["Verdict: Good · Weak · Wrong + note"]);
    wireNumber(5, 870, 408);

    return infoSvg("svg", {
      class: "info-svg info-wire-svg",
      viewBox: "0 0 900 450",
      width: "100%",
      role: "img",
      "aria-label": "Wireframe of the case screen: cases sidebar, case info, soft checks, repeats, explanation, diff and verdict."
    }, children);
  }

  function infoJudging() {
    var steps = [
      "Read the plan notes. They say what a good explanation of this change should get right.",
      "Read the thesis, then each area.",
      "Click any sentence with \"▸ n sources\". The lines it relies on light up yellow in the diff. If they don't support the sentence, it is wrong.",
      "Switch repeats with the tabs, or ← →. Check whether the repeats agree.",
      "Pick a verdict and add a one-line note. Save moves you to the next case you haven't judged."
    ];
    var ol = el("ol", { class: "info-steps" });
    steps.forEach(function (step) { ol.appendChild(el("li", { text: step })); });

    var rubric = el("div", { class: "info-rubric" });
    var cards = [
      ["good", "Good", "True, and a reviewer would understand the change."],
      ["weak", "Weak", "Nothing false, but misses something or is clumsy."],
      ["wrong", "Wrong", "At least one statement is false."]
    ];
    cards.forEach(function (card) {
      var box = el("div", { class: "info-card " + card[0] }, [
        el("strong", { text: card[1] }),
        document.createTextNode(" — " + card[2])
      ]);
      rubric.appendChild(box);
    });

    var keys = el("dl", { class: "keys info-keys" });
    var bindings = [
      ["← →", "repeat"],
      ["j k", "case"],
      ["g w x", "verdict"],
      ["Enter", "save (in the note box)"],
      ["n", "next unjudged case"],
      ["Esc", "close the open citation"]
    ];
    bindings.forEach(function (binding) {
      keys.appendChild(el("dt", null, [el("kbd", { text: binding[0] })]));
      keys.appendChild(el("dd", { text: binding[1] }));
    });

    var warnings = el("ul", { class: "info-list" });
    [
      "A sentence whose highlighted lines say something else.",
      "A thesis built around a file tagged \"not read by ChangeLens\".",
      "Statements under \"Removed by validation\" that were important.",
      "Repeats that disagree with each other."
    ].forEach(function (warning) { warnings.appendChild(el("li", { text: warning })); });

    return [
      infoVisualCase(),
      ol,
      rubric,
      el("h3", { text: "Keyboard" }),
      keys,
      el("h3", { text: "Warning signs" }),
      warnings
    ];
  }

  function infoVisualTree() {
    var rows = [
      ["20260924T142524Z-quality-benchmark/", "one run"],
      ["├─ run.json", "the run: plan, engine commit, start and end time, totals, list of cases"],
      ["├─ build.log", "output of the engine build"],
      ["├─ cases/", "one folder per case"],
      ["│  └─ gin-query-method/", ""],
      ["│     ├─ result.json", "status, every check with its actual value, soft-check tally, metrics"],
      ["│     ├─ verdict.json", "your verdict (only after you save one)"],
      ["│     └─ repeats/1/, 2/, 3/", "one folder per repeat"],
      ["│        ├─ change.patch", "the code change being explained (the diff you see)"],
      ["│        ├─ state.json", "the engine's database rows, including the published explanation"],
      ["│        ├─ engine.log", "the engine's own log"],
      ["│        ├─ protocol.ndjson", "every request to the engine and its answer"],
      ["│        ├─ oracle.json", "what Git says changed"],
      ["│        └─ provider/", "every AI request and answer, one file each"],
      ["└─ heavy/", "large working files (repositories, databases, build). Deleted after a run unless --keep; safe to clear."]
    ];
    var body = el("tbody");
    rows.forEach(function (row) {
      body.appendChild(el("tr", null, [
        el("td", { class: "tree-path", text: row[0] }),
        el("td", { class: "tree-note", text: row[1] })
      ]));
    });
    return el("div", { class: "table-wrap" }, [el("table", { class: "grid info-tree" }, [body])]);
  }

  function infoSnippet(title, lines) {
    return el("div", { class: "info-snippet" }, [
      el("h3", { text: title }),
      el("pre", { class: "info-code", text: lines.join("\n") })
    ]);
  }

  function infoFiles() {
    var runJson = [
      "{",
      "  \"run_id\": \"20260924T142524Z-quality-benchmark\",",
      "  \"plan_id\": \"quality-benchmark\",",
      "  \"engine\": { \"commit\": \"ca2ec13a3a97…\", \"dirty_diff_sha256\": null },",
      "  \"complete\": true,",
      "  \"counts\": { \"pass\": 9, \"fail\": 0, \"error\": 0, \"skipped\": 0 },",
      "  \"totals\": { \"provider\": { \"calls\": 27, \"total_tokens\": 1059865, \"cost\": 0.3265 } }",
      "}"
    ];
    var resultJson = [
      "{",
      "  \"case_id\": \"gin-query-method\",",
      "  \"status\": \"pass\",",
      "  \"judge_tally\": [",
      "    { \"name\": \"should_link\",",
      "      \"expected\": { \"from\": \"routergroup.go\", \"to\": \"ginS/gins.go\" },",
      "      \"passed\": 0, \"scored\": 3 }",
      "  ],",
      "  \"repeats\": [ { \"repeat\": 1, \"status\": \"pass\", \"expectations\": [ … ], \"judge\": [ … ] }, … ]",
      "}"
    ];
    var verdictJson = [
      "{",
      "  \"verdict\": \"weak\",",
      "  \"note\": \"Explains what changed but not why QUERY stays out of Any\",",
      "  \"recorded_at\": \"2026-09-24T16:02:11.412+00:00\"",
      "}"
    ];
    return [
      infoP("Everything lives in .changelens-review/runs/ in the repository. Each run is one folder; nothing here is sent anywhere."),
      infoVisualTree(),
      infoSnippet("run.json (shortened)", runJson),
      infoSnippet("result.json of one case (shortened)", resultJson),
      infoSnippet("verdict.json", verdictJson)
    ];
  }

  function infoHousekeeping() {
    var list = infoLeadList([
      ["Clear heavy", "heavy", "removes a run's heavy/ folder. Results, verdicts and logs stay. Use it freely."],
      ["Delete", null, "removes the whole run folder, verdicts included. It asks first, and warns when the run may still be running."],
      ["Compare", "compare", "— tick two runs on the runs screen. It lines them up case by case and reads A -> B, for example `fail -> pass`."]
    ]);
    var last = el("li");
    last.appendChild(document.createTextNode("A run marked "));
    last.appendChild(el("strong", null, [withTip("incomplete", "incomplete")]));
    last.appendChild(document.createTextNode(" is still running or was stopped. Its screen refreshes every 10 seconds."));
    list.appendChild(last);
    return [list];
  }

  function infoCli() {
    var lines = [
      "uv run --project tests/runtime review run <plan>                  # run a plan",
      "uv run --project tests/runtime review show <run> <case>           # explanation and diff as text",
      "uv run --project tests/runtime review verdict <run> <case> good   # record a verdict",
      "uv run --project tests/runtime review compare <run-a> <run-b>     # compare two runs",
      "uv run --project tests/runtime review clean [--all]               # delete heavy output, or all runs",
      "uv run --project tests/runtime review serve                       # this page"
    ];
    return [
      el("pre", { class: "info-code", text: lines.join("\n") })
    ];
  }

  function infoGlossary(route) {
    var list = el("dl", { class: "info-glossary" });
    Object.keys(GLOSSARY).forEach(function (key) {
      var entry = GLOSSARY[key];
      var dt = el("dt", { id: "term-" + key, text: entry.term });
      var dd = el("dd");
      dd.appendChild(el("strong", { class: "info-term-tip", text: entry.tip }));
      dd.appendChild(el("p", { class: "info-term-body", text: entry.body }));
      list.appendChild(dt);
      list.appendChild(dd);
    });
    if (route && route.term) {
      setTimeout(function () {
        var target = document.getElementById("term-" + route.term);
        if (!target) return;
        target.scrollIntoView({ block: "start" });
        target.classList.add("info-flash");
        setTimeout(function () { target.classList.remove("info-flash"); }, 2000);
      }, 0);
    }
    return list;
  }

  function renderInfo(route) {
    setCrumbs([["Runs", "#/runs"], ["Guide", null]]);
    var page = el("div", { class: "info-page" });
    page.appendChild(infoToc());
    page.appendChild(infoSection("info-what", "What this is", infoWhat()));
    page.appendChild(infoSection("info-flow", "How it fits together", infoFlow()));
    page.appendChild(infoSection("info-checks", "Hard checks, soft checks and your verdict", infoChecks()));
    page.appendChild(infoSection("info-judging", "Judging a case", infoJudging()));
    page.appendChild(infoSection("info-files", "Where things are stored", infoFiles()));
    page.appendChild(infoSection("info-housekeeping", "Housekeeping", infoHousekeeping()));
    page.appendChild(infoSection("info-cli", "Same thing from the terminal", infoCli()));
    page.appendChild(infoSection("info-glossary", "Glossary", [infoGlossary(route)]));
    app.replaceChildren(page);
    window.scrollTo(0, 0);
  }

  var refreshBtn = document.getElementById("refresh");
  if (refreshBtn) refreshBtn.addEventListener("click", function () { dispatch(); });

  var helpBtn = document.getElementById("help-toggle");
  if (helpBtn && help) helpBtn.addEventListener("click", function () {
    help.hidden = !help.hidden;
    helpBtn.setAttribute("aria-expanded", help.hidden ? "false" : "true");
  });

  var errorClose = document.getElementById("errorbar-close");
  if (errorClose) errorClose.addEventListener("click", function () { if (errorbar) errorbar.hidden = true; });

  window.addEventListener("hashchange", dispatch);

  dispatch();
})();
