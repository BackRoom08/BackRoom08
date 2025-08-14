using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;


public class CutScene1 : MonoBehaviour
{
    private PlayableDirector pd;
    public TimelineAsset[] ta;

    // Start is called before the first frame update
    void Start()
    {
        pd = gameObject.GetComponent<PlayableDirector>();

        if (pd == null)
        {
            print("PlayableDirector 없음");
        }
        else
        {
            pd.stopped += OnCutsceneStopped;
        }
    }

    private void OnDisable() //비활성화 할때 이벤트 해제
    {
        if (pd != null)
        {
            pd.stopped -= OnCutsceneStopped;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        print("뭔가 들어옴");
        if (other.CompareTag("Player"))
        {
            print("Player 들어옴");
            pd.Play(ta[0]); //하나면 그냥 pd.Play()
                            //gameObject.SetActive(false);
                            //오디오추가하면 소리재생추가

        }
    }

    private void OnCutsceneStopped(PlayableDirector director) //타임라인이 재생끝나거나 중지되면 호출 할 메서드
    {
        print("타임라인 재생 완료 또는 중지됨. 오브젝트 비활성화.");
        gameObject.SetActive(false); // 타임라인 재생이 끝난 후 오브젝트를 비활성화
    }

}
