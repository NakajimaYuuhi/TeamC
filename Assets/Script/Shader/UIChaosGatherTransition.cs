using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(RectTransform))]
public class UIChaosGatherTransition : MonoBehaviour
{
    public static UIChaosGatherTransition Instance { get; private set; }

    public enum GatherDirection
    {
        Left,
        Right,
        Top,
        Bottom
    }

    [Header("Source")]
    [SerializeField]
    private Sprite sourceSprite;

    [SerializeField]
    private Color imageColor = Color.black;

    [Header("Direction")]
    [SerializeField]
    private GatherDirection direction = GatherDirection.Left;

    [Header("Generation")]
    [SerializeField, Min(1)]
    private int imageCount = 150;

    [SerializeField]
    private Vector2 imageSize = new Vector2(120, 180);

    [SerializeField]
    private Vector2 scaleRange = new Vector2(0.8f, 1.4f);

    [SerializeField, Range(0f, 180f)]
    private float rotationRandomness = 35f;

    [Header("Position")]
    [SerializeField, Range(0f, 1f)]
    private float positionRandomness = 0.8f;

    [SerializeField, Min(0f)]
    private float spawnDistance = 300f;

    [SerializeField, Min(0f)]
    private float pathCurveStrength = 100f;

    [Header("Animation")]
    [SerializeField, Min(0.01f)]
    private float duration = 1.5f;

    [SerializeField, Range(0f, 1f)]
    private float delayRandomness = 0.2f;

    [SerializeField]
    private AnimationCurve movementCurve =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Cover")]
    [SerializeField]
    private bool useCover = true;

    [SerializeField]
    private Color coverColor = Color.black;

    [SerializeField, Range(0f, 1f)]
    private float coverStartProgress = 0.85f;

    [Header("Debug")]
    [SerializeField]
    private bool enableDebugKey = true;

    [SerializeField]
    private Key debugKey = Key.N;

    private RectTransform rootRect;

    private RectTransform fragmentRoot;

    private Image coverImage;

    private readonly List<FragmentData> fragments = new();

    private float currentProgress;

    private float animationStartProgress;
    private float animationTargetProgress;
    private float animationElapsed;
    private float animationDuration;

    private bool isAnimating;
    private bool isGathered;

    private class FragmentData
    {
        public RectTransform rect;

        public Vector2 startPosition;
        public Vector2 targetPosition;
        public Vector2 curveOffset;

        public float startRotation;
        public float targetRotation;

        public float startScale;
        public float targetScale;

        public float delay;
        public float duration;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        rootRect = GetComponent<RectTransform>();

        Generate();

        SetProgress(0f);
    }

    private void Update()
    {
        if (enableDebugKey &&
            Keyboard.current != null &&
            Keyboard.current[debugKey].wasPressedThisFrame)
        {
            if (isGathered)
                PlayScatter();
            else
                PlayGather();
        }

        if (!isAnimating)
            return;

        animationElapsed += Time.unscaledDeltaTime;

        float t = Mathf.Clamp01(
            animationElapsed / animationDuration
        );

        float progress = Mathf.Lerp(
            animationStartProgress,
            animationTargetProgress,
            t
        );

        SetProgress(progress);

        if (t >= 1f)
        {
            isAnimating = false;
        }
    }

