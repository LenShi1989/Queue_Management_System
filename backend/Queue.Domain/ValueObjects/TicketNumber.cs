namespace Queue.Domain.ValueObjects;

/// <summary>
/// 票號格式化：Prefix + 左側零填補序號（§7.1 A001）
/// </summary>
public readonly record struct TicketNumber
{
    public string Prefix { get; }

    public int SequenceNo { get; }

    public int NumberLength { get; }

    public TicketNumber(string prefix, int sequenceNo, int numberLength)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("Prefix 不可為空", nameof(prefix));
        }

        if (numberLength is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(numberLength), "NumberLength 必須介於 1 ~ 10");
        }

        Prefix = prefix.Trim().ToUpperInvariant();
        SequenceNo = sequenceNo;
        NumberLength = numberLength;
    }

    public string Value => $"{Prefix}{SequenceNo.ToString().PadLeft(NumberLength, '0')}";

    public override string ToString() => Value;
}

/// <summary>
/// 排隊排序規則（§17.1）：Priority DESC, CreatedAt ASC, Id ASC
/// 有效優先權可含老化加成（§17.2 Phase 2）：BasePriority + WaitingMinutes / AgingFactor
/// </summary>
public static class QueueOrdering
{
    public const string SqlOrderBy =
        "priority DESC, created_at ASC, id ASC";

    /// <summary>
    /// 計算有效優先權。若未啟用老化則等同原始 Priority。
    /// </summary>
    public static int EffectivePriority(int basePriority, DateTimeOffset createdAt, DateTimeOffset now, bool agingEnabled, int agingFactor)
    {
        if (!agingEnabled || agingFactor <= 0)
        {
            return basePriority;
        }

        var waitingMinutes = Math.Max(0, (now - createdAt).TotalMinutes);
        return basePriority + (int)(waitingMinutes / agingFactor);
    }
}
