using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IAIMonsterHearing
{
    void OnHearNoise(Vector3 pos, float percevied, NoiseEvent raw);
}
