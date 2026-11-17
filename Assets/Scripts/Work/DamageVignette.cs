using UnityEngine;
using UnityEngine.UI;

// Red Vignette
// กะพริบแถบพิกเซลสีแดง 4 ด้านรอบขอบจอเมื่อโดนดาเมจ แล้วค่อย ๆ จางหายภายใน fadeDuration (0.5 วินาที)
//
// ไม่ต้องตั้งค่าอะไรใน Scene: HealthNew เรียก DamageVignette.Flash() แล้วสคริปนี้
// จะสร้าง Canvas และแถบทั้ง 4 ด้านให้เองตอนเรียกครั้งแรก
// (ถ้าอยากปรับค่าใน Inspector ให้สร้าง GameObject ว่างแล้วใส่สคริปนี้ไว้ล่วงหน้า)
public class DamageVignette : MonoBehaviour
{
    private static DamageVignette instance;

    [Header("Flash")]
    // เวลาที่แถบแดงจางหายจนหมด
    [SerializeField] private float fadeDuration = 0.5f;

    // ความเข้มสูงสุดตอนเริ่มกะพริบ (0-1)
    [Range(0f, 1f)]
    [SerializeField] private float peakAlpha = 0.75f;

    [SerializeField] private Color bandColor = new Color(0.9f, 0.1f, 0.1f, 1f);

    [Header("Pixel Bands")]
    // ความหนาของแถบแต่ละด้าน (หน่วยพิกเซลที่ความละเอียดอ้างอิง 1920x1080)
    [SerializeField] private float bandThickness = 140f;

    // จำนวนขั้นของแถบ (มาก = ไล่เฉดละเอียด, น้อย = เป็นพิกเซลหยาบ)
    [SerializeField] private int pixelSteps = 5;

    [Header("Canvas")]
    [SerializeField] private int sortingOrder = 500;

    private CanvasGroup group;
    private float timer;
    private bool playing;

    private readonly System.Collections.Generic.List<Object> createdAssets =
        new System.Collections.Generic.List<Object>();

    // เรียกจากที่ไหนก็ได้เมื่อต้องการให้จอแดงกะพริบ
    public static void Flash()
    {
        if (instance == null)
            instance = new GameObject("DamageVignette").AddComponent<DamageVignette>();

        instance.Play();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        fadeDuration = Mathf.Max(0.01f, fadeDuration);
        pixelSteps = Mathf.Max(1, pixelSteps);

        BuildUI();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        foreach (Object asset in createdAssets)
        {
            if (asset != null)
                Destroy(asset);
        }
    }

    private void Update()
    {
        if (!playing)
            return;

        // ใช้เวลาจริง เพื่อให้ยังจางหายตามปกติแม้เกมถูก Pause
        timer += Time.unscaledDeltaTime;

        float t = Mathf.Clamp01(timer / fadeDuration);
        group.alpha = peakAlpha * (1f - t);

        if (t >= 1f)
            playing = false;
    }

    private void Play()
    {
        timer = 0f;
        playing = true;
        group.alpha = peakAlpha;
    }

    private void BuildUI()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // CanvasGroup คุมความโปร่งใสของทั้ง 4 แถบพร้อมกัน และไม่บังการคลิก UI
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        // บน: เข้มที่ขอบบน / ล่าง: เข้มที่ขอบล่าง / ซ้าย / ขวา
        CreateBand("Top",
            MakeBandSprite(vertical: true, strongAtLowIndex: false),
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, bandThickness));

        CreateBand("Bottom",
            MakeBandSprite(vertical: true, strongAtLowIndex: true),
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, bandThickness));

        CreateBand("Left",
            MakeBandSprite(vertical: false, strongAtLowIndex: true),
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
            new Vector2(bandThickness, 0f));

        CreateBand("Right",
            MakeBandSprite(vertical: false, strongAtLowIndex: false),
            new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
            new Vector2(bandThickness, 0f));
    }

    private void CreateBand(
        string bandName,
        Sprite sprite,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 sizeDelta)
    {
        GameObject band = new GameObject(bandName, typeof(RectTransform), typeof(Image));
        band.transform.SetParent(transform, false);

        RectTransform rect = band.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = sizeDelta;

        Image image = band.GetComponent<Image>();
        image.sprite = sprite;
        image.color = bandColor;
        image.raycastTarget = false;
    }

    // สร้างพื้นผิวเล็ก ๆ ที่ไล่ความโปร่งใสเป็นขั้นแข็ง ๆ (Point filter) ให้ดูเป็นพิกเซล
    private Sprite MakeBandSprite(bool vertical, bool strongAtLowIndex)
    {
        int width = vertical ? 1 : pixelSteps;
        int height = vertical ? pixelSteps : 1;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int i = 0; i < pixelSteps; i++)
        {
            // 0 = ติดขอบจอ (เข้มสุด), pixelSteps-1 = ลึกเข้าไปในจอ (จางสุด)
            int distanceFromEdge = strongAtLowIndex ? i : pixelSteps - 1 - i;
            float alpha = 1f - (float)distanceFromEdge / pixelSteps;

            Color pixel = new Color(1f, 1f, 1f, alpha);

            if (vertical)
                texture.SetPixel(0, i, pixel);
            else
                texture.SetPixel(i, 0, pixel);
        }

        texture.Apply();

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            100f
        );

        createdAssets.Add(texture);
        createdAssets.Add(sprite);

        return sprite;
    }
}
