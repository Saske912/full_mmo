using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mmo.Client.Gateway
{
    /// <summary>POST /v1/session</summary>
    public sealed class SessionRequest
    {
        [JsonProperty("player_id")] public string PlayerId;
        [JsonProperty("display_name")] public string DisplayName;
        [JsonProperty("resolve_x")] public double? ResolveX;
        [JsonProperty("resolve_z")] public double? ResolveZ;
    }

    public sealed class SessionResponse
    {
        [JsonProperty("token")] public string Token;
        [JsonProperty("quests")] public List<QuestApiRow> Quests;
        [JsonProperty("stats")] public PlayerStatsDto Stats;
        [JsonProperty("wallet")] public WalletDto Wallet;
        [JsonProperty("inventory")] public object InventoryRaw;
        [JsonProperty("items")] public List<ItemStackDto> Items;
    }

    public sealed class PlayerStatsDto
    {
        [JsonProperty("level")] public int Level;
        [JsonProperty("xp")] public int Xp;
    }

    public sealed class WalletDto
    {
        [JsonProperty("gold")] public long Gold;
    }

    public sealed class ItemStackDto
    {
        [JsonProperty("item_id")] public string ItemId;
        [JsonProperty("quantity")] public int Quantity;
        [JsonProperty("display_name")] public string DisplayName;
    }

    public sealed class QuestApiRow
    {
        [JsonProperty("quest_id")] public string QuestId;
        [JsonProperty("state")] public string State;
        [JsonProperty("progress")] public int Progress;
        [JsonProperty("target_progress")] public int TargetProgress;
        [JsonProperty("prerequisite_quest_id")] public string PrerequisiteQuestId;
    }

    public sealed class QuestProgressRequest
    {
        [JsonProperty("quest_id")] public string QuestId;
        [JsonProperty("progress")] public int Progress;
    }

    public sealed class QuestProgressResponse
    {
        [JsonProperty("ok")] public bool Ok;
        [JsonProperty("completed")] public bool Completed;
        [JsonProperty("progress")] public int Progress;
        [JsonProperty("target_progress")] public int TargetProgress;
        [JsonProperty("already_complete")] public bool AlreadyComplete;
        [JsonProperty("gold_reward")] public long? GoldReward;
        [JsonProperty("items_rewarded")] public List<ItemStackDto> ItemsRewarded;
        [JsonProperty("newly_unlocked_quests")] public List<string> NewlyUnlockedQuests;
    }

    public sealed class ItemsRemoveRequest
    {
        [JsonProperty("item_id")] public string ItemId;
        [JsonProperty("quantity")] public int Quantity;
    }

    public sealed class ItemsTransferRequest
    {
        [JsonProperty("to_player_id")] public string ToPlayerId;
        [JsonProperty("item_id")] public string ItemId;
        [JsonProperty("quantity")] public int Quantity;
    }

    public sealed class OkResponse
    {
        [JsonProperty("ok")] public bool Ok;
    }

    public sealed class ResolvePreviewResponse
    {
        [JsonProperty("resolve_x")] public double ResolveX;
        [JsonProperty("resolve_z")] public double ResolveZ;
        [JsonProperty("resolved")] public ResolvedCellBundle Resolved;
        [JsonProperty("last_cell")] public LastCellBundle LastCell;
        [JsonProperty("cell_id_mismatch")] public bool CellIdMismatch;
    }

    public sealed class ResolvedCellBundle
    {
        [JsonProperty("found")] public bool Found;
        [JsonProperty("cell_id")] public string CellId;
        [JsonProperty("grpc_endpoint")] public string GrpcEndpoint;
    }

    public sealed class LastCellBundle
    {
        [JsonProperty("found")] public bool Found;
        [JsonProperty("cell_id")] public string CellId;
        [JsonProperty("resolve_x")] public double? ResolveX;
        [JsonProperty("resolve_z")] public double? ResolveZ;
    }
}
