/////////////////////////////////////////
/// PlayerMove.cs
/// 2026/10/20 作者：佐藤蒼也
/// 
/// ログ
/// 2026/10/20 佐藤蒼也：プレイヤー移動処理実装→Move関数
/// 2026/10/20 佐藤蒼也：Shift中の横歩き処理実装→Move関数(もともとPlayerBodyTurn)
/// 2026/10/21 佐藤蒼也：PlayerBodyTurnの処理を統合
/// 2026/10/21 佐藤蒼也：横歩き中は常に進行方向の左を向くよう変更→RotateToward関数
/// 2026/10/21 佐藤蒼也：NPCを押す処理を追加→PushNPC関数
/// 2026/10/22 佐藤蒼也：サイドステップを追加→CheckStepInput・StartStep・UpdateStep・EndStep関数
/// 
/////////////////////////////////////////




using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMove : MonoBehaviour
{
    [Header("プレイヤーのデータ")]
    [SerializeField] private PlayerData playerData;



    [Header("横歩きの設定,通常時に対する速度の割合")]
    [SerializeField, Range(0.1f, 1f)] private float sideStepSpeedRate = 0.5f; //通常時に対する速度の割合

    [Header("サイドステップの設定")]
    [SerializeField, Tooltip("1回のステップで進む距離")]
    private float stepDistance = 2.5f;
    [SerializeField, Tooltip("ステップにかかる時間（秒）")]
    private float stepDuration = 0.2f;
    [SerializeField, Tooltip("ステップのあと、次のステップができるまでの時間（秒）")]
    private float stepCooldown = 0.3f;

    Rigidbody rb;
    private Vector2 moveInput; //移動の入力を格納する変数
    private CameraController cameraController; //移動方向の基準にするカメラ
    private float modelYawOffset = 90f; //モデルの正面のずれ補正
    private CapsuleCollider pushArea;//押すためのコライダー格納

    //プレイヤーパラメータ
    private float moveSpeed; //移動速度を格納する変数
    private float turnSpeed; //進行方向へ体を向ける回転速度
    private Vector3 playerSize; //プレイヤーのサイズを格納する変数
    private float playerPushPower; //プレイヤーの押す力を格納する変数
    private float currentNuisance; //プレイヤーの迷惑度を格納する変数
    private float voiceVolume; //プレイヤーの声の音量を格納する変数
    private float facingMinSpeedRate; //体の向きが真逆のときの速度の割合
    private float facingSpeedCurve;   //向きのずれに対する減速のかかり方

    private float faceLeftTurnSpeed = 5f; //Shift中に体の正面を進行方向の左に向ける回転速度

    //サイドステップ用
    private bool stepRequested;      //ステップの入力があったか
    private Vector2 stepInput;       //ステップを入力したときのキーの向き
    private bool isStepping;         //ステップ中か
    private float stepTimer;         //ステップの残り時間
    private float stepCooldownTimer; //次のステップができるまでの残り時間
    private Vector3 stepVelocity;    //ステップ中の速度


    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        PlayerDataInitialized();//プレイヤー変数初期化

        pushArea = transform.Find("PushArea").GetComponent<CapsuleCollider>();//push用オブジェクトを取得
        pushArea.enabled = false;//push用オブジェクトを非表示にする


        //MainCameraタグのついたカメラからCameraControllerを取得
        if (Camera.main != null)
        {
            cameraController = Camera.main.GetComponent<CameraController>();
        }

        if (cameraController == null)
        {
            Debug.LogError("CameraControllerのついたMainCameraがない");
        }
    }

    void Update()
    {
        CheckStepInput();//サイドステップの入力確認
    }

    void FixedUpdate()
    {
        //クールダウンを減らす
        if (stepCooldownTimer > 0f)
        {
            stepCooldownTimer -= Time.fixedDeltaTime;
        }

        //ステップの入力があればステップを始める
        if (stepRequested)
        {
            StartStep();
        }

        //ステップ中は通常の移動をしない
        if (isStepping)
        {
            UpdateStep();
            return;
        }

        //Shiftが押されているか
        bool isSideStep = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

        Move(isSideStep);//プレイヤー移動処理
    }

    //プレイヤー変数初期化
    private void PlayerDataInitialized()
    {
        moveSpeed = playerData.MoveSpeed; //プレイヤーの移動速度を取得
        playerSize = playerData.PlayerSize; //プレイヤーのサイズを取得
        turnSpeed = playerData.TurnSpeed; //進行方向への回転速度を取得
        playerPushPower = playerData.PlayerPushPower; //プレイヤーの押す力を取得
        currentNuisance = playerData.PlayerNuisance;//プレイヤーの迷惑度を取得

        facingMinSpeedRate = playerData.FacingMinSpeedRate; //向きが真逆のときの速度の割合を取得
        facingSpeedCurve = playerData.FacingSpeedCurve;     //向きのずれに対する減速のかかり方を取得

        //親オブジェクトのサイズを設定
        if (transform.parent != null)
        {
            transform.parent.localScale = playerSize;
        }
        else
        {
            transform.localScale = playerSize;
        }
    }

    //移動の入力を受け取る関数 WASD
    void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();//入力を取得 
    }

    //移動処理
    private void Move(bool isSideStep)
    {
        if (cameraController == null) return;

        //速度を保存
        float speed = moveSpeed;

        //横歩き中は速度を落とす
        if (isSideStep)
        {
            speed *= sideStepSpeedRate;
        }

        //カメラの回転角度から水平な前方向・右方向を取得
        Vector3 forward = cameraController.FlatForward;
        Vector3 right = cameraController.FlatRight;

        //W/Sでカメラの奥・手前、A/Dで画面の左右に進みたい方向
        Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;

        //入力がないときは止まる（向きも変えない）
        if (moveDirection.sqrMagnitude <= 0.01f)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        //体を向けるべき方向を決めて、そちらへ回転させる
        Vector3 desiredFacing;
        if (isSideStep)
        {
            //横歩き：押したキーの方向に進み、体の正面は進行方向の左に向ける 上と進行方向の外積で左側がもとまる
            desiredFacing = Vector3.Cross(moveDirection, Vector3.up);
            RotateToward(desiredFacing, faceLeftTurnSpeed);//進行方向の左側を向くように
        }
        else
        {
            //通常移動：進行方向へ体を向ける
            desiredFacing = moveDirection;
            RotateToward(desiredFacing, turnSpeed);
        }

        //体の向きのずれに応じて速度を落とす
        speed *= GetFacingSpeedRate(desiredFacing);

        Vector3 horizontalVelocity = moveDirection.normalized * speed;
        rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);
    }

    //体の正面を指定した方向へ、指定した速さで滑らかに向ける
    private void RotateToward(Vector3 direction, float speed)
    {
        //向けたい方向にモデルの正面のずれ分を足した向きを目標にする
        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0f, modelYawOffset, 0f);
        Quaternion newRotation = Quaternion.Slerp(rb.rotation, targetRotation, speed * Time.fixedDeltaTime);
        rb.MoveRotation(newRotation);
    }

    //今の体の正面を求める関数
    private Vector3 GetBodyForward()
    {
        //RotateTowardで足したモデルのずれ補正を逆に戻して、本来の正面を求める
        Vector3 bodyForward = rb.rotation * Quaternion.Euler(0f, -modelYawOffset, 0f) * Vector3.forward;
        bodyForward.y = 0f;
        return bodyForward.normalized;
    }

    //体の向きと向くべき方向のずれから、速度の割合を求める
    private float GetFacingSpeedRate(Vector3 desiredFacing)
    {
        //内積で向きのずれを求める
        float dot = Vector3.Dot(GetBodyForward(), desiredFacing.normalized);

        //-1から1 を 0から1 に変換
        float t = (dot + 1f) * 0.5f;

        //カーブをかけて、ずれに対する減速のかかり方を調整する
        t = Mathf.Pow(t, facingSpeedCurve);

        return Mathf.Lerp(facingMinSpeedRate, 1f, t);
    }

    //----------------------------------------------------------------------------------------
    //サイドステップ

    //Shift + A/D が押された瞬間に実行される
    private void CheckStepInput()
    {
        if (Keyboard.current == null) return;
        if (isStepping || stepRequested || stepCooldownTimer > 0f) return;

        bool isShift = Keyboard.current.leftShiftKey.isPressed;
        bool isStepKeyPressed = Keyboard.current.aKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame;

        if (isShift && isStepKeyPressed)
        {
            //押されているキーからステップの向きを決める
            float x = 0f;
            float y = 0f;
            if (Keyboard.current.dKey.isPressed) x += 1f;
            if (Keyboard.current.aKey.isPressed) x -= 1f;
            if (Keyboard.current.wKey.isPressed) y += 1f;
            if (Keyboard.current.sKey.isPressed) y -= 1f;
            if (Mathf.Approximately(x, 0f)) return;

            stepInput = new Vector2(x, y);
            stepRequested = true;
        }
    }

    //ステップ開始
    private void StartStep()
    {
        stepRequested = false;

        if (cameraController == null) return;

        //カメラ基準でステップする方向を求める
        Vector3 direction = cameraController.FlatForward * stepInput.y + cameraController.FlatRight * stepInput.x;
        direction.Normalize();

        if(TryGetComponent<PlayerCryticalStep>(out PlayerCryticalStep crtStep))
        {
            if(crtStep.GetCanStepPosition(transform.position,playerSize,direction))
            {
                return;
            }

        }

        //決めた時間で決めた距離を進む速度
        stepVelocity = direction * (stepDistance / stepDuration);

        stepTimer = stepDuration;
        isStepping = true;
    }

    //ステップ中の処理
    private void UpdateStep()
    {
        rb.linearVelocity = new Vector3(stepVelocity.x, rb.linearVelocity.y, stepVelocity.z);

        stepTimer -= Time.fixedDeltaTime;
        if (stepTimer <= 0f)
        {
            EndStep();
        }
    }

    //ステップ終了
    private void EndStep()
    {
        isStepping = false;
        stepCooldownTimer = stepCooldown;

        //横方向の速度を止める　ないと若干動いたりする
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    //----------------------------------------------------------------------------------------
    //プッシュ

    //プッシュ(スペースキー)が押されたときの処理
    void OnPush(InputValue value)
    {

        if (value.isPressed)
        {
            //押す処理
            PushNPC();
        }
    }

    //NPCを押す処理
    private void PushNPC()
    {
        pushArea.enabled = true;//push用オブジェクトをアクティブに
        //CapsuleColliderの設定から、ワールド上の両端の点と半径を求める
        GetCapsuleWorld(pushArea, out Vector3 point0, out Vector3 point1, out float radius);

        //範囲内のコライダーをすべて取得
        Collider[] hits = Physics.OverlapCapsule(point0, point1, radius);
        //Debug.Log("ヒット数:" + hits.Length); //確認用
        //同じNPCを2回数えないように、数えたNPCを記録しておく
        var countedNPCs = new System.Collections.Generic.HashSet<GameObject>();

        foreach (Collider hit in hits)
        {
            //当たったコライダーから親をたどって、NPCタグの付いたオブジェクトを探す
            GameObject npc = FindNPCRoot(hit.transform);

            //NPCにヒットしたら、まだ数えていないNPCだけ処理する
            if (npc != null && countedNPCs.Add(npc))
            {
                currentNuisance += playerPushPower / 10.0f;

                //NPCのRigidbodyを取得 当たったコライダーのRigidbody → なければNPCの中から探す
                Rigidbody npcRb = hit.attachedRigidbody;
                if (npcRb == null)
                {
                    npcRb = npc.GetComponentInChildren<Rigidbody>();
                }

                if (npcRb != null)
                {
                    //プレイヤーからNPCへの水平な方向に押し飛ばす
                    Vector3 pushDirection = npcRb.position - transform.position;
                    pushDirection.y = 0f;
                    pushDirection.Normalize();

                    npcRb.AddForce(pushDirection * playerPushPower, ForceMode.Impulse);
                }
            }
        }

        pushArea.enabled = false;//処理が終わったらpush用コライダーを無効に
    }

    //当たったオブジェクトから親へたどり、NPCタグの付いたオブジェクトを返す
    private GameObject FindNPCRoot(Transform hitTransform)
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

    //CapsuleColliderのワールド上の両端の点（球の中心）と半径を求める
    //OverlapCapsule関数の引数に使うための計算を行う
    private void GetCapsuleWorld(CapsuleCollider capsule, out Vector3 point0, out Vector3 point1, out float radius)
    {
        Transform t = capsule.transform;
        Vector3 scale = t.lossyScale;

        //Directionの設定（0:X軸 1:Y軸 2:Z軸）に合わせて、カプセルの伸びる向きを決める
        Vector3 axis;
        float heightScale;
        float radiusScale;

        if (capsule.direction == 0)
        {
            axis = Vector3.right;
            heightScale = Mathf.Abs(scale.x);
            radiusScale = Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }
        else if (capsule.direction == 1)
        {
            axis = Vector3.up;
            heightScale = Mathf.Abs(scale.y);
            radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        }
        else
        {
            axis = Vector3.forward;
            heightScale = Mathf.Abs(scale.z);
            radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        }

        radius = capsule.radius * radiusScale;
        float height = capsule.height * heightScale;

        //両端の球の中心までの距離（高さの半分から半径を引いた分）
        float offset = Mathf.Max(height * 0.5f - radius, 0f);

        Vector3 center = t.TransformPoint(capsule.center);
        Vector3 worldAxis = t.TransformDirection(axis); //回転を反映した伸びる向き

        point0 = center + worldAxis * offset;
        point1 = center - worldAxis * offset;
    }

    //----------------------------------------------------------------------------------------
    //PlayerData(元パラメータSO)の値を変更する関数


    //移動速度
    public void SetMoveSpeed(float speed)
    {
        moveSpeed = speed;
    }

    //回転速度(普段の回転)
    public void SetTurnSpeed(float speed)
    {
        turnSpeed = speed;
    }

    //プレイヤーのサイズ
    public void SetPlayerSize(Vector3 size)
    {
        playerSize = size;
    }

    //プレイヤーの声の大きさ
    public void SetVoiceVolume(float volume)
    {
        voiceVolume = volume;
    }

    //プレイヤーの押す力
    public void SetPlayerPushPower(float power)
    {
        playerPushPower = power;
    }

    //プレイヤーの迷惑度
    public void SetPlayerNuisance(float nuisance)
    {
        currentNuisance = nuisance;
    }

    //----------------------------------------------------------------------------------------
    //PlayerData(元パラメータSO)の値を取得する関数

    //移動速度
    public float GetMoveSpeed()
    {
        return moveSpeed;
    }

    //回転速度(普段の回転)
    public float GetTurnSpeed()
    {
        return turnSpeed;
    }

    //プレイヤーのサイズ
    public Vector3 GetPlayerSize()
    {
        return playerSize;
    }

    //プレイヤーの声の大きさ
    public float GetVoiceVolume()
    {
        return voiceVolume;
    }

    //プレイヤーの押す力
    public float GetPlayerPushPower()
    {
        return playerPushPower;
    }

    //プレイヤーの迷惑度
    public float GetPlayerNuisance()
    {
        return currentNuisance;
    }
}