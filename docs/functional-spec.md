# 功能規格與驗收標準 (Functional Spec & Acceptance Criteria)

> **文件版本**：v0.3.0　**最後更新**：2026-11-06
> **與 SRS 的關係**：[srs.md](srs.md) 說明「做什麼、為什麼」；本文件把每個需求拆成可驗證的 **FR / BR / AC**，用來規劃實作順序與判斷「做完了沒」。

## 如何閱讀

| 縮寫 | 全名 | 回答的問題 |
| :--- | :--- | :--- |
| **FR** | Functional Requirement 功能需求 | 系統要提供什麼能力？ |
| **BR** | Business Rule 業務規則 | 這個能力必須遵守什麼規則？ (不分實作方式都成立) |
| **AC** | Acceptance Criteria 驗收標準 | 怎樣算做對？ (Given / When / Then，可直接變成測試) |

**狀態圖例**

| 符號 | 意義 |
| :--- | :--- |
| ✅ | 已實作且有自動化測試 (不需 DB) |
| ⏳ | 程式已寫好，**需接上 PostgreSQL 才能驗證** |
| ⬜ | 尚未開始 |

**編號規則**：`FR-INV-01` 是需求，`BR-INV-03` 是規則，`AC-INV-01-1` 是 `FR-INV-01` 的第 1 條驗收標準。

---

## 1. Inventory & Pricing 模組 (目前實作重點)

### 1.1 功能需求

| ID | 需求 | 對應 SRS | 狀態 |
| :--- | :--- | :--- | :--- |
| FR-INV-01 | 查詢房型在指定區間的每晚剩餘房數、單晚售價、整段可訂房數與總價 | REQ-INV-01 | ✅ 邏輯 / ⏳ API |
| FR-INV-02 | 為一筆預訂佔住區間內每晚的房間，保留 15 分鐘 | REQ-INV-02 (Hold) | ✅ 邏輯 / ⏳ 鎖 |
| FR-INV-03 | 釋放某預訂的佔位，庫存回補 | REQ-INV-02 (Release) | ⬜ |
| FR-INV-04 | 確認某預訂的佔位 (準時付款後轉為永久)；逾期付款走重新驗證 (FR-INV-07) | REQ-INV-02 (Confirm) | ✅ 領域 / ⬜ 用例 |
| FR-INV-05 | 逾期且超過寬限期仍未確認的佔位自動釋放 | SRS 3.3 方案 A | ⬜ |
| FR-INV-06 | 房態與價格的建立 / 維護 (飯店方設定每日房量與價格) | SRS 2.2 | ⬜ |
| FR-INV-07 | 逾期付款重新驗證 (Inventory 端)：重新佔位並確認，或判定失敗交由 Payment 退款；訂單端的恢復流程見 [ADR-0001](adr/0001-late-payment-revalidation.md) | SRS 3.3 | ⬜ |

### 1.2 業務規則

