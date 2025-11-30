using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

public class VNDirector : MonoBehaviour
{
    // ========================= [Enums] =========================
    public enum DirectionType { Left, Right, BottomLeft, BottomRight, Center, Top, RunLeft, RunRight }
    public enum AnimationType { Jump, Shake, Nod, Punch, Run }

    [Header("UI 연결")]
    public Transform characterPanel;

    [Header("설정")]
    public float charWidth = 350f;
    public float defaultDuration = 0.5f;
    public float moveDistance = 800f;

    private const string CharacterPathPrefix = "Images/Characters/";

    // ========================= [Queue System] =========================
    // bool argument: isImmediate (skip animation)
    private Dictionary<string, Queue<Func<bool, UniTask>>> actionQueues = new();
    private Dictionary<string, CancellationTokenSource> activeCTS = new();

    private void EnqueueAction(string charName, Func<bool, UniTask> actionFactory)
    {
        if (!actionQueues.ContainsKey(charName))
        {
            actionQueues[charName] = new Queue<Func<bool, UniTask>>();
        }
        actionQueues[charName].Enqueue(actionFactory);

        if (!activeCTS.ContainsKey(charName) || activeCTS[charName] == null)
        {
            var cts = new CancellationTokenSource();
            activeCTS[charName] = cts;
            ProcessActionQueue(charName, cts.Token).Forget();
        }
    }

    public void CompleteAllActions()
    {
        // 1. Stop all active processing
        foreach (var kvp in activeCTS)
        {
            kvp.Value?.Cancel();
            kvp.Value?.Dispose();
        }
        activeCTS.Clear();

        // 2. Process remaining items in queues immediately
        foreach (var queue in actionQueues.Values)
        {
            while (queue.Count > 0)
            {
                var actionFactory = queue.Dequeue();
                // Execute immediately (skipping animations)
                actionFactory(true).Forget();
            }
        }

        // 3. Ensure all tweens are done (visuals snap to end)
        Tween.CompleteAll();
    }

