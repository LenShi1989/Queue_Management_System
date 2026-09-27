# 排隊號碼進度管理系統 v1.0 開發規格書

> 文件版本：v1.0  
> 建立日期：2026-09-27  
> 技術基準：Vue 3 + Vite + TypeScript + Tailwind CSS + Pinia + ASP.NET Core .NET 10 + EF Core + PostgreSQL/MSSQL + SignalR

---

## 1. 專案概述

### 1.1 專案名稱

**Queue Management System（QMS）排隊號碼進度管理系統**

### 1.2 專案目的

建立一套可支援取號、排隊、叫號、櫃台服務、過號、完成、即時進度查詢及統計分析的通用排隊管理平台。

系統需支援：

- Web 取號
- Kiosk 觸控取號
- 櫃台叫號
- 大型叫號顯示器
- 手機即時查詢
- QR Code 查詢
- SignalR 即時推播
- 多服務類型
- 多櫃台
- 優先權排隊
- 預估等待時間
- 歷程紀錄
- 權限管理
- 統計報表
- API 整合
- 未來可延伸 WMS / MES / AGV 任務排隊

---

# 2. 系統目標

## 2.1 核心目標

```text
取號
 ↓
等待
 ↓
叫號
 ↓
報到 / 處理
 ↓
完成
```

異常流程：

```text
等待 → 取消
叫號 → 過號
處理 → 轉移
```

## 2.2 非功能目標

| 項目 | 目標 |
|---|---|
| API | RESTful API |
| 即時通訊 | SignalR |
| 前端 | Vue 3 |
| ORM | EF Core |
| DB | PostgreSQL / MSSQL |
| Authentication | JWT |
| Authorization | RBAC |
| API 文件 | Swagger / OpenAPI |
| 部署 | Docker / IIS / Nginx |
| Encoding | UTF-8 |
| Timezone | Asia/Taipei |
| API 格式 | JSON |
| Log | Structured Logging |

---

# 3. 系統架構

```text
┌──────────────────────────────────────────────────────┐
│                   Queue Management System             │
└──────────────────────────────────────────────────────┘

 ┌─────────────┐   ┌──────────────┐   ┌──────────────┐
 │ Kiosk 取號  │   │ Vue Web      │   │ Mobile Web   │
 └──────┬──────┘   └──────┬───────┘   └──────┬───────┘
        │                 │                  │
        └─────────────────┼──────────────────┘
                          │ HTTPS / REST
                          ▼
                ┌──────────────────┐
                │ ASP.NET Core 10  │
                │                  │
                │ Queue API        │
                │ Auth / RBAC      │
                │ SignalR          │
                └────────┬─────────┘
                         │
             ┌───────────┼────────────┐
             ▼           ▼            ▼
       ┌──────────┐ ┌─────────┐ ┌──────────┐
       │PostgreSQL│ │  Redis  │ │ External │
       │ / MSSQL  │ │ Cache   │ │ Systems  │
       └──────────┘ └─────────┘ └──────────┘
                         │
                         ▼
                 SignalR Broadcast
                         │
           ┌─────────────┼─────────────┐
           ▼             ▼             ▼
       櫃台畫面       叫號螢幕       手機查詢
```

---

# 4. 系統角色

| Role | 說明 |
|---|---|
| Admin | 系統管理員 |
| Manager | 管理者 |
| Counter | 櫃台服務人員 |
| Display | 叫號顯示器 |
| Kiosk | 自助取號設備 |
| Mobile | 手機查詢使用者 |

---

# 5. 功能模組

## 5.1 基礎設定

- 服務站管理
- 服務類型管理
- 櫃台管理
- 號碼規則
- 優先權設定
- 營業時間
- 叫號設定
- 語音設定
- 顯示器設定

## 5.2 取號

- 一般取號
- VIP 取號
- 預約報到
- 優先取號
- QR Code
- Web 取號
- Kiosk 取號
- 號碼列印

