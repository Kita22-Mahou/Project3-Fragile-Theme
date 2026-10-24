using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SlowObstacle : MonoBehaviour
{
    [Tooltip("ความเร็วเดินที่เหลือ เช่น 0.4 = เดินได้ 40%")]
    [SerializeField] private float movementMultiplier = 0.4f;

    [Tooltip("ความเร็วพุ่งที่เหลือ เช่น 0.55 = แรงพุ่งเหลือ 55%")]
    [SerializeField] private float jumpMultiplier = 0.55f;

    public float MovementMultiplier => movementMultiplier;
    public float JumpMultiplier => jumpMultiplier;

    private readonly HashSet<MouseController> playersInside = new HashSet<MouseController>();


    private void Reset()
    {
        Collider2D obstacleCollider = GetComponent<Collider2D>();

        if (obstacleCollider != null)
        {
            obstacleCollider.isTrigger = true;
        }
    }


    private void Awake()
    {
        Collider2D obstacleCollider = GetComponent<Collider2D>();

        if (obstacleCollider != null)
        {
            obstacleCollider.isTrigger = true;
        }
    }


    private void OnTriggerEnter2D(Collider2D other)
    {
        MouseController player = other.GetComponentInParent<MouseController>();

        if (player == null || !playersInside.Add(player))
        {
            return;
        }

        player.EnterSlowObstacle(this);
    }


    private void OnTriggerExit2D(Collider2D other)
    {
        MouseController player = other.GetComponentInParent<MouseController>();

        if (player == null || !playersInside.Remove(player))
        {
            return;
        }

        player.ExitSlowObstacle(this);
    }


    private void OnDisable()
    {
        foreach (MouseController player in playersInside)
        {
            if (player != null)
            {
                player.ExitSlowObstacle(this);
            }
        }

        playersInside.Clear();
    }
}