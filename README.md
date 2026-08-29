# ServerSleuth

ServerSleuth 是一套跨平台（Windows Server + Linux Server）的**伺服器探勘與遷移評估工具**（Server Discovery and Migration Assessment Tool），以 .NET 8+ LTS 開發。它會掃描一台伺服器，盤點其上的服務、IIS 網站、COM 元件、已安裝軟體、執行環境/SDK、行程、連接埠、排程工作、容器、憑證、資料庫依賴等，並產出**有證據佐證**（evidence-backed）的清單，最終產出遷移風險評估、依賴關係圖與遷移檢查清單。

> **嚴格唯讀 / 僅供檢視。** ServerSleuth 不會停止或重啟服務、不會修改登錄檔/IIS/systemd 設定、不會刪除檔案、不會安裝套件、不會變更防火牆規則，也不會匯出私鑰。所有探勘都是靜態/組態層級的讀取，不會執行未知的二進位檔，也不會用探勘到的憑證去連線任何資料庫或 API。

## 這個專案在做什麼

1. **探勘（Discovery）** — 依平台分別掃描 Windows（Services、IIS、COM、已安裝軟體、排程工作、憑證、執行環境、登錄檔）與 Linux（systemd、套件、cron、Docker/Podman、Kubernetes、組態檔、ELF 相依性）。
2. **關聯與分析（Correlation / Boundary / Dependency Expansion / Validation）** — 將不同掃描器找到的同一個邏輯元件合併成單一實體（例如同時被登錄檔、檔案系統、行程掃描器找到的同一套 Oracle Client），並劃出應用程式邊界、展開依賴關係、驗證依賴圖。
3. **風險評估（Evidence-Based Risk Engine）** — 依證據給出風險發現（Risk Finding），並用固定的信心區間（0.90–1.00 非常高 … 0.00–0.24 非常低）呈現，絕不把推論當成事實。
4. **遷移評估（Migration Assessment）** — 產出每個應用程式/伺服器層級的遷移狀態（Blocked / NeedsRemediation / ReadyWithConditions / Ready）、遷移問題、遷移動作與驗證檢查項目（皆為**宣告式**，工具本身不會執行）。
5. **報表輸出（Reporting）** — 產出 `report.json`（機器可讀）與 `report.html`（人可讀，內容為純靜態、零 JavaScript）。報表中若偵測到 `Password=`、`ConnectionString=`、`API_KEY=`、`TOKEN=`、`SECRET=`、`PRIVATE_KEY=` 等機密樣式，一律以 `[REDACTED]` / `SecretDetected: true`呈現，絕不外洩明文。

專案採分層架構，探勘（Discovery）、關聯分析（Correlation/Analysis）、報表（Reporting）彼此分離，每個掃描器都實作統一的 `IDiscoveryScanner` 介面並登記於掃描器註冊表；單一掃描器失敗不會中止整個掃描（Fault isolation），最終會列出成功/部分成功/失敗/略過的掃描摘要。詳細的模組劃分與設計決策請見 [`ARCHITECTURE.md`](docs/ARCHITECTURE.md)；各掃描器的用途/資料來源/所需權限/已知限制請見 [`SCANNERS.md`](docs/SCANNERS.md)。

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
docs/                                # 架構、掃描器、安全性、遷移說明文件（見下方「延伸文件」）
└── releases/                        # 版本上線前的驗收/健檢紀錄（一次性報告）
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

左側導覽共七個頁面：**Dashboard（儀表板）／Scan（掃描）／Inventory（探勘盤點）／Results（結果）／Migration（遷移評估）／Reports（報表）／Settings（設定）**，皆為可實際操作的真實畫面（沒有任何一個是尚未實作的佔位畫面）。操作流程總覽：