## 5.3 排隊

- 等待佇列
- 排隊順序
- 優先權
- 插隊規則
- 等候人數
- 預估等待時間

## 5.4 櫃台

- 櫃台登入
- 下一號
- 再叫一次
- 開始服務
- 完成
- 過號
- 取消
- 轉移
- 暫停服務

## 5.5 顯示器

- 目前叫號
- 櫃台
- 下一號
- 等待列表
- 語音播放
- 多螢幕
- 廣告輪播

## 5.6 手機查詢

- 查詢號碼
- 目前叫號
- 前面人數
- 預估等待時間
- 即時進度
- QR Code

## 5.7 統計

- 每日取號
- 每日完成
- 過號
- 取消
- 平均等待時間
- 平均服務時間
- 各櫃台效率
- 尖峰時段
- 服務類型統計

---

# 6. Queue Ticket 狀態

```text
Waiting
   │
   ▼
Calling
   │
   ▼
Serving
   │
   ▼
Completed
```

異常：

```text
Waiting ─────→ Cancelled
Calling ─────→ NoShow
Serving ─────→ Transferred
```

### Status Enum

```csharp
public enum QueueTicketStatus
{
    Waiting = 1,
    Calling = 2,
    Serving = 3,
    Completed = 4,
    NoShow = 5,
    Cancelled = 6,
    Transferred = 7,
    Suspended = 8
}
```

---

# 7. 排隊號碼規則

## 7.1 基本格式

```text
A001
A002
A003
```

## 7.2 多服務類型

```text
A001   一般
V001   VIP
R001   預約
E001   急件
```

## 7.3 每日重新編號

```text
2026-09-27
A001
A002
A003

2026-09-28
A001
```

## 7.4 並發安全

不可使用：

```sql
MAX(SequenceNo) + 1
```

必須透過：

- Database Sequence
- Transaction
- Row Lock
- Distributed Lock（若使用 Redis）

避免：

```text
Request A → A015
Request B → A015
```

正確結果：

```text
Request A → A015
Request B → A016
```

---

# 8. 核心作業流程

## 8.1 一般取號

```text
使用者
 ↓
選擇服務
 ↓
POST /api/queue/tickets
 ↓
產生 Ticket
 ↓
取得號碼
 ↓
顯示 A028
 ↓
等待
```

## 8.2 櫃台叫號

```text
Counter
 ↓
下一號
 ↓
Queue Service
 ↓
取得下一個 Waiting Ticket
 ↓
更新 Calling
 ↓
SignalR Broadcast
 ↓
Display 更新
 ↓
Mobile 更新
```

## 8.3 完成服務

```text
Calling
 ↓
Serving
 ↓
Completed
 ↓
記錄服務時間
 ↓
下一號
```

---

# 9. Vue 3 前端架構

```text
src/
├── api/
│   ├── auth.ts
│   ├── queue.ts
│   ├── counter.ts
│   ├── service.ts
│   └── statistics.ts
│
├── components/
│   ├── QueueTicket.vue
│   ├── QueueStatus.vue
│   ├── CounterPanel.vue
│   ├── NumberDisplay.vue
│   └── WaitingList.vue
│
├── views/
│   ├── LoginView.vue
│   ├── KioskView.vue
│   ├── CounterView.vue
│   ├── DisplayView.vue
│   ├── MobileQueueView.vue
│   ├── DashboardView.vue
│   └── SettingsView.vue
│
├── stores/
│   ├── auth.ts
│   ├── queue.ts
│   ├── counter.ts
│   └── display.ts
│
├── services/
│   └── signalr.ts
│
├── router/
│   └── index.ts
│
└── types/
    ├── queue.ts
    ├── counter.ts
    └── service.ts
```

---

# 10. 前端主要畫面

## 10.1 Kiosk 取號

