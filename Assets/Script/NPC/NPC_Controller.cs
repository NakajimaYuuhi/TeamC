using UnityEngine;

// NPCの「頭」。ステートマシンで「どこへ行きたいか」を決めて、
// NPC_FlowLocomotion(足)に方向を渡すだけ。実際の移動・回避・向きは足がやる。
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NPC_FlowLocomotion))]
public class NPC_Controller : MonoBehaviour
{
    private Rigidbody rb;
    private NPC_FlowLocomotion _locomotion;

    private bool physicsLodSuspended;
    private bool controllerEnabledBeforeLod;
    private bool locomotionEnabledBeforeLod;
    private bool wasKinematicBeforeLod;
    private bool detectedCollisionsBeforeLod;
    private bool wasSleepingBeforeLod;
    private Vector3 velocityBeforeLod;
    private Vector3 angularVelocityBeforeLod;

    public bool IsPhysicsLodSuspended => physicsLodSuspended;
    internal NpcPhysicsLodManager PhysicsLodManager { get; set; }

    // ===== インスペクターでの設定項目 =====
    [Header("立ち止まっている状態")]
    // 初期パラメータ
    private Vector3 _defaultPosition;       // 初期位置
    private Quaternion _defaultRotation;    // 初期向き

    // 初期位置に戻る際の速さ
    [SerializeField] private float _returnSpeed = 3.0f;
    // この距離より近づいたら減速し始める
    [SerializeField] private float _returnSlowRadius = 1.0f;
    // 初期位置とみなす範囲
    [SerializeField] private float _stopDeadZone = 0.25f;

    [Header("徘徊状態の設定")]
    [SerializeField]
    private Vector3[] _targetPositions = new Vector3[]
    {
        new Vector3(5, 0, 0),
        new Vector3(-5, 0, 0),
        new Vector3(0, 0, 5)
    };
    [SerializeField] private int _currentTargetIndex = 0;
    [SerializeField] private float _browseSpeed = 6.0f;
    // 徘徊:目標点までこの距離で到着 / ノード移動:エッジの終点のこの手前で到着
    [SerializeField] private float _arrivalDistance = 0.5f;

    [Header("ノードベースでの徘徊状態の設定")]
    [SerializeField] private Node_Edge_Data.Node _node;     // 今向かっているノード
    // 過去のノードのID
    private int _previousNodeID = -1;
    // ノードのデータ(未設定なら自動で探す)
    [SerializeField] private NodeEdgeGenerator _nodeGenerator;
    // エッジが見つからないときの幅
    [SerializeField] private float _defaultEdgeWidth = 2.0f;
    // エッジの幅からはみ出したとき、内側に戻ろうとする強さ
    [SerializeField] private float _lateralGain = 0.5f;
    // 横に戻る補正の上限(1で最大45°くらい斜めに進む)
    [SerializeField] private float _maxLateralCorrection = 0.5f;
    // エッジの幅のこの割合の内側なら、横には補正しない(壁ぎわに張り付かないように1より小さく)
    [SerializeField, Range(0f, 1f)] private float _maxOffsetRate = 0.9f;

    // 今歩いているエッジ(位置で持つ。始点がノードでないときもある)
    private bool _hasEdge;
    private bool _hasNode;   // SetNode されたことがあるか
    private Vector3 _edgeFrom;
    private Vector3 _edgeTo;
    private float _edgeHalfWidth;
    // 中心からのズレ(-1:左端 ～ 1:右端)。エッジを乗り換えても引き継ぐ
    private float _offsetRate;

    // ----- 状態 -----
    public enum NPCStateType
    {
        Standing,
        Browsing,
        Browsing_BasedOnNode
    }

    [Header("状態の設定")]
    [SerializeField] private NPCStateType _initialStateType = NPCStateType.Standing;
    [SerializeField] private NPCStateType _currentStateType = NPCStateType.Standing;
    private StateMachine<NPC_Controller> _NPCState;
    private NPCStanding _NPCStanding = new NPCStanding();
    private NPCBrowsing _NPCBrowsing = new NPCBrowsing();
    private NPCBrowsing_BasedOnNode _NPCBrowsing_BasedOnNode = new NPCBrowsing_BasedOnNode();

