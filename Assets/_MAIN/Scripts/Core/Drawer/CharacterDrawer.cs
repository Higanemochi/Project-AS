using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using R3;
using UnityEngine;
using UnityEngine.UI;

/// <summary>CharacterStore의 시그널을 구독하여 실제 UI 렌더링을 담당</summary>
public class CharacterDrawer : MonoBehaviour
{
    [Header("UI 연결")]
    public Transform characterPanel;

    [Header("설정")]
    public float charWidth = 500f;
    public float defaultDuration = 0.5f;
    public float moveDistance = 800f;

    private CharacterStore _store;
    private FlowStore _flow;
    private readonly Dictionary<string, CharacterSlot> _slots = new();
    private readonly CompositeDisposable _disposables = new();

    // ========================= [Async Queue System] =========================
    private readonly Dictionary<string, SemaphoreSlim> _locks = new();
    private CancellationTokenSource _globalCts = new();

    public void Bind(CharacterStore store, FlowStore flow)
    {
        _store = store;
        _flow = flow;

        _store.OnCharacterAdded += HandleCharacterAdded;
        _store.OnCharacterExiting += HandleCharacterExiting;
        _store.OnCharacterRemoved += HandleCharacterRemoved;
        _store.OnActionRequested += HandleActionRequested;
        _store.OnExpressionRequested += HandleExpressionRequested;

        _flow.IsSkipping.Subscribe(isSkipping =>
        {
            if (isSkipping)
            {
                CompleteAll();
            }
        }).AddTo(_disposables);
    }

    private void OnDestroy()
    {
        if (_store != null)
        {
            _store.OnCharacterAdded -= HandleCharacterAdded;
            _store.OnCharacterExiting -= HandleCharacterExiting;
            _store.OnCharacterRemoved -= HandleCharacterRemoved;
            _store.OnActionRequested -= HandleActionRequested;
            _store.OnExpressionRequested -= HandleExpressionRequested;
        }
        _disposables.Dispose();
        _globalCts.Dispose();
        foreach (var sem in _locks.Values) sem.Dispose();
    }

    // ========================= [Queue Processing] =========================
    private SemaphoreSlim GetOrCreateLock(string charName)
    {
        if (!_locks.TryGetValue(charName, out var sem))
        {
            sem = new SemaphoreSlim(1, 1);
            _locks[charName] = sem;
        }
        return sem;
    }

    private async void EnqueueAsync(string charName, Func<bool, UniTask> action)
    {
        var sem = GetOrCreateLock(charName);
        var token = _globalCts.Token;

        await sem.WaitAsync();
        try
        {
            bool isImmediate = token.IsCancellationRequested;
            await action(isImmediate);
        }
        catch (OperationCanceledException)
        {
            // 취소된 경우 무시
        }
        finally
        {
            sem.Release();
        }
    }

    public void CompleteAll()
    {
        _globalCts.Cancel();
        _globalCts.Dispose();
        _globalCts = new CancellationTokenSource();

        Tween.CompleteAll();
    }

    // ========================= [Event Handlers] =========================
    private void HandleCharacterAdded(string name, CharacterState state, DirectionType direction)
    {
        EnqueueAsync(name, isImmediate => SpawnCharacterAsync(name, state, direction, isImmediate));
    }

    private void HandleCharacterExiting(string name, CharacterState state, DirectionType direction)
    {
        EnqueueAsync(name, isImmediate => ProcessExitAsync(name, state, direction, isImmediate));
    }

    private async UniTask ProcessExitAsync(string name, CharacterState state, DirectionType direction, bool isImmediate)
    {
        if (!_slots.TryGetValue(name, out var slot))
        {
            Debug.LogWarning($"퇴장 실패: '{name}' 슬롯을 찾을 수 없습니다.");
            return;
        }

        _slots.Remove(name);
        slot.gameObject.name = name + "_Removing";

        await ExitCharacterAsync(slot, state, direction, isImmediate);
    }

    private void HandleCharacterRemoved(string name)
    {
        // 슬롯은 ExitCharacterAsync에서 직접 파괴
    }

    private void HandleActionRequested(string name, AnimationType action)
    {
        if (_slots.TryGetValue(name, out var slot))
        {
            EnqueueAsync(name, isImmediate => PlayActionOnSlotAsync(slot, action, isImmediate));
        }
        else
        {
            Debug.LogWarning($"액션 실패: '{name}' 슬롯을 찾을 수 없습니다.");
        }
    }

