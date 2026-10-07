"use client";

import {
  Activity, AlertTriangle, ArrowDownRight, ArrowLeftRight, ArrowRight, ArrowUpRight, BarChart3,
  Check, ChevronDown, ChevronRight, CircleHelp, Clock3, Command, Database, FileCode2, Gauge,
  GitCompareArrows, Layers3, LayoutDashboard, ListFilter, LoaderCircle, Play, Search, Server,
  Sparkles, TrendingDown, TrendingUp, X,
} from "lucide-react";
import { useEffect, useEffectEvent, useMemo, useState, type ReactNode } from "react";
import { ApiError, apiGet, apiPost } from "@/lib/api";
import type {
  AdvisorAnalysisResponse, Baseline, Experiment, ExecutionPlans, MetricComparison, RunComparison,
  RunDetail, RunSummary, TrendMetric,
} from "@/lib/types";

type View = "dashboard" | "experiments" | "runs" | "trends" | "compare" | "advisor" | "plans";
type Change = { run: RunSummary; prior: RunSummary; percent: number };
const navigation: { id: View; label: string; icon: typeof LayoutDashboard }[] = [
  { id: "dashboard", label: "Overview", icon: LayoutDashboard },
  { id: "experiments", label: "Experiments", icon: Layers3 },
  { id: "runs", label: "Benchmark runs", icon: Activity },
  { id: "trends", label: "Historical trends", icon: BarChart3 },
  { id: "compare", label: "Compare runs", icon: GitCompareArrows },
];
const titles: Record<View, string> = {
  dashboard: "Performance overview", experiments: "Experiment explorer", runs: "Benchmark runs",
  trends: "Historical analysis", compare: "Run comparison", advisor: "Advisor analysis", plans: "Execution plans",
};

