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
    private void OnTriggerEnter(Collider other) //임시 조건 지금은 그냥 부닥치면
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
        MapManager mapManager = FindObjectOfType<MapManager>();
        if (mapManager == null)
        {
            Debug.LogError("Scene에 MapManager가 없습니다!");
            yield break; // MapManager가 없으면 코루틴 중단
        }

        // 페이드 아웃
        mapManager.FadeOut(0.1f);
        yield return new WaitForSeconds(0.1f);

        // 플레이어 조작 비활성화
        playerObject.GetComponent<PlayerMove>().enabled = false;
        playerObject.GetComponent<ItemPickUp>().enabled = false;
        
        var cameraScript = playerObject.GetComponentInChildren<NewBehaviourScript>();
        if (cameraScript != null) cameraScript.enabled = false;

        // AI 비활성화
        agent.enabled = false; 
        if (mainAiScript != null) mainAiScript.enabled = false;

        // 데스룸으로 순간이동 , 페이드 인
        transform.position = mapManager.enemyDeadRoomPoints.position;
        playerObject.transform.position = mapManager.playerDeadRoomPoint.position;

        transform.LookAt(playerObject.transform);
        // 플레이어가 적을 수평으로만 바라보도록 수정
        Vector3 directionToEnemy = transform.position - playerObject.transform.position;
        directionToEnemy.y = 0; // Y축 값을 0으로 만들어 수평 방향으로 고정
        if (directionToEnemy != Vector3.zero) // 0 벡터가 아닐 때만 회전 적용 (오류 방지)
        {
            playerObject.transform.rotation = Quaternion.LookRotation(directionToEnemy);
        }

        mapManager.FadeIn(1.5f);

        // 애니메이션 실행
        yield return new WaitForSeconds(2f);
        animator.SetTrigger(attackTriggerName);

        //  애니메이션 시간만큼 대기
        yield return new WaitForSeconds(attackAnimationDuration);

        UIManager.Instance.ShowDeathUI();
        Debug.Log("게임 오버 부분 붙여서 넣기 !");
    

    }
}