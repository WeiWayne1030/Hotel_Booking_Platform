namespace OTA.Shared.Kernel.Domain;

/// <summary>
/// DDD 聚合根 (Aggregate Root) 基底類別，作為交易一致性邊界的守護者，負責管理與發布領域事件 (Domain Events)
/// </summary>
/// <typeparam name="TId">聚合根唯一識別碼型別</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot() { }

    protected AggregateRoot(TId id) : base(id) { }

    /// <summary>
    /// 註冊領域事件，等待工作單元 (Unit of Work) 提交時一併分派
    /// </summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// 清除所有已被分派的領域事件
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
