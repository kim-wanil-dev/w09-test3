using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = System.Random;

namespace Fireworks
{
    /// <summary>런 상태, 준비 화면, 턴 진행과 픽셀 불꽃 표현을 소유한다.</summary>
    public sealed class FireworksGame : MonoBehaviour
    {
        private const int MAX_PRACTICE_POWDER = 12;
        private const int MAX_QUEUE_CARDS = MAX_PRACTICE_POWDER;
        private static readonly int[] _showGoals = { 18, 22, 25 };
        private static readonly string[] _showNames = { "마을의 첫 불빛", "강변 축제", "마지막 밤" };
        private static readonly int[][] _winds = { new[] { 2, 1, 2 }, new[] { 1, 2, 1 }, new[] { 1, 1, 2 } };
        private readonly List<int> _deck = new List<int>();
        private readonly List<int> _draw = new List<int>();
        private readonly List<int> _discard = new List<int>();
        private readonly List<int> _hand = new List<int>();
        private readonly List<int> _replayIds = new List<int>();
        private readonly List<int> _rewards = new List<int>();
        private readonly List<Button> _handButtons = new List<Button>();
        private readonly Button[] _slotButtons = new Button[MAX_QUEUE_CARDS];
        private readonly PlannedCard[] _slots = new PlannedCard[MAX_QUEUE_CARDS];
        private Canvas _canvas;
        private AudioSource _audioSource;
        private AudioClip _launchSound;
        private AudioClip _burstSound;
        private Sprite _sparkSprite;
        private Text _status;
        private Text _summary;
        private Text _log;
        private Text _tooltip;
        private Button _performButton;
        private Button _replayButton;
        private Button _recallButton;
        private Button _speedButton;
        private Button _pauseButton;
        private Button _skipButton;
        private Button _practicePageButton;
        private Button _practiceExitButton;
        private Button _practicePowderButton;
        private int _selected = -1;
        private int _practicePage;
        private int _focusedSlot = -1;
        private int _show;
        private int _turn;
        private int _score;
        private int _smoke;
        private int _powder;
        private int _maxPowder = 4;
        private int _seed;
        private bool _tutorial;
        private bool _threeColors;
        private bool _busy;
        private bool _skipPresentation;
        private bool _reducedFlash;
        private float _speed = 1f;
        private float _volume = 0.6f;
        private readonly List<GameObject> _fireworks = new List<GameObject>();

        /// <summary>카메라와 입력 UI가 없는 장면에서 자동으로 게임 화면을 만든다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureGame()
        {
            if (FindFirstObjectByType<FireworksGame>() == null)
            {
                GameObject game = new GameObject("FireworksGame");
                game.AddComponent<FireworksGame>();
            }
        }

        private void Awake()
        {
            Camera camera = Camera.main;
            if (camera == null) camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.12f);
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.volume = _volume;
            _launchSound = CreateTone("Launch", 620f, 0.22f);
            _burstSound = CreateTone("Burst", 180f, 0.42f);
            _sparkSprite = PixelSprite(Color.white);
            CreatePixelNightSky();
            BuildUi();
            ShowTitle();
        }

        /// <summary>화면 폭죽과 상태 UI를 갱신한다.</summary>
        private void Update()
        {
            for (int i = _fireworks.Count - 1; i >= 0; i--)
            {
                GameObject item = _fireworks[i];
                if (item == null) { _fireworks.RemoveAt(i); continue; }
                if (item.transform.position.y < -4.6f) { Destroy(item); _fireworks.RemoveAt(i); }
            }
        }