| ID | 規則 | 實作位置 | 狀態 |
| :--- | :--- | :--- | :--- |
| BR-INV-01 | `0 ≤ 可用房數 ≤ 總房數`，任何情況都不可為負 (防超賣) | Domain `Reserve`/`Release` + DB CHECK | ✅ 邏輯 / ⏳ CHECK |
| BR-INV-02 | 同一房型同一天只有一筆庫存記錄 | DB 唯一索引 `uq_room_type_date` | ⏳ |
| BR-INV-03 | 住宿區間為 `[入住日, 退房日)`；退房日當天不計住宿；退房日必須晚於入住日 | `StayAvailability` | ✅ |
| BR-INV-04 | 整段可訂房數 = 區間內每晚可用房數的**最小值** (瓶頸) | `StayAvailability` | ✅ |
| BR-INV-05 | 區間內只要有一晚沒有庫存資料，整段視為不可訂 | `StayAvailability` | ✅ |
| BR-INV-06 | 佔位數量必須 > 0 | `HoldInventoryHandler` | ✅ |
| BR-INV-07 | 佔位為**全有或全無**：N 晚要嘛全部扣成功，要嘛全部不扣 (單一交易) | `HoldInventoryHandler` + `SaveChanges` | ✅ 邏輯 / ⏳ 交易 |
| BR-INV-08 | **Strict Expiration**：佔位有效期 15 分鐘，付款時間 ≤ 到期時間才算準時，沒有任何隱性延長 | `InventoryHold.IsExpired`、`Confirm` | ✅ |
| BR-INV-09 | 佔位狀態只能 `Held → Confirmed` 或 `Held → Released`；後兩者為終態，不可再轉換 | `InventoryHold` | ✅ |
| BR-INV-10 | 一筆佔位的庫存**只能回補一次** (避免重複釋放使庫存虛增) | 狀態機 + 排程只撈 `Held` | ✅ 狀態機 / ⬜ 排程 |
| BR-INV-14 | **Grace Period**：排程在逾期後再等寬限期 (預設 2 分鐘，可設定) 才釋放庫存；寬限期只延後「釋放」，**不放寬**付款準時與否的判斷 | `IsReleasableBySweeper` | ✅ 判斷 / ⬜ 排程與設定 |
| BR-INV-15 | 是否準時以金流回報的**付款時間 (`paid_at`)** 判斷，不以收到 Webhook 的時間判斷 | `Confirm(paidAt)` | ✅ |
| BR-INV-16 | **Late Payment Revalidation**：付款已成功但佔位已逾期 (或已被釋放) 時，重新檢查庫存與價格；仍可訂 → 重新佔位並確認，否則 → 取消訂單並自動退款 | Application (待實作) | ⬜ |
| BR-INV-11 | 多人同時搶同一筆庫存時，以版本號 (Version) 樂觀鎖決定先後；輸家重新讀取後重試，**最多 3 次** | `OptimisticRetry` | ✅ 邏輯 / ⏳ DB |
| BR-INV-12 | 只有「版本衝突」才重試；庫存不足、參數錯誤等業務失敗不重試 | `OptimisticRetry` | ✅ |
| BR-INV-13 | 同一預訂在同一天不得有兩筆有效佔位 (冪等) | 尚未有唯一索引 | ⬜ (Phase 5) |

### 1.3 驗收標準

#### FR-INV-01 查詢房態

| AC | Given / When / Then | 對應測試 | 狀態 |
| :--- | :--- | :--- | :--- |
| AC-INV-01-1 | **Given** 12/1、12/2、12/3 各剩 5、2、4 間 **When** 查 12/1~12/4 **Then** 整段可訂 2 間 | `StayAvailabilityTests.Calculate_TakesMinimumAcrossNights` | ✅ |
| AC-INV-01-2 | **Given** 12/4 已全滿 **When** 查 12/1~12/4 **Then** 不受影響 (退房日不計) | `Calculate_CheckOutDay_IsNotCounted` | ✅ |
| AC-INV-01-3 | **Given** 區間內某晚沒有庫存資料 **When** 查詢 **Then** 回傳 `DateNotAvailable` | `Calculate_MissingNight_Fails` | ✅ |
| AC-INV-01-4 | **Given** 退房日 ≤ 入住日 **When** 查詢 **Then** 回傳 `InvalidDateRange` (HTTP 400) | `Calculate_InvalidRange_Fails`、`GetAvailabilityHandlerTests.InvalidRange_ReturnsError` | ✅ |
| AC-INV-01-5 | **When** 查詢成功 **Then** 回傳每晚明細，且總價 = 每晚價格加總 | `GetAvailabilityHandlerTests.Returns_PerNightDetail_AndTotalPrice` | ✅ |
| AC-INV-01-6 | **When** 呼叫 `GET /api/inventory/availability` **Then** 回 200 與上述內容 | (整合測試) | ⏳ |

#### FR-INV-02 佔位

