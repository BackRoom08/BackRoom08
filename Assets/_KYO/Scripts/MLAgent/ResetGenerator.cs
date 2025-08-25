using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResetGenerator : MonoBehaviour
{
    [Tooltip("검사 루트(비우면 이 오브젝트). 이 루트의 모든 자식에서 Generator를 찾습니다.")]
    public Transform root;

    [Tooltip("모두 완료된 뒤 리셋을 수행하기까지 지연(초). 0이면 즉시 리셋")]
    public float resetDelay = 0f;

    [Tooltip("문을 열기 위한 타임라인 트리거")]
    public TimeLineStarter doorTrigger;

    readonly List<Generator> _gens = new();
    int _stack;  // 완료된 개수(스택)

    void Awake()
    {
        if (!root) root = transform;
        CollectGenerators();
        SubscribeAll();
    }

    void OnDestroy()
    {
        UnsubscribeAll();
    }

    void OnTransformChildrenChanged()
    {
        // 자식이 바뀌면 다시 수집/구독
        UnsubscribeAll();
        CollectGenerators();
        SubscribeAll();
        _stack = 0;
    }

    void CollectGenerators()
    {
        _gens.Clear();
        _gens.AddRange(root.GetComponentsInChildren<Generator>(true));
    }

    void SubscribeAll()
    {
        foreach (var g in _gens)
            if (g != null) g.OnCompleted += HandleCompleted;
    }

    void UnsubscribeAll()
    {
        foreach (var g in _gens)
            if (g != null) g.OnCompleted -= HandleCompleted;
    }

    void HandleCompleted(Generator g)
    {
        _stack++;

        if (_stack >= _gens.Count)
        {
            // 문 열기
            if (doorTrigger != null)
            {
                doorTrigger.Interact();
            }

            // 리셋 예약
            Invoke(nameof(ResetAll), resetDelay);
        }

    }

    void ResetAll()
    {
        // 전체 리셋 + 스택 0
        //foreach (var g in _gens)
        //    if (g != null) g.ResetGenerator();
        //_stack = 0;
   
    }
}