    private async UniTaskVoid ProcessActionQueue(string charName, CancellationToken token)
    {
        try
        {
            while (actionQueues.ContainsKey(charName) && actionQueues[charName].Count > 0)
            {
                if (token.IsCancellationRequested) break;

                var actionFactory = actionQueues[charName].Dequeue();
                await actionFactory(false); // Normal execution
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
        finally
        {
            // Only remove if this is the CTS we started with (handling race conditions slightly)
            if (activeCTS.ContainsKey(charName) && activeCTS[charName].Token == token)
            {
                var cts = activeCTS[charName];
                activeCTS.Remove(charName);
                cts.Dispose();
            }
        }
    }

    // ========================= [1. 등장 (Entry)] =========================
    public void AddCharacter(string fileName, string type)
    {
        string path = CharacterPathPrefix + fileName;
        Sprite loadedSprite = Resources.Load<Sprite>(path);
        Debug.Log($"VisualNovelLayoutDirector :: AddCharacter: {fileName} ({path})");

        if (loadedSprite != null)
        {
            EnqueueAction(fileName, (isImmediate) => SpawnAsync(fileName, loadedSprite, GetDirectionType(type), isImmediate));
        }
        else
        {
            Debug.LogError($"이미지 로드 실패: {path}");
        }
    }

    private async UniTask SpawnAsync(string name, Sprite sprite, DirectionType type, bool isImmediate)
    {
        if (FindSlot(name) != null)
        {
            Debug.LogWarning($"이미 존재하는 캐릭터입니다: {name}");
            return;
        }

        // 1. 슬롯 생성 (Programmatic)
        var (newSlot, layoutElement, containerRect, charImage) = CreateCharacterSlot(name);

        // 2. 초기화
        charImage.sprite = sprite;
        FitImageToScreen(charImage);
        layoutElement.preferredWidth = 0;
        layoutElement.minWidth = 0;

        // 3. 순서 재배치
        ArrangeSlotOrder(newSlot.transform, type);

        // 4. 위치 잡기 및 애니메이션
        Vector2 startPos = GetDirectionVector(type);
        containerRect.anchoredPosition = startPos;
        charImage.color = new Color(1, 1, 1, 0);

        if (!isImmediate) await UniTask.WaitForEndOfFrame(this);

        // Tween 실행 및 대기
        float duration = isImmediate ? 0f : defaultDuration;

        if (type == DirectionType.RunLeft || type == DirectionType.RunRight)
        {
            PlayActionAsync(newSlot.transform, AnimationType.Run, isImmediate).Forget();
        }

        await Sequence.Create()
            .Group(Tween.Custom(layoutElement, 0f, charWidth, duration, (t, x) => t.preferredWidth = x, Ease.OutQuart))
            .Group(Tween.UIAnchoredPosition(containerRect, Vector2.zero, duration, Ease.OutQuart))
            .Group(Tween.Alpha(charImage, 1f, duration))
            .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());
    }

    // ========================= [2. 퇴장 (Exit)] =========================
    public void RemoveCharacter(string characterName, string exitTo)
    {
        EnqueueAction(characterName, (isImmediate) => ExitAsync(characterName, GetDirectionType(exitTo), isImmediate));
    }

    private async UniTask ExitAsync(string characterName, DirectionType exitTo, bool isImmediate)
    {
        Transform targetSlot = FindSlot(characterName);

        if (targetSlot == null)
        {
            Debug.LogWarning($"삭제 실패: '{characterName}' 캐릭터를 찾을 수 없습니다.");
            return;
        }

        // 중복 호출 방지를 위해 이름을 바꿔둠
        targetSlot.name += "_Removing";

        LayoutElement layoutElement = targetSlot.GetComponent<LayoutElement>();
        Transform container = targetSlot.GetChild(0); // MotionContainer
        RectTransform containerRect = container.GetComponent<RectTransform>();
        Image charImage = container.GetChild(0).GetComponent<Image>(); // Image

        Vector2 targetPos = GetDirectionVector(exitTo);
        float duration = isImmediate ? 0f : defaultDuration;

        if (exitTo == DirectionType.RunLeft || exitTo == DirectionType.RunRight)
        {
            PlayActionAsync(targetSlot, AnimationType.Run, isImmediate).Forget();
        }

        // 이미지 날리기 & 투명화 & 공간 닫기 (동시 실행 및 대기)
        await Sequence.Create()
            .Group(Tween.UIAnchoredPosition(containerRect, targetPos, duration, Ease.OutQuart))
            .Group(Tween.Alpha(charImage, 0f, duration * 0.8f))
            .Group(Tween.Custom(layoutElement, layoutElement.preferredWidth, 0f, duration, (t, x) => t.preferredWidth = x, Ease.OutQuart))
            .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());

        Destroy(targetSlot.gameObject);
    }

    // ========================= [3. 액션 (Action)] =========================
    public void PlayAction(string characterName, string action)
    {
        EnqueueAction(characterName, (isImmediate) => PlayActionAsync(characterName, GetAnimationType(action), isImmediate));
    }

    private async UniTask PlayActionAsync(string characterName, AnimationType action, bool isImmediate)
    {
        Transform targetSlot = FindSlot(characterName);

        if (targetSlot == null)
        {
            Debug.LogWarning($"액션 실패: '{characterName}' 캐릭터를 찾을 수 없습니다.");
            return;
        }

        await PlayActionAsync(targetSlot, action, isImmediate);
    }

