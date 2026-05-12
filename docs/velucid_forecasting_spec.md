# Velocid — Probabilistic Forecasting System

**Developer Specification — Internal**

| Version | 1.0 |
| --- | --- |
| Status | Draft — for developer review |
| Philosophy | No Estimates. All forecasts derived from observed throughput. |

> The core principle: replace the date field with a probability curve. The team earns a narrower, more confident forecast by working consistently — not by making optimistic promises.

> **Never display a single completion date anywhere in the Velocid UI. Every forecast must be expressed as a probability or a confidence range.**

---

## 1. Philosophy: why single dates are lies

Every project management tool asks you to type a deadline. That date is then treated as fact. Jira puts it in a field. Gantt charts draw a hard line. Stakeholders plan around it. When it slips — and it almost always slips — trust erodes.

Velocid takes a different position: a completion date is not something you declare, it is something that emerges from the actual behavior of your team and your scope. We do not ask teams to estimate. We observe what has happened, model the uncertainty honestly, and show a probability distribution of when the project might end.

### What is wrong with a single date

A single projected completion date implies the future is knowable to the day. It is not. Two sources of variance make any single date misleading:

- **Throughput variance** — the rate at which the team completes tasks changes day to day, sprint to sprint, due to interruptions, complexity, team changes, and morale.
- **Scope variance** — the total number of tasks is not fixed. Scope grows as discovery happens, requirements are clarified, and stakeholders add work.

A single regression line through past progress captures neither of these. It assumes the future will look exactly like the average of the past, which is almost never true.

### What honest forecasting looks like

Instead of one date, we show a probability distribution: a curve that answers the question "by what date will this project be X% likely to be done?" This gives stakeholders a real choice — plan to the 70% date, or invest more buffer and plan to the 85% date. That is a meaningful conversation. "Done by June 14" is not.

---

## 2. Data model

Velocid requires only two time series to run all forecasts. No story points, no hour estimates, no complexity scores.

### The two lines

#### Completed count

A cumulative count of tasks marked done, sampled at regular intervals (daily recommended, weekly acceptable). This is the only throughput signal we need.

```text
completed[t] = total tasks marked done by day t

Example:
  Day 0:  0
  Day 5:  8
  Day 10: 14
  Day 15: 23
  Day 20: 31
```

#### Scope count

A cumulative count of all tasks that exist in the project at each sample point — including newly added ones. This is not a fixed number. It grows.

```text
scope[t] = total tasks in project by day t(done + not done)

Example:
  Day 0:  20
  Day 5:  26
  Day 10: 33
  Day 15: 44
  Day 20: 48
```

### Derived metrics

#### Throughput

The average number of tasks completed per day over a rolling window. Use a 14-day rolling window as the default.

```text
throughput(t, window) = (completed[t] - completed[t - window]) / window

Example (14-day window ending day 20):
  (31 - 8) / 14 = 1.64 tasks/day
```

#### Scope growth rate

The average number of new tasks added per day over the same rolling window.

```text
scopeGrowth(t, window) = (scope[t] - scope[t - window]) / window

Example (14-day window ending day 20):
  (48 - 26) / 14 = 1.57 tasks/day
```

#### Work in progress (WIP) gap

The vertical distance between the scope line and the completed line at any point in time. This is the current backlog.

```text
wip(t) = scope[t] - completed[t]

Example at day 20:
  48 - 31 = 17 tasks remaining
```

> The project completes when `completed[t] >= scope[t]`. This intersection is what all forecasting targets — not a date you type in.

---

## 3. Monte Carlo simulation

Monte Carlo is the correct forecasting method for this problem. Rather than fitting one regression line (which implies false precision), we run thousands of simulations, each sampling a plausible future, and build a distribution of outcomes.

### Why Monte Carlo

- It handles both throughput variance and scope variance simultaneously.
- It produces a full probability distribution, not a single number.
- It naturally captures the tail risk — the scenarios where scope grows faster than throughput and the project never completes.
- It requires no estimates from the team. All inputs are observed history.

### Algorithm

#### Step 1 — compute historical distributions

From the historical time series, collect daily deltas for throughput and scope growth. These become the empirical distributions we sample from during simulation.

Note: the narrative in Section 2 defines throughput as a rolling-window average (useful for display). Here we collect raw **daily deltas** instead — one sample per day — because Monte Carlo needs individual observations to preserve the real variance of the data. Rolling averages would smooth out the tails.

```js
// Collect daily throughput deltas over recent history
throughputSamples = []
for each day t in history:
  delta = completed[t] - completed[t-1]
  // Optionally filter out non-working days (weekends, holidays)
  // to avoid injecting zero-throughput noise into the distribution.
  // If filtered, ensure scope growth samples are filtered identically.
  throughputSamples.push(delta)

// Collect daily scope growth deltas
scopeGrowthSamples = []
for each day t in history:
  scopeGrowthSamples.push(scope[t] - scope[t-1])
```