```text
┌──────────────────────────────┐
│       歡迎使用服務系統        │
│                              │
│      [ 一般服務 ]             │
│                              │
│      [ VIP服務 ]              │
│                              │
│      [ 預約報到 ]             │
└──────────────────────────────┘
```

## 10.2 取號結果

```text
您的號碼

A028

目前叫號：A021
前面等待：7 人
預估等待：21 分鐘
```

## 10.3 櫃台

```text
┌────────────────────────────┐
│ 3號櫃台                     │
├────────────────────────────┤
│                            │
│          A028              │
│                            │
│ [ 下一號 ] [ 再叫一次 ]     │
│ [ 完成 ]   [ 過號 ]         │
│ [ 轉移 ]   [ 暫停 ]         │
└────────────────────────────┘
```

## 10.4 叫號大螢幕

```text
┌──────────────────────────────┐
│          目前叫號             │
│                              │
│             A028             │
│                              │
│            3號櫃台            │
│                              │
│       下一號：A029            │
└──────────────────────────────┘
```

## 10.5 手機

```text
我的號碼：A028

目前叫號：A021

前面：7 人

預估等待：21 分鐘

A022
A023
A024
A025
A026
A027
★ A028
```

---

# 11. ASP.NET Core API

## 11.1 Ticket API

```http
POST   /api/queue/tickets
GET    /api/queue/tickets/{id}
GET    /api/queue/tickets/{id}/status
POST   /api/queue/tickets/{id}/cancel
```

## 11.2 Counter API

```http
POST /api/queue/counters/{counterId}/call-next
POST /api/queue/counters/{counterId}/recall
POST /api/queue/counters/{counterId}/start
POST /api/queue/counters/{counterId}/complete
POST /api/queue/counters/{counterId}/no-show
POST /api/queue/counters/{counterId}/transfer
POST /api/queue/counters/{counterId}/pause
```

## 11.3 Query API

```http
GET /api/queue/current
GET /api/queue/waiting
GET /api/queue/statistics
GET /api/queue/display
```

## 11.4 Service API

```http
GET    /api/queue/services
POST   /api/queue/services
PUT    /api/queue/services/{id}
DELETE /api/queue/services/{id}
```

## 11.5 Counter API

```http
GET    /api/queue/counters
POST   /api/queue/counters
PUT    /api/queue/counters/{id}
DELETE /api/queue/counters/{id}
```

---

# 12. API 範例

## 12.1 取號

```http
POST /api/queue/tickets
Content-Type: application/json
```

```json
{
  "serviceId": 1,
  "priority": 10
}
```

Response：

```json
{
  "ticketId": 1028,
  "ticketNo": "A028",
  "status": "Waiting",
  "position": 7,
  "estimatedMinutes": 21,
  "createdAt": "2026-09-27T20:00:00+08:00"
}
```

## 12.2 下一號

```http
POST /api/queue/counters/3/call-next
```

Response：

```json
{
  "ticketId": 1029,
  "ticketNo": "A029",
  "counterId": 3,
  "counterNo": "3",
  "status": "Calling"
}
```

---

# 13. SignalR

Hub：

```text
/hubs/queue
```

## Server → Client Events

```text
QueueCreated
QueueCalled
QueueRecalled
QueueStarted
QueueCompleted
QueueNoShow
QueueCancelled
QueueTransferred
QueueUpdated
DisplayUpdated
```

## Event 範例

```json
{
  "ticketNo": "A028",
  "counterNo": "3",
  "status": "Calling",
  "timestamp": "2026-09-27T20:01:00+08:00"
}
```

## Vue

```typescript
connection.on("QueueCalled", (data) => {
  queueStore.updateCurrentTicket(data)
})
```

---

# 14. EF Core Entity

## QueueTicket