    public void LoadScene(string sceneName)
    {
        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    public System.Collections.IEnumerator LoadSceneCoroutine(string sceneName)
    {
        PlayGather();
        yield return StartCoroutine(UpdateAnimation());

        var result = SceneManager.LoadSceneAsync(sceneName);
        yield return result;

        PlayScatter();
    }

    private System.Collections.IEnumerator UpdateAnimation()
    {
        if (!isAnimating)
            yield break;

        float t = 0.0f;

        while(t < 1f)
        {
            t = Mathf.Clamp01(
                animationElapsed / animationDuration
            );
            animationElapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Lerp(
                animationStartProgress,
                animationTargetProgress,
                t
            );

            SetProgress(progress);

            isAnimating = false;
        
            yield return null;
        }

    }

    public void PlayGather()
    {
        isGathered = true;

        StartAnimation(1f);
    }

    public void PlayScatter()
    {
        isGathered = false;

        StartAnimation(0f);
    }

    private void StartAnimation(float target)
    {
        animationStartProgress = currentProgress;
        animationTargetProgress = target;

        animationElapsed = 0f;

        animationDuration = Mathf.Max(
            0.0001f,
            duration * Mathf.Abs(target - currentProgress)
        );

        isAnimating = true;
    }

    public void SetProgress(float progress)
    {
        currentProgress = Mathf.Clamp01(progress);

        UpdateFragments();

        UpdateCover();
    }

    private void Generate()
    {
        if (sourceSprite == null)
        {
            Debug.LogError(
                "[UIChaosTransition] Source Sprite is null.",
                this
            );

            enabled = false;
            return;
        }

        CreateCover();

        CreateFragmentRoot();

        GenerateFragments();
    }

    private void CreateCover()
    {
        GameObject obj = new GameObject(
            "CoverImage",
            typeof(RectTransform),
            typeof(Image)
        );

        obj.transform.SetParent(transform, false);

        RectTransform rect =
            obj.GetComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        coverImage = obj.GetComponent<Image>();

        coverImage.color = coverColor;
        coverImage.raycastTarget = false;
    }

    private void CreateFragmentRoot()
    {
        GameObject obj = new GameObject(
            "Fragments",
            typeof(RectTransform)
        );

        obj.transform.SetParent(transform, false);

        fragmentRoot = obj.GetComponent<RectTransform>();

        fragmentRoot.anchorMin = Vector2.zero;
        fragmentRoot.anchorMax = Vector2.one;

        fragmentRoot.offsetMin = Vector2.zero;
        fragmentRoot.offsetMax = Vector2.zero;
    }

    private void GenerateFragments()
    {
        fragments.Clear();

        Rect rect = rootRect.rect;

        int columns = Mathf.CeilToInt(
            Mathf.Sqrt(
                imageCount * rect.width /
                Mathf.Max(1f, rect.height)
            )
        );

        columns = Mathf.Max(1, columns);

        int rows = Mathf.CeilToInt(
            (float)imageCount / columns
        );

        float cellWidth = rect.width / columns;
        float cellHeight = rect.height / rows;

        for (int i = 0; i < imageCount; i++)
        {
            int x = i % columns;
            int y = i / columns;

            float normalizedX =
                (x + Random.value) / columns;

            float normalizedY =
                (y + Random.value) / rows;

            Vector2 target = new Vector2(
                Mathf.Lerp(
                    rect.xMin,
                    rect.xMax,
                    normalizedX
                ),
                Mathf.Lerp(
                    rect.yMin,
                    rect.yMax,
                    normalizedY
                )
            );

            target += new Vector2(
                Random.Range(
                    -cellWidth,
                    cellWidth
                ) * positionRandomness,

                Random.Range(
                    -cellHeight,
                    cellHeight
                ) * positionRandomness
            );

            FragmentData fragment =
                CreateFragment(i, target, rect);

            fragments.Add(fragment);
        }

        /// 集合方向に沿って並べ替える
        fragments.Sort((a, b) =>
        {
            float av = GetDirectionalValue(a.targetPosition);
            float bv = GetDirectionalValue(b.targetPosition);

            return av.CompareTo(bv);
        });

        // 集合順序に応じた遅延を設定
        for (int i = 0; i < fragments.Count; i++)
        {
            float normalizedIndex =
                fragments.Count <= 1
                    ? 0f
                    : (float)i / (fragments.Count - 1);

            FragmentData fragment = fragments[i];

            float randomDelay = Random.Range(
                -delayRandomness,
                delayRandomness
            );

            fragment.delay = Mathf.Clamp01(
                normalizedIndex * 0.65f +
                randomDelay * 0.25f
            );

            fragment.duration = Random.Range(
                0.25f,
                0.45f
            );

            // 後から集まるImageほど背面に配置する
            fragment.rect.SetSiblingIndex(0);
        }
    }

    private FragmentData CreateFragment(
        int index,
        Vector2 target,
        Rect screenRect)
    {
        GameObject obj = new GameObject(
            $"Fragment_{index}",
            typeof(RectTransform),
            typeof(Image)
        );

        obj.transform.SetParent(fragmentRoot, false);

        RectTransform rect =
            obj.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);

        rect.sizeDelta = imageSize;

        Image image = obj.GetComponent<Image>();

        image.sprite = sourceSprite;
        image.color = imageColor;
        image.raycastTarget = false;

        FragmentData data = new FragmentData();

        data.rect = rect;
        data.targetPosition = target;

        data.startPosition =
            GetSpawnPosition(screenRect);

        data.curveOffset = new Vector2(
            Random.Range(
                -pathCurveStrength,
                pathCurveStrength
            ),
            Random.Range(
                -pathCurveStrength,
                pathCurveStrength
            )
        );

        data.targetRotation = Random.Range(
            -rotationRandomness,
            rotationRandomness
        );

        data.startRotation =
            data.targetRotation +
            Random.Range(-90f, 90f);

        data.targetScale = Random.Range(
            scaleRange.x,
            scaleRange.y
        );

        data.startScale = data.targetScale * 0.5f;

        return data;
    }

