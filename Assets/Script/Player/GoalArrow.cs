/////////////////////////////////////////
/// GoalArrow.cs
/// 2026/10/22 作者：佐藤蒼也
/// 
/// ログ
/// 2026/10/22 佐藤蒼也：目的地を示す矢印の実装
/// 
/////////////////////////////////////////

using UnityEngine;

//プレイヤーの足元に、目的地の方向を指す矢印を表示する
//方向は外部から【SetDirection】で受け取る
public class GoalArrow : MonoBehaviour
{
    [Header("表示位置")]
    [SerializeField, Tooltip("プレイヤーの中心から矢印までの距離")]
    private float distanceFromPlayer = 1.15f;
    [SerializeField, Tooltip("地面からの高さ")]
    private float heightFromGround = 0.04f;
    [SerializeField, Tooltip("プレイヤーの位置から地面までの高さ")]
    private float playerFootOffset = 0f;

    [Header("点滅")]
    [SerializeField, Tooltip("点滅の速さ")]
    private float blinkSpeed = 6f;
    [SerializeField, Tooltip("一番薄いときの不透明度"), Range(0f, 1f)]
    private float minAlpha = 0.55f;
    [SerializeField, Tooltip("一番濃いときの不透明度"), Range(0f, 1f)]
    private float maxAlpha = 0.9f;

    private Transform player;        //追従するプレイヤー
    private Renderer arrowRenderer;  //矢印の描画
    private Material arrowMaterial;  //点滅させるマテリアル
    private Color baseColor;         //元の色

    private Vector3 _goalPosition;
    private Vector3 direction;       //目的地の方向
    private bool hasDirection;       //方向を受け取っているか

    void Awake()
    {
        //親のスケールをいじったときに影響を受けないように
        //違和感あるなら消して影響うけるようにしよ
        // transform.SetParent(null, true);

        arrowRenderer = GetComponent<Renderer>();
        arrowMaterial = arrowRenderer.material; //専用のマテリアルを作って取得
        baseColor = arrowMaterial.color;

        arrowRenderer.enabled = false; //方向を受け取るまでは表示しない
    }

    void Start()
    {
        //Playerタグのついたオブジェクトを追従対象にする
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
        else
        {
            Debug.LogError("Playerタグのついたオブジェクトがない");
        }
    }

    void LateUpdate()
    {
        if (player == null) return;

        UpdateGoalArrow();

        //方向がないときは表示しない
        arrowRenderer.enabled = hasDirection;
        if (!hasDirection) return;

        //プレイヤーの足元から、目的地の方向へ少し離れた位置に置く
        //                          new Vector3(0,1,0)
        Vector3 foot = player.position + Vector3.up * playerFootOffset;
        //矢印のポジション = プレイヤーの足元からの距離 + 目的地の座標 * プレイヤーからの距離(矢印の) + 地面からの高さ
        transform.position = foot + direction * distanceFromPlayer + Vector3.up * heightFromGround;

        //テクスチャの上側が目的地の方向を向くように回転させる[かつ]目的地にむかせる
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);

        //不透明度を minAlpha ～ maxAlpha の間で点滅させる
        float t = (Mathf.Sin(Time.time * blinkSpeed) + 1f) * 0.5f;
        Color c = baseColor;
        c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
        arrowMaterial.color = c;
    }

    private void UpdateGoalArrow()
    {
        Vector3 calcDirection = _goalPosition - transform.position;

        //近いなら非表示にする　sqrMagnitude:x^2 + y^2 + z^2
        if (calcDirection.sqrMagnitude < 0.0001f)
        {
            hasDirection = false;
            return;
        }

        direction = calcDirection.normalized;
        hasDirection = true;
    }

    //----------------------------------------------------------------------------------------
    //外部から呼ぶ関数

    //目的地の方向を受け取る
    public void SetDirection(Vector3 newDirection)
    {
        //yを0に
        newDirection.y = 0f;

        _goalPosition = newDirection;
    }

    //矢印を消す
    public void ClearDirection()
    {
        hasDirection = false;
    }

    //マテリアル削除
    void OnDestroy()
    {
        if (arrowMaterial != null)
        {
            Destroy(arrowMaterial);
        }
    }
}