```
啟動 ServerSleuth
        ↓
儀表板（尚無掃描時顯示空狀態；不會捏造任何數字）
        ↓
掃描設定（本機/遠端、平台、輸出設定）
        ↓
開始掃描（顯示執行進度）
        ↓
掃描完成
        ↓
儀表板（顯示本次掃描的真實統計摘要）
        ↓
探勘盤點（依類別瀏覽/搜尋所有探勘到的實體，點選項目可看詳細資料）
        ↓
結果儀表板（風險摘要 / 遷移摘要 / 應用程式清單 / 依賴關係 / 掃描器狀態）
        ↓
應用程式詳細資料（風險 / 遷移狀態 / 依賴 / 問題 / 動作 / 驗證項目）
        ↓
遷移評估（Blocked / Needs Remediation / Ready With Conditions / Ready 統計 + 應用程式清單，
          點選任一應用程式可看與結果儀表板相同的詳細資料面板）
        ↓
報表（開啟 JSON/HTML 純文字內容／匯出報表）
        ↓
設定（掃描預設值：輸出目錄、報表格式、覆蓋原則、詳細模式；語言切換）
        ↓
開始新掃描
```

GUI 完全重用與 CLI 相同的掃描/分析/報表後端（不會重複實作探勘或分析邏輯），畫面上顯示的一律是同一次掃描已完成的結果 —— 切換分頁、選取應用程式、匯出報表都**不會**重新觸發掃描或重新執行任何分析/報表產生邏輯。報表檢視器目前僅以純文字方式呈現 JSON/HTML 內容（不會把 HTML 當成可執行網頁內容渲染，也不會執行任何 JavaScript），這是刻意的安全設計。「遷移評估」畫面純粹是**檢視**用途，畫面上不存在任何「執行遷移」「套用修復」「安裝套件」「重啟服務」之類的操作按鈕。

### 語言切換

視窗右上角有兩個按鈕：**EN** 與 **中文**。點擊即可在英文與繁體中文之間即時切換整個介面的文字（畫面標籤、按鈕、欄位名稱、左側導覽選單、底部狀態列），不需要重新啟動程式，也不會中斷或重新執行目前的掃描/結果。語言選擇僅保存在目前執行的程式記憶體中，關閉程式後會重設為預設的英文，不會被記住到下次啟動。

> 目前有少部分文字尚未納入語言切換（會固定顯示英文），例如：掃描設定的驗證錯誤訊息、掃描階段/風險等級/遷移狀態這類直接來自後端資料的列舉值文字、以及報表匯出/檢視結果的提示訊息。這是本次功能已知的限制，不影響上述「操作流程總覽」中每個畫面本身的操作。

### 各畫面逐步說明

以下逐一說明開啟 GUI 後會依序看到的每個畫面，以及畫面上每個欄位/按鈕的作用。

**1. 儀表板（Dashboard）** —— 程式啟動後預設顯示的畫面，之後也可隨時從左側導覽點選「Dashboard」返回（不會觸發任何掃描）：

- 尚無任何掃描完成時，顯示「尚無掃描結果」的空狀態說明文字與一個 **Start Scan（開始掃描）** 按鈕 —— 不會顯示任何捏造的統計數字。
- 有掃描完成後，顯示：
  - **Last Scan（最近一次掃描）**：目標、平台、狀態；若該次掃描為部分成功（Partial），會明確標示「此次掃描為部分結果」，不會呈現成完全成功。
  - **Discovery（探勘）**：實體數、應用程式數、依賴數。
  - **Risk（風險）**：Critical / High / Medium 三個等級的統計數字。
  - **Migration（遷移）**：Blocked / Needs Remediation / Ready With Conditions / Ready 四個狀態的應用程式數量。
  - 以上所有數字皆直接取自該次掃描已完成的結果，不會重新計算，也不會發明新的 0–100 分數。
  - 底部按鈕：**View Results（檢視結果）**、**Inventory（探勘盤點）**、**New Scan（新掃描）**，分別導覽至對應畫面。

**2. 掃描設定（Scan Configuration）** —— 從左側導覽點選「Scan」（掃描）即可看到這個畫面：

