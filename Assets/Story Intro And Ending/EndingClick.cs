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

        [Header("เสียงพากย์ของ Step นี้")]
        [Tooltip("เสียงพากย์ที่จะเล่นพร้อมกับข้อความของ Step นี้")]
        public AudioClip voiceOverClip;
    }

    public Step[] steps;

    [Header("เมื่อกดจบให้เข้าฉากนี้")]
    public string nextSceneAfterFinish = "co map test";

    [Header("ตั้งค่าเสียงข้อความ")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip textAppearClip;
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [Header("ตั้งค่าเสียงพากย์")]
    [Tooltip("AudioSource สำหรับเสียงพากย์ ถ้าไม่กำหนดจะสร้างให้อัตโนมัติ")]
    [SerializeField] private AudioSource voiceOverSource;
    [Range(0f, 1f)]
    [SerializeField] private float voiceOverVolume = 1f;

    private int current = 0;
    private bool isFinished = false;

    private void Awake()
    {
        // แยก AudioSource ของเสียงพากย์ออกจากเสียง Text SFX
        // เพื่อให้เสียงพากย์สามารถหยุด/เปลี่ยนคลิปได้โดยไม่กระทบเสียงคลิกข้อความ
        if (voiceOverSource == null)
        {
            voiceOverSource = gameObject.AddComponent<AudioSource>();
        }

        voiceOverSource.playOnAwake = false;
        voiceOverSource.loop = false;
        voiceOverSource.spatialBlend = 0f; // เสียง 2D
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

                if (currentStep != null)
                {
                    if (currentStep.show != null)
                    {
                        foreach (var g in currentStep.show)
                        {
                            if (g != null)
                                g.SetActive(true);
                        }
                    }

                    if (currentStep.playTextSound)
                    {
                        PlayTextSound();
                    }

                    // หยุดเสียงพากย์ของ Step ก่อนหน้า แล้วเล่นเสียงของ Step ปัจจุบัน
                    PlayVoiceOver(currentStep.voiceOverClip);
                }
                else
                {
                    // Step ว่าง: หยุดเสียงพากย์เก่าเพื่อไม่ให้เสียงค้างข้ามช่วง
                    StopVoiceOver();
                }

                current++;
            }
            else
            {
                FinishEnding();
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

        // ทุกครั้งที่เปลี่ยน Step ให้หยุดเสียงเดิมก่อน
        voiceOverSource.Stop();
        voiceOverSource.clip = null;

        if (clip == null) return;

        voiceOverSource.clip = clip;
        voiceOverSource.volume = voiceOverVolume;
        voiceOverSource.Play();
    }

    private void StopVoiceOver()
    {
        if (voiceOverSource == null) return;

        voiceOverSource.Stop();
        voiceOverSource.clip = null;
    }

    private void FinishEnding()
    {
        isFinished = true;
        StopVoiceOver();

        if (sfxSource != null)
            sfxSource.Stop();

        SceneManager.LoadScene(nextSceneAfterFinish);
    }

    private void OnDisable()
    {
        StopVoiceOver();
    }
}