| AC | Given / When / Then | 對應測試 | 狀態 |
| :--- | :--- | :--- | :--- |
| AC-INV-02-1 | **Given** 熱門房型每晚 5 間 **When** 佔 2 間 3 晚 **Then** 成功，建立 3 筆佔位，每筆 2 間 | `HoldInventoryHandlerTests.Hold_CreatesOneHoldPerNight` | ✅ |
| AC-INV-02-2 | **Given** 每晚 5 間 **When** 佔 6 間 **Then** 回傳 `InsufficientRooms` (HTTP 409)，**不存檔** | `Hold_MoreThanAvailable_FailsWithoutSaving` | ✅ |
| AC-INV-02-3 | **Given** 數量 ≤ 0 **When** 佔位 **Then** 回傳 `InvalidQuantity` (HTTP 400) | `Hold_InvalidQuantity_Fails` | ✅ |
| AC-INV-02-4 | **Given** 日期超出已建立庫存 **When** 佔位 **Then** 回傳 `DateNotAvailable` (HTTP 404) | `Hold_DateRangeOutsideSeededData_Fails` | ✅ |
| AC-INV-02-5 | **Given** 存檔時發生版本衝突 **When** 佔位 **Then** 重新讀取後重試並成功，且不重複建立佔位 | `Hold_Conflict_RereadsAndRetries` | ✅ |
| AC-INV-02-6 | **Given** 持續衝突 **When** 重試 3 次 **Then** 回傳 `ConcurrencyConflict` (HTTP 409)，不留下任何佔位 | `Hold_ConflictEveryTime_GivesUpWithConflict` | ✅ |
| AC-INV-02-7 | **Given** 某房型某晚只剩 1 間 **When** 2 個請求同時佔 1 間 **Then** 恰好 1 個成功、1 個失敗，可用房數 = 0 | (整合測試，真實 DB) | ⏳ |
| AC-INV-02-8 | **Given** 熱門房型 5 間 **When** 100 個並行請求各佔 1 間 **Then** 成功數**恰為 5**，可用房數 = 0，不為負 | (整合測試 / k6) | ⏳ |
| AC-INV-02-9 | **Given** 3 晚中第 3 晚存檔失敗 **When** 交易回滾 **Then** 前 2 晚的庫存與佔位都沒有變動 (全有或全無) | (整合測試，真實 DB) | ⏳ |
| AC-INV-02-10 | **Given** 直接以 SQL 嘗試把可用房數改為負數 **Then** 被 CHECK constraint 拒絕 | (整合測試，真實 DB) | ⏳ |

#### FR-INV-03 釋放 / FR-INV-04 確認 / FR-INV-05 逾期釋放 / FR-INV-07 逾期付款重新驗證

| AC | Given / When / Then | 對應測試 | 狀態 |
| :--- | :--- | :--- | :--- |
| AC-INV-03-1 | **Given** 預訂有 3 筆 `Held` 佔位 **When** 釋放 **Then** 3 晚庫存各回補，佔位變 `Released` | — | ⬜ |
| AC-INV-03-2 | **Given** 佔位已 `Released` **When** 再釋放一次 **Then** 回傳 `HoldNotActive`，庫存不再增加 | `InventoryHoldTests.Confirm_ThenRelease_Fails` (狀態機) | ✅ 狀態機 / ⬜ 用例 |
| AC-INV-04-1 | **Given** 付款時間 < 到期時間 **When** 確認 **Then** 佔位變 `Confirmed`，庫存不回補 | `Confirm_PaidBeforeExpiry_Succeeds` | ✅ |
| AC-INV-04-2 | **Given** 付款時間 = 到期時間 **When** 確認 **Then** 仍有效 (邊界值) | `Confirm_PaidAtExactExpiry_Succeeds` | ✅ |
| AC-INV-04-3 | **Given** 付款時間 > 到期時間，即使 Webhook 在寬限期內才到 **When** 確認 **Then** 回傳 `HoldExpired`，佔位維持 `Held` | `Confirm_PaidAfterExpiry_FailsEvenWithinGracePeriod` | ✅ |
| AC-INV-04-4 | **Given** 佔位已被釋放 **When** 確認 **Then** 回傳 `HoldNotActive` (交由重新驗證流程處理) | `Confirm_AfterAlreadyReleased_FailsAsNotActive` | ✅ |
| AC-INV-05-1 | **Given** 已逾期但仍在寬限期內 **When** 排程執行 **Then** 不釋放 | `IsReleasableBySweeper_NotDuringGracePeriod` | ✅ 判斷 / ⬜ 排程 |
| AC-INV-05-2 | **Given** 逾期超過寬限期且仍 `Held` **When** 排程執行 **Then** 變 `Released`，庫存回補一次 | `IsReleasableBySweeper_NotDuringGracePeriod` | ✅ 判斷 / ⬜ 排程 |
| AC-INV-05-3 | **When** 排程連續執行兩次 **Then** 庫存只回補一次 | — | ⬜ |
| AC-INV-05-4 | **Given** 佔位 `Confirmed` **When** 排程執行 **Then** 不受影響 | `IsReleasableBySweeper_ConfirmedHold_IsNeverReleasable` | ✅ 判斷 / ⬜ 排程 |
| AC-INV-07-1 | **Given** 付款已成功但佔位已逾期、庫存仍有 **When** 重新驗證 **Then** 重新佔位並確認，訂單 `CONFIRMED` | — | ⬜ |
| AC-INV-07-2 | **Given** 付款已成功但佔位已逾期、庫存已被他人訂走 **When** 重新驗證 **Then** 訂單 `CANCELLED` 並觸發全額退款 | — | ⬜ |
| AC-INV-07-3 | **Given** 逾期期間房價已變動 **When** 重新驗證 **Then** 與原訂單價格快照不同則不自動確認，改為退款 (ADR-0001 Decision 2) | — | ⬜ |

