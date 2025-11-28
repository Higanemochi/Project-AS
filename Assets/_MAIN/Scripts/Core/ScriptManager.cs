using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ScriptManager : MonoBehaviour
{
    [SerializeField]
    TextAsset scriptFile;
    [SerializeField]
    TextMeshProUGUI speakerText;
    [SerializeField]
    GameObject speakerSprite;
    [SerializeField]
    TextMeshProUGUI dialogueText;
    [SerializeField]
    private GameObject choiceButtonPrefab;
    [SerializeField]
    private Transform choiceButtonContainer;
    [SerializeField]
    private Image choiceBackground;
    [SerializeField]
    float charsPerSecond = 45f;

    public VisualNovelLayoutDirector director;
    private readonly float shakeAmount = 1.1f;
    private bool isChoiceAvailable = false;
    private Tween dialogueTween;
    private Script _currentScript;

    void Start()
    {
        speakerText.SetText(" ");
        speakerText.ForceMeshUpdate(true);
        dialogueText.SetText(" ");
        dialogueText.ForceMeshUpdate(true);

        _currentScript = ScriptParser.Parse(scriptFile.text);
        NextStep();
    }


    void Update()
    {
        DisplayEffects(dialogueText);
        if (!isChoiceAvailable && !IsPointerOverInteractiveUI() && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)))
        {
            if (dialogueTween.isAlive)
                dialogueTween.Complete();
            else
                NextStep();
        }

    }

    private void NextStep()
    {
        if (_currentScript.HasNextAction())
        {
            ScriptAction action = _currentScript.Continue();
            ExecuteAction(action);
            return;
        }

        Debug.Log("ScriptManager :: End of Script");
    }

    private void ExecuteAction(ScriptAction action)
    {
        if (action.Type == "label")
        {
            string labelName = action.GetParam("content");
            Debug.Log($"ScriptManager :: Change Label: {labelName}");
            NextStep();
            return;
        }
        if (action.Type == "bg")
        {
            string bgFile = action.GetParam("file");
            Debug.Log($"ScriptManager :: Change Background: {bgFile}");
            NextStep();
            return;
        }
        if (action.Type == "char")
        {
            string charFile = action.GetParam("img");
            string charEntrance = action.GetParam("enter");
            if (charEntrance == "") charEntrance = "center";
            if (charEntrance.ToLower() == "center") director.AddCharacter(charFile, VisualNovelLayoutDirector.EntranceType.Center);
            if (charEntrance.ToLower() == "left") director.AddCharacter(charFile, VisualNovelLayoutDirector.EntranceType.Left);
            if (charEntrance.ToLower() == "right") director.AddCharacter(charFile, VisualNovelLayoutDirector.EntranceType.Right);
            if (charEntrance.ToLower() == "bottomleft") director.AddCharacter(charFile, VisualNovelLayoutDirector.EntranceType.BottomLeft);
            if (charEntrance.ToLower() == "bottomright") director.AddCharacter(charFile, VisualNovelLayoutDirector.EntranceType.BottomRight);
            Debug.Log($"ScriptManager :: Character: {charFile}");
            NextStep();
            return;
        }
        if (action.Type == "remove")
        {
            string charName = action.GetParam("target");
            string exitType = action.GetParam("exit");
            if (exitType == "") exitType = "center";

            VisualNovelLayoutDirector.EntranceType type = VisualNovelLayoutDirector.EntranceType.Center;
            if (exitType.ToLower() == "left") type = VisualNovelLayoutDirector.EntranceType.Left;
            if (exitType.ToLower() == "right") type = VisualNovelLayoutDirector.EntranceType.Right;
            if (exitType.ToLower() == "bottomleft") type = VisualNovelLayoutDirector.EntranceType.BottomLeft;
            if (exitType.ToLower() == "bottomright") type = VisualNovelLayoutDirector.EntranceType.BottomRight;
            if (exitType.ToLower() == "top") type = VisualNovelLayoutDirector.EntranceType.Top;

            director.RemoveCharacter(charName, type);
            Debug.Log($"ScriptManager :: Remove Character: {charName} to {exitType}");
            NextStep();
            return;
        }
        if (action.Type == "action")
        {
            string charName = action.GetParam("target");
            string charAnim = action.GetParam("anim");
            if (charAnim == "") charAnim = "center";
            if (charAnim.ToLower() == "jump") director.PlayAction(charName, VisualNovelLayoutDirector.ActionType.Jump);
            if (charAnim.ToLower() == "shake") director.PlayAction(charName, VisualNovelLayoutDirector.ActionType.Shake);
            if (charAnim.ToLower() == "shakehorizontal") director.PlayAction(charName, VisualNovelLayoutDirector.ActionType.ShakeHorizontal);
            if (charAnim.ToLower() == "nod") director.PlayAction(charName, VisualNovelLayoutDirector.ActionType.Nod);
            if (charAnim.ToLower() == "punch") director.PlayAction(charName, VisualNovelLayoutDirector.ActionType.Punch);
            Debug.Log($"ScriptManager :: Action: {charName} {charAnim}");
            NextStep();
            return;
        }
        if (action.Type == "expr")
        {
            string charName = action.GetParam("target");
            string charExpr = action.GetParam("expr");
            director.ChangeExpression(charName, charExpr);
            Debug.Log($"ScriptManager :: Expression: {charName} {charExpr}");
            NextStep();
            return;
        }
        if (action.Type == "spk")
        {
            string speaker = action.GetParam("name");
            if (speaker == "")
                speakerSprite.SetActive(false);

            speakerText.SetText(speaker);
            speakerText.ForceMeshUpdate(true);
            NextStep();
            return;
        }
        if (action.Type == "msg")
        {
            string dialogue = action.GetParam("content");
            DisplayDialogue(dialogue);
            return;
        }
        if (action.Type == "goto")
        {
            string targetLabel = action.GetParam("content");
            _currentScript.JumpTo(targetLabel);
            NextStep();
            return;
        }
        if (action.Type == "choices")
        {
            Debug.Log("ScriptManager :: Show Choices");
            isChoiceAvailable = true;

            // WTF.. is this shit
            Color tempColor = choiceBackground.color;
            tempColor.a = 0.8f;
            choiceBackground.color = tempColor;

            foreach (var choice in action.Choices)
            {
                string text = choice["content"];
                string target = choice["goto"];
                GameObject buttonObj = Instantiate(choiceButtonPrefab, choiceButtonContainer);
                buttonObj.GetComponentInChildren<TextMeshProUGUI>().text = text;
                buttonObj
                    .GetComponent<Button>()
                    .onClick.AddListener(() =>
                    {
                        foreach (Transform child in choiceButtonContainer)
                            Destroy(child.gameObject);
                        isChoiceAvailable = false;

                        // shitty code
                        tempColor.a = 0f;
                        choiceBackground.color = tempColor;

                        _currentScript.JumpTo(target);
                        NextStep();
                    });
            }
            return;
        }
    }

    public void DebugReload()
    {
        speakerText.SetText(" ");
        speakerText.ForceMeshUpdate(true);
        dialogueText.SetText(" ");
        dialogueText.ForceMeshUpdate(true);

        _currentScript = ScriptParser.Parse(scriptFile.text);
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

    private void DisplayDialogue(string text)
    {
        // Unity 내부 최적화로 인해 줄이 바뀔 시 LinkInfo 배열이 초기화되지 않음.
        // 따라서 수동으로 초기화를 수행.
        dialogueText.textInfo.linkInfo = new TMP_LinkInfo[0];
        dialogueText.SetText(text);
        dialogueText.ForceMeshUpdate(true);
        dialogueText.maxVisibleCharacters = 0;

        dialogueTween = Tween.Custom(
            startValue: 0f,
            endValue: dialogueText.textInfo.characterCount,
            duration: dialogueText.textInfo.characterCount / charsPerSecond,
            onValueChange: x => dialogueText.maxVisibleCharacters = Mathf.RoundToInt(x),
            ease: Ease.Linear
        );
    }

    private void DisplayEffects(TextMeshProUGUI text)
    {
        text.ForceMeshUpdate(true);

        TMP_TextInfo textInfo = text.textInfo;
        TMP_LinkInfo[] linkInfo = textInfo.linkInfo;

        Mesh mesh = text.mesh;
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
                    continue; // 공백은 VertexIndex 0 Return -> Visible이 안 되므로

                if (linkName == "shake")
                {
                    Vector3 offset = new(
                        Random.Range(-shakeAmount, shakeAmount),
                        Random.Range(-shakeAmount, shakeAmount)
                    );
                    for (byte j = 0; j < 4; j++)
                        vertices[idx + j] += offset;
                }
            }
        }

        mesh.vertices = vertices;
        text.canvasRenderer.SetMesh(mesh);
    }
}
