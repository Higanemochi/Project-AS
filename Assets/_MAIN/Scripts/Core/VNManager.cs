using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class VNManager : MonoBehaviour
{
    [SerializeField]
    private TextAsset _scriptFile;
    [SerializeField]
    private DialogueDrawer _dialogueDrawer;
    [SerializeField]
    private ChoiceDrawer _choiceDrawer;
    [SerializeField]
    private CharacterDrawer _characterDrawer;


    // Stores
    private DialogueStore _dialogueStore;
    private ChoiceStore _choiceStore;
    private CharacterStore _characterStore;
    private FlowStore _flowStore;
    private VariableStore _variableStore;

    // 컴파일된 스크립트
    private ScriptAction[] _actions;
    private ScriptContext _context;
    private int _currentIndex = 0;
    private bool _inputReceived = false;

    private void Awake()
    {
        // Store 초기화
        _dialogueStore = new();
        _choiceStore = new();
        _characterStore = new();
        _flowStore = new();
        _variableStore = new VariableStore();

        // 모든 Drawer 바인딩
        _dialogueDrawer.Bind(_dialogueStore, _flowStore);
        _choiceDrawer.Bind(_choiceStore);
        _characterDrawer.Bind(_characterStore, _flowStore);

        // 선택지 선택 이벤트 구독
        _choiceDrawer.OnChoiceSelected += OnChoiceSelected;
    }

    private void Start()
    {
        // 스크립트 로드 및 컴파일
        var (commands, labelMap) = Parser.Parse(_scriptFile.text);
        _actions = Compiler.Compile(commands, labelMap);

        // 컨텍스트 구성
        _context = new ScriptContext
        {
            DialogueStore = _dialogueStore,
            ChoiceStore = _choiceStore,
            CharacterStore = _characterStore,
            FlowStore = _flowStore,
            VariableStore = _variableStore,
            PeekNextType = () =>
                _currentIndex + 1 < _actions.Length
                    ? _actions[_currentIndex + 1].DebugType
                    : null,
            OnScriptChange = HandleScriptChange
        };

        // 실행 시작
        ExecuteScriptAsync().Forget();
    }

    private void HandleScriptChange(string scriptPath)
    {
        TextAsset script = Resources.Load<TextAsset>($"NovelScripts/{scriptPath}");
        if (script == null)
        {
            Debug.LogError($"ScriptManager :: Cannot find script: {scriptPath}");
            return;
        }

        var (commands, labelMap) = Parser.Parse(script.text);
        _actions = Compiler.Compile(commands, labelMap);
        _currentIndex = 0;
        ExecuteScriptAsync().Forget();
    }

    private async UniTaskVoid ExecuteScriptAsync()
    {
        while (_currentIndex < _actions.Length)
        {
            var action = _actions[_currentIndex];

            // choices가 아닌 다른 명령어 실행 시 선택지 UI 숨김
            if (_choiceStore.IsVisible.Value && action.DebugType != "choices")
                _choiceStore.Hide();

            var result = await action.Execute(_context);

            switch (result.Type)
            {
                case ScriptResult.ResultType.Continue:
                    _currentIndex++;
                    break;

                case ScriptResult.ResultType.Wait:
                    await UniTask.WaitUntil(() => _inputReceived);
                    _inputReceived = false;
                    _currentIndex++;
                    break;

                case ScriptResult.ResultType.Jump:
                    _currentIndex = result.NextIndex;
                    break;

                case ScriptResult.ResultType.End:
                    Debug.Log("ScriptManager :: Script ended");
                    return;
            }
        }
        Debug.Log("ScriptManager :: End of Script");
    }

    private void Update()
    {
        // 선택지 표시 중에는 입력 무시
        if (_choiceStore.IsVisible.Value) return;

        if (!IsPointerOverInteractiveUI() && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)))
        {
            if (_dialogueStore.IsDrawing.Value)
            {
                // 공통 FlowStore로 스킵 신호 전달
                _flowStore.RequestSkip();
            }
            else
            {
                _inputReceived = true;
            }
        }
    }

    /// <summary>ChoiceDrawer에서 선택 시 호출</summary>
    private void OnChoiceSelected(string targetLabel, int targetIndex)
    {
        _currentIndex = targetIndex;
        _inputReceived = true;
    }

    private bool IsPointerOverInteractiveUI()
    {
        PointerEventData eventData = new(EventSystem.current)
        {
            position = Input.mousePosition
        };
        List<RaycastResult> results = new();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (RaycastResult result in results)
            if (result.gameObject.GetComponent<Selectable>() != null)
                return true;

        return false;
    }
}