export default function Workspace() {
  const [view, setView] = useState<View>("dashboard");
  const [experiments, setExperiments] = useState<Experiment[]>([]);
  const [runs, setRuns] = useState<RunSummary[]>([]);
  const [apiReady, setApiReady] = useState(false);
  const [runsError, setRunsError] = useState("");
  const [search, setSearch] = useState("");
  const [category, setCategory] = useState("All categories");
  const [difficulty, setDifficulty] = useState("All levels");
  const [experimentId, setExperimentId] = useState("");
  const [runId, setRunId] = useState("");
  const [detail, setDetail] = useState<RunDetail | null>(null);
  const [analysis, setAnalysis] = useState<AdvisorAnalysisResponse | null>(null);
  const [plans, setPlans] = useState<ExecutionPlans | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [runningId, setRunningId] = useState("");
  const [notice, setNotice] = useState("");
  const [history, setHistory] = useState<RunSummary[]>([]);
  const [historyError, setHistoryError] = useState("");
  const [historyLoading, setHistoryLoading] = useState(false);
  const [baseline, setBaseline] = useState<Baseline | null>(null);
  const [metric, setMetric] = useState<TrendMetric>("duration");
  const [runA, setRunA] = useState("");
  const [runB, setRunB] = useState("");
  const [comparison, setComparison] = useState<RunComparison | null>(null);
  const [compareError, setCompareError] = useState("");
  const [compareLoading, setCompareLoading] = useState(false);

  const loadRunData = useEffectEvent((targetRunId: string, targetView: View, signal: AbortSignal) => {
    if (!targetRunId || !["runs", "advisor", "plans"].includes(targetView) || (detail?.runId === targetRunId && plans?.runId === targetRunId && analysis?.runId === targetRunId)) return;
    queueMicrotask(() => { if (!signal.aborted) setDetailLoading(true); });
    Promise.allSettled([
      apiGet<RunDetail>(`/api/runs/${encodeURIComponent(targetRunId)}`, signal),
      apiGet<AdvisorAnalysisResponse>(`/api/runs/${encodeURIComponent(targetRunId)}/analysis`, signal),
      apiGet<ExecutionPlans>(`/api/runs/${encodeURIComponent(targetRunId)}/plans`, signal),
    ]).then(([runResult, analysisResult, plansResult]) => {
      if (signal.aborted) return;
      if (runResult.status === "fulfilled") { setDetail(runResult.value); setRunsError(""); }
      else setRunsError(message(runResult.reason));
      if (analysisResult.status === "fulfilled") setAnalysis(analysisResult.value);
      if (plansResult.status === "fulfilled") setPlans(plansResult.value);
    }).finally(() => { if (!signal.aborted) setDetailLoading(false); });
  });

  const loadHistory = useEffectEvent((targetView: View, targetExperimentId: string, signal: AbortSignal) => {
    if (targetView !== "trends" || !targetExperimentId) return;
    queueMicrotask(() => { if (!signal.aborted) { setHistoryLoading(true); setHistoryError(""); } });
    apiGet<RunSummary[]>(`/api/history/${encodeURIComponent(targetExperimentId)}?latest=100`, signal)
      .then(setHistory).catch((error: unknown) => { if (!signal.aborted) setHistoryError(message(error)); }).finally(() => { if (!signal.aborted) setHistoryLoading(false); });
    apiGet<Baseline>(`/api/baseline/${encodeURIComponent(targetExperimentId)}`, signal)
      .then(setBaseline).catch(() => { if (!signal.aborted) setBaseline(null); });
  });

  const loadComparison = useEffectEvent((targetView: View, firstRunId: string, secondRunId: string, signal: AbortSignal) => {
    if (targetView !== "compare" || !firstRunId || !secondRunId || firstRunId === secondRunId) return;
    queueMicrotask(() => { if (!signal.aborted) { setCompareLoading(true); setCompareError(""); } });
    apiGet<RunComparison>(`/api/compare/${encodeURIComponent(firstRunId)}/${encodeURIComponent(secondRunId)}`, signal)
      .then(setComparison).catch((error: unknown) => { if (!signal.aborted) setCompareError(message(error)); }).finally(() => { if (!signal.aborted) setCompareLoading(false); });
  });

  useEffect(() => {
    const controller = new AbortController();
    apiGet<Experiment[]>("/api/experiments", controller.signal).then((data) => {
      setExperiments(data);
      setExperimentId((current) => current || data[0]?.id || "");
      setApiReady(true);
    }).catch(() => setApiReady(false));
    apiGet<RunSummary[]>("/api/runs?latest=50", controller.signal).then((data) => {
      setRuns(data);
      setRunId((current) => current || (data.length > 0 ? data[0]?.runId : "") || "");
      setRunA((current) => current || data[0]?.runId || "");
      setRunB((current) => current || data[1]?.runId || "");
    }).catch((error: unknown) => setRunsError(message(error)));
    return () => controller.abort();
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    loadRunData(runId, view, controller.signal);
    return () => controller.abort();
  }, [runId, view]);

  useEffect(() => {
    const controller = new AbortController();
    loadHistory(view, experimentId, controller.signal);
    return () => controller.abort();
  }, [view, experimentId]);

  useEffect(() => {
    const controller = new AbortController();
    loadComparison(view, runA, runB, controller.signal);
    return () => controller.abort();
  }, [view, runA, runB]);

  const categories = useMemo(() => ["All categories", ...new Set(experiments.map((item) => item.category).filter(Boolean))], [experiments]);
  const difficulties = useMemo(() => ["All levels", ...new Set(experiments.map((item) => item.difficulty).filter(Boolean))], [experiments]);
  const filteredExperiments = useMemo(() => experiments.filter((item) => {
    const query = search.trim().toLowerCase();
    const searchable = [item.id, item.name, item.category, item.difficulty, ...item.tags].join(" ").toLowerCase();
    return (!query || searchable.includes(query)) && (category === "All categories" || item.category === category) && (difficulty === "All levels" || item.difficulty === difficulty);
  }), [experiments, search, category, difficulty]);
  const filteredRuns = useMemo(() => runs.filter((item) => {
    const query = search.trim().toLowerCase();
    return !query || [item.runId, item.experimentId, item.experimentName, item.status, item.machineName].join(" ").toLowerCase().includes(query);
  }), [runs, search]);
  const regressions = useMemo(() => runChanges(runs).filter((item) => item.percent > 10), [runs]);
  const improvements = useMemo(() => runChanges(runs).filter((item) => item.percent < -10), [runs]);
  const searchTerm = search.trim().toLowerCase();
  const experimentMatches = searchTerm ? experiments.filter((item) => [item.id, item.name, item.category, item.difficulty, ...item.tags].join(" ").toLowerCase().includes(searchTerm)).slice(0, 5) : [];
  const runMatches = searchTerm ? runs.filter((item) => [item.runId, item.experimentId, item.experimentName, item.status, item.machineName].join(" ").toLowerCase().includes(searchTerm)).slice(0, 5) : [];
  const currentExperiment = experiments.find((item) => item.id === experimentId) ?? null;
  const selectedRun = runs.find((item) => item.runId === runId) ?? runs[0] ?? null;

  function go(next: View) { setView(next); setSearch(""); setNotice(""); }
  function selectRun(next: string) { setRunId(next); setView("runs"); }
  function openExperimentSearch(id: string) { setExperimentId(id); setCategory("All categories"); setDifficulty("All levels"); setView("experiments"); }

  async function executeExperiment(id: string) {
    setRunningId(id); setNotice("");
    try {
      const result = await apiPost<RunDetail, { warmupRuns: number; measuredRuns: number }>(`/api/experiments/${encodeURIComponent(id)}/run`, { warmupRuns: 3, measuredRuns: 10 });
      setRunId(result.runId); setDetail(result);
      setAnalysis({ runId: result.runId, findings: result.findings, recommendations: result.recommendations });
      setView("runs");
      setPlans(null);
      const refreshed = await apiGet<RunSummary[]>("/api/runs?latest=50");
      setRuns(refreshed);
      setNotice(`Run ${result.runId} completed and stored.`);
    } catch (error) { setNotice(message(error)); }
    finally { setRunningId(""); }
  }

  const activeNav = view === "advisor" || view === "plans" ? "runs" : view;
  const placeholder = view === "experiments" ? "Search names, tags, categories..." : view === "runs" ? "Search run ID, experiment, status..." : "Search experiments, runs, tags...";

  return <div className="app-shell">
    <aside className="sidebar">
      <div className="brand-lockup"><div className="brand-icon"><Activity size={19} strokeWidth={2.5} /></div><div><span className="brand-name">dugout</span><span className="brand-edition">PERFORMANCE LAB</span></div></div>
      <div className="workspace-switcher"><div className="workspace-avatar">SL</div><div className="workspace-copy"><strong>SalesLab</strong><span>Local workspace</span></div><ChevronDown size={15} /></div>
      <div className="nav-label">WORKSPACE</div>
      <nav className="primary-nav" aria-label="Main navigation">{navigation.map(({ id, label, icon: Icon }) => <button key={id} title={label} aria-label={label} className={`nav-link ${activeNav === id ? "active" : ""}`} onClick={() => go(id)}><Icon size={17} strokeWidth={1.8} /><span>{label}</span>{id === "runs" && runs.length > 0 && <span className="nav-count">{runs.length}</span>}</button>)}</nav>
      <div className="nav-label nav-label-secondary">ANALYSIS</div>
      <button className={`nav-link ${view === "advisor" ? "active" : ""}`} title="Advisor" aria-label="Advisor" onClick={() => go("advisor")}><Sparkles size={17} /><span>Advisor</span>{regressions.length > 0 && <span className="nav-alert">{regressions.length}</span>}</button>
      <button className={`nav-link ${view === "plans" ? "active" : ""}`} title="Execution plans" aria-label="Execution plans" onClick={() => go("plans")}><FileCode2 size={17} /><span>Execution plans</span></button>
      <div className="sidebar-spacer" />
      <div className="sidebar-dataset"><div className="dataset-top"><Database size={15} /><span>DATASET</span><span className="dataset-dot" /></div><strong>SalesLab / Orders</strong><span>1,000,000 rows   |  SQL Server</span></div>
      <div className="sidebar-footer"><div className="user-avatar">DB</div><div><strong>Developer</strong><span>Local environment</span></div><CircleHelp size={17} /></div>
    </aside>

    <div className="main-shell">
      <header className="topbar"><div className="topbar-context"><span>SalesLab</span><ChevronRight size={14} /><strong>{titles[view]}</strong></div><label className="global-search"><Search size={16} /><input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={placeholder} aria-label="Search experiments and runs" />{search ? <button onClick={() => setSearch("")} aria-label="Clear search"><X size={14} /></button> : <kbd><Command size={11} /> K</kbd>}</label>{searchTerm && !["experiments", "runs"].includes(view) && <SearchResults experiments={experimentMatches} runs={runMatches} onExperiment={openExperimentSearch} onRun={selectRun} />}<div className={`api-indicator ${apiReady ? "ready" : "offline"}`}><span className="status-light" /><span>{apiReady ? "API connected" : "API unavailable"}</span></div></header>
      <main className="content-area">
        {notice && <div className={`action-banner ${notice.includes("completed") ? "success" : "error"}`}><span>{notice}</span><button onClick={() => setNotice("")} aria-label="Dismiss message"><X size={15} /></button></div>}
        {view === "dashboard" && <Dashboard experiments={experiments} runs={runs} regressions={regressions} improvements={improvements} apiReady={apiReady} runsError={runsError} experimentId={experimentId} onExperiment={setExperimentId} onNavigate={go} onRun={selectRun} />}
        {view === "experiments" && <ExperimentExplorer experiments={filteredExperiments} categories={categories} difficulties={difficulties} category={category} difficulty={difficulty} runningId={runningId} onCategory={setCategory} onDifficulty={setDifficulty} onRun={executeExperiment} />}
        {view === "runs" && <RunsPage runs={filteredRuns} selectedRunId={runId} detail={detail} loading={detailLoading} error={runsError} onSelectRun={selectRun} onAdvisor={() => go("advisor")} onPlans={() => go("plans")} />}
        {view === "trends" && <TrendsPage experiments={experiments} experiment={currentExperiment} experimentId={experimentId} history={history} baseline={baseline} metric={metric} loading={historyLoading} error={historyError} onExperiment={setExperimentId} onMetric={setMetric} onOpenRun={selectRun} />}
        {view === "compare" && <ComparePage runs={runs} runA={runA} runB={runB} comparison={comparison} loading={compareLoading} error={compareError} onRunA={setRunA} onRunB={setRunB} onOpenRun={selectRun} />}
        {view === "advisor" && <AdvisorPage runs={runs} selectedRunId={runId || selectedRun?.runId || ""} analysis={analysis ?? (detail ? { runId: detail.runId, findings: detail.findings, recommendations: detail.recommendations } : null)} loading={detailLoading} onSelectRun={selectRun} />}
        {view === "plans" && <PlansPage runs={runs} selectedRunId={runId || selectedRun?.runId || ""} plans={plans} loading={detailLoading} onSelectRun={selectRun} />}
      </main>
      <footer className="page-footer"><span>DUGOUT ENGINEERING CONSOLE</span><span>LOCAL   |  {new Date().getFullYear()}</span></footer>
    </div>
  </div>;
}

