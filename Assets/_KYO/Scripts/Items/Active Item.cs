using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ActiveItem", menuName = "Scriptable Object/Items/ActiveItem")]
public class ActiveItem : ItemDatas
{ 
    

    protected override void Reset()
    {

        type = ItemType.Other;
        itemName = "이름을 적어줘욤";
        description = "상호작용이 필요한 아이템 분류용 데이터입니다.";
        

    }
}