    private Vector2 GetSpawnPosition(Rect rect)
    {
        switch (direction)
        {
            case GatherDirection.Left:
                return new Vector2(
                    rect.xMin - spawnDistance,
                    Random.Range(rect.yMin, rect.yMax)
                );

            case GatherDirection.Right:
                return new Vector2(
                    rect.xMax + spawnDistance,
                    Random.Range(rect.yMin, rect.yMax)
                );

            case GatherDirection.Top:
                return new Vector2(
                    Random.Range(rect.xMin, rect.xMax),
                    rect.yMax + spawnDistance
                );

            case GatherDirection.Bottom:
                return new Vector2(
                    Random.Range(rect.xMin, rect.xMax),
                    rect.yMin - spawnDistance
                );
        }

        return Vector2.zero;
    }

    private float GetDirectionalValue(Vector2 position)
    {
        switch (direction)
        {
            // 左側に集まる場合、右端（奥）から順番に配置
            case GatherDirection.Left:
                return -position.x;

            // 右側に集まる場合、左端（奥）から順番に配置
            case GatherDirection.Right:
                return position.x;

            // 上側に集まる場合、下端（奥）から順番に配置
            case GatherDirection.Top:
                return position.y;

            // 下側に集まる場合、上端（奥）から順番に配置
            case GatherDirection.Bottom:
                return -position.y;
        }

        return 0f;
    }

    private void UpdateFragments()
    {
        foreach (FragmentData fragment in fragments)
        {
            float t = Mathf.Clamp01(
                (currentProgress - fragment.delay) /
                Mathf.Max(0.0001f, fragment.duration)
            );

            float curvedT = movementCurve.Evaluate(t);

            Vector2 position = Vector2.LerpUnclamped(
                fragment.startPosition,
                fragment.targetPosition,
                curvedT
            );

            // 移動経路を湾曲させる
            position +=
                fragment.curveOffset *
                Mathf.Sin(t * Mathf.PI);

            fragment.rect.anchoredPosition = position;

            float rotation = Mathf.Lerp(
                fragment.startRotation,
                fragment.targetRotation,
                curvedT
            );

            fragment.rect.localRotation =
                Quaternion.Euler(0f, 0f, rotation);

            float scale = Mathf.Lerp(
                fragment.startScale,
                fragment.targetScale,
                curvedT
            );

            fragment.rect.localScale =
                new Vector3(scale, scale, 1f);

            // 移動開始前は非表示
            fragment.rect.gameObject.SetActive(
                currentProgress > fragment.delay
            );
        }
    }

    private void UpdateCover()
    {
        if (coverImage == null)
            return;

        if (!useCover)
        {
            coverImage.enabled = false;
            return;
        }

        coverImage.enabled = true;

        float alpha = Mathf.InverseLerp(
            coverStartProgress,
            1f,
            currentProgress
        );

        Color color = coverColor;
        color.a *= alpha;

        coverImage.color = color;
    }

    private void OnDestroy()
    {
        fragments.Clear();
    }
}