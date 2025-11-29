using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

public class VNDirector : MonoBehaviour
{
    // ========================= [Enums] =========================
    public enum EntranceType { Left, Right, BottomLeft, BottomRight, Center, Top, LeftRun, RightRun }
    public enum ActionType { Jump, Shake, Nod, Punch, Run }

    [Header("UI 연결")]
    public Transform characterPanel;
    public GameObject slotPrefab;

    [Header("설정")]
    public float charWidth = 350f;
    public float defaultDuration = 0.5f;
    public float moveDistance = 800f;

    // ========================= [Queue System] =========================
    private Dictionary<string, Queue<IEnumerator>> actionQueues = new();
    private Dictionary<string, Coroutine> activeCoroutines = new();

    private void EnqueueAction(string charName, IEnumerator action)
    {
        if (!actionQueues.ContainsKey(charName))
        {
            actionQueues[charName] = new Queue<IEnumerator>();
        }
        actionQueues[charName].Enqueue(action);

        if (!activeCoroutines.ContainsKey(charName) || activeCoroutines[charName] == null)
        {
            activeCoroutines[charName] = StartCoroutine(ProcessActionQueue(charName));
        }
    }

    public void CompleteAllActions()
    {
        // 1. Stop all active processing coroutines
        foreach (var kvp in activeCoroutines)
        {
            if (kvp.Value != null) StopCoroutine(kvp.Value);
        }
        activeCoroutines.Clear();

        // 2. Process remaining items in queues immediately
        foreach (var queue in actionQueues.Values)
        {
            while (queue.Count > 0)
            {
                var action = queue.Dequeue();
                RunImmediate(action);
            }
        }

        // 3. Ensure all tweens are done (visuals snap to end)
        Tween.CompleteAll();
    }

    private void RunImmediate(IEnumerator enumerator)
    {
        while (enumerator.MoveNext())
        {
            var current = enumerator.Current;
            if (current is IEnumerator nested)
            {
                RunImmediate(nested);
            }
            // Force complete any tweens that might have been started
            Tween.CompleteAll();
        }
    }

    private IEnumerator ProcessActionQueue(string charName)
    {
        while (actionQueues.ContainsKey(charName) && actionQueues[charName].Count > 0)
        {
            IEnumerator action = actionQueues[charName].Dequeue();
            yield return StartCoroutine(action);
        }
        activeCoroutines.Remove(charName);
    }

    // ========================= [1. 등장 (Entry)] =========================
    public void AddCharacter(string fileName, EntranceType type)
    {
        string path = "Images/Characters/" + fileName;
        Sprite loadedSprite = Resources.Load<Sprite>(path);
        Debug.Log($"VisualNovelLayoutDirector :: AddCharacter: {fileName} ({path})");

        if (loadedSprite != null)
        {
            EnqueueAction(fileName, SpawnRoutine(fileName, loadedSprite, type));
        }
        else
        {
            Debug.LogError($"이미지 로드 실패: {path}");
        }
    }

