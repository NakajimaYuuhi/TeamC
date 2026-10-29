using UnityEngine;
using Effekseer;

/// <summary>
/// オブジェクトごとにエフェクトの大きさ・位置・向きを決めて再生するコンポーネント。
/// 付けると子オブジェクト "[FX] ..." に EffekseerEmitter が自動で作られ、
/// ここでの設定がそのエミッターに反映される(エミッター側は直接いじらない)。
/// </summary>
public class ObjectEffect : MonoBehaviour
{
    [Header("エフェクト")]
    [SerializeField] EffekseerEffectAsset effect;
    [SerializeField] bool playOnStart;
    [SerializeField] bool loop;

    [Header("大きさ・位置・向き(このオブジェクト基準)")]
    [SerializeField, Min(0f)] float scale = 1f;
    [SerializeField] Vector3 offset;
    [SerializeField] Vector3 rotation;
    [Tooltip("オンにすると、このオブジェクトのScaleもエフェクトの大きさに掛け合わされる")]
    [SerializeField] bool matchObjectScale;

    [Header("再生のしかた")]
    [Tooltip("オン: 再生中もオブジェクトについて行く(オブジェクトを消すとエフェクトも消える)\n" +
             "オフ: 再生した場所に残る(すぐ Destroy してもエフェクトは最後まで再生される)")]
    [SerializeField] bool followObject = true;
    [SerializeField] float speed = 1f;
    [SerializeField] Color color = Color.white;

    [SerializeField, HideInInspector] EffekseerEmitter emitter;

    public EffekseerEmitter Emitter => emitter;

    /// <summary>エフェクトを再生する</summary>
    public EffekseerHandle Play()
    {
        EnsureEmitter();
        Apply();
        if (effect == null) return default;

        if (followObject)
        {
            var h = emitter.Play();
            h.SetAllColor(color);
            return h;
        }

        // オブジェクトから切り離して、その場で再生する
        var t = emitter.transform;
        var handle = EffekseerSystem.PlayEffect(effect, t.position);
        handle.SetRotation(t.rotation);
        handle.SetScale(matchObjectScale ? t.lossyScale : t.localScale);
        handle.speed = speed;
        handle.SetAllColor(color);
        return handle;
    }

    /// <summary>このオブジェクトのエミッターで再生中のエフェクトを止める</summary>
    public void Stop()
    {
        if (emitter != null) emitter.Stop();
    }

    void Awake()
    {
        EnsureEmitter();
        Apply();
    }

    void Start()
    {
        if (playOnStart) Play();
    }

    // エミッター付きの子オブジェクトがなければ作る
    void EnsureEmitter()
    {
        if (emitter != null) return;
        var child = new GameObject("[FX] " + (effect != null ? effect.name : "Effect"));
        child.transform.SetParent(transform, false);
        emitter = child.AddComponent<EffekseerEmitter>();
    }

    // ObjectEffect の設定をエミッターと子オブジェクトに反映する
    void Apply()
    {
        if (emitter == null) return;
        var t = emitter.transform;
        t.localPosition = offset;
        t.localRotation = Quaternion.Euler(rotation);
        t.localScale = Vector3.one * scale;

        emitter.effectAsset = effect;
        emitter.playOnStart = false;      // 再生は ObjectEffect が管理する
        emitter.isLooping = loop;
        emitter.EmitterScale = matchObjectScale ? EffekseerEmitterScale.Global : EffekseerEmitterScale.Local;
        emitter.speed = speed;
        if (effect != null) emitter.gameObject.name = "[FX] " + effect.name;
    }
    public void SetScale(float newScale)
    {
        scale = newScale;
    }

#if UNITY_EDITOR
    // コンポーネントを付けた時点でエミッターを作る
    void Reset()
    {
        EnsureEmitter();
        Apply();
    }



    // Inspector で値を変えたらすぐ反映する(OnValidate 中はオブジェクトを作れないので遅らせる)
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)) return;
            EnsureEmitter();
            Apply();
            // 子とエミッターの変更をシーンに保存されるよう記録する
            UnityEditor.EditorUtility.SetDirty(emitter);
            UnityEditor.EditorUtility.SetDirty(emitter.transform);
            UnityEditor.EditorUtility.SetDirty(emitter.gameObject);
        };
    }

    void OnDrawGizmosSelected()
    {
        var pos = transform.TransformPoint(offset);
        float s = scale * (matchObjectScale ? transform.lossyScale.x : 1f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(pos, 0.5f * s);
    }
#endif
}