function Dashboard({ experiments, runs, regressions, improvements, apiReady, runsError, experimentId, onExperiment, onNavigate, onRun }: {
  experiments: Experiment[]; runs: RunSummary[]; regressions: Change[]; improvements: Change[]; apiReady: boolean; runsError: string; experimentId: string;
  onExperiment: (id: string) => void; onNavigate: (view: View) => void; onRun: (id: string) => void;
}) {
  const samples = runs.filter((item) => item.experimentId === experimentId && item.status.toLowerCase() === "success").slice(0, 10).reverse();
  const latest = runs[0];
  const hosts = new Set(runs.map((item) => item.machineName).filter(Boolean)).size;
  return <>
    <Heading eyebrow={todayLabel()} title="Performance overview" description="Benchmark activity, changes, and signals across your SQL workload."><button className="button button-dark" onClick={() => onNavigate("experiments")}><Play size={15} fill="currentColor" /> Run experiment</button></Heading>
    {!apiReady && <div className="connection-notice"><span className="connection-mark"><AlertTriangle size={16} /></span><span><strong>API unavailable</strong><small>Start Dugout.Api to load experiment and benchmark data.</small></span><code>localhost:5080</code></div>}
    <div className="stats-grid"><Stat label="Experiments" value={experiments.length.toString()} detail="Available in library" icon={<Layers3 size={17} />} accent="lime" /><Stat label="Benchmark runs" value={runsError ? "N/A" : runs.length.toString()} detail={runsError ? "Repository unavailable" : "Recent stored executions"} icon={<Activity size={17} />} accent="blue" /><Stat label="Recent regressions" value={runsError ? "N/A" : regressions.length.toString()} detail="More than 10% slower" icon={<TrendingUp size={17} />} accent="coral" /><Stat label="Recent improvements" value={runsError ? "N/A" : improvements.length.toString()} detail="More than 10% faster" icon={<TrendingDown size={17} />} accent="green" /></div>
    <div className="dashboard-grid">
      <section className="panel trend-panel"><div className="panel-heading"><div><div className="section-kicker">PERFORMANCE PULSE</div><h2>Duration history</h2></div><select aria-label="Select experiment for dashboard chart" value={experimentId} onChange={(event) => onExperiment(event.target.value)}>{experiments.map((item) => <option key={item.id} value={item.id}>{item.id}   |  {item.name}</option>)}</select></div><div className="chart-legend"><span className="legend-line" /> Optimized duration <span className="chart-unit">ms / run</span></div>{samples.length > 1 ? <TrendChart runs={samples} metric="duration" /> : <ChartEmpty loading={!apiReady} />}<div className="chart-footer"><span>{samples.length} recent samples</span><button className="text-button" onClick={() => onNavigate("trends")}>Open trends <ArrowRight size={14} /></button></div></section>
      <section className="panel signals-panel"><div className="panel-heading"><div><div className="section-kicker">SIGNAL WATCH</div><h2>Recent changes</h2></div><button className="icon-button" onClick={() => onNavigate("advisor")} aria-label="Open advisor"><ArrowRight size={16} /></button></div>{runsError ? <InlineError message="Connect to the results repository to view run signals." /> : regressions.length + improvements.length === 0 ? <div className="quiet-empty"><div className="quiet-icon"><Check size={17} /></div><strong>No recent movement flagged</strong><span>Changes are calculated from the latest successful run pairs.</span></div> : <div className="signal-list">{[...regressions.slice(0, 3), ...improvements.slice(0, 2)].slice(0, 5).map((change) => <button key={change.run.runId} className="signal-row" onClick={() => onRun(change.run.runId)}><span className={`signal-mark ${change.percent > 0 ? "bad" : "good"}`}>{change.percent > 0 ? <ArrowUpRight size={15} /> : <ArrowDownRight size={15} />}</span><span className="signal-copy"><strong>{change.run.experimentId}</strong><span>{change.run.experimentName}</span></span><span className={`signal-value ${change.percent > 0 ? "negative" : "positive"}`}>{change.percent > 0 ? "+" : ""}{change.percent.toFixed(1)}%</span></button>)}</div>}<div className="signal-footer"><span>HEALTH CLASSIFICATION</span><span className="health-pill"><i /> {regressions.length ? "Review needed" : "Stable"}</span></div></section>
    </div>
    <div className="lower-grid"><section className="panel table-panel"><div className="panel-heading"><div><div className="section-kicker">EXECUTION LOG</div><h2>Recent runs</h2></div><button className="text-button" onClick={() => onNavigate("runs")}>All runs <ArrowRight size={14} /></button></div>{runsError ? <InlineError message="Run history is currently unavailable." /> : runs.length ? <RunTable runs={runs.slice(0, 6)} onOpenRun={onRun} /> : <EmptyState title="No benchmark runs yet" description="Run an experiment to start building your performance history." action="Browse experiments" onAction={() => onNavigate("experiments")} />}</section>
      <section className="panel environment-panel"><div className="panel-heading"><div><div className="section-kicker">COLLECTION ENVIRONMENT</div><h2>Environment summary</h2></div><Server size={17} className="muted-icon" /></div>{runsError ? <InlineError message="Environment metadata appears with stored runs." /> : latest ? <><div className="environment-host"><span className="host-avatar"><Database size={17} /></span><div><strong>{latest.sqlServerVersion ? latest.sqlServerVersion.split(/[\r\n]/)[0] : "SQL Server"}</strong><span>{latest.machineName || "Machine unavailable"}</span></div><span className="host-live"><i /> STORED</span></div><div className="environment-details"><div><span>EXPERIMENTS</span><strong>{experiments.length}</strong></div><div><span>HOSTS</span><strong>{hosts || 1}</strong></div><div><span>LATEST RUN</span><strong>{dateOnly(latest.startedUtc)}</strong></div></div><div className="environment-note"><Clock3 size={14} /> Last activity {timeAgo(latest.startedUtc)}</div></> : <div className="environment-empty"><Server size={20} /><span>Environment details appear after your first successful run.</span></div>}</section>
    </div>
  </>;
}

