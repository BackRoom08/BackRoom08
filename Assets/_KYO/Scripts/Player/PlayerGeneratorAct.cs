using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerGeneratorAct : MonoBehaviour
{
    public Camera cam;
    public float interactRange = 3f;
    public string generatorTag = "Generator"; // 태그 이름

    Generator currentGenerator;

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 씬이 로드된 후 오브젝트가 준비될 때까지 기다리는 코루틴 시작
        StartCoroutine(InitializeAfterSceneLoad());
    }
    IEnumerator InitializeAfterSceneLoad()
    {
        // MainCamera가 준비될 때까지 대기
        yield return new WaitUntil(() => GameObject.FindGameObjectWithTag("MainCamera") != null);
        cam = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
    }

    void Update()
    {
        if (cam == null) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
        {
            if (hit.collider.CompareTag(generatorTag))
            {
                Generator generator = hit.collider.GetComponent<Generator>();
                if (generator != null && !generator.IsCompleted)
                {
                    if (Input.GetKey(KeyCode.E))
                    {
                        if (currentGenerator != generator)
                        {
                            if (currentGenerator != null)
                                currentGenerator.EndWork(gameObject);

                            if (generator.TryBeginWork(gameObject))
                                currentGenerator = generator;
                        }
                    }
                    else
                    {
                        if (currentGenerator == generator)
                        {
                            currentGenerator.EndWork(gameObject);
                            currentGenerator = null;
                        }
                    }
                }
            }
        }
        else
        {
            // Generator에서 벗어나면 작업 중단
            if (currentGenerator != null)
            {
                currentGenerator.EndWork(gameObject);
                currentGenerator = null;
            }
        }
    }

}
