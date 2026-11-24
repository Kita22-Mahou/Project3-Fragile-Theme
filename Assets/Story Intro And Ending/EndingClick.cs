using UnityEngine;
using UnityEngine.SceneManagement;

public class EndingClick : MonoBehaviour
{
    [System.Serializable]
    public class Step
    {
        public GameObject[] show;
        [Tooltip("ติ๊กถูกถ้าคลิก Step นี้แล้วต้องการให้มีเสียงข้อความขึ้น")]
        public bool playTextSound = true;
    }

    public Step[] steps;

    [Header("เมื่อกดจบให้เข้าฉากนี้")]
    public string nextSceneAfterFinish = "co map test";

    [Header("ตั้งค่าเสียงข้อความ")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip textAppearClip;
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    private int current = 0;
    private bool isFinished = false;

    private void Start()
    {
        foreach (var s in steps)
        {
            if (s == null || s.show == null) continue;
            foreach (var g in s.show)
            {
                if (g != null) g.SetActive(false);
            }
        }
    }

    private void Update()
    {
        if (isFinished) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (current < steps.Length)
            {
                Step currentStep = steps[current];

                if (currentStep.show != null)
                {
                    foreach (var g in currentStep.show)
                    {
                        if (g != null) g.SetActive(true);
                    }
                }

                if (currentStep.playTextSound)
                {
                    PlaySound();
                }

                current++;
            }
            else
            {
                isFinished = true;
                SceneManager.LoadScene(nextSceneAfterFinish);
            }
        }
    }

    private void PlaySound()
    {
        if (textAppearClip == null) return;

        if (sfxSource != null)
        {
            sfxSource.PlayOneShot(textAppearClip, sfxVolume);
        }
        else
        {
            Vector3 pos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            AudioSource.PlayClipAtPoint(textAppearClip, pos, sfxVolume);
        }
    }
}