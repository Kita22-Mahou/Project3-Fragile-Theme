using System.Collections.Generic;
using UnityEngine;

// Save Zone (โซนปลอดภัย)
// Tumi ป่วย: อยู่นอกโซนนี้จะเสีย HP ต่อเนื่อง (ตัวหัก HP อยู่ใน HealthNew)
// อยู่ในโซนนี้ = ไม่เสีย HP จากอาการป่วย
//
// วิธีใช้
// 1) สร้าง GameObject ว่าง แล้วใส่ SaveZone (จะเพิ่ม Collider2D ให้เอง)
// 2) ปรับขนาด/รูปร่างโซนด้วย Collider2D นั้น (เช่น BoxCollider2D)
// 3) วางโซนไว้รอบจุดเกิด / Checkpoint ได้ตามต้องการ
//
// ตรวจแบบเช็กว่า "จุดกึ่งกลางตัวละครอยู่ใน Collider ของโซนหรือไม่" โดยตรง
// จึงไม่ต้องพึ่ง OnTrigger และไม่พลาดตอนวาร์ปเกิดใหม่หรือตอน Rigidbody หลับ
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class SaveZone : MonoBehaviour
{
    private static readonly List<SaveZone> activeZones = new List<SaveZone>();

    [Header("Save Zone")]
    // ปิดชั่วคราวได้ เช่น โซนที่เปิดใช้ภายหลังจากสคริปต์อื่น
    [SerializeField] private bool zoneEnabled = true;

    [Header("Gizmo")]
    [SerializeField] private Color gizmoColor = new Color(0.2f, 1f, 0.4f, 0.25f);

    private Collider2D zoneCollider;

    public bool ZoneEnabled
    {
        get => zoneEnabled;
        set => zoneEnabled = value;
    }

    // ใช้บังคับให้เป็น Trigger ตอนเพิ่ม Component ใหม่ (ไม่ให้โซนกลายเป็นกำแพงตัน)
    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();

        if (col != null)
            col.isTrigger = true;
    }

    // เคลียร์ค่า static กรณีปิด Domain Reload ใน Enter Play Mode Options
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        activeZones.Clear();
    }

    private void Awake()
    {
        zoneCollider = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        if (!activeZones.Contains(this))
            activeZones.Add(this);
    }

    private void OnDisable()
    {
        activeZones.Remove(this);
    }

    // true = จุดนี้อยู่ใน SaveZone ใดโซนหนึ่งที่เปิดใช้งานอยู่
    public static bool IsPointInsideAnyZone(Vector2 point)
    {
        for (int i = activeZones.Count - 1; i >= 0; i--)
        {
            SaveZone zone = activeZones[i];

            if (zone == null)
            {
                activeZones.RemoveAt(i);
                continue;
            }

            if (zone.zoneEnabled &&
                zone.zoneCollider != null &&
                zone.zoneCollider.OverlapPoint(point))
            {
                return true;
            }
        }

        return false;
    }

    private void OnDrawGizmos()
    {
        Collider2D col = zoneCollider != null ? zoneCollider : GetComponent<Collider2D>();

        if (col == null)
            return;

        Bounds b = col.bounds;

        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(b.center, b.size);

        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
