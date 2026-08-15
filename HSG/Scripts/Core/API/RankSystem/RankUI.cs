//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Linq;
//using TMPro;
//using UnityEngine;
//using UnityEngine.UI;
//using Image = UnityEngine.UI.Image;

//namespace NebulaN.Systems.RankSystem;

//public class RankUI : MonoBehaviour
//{
//    public static RankUI Instance;
//    private Canvas canvas;
//    private GameObject root;
//    private GameObject panel;
//    private RectTransform panelRect;
//    private TMP_Text rankText;
//    private TMP_Text scoreText;
//    private TMP_Text nextText;
//    private Transform recordParent;
//    private Sprite[] RankIcons = new Sprite[6];

//    private void Awake() => Instance = this;

//    public static void Create()
//    {
//        if (Instance != null) return;
//        GameObject obj = new GameObject("NebulaRankUI");
//        DontDestroyOnLoad(obj);
//        obj.AddComponent<RankUI>();
//    }

//    private void Start()
//    {
//        LoadResources();
//        CreateCanvas();
//        CreateButton();
//        CreatePanel();
//    }

//    #region Canvas
//    private void CreateCanvas()
//    {
//        GameObject obj = new GameObject("RankCanvas");
//        canvas = obj.AddComponent<Canvas>();
//        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
//        obj.AddComponent<CanvasScaler>();
//        obj.AddComponent<GraphicRaycaster>();
//        DontDestroyOnLoad(obj);
//    }
//    #endregion

//    #region Button
//    private void CreateButton()
//    {
//        GameObject btn = new GameObject("RankButton");
//        btn.transform.SetParent(canvas.transform);
//        RectTransform rect = btn.AddComponent<RectTransform>();
//        rect.anchorMin = new Vector2(1, 0);
//        rect.anchorMax = new Vector2(1, 0);
//        rect.anchoredPosition = new Vector2(-80, 80);
//        rect.sizeDelta = new Vector2(70, 70);
//        Image image = btn.AddComponent<Image>();
//        image.sprite = LoadSprite("Rank/Button");
//        Button button = btn.AddComponent<Button>();
//        button.onClick.AddListener(Open);
//    }
//    #endregion

//    #region Panel
//    private void CreatePanel()
//    {
//        panel = new GameObject("RankPanel");
//        panel.transform.SetParent(canvas.transform);
//        panelRect = panel.AddComponent<RectTransform>();
//        panelRect.anchorMin = new Vector2(.5f, .5f);
//        panelRect.anchorMax = new Vector2(.5f, .5f);
//        panelRect.sizeDelta = new Vector2(650, 500);
//        Image bg = panel.AddComponent<Image>();
//        bg.sprite = LoadSprite("Rank/Panel");
//        CreateTexts();
//        panel.SetActive(false);
//    }

//    private void CreateTexts()
//    {
//        rankText = CreateText("RankName", new Vector2(0, 170), 32);
//        scoreText = CreateText("Score", new Vector2(0, 120), 26);
//        nextText = CreateText("Next", new Vector2(0, 80), 22);
//        GameObject list = new GameObject("Records");
//        list.transform.SetParent(panel.transform);
//        RectTransform rect = list.AddComponent<RectTransform>();
//        rect.anchoredPosition = new Vector2(0, -40);
//        rect.sizeDelta = new Vector2(550, 250);
//        recordParent = list.transform;
//    }

//    private TMP_Text CreateText(string name, Vector2 pos, int size)
//    {
//        GameObject obj = new GameObject(name);
//        obj.transform.SetParent(panel.transform);
//        RectTransform rect = obj.AddComponent<RectTransform>();
//        rect.anchoredPosition = pos;
//        rect.sizeDelta = new Vector2(500, 50);
//        TMP_Text text = obj.AddComponent<TMP_Text>();
//        text.alignment = TextAlignmentOptions.Center;
//        text.fontSize = size;
//        return text;
//    }
//    #endregion

//    public void Open()
//    {
//        panel.SetActive(true);
//        Refresh();
//        AmongUsClient.Instance.StartCoroutine(OpenAnimation().WrapToIl2Cpp());
//    }

//    public void Close() => panel.SetActive(false);

//    private void Refresh()
//    {
//        string code = PatchManager.GetFriendCode(PlayerControl.LocalPlayer);
//        var data = GetData(code);
//        if (data == null) return;
//        int rank = RankSystem.GetRank(code);
//        int score = data.Score;
//        rankText.text = $"Rank {rank}";
//        scoreText.text = $"Score : {score}";
//        nextText.text = $"Next : {GetNeedScore(rank)}";
//        foreach (Transform child in recordParent) Destroy(child.gameObject);
//        foreach (var r in data.Records.OrderByDescending(x => x.Time)) CreateRecord(r);
//    }

//    private void CreateRecord(RankRecord record)
//    {
//        TMP_Text text = CreateText("Record", Vector2.zero, 18);
//        text.transform.SetParent(recordParent);
//        text.text = $"{record.Score:+#;-#;0}  {record.Reason}";
//    }

//    #region Animation
//    IEnumerator OpenAnimation()
//    {
//        panelRect.localScale = Vector3.zero;
//        float timer = 0;
//        while (timer < 0.25f)
//        {
//            timer += Time.deltaTime;
//            float t = timer / .25f;
//            panelRect.localScale = Vector3.one * EaseOutBack(t);
//            yield return null;
//        }
//        panelRect.localScale = Vector3.one;
//    }

//    private float EaseOutBack(float t)
//    {
//        float c1 = 1.70158f;
//        float c3 = c1 + 1;
//        return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
//    }
//    #endregion

//    #region Resource
//    private void LoadResources()
//    {
//        for (int i = 0; i < 6; i++) RankIcons[i] = LoadSprite($"Rank/Icon/{i + 1}");
//    }

//    private Sprite LoadSprite(string path) => null; // 替换为实际加载
//    #endregion

//    private RankSaveData GetData(string code)
//    {
//        var field = typeof(RankSystem).GetField("Datas", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
//        var dic = field?.GetValue(null) as Dictionary<string, RankSaveData>;
//        if (dic != null && dic.TryGetValue(code, out var data)) return data;
//        return null;
//    }

//    private int GetNeedScore(int rank) => rank switch
//    {
//        1 => 100,
//        2 => 250,
//        3 => 500,
//        4 => 900,
//        5 => 1500,
//        _ => 99999
//    };
//}