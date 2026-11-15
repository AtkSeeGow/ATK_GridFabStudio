# GridFabStudio

以瀏覽器設計 Gridfinity 相容收納系統，直接預覽、產生並下載可列印的 STL。

![GridFabStudio 的 Gridfinity 模組化收納概念](src/GridFabStudio.App/assets/media/gridfinity-concept.png)

[線上展示](https://atkseegow.hopto.org/GridFabStudio.App/)

## 功能

- **基本收納盒**：設定 X／Y 格數、高度、壁厚，以及磁鐵孔或螺絲孔。
- **輕量底板**：依格數或實際毫米尺寸產生底板，可調間隙、磁鐵孔與交叉點固定孔。
- **有蓋外箱**：分件產生箱身、盒蓋、翻扣與折疊提把，支援堆疊定位。
- **即時 3D 預覽**：使用 Three.js 在瀏覽器內檢視產出的 STL。
- **STL 詢價**：檢查模型尺寸與網格，透過 PrusaSlicer 估算耗材、列印時間與材料費。
- **詢價後台**：查看詢價內容、下載原始 STL，並儲存正式報價。
- **Gridfinity 資源**：提供系統介紹、規劃入口與精選專用模型索引。

> [!NOTE]
> SCAD 模型與配合公差仍需依印表機、材料及切片參數實際試印。請先用小尺寸模型驗證，再進行大型列印。

## 技術架構

| 層級 | 技術 |
| --- | --- |
| 前端 | Angular 21、TypeScript 5.9、Three.js |
| API | ASP.NET Core 10 Minimal API |
| 參數化建模 | OpenSCAD |
| 切片與估價 | PrusaSlicer CLI |

前端透過 `/api` 呼叫後端；後端把使用者參數傳給 OpenSCAD，於獨立暫存目錄產生 STL，再交由前端預覽或下載。詢價流程會解析 STL，並在 PrusaSlicer 可用時以實際切片結果估算材料用量。

## 開始使用

### 環境需求

- [Node.js 24](https://nodejs.org/)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [OpenSCAD](https://openscad.org/downloads.html)
- PrusaSlicer CLI（選用；僅 STL 自動估價需要）

### 1. 取得專案與安裝前端套件

```powershell
git clone <YOUR_REPOSITORY_URL>
cd ATK_GridFabStudio
npm.cmd --prefix src/GridFabStudio.App install
```

### 2. 啟動 API

請先設定至少 16 字元的本機後台金鑰：

```powershell
$env:Quotes__AdminKey = Read-Host '輸入本機後台金鑰（至少 16 字元）'
dotnet run --project src/GridFabStudio.Api/GridFabStudio.Api.csproj
```

API 預設位於 `http://127.0.0.1:15080`。OpenSCAD 若已加入 `PATH`，或位於專案的 `.tools/openscad/openscad-2021.01/openscad.exe`，系統會自動尋找；也可手動指定：

```powershell
$env:OpenScad__Executable = 'C:\Program Files\OpenSCAD\openscad.exe'
```

### 3. 啟動前端

另開一個 PowerShell 終端：

```powershell
npm.cmd --prefix src/GridFabStudio.App start
```

開啟 [http://localhost:4200](http://localhost:4200)。Angular 開發伺服器會依 `proxy.json` 將 `/api` 轉送至本機 API。

## 設定

可在 `src/GridFabStudio.Api/appsettings.json` 設定，或使用 ASP.NET Core 的雙底線環境變數覆寫，例如 `OpenScad__Executable`。

| 設定 | 用途 | 預設值 |
| --- | --- | --- |
| `Quotes__AdminKey` | 詢價後台金鑰；啟用後台前必須設定 | 空白 |
| `Quotes__Storage` | 詢價與上傳 STL 的儲存位置 | `.gridfab-data/inquiries` |
| `Quotes__MaterialPricePerKg` | PLA 每公斤材料費 | `150` |
| `OpenScad__Executable` | OpenSCAD 執行檔或命令 | 自動偵測 |
| `OpenScad__CaseCellBudget` | 單次外箱產模的格數上限 | `64` |
| `Slicer__Executable` | PrusaSlicer CLI 或包裝腳本 | `prusa-slicer` |
| `Slicer__ConfigPath` | 固定切片設定檔 | `SlicerProfiles/gridfab-pla.ini` |
| `Slicer__TimeoutSeconds` | 單次切片逾時秒數 | `180` |

若 PrusaSlicer 未安裝、模型無法切片或作業逾時，詢價仍可送出，但價格會標記為需要人工審核。請勿提交 `.env`、管理金鑰或正式詢價資料；相關路徑已列入 `.gitignore`。

## 建置

```powershell
# 前端 production build
npm.cmd --prefix src/GridFabStudio.App run build

# 後端 Release build
dotnet build src/GridFabStudio.sln -c Release
```

## 專案結構

```text
ATK_GridFabStudio/
├─ src/
│  ├─ GridFabStudio.App/       Angular 前端、Three.js 預覽與網站素材
│  ├─ GridFabStudio.Api/       ASP.NET Core API、SCAD 模型與切片設定
│  └─ GridFabStudio.sln
└─ README.md
```

## 模型限制

- 基本收納盒：X／Y 各 `1–4` 格，高度 `1–9U`。
- 輕量底板：X／Y 各 `1–6` 格，或寬深各 `42–252 mm`。
- 有蓋外箱：高度 `2–8U`，總格數預設不超過 `64`。
- STL 詢價：單檔最多 `10 MB`、`100,000` 個三角面、數量 `1–100`。
- 預設印表機範圍：`256 × 256 × 260 mm`，可在 `Printer` 設定中調整。

GridFabStudio 採用 42 mm 格距與 7 mm 高度單位；設計參考 [Gridfinity specification](https://gridfinity.xyz/specification/)。
