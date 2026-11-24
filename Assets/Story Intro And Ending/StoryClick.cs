using UnityEngine;
using UnityEngine.SceneManagement;

public class StoryClick : MonoBehaviour
{
    [System.Serializable]
    public class Step
    {
        [Tooltip("GameObjects ที่จะแสดงเมื่อคลิก Step นี้")]
        public GameObject[] show;

        [Tooltip("ติ๊กถูกถ้าต้องการเล่นเสียงข้อความขึ้น (SFX)")]
        public bool playTextSound = true;

        [Tooltip("เสียงพากย์ของข้อความใน Step นี้")]
        public AudioClip voiceOverClip;
    }

    public Step[] steps;

    [Header("เมื่อกดจบให้เข้าฉากนี้")]
    public string nextSceneAfterFinish = "co map test";

    [Header("ตั้งค่าเสียงข้อความ (SFX)")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip textAppearClip;
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [Header("ตั้งค่าเสียงพากย์")]
    [Tooltip("AudioSource สำหรับเสียงพากย์ แนะนำให้แยกจาก SFX")]
    [SerializeField] private AudioSource voiceOverSource;

    [Range(0f, 1f)]
    [SerializeField] private float voiceOverVolume = 1f;

    private int current = 0;
    private bool isFinished = false;

    private void Awake()
    {
        // ถ้าไม่ได้กำหนด AudioSource สำหรับเสียงพากย์ จะสร้างให้อัตโนมัติ
        if (voiceOverSource == null)
        {
            voiceOverSource = gameObject.AddComponent<AudioSource>();
        }

        voiceOverSource.playOnAwake = false;
        voiceOverSource.loop = false;
        voiceOverSource.spatialBlend = 0f; // เสียงพากย์เป็นเสียง 2D
    }

    private void Start()
    {
        if (steps == null) return;

        foreach (var s in steps)
        {
            if (s == null || s.show == null) continue;

            foreach (var g in s.show)
            {
                if (g != null)
                    g.SetActive(false);
            }
        }
    }

    private void Update()
    {
        if (isFinished) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (steps != null && current < steps.Length)
            {
                Step currentStep = steps[current];

                if (currentStep == null)
                {
                    current++;
                    return;
                }

                // แสดงข้อความ/ภาพของ Step นี้
                if (currentStep.show != null)
                {
                    foreach (var g in currentStep.show)
                    {
                        if (g != null)
                            g.SetActive(true);
                    }
                }

                // เสียงคลิก/เสียงข้อความขึ้น (SFX)
                if (currentStep.playTextSound)
                {
                    PlayTextSound();
                }

                // เสียงพากย์ที่ตรงกับข้อความของ Step นี้
                PlayVoiceOver(currentStep.voiceOverClip);

                current++;
            }
            else
            {
                isFinished = true;

                if (voiceOverSource != null)
                    voiceOverSource.Stop();

                SceneManager.LoadScene(nextSceneAfterFinish);
            }
        }
    }

    private void PlayTextSound()
    {
        if (textAppearClip == null) return;

        if (sfxSource != null)
        {
            sfxSource.PlayOneShot(textAppearClip, sfxVolume);
        }
        else
        {
            Vector3 pos = Camera.main != null
                ? Camera.main.transform.position
                : Vector3.zero;

            AudioSource.PlayClipAtPoint(textAppearClip, pos, sfxVolume);
        }
    }

    private void PlayVoiceOver(AudioClip clip)
    {
        if (voiceOverSource == null) return;

        // หยุดเสียงพากย์ก่อนหน้าเสมอ เพื่อไม่ให้เสียงหลาย Step ซ้อนกัน
        voiceOverSource.Stop();

        if (clip == null) return;

        voiceOverSource.clip = clip;
        voiceOverSource.volume = voiceOverVolume;
        voiceOverSource.Play();
    }
}