function ExperimentExplorer({ experiments, categories, difficulties, category, difficulty, runningId, onCategory, onDifficulty, onRun }: {
  experiments: Experiment[]; categories: string[]; difficulties: string[]; category: string; difficulty: string; runningId: string;
  onCategory: (value: string) => void; onDifficulty: (value: string) => void; onRun: (id: string) => void;
}) {
  return <><Heading eyebrow="EXPERIMENT LIBRARY" title="Find your next signal" description="Explore repeatable SQL Server scenarios and run them against SalesLab."><div className="library-count"><span>{String(experiments.length).padStart(2, "0")}</span> experiments</div></Heading>
    <div className="filter-row"><div className="filter-label"><ListFilter size={15} /> FILTERS</div><select value={category} onChange={(event) => onCategory(event.target.value)} aria-label="Filter by category">{categories.map((item) => <option key={item}>{item}</option>)}</select><select value={difficulty} onChange={(event) => onDifficulty(event.target.value)} aria-label="Filter by difficulty">{difficulties.map((item) => <option key={item}>{item}</option>)}</select><span className="filter-result">{experiments.length} shown</span></div>
    {!experiments.length ? <EmptyState title="No experiments match" description="Try another name, category, tag, or difficulty." /> : <div className="experiment-grid">{experiments.map((item) => <article className="experiment-card" key={item.id}><div className="experiment-card-top"><span className="experiment-code">{item.id}</span><span className="difficulty-tag">{item.difficulty}</span></div><h2>{item.name}</h2><p>{item.description || "Compare paired SQL variants against the SalesLab benchmark dataset."}</p><div className="experiment-meta"><span>{item.category}</span><span className="meta-dot" /><span>{item.learningObjectives.length} learning objectives</span></div><div className="tag-list">{item.tags.slice(0, 4).map((tag) => <span className="tag" key={tag}>{tag}</span>)}</div><div className="experiment-card-footer"><button className="button button-dark button-small" disabled={runningId === item.id} onClick={() => onRun(item.id)}>{runningId === item.id ? <LoaderCircle className="spin" size={14} /> : <Play size={13} fill="currentColor" />}{runningId === item.id ? "Running" : "Run benchmark"}</button><span className="explore-arrow"><ArrowUpRight size={16} /></span></div></article>)}</div>}
  </>;
}

