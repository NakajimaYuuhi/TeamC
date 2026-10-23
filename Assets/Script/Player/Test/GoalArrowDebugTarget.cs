/////////////////////////////////////////
/// GoalArrowDebugTarget.cs
/// 2026/10/23 作者：佐藤蒼也
/// 
/// デバッグ用、めんどいから全AI
/// 
/// ログ
/// 2026/10/23 佐藤蒼也：矢印の確認用に、目的地の座標を設定・切り替えできるスクリプトを作成
/// 2026/10/23 佐藤蒼也：目的地にオブジェクトを生成する処理を追加→UpdateGoalMarker関数
/// 
/////////////////////////////////////////

using UnityEngine;
using UnityEngine.InputSystem;

//【確認用】プレイヤーに付けて使う
//Inspectorで設定した目的地の座標へ向けて、矢印に方向を渡す
//目的地にはオブジェクトを生成して、位置がわかるようにする
public class GoalArrowDebugTarget : MonoBehaviour
{
    [Header("目的地の座標（再生中に書き換えてもOK）")]
    [SerializeField, Tooltip("目的地の候補。Gキーで順番に切り替わる")]
    private Vector3[] goalPositions = new Vector3[]
    {
        new Vector3(10f, 0f, 10f),
        new Vector3(-10f, 0f, 5f),
        new Vector3(0f, 0f, -12f),
    };

    [SerializeField, Tooltip("今の目的地の番号（0から）")]
    private int currentIndex = 0;

    [Header("設定")]
    [SerializeField, Tooltip("目的地を切り替えるキー")]
    private Key switchKey = Key.G;
    [SerializeField, Tooltip("目的地にこれより近づいたら矢印を消す")]
    private float hideDistance = 1.5f;

    [Header("目的地に置くオブジェクト")]
    [SerializeField, Tooltip("目的地に生成するプレハブ。空のときは赤い円盤を自動で作る")]
    private GameObject goalMarkerPrefab;
    [SerializeField, Tooltip("目的地の座標から、オブジェクトを置く位置までのずれ")]
    private Vector3 markerOffset = Vector3.zero;

    private GoalArrow goalArrow;  //方向を渡す矢印
    private GameObject goalMarker; //目的地に生成したオブジェクト

    void Start()
    {
        //シーン内のGoalArrowを探す
        goalArrow = FindFirstObjectByType<GoalArrow>();
        if (goalArrow == null)
        {
            Debug.LogError("GoalArrowがシーンにない");
        }

        //目的地のオブジェクトを生成する
        CreateGoalMarker();
    }

    void Update()
    {
        if (goalPositions == null || goalPositions.Length == 0)
        {
            //目的地がなければ矢印とオブジェクトを消す
            if (goalArrow != null) goalArrow.ClearDirection();
            if (goalMarker != null) goalMarker.SetActive(false);
            return;
        }

        //キーで次の目的地に切り替える（最後まで行ったら最初に戻る）
        if (Keyboard.current != null && Keyboard.current[switchKey].wasPressedThisFrame)
        {
            currentIndex = (currentIndex + 1) % goalPositions.Length;
            Debug.Log("目的地を切り替え：" + currentIndex + " " + goalPositions[currentIndex]);
        }

        //Inspectorで範囲外の番号を入れられても大丈夫なようにする
        currentIndex = Mathf.Clamp(currentIndex, 0, goalPositions.Length - 1);

        //目的地のオブジェクトを今の目的地へ動かす
        UpdateGoalMarker();

        if (goalArrow == null) return;

        //目的地の方向 = 目的地の位置 - プレイヤーの位置
        Vector3 dir = goalPositions[currentIndex] - transform.position;
        dir.y = 0f;

        //近づいたら矢印を消す
        if (dir.magnitude < hideDistance)
        {
            goalArrow.ClearDirection();
        }
        else
        {
            goalArrow.SetDirection(dir);
        }
    }

    //目的地のオブジェクトを生成する
    private void CreateGoalMarker()
    {
        if (goalMarkerPrefab != null)
        {
            //プレハブが設定されていればそれを生成する
            goalMarker = Instantiate(goalMarkerPrefab);
        }
        else
        {
            //プレハブがなければ、赤い円盤を作る
            goalMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            goalMarker.transform.localScale = new Vector3(1.5f, 0.02f, 1.5f); //平たくして地面に置く円盤にする
            goalMarker.GetComponent<Renderer>().material.color = Color.red;

            //プレイヤーやNPCがぶつからないように当たり判定を消す
            Destroy(goalMarker.GetComponent<Collider>());
        }

        goalMarker.name = "DebugGoalMarker";
    }

    //目的地のオブジェクトを今の目的地の位置に置く
    //（再生中に座標を書き換えたときもついてくるよう毎フレーム更新する）
    private void UpdateGoalMarker()
    {
        if (goalMarker == null) return;

        goalMarker.SetActive(true);
        goalMarker.transform.position = goalPositions[currentIndex] + markerOffset;
    }

    //このスクリプトが消えるときに、生成したオブジェクトも消す
    void OnDestroy()
    {
        if (goalMarker != null)
        {
            Destroy(goalMarker);
        }
    }

    //Sceneビューで目的地の位置がわかるように印を描く（今の目的地は赤、それ以外は灰色）
    void OnDrawGizmos()
    {
        if (goalPositions == null) return;

        for (int i = 0; i < goalPositions.Length; i++)
        {
            Gizmos.color = (i == currentIndex) ? Color.red : Color.gray;
            Gizmos.DrawWireSphere(goalPositions[i], 0.5f);
            Gizmos.DrawLine(goalPositions[i], goalPositions[i] + Vector3.up * 3f);
        }
    }
}