```csharp
public class QueueTicket
{
    public long Id { get; set; }

    public DateOnly QueueDate { get; set; }

    public string TicketNo { get; set; } = string.Empty;

    public string Prefix { get; set; } = string.Empty;

    public int SequenceNo { get; set; }

    public long ServiceId { get; set; }

    public long? CounterId { get; set; }

    public QueueTicketStatus Status { get; set; }

    public int Priority { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CalledAt { get; set; }

    public DateTimeOffset? ServingAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public DateTimeOffset? NoShowAt { get; set; }

    public string? Remark { get; set; }
}
```

---

# 15. Database Schema

## 15.1 QueueService

```text
QueueService
├── Id
├── Code
├── Name
├── Prefix
├── Priority
├── IsActive
├── CreatedAt
└── UpdatedAt
```

## 15.2 QueueCounter

```text
QueueCounter
├── Id
├── Code
├── Name
├── ServiceId
├── Status
├── IsActive
├── CreatedAt
└── UpdatedAt
```

## 15.3 QueueTicket

```text
QueueTicket
├── Id
├── QueueDate
├── TicketNo
├── Prefix
├── SequenceNo
├── ServiceId
├── CounterId
├── Status
├── Priority
├── CreatedAt
├── CalledAt
├── ServingAt
├── CompletedAt
├── CancelledAt
├── NoShowAt
└── Remark
```

## 15.4 QueueTicketHistory

```text
QueueTicketHistory
├── Id
├── TicketId
├── FromStatus
├── ToStatus
├── CounterId
├── OperatorId
├── Action
├── CreatedAt
└── Remark
```

## 15.5 QueueDailySequence

```text
QueueDailySequence
├── Id
├── QueueDate
├── ServiceId
├── Prefix
├── CurrentNumber
└── UpdatedAt
```

---

# 16. PostgreSQL 建議索引

```sql
CREATE INDEX ix_queue_ticket_date_status
ON queue_ticket(queue_date, status);

CREATE INDEX ix_queue_ticket_service_status
ON queue_ticket(service_id, status);

CREATE INDEX ix_queue_ticket_counter_status
ON queue_ticket(counter_id, status);

CREATE UNIQUE INDEX ux_queue_ticket_date_prefix_sequence
ON queue_ticket(queue_date, prefix, sequence_no);
```

---

# 17. 排隊演算法

## 17.1 基本 FIFO

```text
Priority DESC
+
CreatedAt ASC
```

SQL 概念：

```sql
ORDER BY
    priority DESC,
    created_at ASC
```

## 17.2 防止 VIP 永遠插隊

可以增加 Aging 機制：

```text
等待時間越久
→ 有效優先權逐漸增加
```

例如：

```text
EffectivePriority =
BasePriority + WaitingMinutes / AgingFactor
```

此功能列為 Phase 2。

---

# 18. 預估等待時間

第一階段：

```text
EstimatedWait =
前面人數 × 平均服務時間 ÷ 有效櫃台數
```

例如：

```text
前面：7 人
平均服務：3 分鐘
櫃台：1

Estimated = 7 × 3
          = 21 分鐘
```

第二階段加入：

- 各服務類型平均時間
- 不同櫃台服務能力
- 尖峰時段
- 歷史資料
- VIP / Priority
- 實際即時服務時間

---

# 19. 權限控制

## Admin

```text
User
Role
Service
Counter
Queue Setting
System Setting
Statistics
```

## Manager

```text
Queue
Counter
Statistics
部分設定
```

## Counter

```text
Call
Recall
Start
Complete
NoShow
Transfer
```

## Display

```text
Read Queue Status
Receive SignalR Event
```

## Kiosk

```text
Create Ticket
Read Service
```

---

# 20. Authentication

採用：

```text
ASP.NET Core Identity
+
JWT Bearer
+
Role / Permission
```

Token：

```json
{
  "sub": "123",
  "name": "Counter01",
  "role": "Counter",
  "counterId": "3"
}
```

---

# 21. Audit Log

所有重要操作必須記錄：

