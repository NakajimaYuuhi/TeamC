using System.Collections.Generic;
using UnityEngine;

//ステージごとのランクの境目となるクリアタイム
//ランクの番号は 0=S 1=A 2=B 3=C
[CreateAssetMenu(fileName = "SO_ResultRank", menuName = "Scriptable Objects/ResultRank")]
public class ResultRankTable : ScriptableObject
{
    [System.Serializable]
    public class Limits
    {
        [Tooltip("この秒数以内なら S")]
        public float s = 40f;
        [Tooltip("この秒数以内なら A")]
        public float a = 60f;
        [Tooltip("この秒数以内なら B。これより遅いと C")]
        public float b = 90f;
    }

    [SerializeField, Tooltip("ステージ番号順。足りないステージは最後の境目を使う")]
    List<Limits> stages = new List<Limits> { new Limits() };

    public Limits Get(int stage)
    {
        if (stages == null || stages.Count == 0) return new Limits();
        return stages[Mathf.Clamp(stage, 0, stages.Count - 1)];
    }

    /// <summary>クリアタイムからランクを返す</summary>
    public int GetRank(int stage, float time)
    {
        var l = Get(stage);
        if (time <= l.s) return 0;
        if (time <= l.a) return 1;
        if (time <= l.b) return 2;
        return 3;
    }

    /// <summary>そのランクになれる秒数を返す。C は使わない</summary>
    public float GetLimit(int stage, int rank)
    {
        var l = Get(stage);
        return rank <= 0 ? l.s : rank == 1 ? l.a : l.b;
    }
}
