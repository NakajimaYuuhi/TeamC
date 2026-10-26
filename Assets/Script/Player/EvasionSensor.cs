/////////////////////////////////////////
/// EvasionSensor.cs
/// 2026/10/23 作者：佐藤蒼也
/// 
/// ログ
/// 2026/10/23 佐藤蒼也：回避判定エリアにいるNPCを調べる処理を実装
/// 
/////////////////////////////////////////

using System.Collections.Generic;
using UnityEngine;

//エリアの中にいるNPCを記録し、NPCが入ってきたらイベントで知らせる
public class EvasionSensor : MonoBehaviour
{
    //NPCがエリアに入ってきたときに呼ばれるイベント
    public event System.Action<GameObject> NPCEntered;

    //エリア内にいるNPCと、そのNPCのコライダーがいくつ入っているか
    private readonly Dictionary<GameObject, int> npcsInArea = new Dictionary<GameObject, int>();

    //エリア内にNPCがいるか
    public bool HasNPCNearby
    {
        get
        {
            RemoveMissingNPCs();
            return npcsInArea.Count > 0;
        }
    }

    //コンポーネントを付けたときに、自動でIs Triggerにする
    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        //NPCの視界などのTriggerは無視する（体のコライダーだけを見る）
        if (other.isTrigger) return;

        GameObject npc = FindNPCRoot(other.transform);
        if (npc == null) return;

        if (npcsInArea.TryGetValue(npc, out int count))
        {
            //すでにエリアにいるNPCの、別のコライダーが入ってきた
            npcsInArea[npc] = count + 1;
        }
        else
        {
            //新しくエリアに入ってきたNPC
            npcsInArea[npc] = 1;
            NPCEntered?.Invoke(npc);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.isTrigger) return;

        GameObject npc = FindNPCRoot(other.transform);
        if (npc == null) return;

        if (npcsInArea.TryGetValue(npc, out int count))
        {
            //そのNPCのコライダーが全部出ていったら、エリアからいなくなったとする
            if (count <= 1)
            {
                npcsInArea.Remove(npc);
            }
            else
            {
                npcsInArea[npc] = count - 1;
            }
        }
    }

    //エリア内で消えたり無効になったりしたNPCを取り除く
    //（消えたときはOnTriggerExitが呼ばれないため）
    private void RemoveMissingNPCs()
    {
        List<GameObject> missing = null;

        foreach (GameObject npc in npcsInArea.Keys)
        {
            if (npc == null || !npc.activeInHierarchy)
            {
                if (missing == null) missing = new List<GameObject>();
                missing.Add(npc);
            }
        }

        if (missing == null) return;

        foreach (GameObject npc in missing)
        {
            npcsInArea.Remove(npc);
        }
    }

    //当たったオブジェクトから親へたどり、NPCタグの付いたオブジェクトを返す
    //（ほかのスクリプトからも使えるようにstaticにしている）
    public static GameObject FindNPCRoot(Transform hitTransform)
    {
        Transform current = hitTransform;

        while (current != null)
        {
            if (current.CompareTag("NPC"))
            {
                return current.gameObject;
            }
            current = current.parent; //1つ上の親へ
        }

        return null;
    }

    //エリア内で、指定した位置から一番近いNPCの位置を返す（いなければfalse）
    public bool TryGetNearestNPC(Vector3 from, out Vector3 npcPosition)
    {
        RemoveMissingNPCs();

        npcPosition = Vector3.zero;
        float nearestSqrDistance = float.MaxValue;
        bool found = false;

        foreach (GameObject npc in npcsInArea.Keys)
        {
            Vector3 position = npc.transform.position;
            float sqrDistance = (position - from).sqrMagnitude;

            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                npcPosition = position;
                found = true;
            }
        }

        return found;
    }
}