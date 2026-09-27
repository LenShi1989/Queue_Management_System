# Queue Management System

排隊叫號系統：Kiosk 自助取號、櫃台叫號、叫號顯示器、手機 QR 查詢、後台管理與統計分析。

- 規格書：[`docs/Queue_Management_System_v1.0_開發規格書.md`](docs/Queue_Management_System_v1.0_開發規格書.md)

## 技術棧

| 層級 | 技術 |
| --- | --- |
| 後端 | .NET 10 / ASP.NET Core Minimal-hosting Controller、EF Core 10、PostgreSQL 18、JWT、SignalR、Serilog、Swashbuckle |
| 命名慣例 | `EFCore.NamingConventions`（snake_case） |
| 前端 | Vue 3 + TypeScript + Vite、Pinia、Vue Router、Axios、Tailwind CSS 4、`@microsoft/signalr` |
| 測試 | xUnit + EF Core InMemory |

## 專案結構

```
backend/
  Queue.Domain/          實體、列舉、角色、狀態機、例外
  Queue.Application/     DTO、抽象介面、應用服務、QR/密碼/JWT
  Queue.Infrastructure/  EF Core、Migration、種子資料、DI
  Queue.Api/             Controllers、SignalR Hub、Program、appsettings
  Queue.Tests/           xUnit 測試
frontend/queue-web/      Vue 3 SPA
deploy/
  docker-compose.yml     PostgreSQL + API + Nginx
  docker/                Dockerfile.api、Dockerfile.web
  nginx/default.conf     反向代理（含 WebSocket 升級）
database/scripts/        初始化與備份腳本
docs/                    規格書
```

## 快速開始（本機開發）

### 需求

- .NET SDK 10
- PostgreSQL 18
- Node.js 24 + pnpm 10

### 1. 資料庫

```bash
createdb -U postgres queue
```

連線字串預設在 `backend/Queue.Api/appsettings.json` 的
`ConnectionStrings:DefaultConnection`。種子資料（角色、服務、櫃台、測試帳號）
由 Migration 的 `HasData` 寫入，啟動時會自動套用。

### 2. 後端

```bash
dotnet run --project backend/Queue.Api
```

- API：`http://localhost:5244`
- Swagger UI：`http://localhost:5244/swagger`
- 健康檢查：`http://localhost:5244/health/ready`
- SignalR：`/hubs/queue`

> `Jwt:Secret` 與 `Qr:SigningKey` 長度不足 32 字元會**拒絕啟動**（`OptionGuard.EnsureJwtSecret`）。
> 正式環境請以環境變數注入：`Jwt__Secret`、`Qr__SigningKey`。

### 3. 前端

```bash
cd frontend/queue-web
pnpm install
pnpm dev
```

- 網站：`http://localhost:5173`
- 開發代理已將 `/api`、`/hubs`、`/health` 轉送至 `http://localhost:5244`

### 測試帳號

| 帳號 | 密碼 | 角色 |
| --- | --- | --- |
| `admin` | `a12345678` | Admin, Manager |
| `counter1` | `counter123` | Counter |
| `kiosk` | `kiosk123` | Kiosk |

## 測試與建置

```bash
# 後端單元測試
dotnet test QueueManagement.slnx

# 後端建置
dotnet build backend/Queue.Api --nologo

# 前端型別檢查 + production build
cd frontend/queue-web && pnpm build
```

### 端對端 Smoke Test

需後端與 PostgreSQL 皆已啟動，並先 `cd frontend/queue-web`：

```bash
node smoke-test.mjs
```

腳本會連線 SignalR、登入、取號、叫號、開始、完成，並驗證
`QueueCreated` / `QueueCalled` / `QueueStarted` / `QueueCompleted` /
`DisplayUpdated` 事件確實送達。

## Docker 部署

```bash
cp .env.example .env
# 編輯 .env，至少設定 POSTGRES_PASSWORD / JWT_SECRET / QR_SIGNING_KEY
# 產生方式：openssl rand -base64 48

docker compose -f deploy/docker-compose.yml up -d --build
```

啟動後：

- 前端：`http://localhost:8080`
- API：`http://localhost:8080/api`
- Swagger：`http://localhost:8080/swagger`
- 健康檢查：`http://localhost:8080/health/ready`

Nginx 已處理 SPA fallback 與 SignalR WebSocket 升級。
正式環境請在 Nginx 前置 TLS 終端並將 `PUBLIC_BASE_URL` 設為對外 HTTPS 網址
（QR Code 連結依此產生）。

### 設定對照

| 環境變數 | 對應 appsettings | 說明 |
| --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | `ConnectionStrings:DefaultConnection` | PostgreSQL 連線字串 |
| `Jwt__Secret` | `Jwt:Secret` | ≥32 字元，否則拒絕啟動 |
| `Qr__SigningKey` | `Qr:SigningKey` | QR Token HMAC 密鑰 |
| `Qr__PublicBaseUrl` | `Qr:PublicBaseUrl` | QR 對外連結基底網址 |
| `Queue__*` | `Queue:*` | 排隊規則（§17 / §18） |
| `Database__MigrateOnStartup` | `Database:MigrateOnStartup` | 啟動時自動套用 Migration |

## 資料庫維護

```bash
# 初始化（災難復原／手動建庫）
./database/scripts/init-db.sh

# 每日備份（建議排入 cron，保留 14 天）
./database/scripts/backup-db.sh
# Windows：schtasks 建立排程執行 database/scripts/backup-db.bat
```

## 角色與權限

| 角色 | 說明 |
| --- | --- |
| `Admin` | 全部權限 |
| `Manager` | 後台管理、統計、服務/櫃台/使用者設定 |
| `Counter` | 櫃台叫號、再叫、開始、完成、過號、轉移 |
| `Display` | 顯示器資料與事件 |
| `Kiosk` | 自助取號 |
| `Mobile` | 手機查詢 |

## 票據狀態機

```
Waiting ──► Calling ──► Serving ──► Completed
   │           │  ▲        │
   │           │  └────────┘ (再叫)
   │           ├─► NoShow ──► Waiting
   │           └─► Waiting  (取消插隊/轉單)
   ├─► Cancelled
   └──► Suspended ──► Waiting / Calling
```

`Serving` 可轉 `Transferred`；`Completed` / `Cancelled` / `Transferred` 為終態。
非法轉換會拋出 `INVALID_STATUS_TRANSITION`。

## 已知限制

- `Redis:ConnectionString` 預設為空，尚未接入 Redis；目前狀態儲存於 PostgreSQL。
- `appsettings.json` 內含本機開發用資料庫密碼，**僅供開發**，正式環境請改用環境變數。