    private void HandleExpressionRequested(string name, Sprite newSprite)
    {
        if (_slots.TryGetValue(name, out var slot))
        {
            EnqueueAsync(name, isImmediate => ChangeExpressionOnSlotAsync(slot, newSprite, isImmediate));
        }
        else
        {
            Debug.LogWarning($"표정 변경 실패: '{name}' 슬롯을 찾을 수 없습니다.");
        }
    }

    // ========================= [Spawn] =========================
    private async UniTask SpawnCharacterAsync(string name, CharacterState state, DirectionType direction, bool isImmediate)
    {
        var slot = CreateSlot(name);
        _slots[name] = slot;

        slot.SetSprite(state.Sprite.Value);
        FitImageToScreen(slot.image);
        slot.layoutElement.preferredWidth = 0;
        slot.layoutElement.minWidth = 0;

        ArrangeSlotOrder(slot.transform, direction);

        Vector2 startPos = GetDirectionVector(direction);
        slot.containerRect.anchoredPosition = startPos;
        slot.image.color = new Color(1, 1, 1, 0);

        if (!isImmediate)
            await UniTask.WaitForEndOfFrame(this);

        if (isImmediate)
        {
            // 즉시 모드: 값 직접 설정
            slot.layoutElement.preferredWidth = charWidth;
            slot.containerRect.anchoredPosition = Vector2.zero;
            slot.image.color = Color.white;
        }
        else
        {
            TriggerRunAnimationIfNeeded(slot, direction, isImmediate);

            await Sequence.Create()
                .Group(Tween.Custom(slot.layoutElement, 0f, charWidth, defaultDuration, (t, x) => t.preferredWidth = x, Ease.OutQuart))
                .Group(Tween.UIAnchoredPosition(slot.containerRect, Vector2.zero, defaultDuration, Ease.OutQuart))
                .Group(Tween.Alpha(slot.image, 1f, defaultDuration));
        }

        state.Alpha.Value = 1f;
        state.SlotWidth.Value = charWidth;
        state.Position.Value = Vector2.zero;
    }

    // ========================= [Exit] =========================
    private async UniTask ExitCharacterAsync(CharacterSlot slot, CharacterState state, DirectionType direction, bool isImmediate)
    {
        if (slot == null || slot.gameObject == null) return;

        Vector2 targetPos = GetDirectionVector(direction);

        if (isImmediate)
        {
            // 즉시 모드: 값 직접 설정
            slot.containerRect.anchoredPosition = targetPos;
            slot.image.color = new Color(1, 1, 1, 0);
            slot.layoutElement.preferredWidth = 0;
        }
        else
        {
            TriggerRunAnimationIfNeeded(slot, direction, isImmediate);

            await Sequence.Create()
                .Group(Tween.UIAnchoredPosition(slot.containerRect, targetPos, defaultDuration, Ease.OutQuart))
                .Group(Tween.Alpha(slot.image, 0f, defaultDuration * 0.8f))
                .Group(Tween.Custom(slot.layoutElement, slot.layoutElement.preferredWidth, 0f, defaultDuration, (t, x) => t.preferredWidth = x, Ease.OutQuart));
        }

        // 항상 실행되어야 하는 정리 작업
        _store.FinalRemove(state.Name);
        if (slot != null && slot.gameObject != null)
        {
            Destroy(slot.gameObject);
        }
    }

    // ========================= [Action] =========================
    private async UniTask PlayActionOnSlotAsync(CharacterSlot slot, AnimationType action, bool isImmediate)
    {
        RectTransform targetRect = slot.imageRect;

        Tween.StopAll(targetRect);
        targetRect.anchoredPosition = Vector2.zero;

        if (isImmediate) return;

        switch (action)
        {
            case AnimationType.Jump:
                await Tween.PunchLocalPosition(targetRect, new Vector3(0, 100f, 0), 0.5f, frequency: 2);
                break;
            case AnimationType.Shake:
                await Tween.ShakeLocalPosition(targetRect, new Vector3(50f, 0, 0), 0.5f, frequency: 10);
                break;
            case AnimationType.Run:
                await Tween.PunchLocalPosition(targetRect, new Vector3(0, 50f, 0), 0.5f, frequency: 10);
                break;
            case AnimationType.Nod:
                await Sequence.Create()
                    .Chain(Tween.UIAnchoredPositionY(targetRect, -30f, 0.15f, Ease.OutQuad))
                    .Chain(Tween.UIAnchoredPositionY(targetRect, 0f, 0.15f, Ease.InQuad));
                break;
            case AnimationType.Punch:
                await Tween.PunchScale(targetRect, new Vector3(0.2f, 0.2f, 0), 0.4f, frequency: 1);
                break;
        }
    }

