using UnityEngine;

public class WindLineVisual : MonoBehaviour
{
    [Header("References")]

    [SerializeField] private WindZone windZone;
    [SerializeField] private BoxCollider2D windCollider;


    [Header("Wind Waves")]

    [Range(4, 16)]
    [SerializeField] private int lineCount = 8;

    [SerializeField] private Vector2 lineLengthRange = new Vector2(0.7f, 1.4f);

    [SerializeField] private float visualSpeed = 4f;

    [SerializeField] private float waveAmplitude = 0.035f;

    [SerializeField] private float waveCycles = 1.5f;

    [SerializeField] private float waveAnimationSpeed = 1.5f;

    [Range(8, 24)]
    [SerializeField] private int pointsPerLine = 16;

    [Tooltip("ความเอียงของเส้น")]
    [SerializeField] private float diagonalHeight = 0.2f;

    [Header("Chalk Appearance")]

    [SerializeField] private Color chalkColor = new Color(0.78f, 0.88f, 0.96f, 1f);

    [Range(0.005f, 0.04f)]
    [SerializeField] private float lineWidth = 0.015f;

    [Range(0.1f, 1f)]
    [SerializeField] private float lineOpacity = 0.65f;

    [SerializeField] private int sortingOrder = 20;

    [Header("Fade")]

    [SerializeField] private float fadeSpeed = 4f;

    private class WindWave
    {
        public LineRenderer line;
        public float lane;
        public float length;
        public float phase;
        public float seed;
        public float slope;
        public float amplitude;
        public float cycles;
    }

    private WindWave[] waves;
    private Material lineMaterial;
    private float currentAlpha = 1f;
    private bool initialized;

    private void OnValidate()
    {
        lineCount = Mathf.Clamp(lineCount, 4, 16);
        pointsPerLine = Mathf.Clamp(pointsPerLine, 8, 24);
        lineWidth = Mathf.Clamp(lineWidth, 0.005f, 0.04f);
        waveAmplitude = Mathf.Clamp(waveAmplitude, 0.005f, 0.1f);
        waveCycles = Mathf.Max(0.5f, waveCycles);

        if (lineLengthRange.y < lineLengthRange.x)
        {
            lineLengthRange.y = lineLengthRange.x;
        }
    }


    private void Awake()
    {
        if (windZone == null)
        {
            windZone = GetComponentInParent<WindZone>();
        }

        if (windCollider == null && windZone != null)
        {
            windCollider = windZone.GetComponent<BoxCollider2D>();
        }

        if (windZone == null || windCollider == null)
        {
            Debug.LogError("WindLineVisual: ไม่พบ WindZone หรือ BoxCollider2D", this);
            enabled = false;
            return;
        }

        CreateMaterial();
        CreateWindWaves();

        initialized = true;
    }

    private void CreateMaterial()
    {
        Shader shader = Shader.Find("Sprites/Default");

        if (shader != null)
        {
            lineMaterial = new Material(shader);
        }
    }

    private void CreateWindWaves()
    {
        waves = new WindWave[lineCount];

        for (int i = 0; i < lineCount; i++)
        {
            WindWave wave = new WindWave();

            wave.lane = (i + Random.Range(0.25f, 0.75f)) / lineCount;
            wave.length = Random.Range(lineLengthRange.x, lineLengthRange.y);
            wave.phase = Random.value;
            wave.seed = Random.Range(0f, 1000f);
            wave.slope = Random.Range(-diagonalHeight, diagonalHeight);
            wave.amplitude = waveAmplitude * Random.Range(0.75f, 1.15f);
            wave.cycles = waveCycles * Random.Range(0.8f, 1.2f);

            wave.line = CreateLine("WindWave_" + i);

            waves[i] = wave;
        }
    }


    private LineRenderer CreateLine(string objectName)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();

        line.useWorldSpace = true;
        line.positionCount = pointsPerLine;
        line.loop = false;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 0;
        line.numCornerVertices = 0;
        line.widthMultiplier = lineWidth;
        line.sortingOrder = sortingOrder;

        // เรียวที่ปลายทั้งสองด้าน จึงไม่ดูเป็นก้อน
        line.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.05f),
            new Keyframe(0.15f, 0.7f),
            new Keyframe(0.5f, 1f),
            new Keyframe(0.85f, 0.7f),
            new Keyframe(1f, 0.05f)
        );

        if (lineMaterial != null)
        {
            line.sharedMaterial = lineMaterial;
        }

        return line;
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        float targetAlpha = windZone.IsWindStopped ? 0f : 1f;

        currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

        UpdateWindWaves();
    }

    private void UpdateWindWaves()
    {
        Bounds bounds = windCollider.bounds;

        float directionX = Mathf.Sign(windZone.CurrentWindDirection.x);

        if (Mathf.Approximately(directionX, 0f))
        {
            directionX = 1f;
        }

        float zoneWidth = Mathf.Max(0.1f, bounds.size.x);
        float zoneHeight = Mathf.Max(0.1f, bounds.size.y);

        float elapsed = Time.time * visualSpeed;

        foreach (WindWave wave in waves)
        {
            float laneY = bounds.min.y + zoneHeight * wave.lane;
            float travelRange = zoneWidth + wave.length;

            float travel = Mathf.Repeat(
                wave.phase * travelRange + elapsed,
                travelRange
            );

            float centerX = directionX > 0f
                ? bounds.min.x - wave.length * 0.5f + travel
                : bounds.max.x + wave.length * 0.5f - travel;

            float visibleAlpha = GetVisibility(centerX, wave.length, bounds);
            DrawWave(wave, centerX, laneY, elapsed, visibleAlpha);
        }
    }


    private void DrawWave(WindWave wave, float centerX, float centerY, float elapsed, float visibleAlpha)
    {
        LineRenderer line = wave.line;

        Color color = chalkColor;
        color.a = lineOpacity * currentAlpha * visibleAlpha;

        line.startColor = color;
        line.endColor = color;
        line.widthMultiplier = lineWidth;

        for (int i = 0; i < line.positionCount; i++)
        {
            float t = (float)i / (line.positionCount - 1);

            // วาดเส้นเฉียงเล็กน้อย
            float x = centerX + (t - 0.5f) * wave.length;
            float y = centerY + (t - 0.5f) * wave.slope;

            // คลื่นไซน์ขนาดเล็กและนุ่ม
            float waveOffset = Mathf.Sin(
                t * Mathf.PI * 2f * wave.cycles + elapsed * waveAnimationSpeed + wave.seed
            ) * wave.amplitude;

            // ความหยักละเอียดแบบเส้นชอล์ก
            float roughness = Mathf.Sin(t * 27f + wave.seed) * wave.amplitude * 0.06f;

            line.SetPosition(i, new Vector3(x, y + waveOffset + roughness, boundsZ()));
        }
    }


    private float GetVisibility(float centerX, float length, Bounds bounds)
    {
        float lineMin = centerX - length * 0.5f;
        float lineMax = centerX + length * 0.5f;

        float visibleMin = Mathf.Max(lineMin, bounds.min.x);
        float visibleMax = Mathf.Min(lineMax, bounds.max.x);

        float visibleLength = Mathf.Max(0f, visibleMax - visibleMin);

        return Mathf.Clamp01(visibleLength / Mathf.Max(0.01f, length));
    }


    private float boundsZ()
    {
        return transform.position.z;
    }

    // CLEANUP

    private void OnDestroy()
    {
        if (lineMaterial != null)
        {
            Destroy(lineMaterial);
        }
    }
}