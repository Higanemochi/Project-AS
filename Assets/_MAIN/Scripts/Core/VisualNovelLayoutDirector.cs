using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

public class VisualNovelLayoutDirector : MonoBehaviour
{
    // ========================= [Enums] =========================
    public enum EntranceType { Left, Right, BottomLeft, BottomRight, Center, Top }
    public enum ActionType { Jump, Shake, Nod, Punch, ShakeHorizontal }

    [Header("UI 연결")]
    public Transform characterPanel;
    public GameObject slotPrefab;

    [Header("설정")]
    public float charWidth = 350f;
    public float defaultDuration = 0.5f;
    public float moveDistance = 800f;



    // ========================= [1. 등장 (Entry)] =========================
    public void AddCharacter(string fileName, EntranceType type)
    {
        // 중복 방지
        if (FindSlot(fileName) != null)
        {
            Debug.LogWarning($"이미 존재하는 캐릭터입니다: {fileName}");
            return;
        }

        string path = "Images/Characters/" + fileName;
        Sprite loadedSprite = Resources.Load<Sprite>(path);

        if (loadedSprite != null)
        {
            StartCoroutine(SpawnRoutine(fileName, loadedSprite, type));
        }
        else
        {
            Debug.LogError($"이미지 로드 실패: {path}");
        }
    }

    private IEnumerator SpawnRoutine(string name, Sprite sprite, EntranceType type)
    {
        // 1. 슬롯 생성
        GameObject newSlot = Instantiate(slotPrefab, characterPanel);

        // 오브젝트 이름을 파일명(ID)으로 설정
        newSlot.name = name;

        LayoutElement layoutElement = newSlot.GetComponent<LayoutElement>();

        // [변경] MotionContainer 생성 및 계층 구조 변경
        // 기존: Slot -> Image
        // 변경: Slot -> MotionContainer -> Image
        GameObject motionContainer = new("MotionContainer");
        RectTransform containerRect = motionContainer.AddComponent<RectTransform>();
        motionContainer.transform.SetParent(newSlot.transform, false);

        // Container 설정 (부모 꽉 채우기)
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.sizeDelta = Vector2.zero;

        // 기존 Image를 Container 자식으로 이동
        Transform imageTransform = newSlot.transform.GetChild(0); // Prefab의 첫 번째 자식이 Image라고 가정
        imageTransform.SetParent(motionContainer.transform, false);

        Image charImage = imageTransform.GetComponent<Image>();

        // 2. 초기화
        charImage.sprite = sprite;
        charImage.SetNativeSize();
        layoutElement.preferredWidth = 0; layoutElement.minWidth = 0;

        // 3. 순서 재배치 (기존 로직 유지)
        int totalCount = characterPanel.childCount;
        switch (type)
        {
            case EntranceType.Left:
            case EntranceType.BottomLeft:
                newSlot.transform.SetSiblingIndex(0); break;
            case EntranceType.Right:
            case EntranceType.BottomRight:
                newSlot.transform.SetSiblingIndex(totalCount - 1); break;
            case EntranceType.Center:
            case EntranceType.Top:
                newSlot.transform.SetSiblingIndex((totalCount - 1) / 2); break;
        }

        // 4. 위치 잡기 및 애니메이션
        // [변경] 움직임은 MotionContainer가 담당
        Vector2 startPos = GetDirectionVector(type);
        containerRect.anchoredPosition = startPos; // Image -> Container
        charImage.color = new Color(1, 1, 1, 0);

        yield return new WaitForEndOfFrame();

        Tween.Custom(layoutElement, 0f, charWidth, defaultDuration, (t, x) => t.preferredWidth = x, Ease.OutQuart);
        Tween.UIAnchoredPosition(containerRect, Vector2.zero, defaultDuration, Ease.OutQuart); // Image -> Container
        Tween.Alpha(charImage, 1f, defaultDuration);
    }

    // ========================= [2. 퇴장 (Exit)] =========================
    public void RemoveCharacter(string characterName, EntranceType exitTo)
    {
        Transform targetSlot = FindSlot(characterName);

        if (targetSlot != null)
        {
            StartCoroutine(ExitRoutine(targetSlot, exitTo));
        }
        else
        {
            Debug.LogWarning($"삭제 실패: '{characterName}' 캐릭터를 찾을 수 없습니다.");
        }
    }

