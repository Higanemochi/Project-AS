using UnityEngine;
using UnityEngine.UI;

/// <summary>개별 캐릭터 슬롯 - 상태 구독 및 UI 참조 보유</summary>
public class CharacterSlot : MonoBehaviour
{
    public LayoutElement layoutElement;
    public RectTransform containerRect;
    public Image image;
    public RectTransform imageRect;

    public void Initialize(LayoutElement layout, RectTransform container, Image img, RectTransform imgRect)
    {
        layoutElement = layout;
        containerRect = container;
        image = img;
        imageRect = imgRect;
    }

    public void SetSprite(Sprite sprite)
    {
        if (sprite != null)
            image.sprite = sprite;
    }
}