    private IEnumerator SpawnRoutine(string name, Sprite sprite, EntranceType type)
    {
        if (FindSlot(name) != null)
        {
            Debug.LogWarning($"이미 존재하는 캐릭터입니다: {name}");
            yield break;
        }
        // 1. 슬롯 생성
        GameObject newSlot = Instantiate(slotPrefab, characterPanel);

        // 오브젝트 이름을 파일명(ID)으로 설정
        newSlot.name = name;

        LayoutElement layoutElement = newSlot.GetComponent<LayoutElement>();

        // Slot -> MotionContainer -> Image
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
        FitImageToScreen(charImage);
        layoutElement.preferredWidth = 0; layoutElement.minWidth = 0;

        // 3. 순서 재배치
        int totalCount = characterPanel.childCount;
        switch (type)
        {
            case EntranceType.Left:
            case EntranceType.LeftRun:
            case EntranceType.BottomLeft:
                newSlot.transform.SetSiblingIndex(0); break;
            case EntranceType.Right:
            case EntranceType.RightRun:
            case EntranceType.BottomRight:
                newSlot.transform.SetSiblingIndex(totalCount - 1); break;
            case EntranceType.Center:
            case EntranceType.Top:
                // 삭제 중인 캐릭터를 제외하고 순서 계산
                List<Transform> activeChildren = new();
                for (int i = 0; i < totalCount; i++)
                {
                    Transform child = characterPanel.GetChild(i);
                    if (child != newSlot.transform && !child.name.Contains("_Removing"))
                    {
                        activeChildren.Add(child);
                    }
                }

                int targetIndex = activeChildren.Count / 2;
                if (targetIndex < activeChildren.Count)
                {
                    // activeChildren[targetIndex]의 현재 인덱스 앞에 배치
                    newSlot.transform.SetSiblingIndex(activeChildren[targetIndex].GetSiblingIndex());
                }
                else
                {
                    // 맨 뒤로
                    newSlot.transform.SetSiblingIndex(totalCount - 1);
                }
                break;
        }

        // 4. 위치 잡기 및 애니메이션
        Vector2 startPos = GetDirectionVector(type);
        containerRect.anchoredPosition = startPos;
        charImage.color = new Color(1, 1, 1, 0);

        yield return new WaitForEndOfFrame();

        // Tween 실행 및 대기
        switch (type)
        {
            case EntranceType.LeftRun:
            case EntranceType.RightRun:
                StartCoroutine(PlayActionRoutine(newSlot.transform, ActionType.Run));
                break;
        }

        yield return Sequence.Create()
            .Group(Tween.Custom(layoutElement, 0f, charWidth, defaultDuration, (t, x) => t.preferredWidth = x, Ease.OutQuart))
            .Group(Tween.UIAnchoredPosition(containerRect, Vector2.zero, defaultDuration, Ease.OutQuart))
            .Group(Tween.Alpha(charImage, 1f, defaultDuration))
            .ToYieldInstruction();
    }

    // ========================= [2. 퇴장 (Exit)] =========================
    public void RemoveCharacter(string characterName, EntranceType exitTo)
    {
        EnqueueAction(characterName, ExitRoutine(characterName, exitTo));
    }

    private IEnumerator ExitRoutine(string characterName, EntranceType exitTo)
    {
        Transform targetSlot = FindSlot(characterName);

        if (targetSlot == null)
        {
            Debug.LogWarning($"삭제 실패: '{characterName}' 캐릭터를 찾을 수 없습니다.");
            yield break;
        }

        // 중복 호출 방지를 위해 이름을 바꿔둠
        targetSlot.name += "_Removing";

        LayoutElement layoutElement = targetSlot.GetComponent<LayoutElement>();

        // [변경] 계층 구조 반영
        Transform container = targetSlot.GetChild(0); // MotionContainer
        RectTransform containerRect = container.GetComponent<RectTransform>();
        Image charImage = container.GetChild(0).GetComponent<Image>(); // Image

        Vector2 targetPos = GetDirectionVector(exitTo);

        switch (exitTo)
        {
            case EntranceType.LeftRun:
            case EntranceType.RightRun:
                StartCoroutine(PlayActionRoutine(targetSlot, ActionType.Run));
                break;
        }

        // 이미지 날리기 & 투명화 & 공간 닫기 (동시 실행 및 대기)
        yield return Sequence.Create()
            .Group(Tween.UIAnchoredPosition(containerRect, targetPos, defaultDuration, Ease.OutQuart))
            .Group(Tween.Alpha(charImage, 0f, defaultDuration * 0.8f))
            .Group(Tween.Custom(layoutElement, layoutElement.preferredWidth, 0f, defaultDuration, (t, x) => t.preferredWidth = x, Ease.OutQuart))
            .ToYieldInstruction();

        Destroy(targetSlot.gameObject);
    }

    // ========================= [3. 액션 (Action)] =========================
    public void PlayAction(string characterName, ActionType action)
    {
        EnqueueAction(characterName, PlayActionRoutine(characterName, action));
    }

    private IEnumerator PlayActionRoutine(string characterName, ActionType action)
    {
        Transform targetSlot = FindSlot(characterName);

        if (targetSlot == null)
        {
            Debug.LogWarning($"액션 실패: '{characterName}' 캐릭터를 찾을 수 없습니다.");
            yield break;
        }

        yield return PlayActionRoutine(targetSlot, action);
    }

