using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Image))]
public class UIFragmentDissolveController : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField]
    private float duration = 0.6f;

    [SerializeField]
    private AnimationCurve animationCurve =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Debug")]
    [SerializeField]
    private bool enableDebugKey = true;

    [SerializeField]
    private Key debugKey = Key.N;

    private Image targetImage;
    private Material runtimeMaterial;
    private Material originalMaterial;
    private Coroutine animationCoroutine;

    private float currentProgress = 0.0f;
    private bool isVisible = true;

    private static readonly int ProgressID =
        Shader.PropertyToID("_Progress");

    private void Awake()
    {
        targetImage = GetComponent<Image>();

        originalMaterial = targetImage.material;

        if (originalMaterial == null ||
            originalMaterial.shader == null ||
            originalMaterial.shader.name != "UI/FragmentDissolve")
        {
            Debug.LogError(
                "[FragmentDissolve] UI/FragmentDissolveÇÃMaterialÇê›íËÇµÇƒÇ≠ÇæÇ≥Ç¢ÅB",
                this
            );

            enabled = false;
            return;
        }

        // å¬ï MaterialÇê∂ê¨
        runtimeMaterial = new Material(originalMaterial);

        runtimeMaterial.name =
            originalMaterial.name + "_Runtime";

        targetImage.material = runtimeMaterial;

        SetProgress(0.0f);
    }

    private void Update()
    {
        if (!enableDebugKey)
            return;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current[debugKey].wasPressedThisFrame)
        {
            if (isVisible)
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
        isVisible = false;
        StartAnimation(1.0f);
    }

    public void PlayAppear()
    {
        isVisible = true;
        StartAnimation(0.0f);
    }

    private void StartAnimation(float targetProgress)
    {
        if (runtimeMaterial == null)
            return;

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        animationCoroutine = StartCoroutine(
            Animate(currentProgress, targetProgress)
        );
    }

    private IEnumerator Animate(float start, float end)
    {
        float elapsed = 0.0f;

        float animationDuration =
            Mathf.Max(0.0001f, duration);

        while (elapsed < animationDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(elapsed / animationDuration);

            float curvedT =
                animationCurve.Evaluate(t);

            float progress =
                Mathf.Lerp(start, end, curvedT);

            SetProgress(progress);

            yield return null;
        }

        SetProgress(end);

        animationCoroutine = null;
    }

    private void SetProgress(float progress)
    {
        currentProgress = Mathf.Clamp01(progress);

        if (runtimeMaterial == null)
            return;

        runtimeMaterial.SetFloat(
            ProgressID,
            currentProgress
        );
    }

    private void OnDestroy()
    {
        if (runtimeMaterial == null)
            return;

        if (targetImage != null)
        {
            targetImage.material = originalMaterial;
        }

        Destroy(runtimeMaterial);
    }
}