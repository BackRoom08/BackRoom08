using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;


[RequireComponent(typeof(NavMeshAgent))]
public class EnemyPlayerAttack : MonoBehaviour
{

    [Header("공격 연출 설정")]
    [Tooltip("공격 애니메이션 재생 후 게임오버까지 대기하는 시간")]
    public float attackAnimationDuration = 2f;

    [Tooltip("실행할 공격 애니메이션의 트리거 이름")]
    public string attackTriggerName = "Attack";

    [Header("게임 오버 설정")]
    [Tooltip("게임 오버 시 로드할 씬의 이름. 비워두면 씬을 로드하지 않음")]
    public string gameOverSceneName;

    private Animator animator;
    private NavMeshAgent agent;
    private MonoBehaviour mainAiScript; // 적의 주 AI 스크립트

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();

        // 이 스크립트를 제외한 다른 MonoBehaviour를 찾아서 AI 스크립트로 간주
        foreach (var script in GetComponents<MonoBehaviour>())
        {
            if (script != this)
            {
                mainAiScript = script;
                break;
            }
        }
    }
    private void OnTriggerEnter(Collider other) //임시로 조건 넣어둠
    {
        if (other.CompareTag("Player"))
        {
            InitiateAttack(other.gameObject);
        }
    }


    // 이 함수를 호출하여 공격을 시작
    public void InitiateAttack(GameObject playerObject)
    {
        // AI 스크립트가 활성화 상태일 때만 실행
        if (mainAiScript != null && mainAiScript.enabled)
        {
            StartCoroutine(AttackCoroutine(playerObject));
        }
    }


    private IEnumerator AttackCoroutine(GameObject playerObject)
    {
        // 페이드 아웃
        MapManager.Instance.FadeOut(1.5f);
        yield return new WaitForSeconds(1.5f);

        // 플레이어 조작 비활성화
        playerObject.GetComponent<PlayerMove>().enabled = false;
        playerObject.GetComponent<ItemPickUp>().enabled = false;
        
        var cameraScript = playerObject.GetComponentInChildren<NewBehaviourScript>();
        if (cameraScript != null) cameraScript.enabled = false;

        // Cinemachine Brain 비활성화 (카메라 제어권 확보)
        //var cinemachineBrain = Camera.main.GetComponent<Cinemachine.CinemachineBrain>();
        //if (cinemachineBrain != null)
        //{
        //    cinemachineBrain.enabled = false;
        //}

        // AI 비활성화
        agent.enabled = false; 
        if (mainAiScript != null) mainAiScript.enabled = false;

        // 데스룸으로 순간이동 , 페이드 인
        transform.position = MapManager.Instance.enemyDeadRoomPoints.position;
        playerObject.transform.position = MapManager.Instance.playerDeadRoomPoint.position;

        transform.LookAt(playerObject.transform);
        playerObject.transform.LookAt(transform);

        MapManager.Instance.FadeIn(1.5f);

        // 애니메이션 실행
        animator.SetTrigger(attackTriggerName);

        //  애니메이션 시간만큼 대기
        yield return new WaitForSeconds(attackAnimationDuration);

        Debug.Log("게임 오버!");
    

    }
}