    public float ArrivalDistance => _arrivalDistance;
    public NPCStateType CurrentStateType => _currentStateType;


    // ===== Unityのライフサイクル =====

    void Awake()
    {
        // 初期位置と初期向きを記憶
        _defaultPosition = transform.position;
        _defaultRotation = transform.rotation;

        rb = GetComponent<Rigidbody>();
        _locomotion = GetComponent<NPC_FlowLocomotion>();

        // 回転のフリーズ(向きは足が MoveRotation で変える)
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

        // ステートマシンの初期化
        _NPCState = new StateMachine<NPC_Controller>(this, _NPCStanding);

        // 初期状態の設定
        SetState(_initialStateType);
    }

    void FixedUpdate()
    {
        // 割り込みの時間切れ → 元の目的地に戻す
        if (_isRedirected && _redirectTimer > 0f)
        {
            _redirectTimer -= Time.fixedDeltaTime;
            if (_redirectTimer <= 0f) EndRedirect();
        }

        // ステートマシンの更新
        _NPCState.OnUpdate();
    }

    public void SetPhysicsLodSuspended(bool suspended)
    {
        if (physicsLodSuspended == suspended) return;

        if (suspended)
        {
            controllerEnabledBeforeLod = enabled;
            locomotionEnabledBeforeLod = _locomotion.enabled;
            wasKinematicBeforeLod = rb.isKinematic;
            detectedCollisionsBeforeLod = rb.detectCollisions;
            wasSleepingBeforeLod = rb.IsSleeping();
            if (!wasKinematicBeforeLod)
            {
                velocityBeforeLod = rb.linearVelocity;
                angularVelocityBeforeLod = rb.angularVelocity;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            rb.detectCollisions = false;
            rb.isKinematic = true;
            physicsLodSuspended = true;
            enabled = false;
            _locomotion.enabled = false;   // 足も止める(Thinkの負荷も止まる)
            return;
        }

        rb.isKinematic = wasKinematicBeforeLod;
        rb.detectCollisions = detectedCollisionsBeforeLod;
        if (!wasKinematicBeforeLod)
        {
            rb.linearVelocity = velocityBeforeLod;
            rb.angularVelocity = angularVelocityBeforeLod;
            if (wasSleepingBeforeLod) rb.Sleep();
            else rb.WakeUp();
        }

        physicsLodSuspended = false;
        enabled = controllerEnabledBeforeLod;
        _locomotion.enabled = locomotionEnabledBeforeLod;
    }

    // ===== 足への指示 =====

    private void Move(Vector3 dir, float speed, float rate = 1f)
    {
        _locomotion.MoveSpeed = speed;
        _locomotion.SetDesiredDirection(dir, rate);
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    // ===== 立ち止まり =====

    // 初期位置からずれていたら戻る。戻ったら初期の向きを向いて踏ん張る
    public void UpdateStanding()
    {
        _locomotion.SetIdleFacing(_defaultRotation * Vector3.forward);

        Vector3 toDefault = Flat(_defaultPosition - transform.position);
        float dist = toDefault.magnitude;
        if (dist <= _stopDeadZone)
        {
            _locomotion.Stop();
            return;
        }

        // 近づくほど減速(行き過ぎて往復しないように)
        float rate = Mathf.Clamp(dist / Mathf.Max(_returnSlowRadius, 0.01f), 0.2f, 1f);
        Move(toDefault, _returnSpeed, rate);
    }

    // ===== 巡回地点(点から点) =====

    public void UpdatePatrol()
    {
        _locomotion.ClearIdleFacing();
        if (_targetPositions == null || _targetPositions.Length == 0)
        {
            _locomotion.Stop();
            return;
        }

        Vector3 toTarget = Flat(_targetPositions[_currentTargetIndex] - transform.position);
        if (toTarget.magnitude < _arrivalDistance)
        {
            AdvanceToNextTarget();
            return;
        }
        Move(toTarget, _browseSpeed);
    }

    public void AdvanceToNextTarget()
    {
        if (_targetPositions == null || _targetPositions.Length == 0) return;
        _currentTargetIndex = (_currentTargetIndex + 1) % _targetPositions.Length;
    }

    public Vector3 GetCurrentPatrolPosition()
    {
        if (_targetPositions == null || _targetPositions.Length == 0) return transform.position;
        return _targetPositions[_currentTargetIndex];
    }

    // ===== ノードベースの移動(エッジに沿って平行に進む) =====
    //
    // エッジ上の位置を2つの数字で考える
    //   s   : 始点から終点に向かってどこまで進んだか
    //   lat : 中心線から右にどれだけずれているか
    // 進む方向 = エッジの向き + 目標のズレ(_offsetRate × 幅の半分)へ戻る小さな補正
    // 到着判定 = s が「終点の少し手前」を越えたか(点ではなく線で判定するので1点に集まらない)

    public void UpdateNodeTravel()
    {
        _locomotion.ClearIdleFacing();
        if (!_hasEdge)
        {
            _locomotion.Stop();
            return;
        }

        Vector3 axis = Flat(_edgeTo - _edgeFrom);
        float length = axis.magnitude;
        if (length < 0.01f)
        {
            AdvanceToNextNode();
            return;
        }
        Vector3 forward = axis / length;
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        Vector3 rel = Flat(transform.position - _edgeFrom);
        float s = Vector3.Dot(rel, forward);
        float lat = Vector3.Dot(rel, right);

        // 到着:終点の手前の線を越えた
        if (s >= length - _arrivalDistance)
        {
            AdvanceToNextNode();
            return;
        }

        // 基本はエッジに平行にまっすぐ進むだけ。
        // 押されたり避けたりしてずれたら、そのずれた位置をそのまま新しいズレとして記録する(元には戻らない)。
        // 戻すのは、エッジの幅(_maxOffsetRate)からはみ出したときだけ。
        float correction = 0f;
        if (_edgeHalfWidth > 0.01f)
        {
            float limit = _edgeHalfWidth * _maxOffsetRate;
            if (lat > limit) correction = -Mathf.Min((lat - limit) * _lateralGain, _maxLateralCorrection);
            else if (lat < -limit) correction = Mathf.Min((-limit - lat) * _lateralGain, _maxLateralCorrection);

            // 次のエッジに引き継ぐズレ(割合)
            _offsetRate = Mathf.Clamp(lat / _edgeHalfWidth, -_maxOffsetRate, _maxOffsetRate);
        }

        Move(forward + right * correction, _browseSpeed);
    }

    // 今向かっているノードを設定する(スポナーなどから)。
    // 「どこから来たか」は、このノードに入ってくるエッジのうち、自分が一番近いものとみなす。
    // 見つからなければ、今の位置からノードへ向かう仮のエッジを作る。
    public void SetNode(Node_Edge_Data.Node node)
    {
        _node = node;
        _hasNode = true;
        EnsureGenerator();

        // node に入ってくるエッジのうち、自分が一番近いエッジの始点(一方通行にも対応)
        int bestFrom = _nodeGenerator != null
            ? _nodeGenerator.FindNearestIncomingNode(node.NodeID, transform.position)
            : -1;

        if (bestFrom != -1)
        {
            BeginEdge(_nodeGenerator.GetNode(bestFrom), node, measureOffset: true);
        }
        else
        {
            // 仮のエッジ:今の位置 → ノード
            _previousNodeID = -1;
            _edgeFrom = transform.position;
            _edgeTo = node.position;
            _edgeHalfWidth = _defaultEdgeWidth * 0.5f;
            _offsetRate = 0f;
            _hasEdge = true;
        }
    }

    // 現在のノードの座標を取得
    public Vector3 GetCurrentNodePosition()
    {
        return _node.position;
    }

    // 次のノードに進む(直前のノードにはなるべく戻らない)
    public void AdvanceToNextNode()
    {
        // 最終目的地に着いた → 止まる
        if (_goalNodeID >= 0 && _hasNode && _node.NodeID == _goalNodeID)
        {
            _hasEdge = false;
            GoalReached?.Invoke(this);
            if (_destroyOnGoalReached) Destroy(gameObject);
            return;
        }

        if (_node.adjacents == null || _node.adjacents.Count == 0)
        {
            // 行き止まり:止まる
            _hasEdge = false;
            return;
        }

        int count = _node.adjacents.Count;
        int nextNodeID;

        // 最終目的地があれば、そこへの一番短い経路の次のノードへ
        if (_goalNodeID >= 0 && Route(_node.NodeID, _goalNodeID, out int hop, out _) && hop >= 0)
        {
            AdvanceTo(hop);
            return;
        }

        // 直前のノードが隣接リストに含まれていて、ほかに選択肢がある場合
        int prevIndex = (_previousNodeID != -1 && count > 1)
            ? _node.adjacents.IndexOf(_previousNodeID)
            : -1;

        if (prevIndex != -1)
        {
            // 直前のノードを除いた (count - 1) 個から抽選
            int randomIndex = Random.Range(0, count - 1);
            // 直前と同じインデックス以上の値が出たら、1つずらす
            if (randomIndex >= prevIndex) randomIndex++;
            nextNodeID = _node.adjacents[randomIndex];
        }
        else
        {
            // 行き止まり(戻るしかない)の場合は通常抽選
            nextNodeID = _node.adjacents[Random.Range(0, count)];
        }

        AdvanceTo(nextNodeID);
    }

    // 今のノードから nextNodeID へのエッジを歩き始める
    private void AdvanceTo(int nextNodeID)
    {
        EnsureGenerator();
        if (_nodeGenerator == null)
        {
            _hasEdge = false;
            return;
        }

        Node_Edge_Data.Node from = _node;
        Node_Edge_Data.Node next = _nodeGenerator.GetNode(nextNodeID);
        _node = next;
        // ズレの割合はそのまま引き継ぐ(右寄りの人は次のエッジでも右寄り)
        BeginEdge(from, next, measureOffset: false);
    }

    // ===== 最終目的地(経路) =====
    //
    // 最終目的地を決めると、各ノードで「そこへの一番短い経路」の次のノードを選ぶ(決めないときは今までどおりランダム)。
    // 最終目的地に着いたら止まって GoalReached を呼ぶ。

    private int _goalNodeID = -1;

    [Header("最終目的地")]
    // オン:最終目的地に着いたら消える / オフ:その場で止まる(初期値)
    [SerializeField] private bool _destroyOnGoalReached = false;

    /// <summary>最終目的地のノードID(-1 = なし。ランダムに歩く)</summary>
    public int GoalNodeID => _goalNodeID;
    /// <summary>最終目的地に着いたときに呼ばれる</summary>
    public event System.Action<NPC_Controller> GoalReached;

    /// <summary>最終目的地を決める(ずっと有効。割り込みではない)</summary>
    public void SetGoal(int goalNodeID) => _goalNodeID = goalNodeID;
    /// <summary>最終目的地をなくす(ランダムに歩く)</summary>
    public void ClearGoal() => _goalNodeID = -1;

    /// <summary>
    /// 今いるエッジの両端のうち、最終目的地に早く着ける方へ向かう(必要ならその場でUターン)。
    /// 双方向のエッジで使う。
    /// </summary>
    private void HeadTowardGoal()
    {
        EnsureGenerator();
        if (_nodeGenerator == null || _goalNodeID < 0) return;

        int best = -1;
        float bestLen = float.PositiveInfinity;

        // 候補:今向かっているノードと、来た方のノード
        int[] candidates = { _hasNode ? _node.NodeID : -1, _previousNodeID };
        foreach (int c in candidates)
        {
            if (c < 0) continue;
            float toC = Vector3.Distance(Flat(transform.position), Flat(_nodeGenerator.GetNode(c).position));
            float rest;
            if (c == _goalNodeID) rest = 0f;
            else if (!Route(c, _goalNodeID, out _, out rest)) continue;

            if (toC + rest < bestLen)
            {
                bestLen = toC + rest;
                best = c;
            }
        }

        // どこにもつながっていなければ、目的地に直接向かう(仮のエッジ)
        SetNode(_nodeGenerator.GetNode(best >= 0 ? best : _goalNodeID));
    }

    /// <summary>
    /// start から goal への一番短い経路(エッジの向きを守る)。
    /// firstHop:start の次に進むノード / length:経路の長さ(m)。たどり着けなければ false
    /// </summary>
    private bool Route(int start, int goal, out int firstHop, out float length)
    {
        firstHop = -1;
        length = 0f;
        EnsureGenerator();
        if (_nodeGenerator == null) return false;
        if (start == goal) return true;

        var dist = new System.Collections.Generic.Dictionary<int, float>();
        var first = new System.Collections.Generic.Dictionary<int, int>();
        var open = new System.Collections.Generic.List<int>();
        dist[start] = 0f;
        open.Add(start);

        while (open.Count > 0)
        {
            // 一番近いノードを取り出す(ノード数が少ないので単純な探し方で十分)
            int bi = 0;
            for (int i = 1; i < open.Count; i++)
                if (dist[open[i]] < dist[open[bi]]) bi = i;
            int u = open[bi];
            open.RemoveAt(bi);

            if (u == goal)
            {
                firstHop = first[u];
                length = dist[u];
                return true;
            }

            Node_Edge_Data.Node nu = _nodeGenerator.GetNode(u);
            if (nu.adjacents == null) continue;
            foreach (int v in nu.adjacents)
            {
                float nd = dist[u] + Vector3.Distance(nu.position, _nodeGenerator.GetNode(v).position);
                if (!dist.TryGetValue(v, out float dv) || nd < dv)
                {
                    dist[v] = nd;
                    first[v] = (u == start) ? v : first[u];
                    if (!open.Contains(v)) open.Add(v);
                }
            }
        }
        return false;
    }

    // from → to のエッジを歩き始める
    private void BeginEdge(Node_Edge_Data.Node from, Node_Edge_Data.Node to, bool measureOffset)
    {
        _previousNodeID = from.NodeID;
        _edgeFrom = from.position;
        _edgeTo = to.position;

        float width = _defaultEdgeWidth;
        if (_nodeGenerator != null)
        {
            int edgeIndex = _nodeGenerator.FindEdgeIndex(from.NodeID, to.NodeID);
            if (edgeIndex != -1) width = _nodeGenerator.GetEdgeWidth(edgeIndex);
        }
        _edgeHalfWidth = Mathf.Max(0f, width * 0.5f);

        if (measureOffset)
        {
            // 今いる位置から、中心からのズレを測る(スポーン位置のばらつきがそのまま残る)
            Vector3 axis = Flat(_edgeTo - _edgeFrom);
            Vector3 right = axis.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, axis.normalized) : Vector3.right;
            float lat = Vector3.Dot(Flat(transform.position - _edgeFrom), right);
            _offsetRate = _edgeHalfWidth > 0.01f ? lat / _edgeHalfWidth : 0f;
        }
        _offsetRate = Mathf.Clamp(_offsetRate, -_maxOffsetRate, _maxOffsetRate);
        _hasEdge = true;
    }

    private void EnsureGenerator()
    {
        if (_nodeGenerator == null) _nodeGenerator = FindAnyObjectByType<NodeEdgeGenerator>();
    }

    // ===== 割り込み(目的地の一時変更) =====
    //
    // アイテムなどで目的地を一時的に変えて、あとで元に戻すための関数。
    //  - Begin～ を呼ぶと、元の目的地(状態・ノードデータ・ノード)を保存してから目的地を変える
    //  - 割り込み中にもう一度 Begin～ を呼んでも、保存されるのは最初の(本来の)目的地のまま
    //  - EndRedirect で、今いる位置から本来の目的地へのエッジを作り直して戻る
    //  - duration > 0 なら、その秒数後に自動で EndRedirect する。0以下なら EndRedirect を呼ぶまでそのまま

    private struct RouteSnapshot
    {
        public NPCStateType state;
        public NodeEdgeGenerator generator;
        public Node_Edge_Data.Node node;
        public bool hasNode;
        public int goal;
    }

    private bool _isRedirected;
    private RouteSnapshot _savedRoute;
    private float _redirectTimer = -1f;

    /// <summary>割り込みで目的地が変わっている途中か</summary>
    public bool IsRedirected => _isRedirected;
    /// <summary>自動で戻るまでの残り秒数(自動で戻らないときは -1 以下)</summary>
    public float RedirectTimeLeft => _isRedirected ? _redirectTimer : -1f;
    /// <summary>割り込みが終わって元の目的地に戻ったときに呼ばれる</summary>
    public event System.Action<NPC_Controller> RedirectEnded;

    /// <summary>
    /// 目的地を一時的に別のノードに変える。
    /// generator:そのノードを持っているノードデータ(逆向き用のSOなど)。targetNodeID:向かうノード。
    /// duration:何秒後に元に戻すか(0以下なら EndRedirect を呼ぶまで)。
    /// </summary>
    public void BeginRedirect(NodeEdgeGenerator generator, int targetNodeID, float duration = -1f)
    {
        if (generator == null) return;
        SaveRouteIfNeeded();

        _nodeGenerator = generator;
        SetState(NPCStateType.Browsing_BasedOnNode);
        SetNode(generator.GetNode(targetNodeID));
        _redirectTimer = duration;
    }

    /// <summary>
    /// 最終目的地を一時的に変える(双方向のSOで使う)。
    /// 今いるエッジの両端のうち、新しい目的地に早く着ける方へ向かう(反対方向なら、その場でUターン)。
    /// generator:ノードデータ(普通は今と同じもの。null なら今のまま)。duration:何秒後に元に戻すか(0以下なら EndRedirect まで)。
    /// </summary>
    public void BeginRedirectToGoal(NodeEdgeGenerator generator, int goalNodeID, float duration = -1f)
    {
        SaveRouteIfNeeded();
        if (generator != null) _nodeGenerator = generator;
        _goalNodeID = goalNodeID;
        SetState(NPCStateType.Browsing_BasedOnNode);
        HeadTowardGoal();
        _redirectTimer = duration;
    }

    /// <summary>
    /// 今歩いているエッジの上でUターンして、来た方のノードへ向かう(横の位置は左右を入れ替えて保つ)。
    /// ※ 来た方のノードに着いたあとは、今のノードデータの隣接ノードへ進む。
    ///    一方通行のSOだと、また元の向きに戻ってしまうので、続けて逆向きに進ませたいなら BeginRedirect で逆向き用のSOを渡す。
    /// </summary>
    public void BeginReverse(float duration = -1f)
    {
        if (!_hasEdge) return;
        SaveRouteIfNeeded();

        Vector3 from = _edgeFrom;
        _edgeFrom = _edgeTo;
        _edgeTo = from;
        _offsetRate = -_offsetRate;   // 向きが逆になると左右も逆になる

        EnsureGenerator();
        if (_previousNodeID >= 0 && _nodeGenerator != null)
        {
            int newTarget = _previousNodeID;
            _previousNodeID = _node.NodeID;
            _node = _nodeGenerator.GetNode(newTarget);
        }
        else
        {
            // 仮のエッジ(始点がノードでない)だった → 戻った先で止まる
            _previousNodeID = -1;
            _node = default;
        }

        SetState(NPCStateType.Browsing_BasedOnNode);
        _redirectTimer = duration;
    }

    /// <summary>割り込みを終わらせて、本来の目的地に戻す(割り込み中でなければ何もしない)</summary>
    public void EndRedirect()
    {
        if (!_isRedirected) return;
        _isRedirected = false;
        _redirectTimer = -1f;

        RouteSnapshot r = _savedRoute;
        if (r.generator != null) _nodeGenerator = r.generator;
        _goalNodeID = r.goal;
        SetState(r.state);
        // 今いる位置から本来の目的地へのエッジを作り直す(横のズレも測り直す)
        if (r.goal >= 0) HeadTowardGoal();      // 最終目的地があれば、早く着ける方へ(必要ならUターン)
        else if (r.hasNode) SetNode(r.node);

        RedirectEnded?.Invoke(this);
    }

    private void SaveRouteIfNeeded()
    {
        if (_isRedirected) return;   // すでに割り込み中なら、本来の目的地を保ったまま
        _savedRoute = new RouteSnapshot
        {
            state = _currentStateType,
            generator = _nodeGenerator,
            node = _node,
            hasNode = _hasNode,
            goal = _goalNodeID,
        };
        _isRedirected = true;
    }

    // 古い呼び出しとの互換用
    public void SetNodeData(GameObject data)
    {
        if (data != null) _nodeGenerator = data.GetComponent<NodeEdgeGenerator>();
    }

    public void SetNodeGenerator(NodeEdgeGenerator generator) => _nodeGenerator = generator;

    private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 1e-6f) return Vector3.Distance(p, a);
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
        return Vector3.Distance(p, a + ab * t);
    }

    // ===== 状態の切り替え =====
    public void SetState(string stateName)
    {
        switch (stateName)
        {
            case "Standing": SetState(NPCStateType.Standing); break;
            case "Browsing": SetState(NPCStateType.Browsing); break;
            case "Browsing_BasedOnNode": SetState(NPCStateType.Browsing_BasedOnNode); break;
            default: Debug.LogWarning($"Unknown state: {stateName}"); break;
        }
    }

    public void SetState(NPCStateType stateType)
    {
        switch (stateType)
        {
            case NPCStateType.Standing:
                _NPCState.ChangeState(_NPCStanding);
                break;
            case NPCStateType.Browsing:
                _NPCState.ChangeState(_NPCBrowsing);
                break;
            case NPCStateType.Browsing_BasedOnNode:
                _NPCState.ChangeState(_NPCBrowsing_BasedOnNode);
                break;
            default:
                Debug.LogWarning($"Unknown state type: {stateType}");
                return;
        }
        _currentStateType = stateType;
    }

    // ===== デバッグ表示 =====
    // 選択中:歩いているエッジの幅(灰色)と、目標のズレの線(水色)
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !_hasEdge || _currentStateType != NPCStateType.Browsing_BasedOnNode) return;

        Vector3 axis = Flat(_edgeTo - _edgeFrom);
        if (axis.sqrMagnitude < 1e-6f) return;
        Vector3 right = Vector3.Cross(Vector3.up, axis.normalized);
        Vector3 up = Vector3.up * 0.05f;

        Vector3 l0 = _edgeFrom - right * _edgeHalfWidth + up, l1 = _edgeTo - right * _edgeHalfWidth + up;
        Vector3 r0 = _edgeFrom + right * _edgeHalfWidth + up, r1 = _edgeTo + right * _edgeHalfWidth + up;
        Gizmos.color = Color.gray;
        Gizmos.DrawLine(l0, l1);
        Gizmos.DrawLine(r0, r1);

        Vector3 o = right * (_offsetRate * _edgeHalfWidth) + up;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(_edgeFrom + o, _edgeTo + o);

        // 到着ライン(黄色)
        Vector3 arrive = _edgeTo - axis.normalized * _arrivalDistance + up;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(arrive - right * _edgeHalfWidth, arrive + right * _edgeHalfWidth);
    }
}
