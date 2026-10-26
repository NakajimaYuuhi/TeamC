/////////////////////////////////////////
/// PlayerItemController.cs
/// 2026/10/26 作者：佐藤蒼也
///
/// ログ
/// 2026/10/26 佐藤蒼也：プレイヤーがアイテムを取得して投げる処理を実装→
///
/////////////////////////////////////////


using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

//アイテム側で投擲・拾得のタイミングを受け取りたい場合に実装する
public interface IThrowableItem
{
    void OnPickedUp(GameObject picker);
    void OnThrown(GameObject thrower, Vector3 velocity);
}

//操作方法
//通常モード:アイテムを持っていたら左クリック → アイテムモード
//アイテムモード:WASDで落下地点を選択 / 左クリックでアイテム投擲 / 右クリックでキャンセル

//プレイヤーのアイテム所持・照準・投擲を管理
//Itemタグの付いたオブジェクトに接触すると自動で拾う
//投擲時は拾ったオブジェクトそのものを放物線で飛ばす

//全体の流れ
//1.拾得  :Itemタグに接触 → TryPickup → 非表示にしてitemsに追加
//2.開始  :通常モードのUpdate → 左クリック → EnterAimMode
//3.照準  :アイテムモードのUpdate → 毎フレームUpdateAim
//入力で落下地点を移動 → CalcLaunchVelocityで初速計算 → BuildTrajectoryで軌道計算 → 線とマーカーを表示
//4.投擲  :左クリック → Throw → アイテムを再表示して初速を与える → 通常モードへ
//5.キャンセル:右クリック → ExitAimMode → 通常モードへ
public class PlayerItemController : MonoBehaviour
{
    //通常orアイテム投擲
    public enum Mode { Normal, Aiming }

    [Header("拾得")]
    [Tooltip("拾う対象にするタグ")]
    [SerializeField] string itemTag = "Item";
    [Tooltip("持てるアイテム数")]
    [SerializeField, Min(1)] int capacity = 3;
    [Tooltip("投げたアイテムを再び拾えるようになるまでの秒数")]
    [SerializeField] float repickDelay = 1f;

    [Header("照準")]
    [Tooltip("WASDで落下地点を動かす速さ")]
    [SerializeField] float cursorSpeed = 8f;
    [Tooltip("アイテムモード開始時の落下地点の距離")]
    [SerializeField] float defaultAimDistance = 4f;
    [Tooltip("プレイヤーからの最大投擲距離")]
    [SerializeField, Min(0.5f)] float maxRange = 10f;
    [Tooltip("カメラ")]
    [SerializeField] Transform cameraTransform;
    [Tooltip("アイテムモード中、プレイヤーを落下地点の方へ向けるか")]
    [SerializeField] bool faceTarget = true;
    [Tooltip("アイテムモード中に無効化するコンポーネント。移動スクリプト無効にしてWASD移動を無効にする予定")]
    [SerializeField] Behaviour[] disableWhileAiming;

    [Header("投擲")]
    [Tooltip("投げ始める位置")]
    [SerializeField] Transform throwOrigin;
    [Tooltip("始点・終点の高い方から、さらに何m上を放物線の頂点にするか")]
    [SerializeField] float arcHeight = 1.5f;
    [Tooltip("距離1mあたりに追加する頂点の高さ")]
    [SerializeField] float arcHeightPerMeter = 0.15f;
    [Tooltip("投擲時に回転を加える強さ")]
    [SerializeField] float throwSpin = 5f;

    [Header("予測線")]
    [Tooltip("予測線を描くLineRenderer")]
    [SerializeField] LineRenderer trajectoryLine;
    [Tooltip("予測線の分割数。多いほど滑らか")]
    [SerializeField, Range(8, 128)] int lineResolution = 40;
    [Tooltip("落下地点に表示するマーカー")]
    [SerializeField] Transform landingMarker;
    [Tooltip("地面・障害物判定に使うレイヤー")]
    [SerializeField] LayerMask collisionMask = ~0;
    [Tooltip("落下地点を地面に合わせる際、プレイヤーの何m上からレイを落とすか")]
    [SerializeField] float groundRayHeight = 5f;