> **Weekend / holiday handling:** If the team does not work weekends, a raw 14-day history includes ~4 zero-throughput days. These zeros inflate variance and drag down mean throughput. Filter non-working days from both sample arrays consistently, or use only weekday deltas. Expose this as a project-level setting ("working days: Mon–Fri" vs "every day").

#### Step 2 — run simulations

For each simulation, **re-sample throughput and scope growth each simulated day** from the historical distributions. This produces realistic path simulations where day-to-day variance naturally averages out over long horizons (central limit theorem) while still capturing tail risk from sustained bad stretches.

A static approach (sampling once per simulation) would lock an extreme value in for the entire run, producing artificially wide cones. Per-day re-sampling is the correct model.

```js
SIMULATIONS = 10000
MAX_DAYS    = 365
results     = []

for i in 0..SIMULATIONS:
  c = currentCompleted
  s = currentScope

  for day in 1..MAX_DAYS:
    // Re-sample each day from historical distributions
    v  = randomSample(throughputSamples)    // tasks completed this day
    gr = randomSample(scopeGrowthSamples)   // scope change this day

    // Clamp throughput — a team cannot un-complete tasks
    v = max(0, v)

    // Allow negative scope growth (descoping is valid).
    // Only clamp so scope never drops below completed count.
    c += v
    s += gr
    s = max(s, c)  // scope can't be less than completed

    if c >= s:
      results.push(day)
      break
  // If never finished, don't push — this is a "never finish" run
```

> **Why per-day re-sampling?** A static model (sample `v` and `gr` once, hold constant) reduces to `days = backlog / (v - gr)` — a simple ratio, not a path simulation. It treats "one bad day in history" as "what if every future day is this bad," which over-widens the forecast. Per-day re-sampling lets the simulation explore realistic paths where bad days are followed by normal ones.

#### Step 3 — build the CDF

Sort the results and compute the cumulative distribution function. Each percentile point answers: "by day X, what fraction of simulations had finished?"

```js
results.sort(ascending)
total = results.length
neverFinishPct = (SIMULATIONS - total) / SIMULATIONS * 100

// CDF: for each day d, what % of simulations finished by day d?
cdf = {}
for pct in [50, 70, 80, 85, 90, 95]:
  idx     = ceil(pct / 100 * total) - 1
  dayVal  = results[idx]
  cdf[pct] = today + dayVal days
```

#### Step 4 — sampling strategy

Two valid approaches to sampling. Use empirical sampling as the default; use normal distribution when history is very short (fewer than 10 data points).

| Approach | Description | When to use |
| --- | --- | --- |
| **Empirical** (preferred) | Randomly pick from actual past daily values. Preserves real variance including outliers and clusters. | ≥ 10 days of history available |
| **Truncated normal** | Compute mean and std dev of samples, then draw from N(mean, stddev) clamped to [0, ∞) for throughput. Smoother but loses real-world spikiness. | Fewer than 10 days of history |

> **Why not a plain normal distribution?** A normal distribution can produce negative throughput values, which must be clamped to zero. This clamping shifts the effective mean downward and biases forecasts pessimistically. Use a truncated normal (reject and re-draw negative samples) or a log-normal fit to avoid this distortion.

> Always run at least 5,000 simulations. Below that, the CDF is too noisy for the tail percentiles (90%+) to be reliable. 10,000 is the recommended default.

---

## 4. Scope uncertainty

Most forecasting tools treat scope as a fixed line. This is wrong. Scope changes — typically it grows — and that growth is itself variable and uncertain. Velocid treats both lines as probabilistic.

### The scope cone

Just as the completed line fans out into the future based on throughput variance, the scope line fans out based on scope growth rate variance. The result is two cones, both opening toward the right.

- **Completed cone** — based on sampled throughput variance. Fans upward (optimistic) and downward (pessimistic).
- **Scope cone** — based on sampled scope growth variance. Fans upward (more scope being added) and flatter (scope growth slowing or shrinking).

The project completes where the completed cone intersects the scope cone. This intersection is not a single point — it is a region.

```js
// For each simulation day:
// - sample a throughput delta (completed cone)
// - sample a scope growth delta (scope cone)
// - advance both lines
// The distribution of crossing points IS the forecast
```

> **Independence assumption:** The current model samples throughput and scope growth independently. In practice, they may be negatively correlated — when scope explodes, team morale can drop and throughput decreases. This means the model slightly overweights the "high throughput + high scope growth" combination, making forecasts marginally optimistic. A future improvement is **paired sampling** — drawing throughput and scope growth from the same historical day — which preserves any real-world correlation in the data.