- **Target（掃描目標）**：選擇 `Local`（掃描本機）或 `Remote`（掃描遠端主機）。
- **Platform（平台）**：選擇遠端主機是 `Windows` 還是 `Linux`（僅在選擇 Remote 時可調整；本機掃描會自動偵測平台）。
- **Remote Host（遠端主機）**：輸入遠端主機的名稱或 IP（僅遠端掃描需要）。
- **Connection（連線方式）**：唯讀欄位，依平台自動顯示會使用的連線協定（Windows 用 WinRM，Linux 用 SSH）。
- **Credentials（認證資訊）**：
  - `Username`：登入用的使用者名稱。
  - 若目標是 Windows：`Password`（密碼，輸入框會遮蔽顯示）、`Domain`（網域，選填）、`Require TLS`（是否強制使用 TLS 連線，預設開啟）。
  - 若目標是 Linux：`Private Key Path`（SSH 私鑰檔案路徑）、`Private Key Passphrase Environment Variable`（存放私鑰密碼短語的環境變數名稱，選填）、`Expected Host Key Fingerprint`（預期的遠端主機金鑰指紋 —— 未知的主機金鑰預設會被拒絕連線，這是防止中間人攻擊的安全機制）。
- **Output（輸出設定）**：
  - `Directory`：報表輸出目錄。
  - `Format`：輸出格式，`JSON`、`HTML` 或 `Both`（兩者皆要）。
  - `Overwrite existing report`：勾選後才會覆蓋該目錄下既有的報表，預設不覆蓋。
  - `Verbose`：勾選後掃描過程會顯示更詳細的每個掃描器狀態。
- 點擊 **Validate（驗證）** 會檢查目前設定是否完整合法，任何錯誤會列在按鈕下方（例如遠端掃描缺少必要欄位）。
- 點擊 **Start Scan（開始掃描）** 會先自動驗證，驗證通過後立即進入下一個畫面開始掃描；若驗證失敗則停留在本畫面並顯示錯誤訊息。
- 點擊 **Cancel（取消）** 會清空目前已輸入的驗證狀態與認證資訊（不會影響已輸入的其他欄位）。

**3. 掃描執行（Scan Execution）** —— 點擊「Start Scan」後自動切換到此畫面：

- 畫面上方顯示目前掃描的 **Target** 與平台。
- **Current Stage（目前階段）**：即時顯示掃描目前所在的階段（例如 Preparing、Discovery）。
- 掃描過程中會即時列出每個已回報的掃描器狀態與其目前發現的實體數量，並顯示一個不確定進度的進度條（後端本來就沒有精確的百分比可顯示，因此不會顯示假的數字）。
- 掃描完成後，畫面會顯示 **Scan Completed（掃描已完成）** 區塊，包含：
  - `Status`：掃描結果狀態（例如 Completed、Partial、Failed、Cancelled）。
  - `Errors`：掃描過程中的錯誤數量；若有錯誤訊息也會顯示在下方。
  - `Reports`：已產生的報表檔案路徑清單。
- 底部按鈕：
  - **Cancel Scan（取消掃描）**：掃描進行中可隨時點擊以中止掃描。
  - **View Results（檢視結果）**：掃描完成後點擊，進入「結果儀表板」畫面。
  - **Start New Scan（開始新掃描）**：不論掃描是否完成，點擊即可返回「掃描設定」畫面重新開始。

**4. 探勘盤點（Inventory）** —— 從左側導覽點選「Inventory」即可看到這個畫面（掃描完成前顯示空狀態；不會重新掃描）：

- 上方為類別篩選下拉選單與搜尋框（可依名稱/類型/路徑搜尋），旁邊顯示目前篩選後的項目數量。
- 類別統計區塊：以色塊/文字列出該次掃描實際找到的每一種實體類型（例如 Service、Certificate、ComComponent 等）與各自的數量 —— 只會顯示實際存在的類型，不會列出該次掃描沒找到的類型。
- 下方為可捲動的清單，欄位包含類型、名稱、狀態、版本、架構、所屬應用程式（若該實體同時被多個應用程式邊界擁有，會全部列出，而非只顯示第一個；沒有歸屬的實體顯示「Unassigned」）、證據數量、路徑。
- 點選任一列會在清單下方展開該項目的詳細資料：識別資訊、所屬應用程式、證據（Evidence）清單、中繼資料（Metadata）。
- 這個畫面與結果儀表板內嵌的「Discovery Inventory」區塊是同一份資料、同一個元件，並非兩套邏輯 —— 差別只在於一個是獨立頁面、一個是內嵌於結果儀表板中。