```text
誰
何時
哪個號碼
哪個櫃台
原狀態
新狀態
執行動作
IP
Device
```

例如：

```text
2026-09-27 20:03
Operator: Counter03
Ticket: A028
Action: Complete
From: Serving
To: Completed
```

---

# 22. 統計 Dashboard

## 今日 KPI

```text
取號數       326
完成數       298
等待中        28
過號          18
取消          10

平均等待      12.5 分鐘
平均服務       8.3 分鐘
```

## 報表

- 每小時取號量
- 每日取號量
- 每服務類型
- 每櫃台
- 平均等待
- 平均服務
- 過號率
- 取消率
- 尖峰時段

---

# 23. 語音叫號

支援：

```text
請 A028 號
至 3 號櫃台
```

建議架構：

```text
SignalR
 ↓
Display
 ↓
Web Speech API
```

未來可整合：

- Azure Speech
- Google TTS
- 本機 TTS
- Windows TTS

---

# 24. QR Code

每張票可產生：

```text
Queue Ticket ID
+
Signed Token
```

例如：

```text
https://queue.example.com/q/eyJ...
```

手機查詢：

```text
GET /api/public/queue/{token}
```

注意：

- 不直接暴露資料庫 ID
- Token 必須具備有效期限
- 避免使用者修改 URL 查詢其他票號

---

# 25. Security

必須實作：

- HTTPS
- JWT
- RBAC
- Rate Limit
- Input Validation
- SQL Injection 防護
- XSS 防護
- CSRF 規則
- CORS
- Audit Log
- QR Token 防偽
- API Rate Limiting
- Sensitive Data Masking

---

# 26. Docker 部署

建議服務：

```text
docker-compose.yml

services:
  queue-api
  queue-web
  postgres
  redis
  nginx
```

架構：

```text
Internet / LAN
      │
      ▼
    Nginx
   ┌──┴──┐
   ▼     ▼
 Web     API
         │
    ┌────┴────┐
    ▼         ▼
PostgreSQL   Redis
```

---

# 27. IIS 部署

如果部署到 Windows Server：

```text
IIS
 ↓
Vue Static Files

IIS Reverse Proxy
 ↓
ASP.NET Core API
 ↓
PostgreSQL / MSSQL
```

SignalR 必須確認：

- WebSocket
- Reverse Proxy
- Sticky Session（多節點環境）
- Load Balancer
- Redis Backplane（多 API 節點）

---

# 28. Logging

推薦：

```text
Serilog
+
Structured Logging
```

Log：

```text
Information
Warning
Error
Critical
```

重要事件：

```text
TicketCreated
TicketCalled
TicketCompleted
TicketNoShow
TicketCancelled
LoginFailed
AuthorizationFailed
SystemError
```

---

# 29. Health Check

API：

```http
GET /health
GET /health/live
GET /health/ready
```

檢查：

```text
API
Database
Redis
SignalR
External API
```

---

# 30. Swagger

啟用：

```text
/swagger
```

API 分組：

```text
Auth
Queue
Counter
Service
Display
Statistics
Admin
```

---

# 31. 測試策略

## Unit Test

測試：

- 號碼產生
- 排隊排序
- Priority
- 狀態轉換
- 等待時間
- 預估時間

## Integration Test

測試：

- API
- EF Core
- PostgreSQL
- Transaction
- SignalR

## E2E Test

流程：

```text
取號
 ↓
櫃台叫號
 ↓
Display 更新
 ↓
手機更新
 ↓
完成
```

## Concurrency Test

同時 100 個 Request：

```text
POST /api/queue/tickets
```

要求：

```text
不能產生重複號碼
```

---

# 32. API Error Format

統一：

```json
{
  "success": false,
  "code": "QUEUE_NOT_FOUND",
  "message": "找不到排隊號碼",
  "traceId": "abc123"
}
```

成功：

