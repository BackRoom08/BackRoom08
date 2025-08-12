using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ItemType
{ //스크립터블
  Consumable, //소비 아이템
  Other, //키 아이템 및 잡아이템
  Flash  //손전등
}

[CreateAssetMenu(fileName = "Item", menuName = "Scriptable Object/Items/Item")]
public class ItemDatas : ScriptableObject
{
    public int uid; //아이템 ID
    public ItemType type; //아이템 타입
    public Sprite icon; //아이템 아이콘
    public GameObject modelPrefab; //아이템 모델
    public string itemName; //아이템 이름
    [TextArea(3, 3)]
    public string description; //아이템의 설명

    protected virtual void Reset() 
    {
        type = ItemType.Other;
        icon = null;
        modelPrefab = null;
        itemName = "이름을 적어줘욤";
        description = "설명";
    }
}