function RunsPage({ runs, selectedRunId, detail, loading, error, onSelectRun, onAdvisor, onPlans }: { runs: RunSummary[]; selectedRunId: string; detail: RunDetail | null; loading: boolean; error: string; onSelectRun: (id: string) => void; onAdvisor: () => void; onPlans: () => void }) {
  return <><Heading eyebrow="EXECUTION LOG" title="Benchmark runs" description="Measured executions, query diagnostics, and result quality." />{error && <InlineError message={error} />}<section className="panel table-panel runs-table-panel"><div className="panel-heading"><div><div className="section-kicker">LATEST MEASURED EXECUTIONS</div><h2>Run history <span className="heading-count">{runs.length}</span></h2></div><span className="subtle-meta">Newest first</span></div>{runs.length ? <RunTable runs={runs} selectedRunId={selectedRunId} onOpenRun={onSelectRun} /> : <EmptyState title="No runs found" description="Try clearing the search or run an experiment." />}</section>
    {(loading || detail) && <section className="run-detail-section"><Heading eyebrow={detail?.runId || "LOADING RUN"} title={detail ? `${detail.experimentId}   |  ${detail.status}` : "Loading run details"} description={detail ? `Started ${dateTime(detail.startedUtc)}   |  ${detail.machineName || "Unknown machine"}` : "Fetching metrics, plans, and advisor results."}>{detail && <div className="detail-actions"><button className="button button-outline" onClick={onAdvisor}><Sparkles size={15} /> Advisor</button><button className="button button-outline" onClick={onPlans}><FileCode2 size={15} /> Plans</button></div>}</Heading>{detail ? <><div className="metric-cards"><Metric label="Duration" value={fmt(detail.optimized.durationMedianMs, " ms")} baseline={fmt(detail.baseline.durationMedianMs, " ms")} delta={delta(detail.baseline.durationMedianMs, detail.optimized.durationMedianMs)} /><Metric label="Logical reads" value={fmt(detail.optimized.logicalReadsMedian)} baseline={fmt(detail.baseline.logicalReadsMedian)} delta={delta(detail.baseline.logicalReadsMedian, detail.optimized.logicalReadsMedian)} /><Metric label="CPU time" value={fmt(detail.optimized.cpuTimeMedianMs, " ms")} baseline={fmt(detail.baseline.cpuTimeMedianMs, " ms")} delta={delta(detail.baseline.cpuTimeMedianMs, detail.optimized.cpuTimeMedianMs)} /><Metric label="SQL elapsed" value={fmt(detail.optimized.sqlElapsedTimeMedianMs, " ms")} baseline={fmt(detail.baseline.sqlElapsedTimeMedianMs, " ms")} delta={delta(detail.baseline.sqlElapsedTimeMedianMs, detail.optimized.sqlElapsedTimeMedianMs)} /></div><div className="dashboard-grid detail-grid"><section className="panel"><div className="panel-heading"><div><div className="section-kicker">RESOURCE FOOTPRINT</div><h2>Advanced metrics</h2></div><Gauge size={18} className="muted-icon" /></div><div className="resource-grid"><Resource label="Requested grant" value={detail.optimized.requestedMemoryMedianKb} unit="KB" /><Resource label="Granted grant" value={detail.optimized.grantedMemoryMedianKb} unit="KB" /><Resource label="Used grant" value={detail.optimized.usedMemoryMedianKb} unit="KB" /><Resource label="TempDB pages" value={detail.optimized.tempDbPagesMedian} /><Resource label="Worktable reads" value={detail.optimized.worktableLogicalReadsMedian} /><Resource label="Degree of parallelism" value={detail.optimized.degreeOfParallelismMedian} /></div></section><section className="panel"><div className="panel-heading"><div><div className="section-kicker">RESULT QUALITY</div><h2>Validation</h2></div><span className={`validation-mark ${detail.validationPassed ? "pass" : "fail"}`}>{detail.validationPassed ? <Check size={16} /> : <AlertTriangle size={16} />}</span></div><p className="validation-copy">{detail.validationDetails}</p><div className="validation-meta"><span>EXIT CODE</span><strong>{detail.exitCode}</strong><span>RUN HASH</span><code>{detail.experimentHash.slice(0, 12)}</code></div>{detail.failureReason && <InlineError message={detail.failureReason} />}</section></div></> : <Loading />}</section>}
  </>;
}

function TrendsPage({ experiments, experiment, experimentId, history, baseline, metric, loading, error, onExperiment, onMetric, onOpenRun }: { experiments: Experiment[]; experiment: Experiment | null; experimentId: string; history: RunSummary[]; baseline: Baseline | null; metric: TrendMetric; loading: boolean; error: string; onExperiment: (id: string) => void; onMetric: (metric: TrendMetric) => void; onOpenRun: (id: string) => void }) {
  const options: { id: TrendMetric; label: string; key: keyof RunSummary; unit: string }[] = [{ id: "duration", label: "Duration", key: "optimizedDurationMedianMs", unit: "ms" }, { id: "reads", label: "Logical reads", key: "optimizedLogicalReadsMedian", unit: "reads" }, { id: "cpu", label: "CPU time", key: "optimizedCpuTimeMedianMs", unit: "ms" }];
  const selected = options.find((item) => item.id === metric)!;
  const points = [...history].reverse().filter((run) => run.status.toLowerCase() === "success" && typeof run[selected.key] === "number");
  const baselineValue = baseline?.metrics[metric === "duration" ? "Duration (ms)" : metric === "reads" ? "Logical reads" : "CPU time (ms)"] ?? undefined;
  return <><Heading eyebrow="REGRESSION & TREND ANALYSIS" title="History tells the story" description="Compare measured performance across successful runs and spot drift early."><select className="header-select" value={experimentId} onChange={(event) => onExperiment(event.target.value)} aria-label="Choose experiment for trends">{experiments.map((item) => <option key={item.id} value={item.id}>{item.id}   |  {item.name}</option>)}</select></Heading>
    <div className="segmented metric-segment" role="tablist" aria-label="Trend metric">{options.map((item) => <button key={item.id} className={metric === item.id ? "selected" : ""} role="tab" aria-selected={metric === item.id} onClick={() => onMetric(item.id)}>{item.label}</button>)}</div>{error && <InlineError message={error} />}
    <section className="panel large-trend-panel"><div className="panel-heading"><div><div className="section-kicker">{experiment?.id || "EXPERIMENT"}   |  {selected.unit.toUpperCase()}</div><h2>{selected.label} over time</h2></div><div className="trend-current"><span>LATEST MEDIAN</span><strong>{fmt(points.at(-1)?.[selected.key] as number | null, selected.unit === "reads" ? "" : ` ${selected.unit}`)}</strong></div></div>{loading ? <Loading /> : points.length > 1 ? <TrendChart runs={points} metric={metric} baseline={typeof baselineValue === "number" ? baselineValue : undefined} /> : <ChartEmpty loading={false} />}<div className="chart-footer"><span>{points.length} successful samples   |  {baseline ? `Baseline ${baseline.runId}` : "No baseline set"}</span><span className="chart-unit">Values are optimized medians</span></div></section>
    <section className="panel table-panel trend-history-panel"><div className="panel-heading"><div><div className="section-kicker">RUN-BY-RUN</div><h2>History</h2></div><span className="subtle-meta">{history.length} records</span></div>{history.length ? <RunTable runs={history} onOpenRun={onOpenRun} /> : <EmptyState title="No historical runs" description="Run this experiment to begin recording a trend." />}</section>
  </>;
}

