namespace ServerSleuth.Reporting.Html;

/// <summary>
/// The single inline stylesheet the whole HTML report uses — see skill.md (Phase 9B) §3, §19,
/// §21. No external stylesheet, font, or framework of any kind; every rule here is self-
/// contained and works fully offline. Font stack explicitly includes Traditional Chinese fonts
/// (Microsoft JhengHei, Noto Sans TC) to ensure zh-TW labels and any Chinese data values
/// render correctly on Windows Server, Chrome, and Edge.
/// </summary>
internal static class HtmlDocumentStyles
{
    public const string Css = """
        :root {
          color-scheme: light;
          --bg: #f5f6f8;
          --panel: #ffffff;
          --border: #d7dbe0;
          --text: #1c2430;
          --muted: #5b6472;
          --accent: #2b5fb0;
        }
        * { box-sizing: border-box; }
        body {
          margin: 0;
          padding: 1.5rem;
          background: var(--bg);
          color: var(--text);
          font-family: "Microsoft JhengHei", "Noto Sans TC", -apple-system, "Segoe UI", Helvetica, Arial, sans-serif;
          line-height: 1.45;
        }
        main { max-width: 72rem; margin: 0 auto; }
        h1, h2, h3, h4 { line-height: 1.25; margin: 0 0 0.5rem; }
        h1 { font-size: 1.6rem; }
        h2 { font-size: 1.25rem; margin-top: 2rem; border-bottom: 1px solid var(--border); padding-bottom: 0.35rem; }
        h3 { font-size: 1.05rem; margin-top: 1.25rem; }
        p { margin: 0.35rem 0; }
        section { margin-bottom: 1.5rem; }
        a { color: inherit; }

        /* ── Panels ─────────────────────────────────────────────── */
        .panel {
          background: var(--panel);
          border: 1px solid var(--border);
          border-radius: 8px;
          padding: 1rem 1.25rem;
          margin-bottom: 0.75rem;
        }
        .muted { color: var(--muted); font-size: 0.9rem; }

        /* ── Stat grid ───────────────────────────────────────────── */
        .grid {
          display: grid;
          grid-template-columns: repeat(auto-fit, minmax(11rem, 1fr));
          gap: 0.75rem;
        }
        .stat {
          background: var(--panel);
          border: 1px solid var(--border);
          border-radius: 8px;
          padding: 0.75rem 1rem;
        }
        .stat .value { font-size: 1.4rem; font-weight: 600; }
        .stat .label { color: var(--muted); font-size: 0.82rem; text-transform: uppercase; letter-spacing: 0.03em; }

        /* ── Clickable stat cards (inventory dashboard) ──────────── */
        .stat-link {
          text-decoration: none;
          display: block;
          transition: transform 0.1s ease, box-shadow 0.1s ease;
        }
        .stat-link:hover {
          transform: translateY(-2px);
          box-shadow: 0 4px 10px rgba(0,0,0,0.12);
          border-color: var(--accent);
        }
        #inventory-dashboard .grid {
          grid-template-columns: repeat(auto-fill, minmax(9rem, 1fr));
        }

        /* ── Migration Assessment separator ─────────────────────── */
        #migration-assessment {
          margin-top: 2.5rem;
          border-top: 3px solid var(--accent);
          padding-top: 1rem;
        }

        /* ── Tables ──────────────────────────────────────────────── */
        table { width: 100%; border-collapse: collapse; font-size: 0.9rem; }
        th, td { text-align: left; padding: 0.4rem 0.6rem; border-bottom: 1px solid var(--border); vertical-align: top; }
        th { color: var(--muted); font-weight: 600; font-size: 0.8rem; text-transform: uppercase; letter-spacing: 0.02em; }
        code { background: #eef0f3; padding: 0.05rem 0.3rem; border-radius: 4px; font-size: 0.85em; word-break: break-all; }
        ul.tags { list-style: none; margin: 0.15rem 0; padding: 0; display: flex; flex-wrap: wrap; gap: 0.3rem; }
        ul.tags li { background: #eef0f3; border-radius: 999px; padding: 0.1rem 0.6rem; font-size: 0.78rem; }

        /* ── Collapsible details ─────────────────────────────────── */
        details { margin: 0.4rem 0; }
        details > summary { cursor: pointer; font-weight: 600; }

        /* ── Severity / status badges ────────────────────────────── */
        .badge { display: inline-block; padding: 0.15rem 0.6rem; border-radius: 999px; font-size: 0.78rem; font-weight: 600; color: #fff; }
        .status-blocked, .severity-critical { background: #b3261e; }
        .status-needs-remediation, .severity-high { background: #c56a00; }
        .status-ready-with-conditions, .severity-medium { background: #9a7b00; }
        .status-ready, .severity-none { background: #2e7d32; }
        .severity-low { background: #4d7c9e; }
        .severity-info { background: #6b7280; }
        .coverage-complete { background: #2e7d32; }
        .coverage-partial { background: #9a7b00; }
        .coverage-limited { background: #c56a00; }
        .coverage-unknown { background: #6b7280; }
        .impact-blocking { background: #b3261e; }
        .impact-remediation-required { background: #c56a00; }
        .impact-conditional { background: #9a7b00; }
        .impact-informational { background: #4d7c9e; }
        .impact-unclassified { background: #6b7280; }
        .priority-critical { background: #b3261e; }
        .priority-high { background: #c56a00; }
        .priority-medium { background: #9a7b00; }
        .priority-low { background: #4d7c9e; }
        .priority-informational { background: #6b7280; }

        /* ── Category badges ─────────────────────────────────────── */
        .cat-badge { display: inline-block; padding: 0.1rem 0.5rem; border-radius: 4px; font-size: 0.75rem; font-weight: 600; }
        .cat-system    { background: #e3f2fd; color: #1565c0; }
        .cat-microsoft { background: #e8f5e9; color: #2e7d32; }
        .cat-third-party { background: #fce4ec; color: #880e4f; }
        .cat-custom    { background: #fff3e0; color: #e65100; }

        /* ── Inline status text (service running/stopped) ────────── */
        .status-running  { color: #2e7d32; font-weight: 600; }
        .status-stopped  { color: #b3261e; }
        .status-paused   { color: #9a7b00; }
        .status-enabled  { color: #2e7d32; }
        .status-disabled { color: var(--muted); }
        .status-unknown  { color: var(--muted); }

        /* ── Inventory controls (filter buttons + search) ────────── */
        .inventory-controls {
          display: flex;
          flex-wrap: wrap;
          gap: 0.5rem;
          align-items: center;
          margin: 0.5rem 0 0.75rem;
        }
        .filter-group { display: flex; gap: 0.25rem; flex-wrap: wrap; }
        .filter-btn {
          padding: 0.2rem 0.65rem;
          border: 1px solid var(--border);
          border-radius: 999px;
          background: var(--panel);
          color: var(--muted);
          font-size: 0.78rem;
          cursor: pointer;
          transition: background 0.12s, color 0.12s, border-color 0.12s;
          font-family: inherit;
        }
        .filter-btn:hover { background: var(--bg); color: var(--text); }
        .filter-btn.active { background: var(--accent); color: #fff; border-color: var(--accent); }
        .search-box {
          padding: 0.22rem 0.55rem;
          border: 1px solid var(--border);
          border-radius: 4px;
          font-size: 0.85rem;
          font-family: inherit;
          width: 14rem;
          background: var(--panel);
        }
        .search-box:focus { outline: 2px solid var(--accent); outline-offset: 1px; border-color: transparent; }

        /* ── Inventory stats summary line ────────────────────────── */
        .inventory-stats {
          display: flex;
          flex-wrap: wrap;
          gap: 1rem;
          font-size: 0.85rem;
          color: var(--muted);
          margin-bottom: 0.35rem;
        }

        /* ── Evidence & misc ─────────────────────────────────────── */
        .evidence-list { list-style: none; margin: 0.25rem 0 0; padding: 0; font-size: 0.82rem; }
        .evidence-list li { padding: 0.2rem 0; border-top: 1px dashed var(--border); }
        .evidence-list li:first-child { border-top: none; }
        .empty { color: var(--muted); font-style: italic; font-size: 0.9rem; }

        /* ── Responsive ──────────────────────────────────────────── */
        @media (max-width: 40rem) {
          body { padding: 0.75rem; }
          .grid { grid-template-columns: 1fr 1fr; }
          table, thead, tbody, th, td, tr { display: block; }
          th { display: none; }
          td { border-bottom: none; padding: 0.15rem 0; }
          tr { border-bottom: 1px solid var(--border); padding-bottom: 0.4rem; margin-bottom: 0.4rem; }
          .inventory-controls { flex-direction: column; align-items: flex-start; }
          .search-box { width: 100%; }
        }
        """;
}
