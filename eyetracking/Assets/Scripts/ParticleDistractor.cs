using UnityEngine;

public class PeripheralParticleDistractor : MonoBehaviour
{
    [Header("AR Camera")]
    public Camera arCamera;

    [Header("Peripheral Position")]
    [Range(0.6f, 1.0f)]
    public float rightPeripheralX = 0.88f;
    [Range(0.0f, 0.4f)]
    public float leftPeripheralX = 0.12f;
    public float depthDistance = 3f;
    public bool bothSides = true;

    [Header("Particle Appearance")]
    public Texture2D particleTexture;
    public Color particleColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
    public float particleSize = 0.04f;

    [Header("Motion Settings")]
    public float lateralSpeed = -2.5f;
    public float depthSpeed = -1.5f;
    [Range(0f, 2f)]
    public float noiseStrength = 0.8f;

    [Header("Emission")]
    public float emissionRate = 150f;
    public int maxParticles = 800;
    public float lifetime = 4f;

    private ParticleSystem _rightPS;
    private ParticleSystem _leftPS;

    void Start()
    {
        if (arCamera == null)
            arCamera = Camera.main;

        _rightPS = CreateParticleSystem("RightPeripheralPS", rightPeripheralX, lateralSpeed);

        if (bothSides)
            _leftPS = CreateParticleSystem("LeftPeripheralPS", leftPeripheralX, -lateralSpeed);
    }

    void LateUpdate()
    {
        if (_rightPS != null)
            UpdateTransform(_rightPS.transform, rightPeripheralX);

        if (_leftPS != null)
            UpdateTransform(_leftPS.transform, leftPeripheralX);
    }

    private ParticleSystem CreateParticleSystem(string psName, float viewportX, float lateral)
    {
        GameObject go = new GameObject(psName);
        go.transform.SetParent(this.transform);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.5f, particleSize * 1.5f);
        main.startColor = particleColor;
        main.maxParticles = maxParticles;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = emissionRate;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.5f, 2.5f, 6f);
        shape.randomDirectionAmount = 0.1f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(lateral * 0.8f, lateral * 1.2f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        vel.z = new ParticleSystem.MinMaxCurve(depthSpeed * 0.8f, depthSpeed * 1.2f);

        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.5f, 0.7f),
            new Keyframe(1f, 0.1f)
        );
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOL = ps.colorOverLifetime;
        colorOL.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(particleColor, 0f),
                new GradientColorKey(particleColor, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f,    0f),
                new GradientAlphaKey(1f,    0.1f),
                new GradientAlphaKey(0.85f, 0.9f),
                new GradientAlphaKey(0f,    1f)
            }
        );
        colorOL.color = new ParticleSystem.MinMaxGradient(gradient);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = noiseStrength;
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.3f;
        noise.damping = true;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = 10;

        if (particleTexture != null)
        {
            Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
            mat.mainTexture = particleTexture;
            renderer.material = mat;
        }

        UpdateTransform(go.transform, viewportX);
        ps.Play();
        return ps;
    }

    private void UpdateTransform(Transform t, float viewportX)
    {
        Vector3 viewportPos = new Vector3(viewportX, 0.5f, depthDistance);
        Vector3 worldPos = arCamera.ViewportToWorldPoint(viewportPos);
        t.position = worldPos;
        t.rotation = arCamera.transform.rotation;
    }

    public void StartDistraction()
    {
        _rightPS?.Play();
        _leftPS?.Play();
    }

    public void StopDistraction()
    {
        _rightPS?.Stop();
        _leftPS?.Stop();
    }

    public void SetIntensity(float intensity)
    {
        intensity = Mathf.Clamp01(intensity);

        void Apply(ParticleSystem ps, float lateral)
        {
            if (ps == null) return;
            var e = ps.emission;
            e.rateOverTime = emissionRate * intensity;
            var v = ps.velocityOverLifetime;
            v.x = new ParticleSystem.MinMaxCurve(lateral * 0.8f * intensity, lateral * 1.2f * intensity);
            v.z = new ParticleSystem.MinMaxCurve(depthSpeed * 0.8f * intensity, depthSpeed * 1.2f * intensity);
        }

        Apply(_rightPS, lateralSpeed);
        Apply(_leftPS, -lateralSpeed);
    }

    void OnDestroy()
    {
        if (_rightPS != null) Destroy(_rightPS.gameObject);
        if (_leftPS != null) Destroy(_leftPS.gameObject);
    }
}