**5. 結果儀表板（Results Dashboard）** —— 點擊「View Results」後看到的畫面，也可之後隨時從左側導覽點選「Results」返回（不會重新掃描）：

- 頂部 **Scan Summary（掃描摘要）**：目標、狀態、開始/結束時間、耗時、實體數、錯誤數、涵蓋範圍。
- **Risk Summary（風險摘要）**：依 Critical / High / Medium / Low / Informational 五個等級分別統計的風險發現數量。
- **Migration Summary（遷移摘要）**：依 Blocked（已阻擋）/ Needs Remediation（需要修復）/ Ready With Conditions（有條件就緒）/ Ready（已就緒）統計的應用程式數量。
- **Dependency Summary（依賴摘要）**：依外部依賴類型（資料庫、Redis、外部 API、LDAP、檔案共享等）統計的數量。
- **Applications（應用程式清單）**：可用文字方塊搜尋應用程式名稱、用下拉選單依風險等級篩選、勾選「Only with issues」只顯示有問題的項目。清單中點擊任一列即可在下方展開該應用程式的詳細資料（見下一節）。
- 往下依序可展開（點擊區塊標題即可收合/展開）：**Risk Findings（風險發現）**、**Migration Issues（遷移問題）**、**Migration Actions（遷移動作，僅供檢視，不提供執行按鈕）**、**Verification Checks（驗證檢查項目，僅供檢視）**、**Scanner Status（各掃描器執行狀態）**。
- **Reports（報表）** 區塊：下拉選單選擇已產生的報表檔案，點擊 **Open Report（開啟報表）** 會以純文字方式在下方文字框內顯示該檔案內容（不會重新產生報表，也不會把 HTML 當成網頁執行）。
- **Export Report（匯出報表）** 區塊：選擇 `Format`（JSON/HTML/Both）、`If a file already exists`（FailIfExists 或 Overwrite）與 `Output directory`，點擊 **Export Report** 進行匯出；成功或失敗都會在下方顯示結果訊息。
- 畫面右上角 **New Scan（新掃描）** 按鈕：直接返回「掃描設定」畫面開始新的一次掃描。

**6. 應用程式詳細資料（Application Detail）** —— 在結果儀表板或遷移評估畫面的應用程式清單中點選任一列後，會在清單下方展開此區塊（兩個畫面重用完全相同的元件）：

- **Risk（風險）**：該應用程式的整體風險等級、發現數量與信心水準，以及主要風險清單（Top Risks）。
- **Migration（遷移）**：該應用程式的遷移狀態，以及相關的 Issues（問題）、Actions（動作，僅供檢視）、Verification Checks（驗證檢查項目，分為遷移前/遷移後，僅供檢視）。
- **Dependencies（依賴項目）**：該應用程式所依賴的外部資源清單（類型與目標）。
- 以上所有內容均直接取自該次掃描已完成的結果，不會有任何重新計算或重新掃描的動作；畫面上不存在任何「執行」按鈕。

**7. 遷移評估（Migration）** —— 從左側導覽點選「Migration」即可看到這個畫面（掃描完成前顯示空狀態 + Start Scan 按鈕）：

- 頂部統計：Blocked（已阻擋）/ Needs Remediation（需要修復）/ Ready With Conditions（有條件就緒）/ Ready（已就緒）四個狀態各自的應用程式數量，直接取自既有的遷移評估結果，不會重新計算。
- 下方為應用程式清單，點選任一列會在下方展開「應用程式詳細資料」（見上一節），可檢視該應用程式的 Issues / Actions / Verification Checks / Dependencies。
- 這是**純檢視/評估**畫面 —— 沒有任何按鈕會實際執行遷移動作、套用修復、安裝套件、修改設定或重啟服務。

**8. 報表（Reports）** —— 從左側導覽點選「Reports」即可看到這個畫面（掃描完成前顯示空狀態 + Start Scan 按鈕）：