### The intersection region

The probability of completion at any given date is determined by what fraction of all simulation paths (throughput sample sequence, scope growth sample sequence) result in intersection at or before that date. The amber region in the UI represents this zone.

### The "never finish" signal

In simulations where the scope growth rate is consistently higher than the throughput, the completed line never catches the scope line. These runs do not produce a crossing point and are excluded from the CDF. The percentage of never-finish runs is a critical health metric.

> Display the "never finish" percentage prominently. If it exceeds 20%, show a warning. If it exceeds 50%, the project is in serious structural trouble and the UI should reflect this clearly — not hide it behind a positive-looking chart.

### Scope growth categories

Scope growth samples should be treated carefully. Scope can decrease (tasks removed or descoped), stay flat, or grow. Use the following classification to guide UI messaging:

| Growth rate | Label | Implication |
| --- | --- | --- |
| < 0 | Scope shrinking | Cone narrows. Project accelerates toward finish. |
| = 0 | Scope frozen | No growth. Clean signal. Forecast is most reliable. |
| 0 – 50% of throughput | Scope growing slowly | Project will finish but later than naive estimates suggest. |
| > 50% of throughput | Scope growing fast | High never-finish risk. Widen the cone significantly. |
| ≈ throughput | Treadmill | Completing as fast as adding. Cone fans extremely wide. Project may never finish. |

---

## 5. UI display specification

How we show forecasts is as important as how we calculate them. The goal is to communicate uncertainty honestly without overwhelming users. Every visual decision should make the probability nature of the forecast feel natural, not alarming.

### Stat cards

Above the chart, show three stat cards. These are the headline — the first thing users read. Do not show a single date without a confidence label.

- **50% date** — labeled "50/50 estimate" in muted text.
- **70% date** — labeled "Planning date" and highlighted. This is the recommended date for stakeholder communication.
- **95% date** — labeled "95% likely by" in muted text.

> Never label any date as "the completion date" without a confidence qualifier. Always show the percentage. "Done by Jun 28" is forbidden. "70% confidence: Jun 28" is correct.

### Threshold slider

The slider lives alongside the stat cards and is always visible regardless of which chart view is active. Default 70%, range 50–99%. Adjusting it updates:

- All three stat card dates (always).
- The S-curve crosshair and amber fill region (when the forecast chart is visible).
- A vertical reference line on the progress chart showing the selected confidence date (when the progress chart is visible).

### Never-finish indicator

If any simulations resulted in never-finish, show a fourth stat card to the right of the three date cards:

- **< 20%**: Show the percentage in muted text. No special styling.
- **20–50%**: Amber warning card. Label: *"X% of forecasts never finish — consider reducing scope or increasing team capacity."*
- **> 50%**: Red alert card. Label: *"X% of forecasts never finish — scope is growing faster than work is completing. Review and reduce active scope."*

### Primary view: forecast chart (S-curve)

The primary view is the probability S-curve. It directly answers the only question stakeholders care about: "what date has X% confidence?" This view should be immediately legible to anyone — no statistical training required.