        /// <summary>화면 크기에 맞춘 uGUI 캔버스와 상호작용 요소를 구성한다.</summary>
        private void BuildUi()
        {
            GameObject canvasObject = new GameObject("GameCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            UnityEngine.EventSystems.EventSystem eventSystem = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null) eventSystem = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem)).GetComponent<UnityEngine.EventSystems.EventSystem>();
            UnityEngine.EventSystems.StandaloneInputModule legacyModule = eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (legacyModule != null) Destroy(legacyModule);
            if (eventSystem.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null) eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Panel("Header", new Vector2(0.5f, 1f), new Vector2(1280f, 110f), new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Color(0.04f, 0.07f, 0.18f, 0.96f));
            _status = Label("Status", "마지막 밤의 불꽃술사", 30, new Vector2(0.5f, 0.94f), new Vector2(1000f, 52f), Color.white);
            _summary = Label("Summary", "", 20, new Vector2(0.5f, 0.865f), new Vector2(1000f, 36f), new Color(0.8f, 0.88f, 1f));
            _log = Label("Log", "", 19, new Vector2(0.5f, 0.33f), new Vector2(1050f, 130f), new Color(1f, 0.86f, 0.55f));
            for (int i = 0; i < _slots.Length; i++)
            {
                int index = i;
                _slotButtons[i] = Button("Queue" + i, "추가", new Vector2(0.07f + 0.143f * (i % 7), 0.59f - 0.11f * (i / 7)), new Vector2(160f, 68f), () => OnSlot(index));
                _slotButtons[i].gameObject.AddComponent<FireworksDrag>().OnDrop = data => DragFromQueue(index, data);
            }
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                _handButtons.Add(Button("Card" + i, "", new Vector2(0.1f + 0.2f * i, 0.13f), new Vector2(220f, 125f), () => SelectCard(index)));
                _handButtons[i].gameObject.AddComponent<FireworksDrag>().OnDrop = data => DragFromHand(index, data);
            }
            _performButton = Button("Perform", "공연 시작", new Vector2(0.83f, 0.31f), new Vector2(210f, 64f), Perform);
            _replayButton = Button("Replay", "직전 공연 다시 보기", new Vector2(0.83f, 0.22f), new Vector2(210f, 56f), Replay);
            _recallButton = Button("Recall", "선택 카드 회수", new Vector2(0.63f, 0.22f), new Vector2(190f, 56f), RecallSlot);
            _speedButton = Button("SpeedControl", "연출 1×", new Vector2(0.62f, 0.77f), new Vector2(125f, 42f), ToggleSpeed);
            _pauseButton = Button("PauseControl", "일시정지", new Vector2(0.75f, 0.77f), new Vector2(125f, 42f), TogglePause);
            _skipButton = Button("SkipControl", "즉시 결과", new Vector2(0.88f, 0.77f), new Vector2(125f, 42f), SkipPresentation);
            _practicePageButton = Button("PracticePage", "다음 카드", new Vector2(0.15f, 0.31f), new Vector2(150f, 50f), NextPracticePage);
            _practiceExitButton = Button("PracticeExit", "연습 종료", new Vector2(0.32f, 0.31f), new Vector2(150f, 50f), ExitPractice);
            _practicePowderButton = Button("PracticePowder", "최대 화약 +1", new Vector2(0.49f, 0.31f), new Vector2(160f, 50f), IncreasePracticePowder);
            _tooltip = Label("CardDescription", "", 19, new Vector2(0.5f, 0.41f), new Vector2(800f, 90f), Color.white);
            _tooltip.gameObject.SetActive(false);
            for (int i = 0; i < _handButtons.Count; i++)
            {
                int index = i;
                UnityEngine.EventSystems.EventTrigger trigger = _handButtons[i].gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                UnityEngine.EventSystems.EventTrigger.Entry enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => ShowCardDescription(index));
                trigger.triggers.Add(enter);
                UnityEngine.EventSystems.EventTrigger.Entry exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => _tooltip.gameObject.SetActive(false));
                trigger.triggers.Add(exit);
            }
            for (int i = 0; i < _slotButtons.Length; i++)
            {
                int index = i;
                UnityEngine.EventSystems.EventTrigger trigger = _slotButtons[i].gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                UnityEngine.EventSystems.EventTrigger.Entry enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => { if (!_busy && _slots[index] != null) ShowDescription(_slots[index].Card); });
                trigger.triggers.Add(enter);
                UnityEngine.EventSystems.EventTrigger.Entry exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => _tooltip.gameObject.SetActive(false));
                trigger.triggers.Add(exit);
            }
            SetGameControls(false);
        }

        /// <summary>손패 인덱스의 카드 규칙과 효과 설명을 표시한다.</summary>
        private void ShowCardDescription(int index)
        {
            if (_tutorial) index += _practicePage * 5;
            if (_busy || index >= _hand.Count) return;
            ShowDescription(FireworkCatalog.Get(_hand[index]));
        }

        /// <summary>카드 정의의 비용과 고유 효과를 설명 영역에 표시한다.</summary>
        private void ShowDescription(CardDefinition card)
        {
            string[] descriptions = { "", "빨간 국화. 긴 잔광과 많은 연기를 남깁니다.", "초록 별. 인접한 빨강 또는 무지개탄에 연쇄되면 +3점.", "파란 고리. 폭발 후 연기 1 제거.", "빨간 유성. 큰 폭발과 긴 잔광을 남깁니다.", "파란 부채. 폭발 후 연기 2 제거.", "파란 고리. 연기 최대 2 제거, 2 제거 시 +4점.", "무지개 별. 모든 폭발에 연쇄해 +1점. 앞서 빨강·초록·파랑이 모두 터졌다면 점수 2배.", "파란 이중 고리. 폭발 후 연기 1 제거.", "초록 꽃다발. 앞서 2회 이상 폭발했다면 기본점수 +4.", "파란 별. 폭발 후 연기 1 제거.", "빨간 개막탄. 개막에 폭발하면 추가 +3점.", "초록 피날레탄. 피날레에 폭발하면 추가 +3점.", "초록 잔광탄. 앞서 빨간 폭죽이 터졌다면 점수 2배. 인접 빨강 또는 무지개탄에 연쇄되면 +2점." };
            _tooltip.text = card.Name + "  |  화약 " + card.Cost + " · 기본점수 " + card.BaseScore + " · 연기 " + card.Smoke + "\n" + descriptions[card.Id];
            _tooltip.gameObject.SetActive(true);
            _tooltip.transform.SetAsLastSibling();
        }

        /// <summary>앵커, 크기와 색상을 지정한 배경 패널을 생성한다.</summary>
        private Image Panel(string name, Vector2 anchor, Vector2 size, Vector2 position, Vector2 pivot, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(_canvas.transform, false);
            RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = position;
            Image image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }

        /// <summary>지정한 위치에 읽기 쉬운 UI 텍스트를 생성한다.</summary>
        private Text Label(string name, string value, int size, Vector2 anchor, Vector2 dimensions, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(_canvas.transform, false);
            RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = anchor; rect.anchorMax = anchor; rect.sizeDelta = dimensions;
            Text text = go.GetComponent<Text>(); text.text = value; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size; text.color = color; text.alignment = TextAnchor.MiddleCenter; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; text.raycastTarget = false;
            return text;
        }

        /// <summary>라벨과 버튼 동작을 갖춘 UI 버튼을 생성한다.</summary>
        private Button Button(string name, string value, Vector2 anchor, Vector2 dimensions, UnityEngine.Events.UnityAction action)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(_canvas.transform, false);
            RectTransform rect = go.GetComponent<RectTransform>(); rect.anchorMin = anchor; rect.anchorMax = anchor; rect.sizeDelta = dimensions;
            Image image = go.GetComponent<Image>(); image.color = new Color(0.12f, 0.2f, 0.34f, 0.96f);
            Button button = go.GetComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            Text label = Label(name + "Text", value, 18, anchor, dimensions - new Vector2(12f, 8f), Color.white); label.transform.SetParent(go.transform, false); label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = Vector2.zero; label.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        /// <summary>타이틀에서 튜토리얼 또는 시드 런을 시작한다.</summary>
        private void ShowTitle()
        {
            SetGameControls(false);
            SetHeader("마지막 밤의 불꽃술사", "카드를 순서대로 놓고 불꽃의 연쇄를 설계하세요.");
            Button("TutorialStart", "연습 모드", new Vector2(0.5f, 0.48f), new Vector2(300f, 72f), () => StartRun(true, Environment.TickCount));
            Button("NewStart", "새 런 시작", new Vector2(0.5f, 0.36f), new Vector2(300f, 72f), () => StartRun(false, Environment.TickCount));
            Button("Settings", "설정: 음량 / 섬광", new Vector2(0.5f, 0.24f), new Vector2(300f, 64f), Settings);
        }

        /// <summary>시드로 덱을 초기화하고 첫 공연을 연다.</summary>
        private void StartRun(bool tutorial, int seed)
        {
            ClearDynamicButtons(); _tutorial = tutorial; _practicePage = 0; _maxPowder = 4; _seed = seed; _show = 0; _score = 0; _smoke = 0; _discard.Clear(); _draw.Clear(); _hand.Clear(); _deck.Clear();
            SetGameControls(true);
            _deck.AddRange(new[] { 1,1,1,1,2,2,2,3,3,3,4,4 });
            if (tutorial) _deck.Clear();
            _draw.AddRange(_deck);
            if (!tutorial) Randomize(_draw, new Random(_seed));
            else _draw.AddRange(new[] { 1, 2, 4, 3, 1, 2, 3, 1, 2, 4, 1, 3 });
            BeginShow();
        }

        /// <summary>공연의 점수와 연기를 초기화하고 첫 턴을 준비한다.</summary>
        private void BeginShow()
        {
            _turn = 0; _score = 0; _smoke = 0;
            _threeColors = !_tutorial && _show == 1;
            SetHeader(_tutorial ? "연습 공연" : _showNames[_show], "목표 " + (_tutorial ? "없음" : _showGoals[_show] + "점") + " · 시드 " + _seed);
            BeginTurn();
        }

        /// <summary>턴 화약과 손패를 준비하고 UI를 갱신한다.</summary>
        private void BeginTurn()
        {
            Time.timeScale = 1f;
            if (_pauseButton != null) _pauseButton.GetComponentInChildren<Text>().text = "일시정지";
            _powder = _maxPowder; _selected = -1; _log.text = "카드를 목록에 끌어 놓으세요. 목록의 카드를 눌러 모드를 바꿉니다.";
            _hand.Clear();
            if (_tutorial) { for (int i = 1; i <= FireworkCatalog.Cards.Count; i++) _hand.Add(i); }
            else Draw(5);
            Array.Clear(_slots, 0, _slots.Length);
            UpdateUi();
        }

        /// <summary>덱과 버림 더미에서 최대 요청 수만큼 손패에 카드를 뽑는다.</summary>
        private void Draw(int count)
        {
            Random random = new Random(_seed + _show * 101 + _turn * 17 + _draw.Count + _discard.Count);
            for (int i = 0; i < count; i++)
            {
                if (_draw.Count == 0 && _discard.Count > 0) { _draw.AddRange(_discard); _discard.Clear(); Randomize(_draw, random); }
                if (_draw.Count == 0) break;
                int id = _draw[0]; _draw.RemoveAt(0); _hand.Add(id);
            }
        }

        /// <summary>카드 인덱스를 선택 상태로 두고 현재 손패를 다시 표시한다.</summary>
        private void SelectCard(int index) { if (_tutorial) index += _practicePage * 5; if (!_busy && index < _hand.Count) { _selected = index; UpdateUi(); } }

        /// <summary>연습 모드에서 다음 카드 목록을 표시한다.</summary>
        private void NextPracticePage() { if (!_busy) { _practicePage = (_practicePage + 1) % ((FireworkCatalog.Cards.Count + 4) / 5); _selected = -1; UpdateUi(); } }

        /// <summary>연습을 종료하고 시작 화면으로 돌아간다.</summary>
        private void ExitPractice() { if (!_busy) { _tutorial = false; ShowTitle(); } }

        /// <summary>연습 중 최대 화약과 현재 남은 화약을 1씩 늘린다.</summary>
        private void IncreasePracticePowder()
        {
            if (!_tutorial || _busy || _maxPowder >= MAX_PRACTICE_POWDER) return;
            _maxPowder++;
            _powder++;
            UpdateUi();
        }

        /// <summary>선택 카드를 발사관에 장전하거나 발사 모드를 순환한다.</summary>
        private void OnSlot(int index)
        {
            if (_busy) return;
            if (_selected >= 0 && _selected < _hand.Count) { AppendSelected(); return; }
            _focusedSlot = index;
            if (_slots[index] == null) return;
            PlannedCard current = _slots[index];
            if (current.Card.ChainBonus == 0) { _log.text = "이 카드는 직접 발사만 가능합니다."; return; }
            LaunchMode next = current.Mode == LaunchMode.Direct ? LaunchMode.Chain : LaunchMode.Direct;
            _slots[index] = new PlannedCard(current.Card, next, current.Order); UpdateUi();
        }

        /// <summary>선택한 손패 카드를 남은 화약을 확인해 목록 끝에 추가한다.</summary>
        private void AppendSelected()
        {
            int index = Array.FindIndex(_slots, item => item == null);
            CardDefinition card = FireworkCatalog.Get(_hand[_selected]);
            if (index < 0 || card.Cost > _powder) { _log.text = "화약이 부족하거나 목록이 가득 찼습니다."; return; }
            _powder -= card.Cost; _slots[index] = new PlannedCard(card, LaunchMode.Direct, index);
            if (!_tutorial) _hand.RemoveAt(_selected);
            _selected = -1; _tooltip.gameObject.SetActive(false); UpdateUi();
        }

        /// <summary>드래그한 손패 카드가 목록 위에 놓이면 목록 끝에 추가한다.</summary>
        private void DragFromHand(int index, UnityEngine.EventSystems.PointerEventData data)
        {
            if (_busy || !IsOverQueue(data)) return;
            int handIndex = index + (_tutorial ? _practicePage * 5 : 0);
            if (handIndex < _hand.Count) { _selected = handIndex; AppendSelected(); }
        }

        /// <summary>장전 카드를 목록 밖으로 드래그하면 손패로 회수한다.</summary>
        private void DragFromQueue(int index, UnityEngine.EventSystems.PointerEventData data)
        {
            if (_busy || IsOverQueue(data)) return;
            _focusedSlot = index; RecallSlot();
        }

        /// <summary>포인터가 장전 목록의 어느 위치 위에 있는지 반환한다.</summary>
        private bool IsOverQueue(UnityEngine.EventSystems.PointerEventData data)
        {
            foreach (Button button in _slotButtons)
                if (button.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(button.transform as RectTransform, data.position, data.pressEventCamera)) return true;
            return false;
        }

        /// <summary>선택 발사관의 장전 카드를 회수하고 화약을 반환한다.</summary>
        private void RecallSlot()
        {
            if (_busy || _focusedSlot < 0 || _slots[_focusedSlot] == null) return;
            PlannedCard planned = _slots[_focusedSlot];
            if (!_tutorial && _hand.Count >= 5) { _log.text = "손패가 가득 차 카드를 회수할 수 없습니다."; return; }
            if (!_tutorial) _hand.Add(planned.Card.Id); _powder += planned.Card.Cost;
            for (int i = _focusedSlot; i < _slots.Length - 1; i++)
            {
                PlannedCard next = _slots[i + 1];
                _slots[i] = next == null ? null : new PlannedCard(next.Card, next.Mode, i);
            }
            _slots[_slots.Length - 1] = null; _focusedSlot = -1; UpdateUi();
        }

        /// <summary>유효 계획을 계산해 폭발 연출과 점수 내역을 진행한다.</summary>
        private void Perform()
        {
            if (_busy) return;
            int powder = 0;
            for (int i = 0; i < _slots.Length; i++) if (_slots[i] != null) powder += _slots[i].Card.Cost;
            if (powder > _maxPowder) { _log.text = "최대 화약을 넘습니다."; return; }
            _busy = true; _skipPresentation = false;
            _tooltip.gameObject.SetActive(false);
            foreach (Button button in _slotButtons) button.gameObject.SetActive(false);
            TurnResult result = FireworkRules.Calculate(_slots, _smoke, _winds[_tutorial ? 0 : _show][Math.Min(_turn, 2)], _threeColors, _turn);
            StartCoroutine(PlayTurn(result));
        }

        /// <summary>결정된 폭발 순서를 재생하고 결과 상태를 다음 단계에 반영한다.</summary>
        private IEnumerator PlayTurn(TurnResult result)
        {
            _replayIds.Clear();
            bool hasRedAfterglow = false;
            foreach (ExplosionRecord explosion in result.Explosions)
            {
                _replayIds.Add(explosion.Card.Id);
                if (!_skipPresentation) CreateBurst(explosion.Slot, explosion.Card, hasRedAfterglow);
                if (explosion.Card.Color == FireworkColor.Red) hasRedAfterglow = true;
                _score += explosion.Score; _smoke = explosion.SmokeAfter;
                _log.text = explosion.Card.Name + "  +" + explosion.Score + "점  ·  연기 " + explosion.SmokeBefore + "→" + explosion.SmokeAfter;
                UpdateUi();
                if (!_skipPresentation) yield return new WaitForSeconds(0.65f / _speed);
            }
            bool wasSkipped = _skipPresentation;
            _skipPresentation = false;
            if (Time.timeScale == 0f) Time.timeScale = 1f;
            _score += result.AudienceBonus; _smoke = result.EndSmoke;
            if (!_tutorial) { foreach (int id in _hand) _discard.Add(id); foreach (PlannedCard slot in _slots) if (slot != null) _discard.Add(slot.Card.Id); }
            _turn++;
            _log.text = "관객 보너스 +" + result.AudienceBonus + " · 바람이 연기 " + _winds[_tutorial ? 0 : _show][_turn - 1] + " 제거 · 불발 " + result.Misfires.Count;
            UpdateUi(); yield return new WaitForSeconds(wasSkipped ? 0.1f : 1.4f / _speed);
            _busy = false;
            if (_tutorial && _turn >= 3) { foreach (Button button in _slotButtons) button.gameObject.SetActive(true); BeginShow(); yield break; }
            if (!_tutorial && _turn >= 3) { ResolveShow(); yield break; }
            foreach (Button button in _slotButtons) button.gameObject.SetActive(true);
            BeginTurn();
        }

        /// <summary>공연 성공 여부를 확인하고 보상 또는 런 결과를 연다.</summary>
        private void ResolveShow()
        {
            SetGameControls(false);
            if (_score < _showGoals[_show]) { EndRun(false); return; }
            if (_show >= 2) { EndRun(true); return; }
            _rewards.Clear(); List<int> choices = new List<int>(); for (int i = 1; i <= FireworkCatalog.Cards.Count; i++) choices.Add(i);
            Random random = new Random(_seed + _show * 991); Randomize(choices, random); _rewards.AddRange(choices.GetRange(0, 3));
            SetHeader("공연 성공 · 카드 보상", "카드 한 장을 골라 다음 공연 덱에 추가하거나 보상을 건너뛰세요.");
            for (int i = 0; i < 3; i++) { int index = i; CardDefinition card = FireworkCatalog.Get(_rewards[i]); AddDynamicButton("Reward" + i, card.Name + "\n화약 " + card.Cost + " · 점수 " + card.BaseScore, new Vector2(0.25f + 0.25f * i, 0.48f), new Vector2(260f, 130f), () => ClaimReward(index)); }
            AddDynamicButton("SkipReward", "보상 건너뛰기", new Vector2(0.5f, 0.28f), new Vector2(230f, 58f), () => ClaimReward(-1));
        }

        /// <summary>보상을 덱에 넣거나 건너뛰고 다음 공연을 시작한다.</summary>
        private void ClaimReward(int index)
        {
            ClearDynamicButtons(); SetGameControls(true); if (index >= 0) _deck.Add(_rewards[index]); _show++;
            _draw.Clear(); _discard.Clear(); _draw.AddRange(_deck); Randomize(_draw, new Random(_seed + _show * 997)); BeginShow();
        }

        /// <summary>런 결과를 표시하고 같은 시드 또는 새 시드 재시작을 제공한다.</summary>
        private void EndRun(bool won)
        {
            SetGameControls(false);
            SetHeader(won ? "축제의 마지막 밤 · 승리" : "공연 종료 · 목표 미달", "총점 " + _score + " · 덱 " + _deck.Count + "장 · 시드 " + _seed);
            AddDynamicButton("SameSeed", "같은 시드 다시 하기", new Vector2(0.5f, 0.45f), new Vector2(300f, 64f), () => StartRun(false, _seed));
            AddDynamicButton("NewSeed", "새 시드로 시작", new Vector2(0.5f, 0.33f), new Vector2(300f, 64f), () => StartRun(false, Environment.TickCount));
            AddDynamicButton("Title", "타이틀", new Vector2(0.5f, 0.21f), new Vector2(300f, 58f), () => { ClearDynamicButtons(); ShowTitle(); });
        }

        /// <summary>직전 턴의 폭발 연출을 규칙 상태를 바꾸지 않고 다시 재생한다.</summary>
        private void Replay()
        {
            if (_busy || _replayIds.Count == 0) { _log.text = "다시 볼 공연 기록이 없습니다."; return; }
            StartCoroutine(ReplayRoutine());
        }

        /// <summary>기록된 카드 모양만 다시 연출한다.</summary>
        private IEnumerator ReplayRoutine()
        {
            _busy = true;
            _tooltip.gameObject.SetActive(false);
            foreach (Button button in _slotButtons) button.gameObject.SetActive(false);
            bool hasRedAfterglow = false;
            foreach (int id in _replayIds) { CardDefinition card = FireworkCatalog.Get(id); CreateBurst(0, card, hasRedAfterglow); if (card.Color == FireworkColor.Red) hasRedAfterglow = true; yield return new WaitForSeconds(0.4f / _speed); }
            yield return new WaitForSeconds(1.8f / _speed);
            foreach (Button button in _slotButtons) button.gameObject.SetActive(true);
            _busy = false;
            UpdateUi();
        }

        /// <summary>설정 화면에서 재생 속도, 음량과 섬광 감소를 조정한다.</summary>
        private void Settings()
        {
            AddDynamicButton("Speed", "배속 " + _speed + "x", new Vector2(0.5f, 0.52f), new Vector2(300f, 58f), () => { _speed = _speed == 1f ? 2f : 1f; ClearDynamicButtons(); Settings(); });
            AddDynamicButton("Volume", "음량 " + Mathf.RoundToInt(_volume * 100f) + "%", new Vector2(0.5f, 0.41f), new Vector2(300f, 58f), () => { _volume = _volume <= 0f ? 0.6f : 0f; AudioListener.volume = _volume; ClearDynamicButtons(); Settings(); });
            AddDynamicButton("Flash", "섬광 감소 " + (_reducedFlash ? "켜짐" : "꺼짐"), new Vector2(0.5f, 0.3f), new Vector2(300f, 58f), () => { _reducedFlash = !_reducedFlash; ClearDynamicButtons(); Settings(); });
            AddDynamicButton("SettingsBack", "돌아가기", new Vector2(0.5f, 0.19f), new Vector2(300f, 58f), () => { ClearDynamicButtons(); ShowTitle(); });
        }

        /// <summary>공연 연출을 1배 또는 2배로 전환한다.</summary>
        private void ToggleSpeed()
        {
            _speed = _speed == 1f ? 2f : 1f;
            _speedButton.GetComponentInChildren<Text>().text = "연출 " + _speed + "×";
        }

        /// <summary>공연 연출을 일시정지하거나 다시 진행한다.</summary>
        private void TogglePause()
        {
            if (!_busy) return;
            bool isPaused = Time.timeScale == 0f;
            Time.timeScale = isPaused ? 1f : 0f;
            _pauseButton.GetComponentInChildren<Text>().text = isPaused ? "일시정지" : "계속";
        }

        /// <summary>현재 공연 연출 대기를 건너뛰고 같은 계산 결과를 바로 표시한다.</summary>
        private void SkipPresentation() { if (_busy) _skipPresentation = true; }

        /// <summary>발사관 위치와 카드 형태에 맞춘 색상 픽셀 폭발을 만든다.</summary>
        private void CreateBurst(int slot, CardDefinition card, bool hasRedAfterglow)
        {
            Color color = card.Color == FireworkColor.Red ? new Color(1f, 0.25f, 0.12f) : card.Color == FireworkColor.Green ? new Color(0.2f, 1f, 0.35f) : new Color(0.24f, 0.75f, 1f);
            Vector2 origin = new Vector2(0f, -2.5f);
            GameObject lift = new GameObject("PixelFirework"); lift.transform.position = origin; _fireworks.Add(lift);
            SpriteRenderer core = lift.AddComponent<SpriteRenderer>(); core.sprite = PixelSprite(color); core.sortingOrder = 2;
            PlayEffect(_launchSound, 1f);
            StartCoroutine(LiftAndBurst(lift, slot, card, color, hasRedAfterglow));
        }

        /// <summary>폭죽을 상승시키고 형태별로 분산되는 픽셀 불씨를 만든다.</summary>
        private IEnumerator LiftAndBurst(GameObject lift, int slot, CardDefinition card, Color color, bool hasRedAfterglow)
        {
            Vector3 start = lift.transform.position; float duration = 0.42f / _speed; float time = 0f;
            while (time < duration && lift != null) { time += Time.deltaTime; lift.transform.position = Vector3.Lerp(start, new Vector3(start.x, 1.1f, 0f), time / duration); yield return null; }
            if (lift == null) yield break;
            Vector3 center = lift.transform.position; Destroy(lift, 1.6f / _speed);
            PlayEffect(_burstSound, 1f + slot * 0.14f);
            if (card.Id == 9)
            {
                EmitBurst(center + new Vector3(-1.3f, 0f, 0f), card, color, 1.1f, hasRedAfterglow);
                EmitBurst(center + new Vector3(1.3f, 0f, 0f), card, color, 1.1f, hasRedAfterglow);
                EmitBurst(center + new Vector3(0f, 1f, 0f), card, color, 1.3f, hasRedAfterglow);
            }
            else EmitBurst(center, card, color, 2f, hasRedAfterglow);
            if (card.Id == 8) { yield return new WaitForSeconds(0.25f / _speed); EmitBurst(center, card, color, 1.3f, hasRedAfterglow); }
        }

        /// <summary>지정한 중심과 크기에서 카드 형태에 맞는 불씨를 생성한다.</summary>
        private void EmitBurst(Vector3 center, CardDefinition card, Color color, float scale, bool hasRedAfterglow)
        {
            int count = card.Id == 4 || card.Id == 9 ? 60 : 48;
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f;
                float shape = card.Id == 3 || card.Id == 6 || card.Id == 8 ? 1f : card.Id == 2 || card.Id == 7 || card.Id == 10 || card.Id == 13 ? (i % 10 < 3 ? 1f : 0.48f) : card.Id == 5 ? Mathf.Max(0f, Mathf.Sin(angle)) : 0.8f + 0.2f * Mathf.Sin(i * 9f);
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * shape;
                GameObject spark = new GameObject("PixelSpark"); spark.transform.position = center; spark.transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                spark.transform.localScale = new Vector3(card.Id == 4 ? 1.6f : 1.0f, 0.16f, 1f);
                Color sparkColor = card.Id == 7 && i % 3 == 0 ? Color.green : card.Id == 7 && i % 3 == 1 ? Color.red : color;
                if (card.Id == 13 && hasRedAfterglow && i % 2 == 0) sparkColor = new Color(1f, 0.25f, 0.12f);
                SpriteRenderer renderer = spark.AddComponent<SpriteRenderer>(); renderer.sprite = _sparkSprite; renderer.color = sparkColor; renderer.sortingOrder = 3;
                StartCoroutine(AnimateSpark(spark, direction * (card.Id == 3 || card.Id == 6 || card.Id == 8 ? 2.5f : 2.2f + (i % 7) * 0.08f) * scale, sparkColor, card.Color == FireworkColor.Red)); _fireworks.Add(spark);
            }
        }

        /// <summary>픽셀 불씨를 확산과 낙하 뒤 잔광으로 소멸시킨다.</summary>
        private IEnumerator AnimateSpark(GameObject spark, Vector2 velocity, Color color, bool isRed)
        {
            float life = (isRed ? 2.1f : 1.35f) / _speed, time = 0f; Vector3 origin = spark.transform.position;
            while (time < life && spark != null)
            {
                time += Time.deltaTime; float t = time / life;
                spark.transform.position = origin + (Vector3)(velocity * t) + Vector3.down * (t * t * 0.9f);
                SpriteRenderer renderer = spark.GetComponent<SpriteRenderer>(); renderer.color = Color.Lerp(color, new Color(color.r * 0.2f, color.g * 0.2f, color.b * 0.25f, 0f), t);
                yield return null;
            }
            if (spark != null) Destroy(spark);
        }

        /// <summary>선명한 단일 픽셀 스프라이트를 생성한다.</summary>
        private Sprite PixelSprite(Color color)
        {
            Texture2D texture = new Texture2D(3, 3, TextureFormat.RGBA32, false); texture.filterMode = FilterMode.Point; texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[9]; for (int i = 0; i < pixels.Length; i++) pixels[i] = color; texture.SetPixels(pixels); texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 3f, 3f), new Vector2(0.5f, 0.5f), 24f);
        }

        /// <summary>별, 달, 산 능선과 수면이 있는 640×360 픽셀 밤하늘 배경을 만든다.</summary>
        private void CreatePixelNightSky()
        {
            const int width = 640;
            const int height = 360;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float blend = Mathf.Clamp01(y / 360f);
                Color32 color = Color32.Lerp(new Color32(8, 12, 36, 255), new Color32(27, 24, 64, 255), blend);
                for (int x = 0; x < width; x++) pixels[y * width + x] = color;
            }
            Random random = new Random(90210);
            for (int i = 0; i < 220; i++)
            {
                int x = random.Next(width); int y = random.Next(110, height);
                pixels[y * width + x] = i % 6 == 0 ? new Color32(143, 169, 218, 255) : new Color32(78, 99, 154, 255);
            }
            int moonX = 515, moonY = 268, radius = 22;
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                    if (x * x + y * y <= radius * radius && moonX + x < width && moonY + y < height)
                        pixels[(moonY + y) * width + moonX + x] = new Color32(238, 223, 174, 255);
            for (int x = 0; x < width; x++)
            {
                int ridge = 51 + Mathf.RoundToInt(Mathf.Sin(x * 0.023f) * 12f + Mathf.Sin(x * 0.071f) * 5f);
                for (int y = 0; y < ridge; y++) pixels[y * width + x] = y < 32 ? new Color32(8, 17, 47, 255) : new Color32(20, 28, 65, 255);
                if (x % 13 == 0) for (int y = 5; y < 13; y++) pixels[y * width + x] = new Color32(48, 76, 120, 255);
            }
            texture.SetPixels32(pixels); texture.Apply();
            Sprite sky = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 36f);
            GameObject background = new GameObject("PixelNightSky"); background.transform.position = new Vector3(0f, 0f, 2f);
            SpriteRenderer backgroundRenderer = background.AddComponent<SpriteRenderer>(); backgroundRenderer.sprite = sky; backgroundRenderer.sortingOrder = -10;
        }

        /// <summary>발사와 개화에 사용할 짧은 합성 음향 클립을 만든다.</summary>
        private AudioClip CreateTone(string clipName, float frequency, float duration)
        {
            const int sampleRate = 22050;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float progress = i / (float)sampleCount;
                float envelope = Mathf.Pow(1f - progress, 2f);
                float sweep = frequency * (1f - progress * 0.28f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * sweep * i / sampleRate) * envelope * 0.3f;
            }
            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>현재 음량과 섬광 설정을 적용해 효과음을 재생한다.</summary>
        private void PlayEffect(AudioClip clip, float pitch)
        {
            _audioSource.volume = _volume;
            _audioSource.pitch = pitch;
            if (_volume > 0f) _audioSource.PlayOneShot(clip);
        }

        /// <summary>손패, 발사관, 연기와 예상 결과를 현재 런 상태로 갱신한다.</summary>
        private void UpdateUi()
        {
            int wind = _winds[_tutorial ? 0 : _show][Math.Min(_turn, 2)];
            _summary.text = "공연 " + (_show + 1) + "/3 · " + (_turn == 0 ? "개막" : _turn == 2 ? "피날레" : "2턴") + " · 점수 " + _score + "/" + (_tutorial ? "연습" : _showGoals[_show].ToString()) + " · 연기 " + _smoke + "/5 · 바람 " + (wind == 2 ? "강풍" : "보통") + " (다음 턴 연기 -" + wind + ") · 화약 " + _powder + "/" + _maxPowder;
            for (int i = 0; i < _slots.Length; i++)
            {
                _slotButtons[i].gameObject.SetActive(!_busy && (_slots[i] != null || i == 0 || _slots[i - 1] != null));
                string value = (i + 1) + "번\n";
                if (_slots[i] != null) value += _slots[i].Card.Name + "\n" + ModeName(_slots[i].Mode) + " · " + (_slots[i].Order + 1);
                else value += "여기에 추가";
                _slotButtons[i].GetComponentInChildren<Text>(true).text = value;
            }
            for (int i = 0; i < _handButtons.Count; i++)
            {
                int cardIndex = i + (_tutorial ? _practicePage * 5 : 0);
                bool visible = cardIndex < _hand.Count; _handButtons[i].gameObject.SetActive(visible);
                if (visible) { CardDefinition card = FireworkCatalog.Get(_hand[cardIndex]); _handButtons[i].GetComponentInChildren<Text>().text = card.Name + "\n화약 " + card.Cost + " · 점수 " + card.BaseScore + " · 연기 " + card.Smoke + (cardIndex == _selected ? "\n선택됨" : ""); }
            }
            if (!_busy)
            {
                TurnResult preview = FireworkRules.Calculate(_slots, _smoke, wind, _threeColors, _turn);
                _log.text = "예상 " + preview.TotalScore + "점 · 불발 " + preview.Misfires.Count + " · 턴 종료 연기 " + preview.EndSmoke;
            }
        }

        /// <summary>턴 상태와 점수 정보를 헤더에 반영한다.</summary>
        private void SetHeader(string title, string detail) { _status.text = title; _summary.text = detail; }
        /// <summary>발사 모드의 한국어 표시명을 반환한다.</summary>
        private string ModeName(LaunchMode mode) { return mode == LaunchMode.Direct ? "직접" : "연쇄"; }

        /// <summary>현재 장면에 추가된 메뉴 버튼을 이름 접두사로 정리한다.</summary>
        private void ClearDynamicButtons()
        {
            for (int i = _canvas.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = _canvas.transform.GetChild(i);
                if (child.name.StartsWith("TutorialStart") || child.name.StartsWith("NewStart") || child.name.StartsWith("Settings") || child.name.StartsWith("Reward") || child.name.StartsWith("SkipReward") || child.name.StartsWith("SameSeed") || child.name.StartsWith("NewSeed") || child.name == "Title" || child.name == "Speed" || child.name == "Volume" || child.name == "Flash") Destroy(child.gameObject);
            }
        }

        /// <summary>메뉴 액션용 버튼을 생성해 화면에 연결한다.</summary>
        private void AddDynamicButton(string name, string text, Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction action) { Button(name, text, anchor, size, action); }

        /// <summary>타이틀과 메뉴에서 플레이 전용 발사관과 손패 조작을 표시하거나 숨긴다.</summary>
        private void SetGameControls(bool isVisible)
        {
            foreach (Button button in _slotButtons) if (button != null) button.gameObject.SetActive(isVisible);
            foreach (Button button in _handButtons) if (button != null) button.gameObject.SetActive(isVisible);
            if (_performButton != null) _performButton.gameObject.SetActive(isVisible);
            if (_replayButton != null) _replayButton.gameObject.SetActive(isVisible);
            if (_recallButton != null) _recallButton.gameObject.SetActive(isVisible);
            if (_speedButton != null) _speedButton.gameObject.SetActive(isVisible);
            if (_pauseButton != null) _pauseButton.gameObject.SetActive(isVisible);
            if (_skipButton != null) _skipButton.gameObject.SetActive(isVisible);
            if (_practicePageButton != null) _practicePageButton.gameObject.SetActive(isVisible && _tutorial);
            if (_practiceExitButton != null) _practiceExitButton.gameObject.SetActive(isVisible && _tutorial);
            if (_practicePowderButton != null) _practicePowderButton.gameObject.SetActive(isVisible && _tutorial);
            if (_log != null) _log.gameObject.SetActive(isVisible);
        }

        /// <summary>고정 시드 난수로 리스트를 섞는다.</summary>
        private void Randomize<T>(IList<T> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--) { int j = random.Next(i + 1); T value = list[i]; list[i] = list[j]; list[j] = value; }
        }
    }

    /// <summary>카드 드래그가 끝난 화면 위치를 소유 화면에 전달한다.</summary>
    public sealed class FireworksDrag : MonoBehaviour, UnityEngine.EventSystems.IBeginDragHandler, UnityEngine.EventSystems.IDragHandler, UnityEngine.EventSystems.IEndDragHandler
    {
        private GameObject _ghost;
        public Action<UnityEngine.EventSystems.PointerEventData> OnDrop { get; set; }

        /// <summary>드래그 중 커서를 따라갈 카드 표시를 만든다.</summary>
        public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            _ghost = new GameObject("DraggedCard", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            _ghost.transform.SetParent(canvas.transform, false);
            RectTransform rect = _ghost.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(180f, 70f);
            UnityEngine.UI.Image background = _ghost.GetComponent<UnityEngine.UI.Image>(); background.color = new Color(0.25f, 0.38f, 0.58f, 0.85f); background.raycastTarget = false;
            GameObject label = new GameObject("Label", typeof(RectTransform), typeof(UnityEngine.UI.Text)); label.transform.SetParent(_ghost.transform, false);
            RectTransform labelRect = label.GetComponent<RectTransform>(); labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one; labelRect.offsetMin = Vector2.zero; labelRect.offsetMax = Vector2.zero;
            UnityEngine.UI.Text source = GetComponentInChildren<UnityEngine.UI.Text>(); UnityEngine.UI.Text text = label.GetComponent<UnityEngine.UI.Text>();
            text.font = source.font; text.text = source.text; text.fontSize = 16; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false;
            OnDrag(eventData);
        }
        /// <summary>드래그 표시를 현재 포인터 위치로 이동한다.</summary>
        public void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (_ghost == null) return;
            RectTransform parent = _ghost.transform.parent as RectTransform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out Vector2 point)) _ghost.GetComponent<RectTransform>().anchoredPosition = point;
        }
        /// <summary>드래그 표시를 지우고 놓은 위치를 처리기에 전달한다.</summary>
        public void OnEndDrag(UnityEngine.EventSystems.PointerEventData eventData) { if (_ghost != null) Destroy(_ghost); OnDrop?.Invoke(eventData); }

        private void OnDisable() { if (_ghost != null) Destroy(_ghost); }
    }
}