function ComparePage({ runs, runA, runB, comparison, loading, error, onRunA, onRunB, onOpenRun }: { runs: RunSummary[]; runA: string; runB: string; comparison: RunComparison | null; loading: boolean; error: string; onRunA: (id: string) => void; onRunB: (id: string) => void; onOpenRun: (id: string) => void }) {
  const metrics = comparison?.optimizedMetrics.filter((item) => /duration|logical reads|cpu time|elapsed/i.test(item.metric)) ?? [];
  return <><Heading eyebrow="SIDE-BY-SIDE ANALYSIS" title="Compare benchmark runs" description="Separate query changes from normal run-to-run variation." /><div className="compare-pickers"><RunPicker label="RUN A   |  REFERENCE" runs={runs} value={runA} onChange={onRunA} /><div className="compare-glyph"><ArrowLeftRight size={18} /></div><RunPicker label="RUN B   |  CURRENT" runs={runs} value={runB} onChange={onRunB} /></div>{error && <InlineError message={error} />}{loading ? <Loading /> : comparison ? <><div className="comparison-metrics">{metrics.map((item) => <ComparisonCard key={item.metric} metric={item} />)}</div><section className="panel plan-diff-panel"><div className="panel-heading"><div><div className="section-kicker">EXECUTION PLAN</div><h2>Plan difference</h2></div><button className="text-button" onClick={() => onOpenRun(runB)}>Inspect run <ArrowRight size={14} /></button></div><PlanDiff label="Baseline plan" comparison={comparison.baselinePlan} /><PlanDiff label="Optimized plan" comparison={comparison.optimizedPlan} /></section></> : <EmptyState title={runs.length < 2 ? "Two stored runs are needed" : "Choose two runs to compare"} description={error || "Select a reference run and a current run above."} />}</>;
}

function AdvisorPage({ runs, selectedRunId, analysis, loading, onSelectRun }: { runs: RunSummary[]; selectedRunId: string; analysis: AdvisorAnalysisResponse | null; loading: boolean; onSelectRun: (id: string) => void }) {
  return <><Heading eyebrow="DETERMINISTIC ANALYSIS" title="Advisor findings" description="Evidence-backed conclusions from query metrics, plans, and history."><RunPicker label="SELECT RUN" runs={runs} value={selectedRunId} onChange={onSelectRun} /></Heading>{loading ? <Loading /> : <div className="advisor-layout"><section className="panel advisor-findings"><div className="panel-heading"><div><div className="section-kicker">OBSERVED EVIDENCE</div><h2>Findings <span className="heading-count">{analysis?.findings.length ?? 0}</span></h2></div><Sparkles size={18} className="muted-icon" /></div>{analysis?.findings.length ? <div className="finding-list">{analysis.findings.map((item, index) => <article className="finding-item" key={`${item.rule}-${index}`}><div className={`finding-severity ${item.severity.toLowerCase()}`}><span /></div><div className="finding-body"><div className="finding-meta"><span className="rule-label">{item.rule.replace(/([A-Z])/g, " $1").toUpperCase()}</span><span className="severity-label">{item.severity}</span></div><h3>{item.finding}</h3><p>{item.evidence}</p></div></article>)}</div> : <EmptyState title="No rules fired for this run" description="The advisor only reports conclusions supported by measured evidence." />}</section><aside className="panel recommendation-panel"><div className="panel-heading"><div><div className="section-kicker">NEXT CONSIDERATIONS</div><h2>Recommendations</h2></div><Sparkles size={17} className="muted-icon" /></div>{analysis?.recommendations.length ? <div className="recommendation-list">{analysis.recommendations.map((item, index) => <article className="recommendation-item" key={`${item.rule}-${index}`}><span className="recommendation-number">{String(index + 1).padStart(2, "0")}</span><div><p>{item.recommendation}</p><div className="confidence-line"><span>{item.rule}</span><span>{Math.round(item.confidence * 100)}% confidence</span></div></div></article>)}</div> : <div className="quiet-empty"><div className="quiet-icon"><Check size={17} /></div><strong>No recommendation available</strong><span>Additional recommendations appear when their evidence is present.</span></div>}</aside></div>}</>;
}

function PlansPage({ runs, selectedRunId, plans, loading, onSelectRun }: { runs: RunSummary[]; selectedRunId: string; plans: ExecutionPlans | null; loading: boolean; onSelectRun: (id: string) => void }) {
  return <><Heading eyebrow="QUERY PLAN INSPECTOR" title="Execution plans" description="Review captured plans, operator counts, metadata, and raw Showplan XML."><RunPicker label="SELECT RUN" runs={runs} value={selectedRunId} onChange={onSelectRun} /></Heading>{loading ? <Loading /> : plans ? <div className="plan-columns"><PlanCard title="Baseline" plan={plans.baseline} /><PlanCard title="Optimized" plan={plans.optimized} /></div> : <EmptyState title="Choose a stored run" description="Execution plans are attached to completed benchmark runs." />}</>;
}

function PlanCard({ title, plan }: { title: string; plan: ExecutionPlans["baseline"] }) {
  if (!plan) return <section className="panel plan-card"><div className="panel-heading"><div><div className="section-kicker">{title.toUpperCase()}</div><h2>Plan unavailable</h2></div></div><p className="muted-copy">This run does not have a stored plan for this variant.</p></section>;
  const operators = Object.entries(plan.operators).sort((a, b) => b[1] - a[1]);
  return <section className="panel plan-card"><div className="panel-heading"><div><div className="section-kicker">{title.toUpperCase()}</div><h2>Operator summary</h2></div><span className="plan-hash-label">SHA-256</span></div><div className="hash-box"><span>PLAN HASH</span><code>{plan.planHash}</code></div><div className="plan-metadata"><div><span>ESTIMATED COST</span><strong>{fmt(plan.estimatedCost)}</strong></div><div><span>ESTIMATED ROWS</span><strong>{fmt(plan.estimatedRows)}</strong></div><div><span>SUBTREE COST</span><strong>{fmt(plan.estimatedSubtreeCost)}</strong></div></div><div className="operator-list"><div className="operator-list-heading"><span>PHYSICAL OPERATOR</span><span>COUNT</span></div>{operators.map(([operator, count]) => <div className="operator-row" key={operator}><span>{operator}</span><strong>{count}</strong></div>)}</div><details className="xml-disclosure"><summary><FileCode2 size={15} /> Raw Showplan XML <ChevronDown size={15} /></summary><pre>{plan.planXml}</pre></details></section>;
}