    // ========================= [Expression] =========================
    private async UniTask ChangeExpressionOnSlotAsync(CharacterSlot slot, Sprite newSprite, bool isImmediate)
    {
        if (isImmediate)
        {
            slot.SetSprite(newSprite);
            FitImageToScreen(slot.image);
            return;
        }

        var (maskObj, maskRect, overlayRect) = SetupMaskAndOverlay(slot.image, newSprite);

        float softnessOffset = 100f;
        float targetHeight = overlayRect.sizeDelta.y + softnessOffset;
        float currentWidth = slot.image.rectTransform.rect.width;
        float duration = 0.5f;

        // Tween을 직접 await
        await Tween.UISizeDelta(maskRect, new Vector2(currentWidth, targetHeight), duration, Ease.OutQuart);

        slot.SetSprite(newSprite);
        FitImageToScreen(slot.image);

        Destroy(maskObj);
    }

    // ========================= [Slot Creation] =========================
    private CharacterSlot CreateSlot(string name)
    {
        GameObject slotObj = new(name);
        slotObj.transform.SetParent(characterPanel, false);

        LayoutElement layoutElement = slotObj.AddComponent<LayoutElement>();

        GameObject motionContainer = new("MotionContainer");
        RectTransform containerRect = motionContainer.AddComponent<RectTransform>();
        motionContainer.transform.SetParent(slotObj.transform, false);

        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.sizeDelta = Vector2.zero;

        GameObject imageObj = new("Image");
        imageObj.transform.SetParent(motionContainer.transform, false);

        Image charImage = imageObj.AddComponent<Image>();
        RectTransform imageRect = charImage.rectTransform;

        CharacterSlot slot = slotObj.AddComponent<CharacterSlot>();
        slot.Initialize(layoutElement, containerRect, charImage, imageRect);

        return slot;
    }

    private (GameObject maskObj, RectTransform maskRect, RectTransform overlayRect) SetupMaskAndOverlay(Image charImage, Sprite newSprite)
    {
        GameObject maskObj = new("MaskContainer");
        maskObj.transform.SetParent(charImage.transform, false);

        RectTransform maskRect = maskObj.AddComponent<RectTransform>();
        maskRect.anchorMin = new Vector2(0.5f, 1f);
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

    // ========================= [Helpers] =========================
    private void ArrangeSlotOrder(Transform slotTransform, DirectionType type)
    {
        int totalCount = characterPanel.childCount;
        switch (type)
        {
            case DirectionType.Left:
            case DirectionType.RunLeft:
            case DirectionType.BottomLeft:
                slotTransform.SetSiblingIndex(0);
                break;
            case DirectionType.Right:
            case DirectionType.RunRight:
            case DirectionType.BottomRight:
                slotTransform.SetSiblingIndex(totalCount - 1);
                break;
            case DirectionType.Center:
            case DirectionType.Top:
                List<Transform> activeChildren = new();
                for (int i = 0; i < totalCount; i++)
                {
                    Transform child = characterPanel.GetChild(i);
                    if (child != slotTransform && !child.name.Contains("_Removing"))
                        activeChildren.Add(child);
                }
                int targetIndex = activeChildren.Count / 2;
                if (targetIndex < activeChildren.Count)
                    slotTransform.SetSiblingIndex(activeChildren[targetIndex].GetSiblingIndex());
                else
                    slotTransform.SetSiblingIndex(totalCount - 1);
                break;
        }
    }

    private void TriggerRunAnimationIfNeeded(CharacterSlot slot, DirectionType direction, bool isImmediate)
    {
        if (direction is DirectionType.RunLeft or DirectionType.RunRight)
            PlayActionOnSlotAsync(slot, AnimationType.Run, isImmediate).Forget();
    }

    private Vector2 GetDirectionVector(DirectionType type) => type switch
    {
        DirectionType.Left or DirectionType.RunLeft => new Vector2(-moveDistance, 0),
        DirectionType.Right or DirectionType.RunRight => new Vector2(moveDistance, 0),
        DirectionType.Center or DirectionType.BottomLeft or DirectionType.BottomRight => new Vector2(0, -moveDistance),
        DirectionType.Top => new Vector2(0, moveDistance),
        _ => Vector2.zero,
    };

    private void FitImageToScreen(Image image)
    {
        image.SetNativeSize();
        float maxHeight = Screen.height * 0.95f;
        if (image.rectTransform.rect.height > maxHeight)
        {
            float aspectRatio = image.rectTransform.rect.width / image.rectTransform.rect.height;
            image.rectTransform.sizeDelta = new Vector2(maxHeight * aspectRatio, maxHeight);
        }
    }
}
