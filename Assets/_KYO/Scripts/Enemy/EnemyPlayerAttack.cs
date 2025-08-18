using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// 이 스크립트는 다른 Enemy AI 스크립트에서 호출하여 사용합니다.

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

    // 내부 참조
    private Animator animator;
    private NavMeshAgent agent;
    private MonoBehaviour mainAiScript; // 적의 주 AI 스크립트 (예: EnemySmilerMob)

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

    // 외부(AI 스크립트)에서 이 함수를 호출하여 공격을 시작합니다.
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
        // 1. 플레이어 조작 비활성화
        playerObject.GetComponent<PlayerMove>().enabled = false;
        playerObject.GetComponent<ItemPickUp>().enabled = false;
        var cameraScript = playerObject.GetComponentInChildren<NewBehaviourScript>();
        if (cameraScript != null) cameraScript.enabled = false;

        // Cinemachine Brain 비활성화 (카메라 제어권 확보)
        var cinemachineBrain = Camera.main.GetComponent<Cinemachine.CinemachineBrain>();
        if (cinemachineBrain != null)
        {
            cinemachineBrain.enabled = false;
        }

        // 2. AI 비활성화 (isStopped 대신 enabled = false로 확실하게 제어권 전환)
        agent.enabled = false; 
        if (mainAiScript != null) mainAiScript.enabled = false;

        // 3. 적을 플레이어 눈 앞에 위치시키기
        Transform playerCamera = Camera.main.transform;
        
        transform.rotation = Quaternion.LookRotation(playerCamera.position - transform.position);
        
        // X, Z축 회전을 0으로 강제하여 기울어지지 않게 함
        Vector3 currentSmilerEuler = transform.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(0, currentSmilerEuler.y, 0);

        // 4. 플레이어의 몸통을 회전시켜 적을 바라보게 함 (즉시 회전)
        Transform playerBodyTransform = playerObject.GetComponentInChildren<NewBehaviourScript>().Playerbody; // Playerbody Transform 가져오기
        
        Vector3 lookDirectionBody = transform.position - playerBodyTransform.position;
        lookDirectionBody.y = 0; // Y축 회전만 고려 (수평 회전)
        Quaternion targetBodyRotation = Quaternion.LookRotation(lookDirectionBody);

        playerBodyTransform.rotation = targetBodyRotation; // 즉시 목표 각도로 회전

        // 카메라의 상하 시선(X축 회전)을 중립으로 초기화하여 정면을 바라보게 함
        Camera.main.transform.localRotation = Quaternion.Euler(0, 0, 0);

        // 5. 공격 애니메이션 실행
        animator.SetTrigger(attackTriggerName);

        // 6. 게임 오버 처리 (애니메이션 시간만큼 대기)
        yield return new WaitForSeconds(attackAnimationDuration);

        Debug.Log("게임 오버!");
        if (!string.IsNullOrEmpty(gameOverSceneName))
        {
            SceneManager.LoadScene(gameOverSceneName);
        }

        // Cinemachine Brain 다시 활성화 (선택 사항, 게임 오버 후 씬 전환 시 필요 없음)
        if (cinemachineBrain != null)
        {
            cinemachineBrain.enabled = true;
        }
    }
}