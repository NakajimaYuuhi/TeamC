using UnityEngine;

//ステージのクリア状況を PlayerPrefs に保存する
//ステージ番号は 0 始まり。ステージ 0 はいつでも遊べて、ほかは前のステージのクリアで解放される
public static class StageProgress
{
    const string ClearKey = "StageClear_";
    const string SeenKey = "StageUnlockSeen_";

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

    public static int ClearedCount(int total)
    {
        int n = 0;
        for (int i = 0; i < total; i++) if (IsCleared(i)) n++;
        return n;
    }

    /// <summary>クリアの記録をすべて消す。テスト用</summary>
    public static void ResetAll(int total)
    {
        for (int i = 0; i < total; i++)
        {
            PlayerPrefs.DeleteKey(ClearKey + i);
            PlayerPrefs.DeleteKey(SeenKey + i);
        }
        PlayerPrefs.Save();
    }
}
