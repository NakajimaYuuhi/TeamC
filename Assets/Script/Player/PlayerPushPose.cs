using UnityEngine;
using UnityEngine.InputSystem;

//押す入力で、モデルの両腕を前へ突き出し、押す音を鳴らす。プレイヤーのいちばん上に NPCLook と一緒に付ける
public class PlayerPushPose : MonoBehaviour
{
    [Tooltip("押すときの音")]
    [SerializeField] AudioClip pushSound;
    [Tooltip("押す音の大きさ")]
    [SerializeField, Range(0f, 1f)] float pushVolume = 0.9f;

    NPCLook look;

    void Awake()
    {
        look = GetComponent<NPCLook>();
    }

    //PlayerInput から PlayerMove と同じく届く
    void OnPush(InputValue value)
    {
        if (!value.isPressed) return;
        SceneAudioController.Instance?.PlayStep(pushSound, pushVolume);
        if (look == null || look.Swing == null) return;
        look.Swing.Push();
    }
}