- **Latest Scan（最近一次掃描）**：目標、平台、完成時間、狀態。
- **Available Reports（可用的報表）**：下拉選單選擇已產生的報表檔案，點擊 **Open（開啟）** 會以純文字方式在下方文字框內顯示該檔案內容（JSON 與 HTML 皆以純文字呈現，不會把 HTML 當成網頁渲染，也不會執行任何 JavaScript）。
- **Export Report（匯出報表）**：選擇 `Format`（JSON/HTML/Both）、`If a file already exists`（FailIfExists 或 Overwrite）與 `Output directory`，點擊匯出；成功或失敗都會顯示結果訊息。
- 這個畫面重用與結果儀表板完全相同的匯出/檢視服務，並非另一套匯出邏輯。

**9. 設定（Settings）** —— 從左側導覽點選「Settings」即可看到這個畫面：

- **General（一般）**：Default Output Directory（預設輸出目錄）、Default Report Format（預設報表格式）、Default Overwrite Policy（預設覆蓋原則）、Verbose Output（詳細輸出）。這些值就是「掃描設定」畫面對應欄位目前的值本身（同一份狀態），修改後，下次打開「掃描設定」畫面就會看到新的預設值 —— 但**不會**回頭修改任何已經送出的掃描請求。
- **Language（語言）**：與視窗右上角相同的 EN / 中文切換按鈕，方便在此頁面直接切換。
- 這個頁面不會持久化到磁碟（關閉程式後重設），也不包含任何密碼/憑證相關欄位。

## 發布 / 下載即用版本（Release / Distribution）

v1.0.0 起，如果不想自行建置，可以用 `build-release.ps1`（Windows 主機）或 `build-release.sh`（Linux/macOS 主機）產生開箱即用的單一執行檔（自我包含、免安裝 .NET 執行環境）：

```powershell
# 在 Windows 上執行：建置 Windows GUI、Windows CLI、Linux CLI 三種執行檔，並打包成 ZIP/tar.gz
.\build-release.ps1
```

```bash
# 在 Linux/macOS 上執行：只建置 Linux CLI 執行檔（並打包成 tar.gz）
./build-release.sh
```

`build-release.ps1` 是 PowerShell 腳本，只能在 Windows 上執行；GUI（WPF，`net8.0-windows`）與 Windows 版 CLI（同樣使用 `net8.0-windows` TFM，以便編譯進 Windows 探勘能力）都只能在 Windows 主機上建置。如果你是在 Linux 環境下開發/建置，`build-release.sh` 讓你不需要 Windows 或 PowerShell 就能直接產生 Linux x64 的 CLI 執行檔；兩者都會輸出到同一個 `release/` 目錄結構，且都會更新 `release/SHA256SUMS.txt`（`build-release.sh` 只會更新/新增 Linux 那兩行，不會動到已存在的 Windows 校驗碼）。版本號統一從 [`Directory.Build.props`](Directory.Build.props) 讀取，兩支腳本都不會自行寫死版本字串。

會產出：

```
release/
├── windows/
│   ├── ServerSleuth.exe          # WPF 桌面 GUI（Windows x64，自我包含單一執行檔）
│   ├── serversleuth-cli.exe      # 命令列工具（Windows x64，自我包含單一執行檔）
│   ├── README.txt / VERSION      # 使用者導向的簡短說明（打包進 ZIP 內）
├── linux/
│   └── serversleuth              # 命令列工具（Linux x64，自我包含單一執行檔）
│       README.txt / VERSION      # 同上（打包進 tar.gz 內）
├── ServerSleuth-v1.0.0-windows-x64.zip
├── ServerSleuth-v1.0.0-linux-x64.tar.gz
├── SHA256SUMS.txt                 # 每個執行檔與每個壓縮檔的 SHA-256 校驗碼
└── VERSION                        # 純文字版本號
```

`release/`（如同先前的 `dist/`）已加入 `.gitignore`，不會進入 git 版本控制 —— 正式發布時應改用 GitHub Release 或其他 artifact 儲存機制上傳這些壓縮檔，而不是把上百 MB 的執行檔提交進原始碼庫。

### Windows 使用者

