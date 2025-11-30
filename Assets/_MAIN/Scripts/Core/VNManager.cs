using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class VNManager : MonoBehaviour
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

    public VNDirector director;
    private readonly float shakeAmount = 1.1f;
    private bool isChoiceAvailable = false;
    private Tween dialogueTween;
    private Command _currentScript;

    public static string NextScriptPath = "";

    void Start()
    {
        speakerText.SetText(" ");
        speakerText.ForceMeshUpdate(true);
        dialogueText.SetText(" ");
        dialogueText.ForceMeshUpdate(true);

        if (!string.IsNullOrEmpty(NextScriptPath))
        {
            TextAsset loadedScript = Resources.Load<TextAsset>($"NovelScripts/{NextScriptPath}");
            if (loadedScript != null)
            {
                _currentScript = Parser.Parse(loadedScript.text);
                NextScriptPath = "";
            }
            else
            {
                Debug.LogError($"ScriptManager :: Cannot find script: {NextScriptPath}");
                _currentScript = Parser.Parse(scriptFile.text);
            }
        }
        else
        {
            _currentScript = Parser.Parse(scriptFile.text);
        }

        NextStep();
    }

    void Update()
    {
        DisplayEffects(dialogueText);
        if (!isChoiceAvailable && !IsPointerOverInteractiveUI() && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)))
        {
            if (dialogueTween.isAlive)
            {
                director.CompleteAllActions();
                dialogueTween.Complete();
            }

            else
                NextStep();
        }

    }

    private void NextStep()
    {
        if (_currentScript.HasNextAction())
        {
            CommandSet command = _currentScript.Continue();
            Execute(command);
            return;
        }

        Debug.Log("ScriptManager :: End of Script");
    }

    private void Execute(CommandSet command)
    {
        if (command.Type == "label")
        {
            string labelName = command.GetParam("content");
            Debug.Log($"ScriptManager :: Change Label: {labelName}");
            NextStep();
            return;
        }
        if (command.Type == "bg")
        {
            string bgFile = command.GetParam("file");
            Debug.Log($"ScriptManager :: Change Background: {bgFile}");
            NextStep();
            return;
        }
        if (command.Type == "char")
        {
            string charFile = command.GetParam("img");
            if (string.IsNullOrEmpty(charFile))
            {
                charFile = command.GetParam("target");
            }
            string charEntrance = command.GetParam("enter");
            if (charEntrance == "") charEntrance = "center";
            if (charEntrance.ToLower() == "center") director.AddCharacter(charFile, VNDirector.EntranceType.Center);
            if (charEntrance.ToLower() == "top") director.AddCharacter(charFile, VNDirector.EntranceType.Top);
            if (charEntrance.ToLower() == "left") director.AddCharacter(charFile, VNDirector.EntranceType.Left);
            if (charEntrance.ToLower() == "right") director.AddCharacter(charFile, VNDirector.EntranceType.Right);
            if (charEntrance.ToLower() == "bottomleft") director.AddCharacter(charFile, VNDirector.EntranceType.BottomLeft);
            if (charEntrance.ToLower() == "bottomright") director.AddCharacter(charFile, VNDirector.EntranceType.BottomRight);
            if (charEntrance.ToLower() == "leftrun") director.AddCharacter(charFile, VNDirector.EntranceType.LeftRun);
            if (charEntrance.ToLower() == "rightrun") director.AddCharacter(charFile, VNDirector.EntranceType.RightRun);
            Debug.Log($"ScriptManager :: Character: {charFile}");
            NextStep();
            return;
        }
        if (command.Type == "remove")
        {
            string charName = command.GetParam("target");
            string exitType = command.GetParam("exit");
            if (exitType == "") exitType = "center";

            VNDirector.EntranceType type = new();
            if (exitType.ToLower() == "center") type = VNDirector.EntranceType.Center;
            if (exitType.ToLower() == "left") type = VNDirector.EntranceType.Left;
            if (exitType.ToLower() == "right") type = VNDirector.EntranceType.Right;
            if (exitType.ToLower() == "bottomleft") type = VNDirector.EntranceType.BottomLeft;
            if (exitType.ToLower() == "bottomright") type = VNDirector.EntranceType.BottomRight;
            if (exitType.ToLower() == "top") type = VNDirector.EntranceType.Top;
            if (exitType.ToLower() == "leftrun") type = VNDirector.EntranceType.LeftRun;
            if (exitType.ToLower() == "rightrun") type = VNDirector.EntranceType.RightRun;

            director.RemoveCharacter(charName, type);
            Debug.Log($"ScriptManager :: Remove Character: {charName} to {exitType}");
            NextStep();
            return;
        }
        if (command.Type == "action")
        {
            string charName = command.GetParam("target");
            string charAnim = command.GetParam("anim");
            if (charAnim == "") charAnim = "center";
            if (charAnim.ToLower() == "jump") director.PlayAction(charName, VNDirector.ActionType.Jump);
            if (charAnim.ToLower() == "shake") director.PlayAction(charName, VNDirector.ActionType.Shake);
            if (charAnim.ToLower() == "run") director.PlayAction(charName, VNDirector.ActionType.Run);
            if (charAnim.ToLower() == "nod") director.PlayAction(charName, VNDirector.ActionType.Nod);
            if (charAnim.ToLower() == "punch") director.PlayAction(charName, VNDirector.ActionType.Punch);
            Debug.Log($"ScriptManager :: Action: {charName} {charAnim}");
            NextStep();
            return;
        }
        if (command.Type == "expr")
        {
            string charName = command.GetParam("target");
            string charExpr = command.GetParam("expr");
            director.ChangeExpression(charName, charExpr);
            Debug.Log($"ScriptManager :: Expression: {charName} {charExpr}");
            NextStep();
            return;
        }
        if (command.Type == "spk")
        {
            string speaker = command.GetParam("name");
            if (speakerSprite.activeSelf == false)
                speakerSprite.SetActive(true);
            if (speaker == "")
                speakerSprite.SetActive(false);

            speaker = Store.Instance.ReplaceVariables(speaker);
            Debug.Log($"ScriptManager :: Speaker: {speaker}");
            speakerText.SetText(speaker);
            speakerText.ForceMeshUpdate(true);
            NextStep();
            return;
        }
        if (command.Type == "msg")
        {
            string dialogue = command.GetParam("content");
            dialogue = Store.Instance.ReplaceVariables(dialogue);


            DisplayDialogue(dialogue);

            if (_currentScript.PeekNext()?.Type == "choices")
            {
                NextStep();
            }
            return;
        }
        if (command.Type == "goto")
        {
            string targetLabel = command.GetParam("content");
            _currentScript.JumpTo(targetLabel);
            NextStep();
            return;
        }
        if (command.Type == "choices")
        {
            Debug.Log("ScriptManager :: Show Choices");
            isChoiceAvailable = true;

            // WTF.. is this shit
            Color tempColor = choiceBackground.color;
            tempColor.a = 0.8f;
            choiceBackground.color = tempColor;

            foreach (var choice in command.Choices)
            {
                string text = Store.Instance.ReplaceVariables(choice["content"]);
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
        if (command.Type == "var")
        {
            foreach (var entry in command.Params)
            {
                Store.Instance.SetVariable(entry.Key, entry.Value.ToString());
            }
            NextStep();
            return;
        }
        if (command.Type == "add")
        {
            foreach (var entry in command.Params)
            {
                Store.Instance.AddVariable(entry.Key, entry.Value.ToString());
            }
            NextStep();
            return;
        }
        if (command.Type == "scene")
        {
            string sceneName = command.GetParam("file");
            string nextScript = command.GetParam("script");
            Debug.Log($"ScriptManager :: Load Scene: {sceneName}, Next Script: {nextScript}");

            NextScriptPath = nextScript;
            SceneManager.LoadScene(sceneName);
            return;
        }
    }

    public void DebugReload()
    {
        speakerText.SetText(" ");
        speakerText.ForceMeshUpdate(true);
        dialogueText.SetText(" ");
        dialogueText.ForceMeshUpdate(true);

        _currentScript = Parser.Parse(scriptFile.text);
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