    private async UniTask PlayActionAsync(Transform targetSlot, AnimationType action, bool isImmediate)
    {
        // [변경] 계층 구조 반영: Slot -> Container -> Image
        // 액션은 Image에만 적용 (Container는 이동 담당)
        RectTransform targetImageRect = targetSlot.GetChild(0).GetChild(0).GetComponent<RectTransform>();

        // 기존 애니메이션 정지 및 초기화
        Tween.StopAll(targetImageRect);
        targetImageRect.anchoredPosition = Vector2.zero;

        if (isImmediate) return; // 즉시 실행 시 애니메이션 스킵

        Tween actionTween = default;
        Sequence actionSequence = default;
        bool isSequence = false;

        switch (action)
        {
            case AnimationType.Jump:
                actionTween = Tween.PunchLocalPosition(targetImageRect, new Vector3(0, 100f, 0), 0.5f, frequency: 2);
                break;

            case AnimationType.Shake:
                actionTween = Tween.ShakeLocalPosition(targetImageRect, new Vector3(50f, 0, 0), 0.5f, frequency: 10);
                break;

            case AnimationType.Run:
                actionTween = Tween.PunchLocalPosition(targetImageRect, new Vector3(0, 50f, 0), 0.5f, frequency: 10);
                break;

            case AnimationType.Nod:
                isSequence = true;
                actionSequence = Sequence.Create()
                    .Chain(Tween.UIAnchoredPositionY(targetImageRect, -30f, 0.15f, Ease.OutQuad))
                    .Chain(Tween.UIAnchoredPositionY(targetImageRect, 0f, 0.15f, Ease.InQuad));
                break;

            case AnimationType.Punch:
                actionTween = Tween.PunchScale(targetImageRect, new Vector3(0.2f, 0.2f, 0), 0.4f, frequency: 1);
                break;
        }

        if (isSequence)
        {
            if (actionSequence.isAlive) await actionSequence.ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        else
        {
            if (actionTween.isAlive) await actionTween.ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());
        }
    }

    // ========================= [4. 표정 변경 (Change Expression)] =========================
    public void ChangeExpression(string characterName, string spriteName)
    {
        EnqueueAction(characterName, (isImmediate) => ChangeExpressionAsync(characterName, spriteName, isImmediate));
    }

