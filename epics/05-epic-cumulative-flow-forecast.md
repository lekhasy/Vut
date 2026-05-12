# Epic 5: Probabilistic Forecasting & Progress Visualization

## Vertical Slice Statement

Team leads and stakeholders view a probabilistic forecast powered by Monte Carlo simulation, showing when their project will be done — not as a single date, but as a probability distribution with confidence ranges. The primary view is an S-curve (CDF) that answers "what date has X% confidence?"; the secondary view is a progress & forecast chart showing historical completed/scope lines with projected cones and an intersection region. After this Epic, Velucid delivers its central promise: honest, data-driven forecasting that never lies with a single date.

> See `docs/velucid_forecasting_spec.md` for the full technical specification of the forecasting algorithm.

## Target Personas

- Team Lead / Engineering Manager (primary -- communicates forecast probabilities to stakeholders)
- Organization Owner (secondary -- cross-product visibility)
- Developer / Team Member (tertiary -- curious about progress and project health)

## User Stories

1. As a team lead, I want to see a probability distribution of when my project will be done so that I can communicate honest forecasts instead of false-precision single dates.
2. As a team lead, I want to see stat cards showing 50%, 70%, and 95% confidence dates so that I can quickly read the headline forecast without interpreting a chart.
3. As a team lead, I want to adjust a threshold slider (50–99%) so that I can choose the confidence level that matches my risk tolerance and see all dates update instantly.
4. As a team lead, I want to see an S-curve (CDF) chart so that I can visually understand the probability of completion across all dates.
5. As a team lead, I want to see a progress & forecast chart showing historical completed/scope lines with projected cones so that I can understand throughput trends, scope growth, and why the forecast looks the way it does.
6. As a team lead, I want to see a never-finish indicator when simulations show scope growing faster than throughput so that I can take corrective action before it's too late.
7. As a team lead, I want to see a forecast spread health signal (high predictability, normal, high uncertainty) so that I can assess project health at a glance.
8. As a team lead, I want to filter the forecast by tags (e.g., `team:backend`) so that I can see forecasts for subsets of work.
9. As a team lead, I want to add a target date marker on the chart so that I can see the probability of hitting that target instead of pretending it's a hard deadline.
10. As a team member, I want the forecast to be read-only and based entirely on observed data so that I can trust it reflects reality, not wishful thinking.
11. As a team lead, I want the forecast to show a "gathering data" state when fewer than 7 days of history exist so that I'm not misled by premature projections.
12. As a team lead, I want to hover over any date on either chart and see contextual information (completed tasks, scope, WIP gap, probability) so that I can explore the data in detail.

## Acceptance Criteria

- [ ] A "Forecast" tab (or navigation item) is available within each product, alongside Backlog and Kanban Board.
- [ ] Three stat cards display above the chart:
  - 50% date labeled "50/50 estimate" (muted).
  - 70% date labeled "Planning date" (highlighted).
  - 95% date labeled "95% likely by" (muted).
  - No date is ever shown without a confidence qualifier.
- [ ] A threshold slider (range 50–99%, default 70%) is always visible alongside stat cards. Adjusting it updates all stat card dates, S-curve crosshair, and confidence date line instantly (no re-running simulations).
- [ ] A never-finish indicator appears as a fourth stat card when any simulations fail to converge:
  - < 20%: muted percentage text.
  - 20–50%: amber warning with guidance message.
  - > 50%: red alert with urgent guidance message.
