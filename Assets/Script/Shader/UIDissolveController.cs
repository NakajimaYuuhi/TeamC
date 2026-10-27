using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UIDissolveController : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField]
    private float duration = 0.5f;

    [SerializeField]
    private AnimationCurve animationCurve =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Debug")]
    [SerializeField]
    private bool enableDebugKey = true;

    [SerializeField]
    private KeyCode debugKey = KeyCode.N;

    private Image targetImage;
    private Material runtimeMaterial;
    private Coroutine animationCoroutine;

    private float currentProgress = 0.0f;

    private static readonly int ProgressID =
        Shader.PropertyToID("_Progress");

    private void Awake()
    {
        targetImage = GetComponent<Image>();

        // 元のマテリアルを共有せず、個別のインスタンスを生成
        Material sourceMaterial = targetImage.material;

        runtimeMaterial = new Material(sourceMaterial);

        targetImage.material = runtimeMaterial;

        SetProgress(0.0f);
        //runtimeMaterial.SetFloat(ProgressID, 0.0f);
    }

    private void Update()
    {
        if (!enableDebugKey)
            return;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.nKey.wasPressedThisFrame)
        {
            Debug.Log("[UIDissolve] Nキー入力検出");

            if (currentProgress < 0.5f)
            {
                PlayDisappear();
            }
            else
            {
                PlayAppear();
            }
        }
    }

    public void PlayDisappear()
    {
        StartAnimation(0.0f, 1.0f);
    }

    public void PlayAppear()
    {
        StartAnimation(1.0f, 0.0f);
    }

    private void StartAnimation(float start, float end)
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        gameObject.SetActive(true);

        animationCoroutine =
            StartCoroutine(Animate(start, end));
    }

    private IEnumerator Animate(float start, float end)
    {
        float elapsed = 0.0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            float curveT = animationCurve.Evaluate(t);

            float progress = Mathf.Lerp(start, end, curveT);

            //runtimeMaterial.SetFloat(ProgressID, progress);
            SetProgress(progress);

            yield return null;
        }

        //runtimeMaterial.SetFloat(ProgressID, end);
        SetProgress(end);

        animationCoroutine = null;
    }
    private void SetProgress(float progress)
    {
        currentProgress = Mathf.Clamp01(progress);

        if (runtimeMaterial == null)
            return;

        runtimeMaterial.SetFloat(ProgressID, currentProgress);
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }
}