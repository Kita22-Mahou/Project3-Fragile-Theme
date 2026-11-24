using UnityEngine;
using System.Collections.Generic;

public class StoryClick : MonoBehaviour
{
    // แต่ละคลิก = รายการวัตถุที่จะโผล่พร้อมกัน
    [System.Serializable]
    public class Step
    {
        public GameObject[] show;
    }

    public Step[] steps;

    int current = 0;

    void Start()
    {
        // ซ่อนทุกอย่างก่อน
        foreach (var s in steps)
            foreach (var g in s.show)
                g.SetActive(false);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (current < steps.Length)
            {
                foreach (var g in steps[current].show)
                    g.SetActive(true);
                current++;
            }
            else
            {
                Debug.Log("Story จบแล้ว"); // ขั้นต่อไปค่อยใส่ให้เข้าเกม
            }
        }
    }
}