    private IEnumerator ExitRoutine(Transform slotTransform, EntranceType exitTo)
    {
        // 중복 호출 방지를 위해 이름을 바꿔둠 (빠르게 연타했을 때 에러 방지)
        slotTransform.name += "_Removing";

        LayoutElement layoutElement = slotTransform.GetComponent<LayoutElement>();

        // [변경] 계층 구조 반영
        Transform container = slotTransform.GetChild(0); // MotionContainer
        RectTransform containerRect = container.GetComponent<RectTransform>();
        Image charImage = container.GetChild(0).GetComponent<Image>(); // Image

        Vector2 targetPos = GetDirectionVector(exitTo);

        // 이미지 날리기 & 투명화
        // [변경] 움직임은 Container, 투명도는 Image
        Tween.UIAnchoredPosition(containerRect, targetPos, defaultDuration, Ease.OutQuart);
        Tween.Alpha(charImage, 0f, defaultDuration * 0.8f);

        // 공간 닫기
        yield return Tween.Custom(layoutElement, layoutElement.preferredWidth, 0f, defaultDuration,
            (t, x) => t.preferredWidth = x, Ease.OutQuart).ToYieldInstruction();

        Destroy(slotTransform.gameObject);
    }

    // ========================= [3. 액션 (Action)] =========================
    public void PlayAction(string characterName, ActionType action)
    {
        StartCoroutine(PlayActionRoutine(characterName, action));
    }

    private IEnumerator PlayActionRoutine(string characterName, ActionType action)
    {
        Transform targetSlot = FindSlot(characterName);

        if (targetSlot == null)
        {
            Debug.LogWarning($"액션 실패: '{characterName}' 캐릭터를 찾을 수 없습니다.");
            yield break;
        }

        // [변경] 계층 구조 반영: Slot -> Container -> Image
        // 액션은 Image에만 적용 (Container는 이동 담당)
        RectTransform targetImageRect = targetSlot.GetChild(0).GetChild(0).GetComponent<RectTransform>();

        // 기존 애니메이션 정지 및 초기화
        Tween.StopAll(targetImageRect);
        targetImageRect.anchoredPosition = Vector2.zero;

        switch (action)
        {
            case ActionType.Jump:
                // frequency: 2 (위로 갔다가 한두 번 띠용~ 하고 멈춤)
                Tween.PunchLocalPosition(targetImageRect, new Vector3(0, 100f, 0), 0.5f, frequency: 2);
                break;

            case ActionType.Shake:
                // 좌우 흔들기 (진동 횟수 10번)
                Tween.ShakeLocalPosition(targetImageRect, new Vector3(50f, 0, 0), 0.5f, frequency: 10);
                break;

            case ActionType.ShakeHorizontal:
                // 상하 흔들기 (진동 횟수 10번)
                Tween.PunchLocalPosition(targetImageRect, new Vector3(0, 50f, 0), 0.5f, frequency: 10);
                break;

            case ActionType.Nod:
                // (Sequence는 변경 없음)
                Sequence.Create()
                    .Chain(Tween.UIAnchoredPositionY(targetImageRect, -30f, 0.15f, Ease.OutQuad))
                    .Chain(Tween.UIAnchoredPositionY(targetImageRect, 0f, 0.15f, Ease.InQuad));
                break;

            case ActionType.Punch:
                // frequency: 1 (커졌다가 딱 한 번 출렁이고 복구됨)
                Tween.PunchScale(targetImageRect, new Vector3(0.2f, 0.2f, 0), 0.4f, frequency: 1);
                break;
        }
    }

