using UnityEngine;

public class NPC_AnimationManager : MonoBehaviour
{
    public enum AnimationState
    {
        Idle,       // 立ち
        Walk,       // 歩き
        Reaction,   // リアクション
        Died,       // 死
        None,       // なし
    }
    [Header("現在の再生しているアニメーション"),SerializeField,EnumButtons]
    public AnimationState curAnimState = AnimationState.None;
    // アニメーション時間を管理する変数
    private float animFrame = 0.0f;



    [Header("歩きのパラメタ")]
    [SerializeField,Min(0.0f)] private float walkAmplitude = 0.15f; // 上下の移動量
    [SerializeField,Min(0.0f)] private float walkFrequency = 2.0f;// 秒間

    [Header("リアクションのパラメタ")]
    [SerializeField] private float reactionDuration = 0.5f;
    [SerializeField] private float reactionXAngle = 15.0f;
    [SerializeField] private float reactionZAngle = 20.0f;
    private float reactionTime = 0.0f;

    // アニメーション開始時のローカル座標
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;

    // Awake is called once when the script instance is being loaded
    void Awake()
    {
        curAnimState = AnimationState.Walk;
    }

    // Update is called once per frame
    void Update()
    {
        animFrame += Time.deltaTime;
        switch (curAnimState)
        {
            case AnimationState.Idle:
                IdleAnim();
                break;
            case AnimationState.Walk:
                WalkAnim();
                break;
            case AnimationState.Reaction:
                ReactionAnim();
                break;
            case AnimationState.Died:
                // Handle Died animation logic here
                break;
            case AnimationState.None:
                // Handle None state logic here
                break;
        }
    }
    public void SetAnimation(AnimationState nextState)
    {
        // Reactionの再発火は許可する
        if (curAnimState == nextState &&
            nextState != AnimationState.Reaction)
        {
            return;
        }

        curAnimState = nextState;
        animFrame = 0.0f;
    }

    private void IdleAnim()
    {
        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;
    }

    public void WalkAnim()
    {

        // Sin波による上下移動
        float offsetY = Mathf.Sin(
            animFrame * walkFrequency * Mathf.PI * 2.0f
        ) * walkAmplitude + 0.5f;

        transform.localPosition =
            initialLocalPosition + Vector3.up * offsetY;

        transform.localRotation = initialLocalRotation;
    }

    public void ReactionAnim()
    {
        float t = Mathf.Clamp01(
            animFrame / Mathf.Max(reactionDuration, 0.0001f)
        );

        float wave = Mathf.Sin(t * Mathf.PI * 2.0f);
        float damping = 1.0f - t;

        // X軸とZ軸の回転
        float angleX = wave * damping * reactionXAngle;
        float angleZ = wave * damping * reactionZAngle;

        transform.localPosition = initialLocalPosition;

        transform.localRotation =
            Quaternion.Euler(angleX, 0.0f, angleZ);

        if (t >= 1.0f)
        {
            SetAnimation(AnimationState.Walk);
        }
    }

    public void PlayIdle()
    {
        SetAnimation(AnimationState.Idle);
    }
    public void PlayWalk()
    {
        SetAnimation(AnimationState.Walk);
    }
    public void PlayReaction()
    {
        SetAnimation(AnimationState.Reaction);
    }
}
