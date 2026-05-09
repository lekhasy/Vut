# Epic 5: Cumulative Flow & Completion Forecast

## Vertical Slice Statement

Team leads and stakeholders view a stacked-area cumulative flow diagram showing how work has moved through statuses over time, with a projected completion date derived from historical throughput. After this Epic, Vut delivers its central promise: a single, data-driven answer to "when will it be done?"

## Target Personas

- Team Lead / Engineering Manager (primary -- communicates forecast to stakeholders)
- Organization Owner (secondary -- cross-product visibility)
- Developer / Team Member (tertiary -- curious about progress)

## User Stories

1. As a team lead, I want to see a cumulative flow diagram for my product so that I can visualize how work has flowed through the process over time.
2. As a team lead, I want the diagram to show one colored band per status so that I can see the distribution of work across statuses on any given day.
3. As a team lead, I want a projected completion date displayed on the chart so that I can answer "when will it be done?" without estimation.
4. As a team lead, I want the projection to show a range (not a single date) so that I understand the confidence level of the forecast.
5. As a team lead, I want to filter the diagram by tags (e.g., `team:backend`) so that I can see forecasts for subsets of work.
6. As a team lead, I want the chart to update when I apply a tag filter so that the projection recalculates based on the filtered subset.
7. As a team member, I want the report to be read-only so that I can trust it reflects actual data, not wishful thinking.

## Acceptance Criteria

- [ ] A "Report" tab (or navigation item) is available within each product, alongside Backlog and Kanban Board.
- [ ] The cumulative flow diagram renders as a stacked area chart:
  - X-axis: time (days or weeks, auto-scaled based on data range).
  - Y-axis: count of tasks.
  - Each status is a colored band. The total height at any point equals the total task count on that day.
- [ ] The chart renders correctly with as few as 1 day of data (may show a single data point; projection is hidden or marked as "insufficient data").
- [ ] A projected completion date is displayed as a vertical line or annotation on the chart, with a clear label (e.g., "Projected: Mar 15, 2026").
- [ ] The projection includes a confidence range (e.g., "Likely range: Mar 10 - Mar 22") derived from throughput variance.
- [ ] The projection method uses historical throughput (rate of tasks reaching the final status). No story points, no estimates.
- [ ] Tag-based filtering: a filter control allows selecting tags to include. Only tasks matching the selected tags contribute to the chart and projection.
- [ ] Changing the tag filter recalculates and redraws the chart and projection in real time (under 2 seconds).
- [ ] The projection is hidden or shows an "insufficient data" message when fewer than N data points exist (N to be determined during implementation; suggested: 5 days of status transitions).
- [ ] All product members can view the report; it is read-only.
- [ ] Color contrast between status bands meets WCAG 2.1 AA guidelines.

## Out of Scope for This Epic

- Monte Carlo simulation for projections (Phase 2 enhancement).
- Organization-level cross-product reports (Phase 3).
- Multiple report types (hard constraint: one report only).
- Interactive editing of the chart or projection.

## Estimated Complexity

**Large** -- This is the signature feature of Vut. The cumulative flow diagram, throughput calculation, projection algorithm, and interactive tag-filtered chart represent significant work.

## How to Demo

1. Navigate to the "Vut Mobile App" product. Switch to the Report tab.
2. The cumulative flow diagram displays with colored bands for each status. Early data shows most tasks in "New."
3. Over a simulated or real 2-week period, tasks have been moved through statuses. The bands show the flow pattern: "New" shrinking, "Done" growing.
4. A vertical line marks the projected completion date. A label reads: "Projected: Jun 12, 2026 (Range: Jun 5 - Jun 20)."
5. Apply a tag filter: select `area:frontend`. The chart redraws showing only frontend-tagged tasks. The projection updates to a different date.
6. Remove the filter. The full chart returns.
7. Navigate to a brand-new product with no tasks. The report shows an empty state: "Add tasks and move them through your workflow to see the cumulative flow diagram."
8. Navigate to a product with 1 day of data. The chart shows a single data point. The projection area says: "Insufficient data for projection. Keep moving tasks through your workflow."