function Heading({ eyebrow, title, description, children }: { eyebrow: string; title: string; description: string; children?: ReactNode }) {
  return <div className="page-heading"><div><div className="page-eyebrow">{eyebrow}</div><h1>{title}</h1><p>{description}</p></div>{children && <div className="heading-actions">{children}</div>}</div>;
}
function Stat({ label, value, detail, icon, accent }: { label: string; value: string; detail: string; icon: ReactNode; accent: string }) { return <article className={`stat-card accent-${accent}`}><div className="stat-top"><span>{label}</span><span className="stat-icon">{icon}</span></div><strong className="stat-value">{value}</strong><span className="stat-detail">{detail}</span></article>; }
function Metric({ label, value, baseline, delta: change }: { label: string; value: string; baseline: string; delta: number | null }) { return <article className="metric-card"><span className="metric-label">{label}</span><strong>{value}</strong><div className="metric-comparison"><span>Baseline {baseline}</span>{change !== null && <span className={change <= 0 ? "delta-good" : "delta-bad"}>{change > 0 ? "+" : ""}{change.toFixed(1)}%</span>}</div></article>; }
function Resource({ label, value, unit = "" }: { label: string; value: number | null; unit?: string }) { return <div className="resource-metric"><span>{label}</span><strong>{fmt(value, unit ? ` ${unit}` : "")}</strong></div>; }
function RunTable({ runs, selectedRunId, onOpenRun }: { runs: RunSummary[]; selectedRunId?: string; onOpenRun: (id: string) => void }) { return <div className="table-scroll"><table className="run-table"><thead><tr><th>RUN / EXPERIMENT</th><th>STATUS</th><th>DURATION</th><th>LOGICAL READS</th><th>STARTED</th><th /></tr></thead><tbody>{runs.map((run) => <tr key={run.runId} className={selectedRunId === run.runId ? "selected-row" : ""} onClick={() => onOpenRun(run.runId)} tabIndex={0} onKeyDown={(event) => { if (event.key === "Enter") onOpenRun(run.runId); }}><td><div className="run-id-cell"><span className="run-avatar">{run.experimentId.slice(-2)}</span><span><strong>{run.runId}</strong><small>{run.experimentId}   |  {run.experimentName}</small></span></div></td><td><Status status={run.status} /></td><td className="mono-cell">{fmt(run.optimizedDurationMedianMs, " ms")}</td><td className="mono-cell">{fmt(run.optimizedLogicalReadsMedian)}</td><td className="date-cell">{dateTime(run.startedUtc)}</td><td><ChevronRight className="row-chevron" size={15} /></td></tr>)}</tbody></table></div>; }
function Status({ status }: { status: string }) { const value = status.toLowerCase(); return <span className={`status-pill ${value === "success" ? "success" : value === "failed" ? "failed" : "running"}`}><i />{status}</span>; }