### 1.4 逾期政策：Strict Expiration + Grace Period + Late Payment Revalidation

三個機制各自處理不同問題，缺一不可：

| 機制 | 解決什麼問題 | 規則 |
| :--- | :--- | :--- |
| **Strict Expiration** | 「到底算不算準時付款」要有唯一標準 | 以金流回報的**付款時間**對比佔位到期時間。`paid_at ≤ expires_at` 準時；晚一秒都算逾期 |
| **Grace Period** | 旅客在最後一分鐘付款，但金流 Webhook 慢了幾十秒才到，不能因此誤釋放 | 排程在 `expires_at + 寬限期` 之後才釋放庫存。寬限期內庫存仍被佔住，讓準時付款的 Webhook 有時間抵達 |
| **Late Payment Revalidation** | 付款時間已逾期 (或庫存已釋放) 但錢已經收了 | 重新檢查庫存與價格：都沒問題 → 重新佔位並確認；否則 → 取消並自動退款 |

**情境對照** (佔位 12:00 到期，寬限期 2 分鐘)：

| 付款時間 | Webhook 抵達 | 結果 |
| :--- | :--- | :--- |
| 11:59:50 | 12:00:30 (寬限期內) | 準時 → 確認成功 (靠 Grace Period 保住庫存) |
| 11:59:50 | 12:03:00 (已被釋放) | 準時但庫存已釋放 → 重新驗證；庫存仍有則確認，否則退款 |
| 12:00:30 | 12:00:40 | 逾期 → 重新驗證 (寬限期**不**延長付款期限) |
| 12:10:00 | 12:10:05 | 逾期 → 重新驗證 |

> **為什麼寬限期不延長付款期限**：若寬限期內付款也算準時，等於實際付款期限變成 17 分鐘，且「準時」的定義會依排程執行時機而變，難以測試與解釋。把「判斷準時」與「何時釋放庫存」分開，兩者就都是確定性的。

> **設定**：寬限期預設 2 分鐘，計畫以設定檔提供 (如 `Inventory:HoldGracePeriodSeconds`)，尚未實作；目前由呼叫端以參數傳入。數值為暫定，可依金流實際的 Webhook 延遲調整。

---

## 2. Catalog 模組 (未開始)

| FR | 需求 | 業務規則 | 驗收標準 (摘要) |
| :--- | :--- | :--- | :--- |
| FR-CAT-01 (REQ-CAT-01) 搜尋飯店 | 依城市 / 行政區 / 地標搜尋，並可依星級、價格區間、WiFi、停車場、早餐過濾 | BR-CAT-01 只顯示 `is_active = true` 的飯店<br>BR-CAT-02 價格過濾以 Inventory 回報的售價為準 (Catalog 不自行存價格，不跨模組 JOIN) | AC-1 搜尋「東京」只回東京的飯店<br>AC-2 多條件為 AND 關係<br>AC-3 查無結果回空清單而非錯誤<br>AC-4 停用的飯店不出現 |
| FR-CAT-02 (REQ-CAT-02) 房型詳情 | 顯示房型名稱、床型、成人/兒童人數上限、面積 | BR-CAT-03 房型一定隸屬單一飯店 | AC-1 查不存在的房型回 404<br>AC-2 回傳欄位完整 |

---

## 3. Booking 模組 (未開始)