    [Header("入力")]
    [Tooltip("未設定なら親から自動で探す")]
    [SerializeField] PlayerInput playerInput;
    [Tooltip("アイテムモード開始・投擲")]
    [SerializeField] string useItemActionName = "UseItem";
    [Tooltip("アイテムモードのキャンセル")]
    [SerializeField] string cancelItemActionName = "CancelItem";
    [Tooltip("落下地点の移動")]
    [SerializeField] string aimMoveActionName = "Move";

    public Mode CurrentMode { get; private set; } = Mode.Normal;                 //現在のモード
    public IReadOnlyList<GameObject> Items => items;                             //所持アイテム一覧 外部からは読み取りのみ
    public GameObject CurrentItem => items.Count > 0 ? items[0] : null;         //次に投げるアイテム リストの先頭

    //モード切替時
    public event Action<Mode> ModeChanged;
    //所持アイテムが増減したとき
    public event Action InventoryChanged;

    readonly List<GameObject> items = new List<GameObject>();                                    //所持アイテム
    readonly Dictionary<GameObject, float> repickBlockedUntil = new Dictionary<GameObject, float>(); //投げたアイテムが再び拾えるようになる時刻
    readonly List<Vector3> linePoints = new List<Vector3>();                                     //予測線の頂点
    readonly RaycastHit[] hitBuffer = new RaycastHit[16];                                        //レイキャスト結果の使い回し用
    Collider[] ownColliders;      //プレイヤー自身のコライダー
    Vector3 aimOffset;            //プレイヤーから落下地点への水平オフセット
    Vector3 launchVelocity;       //現在の照準での初速
    InputAction useItemAction;    //アイテムモード開始・投擲
    InputAction cancelItemAction; //アイテムモードのキャンセル
    InputAction aimMoveAction;    //落下地点の移動

    //初期化 参照の準備
    void Awake()
    {
        //マーカーと予測線はプレイヤーから切り離し、コライダーも無効化する
        //子のままだとプレイヤーの当たり判定の一部になり、地面に押し上げられて浮くため
        DetachVisual(landingMarker);
        if (trajectoryLine) DetachVisual(trajectoryLine.transform);

        //自分のコライダー・投擲位置・カメラを取得
        ownColliders = GetComponentsInChildren<Collider>();
        if (!throwOrigin) throwOrigin = transform;
        if (!cameraTransform && Camera.main) cameraTransform = Camera.main.transform;

        //最初は予測線とマーカーを隠しておく
        SetAimVisuals(false);
    }

    //初期化 入力アクションの取得
    void Start()
    {
        //PlayerInput からアクションを名前で取得する
        if (!playerInput) playerInput = GetComponentInParent<PlayerInput>();
        if (!playerInput)
        {
            Debug.LogError("PlayerInputがない", this);
            return;
        }
        useItemAction = FindAction(useItemActionName);
        cancelItemAction = FindAction(cancelItemActionName);
        aimMoveAction = FindAction(aimMoveActionName);
    }

    //アクションを名前で探す
    InputAction FindAction(string actionName)
    {
        InputAction action = playerInput.actions.FindAction(actionName);
        if (action == null) Debug.LogError($"{actionName}アクションがない", this);
        return action;
    }

    //プレイヤーが消えるとき
    void OnDestroy()
    {
        //切り離したマーカー・予測線はプレイヤーと一緒に片付ける
        if (landingMarker) Destroy(landingMarker.gameObject);
        if (trajectoryLine) Destroy(trajectoryLine.gameObject);
    }

    //スクリプトが無効になったとき、アイテムモード中なら解除する
    void OnDisable()
    {
        if (CurrentMode == Mode.Aiming) ExitAimMode();
    }

    //───────── 拾得 ─────────
    //アイテムの Collider がトリガーでも通常コライダーでも拾えるよう 3 種類の接触を見る

    void OnTriggerEnter(Collider other) => TryPickup(other);
    void OnCollisionEnter(Collision collision) => TryPickup(collision.collider);
    void OnControllerColliderHit(ControllerColliderHit hit) => TryPickup(hit.collider); //CharacterController 用