```json
{
  "success": true,
  "data": {}
}
```

---

# 33. 建議專案結構

```text
QueueManagement/
│
├── backend/
│   ├── Queue.Api/
│   ├── Queue.Application/
│   ├── Queue.Domain/
│   ├── Queue.Infrastructure/
│   └── Queue.Tests/
│
├── frontend/
│   └── queue-web/
│
├── deploy/
│   ├── docker/
│   ├── nginx/
│   └── iis/
│
├── database/
│   ├── migrations/
│   └── scripts/
│
├── docs/
│   └── QueueManagementSystem.md
│
├── docker-compose.yml
└── README.md
```

---

# 34. Phase 開發計畫

## Phase 1：MVP

目標：建立可運作的基本叫號系統。

### Backend

- ASP.NET Core .NET 10
- EF Core
- PostgreSQL / MSSQL
- QueueService
- QueueCounter
- QueueTicket
- Ticket History
- REST API
- JWT
- Swagger

### Frontend

- Vue 3
- Vite
- TypeScript
- Tailwind
- Pinia
- Kiosk
- Counter
- Display
- Mobile Query

### 即時

- SignalR
- QueueCalled
- QueueCompleted
- Display Update

### 完成條件

```text
取號
 ↓
A001
 ↓
櫃台下一號
 ↓
Display 顯示
 ↓
完成
```

---

# 35. Phase 2

加入：

- 多服務
- VIP
- Priority
- 預約
- 過號
- 轉移
- QR Code
- 語音
- Redis
- 統計 Dashboard
- 報表
- 排隊預估時間
- Kiosk Printer
- 多櫃台策略

---

# 36. Phase 3

加入智慧化：

- AI 等待時間預測
- 動態優先權
- 尖峰預測
- 人力配置建議
- 多站點
- 多分店
- 雲端管理
- 多租戶
- WMS 整合
- MES 整合
- AGV 任務排程

---

# 37. WMS / AGV 延伸架構

此系統可以直接延伸成 WMS Task Queue。

```text
Queue Engine
     │
     ├── Customer Queue
     │
     ├── WMS Work Queue
     │
     ├── Picking Queue
     │
     ├── Packing Queue
     │
     ├── Loading Queue
     │
     └── AGV Task Queue
```

例如：

```text
Task #10028

待處理
 ↓
排隊
 ↓
派工
 ↓
AGV 接單
 ↓
前往起點
 ↓
搬運
 ↓
前往終點
 ↓
完成
```

因此 Queue Engine 可抽象成：

```csharp
Queue<TTask>
```

未來可與：

```text
WMS
MES
MCS
RCS
AGV
ERP
```

整合。

---

# 38. 建議 MVP 開發順序

```text
01 建立 Solution
       ↓
02 建立 Domain
       ↓
03 建立 EF Core Entity
       ↓
04 建立 PostgreSQL
       ↓
05 Migration
       ↓
06 Queue API
       ↓
07 Ticket Service
       ↓
08 Counter API
       ↓
09 SignalR
       ↓
10 Vue 3
       ↓
11 Kiosk
       ↓
12 Counter
       ↓
13 Display
       ↓
14 Mobile Query
       ↓
15 JWT / RBAC
       ↓
16 Statistics
       ↓
17 Docker
       ↓
18 Integration Test
```

---

# 39. Definition of Done

## Backend

- [ ] API 可正常啟動
- [ ] Swagger 正常
- [ ] DB Migration 正常
- [ ] CRUD 完成
- [ ] Ticket 取號完成
- [ ] Counter 叫號完成
- [ ] Status Flow 完成
- [ ] SignalR 完成
- [ ] JWT 完成
- [ ] RBAC 完成
- [ ] Audit Log 完成
- [ ] Health Check 完成

## Frontend