function TrendChart({ runs, metric, baseline }: { runs: RunSummary[]; metric: TrendMetric; baseline?: number }) {
  const key: keyof RunSummary = metric === "reads" ? "optimizedLogicalReadsMedian" : metric === "cpu" ? "optimizedCpuTimeMedianMs" : "optimizedDurationMedianMs";
  const values = runs.map((item) => item[key]).filter((item): item is number => typeof item === "number");
  if (values.length < 2) return <ChartEmpty loading={false} />;
  const width = 760, height = 220, padX = 14, padY = 24;
  const min = Math.min(...values, ...(baseline === undefined ? [] : [baseline]));
  const max = Math.max(...values, ...(baseline === undefined ? [] : [baseline]));
  const spread = max - min || Math.abs(max) * 0.12 || 1, low = min - spread * 0.12, high = max + spread * 0.12;
  const points = values.map((value, index) => ({ x: padX + index / (values.length - 1) * (width - padX * 2), y: height - padY - (value - low) / (high - low) * (height - padY * 2), value }));
  const baselineY = baseline === undefined ? null : height - padY - (baseline - low) / (high - low) * (height - padY * 2);
  return <div className="trend-chart-wrap"><svg className="trend-chart" viewBox={`0 0 ${width} ${height}`} role="img" aria-label={`${metric} trend across ${values.length} runs`}>
    {[0, 1, 2, 3].map((index) => { const y = padY + index * ((height - padY * 2) / 3); return <g key={index}><line x1={padX} x2={width - padX} y1={y} y2={y} className="chart-gridline" /><text x={padX} y={y - 5} className="chart-axis-label">{(high - ((high - low) * index) / 3).toLocaleString(undefined, { maximumFractionDigits: 0 })}</text></g>; })}
    {baselineY !== null && <g><line x1={padX} x2={width - padX} y1={baselineY} y2={baselineY} className="baseline-line" /><text x={width - padX} y={baselineY - 6} textAnchor="end" className="baseline-label">BASELINE</text></g>}
    <polyline points={points.map((point) => `${point.x},${point.y}`).join(" ")} className="trend-line" />
    {points.map((point, index) => <circle key={index} cx={point.x} cy={point.y} r={index === points.length - 1 ? 5 : 3.5} className={index === points.length - 1 ? "chart-point current" : "chart-point"}><title>{point.value.toLocaleString()}   |  {runs[index]?.runId}</title></circle>)}
    <text x={padX} y={height - 4} className="chart-axis-label">{runs[0] ? dateOnly(runs[0].startedUtc) : ""}</text><text x={width - padX} y={height - 4} textAnchor="end" className="chart-axis-label">{runs.at(-1) ? dateOnly(runs.at(-1)!.startedUtc) : ""}</text>
  </svg></div>;
}
function ChartEmpty({ loading }: { loading: boolean }) { return <div className="chart-empty">{loading ? <LoaderCircle className="spin" size={19} /> : <BarChart3 size={19} />}<span>{loading ? "Loading measured history" : "At least two measured runs are needed to draw a trend."}</span></div>; }
function RunPicker({ label, runs, value, onChange }: { label: string; runs: RunSummary[]; value: string; onChange: (id: string) => void }) { return <label className="run-picker"><span>{label}</span><select value={value} onChange={(event) => onChange(event.target.value)} aria-label={label}>{runs.map((run) => <option key={run.runId} value={run.runId}>{run.runId}   |  {run.experimentId}   |  {dateOnly(run.startedUtc)}</option>)}</select><ChevronDown size={14} /></label>; }
function ComparisonCard({ metric }: { metric: MetricComparison }) { const positive = metric.improvementPercent !== null && metric.improvementPercent > 0; return <article className="comparison-card"><span>{metric.metric}</span><div className="comparison-values"><strong>{fmt(metric.previousMedian)}</strong><ArrowRight size={15} /><strong>{fmt(metric.currentMedian)}</strong></div><div className="comparison-footer"><span>Run A to Run B</span><span className={positive ? "delta-good" : "delta-bad"}>{metric.improvementPercent === null ? "n/a" : `${positive ? "+" : ""}${metric.improvementPercent.toFixed(1)}%`}</span></div></article>; }
function PlanDiff({ label, comparison }: { label: string; comparison: RunComparison["baselinePlan"] }) { return <div className="plan-comparison"><div><strong>{label}</strong><span className={`plan-match ${comparison?.planHashesMatch ? "match" : "change"}`}>{comparison ? comparison.planHashesMatch ? "Hash match" : "Plan changed" : "Unavailable"}</span></div>{comparison?.operatorChanges.length ? <div className="operator-changes">{comparison.operatorChanges.map((item) => <span key={item.physicalOperator}>{item.physicalOperator}<b>{item.previousCount} to {item.currentCount}</b></span>)}</div> : <p>{comparison ? "Operator counts unchanged." : "Plan metadata unavailable for one of these runs."}</p>}</div>; }
function SearchResults({ experiments, runs, onExperiment, onRun }: { experiments: Experiment[]; runs: RunSummary[]; onExperiment: (id: string) => void; onRun: (id: string) => void }) {
  return <div className="search-results" role="region" aria-label="Search results">
    <div className="search-result-section"><div className="search-result-heading"><Layers3 size={13} /> EXPERIMENTS <span>{experiments.length}</span></div>{experiments.length ? experiments.map((item) => <button key={item.id} className="search-result-row" onClick={() => onExperiment(item.id)}><span className="search-result-code">{item.id}</span><span><strong>{item.name}</strong><small>{item.category} | {item.tags.join(", ")}</small></span><ArrowRight size={14} /></button>) : <p className="search-no-result">No matching experiments.</p>}</div>
    <div className="search-result-section"><div className="search-result-heading"><Activity size={13} /> RUNS <span>{runs.length}</span></div>{runs.length ? runs.map((item) => <button key={item.runId} className="search-result-row" onClick={() => onRun(item.runId)}><span className="search-result-code">{item.runId}</span><span><strong>{item.experimentName}</strong><small>{item.experimentId} | {dateTime(item.startedUtc)}</small></span><ArrowRight size={14} /></button>) : <p className="search-no-result">No matching runs.</p>}</div>
  </div>;
}
function EmptyState({ title, description, action, onAction }: { title: string; description: string; action?: string; onAction?: () => void }) { return <div className="empty-state"><div className="empty-state-icon"><BarChart3 size={19} /></div><strong>{title}</strong><p>{description}</p>{action && onAction && <button className="button button-outline button-small" onClick={onAction}>{action}<ArrowRight size={14} /></button>}</div>; }
function InlineError({ message: text }: { message: string }) { return <div className="inline-error"><AlertTriangle size={15} /><span>{text}</span></div>; }
function Loading() { return <div className="loading-block"><LoaderCircle className="spin" size={18} /><span>Loading benchmark data</span></div>; }
function runChanges(runs: RunSummary[]): Change[] { const groups = new Map<string, RunSummary[]>(); for (const run of runs.filter((item) => item.status.toLowerCase() === "success")) groups.set(run.experimentId, [...(groups.get(run.experimentId) ?? []), run]); const changes: Change[] = []; for (const group of groups.values()) { const ordered = [...group].sort((a, b) => Date.parse(a.startedUtc) - Date.parse(b.startedUtc)); if (ordered.length < 2) continue; const prior = ordered.at(-2)!, run = ordered.at(-1)!, percent = delta(prior.optimizedDurationMedianMs, run.optimizedDurationMedianMs); if (percent !== null) changes.push({ run, prior, percent }); } return changes.sort((a, b) => Date.parse(b.run.startedUtc) - Date.parse(a.run.startedUtc)); }
function delta(prior: number | null, current: number | null) { return prior === null || current === null || prior === 0 ? null : (current - prior) / Math.abs(prior) * 100; }
function fmt(value: number | null | undefined, unit = "") { return value === null || value === undefined || !Number.isFinite(value) ? "n/a" : `${value.toLocaleString(undefined, { maximumFractionDigits: value < 10 ? 2 : 1 })}${unit}`; }
function dateOnly(value: string) { const date = new Date(value); return Number.isNaN(date.valueOf()) ? "N/A" : new Intl.DateTimeFormat(undefined, { month: "short", day: "2-digit" }).format(date); }
function dateTime(value: string) { const date = new Date(value); return Number.isNaN(date.valueOf()) ? "N/A" : new Intl.DateTimeFormat(undefined, { month: "short", day: "2-digit", hour: "2-digit", minute: "2-digit" }).format(date); }
function timeAgo(value: string) { const elapsed = Date.now() - Date.parse(value); if (!Number.isFinite(elapsed) || elapsed < 0) return "time unavailable"; const hours = Math.floor(elapsed / 3_600_000); return hours < 1 ? "less than an hour ago" : hours < 24 ? `${hours} hours ago` : `${Math.floor(hours / 24)} days ago`; }
function todayLabel() { return new Intl.DateTimeFormat(undefined, { weekday: "long", month: "long", day: "2-digit", year: "numeric", timeZone: "UTC" }).format(new Date()).toUpperCase(); }
function message(error: unknown) { return error instanceof ApiError ? error.message : error instanceof Error ? error.message : "The request could not be completed."; }