| FR | 需求 | 業務規則 | 驗收標準 (摘要) |
| :--- | :--- | :--- | :--- |
| FR-BKG-01 (REQ-BKG-01) 建立訂單 | 填寫住客資料，成功佔位後建立 `PENDING_PAYMENT` 訂單並開始 15 分鐘倒數 | BR-BKG-01 **先佔位成功才建單**，佔位失敗不得留下訂單<br>BR-BKG-02 訂單到期時間 = 佔位到期時間<br>BR-BKG-03 同一 `Idempotency-Key` 只能建立一筆訂單<br>BR-BKG-04 訂單號全域唯一 | AC-1 庫存足夠 → 建單成功<br>AC-2 庫存不足 → 409，無訂單<br>AC-3 同 key 重送兩次 → 只有一筆訂單，第二次回同一結果<br>AC-4 必填欄位缺漏 → 400 |
| FR-BKG-02 (REQ-BKG-02) 查詢訂單 | 以手機 / Email + 驗證碼查歷史與有效訂單 | BR-BKG-05 只能看到自己的訂單 | AC-1 驗證碼錯誤 → 401<br>AC-2 只回傳該聯絡人的訂單 |
| FR-BKG-03 (REQ-BKG-03) 取消訂單 | 依取消政策計算退款並釋放庫存 | BR-BKG-06 入住前 48 小時以上免費取消<br>BR-BKG-07 48 小時內取消扣首晚房費<br>BR-BKG-08 只有 `PENDING_PAYMENT`、`CONFIRMED` 可取消 | AC-1 距入住 > 48h → 全額退款<br>AC-2 距入住 ≤ 48h → 退款 = 總額 − 首晚房費<br>AC-3 已取消的訂單再取消 → 失敗<br>AC-4 取消後庫存回補 |
| FR-BKG-04 訂單狀態機 | `PENDING_PAYMENT → CONFIRMED → COMPLETED`；任何非終態可 `→ CANCELLED` | BR-BKG-09 終態 (`CANCELLED`、`COMPLETED`) **不可再轉換，包含不可由 `CANCELLED` 回到 `CONFIRMED`**<br>BR-BKG-10 逾期且超過寬限期仍未付款 → 自動 `CANCELLED` (BR-INV-14)<br>BR-BKG-11 逾期後才收到的成功付款需經重新驗證 (BR-INV-16)，成功時**建立新 Booking**，不修改原訂單<br>BR-BKG-12 新 Booking 以 `OriginalBookingId` 關聯原訂單<br>BR-BKG-13 訂單建立時鎖定價格快照 (單價明細、總額、幣別) | AC-1 非法轉換被拒絕<br>AC-2 逾期未付 → 自動取消並釋放庫存<br>AC-3 已 `CANCELLED` 的訂單任何情況下都不能轉為 `CONFIRMED`<br>AC-4 逾期付款且庫存仍有、價格一致 → 建立新 Booking (`CONFIRMED`)，原訂單維持 `CANCELLED`，新訂單的 `OriginalBookingId` = 原訂單<br>AC-5 逾期付款且庫存不足 → 不建立新訂單，發起全額退款<br>AC-6 逾期付款但價格 / 幣別 / 重要條件與快照不一致 → 不建立新訂單，發起全額退款，不補差價<br>AC-7 同一筆付款的重新驗證被重送 → 只產生一筆 RecoveryAttempt 與最多一張新訂單<br>AC-8 恢復過程每一步的結果可由 RecoveryAttempt 查詢 |

---

## 4. Payment 模組 (未開始)

| FR | 需求 | 業務規則 | 驗收標準 (摘要) |
| :--- | :--- | :--- | :--- |
| FR-PAY-01 (REQ-PAY-01) 建立交易 | 取得付款連結 / Client Secret | BR-PAY-01 金額以訂單總額為準，不信任前端傳入<br>BR-PAY-02 外部交易單號唯一 | AC-1 金額與訂單不符 → 拒絕<br>AC-2 訂單已逾期 → 不能建立交易 |
| FR-PAY-02 Webhook | 驗證簽章後處理付款結果，發布 `PaymentCompletedEvent` | BR-PAY-03 簽章驗證失敗一律拒絕<br>BR-PAY-04 同一筆 Webhook 重送只處理一次<br>BR-PAY-05 付款成功但重新驗證失敗 (逾期且庫存不足，或價格 / 幣別 / 重要條件不符)，需自動全額退款，不得自動補差價<br>BR-PAY-07 Payment 與 Booking 狀態分離，付款透過 PaymentApplication 分配給訂單，一筆付款可由原訂單轉到新訂單<br>BR-PAY-06 Webhook 需帶金流回報的付款時間，作為準時判斷依據 (BR-INV-15) | AC-1 簽章錯誤 → 401，不改狀態<br>AC-2 重送同一事件 → 結果相同、不重複確認<br>AC-3 準時付款 → 訂單 `CONFIRMED`、佔位 `Confirmed`<br>AC-4 逾期付款 → 進入重新驗證，失敗則 `REFUNDED`<br>AC-5 重新驗證成功 → PaymentApplication 將付款分配給新 Booking |

