# ServerSleuth

ServerSleuth 是一套跨平台（Windows Server + Linux Server）的**伺服器探勘與遷移評估工具**（Server Discovery and Migration Assessment Tool），以 .NET 8+ LTS 開發。它會掃描一台伺服器，盤點其上的服務、IIS 網站、COM 元件、已安裝軟體、執行環境/SDK、行程、連接埠、排程工作、容器、憑證、資料庫依賴等，並產出**有證據佐證**（evidence-backed）的清單，最終產出遷移風險評估、依賴關係圖與遷移檢查清單。

> **嚴格唯讀 / 僅供檢視。** ServerSleuth 不會停止或重啟服務、不會修改登錄檔/IIS/systemd 設定、不會刪除檔案、不會安裝套件、不會變更防火牆規則，也不會匯出私鑰。所有探勘都是靜態/組態層級的讀取，不會執行未知的二進位檔，也不會用探勘到的憑證去連線任何資料庫或 API。

## 這個專案在做什麼

1. **探勘（Discovery）** — 依平台分別掃描 Windows（Services、IIS、COM、已安裝軟體、排程工作、憑證、執行環境、登錄檔）與 Linux（systemd、套件、cron、Docker/Podman、Kubernetes、組態檔、ELF 相依性）。
2. **關聯與分析（Correlation / Boundary / Dependency Expansion / Validation）** — 將不同掃描器找到的同一個邏輯元件合併成單一實體（例如同時被登錄檔、檔案系統、行程掃描器找到的同一套 Oracle Client），並劃出應用程式邊界、展開依賴關係、驗證依賴圖。
3. **風險評估（Evidence-Based Risk Engine）** — 依證據給出風險發現（Risk Finding），並用固定的信心區間（0.90–1.00 非常高 … 0.00–0.24 非常低）呈現，絕不把推論當成事實。
4. **遷移評估（Migration Assessment）** — 產出每個應用程式/伺服器層級的遷移狀態（Blocked / NeedsRemediation / ReadyWithConditions / Ready）、遷移問題、遷移動作與驗證檢查項目（皆為**宣告式**，工具本身不會執行）。
5. **報表輸出（Reporting）** — 產出 `report.json`（機器可讀）與 `report.html`（人可讀，內容為純靜態、零 JavaScript）。報表中若偵測到 `Password=`、`ConnectionString=`、`API_KEY=`、`TOKEN=`、`SECRET=`、`PRIVATE_KEY=` 等機密樣式，一律以 `[REDACTED]` / `SecretDetected: true`呈現，絕不外洩明文。

專案採分層架構，探勘（Discovery）、關聯分析（Correlation/Analysis）、報表（Reporting）彼此分離，每個掃描器都實作統一的 `IDiscoveryScanner` 介面並登記於掃描器註冊表；單一掃描器失敗不會中止整個掃描（Fault isolation），最終會列出成功/部分成功/失敗/略過的掃描摘要。詳細的模組劃分與設計決策請見 [`ARCHITECTURE.md`](ARCHITECTURE.md)；完整開發歷程與各階段測試結果請見 [`PROGRESS.md`](PROGRESS.md)；各掃描器的用途/資料來源/所需權限/已知限制請見 [`SCANNERS.md`](SCANNERS.md)。

## 專案結構

```
src/
├── ServerSleuth.Core/               # 領域模型、Evidence、Enum（平台無關）
├── ServerSleuth.Infrastructure/     # 共用的檔案系統/網路/行程抽象
├── ServerSleuth.Windows/            # Windows 探勘掃描器
├── ServerSleuth.Linux/              # Linux 探勘掃描器
├── ServerSleuth.Analysis/           # 關聯、邊界、風險、遷移評估
├── ServerSleuth.Reporting/          # JSON / HTML 報表產生與匯出
├── ServerSleuth.Cli/                # 命令列工具（serversleuth）
├── ServerSleuth.Gui/                # WPF 桌面應用程式
├── ServerSleuth.Gui.Contracts/      # GUI 與執行主機共用的 DTO/介面
└── ServerSleuth.Gui.ExecutionHost/  # GUI 專用的執行/匯出組合層（唯一可碰觸 Windows/Linux/Infrastructure/Reporting 的 GUI 相關組件）
test/                                # 對應每個 src 專案的測試專案
```

## 需求環境

- .NET SDK 8.0 或以上（開發環境為 .NET SDK 10.0.400，含 net8.0 目標框架）
- 命令列工具（`ServerSleuth.Cli`）跨平台（Windows / Linux）皆可建置與執行
- 桌面 GUI（`ServerSleuth.Gui`）僅支援 Windows（採用 WPF，`net8.0-windows`）

## 建置與測試

```bash
dotnet restore
dotnet build
dotnet test
```

## 使用方式 — 命令列（CLI）

建置後於 `src/ServerSleuth.Cli` 執行，或直接 `dotnet run --project src/ServerSleuth.Cli`：

```
ServerSleuth — cross-platform server discovery and migration assessment tool.

用法：
  serversleuth --help
  serversleuth --version
  serversleuth scan [options]

指令：
  scan          掃描本機（或指定的遠端主機），執行完整遷移評估流程並輸出報表。
```

