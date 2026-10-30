using UnityEngine;

//ステージのクリア状況と、いちばんいい記録を PlayerPrefs に保存する
//ステージ番号は 0 始まり。ステージ 0 はいつでも遊べて、ほかは前のステージのクリアで解放される
public static class StageProgress
{
    const string ClearKey = "StageClear_";
    const string SeenKey = "StageUnlockSeen_";
    const string TimeKey = "StageBestTime_";
    const string RankKey = "StageBestRank_";

    public static bool IsCleared(int stage) => PlayerPrefs.GetInt(ClearKey + stage, 0) == 1;

    public static bool IsUnlocked(int stage) => stage <= 0 || IsCleared(stage - 1);

    /// <summary>クリアを記録する</summary>
    public static void MarkCleared(int stage)
    {
        if (stage < 0 || IsCleared(stage)) return;
        PlayerPrefs.SetInt(ClearKey + stage, 1);
        PlayerPrefs.Save();
    }

    /// <summary>解放されたのに、解放の演出をまだ見ていない</summary>
    public static bool IsNewlyUnlocked(int stage)
        => stage > 0 && IsUnlocked(stage) && PlayerPrefs.GetInt(SeenKey + stage, 0) == 0;

    public static void MarkUnlockSeen(int stage)
    {
        PlayerPrefs.SetInt(SeenKey + stage, 1);
        PlayerPrefs.Save();
    }

    /// <summary>クリアの記録を残す。速いタイムと、いいランクを、それぞれ前より良ければ上書きする。ランクは 0 が S</summary>
    public static void SaveBest(int stage, float time, int rank)
    {
        if (stage < 0) return;
        if (!PlayerPrefs.HasKey(TimeKey + stage) || time < PlayerPrefs.GetFloat(TimeKey + stage)) PlayerPrefs.SetFloat(TimeKey + stage, time);
        if (!PlayerPrefs.HasKey(RankKey + stage) || rank < PlayerPrefs.GetInt(RankKey + stage)) PlayerPrefs.SetInt(RankKey + stage, rank);
        PlayerPrefs.Save();
    }

    /// <summary>いちばんいい記録。まだなければ false</summary>
    public static bool TryGetBest(int stage, out float time, out int rank)
    {
        time = PlayerPrefs.GetFloat(TimeKey + stage, 0f);
        rank = PlayerPrefs.GetInt(RankKey + stage, -1);
        return PlayerPrefs.HasKey(TimeKey + stage) && rank >= 0;
    }

    public static int ClearedCount(int total)
    {
        int n = 0;
        for (int i = 0; i < total; i++) if (IsCleared(i)) n++;
        return n;
    }

    /// <summary>クリアと記録をすべて消す。タイトルのリセットからも使う</summary>
    public static void ResetAll(int total)
    {
        for (int i = 0; i < total; i++)
        {
            PlayerPrefs.DeleteKey(ClearKey + i);
            PlayerPrefs.DeleteKey(SeenKey + i);
            PlayerPrefs.DeleteKey(TimeKey + i);
            PlayerPrefs.DeleteKey(RankKey + i);
        }
        PlayerPrefs.Save();
    }
}
