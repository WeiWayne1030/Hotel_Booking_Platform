# OTA (Hotel Booking Platform) - Modular Monolith

> 一站式飯店預訂系統 (Online Travel Agency - Hotel Booking Engine)，採用 **.NET 9 模組化單體架構 (Modular Monolith)** 設計，聚焦於**高併發房態查詢、庫存防超賣、分散式鎖與防重複下單 (Idempotency)** 之實踐。

---

> [!CAUTION]
> ### ⚠️ 敏感資料防護與資安管理規範 (Security Notice)
> **本專案嚴格禁止提交任何機密資訊至 Git 版本控制庫！**
> 
> 1. **切勿提交真實連線字串與密碼**：
>    - 包括 PostgreSQL 密碼、Redis 連線字串、金流金鑰 (Stripe / Line Pay Secret Key)、JWT 私鑰與數位憑證 (`*.pfx`, `*.pem`, `*.key`)。
> 2. **本機開發敏感資訊管理 (推薦方式)**：
>    - 請使用 .NET 內建的 **User Secrets** 工具，密鑰將儲存在本機使用者目錄，絕不會隨程式碼上傳：
>      ```bash
>      cd src/Host/OTA.Api
>      dotnet user-secrets init
>      dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Database=ota_db;Username=postgres;Password=YOUR_LOCAL_PASSWORD"
>      dotnet user-secrets set "Redis:ConnectionString" "localhost:6379,password=YOUR_REDIS_PASSWORD"
>      ```
> 3. **CI/CD 與正式環境**：
>    - 一律採用環境變數 (Environment Variables) 或雲端密鑰庫 (如 Azure Key Vault / AWS Secrets Manager) 注入。
> 4. **設定檔範本原則**：
>    - 程式庫中僅保留 `appsettings.json` 作為設定結構範本，內部連線字串與金鑰一律使用佔位符（如 `YOUR_POSTGRES_PASSWORD`），真正的配置已透過 `.gitignore` 阻絕上傳。

---

## 專案架構概觀

本專案採用 **Modular Monolith** 風格，在單一部署單元中落實領域驅動 (DDD) 的嚴格模組邊界：

```text
OTA/
├── docs/                                    # 專案架構與需求文件
│   ├── srs.md                               # 軟體需求規格說明書 (高併發與飯店預訂)
│   └── database-design.md                   # 資料庫設計與 DDL 規格 (PostgreSQL)
├── src/
│   ├── Host/
│   │   └── OTA.Api/                         # Web API 啟動進入點、全域中介軟體
│   ├── Modules/
│   │   ├── Catalog/                         # 飯店與房型目錄 (展示、地點查詢)
│   │   ├── Inventory/                       # 核心高併發戰場 (房態日曆、Redis Lua 預扣庫存)
│   │   ├── Booking/                         # 預訂流程 (佔位倒數、超時自動釋放)
│   │   └── Payment/                         # 金流結算與通知
│   └── Shared/
│       ├── OTA.Shared.Kernel/               # 基礎 DDD 元件 (Entity, ValueObject, IDomainEvent)
│       └── OTA.Shared.Infrastructure/       # 共用設施 (Redis 輔助類、Idempotency 冪等過濾器)
├── tests/
│   ├── OTA.ArchitectureTests/               # 架構守門員 (NetArchTest: 自動驗證禁止跨模組違規引用)
│   └── OTA.Modules.Inventory.Tests/         # 庫存併發單元測試
└── load-tests/                              # k6 高併發壓測腳本
```

## 開發環境需求
- **.NET SDK**：9.0+
- **Database**：PostgreSQL 16+
- **Cache**：Redis 7+
- **Load Testing**：k6
