using UnityEngine;
using UnityEngine.InputSystem;

//押す入力で、モデルの両腕を前へ突き出す。プレイヤーのいちばん上に NPCLook と一緒に付ける
public class PlayerPushPose : MonoBehaviour
{
    NPCLook look;

    void Awake()
    {
        look = GetComponent<NPCLook>();
    }

    //PlayerInput から PlayerMove と同じく届く
    void OnPush(InputValue value)
    {
        if (!value.isPressed || look == null || look.Swing == null) return;
        look.Swing.Push();
    }
}
