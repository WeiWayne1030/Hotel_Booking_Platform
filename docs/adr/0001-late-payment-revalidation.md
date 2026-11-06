# ADR-0001：Late Payment Revalidation 策略

| 項目 | 內容 |
| :--- | :--- |
| **狀態** | Accepted |
| **日期** | 2026-11-06 |
| **範圍** | Booking 模組 (並涉及 Payment、Inventory 的協作) |
| **相關文件** | [functional-spec.md](../functional-spec.md) 第 1.4 節 (逾期政策)、BR-INV-16、BR-BKG-09 |

---

## 1. 背景 (Context)

逾期政策已決議採 Strict Expiration + Grace Period + Late Payment Revalidation。其中 **Late Payment Revalidation** 處理的情境是：

> 旅客的付款已成功 (錢已收到)，但付款時間晚於佔位到期時間，或庫存已被釋放，原訂單已經 `CANCELLED`。

此時有兩個設計問題需要定案：

1. 重新驗證成功時，已 `CANCELLED` 的訂單要如何處理？
2. 重新驗證時，價格以什麼為準？

這兩點會直接決定 Booking 狀態機、Payment 與 Booking 的關聯方式，以及資料表設計。

---

## 2. 決策 (Decision)

### Decision 1：訂單恢復策略

- **不允許**已進入 `CANCELLED` 的 Booking 回復為 `CONFIRMED`。
- Late Payment Revalidation 成功時，**建立新的 Booking**。
- 新 Booking 以 `OriginalBookingId` 關聯原始 Booking。
- 原始 Payment 透過 **PaymentApplication** 分配給新 Booking。
- **RecoveryAttempt** 負責記錄整個恢復過程。

### Decision 2：價格驗證策略

- 重新驗證時，**必須**比對原始訂單的**價格快照 (Price Snapshot)**。
- 價格、幣別或重要預訂條件不一致時，**不允許自動確認**。
- 目前採取「**價格不同即退款**」的政策。
- **不允許**未經使用者同意自動補差價。

---

## 3. 考慮過的替代方案 (Alternatives Considered)

| 方案 | 說明 | 未採用原因 |
| :--- | :--- | :--- |
| 讓 `CANCELLED` 恢復為 `CONFIRMED` | 在原訂單上直接改回狀態，實作最簡單 | 終態被打破；取消時已發出的通知、已釋放的庫存、已計算的退款都要一併回溯；稽核歷史會出現「取消又復活」，難以還原事實 |
| 逾期取消不算終態 | 把 `CANCELLED` 拆成「可恢復」與「不可恢復」兩種 | 狀態機變複雜，每個消費端都要判斷是哪一種取消 |
| 價格不同時自動補差價 | 讓更貴時扣款補足、更便宜時退差額 | 未經旅客同意扣款有法遵與客訴風險；需要額外的授權流程 |
| 以最新價格為準 | 重新驗證時按現價成交 | 旅客實際支付的金額與成交價不符，需要處理差額，等同於上一個方案 |

---

## 4. 後果 (Consequences)

### 優點

- **終態不可逆**：`CANCELLED` 與 `COMPLETED` 維持 BR-BKG-09 的定義，狀態機簡單且可預測。
- **保留完整訂單歷史**：原訂單的取消事實不被覆寫，新訂單透過 `OriginalBookingId` 串起因果。
- **Payment 與 Booking 狀態分離**：付款狀態不再等同於訂單狀態，一筆付款可以對應到不同的訂單。
- **支援重試、冪等及補償交易**：恢復流程有自己的紀錄，可重複執行而不重複成交，失敗時可退款補償。

### 缺點

- 必須新增 **RecoveryAttempt**。
- 必須處理 **Payment 與新 Booking 的關聯** (PaymentApplication)。
- 增加**跨模組協調** (Booking、Payment、Inventory) 及**退款流程**的複雜度。

---

## 5. 後續工作與細節補充 (Follow-ups)

以下是為了落實本決策而衍生的項目，**尚未實作**。標示「待定」者是本 ADR 沒有明確規定、需要在實作前補決議的細節。

### 5.1 概念與資料

| 項目 | 說明 |
| :--- | :--- |
| `Booking.OriginalBookingId` | 可為空。一般訂單為空；經恢復產生的新訂單指向被取消的原訂單 |
| `Booking.PriceSnapshot` | 建立訂單當下鎖定的單價明細、總額、幣別。價格比對的唯一依據 |
| `PaymentApplication` | 記錄「某筆 Payment 的多少金額被分配給哪一個 Booking」。付款不再與訂單一對一綁定 |
| `RecoveryAttempt` | 記錄一次恢復嘗試：原訂單、付款、各步驟結果 (重新佔位、價格比對、新訂單建立、退款)、最終結局 |

### 5.2 恢復流程 (示意)

```
收到成功付款，原訂單已 CANCELLED 或佔位已逾期
  → 建立 RecoveryAttempt (以付款交易編號做冪等鍵；同一筆付款重送不重複建立)
  → 比對價格快照與現價/幣別/重要條件
        不一致 → 結局 = 退款
  → 重新佔位 (HoldInventory，沿用現有用例)
        庫存不足 → 結局 = 退款
  → 建立新 Booking (OriginalBookingId = 原訂單)，PaymentApplication 把付款分配給新訂單
  → 確認佔位 → 新 Booking = CONFIRMED，結局 = 恢復成功
任何一步失敗 → 補償：釋放已重新佔住的庫存，並發起退款
```

### 5.3 待定事項

1. **「重要預訂條件」的範圍**：至少應包含房型、入住日、退房日、房間數；取消政策版本是否納入需決定。
2. **價格比對的基準**：是比對「原訂單價格快照」與「現在的每晚售價」，還是只比對總額？本 ADR 的要求是比對快照，具體欄位待 `PriceSnapshot` 設計時定義。
3. **恢復的時效上限**：付款晚到多久內還嘗試恢復？超過是否直接退款？
4. **新 Booking 的付款期限**：新訂單已有付款，應直接進入 `CONFIRMED`，不需再經過 `PENDING_PAYMENT`，需確認狀態機是否允許這條路徑。
5. **通知內容**：旅客收到的是「訂單已恢復成功」還是「新訂單已成立」，影響訂單編號的呈現。

### 5.4 對現有文件的影響

- [functional-spec.md](../functional-spec.md)：待決議第 3、5 項改為已決議；BR-BKG-11 與 BR-PAY-05 補充；新增 Booking 與 Payment 的 BR/AC。
- [database-design.md](../database-design.md)：之後需新增 `original_booking_id`、價格快照欄位、`payment_applications`、`recovery_attempts`，尚未修改。

---

## 6. 不在此 ADR 範圍內

- 退款本身的實作 (金流介接細節)。
- 寬限期的長度與設定方式 (見 functional-spec 1.4 節)。
- Inventory 端的 `Confirm` 與 `IsReleasableBySweeper` (已於 Domain 實作，不受本決策影響)。