    // ========================= [4. 표정 변경 (Change Expression)] =========================
    public void ChangeExpression(string characterName, string spriteName)
    {
        Transform targetSlot = FindSlot(characterName);
        if (targetSlot == null) return;

        // [변경] 계층 구조 반영
        Image charImage = targetSlot.GetChild(0).GetChild(0).GetComponent<Image>();
        Sprite newSprite = Resources.Load<Sprite>("Images/Characters/" + spriteName);

        if (newSprite != null)
        {
            // [수정] 기존 이미지를 복제하여 오버레이 생성
            // Instantiate는 원본의 위치, 회전, 크기(Scale)를 그대로 복사하므로
            // 별도로 위치나 스케일을 0/1로 초기화하면 안 됨 (좌우 반전된 캐릭터 등이 원상복구 되어버릴 수 있음)
            // 1. 마스크 컨테이너 생성 (Softness 효과를 위해)
            GameObject maskObj = new("MaskContainer");
            maskObj.transform.SetParent(charImage.transform, false); // [변경] 부모를 이미지로 설정하여 액션(Scale/Move) 동기화

            RectTransform maskRect = maskObj.AddComponent<RectTransform>();
            maskRect.anchorMin = new Vector2(0.5f, 1f); // Top Center
            maskRect.anchorMax = new Vector2(0.5f, 1f);
            maskRect.pivot = new Vector2(0.5f, 1f);

            // 마스크 영역을 위로 올려서 상단 Softness가 이미지에 영향을 주지 않도록 함
            float softnessOffset = 100f;
            float currentWidth = charImage.rectTransform.rect.width; // [변경] 실제 이미지 너비 사용
            maskRect.anchoredPosition = new Vector2(0, softnessOffset);
            maskRect.sizeDelta = new Vector2(currentWidth, 0); // 너비는 캐릭터 폭, 높이는 0부터 시작

            RectMask2D rectMask = maskObj.AddComponent<RectMask2D>();
            rectMask.softness = new Vector2Int(0, (int)softnessOffset); // 세로 방향 Softness 설정

            // 2. 오버레이 이미지 생성 및 설정
            // [변경] Instantiate 대신 직접 생성 (이미지에 자식이 있을 경우 복제 방지)
            GameObject overlayObj = new("ExpressionOverlay");
            overlayObj.transform.SetParent(maskObj.transform, false);

            Image overlayImage = overlayObj.AddComponent<Image>();
            overlayImage.sprite = newSprite;
            overlayImage.color = charImage.color;
            overlayImage.material = charImage.material;
            overlayImage.raycastTarget = charImage.raycastTarget;
            overlayImage.type = Image.Type.Simple;
            overlayImage.preserveAspect = true;

            // 오버레이 위치 보정 (마스크가 올라간 만큼 내려줌)
            RectTransform overlayRect = overlayImage.rectTransform;
            overlayRect.anchorMin = new Vector2(0.5f, 1f);
            overlayRect.anchorMax = new Vector2(0.5f, 1f);
            overlayRect.pivot = new Vector2(0.5f, 1f);
            overlayRect.anchoredPosition = new Vector2(0, -softnessOffset); // 원위치 유지

            overlayImage.SetNativeSize();

            // 렌더링 순서 보장 (마스크 컨테이너를 가장 앞으로)
            maskObj.transform.SetAsLastSibling();

            // 3. 애니메이션 실행 (마스크 높이를 키워서 이미지를 드러냄)
            // 목표 높이: 캐릭터 이미지 높이 + 오프셋
            float targetHeight = overlayRect.sizeDelta.y + softnessOffset;

            Tween.UISizeDelta(maskRect, new Vector2(currentWidth, targetHeight), 0.5f, Ease.OutQuart)
                .OnComplete(() =>
                {
                    // 원본 교체 및 정리
                    charImage.sprite = newSprite;
                    charImage.SetNativeSize();

                    Destroy(maskObj); // 마스크 컨테이너 삭제 (자식인 오버레이도 같이 삭제됨)
                });
        }
        else
        {
            Debug.LogError($"표정 스프라이트를 찾을 수 없습니다: {spriteName}");
        }
    }

    // [Helper] 이름으로 슬롯 찾기
    private Transform FindSlot(string name)
    {
        // CharacterPanel 바로 아래 자식들 중에서 이름을 검색
        return characterPanel.Find(name);
    }

    private Vector2 GetDirectionVector(EntranceType type)
    {
        return type switch
        {
            EntranceType.Left => new Vector2(-moveDistance, 0),
            EntranceType.Right => new Vector2(moveDistance, 0),
            EntranceType.Center or EntranceType.BottomLeft or EntranceType.BottomRight => new Vector2(0, -moveDistance),
            EntranceType.Top => new Vector2(0, moveDistance),
            _ => Vector2.zero,
        };
    }
}