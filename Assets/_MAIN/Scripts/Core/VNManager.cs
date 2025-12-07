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
    private bool isChoiceAvailable = false;
    private Tween dialogueTween;
    private Script _currentScript;

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
        if (_currentScript.HasNextCommand())
        {
            Command command = _currentScript.Continue();
            Execute(command);
            return;
        }

        Debug.Log("ScriptManager :: End of Script");
    }

    private void Execute(Command command)
    {
        switch (command.Type)
        {
            case "label":
                Debug.Log($"ScriptManager :: Change Label: {command.GetParam("content")}");
                NextStep();
                return;
            case "bg":
                Debug.Log($"ScriptManager :: Change Background: {command.GetParam("file")}");
                NextStep();
                return;
            case "char":
                director.AddCharacter(command.GetParam("img"), command.GetParam("enter").ToLower());
                Debug.Log($"ScriptManager :: Character: {command.GetParam("img")}");
                NextStep();
                return;
            case "remove":
                director.RemoveCharacter(command.GetParam("target"), command.GetParam("exit").ToLower());
                Debug.Log($"ScriptManager :: Remove Character: {command.GetParam("target")} to {command.GetParam("exit").ToLower()}");
                NextStep();
                return;
            case "action":
                director.PlayAction(command.GetParam("target"), command.GetParam("anim").ToLower());
                Debug.Log($"ScriptManager :: Action: {command.GetParam("target")} {command.GetParam("anim").ToLower()}");
                NextStep();
                return;
            case "expr":
                director.ChangeExpression(command.GetParam("target"), command.GetParam("expr").ToLower());
                Debug.Log($"ScriptManager :: Expression: {command.GetParam("target")} {command.GetParam("expr").ToLower()}");
                NextStep();
                return;
            case "spk":
                if (speakerSprite.activeSelf == false)
                    speakerSprite.SetActive(true);
                if (command.GetParam("name") == "")
                    speakerSprite.SetActive(false);

                string speaker = Store.Instance.ReplaceVariables(command.GetParam("name"));
                Debug.Log($"ScriptManager :: Speaker: {speaker}");
                speakerText.SetText(speaker);
                speakerText.ForceMeshUpdate(true);
                NextStep();
                return;
            case "msg":
                string dialogue = command.GetParam("content");
                dialogue = Store.Instance.ReplaceVariables(dialogue);

                DisplayDialogue(dialogue);

                if (_currentScript.PeekNext()?.Type == "choices")
                {
                    NextStep();
                }
                return;
            case "goto":
                string targetLabel = command.GetParam("content");
                _currentScript.JumpTo(targetLabel);
                NextStep();
                return;
            case "choices":
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
            case "var":
                foreach (var entry in command.Params)
                {
                    Store.Instance.SetVariable(entry.Key, entry.Value.ToString());
                }
                NextStep();
                return;
            case "add":
                foreach (var entry in command.Params)
                {
                    Store.Instance.AddVariable(entry.Key, entry.Value.ToString());
                }
                NextStep();
                return;
            case "scene":
                string sceneName = command.GetParam("file");
                string nextScript = command.GetParam("script");
                Debug.Log($"ScriptManager :: Load Scene: {sceneName}, Next Script: {nextScript}");

                NextScriptPath = nextScript;
                SceneManager.LoadScene(sceneName);
                return;
            default:
                Debug.LogWarning($"ScriptManager :: Unknown command: {command.Type}");
                NextStep();
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

    public bool IsDialoguePlaying()
    {
        return dialogueTween.isAlive;
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
                    Vector3 offset = new(Random.Range(-1.1f, 1.1f), Random.Range(-1.1f, 1.1f));
                    for (byte j = 0; j < 4; j++)
                        vertices[idx + j] += offset;
                }
            }
        }

        mesh.vertices = vertices;
        text.canvasRenderer.SetMesh(mesh);
    }
}