    //接触したものがアイテムなら拾う
    void TryPickup(Collider col)
    {
        if (col == null) return;

        //1.接触したものがItemタグのオブジェクトか調べる
        //子コライダーに当たった場合も、タグの付いたルートを探す
        GameObject item = FindTaggedRoot(col.transform);
        if (item == null) return;

        //2.拾えるか確認する 所持済み・所持数上限・投げた直後なら拾わない
        if (items.Contains(item) || items.Count >= capacity) return;
        if (repickBlockedUntil.TryGetValue(item, out float until) && Time.time < until) return;

        //3.所持リストに追加
        repickBlockedUntil.Remove(item);
        items.Add(item);

        //4.アイテム側に拾われたことを通知して、非表示にする
        item.transform.SetParent(null);
        item.SendMessage("OnPickedUp", gameObject, SendMessageOptions.DontRequireReceiver);
        foreach (var t in item.GetComponents<IThrowableItem>()) t.OnPickedUp(gameObject);
        item.SetActive(false); //所持中は非表示

        //5.UIなどに所持数の変化を通知
        InventoryChanged?.Invoke();
    }

    //親をたどって、Itemタグの付いたオブジェクトを探す 無ければnullを返す
    GameObject FindTaggedRoot(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            if (t.IsChildOf(transform)) return null; //自分自身は除外
            if (t.CompareTag(itemTag)) return t.gameObject;
        }
        return null;
    }

    //───────── 更新 ─────────

    //毎フレーム:モードごとに入力を見て処理を振り分ける
    void Update()
    {
        //所持中のアイテムが外部で破棄されていたら取り除く
        if (items.RemoveAll(i => i == null) > 0)
        {
            InventoryChanged?.Invoke();
            if (CurrentMode == Mode.Aiming && CurrentItem == null) ExitAimMode();
        }

        switch (CurrentMode)
        {
            //通常モード:アイテムを持っていて左クリックされたらアイテムモードへ
            case Mode.Normal:
                if (CurrentItem != null && UseItemPressed())
                    EnterAimMode();
                break;

            //アイテムモード:右クリックでキャンセル / 照準を更新 / 左クリックで投擲
            case Mode.Aiming:
                if (CancelItemPressed())        
                {
                    ExitAimMode();              
                    break;
                }
                UpdateAim();                    
                if (UseItemPressed())           
                    Throw();
                break;
        }
    }

    //アイテムモードに入る
    void EnterAimMode()
    {
        //落下地点の初期位置をプレイヤーの正面に置く
        aimOffset = Flat(transform.forward) * Mathf.Min(defaultAimDistance, maxRange);
        //移動を止め、予測線とマーカーを表示
        SetMovementEnabled(false);
        SetAimVisuals(true);
        SetMode(Mode.Aiming);
        UpdateAim(); //開始フレームから線を表示
    }

    //アイテムモードを抜ける
    void ExitAimMode()
    {
        //予測線とマーカーを隠し、移動を元に戻す
        SetAimVisuals(false);
        SetMovementEnabled(true);
        SetMode(Mode.Normal);
    }

    //照準の更新 アイテムモード中に毎フレーム呼ぶ
    void UpdateAim()
    {
        //1.WASD でカメラ基準に落下地点を移動
        Vector2 input = ReadMove();
        Transform basis = cameraTransform ? cameraTransform : transform;
        Vector3 forward = Flat(basis.forward);
        Vector3 right = Flat(basis.right);
        aimOffset += (right * input.x + forward * input.y) * (cursorSpeed * Time.deltaTime);
        aimOffset = Vector3.ClampMagnitude(aimOffset, maxRange); //最大投擲距離に収める

        //2.プレイヤーを落下地点の方へ向ける
        if (faceTarget && aimOffset.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(Flat(aimOffset), Vector3.up);

        //3.目標点を地面に合わせ、そこへ届く初速を計算
        Vector3 target = ProjectToGround(transform.position + aimOffset);
        Vector3 start = throwOrigin.position;
        launchVelocity = CalcLaunchVelocity(start, target, out float flightTime);

        //4.軌道をサンプリング 途中の壁などに当たればそこで止める
        Vector3 landing = BuildTrajectory(start, launchVelocity, flightTime, out Vector3 normal);

        //5.計算した軌道を予測線に反映
        if (trajectoryLine)
        {
            trajectoryLine.positionCount = linePoints.Count;
            for (int i = 0; i < linePoints.Count; i++)
                trajectoryLine.SetPosition(i, linePoints[i]);
        }

        //6.着弾点にマーカーを置き、地面の傾きに合わせる
        if (landingMarker)
        {
            landingMarker.position = landing + normal * 0.02f; //地面とのちらつき防止
            landingMarker.rotation = Quaternion.FromToRotation(Vector3.up, normal);
        }
    }

    //アイテムを投げる
    void Throw()
    {
        //1.所持リストの先頭を取り出す
        GameObject item = CurrentItem;
        items.RemoveAt(0);

        //2.拾ったオブジェクトをそのまま投げる位置に出して再表示
        item.transform.SetPositionAndRotation(throwOrigin.position, Quaternion.LookRotation(launchVelocity));
        item.SetActive(true);

        //3.物理で飛ばせるようにする
        if (!item.TryGetComponent(out Rigidbody rb))
        {
            rb = item.AddComponent<Rigidbody>();
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
        rb.isKinematic = false;
        rb.useGravity = true;

        //4.初速を与える
        //予測線どおりに飛ぶようにする
        Vector3 v = launchVelocity - Physics.gravity * (0.5f * Time.fixedDeltaTime);
        rb.linearVelocity = v;
        rb.angularVelocity = UnityEngine.Random.insideUnitSphere * throwSpin;

        //5.投げた直後に自分と衝突・再拾得しないようにする
        repickBlockedUntil[item] = Time.time + repickDelay;
        StartCoroutine(IgnoreSelfCollision(item, repickDelay));

        //6.アイテム側に投げられたことを通知
        item.SendMessage("OnThrown", launchVelocity, SendMessageOptions.DontRequireReceiver);
        foreach (var t in item.GetComponents<IThrowableItem>()) t.OnThrown(gameObject, launchVelocity);

        //7.所持数の変化を通知して通常モードに戻る
        InventoryChanged?.Invoke();
        ExitAimMode();
    }

    //一定時間だけ、投げたアイテムとプレイヤーの衝突を無視する
    IEnumerator IgnoreSelfCollision(GameObject item, float duration)
    {
        Collider[] itemCols = item.GetComponentsInChildren<Collider>();
        SetIgnore(itemCols, true);
        yield return new WaitForSeconds(duration);
        if (item) SetIgnore(itemCols, false);
    }

    //アイテムとプレイヤーのコライダー同士の衝突を無視する/戻す
    void SetIgnore(Collider[] itemCols, bool ignore)
    {
        foreach (var a in itemCols)
            foreach (var b in ownColliders)
                if (a && b) Physics.IgnoreCollision(a, b, ignore);
    }

    //───────── 弾道計算 ─────────

    //start から end へ放物線で届く初速を求める
    //頂点の高さを決めてから逆算するので、高低差があっても必ず解が出る。
    Vector3 CalcLaunchVelocity(Vector3 start, Vector3 end, out float flightTime)
    {
        float g = -Physics.gravity.y; //正の値

        //1.水平方向の移動量を求める
        Vector3 flat = end - start;
        flat.y = 0f;

        //2.放物線の頂点の高さを決める 遠いほど高くする
        float extra = Mathf.Max(0.05f, arcHeight + flat.magnitude * arcHeightPerMeter);
        float apexY = Mathf.Max(start.y, end.y) + extra;

        //3.頂点まで上がるのに必要な上向きの速さと、上り・下りの時間を求める
        float vy = Mathf.Sqrt(2f * g * (apexY - start.y));
        float timeUp = vy / g;
        float timeDown = Mathf.Sqrt(2f * (apexY - end.y) / g);
        flightTime = timeUp + timeDown;

        //4.飛行時間でちょうど届く水平方向の速さを求めて合成する
        Vector3 v = flat / flightTime;
        v.y = vy;
        return v;
    }

    //時刻tの位置 start + v*t + 0.5*g*t^2 を分割して linePoints に格納し、着弾点を返す。
    Vector3 BuildTrajectory(Vector3 start, Vector3 velocity, float flightTime, out Vector3 normal)
    {
        //1.始点だけ入れて初期化
        linePoints.Clear();
        linePoints.Add(start);
        normal = Vector3.up;

        Vector3 g = Physics.gravity;
        Vector3 prev = start;
        float totalTime = flightTime * 1.05f; //地面との交点を確実に拾うため少し延長

        //2.飛行時間を lineResolution 個に分けて、各時刻の位置を求める
        for (int i = 1; i <= lineResolution; i++)
        {
            float t = totalTime * i / lineResolution;
            Vector3 p = start + velocity * t + 0.5f * t * t * g;

            //3.前の点から今の点までの間に何かあれば、そこを着弾点にして終了
            if (CastSegment(prev, p, out RaycastHit hit))
            {
                linePoints.Add(hit.point);
                normal = hit.normal;
                return hit.point;
            }

            //4.何もなければ点を追加して次へ
            linePoints.Add(p);
            prev = p;
        }

        //最後まで何にも当たらなかった場合は最後の点を着弾点にする
        return prev;
    }

    //指定した位置の真下の地面の位置を求める
    Vector3 ProjectToGround(Vector3 point)
    {
        //プレイヤーより少し上から真下にレイを飛ばす
        Vector3 from = new Vector3(point.x, transform.position.y + groundRayHeight, point.z);
        if (CastSegment(from, from + Vector3.down * (groundRayHeight * 3f), out RaycastHit hit))
            return hit.point;

        point.y = transform.position.y; //地面が無ければプレイヤーの高さ
        return point;
    }

    //from から to へレイを飛ばし、プレイヤー自身を除いた最も近いヒットを返す。
    bool CastSegment(Vector3 from, Vector3 to, out RaycastHit nearest)
    {
        nearest = default;

        //1.方向と距離を求める 距離がほぼ0なら何もしない
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 1e-5f) return false;

        //2.レイが当たったものを全部取得 トリガーは無視
        int count = Physics.RaycastNonAlloc(from, dir / dist, hitBuffer, dist,
            collisionMask, QueryTriggerInteraction.Ignore);

        //3.プレイヤー自身を除いて、一番近いものを選ぶ
        bool found = false;
        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < best)
            {
                best = h.distance;
                nearest = h;
                found = true;
            }
        }
        return found;
    }

    //───────── 補助 ─────────

    //モードを切り替えて通知する
    void SetMode(Mode mode)
    {
        if (CurrentMode == mode) return;
        CurrentMode = mode;
        ModeChanged?.Invoke(mode);
    }

    //予測線とマーカーの表示・非表示
    void SetAimVisuals(bool visible)
    {
        if (trajectoryLine) trajectoryLine.enabled = visible;
        if (landingMarker) landingMarker.gameObject.SetActive(visible);
    }

    //disableWhileAiming に入れた移動スクリプトなどのコンポーネントの有効・無効を切り替える
    void SetMovementEnabled(bool enabled)
    {
        if (disableWhileAiming == null) return;
        foreach (var b in disableWhileAiming)
            if (b) b.enabled = enabled;
    }

    //オブジェクトを親から切り離し、コライダーを無効にする
    static void DetachVisual(Transform t)
    {
        if (!t) return;
        t.SetParent(null, true);
        foreach (var c in t.GetComponentsInChildren<Collider>(true))
            c.enabled = false;
    }

    //ベクトルの上下成分を消して水平方向の単位ベクトルにする
    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }

    //───────── 入力 Input Systemのアクション─────────

    //アイテムモード開始・投擲が押されたか
    bool UseItemPressed() => useItemAction != null && useItemAction.WasPressedThisFrame();

    //キャンセルが押されたか
    bool CancelItemPressed() => cancelItemAction != null && cancelItemAction.WasPressedThisFrame();

    //落下地点の移動入力
    Vector2 ReadMove()
    {
        if (aimMoveAction == null) return Vector2.zero;
        Vector2 v = aimMoveAction.ReadValue<Vector2>();
        return v.sqrMagnitude > 1f ? v.normalized : v;
    }
}