using System;
using System.Collections.Generic;
using PrimeTween;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>ChoiceStore의 시그널을 구독하여 선택지 UI 렌더링을 담당</summary>
public class ChoiceDrawer : MonoBehaviour
{
    [Header("UI 연결")]
    public Transform buttonContainer;
    public Image background;
    public GameObject buttonPrefab;

    [Header("설정")]
    public float fadeDuration = 0.3f;

    private ChoiceStore _state;
    private readonly CompositeDisposable _disposables = new();

    /// <summary>선택지 클릭 시 발생하는 이벤트 (targetLabel, targetIndex 전달)</summary>
    public event Action<string, int> OnChoiceSelected;

    private void Awake()
    {
        // 초기 상태: 배경 투명
        if (background != null)
        {
            var color = background.color;
            color.a = 0f;
            background.color = color;
        }
    }

    public void Bind(ChoiceStore state)
    {
        _state = state;
        _state.IsVisible.Subscribe(OnVisibilityChanged).AddTo(_disposables);
        _state.Options.Subscribe(OnOptionsChanged).AddTo(_disposables);
    }

    private void OnVisibilityChanged(bool isVisible)
    {
        if (background == null) return;

        var color = background.color;
        color.a = isVisible ? 0.8f : 0f;
        background.color = color;
    }

    private void OnOptionsChanged(List<ChoiceOption> options)
    {
        // 기존 버튼 정리
        foreach (Transform child in buttonContainer)
            Destroy(child.gameObject);

        if (options == null || options.Count == 0) return;

        // 새 버튼 생성
        foreach (var option in options)
        {
            var buttonObj = Instantiate(buttonPrefab, buttonContainer);
            buttonObj.GetComponentInChildren<TextMeshProUGUI>().text = option.Text;

            var targetLabel = option.TargetLabel;
            var targetIndex = option.TargetIndex;
            buttonObj.GetComponent<Button>().onClick.AddListener(() =>
            {
                OnChoiceSelected?.Invoke(targetLabel, targetIndex);
            });
        }
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
    }
}