---

## 5. 非功能驗收 (來自 SRS 第 6 章)

| ID | 標準 | 驗證方式 | 狀態 |
| :--- | :--- | :--- | :--- |
| NFR-01 | 搜尋 / 查價 API 99% 請求 < 150ms | k6 讀取壓測 (Phase 3 快取後) | ⬜ |
| NFR-02 | 搶購 API 1,000 TPS、回應 < 300ms、零超賣 | k6 | ⬜ |
| NFR-03 | 單一房型剩 5 間、100 VU 併發下單，庫存精準度 100% | k6 + 結束後查 DB 對帳 | ⬜ |
| NFR-04 | 每個請求有統一 `Trace-Id` | 整合測試檢查回應 header / 日誌 | ⬜ |
| NFR-05 | Repo 不含任何機密 | 提交前檢查；`.env` 與 `appsettings.*.json` 在 `.gitignore` | ✅ |

---

## 6. 決議與待決議事項

### 已決議

| # | 議題 | 決議 | 日期 |
| :--- | :--- | :--- | :--- |
| D1 | 訂單狀態名稱 | 統一為 `PENDING_PAYMENT / CONFIRMED / CANCELLED / COMPLETED`；已同步修改 `srs.md` 與 `database-design.md` | 2026-11-06 |
| D2 | 逾期佔位能否被確認 | 採 **Strict Expiration + Grace Period + Late Payment Revalidation**，見 1.4 節與 BR-INV-08、14、15、16 | 2026-11-06 |
| D3 | 已取消訂單能否復活 | **不可**。重新驗證成功時建立新 Booking (`OriginalBookingId`)，付款以 PaymentApplication 分配，過程由 RecoveryAttempt 記錄。見 [ADR-0001](adr/0001-late-payment-revalidation.md) | 2026-11-06 |
| D4 | 重新驗證的價格基準 | 比對原訂單**價格快照**；價格、幣別或重要預訂條件不一致即**退款**，不自動補差價。見 [ADR-0001](adr/0001-late-payment-revalidation.md) | 2026-11-06 |

### 待決議

> 另有 ADR-0001 衍生的細節待定事項 (重要預訂條件範圍、恢復時效上限等)，見 [ADR-0001 第 5.3 節](adr/0001-late-payment-revalidation.md)。

1. **佔位粒度**：目前每晚一筆 `inventory_holds`。若改成「一個預訂一筆 + 日期區間」，查詢與釋放較簡單，但每晚鎖定的對應關係要另外處理。
2. **取消政策的資料來源**：48 小時規則是全站固定，還是每個房型 / 飯店可設定？影響 Catalog 或 Booking 要不要存政策。
3. **庫存的時間單位**：`stay_date` 是以哪個時區的日期為準 (飯店當地)？影響「今天」能否訂、跨時區的查詢。

---

## 7. 建議實作順序與對應驗收

| 順序 | 內容 | 完成的判斷 |
| :--- | :--- | :--- |
| 1 ✅ | Inventory 領域邏輯、佔位 handler、查詢 handler、API 端點 | AC-INV-01-1~5、AC-INV-02-1~6 全綠 |
| 2 ⏳ | 接上 PostgreSQL：套用 migration、寫入 mock 資料、整合測試 | AC-INV-01-6、AC-INV-02-7~10 全綠 |
| 3 | 釋放 / 確認 / 逾期釋放 / 逾期付款重新驗證 (FR-INV-03~05、07) | AC-INV-03~05、07 全綠 |
| 4 | Redis 快取查詢 (Phase 3) | NFR-01 |
| 5 | Redis + Lua 預扣 (Phase 4) | NFR-02、NFR-03 |
| 6 | Booking、Payment、冪等性 (Phase 5) | FR-BKG-*、FR-PAY-*、BR-INV-13 |
| 7 | Catalog | FR-CAT-* |