    private async UniTask ChangeExpressionAsync(string characterName, string spriteName, bool isImmediate)
    {
        Transform targetSlot = FindSlot(characterName);
        if (targetSlot == null) return;

        Image charImage = targetSlot.GetChild(0).GetChild(0).GetComponent<Image>();
        Sprite newSprite = Resources.Load<Sprite>(CharacterPathPrefix + spriteName);

        if (newSprite != null)
        {
            if (isImmediate)
            {
                charImage.sprite = newSprite;
                FitImageToScreen(charImage);
                return;
            }

            // 마스크 및 오버레이 설정
            var (maskObj, maskRect, overlayRect) = SetupMaskAndOverlay(charImage, newSprite);

            // 3. 애니메이션 실행 (마스크 높이를 키워서 이미지를 드러냄)
            float softnessOffset = 100f;
            float targetHeight = overlayRect.sizeDelta.y + softnessOffset;
            float currentWidth = charImage.rectTransform.rect.width;

            await Tween.UISizeDelta(maskRect, new Vector2(currentWidth, targetHeight), 0.5f, Ease.OutQuart)
                .ToUniTask(cancellationToken: this.GetCancellationTokenOnDestroy());

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

    // ========================= [Helpers] =========================

    private (GameObject slot, LayoutElement layout, RectTransform container, Image image) CreateCharacterSlot(string name)
    {
        GameObject newSlot = new GameObject(name);
        newSlot.transform.SetParent(characterPanel, false);

        LayoutElement layoutElement = newSlot.AddComponent<LayoutElement>();

        GameObject motionContainer = new("MotionContainer");
        RectTransform containerRect = motionContainer.AddComponent<RectTransform>();
        motionContainer.transform.SetParent(newSlot.transform, false);

        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.sizeDelta = Vector2.zero;

        GameObject imageObj = new GameObject("Image");
        imageObj.transform.SetParent(motionContainer.transform, false);

        Image charImage = imageObj.AddComponent<Image>();

        return (newSlot, layoutElement, containerRect, charImage);
    }

    private (GameObject maskObj, RectTransform maskRect, RectTransform overlayRect) SetupMaskAndOverlay(Image charImage, Sprite newSprite)
    {
        GameObject maskObj = new("MaskContainer");
        maskObj.transform.SetParent(charImage.transform, false);

        RectTransform maskRect = maskObj.AddComponent<RectTransform>();
        maskRect.anchorMin = new Vector2(0.5f, 1f); // Top Center
        maskRect.anchorMax = new Vector2(0.5f, 1f);
        maskRect.pivot = new Vector2(0.5f, 1f);

        float softnessOffset = 100f;
        float currentWidth = charImage.rectTransform.rect.width;
        maskRect.anchoredPosition = new Vector2(0, softnessOffset);
        maskRect.sizeDelta = new Vector2(currentWidth, 0);

        RectMask2D rectMask = maskObj.AddComponent<RectMask2D>();
        rectMask.softness = new Vector2Int(0, (int)softnessOffset);

        GameObject overlayObj = new("ExpressionOverlay");
        overlayObj.transform.SetParent(maskObj.transform, false);

        Image overlayImage = overlayObj.AddComponent<Image>();
        overlayImage.sprite = newSprite;
        overlayImage.color = charImage.color;
        overlayImage.material = charImage.material;
        overlayImage.raycastTarget = charImage.raycastTarget;
        overlayImage.type = Image.Type.Simple;
        overlayImage.preserveAspect = true;

        RectTransform overlayRect = overlayImage.rectTransform;
        overlayRect.anchorMin = new Vector2(0.5f, 1f);
        overlayRect.anchorMax = new Vector2(0.5f, 1f);
        overlayRect.pivot = new Vector2(0.5f, 1f);
        overlayRect.anchoredPosition = new Vector2(0, -softnessOffset);

        FitImageToScreen(overlayImage);

        maskObj.transform.SetAsLastSibling();

        return (maskObj, maskRect, overlayRect);
    }

    private void ArrangeSlotOrder(Transform slotTransform, DirectionType type)
    {
        int totalCount = characterPanel.childCount;
        switch (type)
        {
            case DirectionType.Left:
            case DirectionType.RunLeft:
            case DirectionType.BottomLeft:
                slotTransform.SetSiblingIndex(0); break;
            case DirectionType.Right:
            case DirectionType.RunRight:
            case DirectionType.BottomRight:
                slotTransform.SetSiblingIndex(totalCount - 1); break;
            case DirectionType.Center:
            case DirectionType.Top:
                List<Transform> activeChildren = new();
                for (int i = 0; i < totalCount; i++)
                {
                    Transform child = characterPanel.GetChild(i);
                    if (child != slotTransform && !child.name.Contains("_Removing"))
                    {
                        activeChildren.Add(child);
                    }
                }

                int targetIndex = activeChildren.Count / 2;
                if (targetIndex < activeChildren.Count)
                {
                    slotTransform.SetSiblingIndex(activeChildren[targetIndex].GetSiblingIndex());
                }
                else
                {
                    slotTransform.SetSiblingIndex(totalCount - 1);
                }
                break;
        }
    }

    private Transform FindSlot(string name)
    {
        return characterPanel.Find(name);
    }

    private Vector2 GetDirectionVector(DirectionType type)
    {
        return type switch
        {
            DirectionType.Left or DirectionType.RunLeft => new Vector2(-moveDistance, 0),
            DirectionType.Right or DirectionType.RunRight => new Vector2(moveDistance, 0),
            DirectionType.Center or DirectionType.BottomLeft or DirectionType.BottomRight => new Vector2(0, -moveDistance),
            DirectionType.Top => new Vector2(0, moveDistance),
            _ => Vector2.zero,
        };
    }

    private void FitImageToScreen(Image image)
    {
        image.SetNativeSize();

        float maxHeight = Screen.height * 0.95f;

        if (image.rectTransform.rect.height > maxHeight)
        {
            float aspectRatio = image.rectTransform.rect.width / image.rectTransform.rect.height;
            float newHeight = maxHeight;
            float newWidth = newHeight * aspectRatio;

            image.rectTransform.sizeDelta = new Vector2(newWidth, newHeight);
        }
    }

    private AnimationType GetAnimationType(string str)
    {
        return str switch
        {
            "jump" => AnimationType.Jump,
            "shake" => AnimationType.Shake,
            "run" => AnimationType.Run,
            "nod" => AnimationType.Nod,
            "punch" => AnimationType.Punch
        };
    }

    private DirectionType GetDirectionType(string str)
    {
        return str switch
        {
            "left" => DirectionType.Left,
            "right" => DirectionType.Right,
            "center" => DirectionType.Center,
            "bottomleft" => DirectionType.BottomLeft,
            "bottomright" => DirectionType.BottomRight,
            "top" => DirectionType.Top,
            "runleft" => DirectionType.RunLeft,
            "runright" => DirectionType.RunRight
        };
    }
}