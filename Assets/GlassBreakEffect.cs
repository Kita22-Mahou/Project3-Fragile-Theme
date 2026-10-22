
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class GlassBreakEffect : MonoBehaviour
{
    [Header("Shard Shape")]
    [Tooltip("จำนวนการแบ่งสามเหลี่ยม ยิ่งมากยิ่งมีเศษเล็ก")]
    [Range(1, 8)]
    [SerializeField] private int subdivisionsPerTriangle = 4;

    [Header("Burst")]
    [SerializeField] private float minBurstSpeed = 160f;
    [SerializeField] private float maxBurstSpeed = 320f;
    [SerializeField] private float upwardBias = 0.2f;

    [Header("Physics")]
    [SerializeField] private float shardGravity = 350f;
    [SerializeField] private float minRotationSpeed = 300f;
    [SerializeField] private float maxRotationSpeed = 900f;

    [Header("Lifetime")]
    [SerializeField] private float effectDuration = 0.7f;
    [Range(0f, 1f)]
    [SerializeField] private float fadeStartPercent = 0.35f;

    private class ShardState
    {
        public RectTransform rect;
        public GlassShard graphic;
        public Vector2 velocity;
        public float rotationSpeed;
        public Color startColor;
    }

    private struct SpritePoint
    {
        public Vector2 position;
        public Vector2 uv;

        public SpritePoint(Vector2 position, Vector2 uv)
        {
            this.position = position;
            this.uv = uv;
        }
    }

    public void Break(Image sourceImage)
    {
        if (sourceImage == null || sourceImage.sprite == null)
            return;

        RectTransform sourceRect = sourceImage.rectTransform;

        if (sourceRect.parent == null)
            return;

        Sprite sprite = sourceImage.sprite;

        RectTransform root = new GameObject(
            "GlassBreak",
            typeof(RectTransform)
        ).GetComponent<RectTransform>();

        root.SetParent(sourceRect.parent, false);

        // จำลองตำแหน่งและขนาดของรูปเดิม
        root.anchorMin = sourceRect.anchorMin;
        root.anchorMax = sourceRect.anchorMax;
        root.pivot = sourceRect.pivot;
        root.sizeDelta = sourceRect.sizeDelta;
        root.anchoredPosition3D = sourceRect.anchoredPosition3D;
        root.localRotation = sourceRect.localRotation;
        root.localScale = sourceRect.localScale;
        root.SetAsLastSibling();

        Vector2 displaySize = sourceRect.rect.size;
        Vector2 pivot = sourceRect.pivot;
        Bounds bounds = sprite.bounds;

        Vector2[] spriteVertices = sprite.vertices;
        Vector2[] spriteUVs = sprite.uv;
        ushort[] triangles = sprite.triangles;

        if (triangles == null || triangles.Length < 3)
        {
            Destroy(root.gameObject);
            return;
        }

        List<ShardState> shards = new List<ShardState>();

        int divisions = Mathf.Clamp(
            subdivisionsPerTriangle,
            1,
            8
        );

        // แบ่งสามเหลี่ยมทุกอันของ Sprite เป็นเศษชิ้นเล็ก
        for (int i = 0; i < triangles.Length; i += 3)
        {
            SpritePoint a = new SpritePoint(
                spriteVertices[triangles[i]],
                spriteUVs[triangles[i]]
            );

            SpritePoint b = new SpritePoint(
                spriteVertices[triangles[i + 1]],
                spriteUVs[triangles[i + 1]]
            );

            SpritePoint c = new SpritePoint(
                spriteVertices[triangles[i + 2]],
                spriteUVs[triangles[i + 2]]
            );

            SubdivideTriangle(
                a, b, c,
                divisions,
                sprite,
                root,
                displaySize,
                pivot,
                bounds,
                sourceImage.color,
                shards
            );
        }

        StartCoroutine(AnimateShards(root, shards));
    }

    private SpritePoint Interpolate(
        SpritePoint a,
        SpritePoint b,
        SpritePoint c,
        float u,
        float v)
    {
        return new SpritePoint(
            a.position +
                (b.position - a.position) * u +
                (c.position - a.position) * v,
            a.uv +
                (b.uv - a.uv) * u +
                (c.uv - a.uv) * v
        );
    }

    private void SubdivideTriangle(
        SpritePoint a,
        SpritePoint b,
        SpritePoint c,
        int divisions,
        Sprite sprite,
        RectTransform root,
        Vector2 displaySize,
        Vector2 pivot,
        Bounds bounds,
        Color tint,
        List<ShardState> shards)
    {
        for (int i = 0; i < divisions; i++)
        {
            for (int j = 0; j < divisions - i; j++)
            {
                SpritePoint p0 = Interpolate(
                    a, b, c,
                    (float)i / divisions,
                    (float)j / divisions
                );

                SpritePoint p1 = Interpolate(
                    a, b, c,
                    (float)(i + 1) / divisions,
                    (float)j / divisions
                );

                SpritePoint p2 = Interpolate(
                    a, b, c,
                    (float)i / divisions,
                    (float)(j + 1) / divisions
                );

                CreateShard(
                    p0, p1, p2,
                    sprite, root,
                    displaySize, pivot, bounds,
                    tint, shards
                );

                // สร้างสามเหลี่ยมชิ้นที่สองของช่อง
                if (i + j < divisions - 1)
                {
                    SpritePoint p3 = Interpolate(
                        a, b, c,
                        (float)(i + 1) / divisions,
                        (float)(j + 1) / divisions
                    );

                    CreateShard(
                        p1, p3, p2,
                        sprite, root,
                        displaySize, pivot, bounds,
                        tint, shards
                    );
                }
            }
        }
    }

    private Vector2 MapToUI(
        Vector2 spritePosition,
        Bounds bounds,
        Vector2 size,
        Vector2 pivot)
    {
        float x = bounds.size.x > 0f
            ? (spritePosition.x - bounds.min.x) /
                bounds.size.x * size.x
            : 0f;

        float y = bounds.size.y > 0f
            ? (spritePosition.y - bounds.min.y) /
                bounds.size.y * size.y
            : 0f;

        return new Vector2(
            x - size.x * pivot.x,
            y - size.y * pivot.y
        );
    }

    private void CreateShard(
        SpritePoint p0,
        SpritePoint p1,
        SpritePoint p2,
        Sprite sprite,
        RectTransform root,
        Vector2 displaySize,
        Vector2 pivot,
        Bounds bounds,
        Color tint,
        List<ShardState> shards)
    {
        Vector2 v0 = MapToUI(p0.position, bounds, displaySize, pivot);
        Vector2 v1 = MapToUI(p1.position, bounds, displaySize, pivot);
        Vector2 v2 = MapToUI(p2.position, bounds, displaySize, pivot);

        Vector2 center = (v0 + v1 + v2) / 3f;

        GameObject obj = new GameObject(
            "PizzaShard",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(GlassShard)
        );

        obj.transform.SetParent(root, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = displaySize;

        Vector2 parentCenterOffset = new Vector2(
            (0.5f - pivot.x) * displaySize.x,
            (0.5f - pivot.y) * displaySize.y
        );

        rect.anchoredPosition = center - parentCenterOffset;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        GlassShard graphic = obj.GetComponent<GlassShard>();

        graphic.Initialize(
            sprite.texture,
            new Vector2[]
            {
                v0 - center,
                v1 - center,
                v2 - center
            },
            new Vector2[]
            {
                p0.uv,
                p1.uv,
                p2.uv
            },
            tint
        );

        // กระเด็นออกจากศูนย์กลาง พร้อมสุ่มทิศเล็กน้อย
        Vector2 direction = center - parentCenterOffset;

        if (direction.sqrMagnitude < 0.001f)
            direction = Random.insideUnitCircle;

        direction = direction.normalized
            + Random.insideUnitCircle * 0.45f
            + Vector2.up * upwardBias;

        direction.Normalize();

        float speed = Random.Range(
            minBurstSpeed,
            maxBurstSpeed
        );

        shards.Add(new ShardState
        {
            rect = rect,
            graphic = graphic,
            velocity = direction * speed,
            rotationSpeed = Random.Range(
                minRotationSpeed,
                maxRotationSpeed
            ) * (Random.value < 0.5f ? -1f : 1f),
            startColor = tint
        });
    }

    private IEnumerator AnimateShards(
        RectTransform root,
        List<ShardState> shards)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, effectDuration);
        float fadeStart = duration * fadeStartPercent;

        while (elapsed < duration)
        {
            float deltaTime = Time.deltaTime;
            elapsed += deltaTime;

            float fade = Mathf.InverseLerp(
                fadeStart,
                duration,
                elapsed
            );

            foreach (ShardState shard in shards)
            {
                if (shard.rect == null || shard.graphic == null)
                    continue;

                shard.rect.anchoredPosition +=
                    shard.velocity * deltaTime;

                shard.velocity +=
                    Vector2.down * shardGravity * deltaTime;

                shard.rect.Rotate(
                    0f,
                    0f,
                    shard.rotationSpeed * deltaTime
                );

                Color color = shard.startColor;
                color.a *= 1f - fade;
                shard.graphic.color = color;
            }

            yield return null;
        }

        if (root != null)
            Destroy(root.gameObject);
    }
}
