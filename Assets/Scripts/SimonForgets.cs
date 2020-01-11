using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using System.Text.RegularExpressions;
using Rnd = UnityEngine.Random;
public class SimonForgets : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public TextMesh counterScreen;
    public MeshRenderer[] leds;
    public KMSelectable[] buttons;
    public Material[] colorMats;
    public Material[] opaqueMats;
    enum Colors
    {
        Purple, // Violet
        Magenta,
        Blue,
        Cyan,
        Green,
        Yellow,
        Orange,
        Red,
        Pink,
        White
    }
    private const string _vowels = "AEIOUY";
    private static int _moduleIdCounter = 0;
    private int _moduleId;
    private bool _moduleSolved = false;
    private static string[] _ignoredModules = null;
    private int _tick = 0;
    private int _stageCounter = 0;
    private int _solvedModules;
    private int _solvableModulesCount = 1; // Update is called before Activate
    private List<Colors> _colorOrder;
    private List<Colors> _flashingColors = new List<Colors>();
    private List<Colors> _onLeds = new List<Colors>();
    private List<Colors> _answerS1;
    private bool _waitS1;
    private Colors[][] _answersS2;
    private int _answersLength = 0;
    private bool _waitForAnswer = false;
    private int _answerIndex = 0;
    private readonly int[,] _stage10Table = new int[,] {
        {+0,+5,-3,+2,+1,+2,-3,+3,+1,-4},
        {+5,+0,+1,+4,-3,+0,-2,+2,-1,+2},
        {-3,+1,+0,-1,+2,-1,-5,+4,+1,+0},
        {+2,+4,-1,+0,+0,+3,+6,-4,+5,-1},
        {+1,-3,+2,+0,+0,-5,+2,+0,-2,+4},
        {+2,+0,-1,+3,-5,+0,+4,+1,+0,-5},
        {-3,-2,-5,+6,+2,+4,+0,-1,+3,+2},
        {+3,+2,+4,-4,+0,+1,-1,+0,+1,-2},
        {+1,-1,+1,+5,-2,+0,+3,+1,+0,+1},
        {-4,+2,+0,-1,+4,-5,+2,-2,+1,+0}
    };

    void Awake()
    {
        _moduleId = _moduleIdCounter++;
        if (_ignoredModules == null)
        {
            _ignoredModules = GetComponent<KMBossModule>().GetIgnoredModules("Simon Forgets", new string[] {
                "14",
                "Cruel Purgatory",
                "Cookie Jars",
                "Divided Squares",
                "Forget Enigma",
                "Forget Everything",
                "Forget It Not",
                "Forget Me Later",
                "Forget Me Not",
                "Forget Perspective",
                "Forget Them All",
                "Forget This",
                "Forget Us Not",
                "Hogwarts",
                "Organization",
                "Purgatory",
                "Simon's Stages",
                "Simon Forgets",
                "Souvenir",
                "Tallordered Keys",
                "The Swan",
                "The Time Keeper",
                "Timing is Everything",
                "The Troll",
                "Turn The Key",
                "The Very Annoying Button",
                "Ultimate Custom Night",
                "Übermodule"
            });
        }
        Debug.LogFormat("[Simon Forgets #{0}] Ignored modules: {1}", _moduleId, printStringArray(_ignoredModules));

        for (int i = 0; i < buttons.Length; ++i)
        {
            int j = i;
            buttons[i].OnInteract += delegate { buttonPress(j); return false; };
        }

    }

    void Start()
    {
        Module.OnActivate += Activate;
        // scale lights depending on bomb casing
        float scalar = transform.lossyScale.x;
        foreach (KMSelectable button in buttons)
            button.GetComponentInChildren<Light>().range *= scalar;
        foreach (MeshRenderer led in leds)
            led.GetComponentInChildren<Light>().range *= scalar;
    }

    void Activate()
    {
        List<string> solvableModules = Bomb.GetSolvableModuleNames().Where(x => !_ignoredModules.Contains(x)).ToList();
        Debug.LogFormat("[Simon Forgets #{0}] Found {1} solvable modules", _moduleId, solvableModules.Count);
        _solvableModulesCount = solvableModules.Count;
        _answersS2 = new Colors[_solvableModulesCount][];
    }

    string printStringList<T>(List<T> list)
    {
        string str = "{ ";
        if (list.Count == 1) return str + list[0] + " }";
        for (int i = 0; i < list.Count - 1; ++i)
        {
            str += list[i] + ", ";
        }
        return str + list.Last() + " }";
    }

    string printStringArray<T>(T[] array)
    {
        string str = "{ ";
        if (array.Length == 1) return str + array[0] + " }";
        for (int i = 0; i < array.Length - 1; ++i)
        {
            str += array[i] + ", ";
        }
        return str + array.Last() + " }";
    }

    IEnumerator inputting()
    {
        turnOffButtons();
        yield return new WaitForSeconds(5f);

        if (_waitS1)
        {
            _answerIndex = 0;
            updateModuleVisuals();
            StartCoroutine(colorFlash());
        }
    }

    void buttonPress(int i)
    {
        buttons[i].AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
        if (!_moduleSolved && (_waitForAnswer || _waitS1))
        {
            Debug.LogFormat("[Simon Forgets #{0}] Button {1} pressed", _moduleId, _colorOrder[i]);

            Colors expected;
            if (_waitForAnswer) // final answer
                expected = getNthElement(_answersS2, _answerIndex);
            else // section 1 answer
                expected = _answerS1[_answerIndex];

            StopAllCoroutines(); // stop colour flash  
            StartCoroutine(inputting()); // delay for input

            if (expected == _colorOrder[i])
            {
                _answerIndex++;
                if (_waitS1)
                {
                    if (_answerIndex == _answerS1.Count)
                    {
                        _waitS1 = false;
                        _answerIndex = 0;
                        turnOffLeds();
                    }
                }
                else if (_answerIndex == _answersLength)
                {
                    Module.HandlePass();
                    _moduleSolved = true;
                }
            }
            else
            {
                Debug.LogFormat("[Simon Forgets #{0}] Strike: expected {1}", _moduleId, expected);
                Module.HandleStrike();
                int index;
                if (_waitS1)
                {
                    index = _colorOrder.IndexOf(_answerS1[_answerIndex]);
                    _answerIndex = 0; // reset current stage if not final answer
                }
                else
                {
                    index = _colorOrder.IndexOf(getNthElement(_answersS2, _answerIndex));
                }
                StartCoroutine(showCorrectColor(index));
            }
        }
        else
        {
            Debug.LogFormat("[Simon Forgets #{0}] Strike: input received while not waiting for an answer", _moduleId);
            Module.HandleStrike();
        }
    }

    //coroutine flashing colours
    IEnumerator colorFlash()
    {
        while (!_moduleSolved && (!_waitForAnswer || _waitS1))
        {
            foreach (Colors c in _flashingColors)
            {
                Light light = buttons[_colorOrder.IndexOf(c)].GetComponentInChildren<Light>();
                // ON
                light.enabled = true;
                yield return new WaitForSeconds(0.4f);
                // OFF
                light.enabled = false;
                yield return new WaitForSeconds(0.4f);
            }
            //pause before restart
            yield return new WaitForSeconds(2f);
        }
    }

    IEnumerator showCorrectColor(int _index)
    {
        Light light = buttons[_index].GetComponentInChildren<Light>();
        // ON
        light.enabled = true;
        yield return new WaitForSeconds(1f);
        // OFF
        light.enabled = false;
    }

    void updateModuleVisuals()
    {
        turnOffLeds();
        turnOffButtons();
        for (int i = 0; i < 10; ++i)
        {
            buttons[i].GetComponent<Renderer>().material = opaqueMats[(int)_colorOrder[i]];
            buttons[i].GetComponentInChildren<Light>().color = colorMats[(int)_colorOrder[i]].color;
        }
        if (!_waitForAnswer)
        {
            foreach (Colors c in _onLeds)
                leds[(int)c].GetComponentInChildren<Light>().enabled = true;
        }
        // update stage number
        if (_waitForAnswer)
            counterScreen.text = "";
        else
            counterScreen.text = _stageCounter.ToString().PadLeft(3, '0');
    }
    void turnOffLeds()
    {
        foreach (MeshRenderer m in leds)
            m.GetComponentInChildren<Light>().enabled = false;
    }

    void turnOffButtons()
    {
        for (int i = 0; i < 10; ++i)
            buttons[i].GetComponentInChildren<Light>().enabled = false;
    }

    void Update()
    {
        if (_moduleSolved || _answersS2 == null)
            return;
        if (++_tick == 5)
        {
            _tick = 0;
            if (_solvableModulesCount <= 1)
            {
                Debug.LogFormat("[Simon Forgets #{0}] Not enough solvable modules: autosolving", _moduleId);
                Module.HandlePass();
                _moduleSolved = true;
                return;
            }

            // count solved modules minus ignored
            List<String> solves = Bomb.GetSolvedModuleNames().ToList();
            foreach (String d in _ignoredModules)
                solves.Remove(d);
            foreach (KMBombModule d in TwitchAbandonModule)
                solves.Remove(d.ModuleDisplayName);

            // same state
            if (_solvedModules == solves.Count)
                return;
            _solvedModules = solves.Count;

            if (_waitS1) // expected s1 before solving another module
            {
                Debug.LogFormat("[Simon Forgets #{0}] Strike: other module solved before entering Stage Sequence", _moduleId);
                Module.HandleStrike();
                if (_solvedModules != _solvableModulesCount)
                    return; // keep current solution
            }
            if (_solvedModules == _solvableModulesCount)
            {
                // remove stages not set due to strikes (solving 2 modules in a row)
                _answersS2 = _answersS2.Where(c => c != null).ToArray();
                foreach (Colors[] colors in _answersS2)
                    _answersLength += colors.Length;
                _waitForAnswer = true;
                _waitS1 = false;
                generateStage(); // change button position
                updateModuleVisuals();
                Debug.LogFormat("[Simon Forgets #{0}] Waiting for answer: {1}", _moduleId, printStringList(_answersS2.SelectMany(e => e).ToList()));
                return;
            }

            _stageCounter++;
            generateStage();
            updateModuleVisuals();
            _waitS1 = true;
            StartCoroutine(colorFlash());
        }
    }

    void generateStage()
    {
        StopAllCoroutines();

        // reset previous variables
        _colorOrder = Enum.GetValues(typeof(Colors)).Cast<Colors>().ToList();
        List<Colors> colors = new List<Colors>(_colorOrder);
        // pick new button colors and shuffle them
        for (int i = 0; i < _colorOrder.Count; ++i)
        {
            Colors tmp = _colorOrder[i];
            int index = Rnd.Range(i, _colorOrder.Count);
            _colorOrder[i] = _colorOrder[index];
            _colorOrder[index] = tmp;
        }
        if (_waitForAnswer)
            Debug.LogFormat("[Simon Forgets #{0}] Color order for the final answer: {1}", _moduleId, printStringList(_colorOrder));
        else
            Debug.LogFormat("[Simon Forgets #{0}] Color order for stage {1}: {2}", _moduleId, _stageCounter, printStringList(_colorOrder));
        // exit after picking new button positions
        if (_waitForAnswer)
            return;

        _flashingColors.Clear();
        int nColors = getNumberOfColors();
        for (int i = 0; i < nColors; ++i)
            _flashingColors.Add((Colors)Rnd.Range(0, 10));
        Debug.LogFormat("[Simon Forgets #{0}] Flashing colors: {1}", _moduleId, printStringList(_flashingColors));

    leds:
        // pick new led colors
        _onLeds.Clear();
        int nLeds = Rnd.Range(1, 11);
        for (int i = 0; i < nLeds; ++i)
        {
            int index = Rnd.Range(0, colors.Count);
            _onLeds.Add(colors[index]);
            colors.RemoveAt(index);
        }
        // special case for the tenth stage, need exactly two lit leds
        if (_stageCounter == 10 && _onLeds.Count != 2)
        {
            if (_onLeds.Count < 2)
            {
                goto leds;
            }
            while (_onLeds.Count > 2) _onLeds.RemoveAt(0);
        }
        // special case for stages >= 12 and leds conditions leading to stage 10 rules (2 lit leds)
        if (!_onLeds.Contains(Colors.White) && !_onLeds.Contains(Colors.Yellow) && !_onLeds.Contains(Colors.Pink)
            && !_onLeds.Contains(Colors.Magenta) && !_onLeds.Contains(Colors.Red) && !_onLeds.Contains(Colors.Blue)
            && _onLeds.Contains(Colors.Green))
            goto leds;

        Debug.LogFormat("[Simon Forgets #{0}] Leds ON: {1}", _moduleId, printStringList(_onLeds));
        // append solution
        List<Colors> stageSeqence = computeStageSequence();
        Debug.LogFormat("[Simon Forgets #{0}] Computed Stage Sequence Section 1: {1}", _moduleId, printStringList(stageSeqence));

        _answerS1 = new List<Colors>(stageSeqence);

        computeCurrentStageSolution(ref stageSeqence);
        Debug.LogFormat("[Simon Forgets #{0}] Computed Stage Sequence Section 2: {1}", _moduleId, printStringList(stageSeqence));

        _answersS2[_stageCounter - 1] = new Colors[stageSeqence.Count];
        stageSeqence.ToArray().CopyTo(_answersS2[_stageCounter - 1], 0);
    }

    int getNumberOfColors()
    {
        if (_stageCounter <= 2)
            return 5;
        if (_stageCounter <= 4)
            return 4;
        return 3;
    }

    List<Colors> computeStageSequence()
    {
        List<Colors> sequence = new List<Colors>();
        foreach (Colors color in _flashingColors)
        {
            switch (color)
            {
                case Colors.Purple:
                    if (isTopRow(Colors.Green)) // Green top row
                    {
                        if (_onLeds.Contains(Colors.Yellow)) // Yellow led lit
                        {
                            if (_stageCounter % 2 == 0) // Stage number even
                                sequence.Add(Colors.Red);
                            else
                                sequence.Add(Colors.Magenta);
                        }
                        else
                        {
                            if (_stageCounter % 2 == 1)
                                sequence.Add(Colors.Purple); // Stage number odd
                            else
                                sequence.Add(Colors.Blue);
                        }
                    }
                    else
                    {
                        if (_onLeds.Contains(Colors.Magenta))
                        {
                            if (Bomb.GetSolvableModuleNames().Count % 2 == 1) // odd nb of solvable modules
                                sequence.Add(Colors.Green);
                            else
                                sequence.Add(Colors.Orange);
                        }
                        else
                        {
                            if (Bomb.GetSolvableModuleNames().Count % 2 == 1) // odd nb of solvable modules
                                sequence.Add(Colors.Pink);
                            else
                                sequence.Add(Colors.White);
                        }
                    }
                    break;
                case Colors.Magenta:
                    if (!_onLeds.Contains(Colors.White)) // White led unlit
                    {
                        if (isBottomRow(Colors.Green)) // Green bottom row
                        {
                            if (Bomb.GetSerialNumberNumbers().Last() % 2 == 0) // last digit even
                                sequence.Add(Colors.Yellow);
                            else
                                sequence.Add(Colors.Red);
                        }
                        else
                        {
                            if (Bomb.GetSerialNumberNumbers().Last() % 2 == 1) // last digit odd
                                sequence.Add(Colors.Magenta);
                            else
                                sequence.Add(Colors.Cyan);
                        }
                    }
                    else
                    {
                        if (isTopRow(Colors.Purple)) // Purple top row
                        {
                            if (Bomb.GetSolvedModuleNames().Count % 2 == 0) // solved even
                                sequence.Add(Colors.Purple);
                            else
                                sequence.Add(Colors.Blue);
                        }
                        else
                        {
                            if (Bomb.GetSolvedModuleNames().Count % 2 == 1) // solved odd
                                sequence.Add(Colors.Pink);
                            else
                                sequence.Add(Colors.White);
                        }
                    }
                    break;
                case Colors.Blue:
                    if (!_onLeds.Contains(Colors.Green)) // Green led unlit
                    {
                        if (isBottomRow(Colors.Cyan)) // Cyan bottom row
                        {
                            if (Bomb.GetSerialNumberNumbers().Last() % 2 == 0) // Even last digit
                                sequence.Add(Colors.Blue);
                            else
                                sequence.Add(Colors.Red);
                        }
                        else
                        {
                            if (Bomb.GetSerialNumberNumbers().Last() % 2 == 1) // Odd last digit
                                sequence.Add(Colors.Orange);
                            else
                                sequence.Add(Colors.Yellow);
                        }
                    }
                    else
                    {
                        if (isTopRow(Colors.White)) // White top row
                        {
                            if (Bomb.GetSolvedModuleNames().Count % 2 == 0) // even solved modules
                                sequence.Add(Colors.Cyan);
                            else
                                sequence.Add(Colors.Green);
                        }
                        else
                        {
                            if (Bomb.GetSolvedModuleNames().Count % 2 == 1) // odd solved modules
                                sequence.Add(Colors.White);
                            else
                                sequence.Add(Colors.Orange);
                        }
                    }
                    break;
                case Colors.Cyan:
                    if (isTopRow(Colors.Red)) // Red top row
                    {
                        if (_onLeds.Contains(Colors.Pink)) // Pink led lit
                        {
                            if (_stageCounter % 2 == 0) // stage even
                                sequence.Add(Colors.Orange);
                            else
                                sequence.Add(Colors.Pink);
                        }
                        else
                        {
                            if (_stageCounter % 2 == 1) // stage odd
                                sequence.Add(Colors.Yellow);
                            else
                                sequence.Add(Colors.Green);
                        }
                    }
                    else
                    {
                        if (_onLeds.Contains(Colors.Blue))
                        {
                            if (Bomb.GetSolvableModuleNames().Count % 2 == 1) // Odd solvable modules
                                sequence.Add(Colors.Blue);
                            else
                                sequence.Add(Colors.White);
                        }
                        else
                        {
                            if (Bomb.GetSolvableModuleNames().Count % 2 == 1) // Odd solvable modules
                                sequence.Add(Colors.Magenta);
                            else
                                sequence.Add(Colors.Pink);
                        }
                    }
                    break;
                case Colors.Green:
                    if (!_onLeds.Contains(Colors.Cyan)) // cyan led unlit
                    {
                        if (isBottomRow(Colors.Red)) // red bottom row
                        {
                            if (Bomb.GetSerialNumberNumbers().Last() % 2 == 0) // last digit even
                                sequence.Add(Colors.Cyan);
                            else
                                sequence.Add(Colors.Purple);
                        }
                        else
                        {
                            if (Bomb.GetSerialNumberNumbers().Last() % 2 == 1) // last digit odd
                                sequence.Add(Colors.Blue);
                            else
                                sequence.Add(Colors.Pink);
                        }
                    }
                    else
                    {
                        if (isTopRow(Colors.Yellow)) // yellow top
                        {
                            if (Bomb.GetSolvedModuleNames().Count % 2 == 0) // even solved modules
                                sequence.Add(Colors.White);
                            else
                                sequence.Add(Colors.Red);
                        }
                        else
                        {
                            if (Bomb.GetSolvedModuleNames().Count % 2 == 1) // odd solved modules
                                sequence.Add(Colors.Green);
                            else
                                sequence.Add(Colors.Magenta);
                        }
                    }
                    break;
                case Colors.Yellow:
                    if (isTopRow(Colors.White)) // white top row
                    {
                        if (_onLeds.Contains(Colors.Green)) // green led lit
                        {
                            if (_stageCounter % 2 == 0) // stage even
                                sequence.Add(Colors.Blue);
                            else
                                sequence.Add(Colors.Green);
                        }
                        else
                        {
                            if (_stageCounter % 2 == 1) // stage odd
                                sequence.Add(Colors.Orange);
                            else
                                sequence.Add(Colors.Pink);
                        }
                    }
                    else
                    {
                        if (_onLeds.Contains(Colors.Purple)) // purple lit
                        {
                            if (Bomb.GetSolvableModuleNames().Count % 2 == 1) // odd solvable modules
                                sequence.Add(Colors.White);
                            else
                                sequence.Add(Colors.Yellow);
                        }
                        else
                        {
                            if (Bomb.GetSolvableModuleNames().Count % 2 == 1) // odd solvable modules
                                sequence.Add(Colors.Red);
                            else
                                sequence.Add(Colors.Magenta);
                        }
                    }
                    break;
                case Colors.Orange:
                    if (!_onLeds.Contains(Colors.Blue)) // blue led unlit
                    {
                        if (isBottomRow(Colors.Green)) // green bottom row
                        {
                            if (Bomb.GetSerialNumberNumbers().Last() % 2 == 0) // last digit even
                                sequence.Add(Colors.Purple);
                            else
                                sequence.Add(Colors.Yellow);
                        }
                        else
                        {
                            if (Bomb.GetSerialNumberNumbers().Last() % 2 == 1) // last digit odd
                                sequence.Add(Colors.Blue);
                            else
                                sequence.Add(Colors.Purple);
                        }
                    }
                    else
                    {
                        if (isTopRow(Colors.Purple)) // purple top row
                        {
                            if (Bomb.GetSolvedModuleNames().Count % 2 == 0) // solved modules even
                                sequence.Add(Colors.White);
                            else
                                sequence.Add(Colors.Red);
                        }
                        else
                        {
                            if (Bomb.GetSolvedModuleNames().Count % 2 == 1) // solved modules odd
                                sequence.Add(Colors.Green);
                            else
                                sequence.Add(Colors.Cyan);
                        }
                    }
                    break;
                case Colors.Red:
                    if (isTopRow(Colors.Orange)) // orange top row
                    {
                        if (_onLeds.Contains(Colors.Yellow))
                        {
                            if (_stageCounter % 2 == 0) // stage even
                                sequence.Add(Colors.Red);
                            else
                                sequence.Add(Colors.White);
                        }
                        else
                        {
                            if (_stageCounter % 2 == 1) // stage odd
                                sequence.Add(Colors.Cyan);
                            else
                                sequence.Add(Colors.Orange);
                        }
                    }
                    else
                    {
                        if (_onLeds.Contains(Colors.White)) // white led lit
                        {
                            if (Bomb.GetSolvableModuleNames().Count % 2 == 1) // odd solvable modules
                                sequence.Add(Colors.Blue);
                            else
                                sequence.Add(Colors.Magenta);
                        }
                        else
                        {
                            if (Bomb.GetSolvableModuleNames().Count % 2 == 1) // odd solvable modules
                                sequence.Add(Colors.Green);
                            else
                                sequence.Add(Colors.Pink);
                        }
                    }
                    break;
                case Colors.Pink:
                    if (isTopRow(Colors.Blue)) // blue top row
                    {
                        if (_onLeds.Contains(Colors.Red)) // red led lit
                        {
                            if (_stageCounter % 2 == 0) // stage even
                                sequence.Add(Colors.Green);
                            else
                                sequence.Add(Colors.Magenta);
                        }
                        else
                        {
                            if (_stageCounter % 2 == 1) // stage odd
                                sequence.Add(Colors.Red);
                            else
                                sequence.Add(Colors.White);
                        }
                    }
                    else
                    {
                        if (_onLeds.Contains(Colors.Blue)) // blue led lit
                        {
                            if (Bomb.GetSolvableModuleNames().Count % 2 == 1) // odd solvable modules
                                sequence.Add(Colors.Orange);
                            else
                                sequence.Add(Colors.Pink);
                        }
                        else
                        {
                            if (Bomb.GetSolvableModuleNames().Count % 2 == 1) // odd solvable modules
                                sequence.Add(Colors.Blue);
                            else
                                sequence.Add(Colors.Green);
                        }
                    }
                    break;
                case Colors.White:
                    if (!_onLeds.Contains(Colors.Pink)) // pink led unlit
                    {
                        if (isBottomRow(Colors.Cyan)) // cyan bottom row
                        {
                            if (Bomb.GetSerialNumberNumbers().Last() % 2 == 0) // last digit even
                                sequence.Add(Colors.Cyan);
                            else
                                sequence.Add(Colors.Yellow);
                        }
                        else
                        {
                            if (Bomb.GetSerialNumberNumbers().Last() % 2 == 1) // last digit odd
                                sequence.Add(Colors.White);
                            else
                                sequence.Add(Colors.Red);
                        }
                    }
                    else
                    {
                        if (isTopRow(Colors.Yellow)) // yellow top row
                        {
                            if (Bomb.GetSolvedModuleNames().Count % 2 == 0) // even solved modules
                                sequence.Add(Colors.Purple);
                            else
                                sequence.Add(Colors.Blue);
                        }
                        else
                        {
                            if (Bomb.GetSolvedModuleNames().Count % 2 == 1) // odd solved modules
                                sequence.Add(Colors.Pink);
                            else
                                sequence.Add(Colors.Yellow);
                        }
                    }
                    break;
            }
        }
        return sequence;
    }

    void computeCurrentStageSolution(ref List<Colors> colors)
    {
        if (_stageCounter == 1)
            stage1(ref colors);
        else if (_stageCounter == 2)
            stage2(ref colors);
        else if (_stageCounter == 3)
            stage3(ref colors);
        else if (_stageCounter == 4)
            stage4(ref colors);
        else if (_stageCounter == 5)
            stage5(ref colors);
        else if (_stageCounter == 6)
            stage6(ref colors);
        else if (_stageCounter == 7)
            stage7(ref colors);
        else if (_stageCounter == 8)
            stage8(ref colors);
        else if (_stageCounter == 9)
            stage9(ref colors);
        else if (_stageCounter == 10)
            stage10(ref colors);
        else if (_stageCounter == 11)
            stage11(ref colors);
        else
            stagePost11(ref colors);
    }

    void stage1(ref List<Colors> colors)
    {
        if (Bomb.GetIndicators().Count() == 0)
            shift(ref colors, 5);
        else if (Bomb.IsIndicatorOff(Indicator.CAR) && Bomb.IsIndicatorOn(Indicator.FRK))
            shift(ref colors, 2);
        else if (Bomb.IsIndicatorOn(Indicator.CAR) || Bomb.IsIndicatorOff(Indicator.FRK))
            shift(ref colors, -4);
        else if (Bomb.GetOnIndicators().Count() == 0)
            shift(ref colors, Bomb.GetOffIndicators().Count());
        else
            shift(ref colors, -Bomb.GetOnIndicators().Count());
    }

    void stage2(ref List<Colors> colors)
    {
        if (Bomb.GetPortCount(Port.Serial) > 1 && Bomb.GetSerialNumberLetters().Any(c => _vowels.Contains(c)))
            shift(ref colors, -3);
        else if (Bomb.IsPortPresent(Port.Serial))
            shift(ref colors, 6);
        else if (!_answersS2[_stageCounter - 2].Contains(Colors.Red))
            shift(ref colors, -(_colorOrder.IndexOf(Colors.Red) + 1));
        else
            shift(ref colors, Bomb.GetSerialNumberNumbers().Last());
    }

    void stage3(ref List<Colors> colors)
    {
        if (Bomb.GetBatteryHolderCount() > 3)
            shift(ref colors, -(Bomb.GetBatteryCount() + 2));
        else if (Bomb.GetBatteryCount() > 3)
            shift(ref colors, Bomb.GetBatteryHolderCount() + 1);
        else if (!_answersS2[_stageCounter - 2].Contains(Colors.Blue))
            shift(ref colors, -(_colorOrder.IndexOf(Colors.Blue) + 1));
        else
            shift(ref colors, Bomb.GetBatteryCount());
    }

    void stage4(ref List<Colors> colors)
    {
        if (_onLeds.Contains(Colors.Red) && _onLeds.Contains(Colors.Green))
            shift(ref colors, _colorOrder.IndexOf(Colors.White) + 1);
        else if (_onLeds.Contains(Colors.Green) && !_onLeds.Contains(Colors.Red))
            shift(ref colors, -1);
        else if (!_onLeds.Contains(Colors.Green) && _onLeds.Contains(Colors.Red))
            shift(ref colors, 2);
        else
            shift(ref colors, -1);
    }

    void stage5(ref List<Colors> colors)
    {
        int offset = 0;
        foreach (Colors c in _onLeds)
        {
            switch (c)
            {
                case Colors.White: offset += 2; break;
                case Colors.Orange: offset -= 1; break;
                case Colors.Yellow: offset += 3; break;
                case Colors.Green: offset -= 2; break;
                case Colors.Pink: offset += 5; break;
                case Colors.Cyan: offset -= 5; break;
                case Colors.Purple: offset -= 3; break;
                case Colors.Magenta: offset += 1; break;
                case Colors.Blue: offset += 4; break;
                case Colors.Red: offset -= 4; break;
            }
        }

        int n = (int)char.GetNumericValue(Bomb.GetSerialNumber().Last());
        if (n > 0 && n % 2 == 0)
            shift(ref colors, offset);
        else
            shift(ref colors, -offset);
    }

    void stage6(ref List<Colors> colors)
    {
        if (!_onLeds.Contains(Colors.White))
            shift(ref colors, -2);
        else if (!_onLeds.Contains(Colors.Red))
            shift(ref colors, 3);
        else if (!_onLeds.Contains(Colors.Blue))
            shift(ref colors, -4);
        else if (!_onLeds.Contains(Colors.Green))
            shift(ref colors, 1);
        else if (!_onLeds.Contains(Colors.Pink))
            shift(ref colors, -1);
        else if (!_onLeds.Contains(Colors.Yellow))
            shift(ref colors, 2);
        else if (!_onLeds.Contains(Colors.Cyan))
            shift(ref colors, -4);
        else if (!_onLeds.Contains(Colors.Orange))
            shift(ref colors, 3);
        else { } // do nothing
    }

    void stage7(ref List<Colors> colors)
    {
        if (_answersS2[_stageCounter - 2].Contains(Colors.Red))
            shift(ref colors, 1);
        else if (_answersS2[_stageCounter - 2].Contains(Colors.Yellow))
            shift(ref colors, -3);
        else if (_answersS2[_stageCounter - 2].Contains(Colors.Green))
            shift(ref colors, 2);
        else if (_answersS2[_stageCounter - 2].Contains(Colors.Blue))
            shift(ref colors, -1);
        else
            shift(ref colors, 3);
    }

    void stage8(ref List<Colors> colors)
    {
        if (_onLeds.Contains(Colors.Orange))
            shift(ref colors, -Bomb.GetSerialNumberNumbers().Last());
        else if (_onLeds.Contains(Colors.Pink))
            shift(ref colors, Bomb.GetSolvedModuleNames().Count); // warning need verif
        else if (_onLeds.Contains(Colors.Green))
            shift(ref colors, -Bomb.GetBatteryCount());
        else if (_onLeds.Contains(Colors.Purple))
            shift(ref colors, Bomb.GetSerialNumberNumbers().First());
        else
            shift(ref colors, 4);
    }

    void stage9(ref List<Colors> colors)
    {
        // At least 2 letters from "WORD"
        if (Bomb.GetSerialNumberLetters().Where(c => "STEINWAY".Contains(c)).Count() >= 2) // warning
            shift(ref colors, 4);
        else if (Bomb.GetSerialNumberLetters().Where(c => "INTIMATE".Contains(c)).Count() >= 2) // warning
            shift(ref colors, -2);
        else if (Bomb.GetSerialNumberLetters().Where(c => "ORIENTAL".Contains(c)).Count() >= 2) // warning
            shift(ref colors, 3);
        else if (Bomb.GetSerialNumberLetters().Where(c => "TACHYCARDIA".Contains(c)).Count() >= 2) // warning
            shift(ref colors, -7);
        else
            shift(ref colors, 2);
    }

    void stage10(ref List<Colors> colors)
    {
        if (_onLeds.Count != 2)
        {
            Debug.LogFormat("[Simon Forgets #{0}] Error while generating Tenth Stage rules. Autosolving");
            Module.HandlePass();
            _moduleSolved = true;
            return;
        }
        bool hasVowel = Bomb.GetSerialNumberLetters().Any(c => _vowels.Contains(c));
        int offset = _stage10Table[getStage10IndexColor(_onLeds[0]), getStage10IndexColor(_onLeds[1])];
        shift(ref colors, hasVowel ? offset : -offset);
    }

    int getStage10IndexColor(Colors c)
    {
        switch (c)
        {
            case Colors.White: return 0;
            case Colors.Orange: return 1;
            case Colors.Yellow: return 2;
            case Colors.Green: return 3;
            case Colors.Pink: return 4;
            case Colors.Cyan: return 5;
            case Colors.Purple: return 6;
            case Colors.Magenta: return 7;
            case Colors.Blue: return 8;
            case Colors.Red: return 9;
        }
        return -1;
    }

    void stage11(ref List<Colors> colors)
    {
        if (_onLeds.Contains(Colors.White))
            shift(ref colors, _colorOrder.IndexOf(Colors.White) + 1);
        else if (_onLeds.Contains(Colors.Orange))
            shift(ref colors, -(_colorOrder.IndexOf(Colors.Red) + 1));
        else if (_onLeds.Contains(Colors.Yellow))
            shift(ref colors, -(_colorOrder.IndexOf(Colors.Pink) + 1));
        else if (_onLeds.Contains(Colors.Green))
            shift(ref colors, _colorOrder.IndexOf(Colors.Purple) + 1);
        else if (_onLeds.Contains(Colors.Pink))
            shift(ref colors, -(_colorOrder.IndexOf(Colors.Red) + 1));
        else if (_onLeds.Contains(Colors.Cyan))
            shift(ref colors, -(_colorOrder.IndexOf(Colors.Orange) + 1));
        else if (_onLeds.Contains(Colors.Purple))
            shift(ref colors, -(_colorOrder.IndexOf(Colors.Cyan) + 1));
        else if (_onLeds.Contains(Colors.Magenta))
            shift(ref colors, -(_colorOrder.IndexOf(Colors.Blue) + 1));
        else if (_onLeds.Contains(Colors.Blue))
            shift(ref colors, -(_colorOrder.IndexOf(Colors.Yellow) + 1));
        else if (_onLeds.Contains(Colors.Red))
            shift(ref colors, -(_colorOrder.IndexOf(Colors.Magenta) + 1));
        else { } // do nothing
    }

    void stagePost11(ref List<Colors> colors)
    {
        if (_onLeds.Contains(Colors.White))
        {
            Debug.LogFormat("[Simon Forgets #{0}] Stage {1}: using {2} rule", _moduleId, _stageCounter, "+3");
            shift(ref colors, 3);
        }
        else if (_onLeds.Contains(Colors.Yellow))
        {
            Debug.LogFormat("[Simon Forgets #{0}] Stage {1}: using {2} rule", _moduleId, _stageCounter, "eleventh");
            stage11(ref colors);
        }
        else if (_onLeds.Contains(Colors.Pink))
        {
            Debug.LogFormat("[Simon Forgets #{0}] Stage {1}: using {2} rule", _moduleId, _stageCounter, "eighth");
            stage8(ref colors);
        }
        else if (_onLeds.Contains(Colors.Magenta))
        {
            Debug.LogFormat("[Simon Forgets #{0}] Stage {1}: using {2} rule", _moduleId, _stageCounter, "sixth");
            stage6(ref colors);
        }
        else if (_onLeds.Contains(Colors.Red))
        {
            Debug.LogFormat("[Simon Forgets #{0}] Stage {1}: using {2} rule", _moduleId, _stageCounter, "fifth");
            stage5(ref colors);
        }
        else if (_onLeds.Contains(Colors.Blue))
        {
            Debug.LogFormat("[Simon Forgets #{0}] Stage {1}: using {2} rule", _moduleId, _stageCounter, "seventh");
            stage7(ref colors);
        }
        else if (_onLeds.Contains(Colors.Green))
        {
            Debug.LogFormat("[Simon Forgets #{0}] Stage {1}: using {2} rule", _moduleId, _stageCounter, "tenth");
            stage10(ref colors);
        }
        else
        {
            Debug.LogFormat("[Simon Forgets #{0}] Stage {1}: using {2} rule", _moduleId, _stageCounter, "fourth");
            stage4(ref colors);
        }
    }

    void shift(ref List<Colors> list, int offset)
    {
        Debug.LogFormat("[Simon Forgets #{0}] Shift each color by {1}", _moduleId, offset);
        if (offset == 0) return;
        for (int i = 0; i < list.Count; ++i)
        {
            list[i] = _colorOrder[mod((_colorOrder.IndexOf(list[i]) + offset), 10)];
        }
    }

    T getNthElement<T>(T[][] array, int index) where T : new()
    {
        foreach (T[] items in array)
        {
            foreach (T item in items)
            {
                if (index == 0)
                    return item;
                --index;
            }
        }
        return new T();
    }

    bool isTopRow(Colors color)
    {
        return (_colorOrder.IndexOf(color) < 5);
    }
    bool isBottomRow(Colors color)
    {
        return !isTopRow(color);
    }

    // special modulo to handle negative numbers
    int mod(int a, int n)
    {
        return (a % n + n) % n;
    }

    // Twitch plays
    private bool isValid(string par)
    {
        string[] something = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" };
        if (!something.Contains(par))
        {
            return false;
        }
        return true;
    }
#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} press <#> [Presses buttons at positions 1-10 in reading order with a space between each number]";
#pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        command = Regex.Replace(command, @"\s+", " ").Trim();
        string[] parameters = command.Split(' ');
        if (Regex.IsMatch(parameters[0], @"^\s*press\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            if (parameters.Length < 2)
            {
                yield return "sendtochaterror Please specify what buttons you would like to press!";
            }
            else
            {
                for (int i = 1; i < parameters.Length; i++)
                {
                    if (!isValid(parameters.ElementAt(i)))
                    {
                        yield return "sendtochaterror " + parameters[i] + ", which was a number inputted to the module, is invalid. Please use numbers 1-10.";
                        yield break;
                    }
                }
                for (int i = 1; i < parameters.Length; i++)
                {
                    int temp = 0;
                    int.TryParse(parameters[i], out temp);
                    buttons[temp - 1].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
            }
        }
    }
    private List<KMBombModule> TwitchAbandonModule = new List<KMBombModule>();
    IEnumerator TwitchHandleForcedSolve()
    {
        yield return null;
        _moduleSolved = true;
        counterScreen.text = "";
        turnOffLeds();
        turnOffButtons();
    }
}