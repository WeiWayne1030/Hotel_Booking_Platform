using MediatR;

namespace OTA.Shared.Kernel.Domain;

/// <summary>
/// 領域事件介面，繼承自 MediatR 的 INotification，支援在進程內以發布/訂閱模型分派事件
/// </summary>
public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTimeOffset OccurredOn { get; }
}

/// <summary>
/// 領域事件基礎抽象紀錄，預設自動生成 EventId 與觸發時間
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
}
