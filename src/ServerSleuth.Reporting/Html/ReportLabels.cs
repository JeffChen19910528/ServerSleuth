namespace ServerSleuth.Reporting.Html;

/// <summary>
/// Single source of truth for all HTML-report UI labels in English and Traditional Chinese.
/// Data values (server names, file paths, software names, service names) are never stored here —
/// only static UI labels, headings, column names, filter labels, and category display strings.
/// En strings match the hardcoded strings used before i18n was added, preserving existing test
/// expectations when <see cref="ReportLanguage.En"/> is in effect.
/// </summary>
internal static class ReportLabels
{
    private static readonly Dictionary<string, (string En, string ZhTw)> Labels =
        new(StringComparer.Ordinal)
        {
            // Page — Server Deployment Inventory (report redesign)
            ["page-title"] = ("Server Deployment Inventory", "Server 部署清冊"),
            ["server-info"] = ("Server Information", "伺服器資訊"),
            ["summary"] = ("Summary", "統計摘要"),
            ["scan-time"] = ("Scan Time:", "掃描時間："),
            ["show-system-components"] = ("Show System Components", "顯示系統元件"),
            ["stat-application-components"] = ("Application Components", "程式元件"),
            ["application-components"] = ("Application Components", "程式元件"),
            ["business-scheduled-tasks"] = ("Business Scheduled Tasks", "企業排程工作"),
            ["application-runtime"] = ("Application Runtime", "程式執行環境"),
            ["application-database-refs"] = ("Application Database References", "程式使用的資料庫"),
            ["col-server-name"] = ("Server Name:", "伺服器名稱："),
            ["col-operating-system"] = ("Operating System:", "作業系統："),
            ["col-component"] = ("Component", "元件"),
            ["col-database"] = ("Database", "資料庫"),
            ["col-certificate"] = ("Certificate", "憑證"),
            ["col-configuration-file"] = ("Configuration File", "設定檔"),
            ["col-external-connection"] = ("External Connection", "外部連線"),
            ["col-runtime"] = ("Runtime", "執行環境"),
            ["app-type-iis"] = ("IIS Application", "IIS 應用程式"),
            ["app-type-service"] = ("Windows Service", "Windows 服務"),
            ["app-type-console"] = ("Console Application", "主控台應用程式"),
            ["status-enabled"] = ("Enabled", "已啟用"),
            ["status-disabled"] = ("Disabled", "已停用"),
            ["status-running"] = ("Running", "執行中"),
            ["status-stopped"] = ("Stopped", "已停止"),
            ["status-installed"] = ("Installed", "已安裝"),
            ["status-configured"] = ("Configured", "已設定"),
            ["cls-system"] = ("System", "系統"),
            ["cls-third-party"] = ("Third-party", "第三方"),
            ["cls-business"] = ("Business", "業務系統"),
            ["cls-custom"] = ("Custom", "自訂"),
            ["cls-unknown"] = ("Unknown", "未知"),

            // Legacy inventory-overview labels (kept for any remaining internal reference)
            ["inventory-overview"] = ("Server Inventory Overview", "伺服器資產總覽"),
            ["server-overview"] = ("Server Overview", "伺服器總覽"),

            // Inventory section titles
            ["applications"] = ("Applications", "應用程式"),
            ["dll-binaries"] = ("Application Components (DLL / Binary)", "應用程式元件 (DLL / 執行檔)"),
            ["windows-services"] = ("Windows Services", "Windows 服務"),
            ["com-components"] = ("COM Components", "COM 元件"),
            ["installed-software"] = ("Installed Software", "已安裝軟體"),
            ["runtime-requirements"] = ("Runtime Requirements", "執行環境需求"),
            ["scheduled-tasks"] = ("Scheduled Tasks", "排程工作"),
            ["certificates"] = ("Certificates", "憑證"),
            ["configuration-files"] = ("Configuration Files", "設定檔"),
            ["external-connections"] = ("External Connections", "外部連線"),

            // Dashboard stat labels
            ["stat-applications"] = ("Applications", "應用程式"),
            ["stat-software"] = ("Installed Software", "已安裝軟體"),
            ["stat-services"] = ("Windows Services", "Windows 服務"),
            ["stat-running-services"] = ("Running Services", "執行中的服務"),
            ["stat-scheduled-tasks"] = ("Scheduled Tasks", "排程工作"),
            ["stat-runtimes"] = ("Runtime / Framework", "執行環境"),
            ["stat-certificates"] = ("Certificates", "憑證"),
            ["stat-com-components"] = ("COM Components", "COM 元件"),
            ["stat-dll-binaries"] = ("DLL / Binary", "DLL / 執行檔"),
            ["stat-configurations"] = ("Configurations", "設定檔"),
            ["stat-external-connections"] = ("External Connections", "外部連線"),

            // Migration Assessment
            ["migration-assessment"] = ("Migration Assessment", "移轉評估"),
            ["migration-assessment-desc"] = (
                "The following assessment analyzes migration risks and required actions for moving this server to a new environment.",
                "以下評估結果分析此伺服器移轉至新環境時可能面臨的風險與需要採取的行動。"),
            ["migration-checklist"] = ("Migration Checklist", "移轉準備清單"),
            ["migration-checklist-desc"] = (
                "What to prepare when moving this server's applications to a new server.",
                "移轉此伺服器應用程式至新伺服器時所需的準備事項。"),

            // Checklist categories (En values must match hardcoded strings in legacy tests)
            ["chk-dll"] = ("Application Components (DLL / Binary)", "應用程式元件 (DLL / 執行檔)"),
            ["chk-runtime"] = ("Runtime Requirements", "執行環境需求"),
            ["chk-service"] = ("Windows Services", "Windows 服務"),
            ["chk-com"] = ("COM Components", "COM 元件"),
            ["chk-software"] = ("Installed Software", "已安裝軟體"),
            ["chk-task"] = ("Scheduled Tasks", "排程工作"),
            ["chk-cert"] = ("Certificates", "憑證"),
            ["chk-config"] = ("Configuration", "設定"),
            ["chk-external"] = ("External Connections", "外部連線"),
            ["chk-col-category"] = ("Category", "分類"),
            ["chk-col-discovered"] = ("Discovered", "已探索數量"),
            ["chk-col-action"] = ("Migration Action", "移轉動作"),

            // Verification checks
            ["pre-migration-checks"] = ("Pre-Migration Verification Checks", "移轉前驗證檢查"),
            ["post-migration-checks"] = ("Post-Migration Verification Checks", "移轉後驗證檢查"),

            // Server-level
            ["server-level-issues"] = ("Server-Level Issues", "伺服器層級問題"),
            ["coverage"] = ("Assessment Coverage", "評估涵蓋範圍"),
            ["coverage-desc"] = (
                "Coverage is independent of Migration Status — a Ready server may still have Partial or Limited coverage.",
                "涵蓋範圍與移轉狀態無關：即使伺服器已就緒，掃描涵蓋範圍仍可能為部分或有限。"),
            ["shared-infrastructure"] = ("Shared Infrastructure", "共用基礎架構"),
            ["shared-infrastructure-desc"] = (
                "Each row below is exactly one logical dependency shared by every boundary listed — never duplicated per boundary.",
                "下方每一列代表一個邏輯相依性，由所有列出的邊界共用，不會按邊界重複。"),
            ["dependencies"] = ("Dependencies", "相依性"),
            ["diagnostics"] = ("Diagnostics", "診斷資訊"),
            ["graph-validation"] = ("Graph Validation Errors", "關係圖驗證錯誤"),
            ["actions"] = ("Migration Actions", "移轉動作"),
            ["section-pre-migration"] = ("Pre-Migration", "移轉前"),
            ["section-post-migration"] = ("Post-Migration", "移轉後"),
            ["section-review-docs"] = ("Review / Documentation", "審查 / 文件"),

            // Table columns (common)
            ["col-name"] = ("Name", "名稱"),
            ["col-application"] = ("Application", "應用程式"),
            ["col-version"] = ("Version / Details", "版本 / 詳細資訊"),
            ["col-path"] = ("Path", "路徑"),
            ["col-status"] = ("Status", "狀態"),
            ["col-publisher"] = ("Publisher", "發行者"),
            ["col-type"] = ("Type", "類型"),
            ["col-display-name"] = ("Display Name", "顯示名稱"),
            ["col-start-type"] = ("Startup Type", "啟動類型"),
            ["col-account"] = ("Logon Account", "登入帳戶"),
            ["col-category"] = ("Category", "分類"),
            ["col-install-date"] = ("Install Date", "安裝日期"),
            ["col-action"] = ("Action", "動作"),
            ["col-trigger"] = ("Trigger", "觸發程序"),
            ["col-run-as"] = ("Run As", "執行身份"),
            ["col-folder"] = ("Folder", "資料夾"),
            ["col-subject"] = ("Subject", "主旨"),
            ["col-issuer"] = ("Issuer", "簽發者"),
            ["col-valid-to"] = ("Valid To", "有效期限"),
            ["col-thumbprint"] = ("Thumbprint", "指紋"),
            ["col-endpoint"] = ("Endpoint", "端點"),
            ["col-kind"] = ("Kind", "種類"),
            ["col-format"] = ("Format", "格式"),

            // Category display strings
            ["cat-windows-system"] = ("Windows System", "Windows 系統"),
            ["cat-microsoft"] = ("Microsoft", "Microsoft"),
            ["cat-third-party"] = ("Third-party", "第三方"),
            ["cat-custom"] = ("Custom / Business", "自訂 / 業務"),
            ["cat-unknown"] = ("Unknown", "未知"),

            // Filter buttons
            ["filter-all"] = ("All", "全部"),
            ["filter-running"] = ("Running", "執行中"),
            ["filter-stopped"] = ("Stopped", "停止"),
            ["filter-system"] = ("System", "系統"),
            ["filter-third-party"] = ("Third-party", "第三方"),
            ["filter-custom"] = ("Custom", "自訂"),
            ["filter-microsoft"] = ("Microsoft", "Microsoft"),

            // Search
            ["search-placeholder"] = ("Search...", "搜尋..."),

            // Empty states
            ["empty-none"] = ("None.", "無。"),
            ["empty-no-findings"] = ("No application boundaries with attributed findings.", "無具有歸因發現項目的應用程式邊界。"),
            ["empty-no-dep-shared"] = (
                "No dependency is shared across more than one application boundary.",
                "無相依性跨多個應用程式邊界共用。"),
            ["empty-no-deps"] = ("No dependencies identified.", "未識別到相依性。"),
            ["empty-no-graph-errors"] = ("No structural graph-integrity errors.", "無結構性關係圖完整性錯誤。"),
            ["empty-no-coverage-warnings"] = ("No coverage warnings.", "無涵蓋範圍警告。"),
            ["empty-no-actions"] = ("No actions required.", "無需要採取的動作。"),

            // Migration summary labels (condensed view)
            ["ma-blocking-issues"] = ("Blocking Issues", "封鎖性問題"),
            ["ma-actions"] = ("Actions Required", "需要執行的動作"),
            ["ma-checks"] = ("Verification Checks", "驗證檢查"),

            // Coverage table columns
            ["cov-col-scanner"] = ("Scanner", "掃描器"),
            ["cov-col-scanner-status"] = ("Status", "狀態"),
            ["cov-col-platform"] = ("Platform", "平台"),
            ["cov-col-reason"] = ("Reason", "原因"),
            ["cov-col-evidence"] = ("Evidence", "證據"),

            // Issues table columns
            ["iss-col-issue"] = ("Issue", "問題"),
            ["iss-col-rule"] = ("Rule", "規則"),
            ["iss-col-severity"] = ("Severity", "嚴重程度"),
            ["iss-col-impact"] = ("Impact", "影響"),
            ["iss-col-confidence"] = ("Confidence", "信心度"),
            ["iss-col-affected"] = ("Affected", "受影響項目"),
            ["iss-col-evidence"] = ("Evidence", "證據"),
            ["iss-required-action"] = ("Required action:", "必要動作："),
            ["iss-policy-reason"] = ("Policy decision:", "政策決定："),

            // Dependency table columns
            ["dep-col-dep"] = ("Dependency", "相依性"),
            ["dep-col-type"] = ("Type", "類型"),
            ["dep-col-target"] = ("Target", "目標"),
            ["dep-col-phase"] = ("Phase", "階段"),
            ["dep-col-confidence"] = ("Confidence", "信心度"),
            ["dep-col-boundaries"] = ("Affected Boundaries", "受影響的邊界"),
            ["dep-col-evidence"] = ("Evidence", "證據"),

            // Action table columns
            ["act-col-action"] = ("Action", "動作"),
            ["act-col-type"] = ("Type", "類型"),
            ["act-col-priority"] = ("Priority", "優先順序"),
            ["act-col-phase"] = ("Phase", "階段"),
            ["act-col-related"] = ("Related", "相關項目"),
            ["act-col-evidence"] = ("Evidence", "證據"),

            // Check table columns
            ["chk-col-check"] = ("Check", "檢查"),
            ["chk-col-type"] = ("Type", "類型"),
            ["chk-col-phase"] = ("Phase", "階段"),
            ["chk-col-related"] = ("Related", "相關項目"),
            ["chk-col-evidence"] = ("Evidence", "證據"),

            // Graph validation table columns
            ["gv-col-category"] = ("Category", "分類"),
            ["gv-col-code"] = ("Code", "代碼"),
            ["gv-col-severity"] = ("Severity", "嚴重程度"),
            ["gv-col-message"] = ("Message", "訊息"),
            ["gv-col-entities"] = ("Entities", "實體"),

            // Diagnostics stat labels
            ["diag-apps"] = ("Applications Consolidated", "已彙整應用程式"),
            ["diag-server-issues"] = ("Server-Level Issues", "伺服器層級問題"),
            ["diag-shared-infra"] = ("Shared Infrastructure Dependencies", "共用基礎架構相依性"),
            ["diag-coverage-warnings"] = ("Coverage Warnings", "涵蓋範圍警告"),
            ["diag-graph-errors"] = ("Graph Validation Errors", "關係圖驗證錯誤"),

            // Application section
            ["app-affected-entities"] = ("Affected entities:", "受影響的實體："),
            ["app-affected-boundaries"] = ("Affected boundaries:", "受影響的邊界："),
            ["app-pre-migration-checks"] = ("Pre-Migration Checks", "移轉前檢查"),
            ["app-post-migration-checks"] = ("Post-Migration Checks", "移轉後檢查"),
        };

    public static string Get(string key, ReportLanguage language)
    {
        if (!Labels.TryGetValue(key, out var pair)) return key;
        return language == ReportLanguage.ZhTw ? pair.ZhTw : pair.En;
    }
}