- [ ] The primary view is the S-curve (CDF):
  - X-axis: calendar dates (today to p95 date).
  - Left Y-axis: cumulative probability (0–100%).
  - Right Y-axis: probability density (histogram bars behind the curve).
  - S-curve line in blue (#378ADD). Threshold line in amber (#EF9F27) with crosshair.
  - Region to left of threshold filled in pale amber.
- [ ] The secondary view is the progress & forecast chart (dual-cone):
  - Scope line (solid gray), completed line (solid blue with light fill below).
  - Scope cone (gray dashed bands, p10–p90), completed cone (blue dashed bands, p10–p90).
  - Intersection region in amber (~35% opacity).
  - Confidence date line (vertical solid amber at slider setting).
  - Today marker (vertical dashed line).
  - X-axis: calendar dates. Y-axis: task count (cumulative).
- [ ] This chart is NOT labeled as a "Cumulative Flow Diagram" — it is a "Progress & Forecast" chart.
- [ ] Forecast spread health signal displayed alongside stat cards:
  - < 3 weeks span: "High predictability."
  - 3–8 weeks: no label.
  - 8+ weeks: "High uncertainty."
  - No convergence: red warning state.
- [ ] Hover tooltips show contextual information:
  - Progress chart: completed tasks, scope, WIP gap, probability of completion (future dates).
  - Forecast chart: probability of completion, corresponding confidence date.
- [ ] The forecast is powered by Monte Carlo simulation (10,000 runs) using observed throughput and scope growth — no estimates, no story points.
- [ ] "Gathering data" state displayed when fewer than 7 days of history exist. No forecast shown.
- [ ] Tag-based filtering: a filter control allows selecting tags. Only tasks matching selected tags contribute to the forecast. Changing the filter recalculates within 2 seconds.
- [ ] Users can add a target date marker (vertical reference line) on either chart, showing the probability of completion by that date.
- [ ] All product members can view the forecast; it is read-only.
- [ ] Rolling window for sampling is 14 days by default, with user-adjustable setting (7, 14, 30 days) for advanced users.
- [ ] A project-level "working days" setting (default: Mon–Fri) controls which days are included in sampling distributions.
- [ ] Color contrast between chart elements meets WCAG 2.1 AA guidelines.

## Out of Scope for This Epic

- Organization-level cross-product forecasts (Phase 3).
- Multiple report types (hard constraint: one report only).
- Interactive editing of the chart or forecast.
- Paired sampling (correlating throughput and scope growth from the same historical day) — future enhancement.

## Estimated Complexity

**Extra Large** -- This is the signature feature of Velucid. The Monte Carlo simulation engine, two-chart visualization system (S-curve + dual-cone progress), stat cards, threshold slider, never-finish detection, forecast spread health signals, and tag-filtered recalculation represent the most significant work in the product.

## How to Demo

1. Navigate to the "Velucid Mobile App" product. Switch to the Forecast tab.
2. Three stat cards display: "50/50 estimate: May 28," "Planning date: Jun 12" (highlighted, 70% confidence), "95% likely by: Jul 3."
3. The S-curve shows the probability distribution — a smooth curve from ~0% on the left to ~100% on the right. An amber crosshair marks the 70% threshold at Jun 12.
4. Adjust the threshold slider from 70% to 85%. All three stat cards update instantly. The S-curve crosshair moves to a later date.
5. Switch to the Progress & Forecast view. Two solid lines (blue = completed, gray = scope) show historical data up to today. Beyond today, both lines fan out into cones. The amber intersection region shows the probable completion window.
6. Hover over a future date — a tooltip shows: "Completed: ~42 (projected), Scope: ~48 (projected), WIP gap: 6, 62% probability of completion by this date."
7. A fourth stat card reads: "8% of forecasts never finish" in muted text — scope growth is manageable.
8. Apply a tag filter: select `area:frontend`. The forecast recalculates — stat cards, S-curve, and cones all update. The frontend-only forecast shows a different (earlier) completion window.
9. Remove the filter. The full forecast returns.
10. Navigate to a brand-new product with 3 days of data. The Forecast tab shows: "Gathering data — 4 more days of activity needed before forecasting can begin."
11. Navigate to a product where scope has been growing faster than throughput. The never-finish stat card shows in red: "62% of forecasts never finish — scope is growing faster than work is completing. Review and reduce active scope." The forecast spread label reads "High uncertainty."
