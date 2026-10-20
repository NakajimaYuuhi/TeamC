using UnityEngine;

// ----- 基底クラス -----
public abstract class NPCState : StateBase<NPC_Controller>
{
    public abstract bool AllowsSubUpdate { get; }

}

public class NPCStanding : NPCState
{
    public override bool AllowsSubUpdate => true;


    public override void OnEnter()
    {
    }

    public override void OnUpdate()
    {
        owner.ReturnToDefaultPosition();
    }

    public override void OnExit()
    {
    }
}

public class NPCBrowsing : NPCState
{
    public override bool AllowsSubUpdate => true;

    public override void OnEnter()
    {
    }

    public override void OnUpdate()
    {
        if (owner == null) return;

        // 1. 【判断】現在の巡回ポイントの座標を取得
        Vector3 targetPos = owner.GetCurrentPatrolPosition();

        // 2. 【判断】到着したかどうかの監視
        float distance = Vector3.Distance(targetPos, owner.transform.position);
        if (distance < owner.ArrivalDistance)
        {
            Debug.Log($"NPC has arrived at target position: {targetPos}");
            owner.AdvanceToNextTarget(); // 到着したら次のポイントへ
            return;
        }

        // 3. 【命令】コントローラーに「ここへ動け」と指示を出す
        owner.MoveTowardsPatrolPoint(targetPos);
    }

    public override void OnExit()
    {
    }

}