    private IEnumerator PlayActionRoutine(Transform targetSlot, ActionType action)
    {
        // [변경] 계층 구조 반영: Slot -> Container -> Image
        // 액션은 Image에만 적용 (Container는 이동 담당)
        RectTransform targetImageRect = targetSlot.GetChild(0).GetChild(0).GetComponent<RectTransform>();

        // 기존 애니메이션 정지 및 초기화
        Tween.StopAll(targetImageRect);
        targetImageRect.anchoredPosition = Vector2.zero;

        Tween actionTween = default;
        Sequence actionSequence = default;
        bool isSequence = false;

        switch (action)
        {
            case ActionType.Jump:
                actionTween = Tween.PunchLocalPosition(targetImageRect, new Vector3(0, 100f, 0), 0.5f, frequency: 2);
                break;

            case ActionType.Shake:
                actionTween = Tween.ShakeLocalPosition(targetImageRect, new Vector3(50f, 0, 0), 0.5f, frequency: 10);
                break;

            case ActionType.Run:
                actionTween = Tween.PunchLocalPosition(targetImageRect, new Vector3(0, 50f, 0), 0.5f, frequency: 10);
                break;

            case ActionType.Nod:
                isSequence = true;
                actionSequence = Sequence.Create()
                    .Chain(Tween.UIAnchoredPositionY(targetImageRect, -30f, 0.15f, Ease.OutQuad))
                    .Chain(Tween.UIAnchoredPositionY(targetImageRect, 0f, 0.15f, Ease.InQuad));
                break;

            case ActionType.Punch:
                actionTween = Tween.PunchScale(targetImageRect, new Vector3(0.2f, 0.2f, 0), 0.4f, frequency: 1);
                break;
        }

        if (isSequence)
        {
            if (actionSequence.isAlive) yield return actionSequence.ToYieldInstruction();
        }
        else
        {
            if (actionTween.isAlive) yield return actionTween.ToYieldInstruction();
        }
    }

    // ========================= [4. 표정 변경 (Change Expression)] =========================
    public void ChangeExpression(string characterName, string spriteName)
    {
        EnqueueAction(characterName, ChangeExpressionRoutine(characterName, spriteName));
    }

    private IEnumerator ChangeExpressionRoutine(string characterName, string spriteName)
    {
        Transform targetSlot = FindSlot(characterName);
        if (targetSlot == null) yield break;

        // [변경] 계층 구조 반영
        Image charImage = targetSlot.GetChild(0).GetChild(0).GetComponent<Image>();
        Sprite newSprite = Resources.Load<Sprite>("Images/Characters/" + spriteName);

        if (newSprite != null)
        {
            // 1. 마스크 컨테이너 생성
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

            // 2. 오버레이 이미지 생성
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

            FitImageToScreen(overlayImage);

            // 렌더링 순서 보장 (마스크 컨테이너를 가장 앞으로)
            maskObj.transform.SetAsLastSibling();

            // 3. 애니메이션 실행 (마스크 높이를 키워서 이미지를 드러냄)
            // 목표 높이: 캐릭터 이미지 높이 + 오프셋
            float targetHeight = overlayRect.sizeDelta.y + softnessOffset;

            yield return Tween.UISizeDelta(maskRect, new Vector2(currentWidth, targetHeight), 0.5f, Ease.OutQuart)
                .ToYieldInstruction();

            // 원본 교체 및 정리
            charImage.sprite = newSprite;
            FitImageToScreen(charImage);

            Destroy(maskObj);
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
            EntranceType.Left or EntranceType.LeftRun => new Vector2(-moveDistance, 0),
            EntranceType.Right or EntranceType.RightRun => new Vector2(moveDistance, 0),
            EntranceType.Center or EntranceType.BottomLeft or EntranceType.BottomRight => new Vector2(0, -moveDistance),
            EntranceType.Top => new Vector2(0, moveDistance),
            _ => Vector2.zero,
        };
    }

    private void FitImageToScreen(Image image)
    {
        image.SetNativeSize();

        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas == null) return;

        RectTransform canvasRect = rootCanvas.GetComponent<RectTransform>();
        // 화면 높이의 95%를 넘지 않도록 설정
        float maxHeight = canvasRect.rect.height * 0.95f;

        if (image.rectTransform.rect.height > maxHeight)
        {
            float aspectRatio = image.rectTransform.rect.width / image.rectTransform.rect.height;
            float newHeight = maxHeight;
            float newWidth = newHeight * aspectRatio;

            image.rectTransform.sizeDelta = new Vector2(newWidth, newHeight);
        }
    }
}