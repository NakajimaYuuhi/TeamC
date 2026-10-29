using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class PlayerStepFeedback : MonoBehaviour
{
    [SerializeField, InspectorName("ステップ音")]
    [Tooltip("横ステップ開始時に一度だけ再生する音です。")]
    private AudioClip stepSound;

    [SerializeField, Range(0f, 1f), InspectorName("ステップ音量")]
    private float stepVolume = 0.95f;

    [SerializeField, InspectorName("砂埃マテリアル")]
    [Tooltip("足元に出すParticleSystem用のマテリアルです。")]
    private Material dustMaterial;

    private Collider bodyCollider;
    private ParticleSystem dust;

    public Vector3 FootPosition
    {
        get
        {
            Bounds bounds = bodyCollider.bounds;
            return new Vector3(bounds.center.x, bounds.min.y + 0.05f, bounds.center.z);
        }
    }

    private void Awake()
    {
        bodyCollider = GetComponent<Collider>();

        var dustObject = new GameObject("ステップ砂埃");
        dustObject.transform.SetParent(transform, false);
        dust = dustObject.AddComponent<ParticleSystem>();
        dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = dust.main;
        main.duration = 0.55f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.3f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 48;

        var emission = dust.emission;
        emission.enabled = false;

        var shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.18f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        var velocity = dust.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.y = 0.25f;

        var size = dust.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 1.2f));

        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.68f, 0.59f, 0.45f), 0f),
                new GradientColorKey(new Color(0.58f, 0.52f, 0.43f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.6f, 0.08f),
                new GradientAlphaKey(0.3f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        var color = dust.colorOverLifetime;
        color.enabled = true;
        color.color = gradient;

        var renderer = dust.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = dustMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    public void Play(Vector3 footPosition)
    {
        dust.transform.position = footPosition;
        dust.Play();
        dust.Emit(12);
        SceneAudioController.Instance?.PlayStep(stepSound, stepVolume);
    }

    private void OnDisable()
    {
        if (dust != null) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