- X-axis: calendar dates extending from today to the p95 date.
- Left Y-axis: cumulative probability (0–100%).
- Right Y-axis: probability density (the histogram bars behind the curve).
- The S-curve line is drawn in blue (#378ADD).
- The threshold line (controlled by the slider above) drawn in amber (#EF9F27), with crosshair lines showing the exact date.
- The region to the left of the threshold is filled in pale amber — "this is the window you're committing to."

### Secondary view: progress & forecast chart (dual-cone)

Available as a tab or toggle below the stat cards. Shows the historical progress and projected cones — the detailed "why" behind the forecast. This view is for team leads who want to understand throughput trends, scope growth, and cone width.

> **Why not call this a CFD?** A traditional Cumulative Flow Diagram shows bands for each kanban column (backlog → in progress → review → done). This chart only shows two lines (completed + scope), so calling it a "CFD" would create incorrect expectations for users familiar with the traditional format.

| Element | Specification |
| --- | --- |
| Scope line (solid) | Gray (#5F5E5A), 2px. Draws the actual historical scope growth up to today. |
| Completed line (solid) | Blue (#378ADD), 2.5px, with light fill below. Historical actuals only. |
| Scope cone (dashed) | Gray bands, two opacity levels (outer 15%, inner 25%). Represents p10–p90 of scope growth projections. |
| Completed cone (dashed) | Blue bands, two opacity levels (outer 15%, inner 25%). Represents p10–p90 of throughput projections. |
| Intersection region | Amber (#EF9F27), ~35% opacity fill. The overlap zone where the cones meet. This is the visual answer to "when will this finish?" |
| Confidence date line | Vertical solid amber line at the date corresponding to the current slider setting. Anchors the cone to the stat cards above. |
| Today marker | Vertical dashed line at today. The point where history ends and forecast begins. |
| X-axis | Time. Show date labels, not day numbers. |
| Y-axis | Task count (cumulative). No story points, no hours. |

### Forecast spread as a health signal

The spread between p50 and p95 dates is a direct proxy for project health. Show this as a label on the stat card row:

- **Narrow spread** (p50 to p95 span < 3 weeks) — label as "High predictability". Team is consistent, scope is stable.
- **Medium spread** (3–8 weeks span) — no label. Normal state.
- **Wide spread** (8+ weeks span) — label as "High uncertainty". Throughput is erratic, scope is growing fast, or both.
- **No convergence** (cones never intersect) — red warning state. "At current rates, this project has no projected completion."

### Hover tooltips

Hovering over any date on either chart shows contextual information:

**On the progress & forecast chart:**

- Completed tasks as of that date (actual if in the past, projected if in the future).
- Scope as of that date (actual if in the past, projected if in the future).
- WIP gap (scope minus completed).
- Probability of completion by that date (future dates only).

**On the forecast chart (S-curve):**

- The probability of completion by that date.
- The corresponding confidence date (e.g., "70% confidence: Jun 28").

### No deadline field

Velocid does not have a "deadline" or "due date" field on projects. If users need to mark a target, they can add a vertical reference line on either chart — a "target marker" — which shows the probability of completion by that date as a label on the line. This makes the question honest: not "will we hit the deadline" but "what are our chances of hitting this target."

---

## 6. Implementation notes

### Data collection

- Sample completed and scope counts once per day, at a consistent time (e.g. midnight UTC).
- Store as a simple time series: `{ date, completedCount, scopeCount }`.
- Backfill is acceptable — if a project is imported from another tool, reconstruct the time series from change logs.
- Minimum viable history for forecasting: 7 days. Below 7 days, show a "gathering data" state instead of a forecast.
- Mark non-working days (weekends, holidays) in the time series so they can be excluded from sampling distributions. A project-level "working days" setting controls this (default: Mon–Fri).

### Simulation performance

10,000 simulations with up to 365 days each is at most 3.65 million iterations per forecast. This runs in under 100ms in a modern JS environment if kept simple. Do not over-engineer it.

- Run simulations on the server, not the client. Cache the CDF and invalidate when new data arrives.
- Re-run simulations daily (or on any scope/completion change).
- Return the full CDF array to the client so the threshold slider can update instantly without re-running simulations.

### Edge cases

- **No history yet (< 7 days)**: show an empty chart with a "Gathering data" message. Do not show a forecast.
- **Scope is zero**: invalid project state. Show an error.
- **Completed > Scope**: data integrity issue (e.g. tasks removed after completion). Flag and investigate. Do not forecast.
- **Throughput is zero for an extended period** (team inactive): never-finish percentage will be very high. Surface this clearly.
- **Scope growth rate exceeds throughput in >50% of simulations**: show a structural warning. Suggest reducing WIP or scope.

### Rolling window recommendation

Use a 14-day rolling window for computing throughput and scope growth samples to feed into Monte Carlo. This balances recency (capturing current team state) against noise (avoiding overreaction to a single bad day). Expose this as a user-adjustable setting (7, 14, 30 days) for advanced users.

> The rolling window length is a product decision as much as a technical one. A 7-day window makes the forecast react quickly to team changes — good for dynamic environments. A 30-day window smooths out short-term noise — better for stable, long-running projects. Default to 14 days.

---

## 7. Glossary

| Term | Definition |
| --- | --- |
| Throughput | The number of tasks a team completes per unit of time. The primary input to forecasting. |
| Scope | The total number of tasks in the project at a given point — including completed and not-yet-started tasks. Not fixed. |
| WIP gap | Work in progress: the difference between scope and completed at any moment. The size of the remaining backlog. |
| CDF | Cumulative Distribution Function. A curve mapping each date to the probability that the project finishes by that date. The S-curve in the UI. |
| Monte Carlo | A simulation technique that runs many random samples to build a probability distribution. Used here to model both throughput and scope growth uncertainty. |
| Cone of uncertainty | The visual fan shape that represents the growing uncertainty of a projection over time. Both the completed and scope lines fan out into cones. |
| Intersection region | The zone where the completed cone and the scope cone overlap. Represents the probable window of project completion. |
| Never-finish % | The fraction of Monte Carlo simulations where scope grew faster than throughput for the entire simulation and the project never completed. |
| p50 / p70 / p85 / p95 | Percentile dates from the CDF. The p70 date means 70% of simulations finished by that date. |
| No Estimates | A software development philosophy that rejects story points and hour estimates in favor of flow metrics (throughput, cycle time) derived from observation. |

---

*Velocid — built for teams who respect honest numbers.*
