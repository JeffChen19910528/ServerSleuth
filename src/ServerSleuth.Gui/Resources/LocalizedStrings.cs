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
        ["Nav.Inventory.Label"] = ("Inventory", "盤點"),
        ["Nav.Inventory.Description"] = ("Discovered services, applications, and other assets on this server will appear here.", "此伺服器上探勘到的服務、應用程式及其他資產將顯示於此。"),
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

        // Common — shared across multiple pages (folder/file picker buttons)
        ["Common.Browse"] = ("Browse…", "瀏覽…"),

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
        ["Dashboard.Inventory"] = ("Discovery Inventory", "探勘盤點"),
        ["Dashboard.InventoryCategoryTooltip"] = ("Category", "類別"),
        ["Dashboard.InventoryCategoryAll"] = ("All", "全部"),
        ["Dashboard.InventorySearchTooltip"] = ("Search name, type, or path", "搜尋名稱、類型或路徑"),
        ["Dashboard.InventoryCountLabel"] = ("Count: ", "數量："),
        ["Dashboard.InventoryPartialCoverage"] = ("Some scanners were partially supported — this inventory may be incomplete.", "部分掃描器僅部分支援 — 此盤點可能不完整。"),
        ["Dashboard.NoInventory"] = ("No discovered entities.", "未探勘到任何實體。"),
        ["Dashboard.ColType"] = ("Type", "類型"),
        ["Dashboard.ColName"] = ("Name", "名稱"),
        ["Dashboard.ColStatus"] = ("Status", "狀態"),
        ["Dashboard.ColVersion"] = ("Version", "版本"),
        ["Dashboard.ColArchitecture"] = ("Architecture", "架構"),
        ["Dashboard.ColPath"] = ("Path", "路徑"),
        ["Dashboard.ColOwnerApplication"] = ("Application", "所屬應用程式"),
        ["Dashboard.ColEvidenceCount"] = ("Evidence", "證據數"),
        ["InventoryDetail.Id"] = ("Id: ", "識別碼："),
        ["InventoryDetail.Type"] = ("Type: ", "類型："),
        ["InventoryDetail.Status"] = ("Status: ", "狀態："),
        ["InventoryDetail.Version"] = ("Version: ", "版本："),
        ["InventoryDetail.Architecture"] = ("Architecture: ", "架構："),
        ["InventoryDetail.Path"] = ("Path: ", "路徑："),
        ["InventoryDetail.Publisher"] = ("Publisher: ", "發行者："),
        ["InventoryDetail.Source"] = ("Source: ", "來源："),
        ["InventoryDetail.Confidence"] = ("Confidence: ", "信心水準："),
        ["InventoryDetail.Applications"] = ("Affected Applications", "受影響的應用程式"),
        ["InventoryDetail.Unassigned"] = ("Unassigned", "未分配"),
        ["InventoryDetail.Evidence"] = ("Evidence", "證據"),
        ["InventoryDetail.NoEvidence"] = ("No evidence recorded.", "無記錄的證據。"),
        ["InventoryDetail.Metadata"] = ("Metadata", "中繼資料"),
        ["InventoryDetail.NoMetadata"] = ("No metadata recorded.", "無記錄的中繼資料。"),

        // GUI-10 §8: ScheduledTask-specific detail fields
        ["InventoryDetail.ScheduledTask"] = ("Scheduled Task Details", "排程工作詳細資料"),
        ["InventoryDetail.ScheduledTask.Folder"] = ("Folder: ", "資料夾："),
        ["InventoryDetail.ScheduledTask.Trigger"] = ("Trigger: ", "觸發程序："),
        ["InventoryDetail.ScheduledTask.Action"] = ("Action: ", "動作："),
        ["InventoryDetail.ScheduledTask.RunAsAccount"] = ("Run As: ", "執行身分："),
        ["InventoryDetail.ScheduledTask.Enabled"] = ("Enabled: ", "已啟用："),
        ["InventoryDetail.ScheduledTask.NextRun"] = ("Next Run: ", "下次執行時間："),
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

        // GUI-7B: Migration page (distinct from Results' own "Dashboard.MigrationSummary" etc.)
        ["Migration.Title"] = ("Migration Assessment", "遷移評估"),
        ["Migration.NoResultsTitle"] = ("No migration assessment is available yet.", "尚無可用的遷移評估。"),
        ["Migration.NoResultsBody"] = ("Run a scan to determine application migration status.", "執行掃描以判斷應用程式的遷移狀態。"),
        ["Migration.Applications"] = ("Applications", "應用程式"),

        // GUI-7B: Reports page (distinct from Results' own "Dashboard.Reports" expander)
        ["Reports.Title"] = ("Reports", "報表"),
        ["Reports.NoResultsTitle"] = ("No report has been generated yet.", "尚未產生任何報表。"),
        ["Reports.NoResultsBody"] = ("Run a scan to generate JSON and HTML reports.", "執行掃描以產生 JSON 與 HTML 報表。"),
        ["Reports.Available"] = ("Available Reports", "可用的報表"),

        // GUI-7B: Settings page
        ["Settings.Title"] = ("Settings", "設定"),
        ["Settings.General"] = ("General", "一般"),
        ["Settings.DefaultOutputDirectory"] = ("Default Output Directory", "預設輸出目錄"),
        ["Settings.DefaultReportFormat"] = ("Default Report Format", "預設報表格式"),
        ["Settings.DefaultOverwritePolicy"] = ("Default Overwrite Policy", "預設覆蓋原則"),
        ["Settings.VerboseOutput"] = ("Verbose Output", "詳細輸出"),
        ["Settings.Language"] = ("Language", "語言"),

        // GUI-7A: Dashboard overview (MainViewModel's NavigationPage.Dashboard — a distinct
        // lightweight summary page, not to be confused with the "Dashboard.*"-prefixed keys
        // above, which belong to the Results dashboard and predate this page).
        ["Overview.Subtitle"] = ("Server Discovery & Migration Assessment", "伺服器探勘與遷移評估"),
        ["Overview.NoResultsTitle"] = ("No scan results yet.", "尚無掃描結果。"),
        ["Overview.NoResultsBody"] = ("Scan this server to discover services, applications, dependencies, risks, and migration information.", "掃描此伺服器以探勘服務、應用程式、依賴項目、風險與遷移資訊。"),
        ["Overview.LastScan"] = ("Last Scan", "最近一次掃描"),
        ["Overview.PartialNotice"] = ("This scan is partial — some scanners were not fully supported.", "此次掃描為部分結果 — 部分掃描器未完全受支援。"),
        ["Overview.Discovery"] = ("Discovery", "探勘"),
        ["Overview.EntitiesLabel"] = ("Entities", "實體數"),
        ["Overview.ApplicationsLabel"] = ("Applications", "應用程式數"),
        ["Overview.DependenciesLabel"] = ("Dependencies", "依賴數"),
        ["Overview.Risk"] = ("Risk", "風險"),
        ["Overview.CriticalLabel"] = ("Critical", "嚴重"),
        ["Overview.HighLabel"] = ("High", "高"),
        ["Overview.MediumLabel"] = ("Medium", "中"),
        ["Overview.Migration"] = ("Migration", "遷移"),
        ["Overview.BlockedLabel"] = ("Blocked", "已阻擋"),
        ["Overview.NeedsRemediationLabel"] = ("Needs Remediation", "需要修復"),
        ["Overview.ReadyWithConditionsLabel"] = ("Ready With Conditions", "有條件就緒"),
        ["Overview.ReadyLabel"] = ("Ready", "已就緒"),

        // GUI-8C: Migration Checklist page labels
        ["Migration.Checklist"] = ("Migration Checklist", "遷移清單"),
        ["Migration.ChecklistSummary"] = ("Total inventory items to prepare across all applications:", "所有應用程式需準備的盤點項目總計："),
        ["Migration.Inv.DllBinaries"] = ("DLL / Binaries", "DLL / 二進位檔"),
        ["Migration.Inv.Runtimes"] = ("Runtime", "執行階段"),
        ["Migration.Inv.Services"] = ("Windows Services", "Windows 服務"),
        ["Migration.Inv.ComComponents"] = ("COM Components", "COM 元件"),
        ["Migration.Inv.Software"] = ("Installed Software", "已安裝軟體"),
        ["Migration.Inv.ScheduledTasks"] = ("Scheduled Tasks", "排程工作"),
        ["Migration.Inv.Certificates"] = ("Certificates", "憑證"),
        ["Migration.Inv.Configurations"] = ("Configuration", "設定檔"),
        ["Migration.Inv.ExternalConnections"] = ("External Connections", "外部連線"),

        // GUI-10 §4, §5: Migration Preparation card — distinct from the Migration Checklist card
        // above. That card shows INVENTORY counts (how many of each thing were discovered); this
        // one shows INTENT counts (how many preparation actions of each kind are required) — the
        // same discovered item can require several actions at once, so these numbers are
        // deliberately not "N services" but "N Create actions, N Configure actions, N Verify
        // actions" etc.
        ["Migration.Preparation"] = ("Migration Preparation", "遷移準備"),
        ["Migration.PreparationSummary"] = ("Preparation actions required on the destination server — not risk findings, and not commands that will be executed. One discovered item may require more than one action.", "在目的伺服器上需要執行的準備動作 — 並非風險發現項目，也不會被自動執行。單一盤點項目可能需要多項準備動作。"),
        ["Migration.Intent.Deploy"] = ("Deploy", "部署"),
        ["Migration.Intent.Install"] = ("Install", "安裝"),
        ["Migration.Intent.Create"] = ("Create", "建立"),
        ["Migration.Intent.Register"] = ("Register", "註冊"),
        ["Migration.Intent.Configure"] = ("Configure", "設定"),
        ["Migration.Intent.Verify"] = ("Verify", "驗證"),
        ["Migration.Intent.Review"] = ("Review", "審查"),

        // GUI-8C: Migration action labels (used as annotations on component sections)
        ["AppDetail.Action.Copy"] = ("→ Copy", "→ 複製"),
        ["AppDetail.Action.Install"] = ("→ Install / Verify", "→ 安裝 / 驗證"),
        ["AppDetail.Action.Create"] = ("→ Create / Configure / Verify", "→ 建立 / 設定 / 驗證"),
        ["AppDetail.Action.Register"] = ("→ Register / Verify", "→ 註冊 / 驗證"),
        ["AppDetail.Action.Configure"] = ("→ Configure / Verify", "→ 設定 / 驗證"),
        ["AppDetail.Action.InstallSoftware"] = ("→ Install / Review / Verify", "→ 安裝 / 審查 / 驗證"),
        ["AppDetail.Action.Verify"] = ("→ Verify / Review", "→ 驗證 / 審查"),

        // GUI-8A: Inventory-first Dashboard per-type labels
        ["Overview.Inventory"] = ("Inventory", "盤點"),
        ["Overview.Inv.Applications"] = ("Applications", "應用程式"),
        ["Overview.Inv.DllBinaries"] = ("DLL / Binaries", "DLL / 二進位檔"),
        ["Overview.Inv.Services"] = ("Services", "服務"),
        ["Overview.Inv.ComComponents"] = ("COM Components", "COM 元件"),
        ["Overview.Inv.InstalledSoftware"] = ("Installed Software", "已安裝軟體"),
        ["Overview.Inv.Runtime"] = ("Runtime", "執行階段"),
        ["Overview.Inv.ScheduledTasks"] = ("Scheduled Tasks", "排程工作"),
        ["Overview.Inv.Certificates"] = ("Certificates", "憑證"),
        ["Overview.Inv.Configuration"] = ("Configuration", "設定檔"),
        ["Overview.Inv.ExternalConnections"] = ("External Connections", "外部連線"),

        // Application Detail — GUI-8B: Application Components (inventory-first, above risk)
        ["AppDetail.Components"] = ("Application Components", "應用程式元件"),
        ["AppDetail.NoComponents"] = ("No components discovered for this application.", "未探勘到此應用程式的任何元件。"),
        ["AppDetail.DllBinaries"] = ("DLL / Binary", "DLL / 二進位檔"),
        ["AppDetail.Runtimes"] = ("Runtime Requirements", "執行階段需求"),
        ["AppDetail.Services"] = ("Windows Services", "Windows 服務"),
        ["AppDetail.ComComponents"] = ("COM Components", "COM 元件"),
        ["AppDetail.Configurations"] = ("Configuration", "設定檔"),
        ["AppDetail.Certificates"] = ("Certificates", "憑證"),
        ["AppDetail.ScheduledTasks"] = ("Scheduled Tasks", "排程工作"),
        ["AppDetail.Software"] = ("Installed Software", "已安裝軟體"),
        ["AppDetail.ExternalConnections"] = ("External Connections", "外部連線"),
        ["AppDetail.MigrationPrep"] = ("Migration Preparation", "遷移準備"),

        // Application Detail — GUI-8B: column/field labels
        ["AppDetail.Col.Name"] = ("Name", "名稱"),
        ["AppDetail.Col.Path"] = ("Path", "路徑"),
        ["AppDetail.Col.Version"] = ("Version", "版本"),
        ["AppDetail.Col.Architecture"] = ("Architecture", "架構"),
        ["AppDetail.Col.Status"] = ("Status", "狀態"),
        ["AppDetail.Col.DisplayName"] = ("Display Name", "顯示名稱"),
        ["AppDetail.Col.StartType"] = ("Start Type", "啟動類型"),
        ["AppDetail.Col.ServiceAccount"] = ("Service Account", "服務帳號"),
        ["AppDetail.Col.ExecutablePath"] = ("Executable Path", "可執行檔路徑"),
        ["AppDetail.Col.Clsid"] = ("CLSID", "CLSID"),
        ["AppDetail.Col.ProgId"] = ("ProgID", "ProgID"),
        ["AppDetail.Col.ThreadingModel"] = ("Threading Model", "執行緒模型"),
        ["AppDetail.Col.Subject"] = ("Subject", "主旨"),
        ["AppDetail.Col.Issuer"] = ("Issuer", "簽發者"),
        ["AppDetail.Col.Thumbprint"] = ("Thumbprint", "指紋"),
        ["AppDetail.Col.ValidTo"] = ("Valid To", "有效期至"),
        ["AppDetail.Col.Folder"] = ("Folder", "資料夾"),
        ["AppDetail.Col.Trigger"] = ("Trigger", "觸發條件"),
        ["AppDetail.Col.Action"] = ("Action", "動作"),
        ["AppDetail.Col.RunAsAccount"] = ("Run As", "執行帳號"),
        ["AppDetail.Col.Publisher"] = ("Publisher", "發行者"),
        ["AppDetail.Col.InstallDate"] = ("Install Date", "安裝日期"),
        ["AppDetail.Col.Kind"] = ("Kind", "類型"),
        ["AppDetail.Col.Endpoint"] = ("Endpoint", "端點"),

        // Application Detail — existing
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