### 掃描本機

```bash
serversleuth scan
```

預設會將報表輸出到 `./serversleuth-report`（`report.json` 與 `report.html` 皆輸出）。若該目錄下已有報表，預設不會覆蓋；加上 `--overwrite` 才會覆蓋既有報表。

### 常用選項

| 選項 | 說明 |
|---|---|
| `--output <directory>` | 報表輸出目錄，預設 `./serversleuth-report` |
| `--format <json\|html\|both>` | 輸出格式，預設 `both` |
| `--overwrite` | 覆蓋既有報表（預設關閉） |
| `--quiet` / `-q` | 只印錯誤訊息 |
| `--verbose` | 顯示每個掃描器的狀態、實體數與各階段耗時 |
| `--target <local\|host>` | 掃描目標，`local`（預設）或遠端主機名稱/IP |

### 掃描遠端主機

掃描遠端 **Linux** 主機（需搭配 SSH 選項）：

```bash
serversleuth scan --target 192.168.1.10 \
  --ssh-user deploy \
  --ssh-key ~/.ssh/id_ed25519 \
  --ssh-host-fingerprint <遠端主機的 SHA-256 host key fingerprint> \
  --output ./report-linux-host
```

掃描遠端 **Windows** 主機（需搭配 WinRM 選項；密碼一律透過環境變數傳遞，絕不直接寫在命令列上）：

```bash
export SERVERSLEUTH_WINRM_PW="..."
serversleuth scan --target win-host-01 \
  --winrm-user Administrator \
  --winrm-password-env SERVERSLEUTH_WINRM_PW \
  --output ./report-win-host
```

> 遠端連線一律要求驗證憑據透過環境變數/金鑰檔傳入，且未知的 SSH host key 預設會被拒絕（需先提供 `--ssh-host-fingerprint`）。完整選項請執行 `serversleuth scan --help` 查看。

## 使用方式 — 桌面 GUI（Windows）

```bash
dotnet run --project src/ServerSleuth.Gui
```

或直接執行建置產出的 `ServerSleuth.Gui.exe`（`src/ServerSleuth.Gui/bin/Debug/net8.0-windows/`）。

操作流程：

```
掃描設定（本機/遠端、平台、輸出設定）
        ↓
開始掃描（顯示執行進度）
        ↓
掃描完成
        ↓
結果儀表板（風險摘要 / 遷移摘要 / 應用程式清單 / 依賴關係 / 掃描器狀態）
        ↓
應用程式詳細資料（風險 / 遷移狀態 / 依賴 / 問題 / 動作 / 驗證項目）
        ↓
匯出報表（JSON / HTML / 兩者皆要；可選擇是否覆蓋既有檔案）／開啟已產生的報表
        ↓
開始新掃描
```

GUI 完全重用與 CLI 相同的掃描/分析/報表後端（不會重複實作探勘或分析邏輯），畫面上顯示的一律是同一次掃描已完成的結果 —— 切換分頁、選取應用程式、匯出報表都**不會**重新觸發掃描。報表檢視器目前僅以純文字方式呈現 JSON/HTML 內容（不會把 HTML 當成可執行網頁內容渲染，也不會執行任何 JavaScript），這是刻意的安全設計。

## 輸出內容

一次掃描完成後，輸出目錄中會包含：

- `report.json` — 完整的機器可讀報表（含掃描摘要、風險發現、遷移評估、依賴關係圖等）
- `report.html` — 對應的人類可讀報表（純靜態 HTML，零 JavaScript）

## 目前完成進度

專案已完成 Phase 1–10E-3（核心領域模型、Windows/Linux 探勘、關聯分析、風險引擎、遷移評估、報表輸出、CLI）以及 GUI-1 至 GUI-6（WPF 應用程式外殼、掃描設定、掃描執行、結果儀表板、報表匯出/檢視、最終上線前的健檢與強化）。詳細的版本異動請見 [`CHANGELOG.md`](CHANGELOG.md)。

> GUI 的互動式視覺驗證（實際點擊操作、畫面截圖）目前尚未在任何開發環境中執行過（本專案的開發流程沒有可用的 Windows 桌面自動化/截圖能力），此限制已在文件中如實記載，未來如需要正式驗收，仍建議由使用者在真實桌面環境中親自操作一次完整流程。

## 安全與隱私原則

- 唯讀探勘，不對受掃描系統做任何寫入/變更。
- 絕不外洩機密內容（密碼、連線字串、API 金鑰、Token、私鑰等一律遮罩）。
- 不執行任何被探勘到的或來路不明的二進位檔（偏好靜態解析 PE/ELF metadata）。
- 核心探勘流程不含遙測，預設不會把掃描資料上傳到任何地方。
- 不會用探勘到的憑證去連線資料庫、API 或其他外部系統。

## 延伸文件

- [`ARCHITECTURE.md`](ARCHITECTURE.md) — 實際落地的架構與各階段設計決策記錄
- [`SCANNERS.md`](SCANNERS.md) — 每個掃描器的用途、資料來源、所需權限與限制
- [`CHANGELOG.md`](CHANGELOG.md) — 版本異動紀錄
