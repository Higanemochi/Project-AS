using PrimeTween;
using R3;
using TMPro;
using UnityEngine;

class DialogueDrawer : MonoBehaviour
{
    [Header("Dialogue Settings")]
    public GameObject speakerPanel;
    public TextMeshProUGUI dialogueTMP;
    public TextMeshProUGUI speakerTMP;
    public float charsPerSecond = 50f;

    private Tween dialogueTween;
    private DialogueStore _state;
    private FlowStore _flow;
    private readonly CompositeDisposable _disposables = new();

    private void Awake()
    {
        // VNManager.Start()보다 먼저 실행되어야 함
        speakerTMP.SetText(" ");
        speakerTMP.ForceMeshUpdate(true);
        dialogueTMP.SetText(" ");
        dialogueTMP.ForceMeshUpdate(true);
    }

    private void Update()
    {
        DisplayEffects(dialogueTMP);
    }

    public void Bind(DialogueStore state, FlowStore flow)
    {
        _state = state;
        _flow = flow;

        _state.Dialogue.Subscribe(x =>
        {
            _state.IsDrawing.Value = true;
            DrawDialogue(x);
        }).AddTo(_disposables);

        _state.Speaker.Subscribe(x => DrawSpeaker(x)).AddTo(_disposables);

        // FlowStore에서 IsSkipping 구독
        _flow.IsSkipping.Subscribe(x =>
        {
            if (x && dialogueTween.isAlive)
            {
                dialogueTween.Complete();
                _state.IsDrawing.Value = false;
                _flow.ResetSkip();
            }
        }).AddTo(_disposables);
    }

    private void DrawSpeaker(string speaker)
    {
        if (speaker == "")
        {
            speakerPanel.SetActive(false);
            return;
        }

        speakerPanel.SetActive(true);
        speakerTMP.SetText(speaker);
        speakerTMP.ForceMeshUpdate(true);
    }

    private void DrawDialogue(string text)
    {
        // Unity 내부 최적화로 인해 줄이 바뀔 시 LinkInfo 배열이 초기화되지 않음.
        // 따라서 수동으로 초기화를 수행.
        dialogueTMP.textInfo.linkInfo = new TMP_LinkInfo[0];
        dialogueTMP.SetText(text);
        dialogueTMP.ForceMeshUpdate(true);

        int charCount = dialogueTMP.textInfo.characterCount;

        // 빈 텍스트면 즉시 완료
        if (charCount == 0)
        {
            dialogueTMP.maxVisibleCharacters = 0;
            _state.IsDrawing.Value = false;
            return;
        }

        dialogueTMP.maxVisibleCharacters = 0;

        dialogueTween = Tween.Custom(
            startValue: 0f,
            endValue: charCount,
            duration: charCount / charsPerSecond,
            onValueChange: x => dialogueTMP.maxVisibleCharacters = Mathf.RoundToInt(x),
            ease: Ease.Linear
        ).OnComplete(() => _state.IsDrawing.Value = false);
    }

    private void DisplayEffects(TextMeshProUGUI textObj)
    {
        textObj.ForceMeshUpdate(true);

        TMP_TextInfo textInfo = textObj.textInfo;
        TMP_LinkInfo[] linkInfo = textInfo.linkInfo;

        Mesh mesh = textObj.mesh;
        Vector3[] vertices = mesh.vertices;

        foreach (var link in linkInfo)
        {
            string linkName = link.GetLinkID();
            int start = link.linkTextfirstCharacterIndex;
            int end = link.linkTextfirstCharacterIndex + link.linkTextLength;

            for (var i = start; i < end; i++)
            {
                TMP_CharacterInfo c = textInfo.characterInfo[i];
                int idx = c.vertexIndex;

                if (!c.isVisible)
                    continue;

                if (linkName == "shake")
                {
                    Vector3 offset = new(Random.Range(-1.1f, 1.1f), Random.Range(-1.1f, 1.1f));
                    for (byte j = 0; j < 4; j++)
                        vertices[idx + j] += offset;
                }
            }
        }

        mesh.vertices = vertices;
        textObj.canvasRenderer.SetMesh(mesh);
    }

    private void OnDestroy()
    {
        _disposables.Dispose();
    }
}