1. 下載 `ServerSleuth-v1.0.0-windows-x64.zip` 並解壓縮。
2. **桌面 GUI**：雙擊 `ServerSleuth.exe` 即可 —— 自我包含單一執行檔，免安裝 .NET 執行環境。
3. **命令列工具**：`serversleuth-cli.exe --help`。

### Linux 使用者

1. 下載 `ServerSleuth-v1.0.0-linux-x64.tar.gz` 並解壓縮：`tar -xzf ServerSleuth-v1.0.0-linux-x64.tar.gz`。
2. 賦予執行權限並執行：

   ```bash
   chmod +x serversleuth
   ./serversleuth --help
   ./serversleuth scan --output ./serversleuth-report
   ```

   這是自我包含的單一執行檔，已實際在不含 `dotnet`/.NET SDK 的獨立 Linux 環境（WSL2）中驗證過 `--help` 與一次完整的本機掃描（含 `report.json`/`report.html` 產出）皆可正常執行。

## 輸出內容

一次掃描完成後，輸出目錄中會包含：

- `report.json` — 完整的機器可讀報表（含掃描摘要、風險發現、遷移評估、依賴關係圖等）
- `report.html` — 對應的人類可讀報表（純靜態 HTML，零 JavaScript）

## 目前完成進度

專案已完成 Phase 1–10E-3（核心領域模型、Windows/Linux 探勘、關聯分析、風險引擎、遷移評估、報表輸出、CLI）以及 GUI-1 至 GUI-7C（WPF 應用程式外殼、掃描設定、掃描執行、結果儀表板、報表匯出/檢視、語言切換、探勘盤點、儀表板摘要、遷移評估、報表、設定，以及最終整合驗收與上線前健檢）。GUI 的七個導覽頁面（Dashboard / Scan / Inventory / Results / Migration / Reports / Settings）皆為完整可用的真實畫面，沒有任何一個仍是尚未實作的佔位畫面。詳細的版本異動請見 [`CHANGELOG.md`](docs/CHANGELOG.md)；GUI-7C 的最終驗收證據見 [`docs/releases/FINAL_RELEASE_SIGNOFF.md`](docs/releases/FINAL_RELEASE_SIGNOFF.md)。

> GUI 的互動式視覺驗證（實際點擊操作、畫面截圖）目前尚未在任何開發環境中執行過（本專案的開發流程沒有可用的 Windows 桌面自動化/截圖能力），此限制已在文件中如實記載。[`docs/releases/FINAL_USER_ACCEPTANCE_CHECKLIST.md`](docs/releases/FINAL_USER_ACCEPTANCE_CHECKLIST.md) 提供完整七個頁面的人工點擊驗收清單，正式驗收前建議由使用者在真實桌面環境中親自操作一次完整流程並勾選確認。

## 安全與隱私原則

- 唯讀探勘，不對受掃描系統做任何寫入/變更。
- 絕不外洩機密內容（密碼、連線字串、API 金鑰、Token、私鑰等一律遮罩）。
- 不執行任何被探勘到的或來路不明的二進位檔（偏好靜態解析 PE/ELF metadata）。
- 核心探勘流程不含遙測，預設不會把掃描資料上傳到任何地方。
- 不會用探勘到的憑證去連線資料庫、API 或其他外部系統。

## 延伸文件

- [`ARCHITECTURE.md`](docs/ARCHITECTURE.md) — 實際落地的架構與各階段設計決策記錄
- [`SCANNERS.md`](docs/SCANNERS.md) — 每個掃描器的用途、資料來源、所需權限與限制
- [`CHANGELOG.md`](docs/CHANGELOG.md) — 版本異動紀錄
- [`SECURITY.md`](docs/SECURITY.md) — 安全設計原則與弱點通報方式
- [`MIGRATION.md`](docs/MIGRATION.md) — 如何解讀 Migration Assessment 輸出、規劃實際遷移
- [`docs/releases/`](docs/releases/) — 各版本上線前的驗收/健檢一次性報告（例如 `FINAL_RELEASE_SIGNOFF.md`、`FINAL_USER_ACCEPTANCE_CHECKLIST.md`）
