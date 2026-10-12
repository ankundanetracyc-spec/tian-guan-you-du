using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

/// <summary>
/// The sect-owned item account. Balances are derived from its unique movement ledger,
/// so stock cannot change without a traceable deposit or withdrawal record.
/// </summary>
[GlobalClass]
public partial class YuxuSectTreasuryLedger : Resource
{
    private const string BeastBoneDutyId = "beast_bone_submission";
    private const string YaopuCultivationDutyId = "yaopu_cultivate_mist_bell";
    private const string BeastBoneItemPath = "res://resources/entities/items/兽骨.tres";
    private const string MistBellFlowerItemPath = "res://resources/entities/items/雾铃花.tres";

    [Export] public Array<YuxuSectTreasuryRecord> records = [];

    public long get_item_balance(string itemPath)
    {
        if (string.IsNullOrWhiteSpace(itemPath)) return 0;
        records ??= [];
        long balance = 0;
        foreach (var record in records)
        {
            if (record is null || record.item_path != itemPath || record.quantity <= 0) continue;
            if (record.movement_kind == YuxuSectTreasuryRecord.KindDeposit)
                balance += record.quantity;
            else if (record.movement_kind == YuxuSectTreasuryRecord.KindWithdrawal)
                balance -= record.quantity;
        }
        return balance;
    }

    public bool contains_source_transaction(string sourceTransactionId)
    {
        if (string.IsNullOrWhiteSpace(sourceTransactionId)) return false;
        records ??= [];
        foreach (var record in records)
            if (record is not null && record.source_transaction_id == sourceTransactionId)
                return true;
        return false;
    }

    public bool try_record_movement(YuxuSectTreasuryRecord? record)
    {
        if (record is null || !record.is_valid()) return false;
        if (record.movement_kind == YuxuSectTreasuryRecord.KindDeposit && !is_supported_deposit(record)) return false;
        records ??= [];
        foreach (var existing in records)
            if (existing is not null &&
                (existing.record_id == record.record_id || existing.source_transaction_id == record.source_transaction_id))
                return false;

        if (record.movement_kind == YuxuSectTreasuryRecord.KindWithdrawal &&
            get_item_balance(record.item_path) < record.quantity)
            return false;

        records.Add(record.Duplicate(true) as YuxuSectTreasuryRecord ?? record);
        return true;
    }

    /// <summary>Record an explicit future duty withdrawal; the source transaction ID is mandatory and unique.</summary>
    public bool try_record_withdrawal(
        string sourceTransactionId,
        string sourceDutyId,
        string sourceTitle,
        string itemPath,
        int quantity,
        string occurredAt)
    {
        return try_record_movement(new YuxuSectTreasuryRecord
        {
            record_id = Guid.NewGuid().ToString("N"),
            source_transaction_id = sourceTransactionId,
            source_duty_id = sourceDutyId,
            movement_kind = YuxuSectTreasuryRecord.KindWithdrawal,
            item_path = itemPath,
            quantity = quantity,
            source_title = sourceTitle,
            occurred_at = occurredAt,
        });
    }

    public bool remove_record_by_source_transaction(string sourceTransactionId)
    {
        if (string.IsNullOrWhiteSpace(sourceTransactionId)) return false;
        records ??= [];
        for (var i = records.Count - 1; i >= 0; i--)
        {
            if (records[i] is not { } record || record.source_transaction_id != sourceTransactionId) continue;
            records.RemoveAt(i);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Backfill only historical official-duty receipts whose stable source ID names one of the two
    /// explicitly defined deposits. Narrative summaries are never parsed for item or quantity data.
    /// </summary>
    public int backfill_v2_delivery_records(HumanData? playerData)
    {
        if (playerData is null) return 0;
        var contributionRecords = playerData.sect_contribution_records;
        if (contributionRecords is null || contributionRecords.Count == 0) return 0;

        var sourceCounts = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var contribution in contributionRecords)
        {
            if (contribution is null || string.IsNullOrWhiteSpace(contribution.source_transaction_id)) continue;
            sourceCounts[contribution.source_transaction_id] = sourceCounts.GetValueOrDefault(contribution.source_transaction_id) + 1;
        }

        var backfilled = 0;
        foreach (var contribution in contributionRecords)
        {
            if (contribution is null || string.IsNullOrWhiteSpace(contribution.record_id) ||
                string.IsNullOrWhiteSpace(contribution.source_transaction_id) ||
                string.IsNullOrWhiteSpace(contribution.occurred_at) ||
                sourceCounts.GetValueOrDefault(contribution.source_transaction_id) != 1 ||
                (contribution.transaction_kind != YuxuSectContributionRecord.KindOfficialDutyReward &&
                 !string.IsNullOrWhiteSpace(contribution.transaction_kind)) ||
                !try_get_v2_deposit_definition(contribution.source_transaction_id, out var dutyId, out var itemPath, out var quantity, out var title) ||
                contains_source_transaction(contribution.source_transaction_id))
                continue;

            var record = new YuxuSectTreasuryRecord
            {
                record_id = $"v2-backfill:{contribution.source_transaction_id}",
                source_transaction_id = contribution.source_transaction_id,
                source_duty_id = dutyId,
                movement_kind = YuxuSectTreasuryRecord.KindDeposit,
                item_path = itemPath,
                quantity = quantity,
                source_title = title,
                occurred_at = contribution.occurred_at,
            };
            if (try_record_movement(record)) backfilled++;
        }
        return backfilled;
    }

    private static bool try_get_v2_deposit_definition(
        string sourceTransactionId,
        out string dutyId,
        out string itemPath,
        out int quantity,
        out string title)
    {
        dutyId = "";
        itemPath = "";
        quantity = 0;
        title = "";

        var bonePrefix = $"yuxu:official:{BeastBoneDutyId}:";
        if (has_guid_instance_id(sourceTransactionId, bonePrefix))
        {
            dutyId = BeastBoneDutyId;
            itemPath = BeastBoneItemPath;
            quantity = 1;
            title = "历史回填·上缴兽骨";
            return true;
        }

        var flowerPrefix = $"yuxu:official:{YaopuCultivationDutyId}:";
        if (!has_guid_instance_id(sourceTransactionId, flowerPrefix)) return false;
        dutyId = YaopuCultivationDutyId;
        itemPath = MistBellFlowerItemPath;
        quantity = 3;
        title = "历史回填·培育雾铃花";
        return true;
    }

    private static bool has_guid_instance_id(string sourceTransactionId, string prefix)
    {
        if (!sourceTransactionId.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var instanceId = sourceTransactionId[prefix.Length..];
        return instanceId.Length == 32 && Guid.TryParseExact(instanceId, "N", out _);
    }

    private static bool is_supported_deposit(YuxuSectTreasuryRecord record)
    {
        var bonePrefix = $"yuxu:official:{BeastBoneDutyId}:";
        if (record.source_duty_id == BeastBoneDutyId && record.item_path == BeastBoneItemPath &&
            record.quantity == 1 && has_guid_instance_id(record.source_transaction_id, bonePrefix))
            return true;

        var flowerPrefix = $"yuxu:official:{YaopuCultivationDutyId}:";
        return record.source_duty_id == YaopuCultivationDutyId && record.item_path == MistBellFlowerItemPath &&
               record.quantity == 3 && has_guid_instance_id(record.source_transaction_id, flowerPrefix);
    }
}
