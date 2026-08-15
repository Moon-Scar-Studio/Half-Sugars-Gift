//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Security.Cryptography;
//using System.Text;
//using Virial;
//using Virial.Assignable;
//using Virial.Game;
//using Virial.Helpers;

//namespace NebulaN.Systems.RankSystem;

//#region JSON
//[Serializable]
//public class RankRecord
//{
//    [JsonSerializableField(true, false)] public long Time;
//    [JsonSerializableField(true, false)] public int Score;
//    [JsonSerializableField(true, false)] public string Reason = "";
//}

//[Serializable]
//public class RankSaveData
//{
//    [JsonSerializableField(true, false)] public string FriendCode = "";
//    [JsonSerializableField(true, false)] public int Score = 0;
//    [JsonSerializableField(true, false)] public List<RankRecord> Records = new();
//    [JsonSerializableField(true, false)] public string Hash = "";
//}
//#endregion

//public interface IRankSystem
//{
//    int WinScore => 0;
//    int LoseScore => 0;
//    bool CloseRank => false;
//}

//public static class RankSystem
//{
//    private static readonly string SaveDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NebulaN", "Rank");
//    private static readonly string SavePath = Path.Combine(SaveDirectory, "rank.json");
//    private const string HashKey = "NebulaN_Rank_System";
//    private static Dictionary<string, RankSaveData> Datas = new();

//    public static void Initialize() => Load();

//    #region Rank
//    public static int GetRank(byte playerId)
//    {
//        var player = GamePlayer.GetPlayer(playerId);
//        if (player == null) return 255;
//        return GetRank(PatchManager.GetFriendCode(player.VanillaPlayer));
//    }

//    public static int GetRank(string friendCode)
//    {
//        var data = GetData(friendCode);
//        if (data == null) return 255;
//        return CalculateRank(data.Score);
//    }

//    public static int GetRankScore(byte playerId)
//    {
//        var player = GamePlayer.GetPlayer(playerId);
//        if (player == null) return -1;
//        return GetRankScore(PatchManager.GetFriendCode(player.VanillaPlayer));
//    }

//    public static int GetRankScore(string friendCode) => GetData(friendCode)?.Score ?? -1;

//    private static int CalculateRank(int score)
//    {
//        if (score < 100) return 1;
//        if (score < 250) return 2;
//        if (score < 500) return 3;
//        if (score < 900) return 4;
//        if (score < 1500) return 5;
//        return 6;
//    }
//    #endregion

//    #region Add Remove
//    public static void AddRankScore(byte playerId, int score, string reason)
//    {
//        if (!CanUseRank()) return;
//        var player = GamePlayer.GetPlayer(playerId);
//        if (player == null) return;
//        if (IsCloseRank(player)) return;
//        RpcAddScore.Invoke((playerId, score, reason));
//    }

//    public static void RemoveRankScore(byte playerId, int score, string reason) => AddRankScore(playerId, -Math.Abs(score), reason);

//    private static bool IsCloseRank(GamePlayer player) => player.Role is IRankSystem rank && rank.CloseRank;

//    private static bool CanUseRank() => NebulaAPI.CurrentGame?.GetModule<IGameModeStandard>() != null;
//    #endregion

//    #region RPC
//    public static RemoteProcess<(byte id, int score, string reason)> RpcAddScore = new(
//        "RankAddScore",
//        (msg, _) =>
//        {
//            var player = GamePlayer.GetPlayer(msg.id);
//            if (player == null) return;
//            string code = PatchManager.GetFriendCode(player.VanillaPlayer);
//            AddLocalScore(code, msg.score, msg.reason);
//        });

//    private static void AddLocalScore(string friendCode, int score, string reason)
//    {
//        var data = GetData(friendCode);
//        if (data == null)
//        {
//            data = new RankSaveData() { FriendCode = friendCode };
//            Datas[friendCode] = data;
//        }
//        data.Score += score;
//        if (data.Score < 0) data.Score = 0;
//        data.Records.Add(new RankRecord() { Time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), Score = score, Reason = reason });
//        long limit = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeSeconds();
//        data.Records = data.Records.Where(r => r.Time >= limit).ToList();
//        Save();
//    }
//    #endregion

//    #region Data
//    private static RankSaveData? GetData(string friendCode)
//    {
//        if (Datas.TryGetValue(friendCode, out var data))
//        {
//            if (CheckHash(data)) return data;
//            data.Score = 0;
//            data.Records.Clear();
//            Save();
//            return data;
//        }
//        return null;
//    }

//    public static void Load()
//    {
//        if (!Directory.Exists(SaveDirectory)) Directory.CreateDirectory(SaveDirectory);
//        if (!File.Exists(SavePath))
//        {
//            Datas = new();
//            Save();
//            return;
//        }
//        try
//        {
//            string json = File.ReadAllText(SavePath);
//            Datas = JsonStructure.Deserialize<Dictionary<string, RankSaveData>>(json);
//        }
//        catch { Datas = new(); }
//    }

//    public static void Save()
//    {
//        foreach (var data in Datas.Values) data.Hash = CreateHash(data);
//        string json = JsonStructure.Serialize(Datas);
//        File.WriteAllText(SavePath, json);
//    }

//    private static string CreateHash(RankSaveData data)
//    {
//        string raw = data.FriendCode + data.Score + HashKey;
//        using SHA256 sha = SHA256.Create();
//        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw)));
//    }

//    private static bool CheckHash(RankSaveData data) => data.Hash == CreateHash(data);
//    #endregion
//}