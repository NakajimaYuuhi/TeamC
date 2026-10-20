using UnityEngine;

// ----- 汎用クラス -----
public abstract class StateBase<T>
{
    // 所有者（プレイヤーや敵のコンポーネント）への参照を保持
    protected T owner;

    // 最初に対象を登録する
    public void Setup(T ownerInstance)
    {
        owner = ownerInstance;
    }

    public abstract void OnEnter();
    public abstract void OnUpdate();
    public abstract void OnExit();
}

// ----- 汎用ステートマシン -----
public class StateMachine<T>
{
    // 現在のステート（T型に対応したステート）
    private StateBase<T> currentState;

    private readonly T owner;

    // 現在のStateを外部から参照できるようにする
    public StateBase<T> CurrentState => currentState;

    public StateMachine(T owner, StateBase<T> initialState)
    {
        this.owner = owner;

        //Debug.Log(this.owner == null);
        // インスタンス化された瞬間に、最初のステートを起動させる
        ChangeState(initialState);
    }

    // 状態切り替え
    public void ChangeState(StateBase<T> newState)
    {
        if (currentState != null) currentState.OnExit();

        currentState = newState;
        currentState.Setup(owner);

        //Debug.Log(currentState == null);
        if (currentState != null) currentState.OnEnter();
    }

    // 毎フレームの更新
    public void OnUpdate()
    {
        if (currentState != null) currentState.OnUpdate();
    }
}