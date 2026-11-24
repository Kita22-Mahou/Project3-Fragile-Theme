using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FinishLine : MonoBehaviour
{
    [Header("ตั้งค่าฉากจบ")]
    [SerializeField] private string endingSceneName = "Story Ending";
    [SerializeField] private float delayBeforeLoad = 0f;

    private bool hasWon;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("ชน Trigger แล้ว: " + other.name);

        if (hasWon)
            return;

        MouseController player = other.GetComponentInParent<MouseController>();

        // ถ้าอยากให้ทริกเกอร์ทำงานแม้สคริปต์ผู้เล่นไม่ได้ชื่อ MouseController ให้คงส่วนนี้ไว้
        hasWon = true;

        if (player != null)
        {
            player.movementLocked = true;
        }

        Rigidbody2D rb = other.GetComponentInParent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        StartCoroutine(LoadEndingScene());
    }

    private IEnumerator LoadEndingScene()
    {
        Time.timeScale = 1f;

        if (delayBeforeLoad > 0f)
            yield return new WaitForSecondsRealtime(delayBeforeLoad);

        if (!string.IsNullOrEmpty(endingSceneName))
        {
            Debug.Log("กำลังโหลดฉาก: " + endingSceneName);
            SceneManager.LoadScene(endingSceneName);
        }
    }

    public void PlayAgain()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }
}