using UnityEngine;

// ----- 基底クラス -----
// 各状態は「NPC_Controller のどの更新処理を呼ぶか」だけを決める。
// 判断(到着・次の目的地)と移動の指示は NPC_Controller 側にまとめてある。
public abstract class NPCState : StateBase<NPC_Controller>
{
    public abstract bool AllowsSubUpdate { get; }
}

// 立ち止まり:初期位置に戻って、初期の向きで踏ん張る
public class NPCStanding : NPCState
{
    public override bool AllowsSubUpdate => true;

    public override void OnEnter() { }

    public override void OnUpdate()
    {
        if (owner == null) return;
        owner.UpdateStanding();
    }

    public override void OnExit() { }
}

// 徘徊:巡回地点を点から点へ回る
public class NPCBrowsing : NPCState
{
    public override bool AllowsSubUpdate => true;

    public override void OnEnter() { }

    public override void OnUpdate()
    {
        if (owner == null) return;
        owner.UpdatePatrol();
    }

    public override void OnExit() { }
}

// ノードベースの徘徊:エッジに沿って、中心からのズレを保ったまま平行に進む
public class NPCBrowsing_BasedOnNode : NPCState
{
    public override bool AllowsSubUpdate => true;

    public override void OnEnter() { }

    public override void OnUpdate()
    {
        if (owner == null) return;
        owner.UpdateNodeTravel();
    }

    public override void OnExit() { }
}