- [ ] Kiosk 完成
- [ ] Counter 完成
- [ ] Display 完成
- [ ] Mobile 完成
- [ ] SignalR 即時更新
- [ ] Responsive
- [ ] Error Handling
- [ ] Loading State

## Database

- [ ] Table
- [ ] FK
- [ ] Index
- [ ] Unique Constraint
- [ ] Transaction
- [ ] Migration
- [ ] Backup

## Deployment

- [ ] Docker
- [ ] Nginx
- [ ] HTTPS
- [ ] Environment Variables
- [ ] Health Check
- [ ] Log
- [ ] Backup

---

# 40. 環境變數

```env
ASPNETCORE_ENVIRONMENT=Production

ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=queue;Username=queue;Password=***

Jwt__Issuer=QueueManagement
Jwt__Audience=QueueWeb
Jwt__Secret=***

Redis__ConnectionString=redis:6379

Cors__AllowedOrigins=https://queue.example.com
```

---

# 41. 未來擴充

## 多站點

```text
Site A
 ├── Counter 1
 ├── Counter 2
 └── Counter 3

Site B
 ├── Counter 1
 └── Counter 2
```

## 多租戶

```text
Tenant
 ├── Site
 ├── Service
 ├── Counter
 ├── Queue
 └── User
```

## WMS

```text
Warehouse
 ├── WorkStation
 ├── WorkQueue
 ├── Task
 └── Worker
```

## AGV

```text
Task Queue
 ↓
Dispatcher
 ↓
RCS
 ↓
AGV
```

---

# 42. 最終技術選型

| Layer | Technology |
|---|---|
| Frontend | Vue 3 |
| Build | Vite |
| Language | TypeScript |
| CSS | Tailwind CSS |
| State | Pinia |
| Backend | ASP.NET Core .NET 10 |
| ORM | EF Core |
| API | REST |
| Realtime | SignalR |
| Authentication | JWT |
| Authorization | RBAC |
| Database | PostgreSQL / MSSQL |
| Cache | Redis |
| API Docs | Swagger / OpenAPI |
| Logging | Serilog |
| Container | Docker |
| Reverse Proxy | Nginx / IIS |
| Testing | xUnit + Integration Test |
| CI/CD | GitLab CI/CD |

---

# 43. MVP 最終功能清單

```text
[✓] 服務類型
[✓] 櫃台
[✓] 取號
[✓] 排隊
[✓] 下一號
[✓] 再叫一次
[✓] 開始服務
[✓] 完成
[✓] 過號
[✓] 取消
[✓] 轉移
[✓] 即時叫號
[✓] 大螢幕
[✓] 手機查詢
[✓] QR Code
[✓] SignalR
[✓] JWT
[✓] RBAC
[✓] Audit Log
[✓] Dashboard
[✓] Statistics
[✓] Health Check
[✓] Swagger
[✓] Docker
```

---

# 44. 開發結論

本系統第一階段應以「穩定、即時、並發安全」為主要目標。

核心設計原則：

1. **Queue Ticket 與 Queue Status 分離**
2. **號碼產生必須具備並發安全**
3. **所有狀態異動都建立 History**
4. **SignalR 作為即時進度通知機制**
5. **API 與前端分離**
6. **服務類型與櫃台採可配置設計**
7. **排隊演算法不可寫死**
8. **預估等待時間可逐步升級**
9. **權限與 Audit Log 從 MVP 即納入**
10. **架構預留 WMS / MES / AGV 整合能力**

最終形成：

```text
                  Queue Engine
                       │
        ┌──────────────┼──────────────┐
        ▼              ▼              ▼
   Customer Queue   Work Queue    AGV Queue
        │              │              │
        ▼              ▼              ▼
      Kiosk           WMS            RCS
      Counter         MES            AGV
      Display
      Mobile
```

**本規格書可作為後續 AI Coding Agent（Claude Code / OpenCode / OpenClaw）建立專案、產生資料庫、API、Vue 前端及測試程式的基準文件。**
