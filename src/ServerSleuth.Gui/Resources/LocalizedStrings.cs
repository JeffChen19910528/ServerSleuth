using ServerSleuth.Gui.Services;

namespace ServerSleuth.Gui.Resources;

/// <summary>
/// The single source of truth for every localizable GUI string — one dictionary, both
/// languages, keyed identically. Deliberately NOT split into two separate XAML
/// ResourceDictionary files: a single table means an English string and its Traditional
/// Chinese counterpart can never drift apart by one file gaining a key the other lacks (see
/// <see cref="LanguageService"/>'s doc comment for why this feeds
/// <c>Application.Current.Resources</c> directly rather than merging a second
/// <c>ResourceDictionary</c>).
///
/// Scope: covers every STATIC label/button/header/column-header/empty-state string in the
/// four real screens (Scan Configuration, Scan Execution, Results Dashboard, Application
/// Detail) plus the navigation shell and status footer. It deliberately does NOT cover
/// dynamically-generated text whose source is a backend enum value rendered via plain
/// <c>ToString()</c> (e.g. <c>ScanStage</c>, <c>RiskSeverity</c>, migration status), scan
/// validation messages, or exception/export/report-viewer result text — see the GUI-7
/// addendum in ARCHITECTURE.md for why those are out of scope for this pass.
/// </summary>
public static class LocalizedStrings
{
    private static readonly Dictionary<string, (string En, string ZhHant)> Table = new()
    {
        // Navigation shell
        ["Nav.Dashboard.Label"] = ("Dashboard", "儀表板"),
        ["Nav.Dashboard.Description"] = ("An overview of the current target and its most recent scan will appear here.", "目前掃描目標及其最近一次掃描的概觀將顯示於此。"),
        ["Nav.Scan.Label"] = ("Scan", "掃描"),
        ["Nav.Scan.Description"] = ("Scan configuration and target selection will appear here.", "掃描設定與目標選擇將顯示於此。"),
        ["Nav.Results.Label"] = ("Results", "結果"),
        ["Nav.Results.Description"] = ("Discovered entities and their relationships will appear here.", "探勘到的實體及其關聯將顯示於此。"),
        ["Nav.Migration.Label"] = ("Migration", "遷移"),
        ["Nav.Migration.Description"] = ("Risk findings and the migration assessment will appear here.", "風險發現與遷移評估將顯示於此。"),
        ["Nav.Reports.Label"] = ("Reports", "報表"),
        ["Nav.Reports.Description"] = ("Generated reports and export options will appear here.", "已產生的報表與匯出選項將顯示於此。"),
        ["Nav.Settings.Label"] = ("Settings", "設定"),
        ["Nav.Settings.Description"] = ("Application preferences will appear here.", "應用程式偏好設定將顯示於此。"),

        // Status footer
        ["Status.Idle"] = ("Idle", "閒置"),
        ["Status.Scanning"] = ("Scanning…", "掃描中…"),
        ["Status.NoTargetSelected"] = ("No target selected", "尚未選擇掃描目標"),
        ["Status.ErrorPrefix"] = ("Error: ", "錯誤："),

        // Language toggle
        ["Language.English"] = ("EN", "EN"),
        ["Language.TraditionalChinese"] = ("中文", "中文"),

        // Scan Configuration
        ["ScanConfig.Title"] = ("Scan Configuration", "掃描設定"),
        ["ScanConfig.Target"] = ("Target", "掃描目標"),
        ["ScanConfig.Local"] = ("Local", "本機"),
        ["ScanConfig.Remote"] = ("Remote", "遠端"),
        ["ScanConfig.Platform"] = ("Platform", "平台"),
        ["ScanConfig.RemoteHost"] = ("Remote Host", "遠端主機"),
        ["ScanConfig.Connection"] = ("Connection", "連線方式"),
        ["ScanConfig.Credentials"] = ("Credentials", "認證資訊"),
        ["ScanConfig.Username"] = ("Username", "使用者名稱"),
        ["ScanConfig.Password"] = ("Password", "密碼"),
        ["ScanConfig.DomainOptional"] = ("Domain (optional)", "網域（選填）"),
        ["ScanConfig.RequireTls"] = ("Require TLS", "需要 TLS"),
        ["ScanConfig.PrivateKeyPath"] = ("Private Key Path", "私鑰檔案路徑"),
        ["ScanConfig.PassphraseEnvVar"] = ("Private Key Passphrase Environment Variable (optional)", "私鑰密碼短語環境變數（選填）"),
        ["ScanConfig.HostFingerprint"] = ("Expected Host Key Fingerprint", "預期的主機金鑰指紋"),
        ["ScanConfig.Output"] = ("Output", "輸出設定"),
        ["ScanConfig.Directory"] = ("Directory", "輸出目錄"),
        ["ScanConfig.Format"] = ("Format", "輸出格式"),
        ["ScanConfig.OverwriteExisting"] = ("Overwrite existing report", "覆蓋既有報表"),
        ["ScanConfig.Verbose"] = ("Verbose", "詳細模式"),
        ["ScanConfig.Validate"] = ("Validate", "驗證"),
        ["ScanConfig.StartScan"] = ("Start Scan", "開始掃描"),
        ["ScanConfig.Cancel"] = ("Cancel", "取消"),

        // Scan Execution
        ["ScanExec.Title"] = ("Scan Execution", "掃描執行"),
        ["ScanExec.TargetLabel"] = ("Target: ", "目標："),
        ["ScanExec.CurrentStage"] = ("Current Stage", "目前階段"),
        ["ScanExec.EntitiesDiscoveredLabel"] = ("Entities discovered: ", "已探勘實體數："),
        ["ScanExec.EntitiesSuffix"] = (" entities)", " 個實體）"),
        ["ScanExec.ScanCompleted"] = ("Scan Completed", "掃描已完成"),
        ["ScanExec.StatusLabel"] = ("Status: ", "狀態："),
        ["ScanExec.ErrorsLabel"] = ("Errors: ", "錯誤數："),
        ["ScanExec.ReportsLabel"] = ("Reports:", "報表："),
        ["ScanExec.CancelScan"] = ("Cancel Scan", "取消掃描"),
        ["ScanExec.ViewResults"] = ("View Results", "檢視結果"),
        ["ScanExec.StartNewScan"] = ("Start New Scan", "開始新掃描"),

        // Results Dashboard
        ["Dashboard.ScanResults"] = ("Scan Results", "掃描結果"),
        ["Dashboard.NewScan"] = ("New Scan", "新掃描"),
        ["Dashboard.ScanSummary"] = ("Scan Summary", "掃描摘要"),
        ["Dashboard.TargetLabel"] = ("Target: ", "目標："),
        ["Dashboard.StatusLabel"] = ("Status: ", "狀態："),
        ["Dashboard.StartedLabel"] = ("Started: ", "開始時間："),
        ["Dashboard.FinishedLabel"] = ("Finished: ", "結束時間："),
        ["Dashboard.DurationLabel"] = ("Duration: ", "耗時："),
        ["Dashboard.EntitiesLabel"] = ("Entities: ", "實體數："),
        ["Dashboard.ErrorsLabel"] = ("Errors: ", "錯誤數："),
        ["Dashboard.CoverageLabel"] = ("Coverage: ", "涵蓋範圍："),
        ["Dashboard.NoResults"] = ("No results are available for this scan yet.", "此次掃描尚無可用結果。"),
        ["Dashboard.RiskSummary"] = ("Risk Summary", "風險摘要"),
        ["Dashboard.CriticalLabel"] = (" Critical: ", " 嚴重："),
        ["Dashboard.HighLabel"] = (" High: ", " 高："),
        ["Dashboard.MediumLabel"] = (" Medium: ", " 中："),
        ["Dashboard.LowLabel"] = (" Low: ", " 低："),
        ["Dashboard.InformationalLabel"] = (" Informational: ", " 資訊："),
        ["Dashboard.MigrationSummary"] = ("Migration Summary", "遷移摘要"),
        ["Dashboard.BlockedLabel"] = ("Blocked: ", "已阻擋："),
        ["Dashboard.NeedsRemediationLabel"] = ("Needs Remediation: ", "需要修復："),
        ["Dashboard.ReadyWithConditionsLabel"] = ("Ready With Conditions: ", "有條件就緒："),
        ["Dashboard.ReadyLabel"] = ("Ready: ", "已就緒："),
        ["Dashboard.DependencySummary"] = ("Dependency Summary", "依賴摘要"),
        ["Dashboard.NoDependencies"] = ("No external dependencies detected.", "未偵測到外部依賴。"),
        ["Dashboard.Applications"] = ("Applications", "應用程式"),
        ["Dashboard.SearchApplicationTooltip"] = ("Search application name", "搜尋應用程式名稱"),
        ["Dashboard.OnlyWithIssues"] = ("Only with issues", "只顯示有問題的項目"),
        ["Dashboard.NoApplications"] = ("No applications were found for this scan.", "此次掃描未發現任何應用程式。"),
        ["Dashboard.ColApplication"] = ("Application", "應用程式"),
        ["Dashboard.ColMigrationStatus"] = ("Migration Status", "遷移狀態"),
        ["Dashboard.ColRisk"] = ("Risk", "風險"),
        ["Dashboard.ColConfidence"] = ("Confidence", "信心水準"),
        ["Dashboard.ColIssues"] = ("Issues", "問題數"),
        ["Dashboard.ColDependencies"] = ("Dependencies", "依賴數"),
        ["Dashboard.ColEntities"] = ("Entities", "實體數"),
        ["Dashboard.RiskFindings"] = ("Risk Findings", "風險發現"),
        ["Dashboard.NoRiskFindings"] = ("No risk findings.", "無風險發現。"),
        ["Dashboard.MigrationIssues"] = ("Migration Issues", "遷移問題"),
        ["Dashboard.NoMigrationIssues"] = ("No migration issues detected.", "未偵測到遷移問題。"),
        ["Dashboard.MigrationActions"] = ("Migration Actions", "遷移動作"),
        ["Dashboard.NoMigrationActions"] = ("No migration actions.", "無遷移動作。"),
        ["Dashboard.VerificationChecks"] = ("Verification Checks", "驗證檢查項目"),
        ["Dashboard.PreMigration"] = ("Pre-Migration", "遷移前"),
        ["Dashboard.PostMigration"] = ("Post-Migration", "遷移後"),
        ["Dashboard.ScannerStatus"] = ("Scanner Status", "掃描器狀態"),
        ["Dashboard.Reports"] = ("Reports", "報表"),
        ["Dashboard.NoReportFiles"] = ("No report files were written.", "尚未產生任何報表檔案。"),
        ["Dashboard.OpenReport"] = ("Open Report", "開啟報表"),
        ["Dashboard.ExportReport"] = ("Export Report", "匯出報表"),
        ["Dashboard.ExportedLabel"] = ("Exported: ", "已匯出："),
        ["Dashboard.Format"] = ("Format", "格式"),
        ["Dashboard.IfFileExists"] = ("If a file already exists", "若檔案已存在"),
        ["Dashboard.OutputDirectory"] = ("Output directory", "輸出目錄"),

        // Application Detail
        ["AppDetail.Risk"] = ("Risk", "風險"),
        ["AppDetail.FindingsLabel"] = (" — Findings: ", " — 發現數："),
        ["AppDetail.ConfidenceLabel"] = (" — Confidence: ", " — 信心水準："),
        ["AppDetail.TopRisks"] = ("Top Risks", "主要風險"),
        ["AppDetail.NoFindings"] = ("No application findings.", "此應用程式無任何發現。"),
        ["AppDetail.Migration"] = ("Migration", "遷移"),
        ["AppDetail.StatusLabel"] = ("Status: ", "狀態："),
        ["AppDetail.Issues"] = ("Issues", "問題"),
        ["AppDetail.NoIssues"] = ("No migration issues detected.", "未偵測到遷移問題。"),
        ["AppDetail.Actions"] = ("Actions", "動作"),
        ["AppDetail.NoActions"] = ("No migration actions.", "無遷移動作。"),
        ["AppDetail.VerificationChecks"] = ("Verification Checks", "驗證檢查項目"),
        ["AppDetail.PrePrefix"] = ("[Pre] ", "［前］"),
        ["AppDetail.PostPrefix"] = ("[Post] ", "［後］"),
        ["AppDetail.Dependencies"] = ("Dependencies", "依賴項目"),
        ["AppDetail.NoDependencies"] = ("No external dependencies detected.", "未偵測到外部依賴。"),
    };

    public static IReadOnlyCollection<string> Keys => Table.Keys;

    /// <summary>Never throws on an unknown key — returns the key itself so a missing
    /// translation is visibly obvious in the UI rather than crashing the app.</summary>
    public static string Get(string key, GuiLanguage language)
    {
        if (!Table.TryGetValue(key, out var value))
        {
            return key;
        }

        return language == GuiLanguage.TraditionalChinese ? value.ZhHant : value.En;
    }
}
