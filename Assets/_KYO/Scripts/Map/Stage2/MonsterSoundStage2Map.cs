using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MonsterSoundStage2Map : MonoBehaviour
{//맵에 일정주기로 계속 몬스터소리가 나게 할것.
    public AudioClip[] monsterSounds;
    public float MinInterval = 3f;
    public float MaxInterval = 6f;

    private AudioSource audioSource;

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f; // 2D 사운드로 전맵에 들리게 설정
        audioSource.playOnAwake = false;
        if (UIManager.Instance != null && UIManager.Instance.bgmGroup != null)
        {
            audioSource.outputAudioMixerGroup = UIManager.Instance.bgmGroup;
        }

        StartCoroutine(PlayMonsterSoundsLoop());

    }
    IEnumerator PlayMonsterSoundsLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(MinInterval, MaxInterval));

            if (monsterSounds.Length > 0)
            {
                int index = Random.Range(0, monsterSounds.Length);
                AudioClip selectedClip = monsterSounds[index];
                audioSource.PlayOneShot(selectedClip);
            }
        }
    }

}
