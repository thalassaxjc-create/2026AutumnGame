using System.Collections.Generic;
using UnityEngine;

public enum CurtainPatternKind { None, Turtle, VermilionJade }

public readonly struct CurtainPatternResult
{
    public int Correct { get; }
    public int Total { get; }
    public WorkshopOrder.QualityGrade Grade { get; }

    public CurtainPatternResult(int correct, int total)
    {
        Correct = correct;
        Total = total;
        Grade = correct == total ? WorkshopOrder.QualityGrade.S
            : correct * 100 >= total * 90 ? WorkshopOrder.QualityGrade.A
            : correct * 100 >= total * 75 ? WorkshopOrder.QualityGrade.B
            : correct * 100 >= total * 60 ? WorkshopOrder.QualityGrade.C
            : WorkshopOrder.QualityGrade.D;
    }
}

/// <summary>Order-owned stitching, with no scene objects or native resources.</summary>
public sealed class CurtainPatternDraft
{
    public const int TurtleCount = 4;
    public const int Frame = 4;
    public const int MainPins = 6;
    public const int DetourPin = 6;
    public const int RightJoin = 2;
    public const int LeftJoin = 5;

    enum Action { Start, Stitch, SwitchThread, Press }
    readonly Stack<(Action action, int loop, int pin)> history = new();
    readonly List<int>[] paths = { new(), new(), new(), new(), new() };
    readonly bool[] pressed = new bool[MainPins];

    public CurtainPatternKind Kind { get; }
    public int ActiveLoop { get; private set; } = -1;
    public bool UsesVermilion { get; private set; }
    public bool CanUndo => history.Count > 0;
    public int CurrentPin => ActiveLoop < 0 ? -1 : paths[ActiveLoop][paths[ActiveLoop].Count - 1];
    public bool CanSwitchThread => Kind == CurtainPatternKind.VermilionJade &&
        !UsesVermilion && ActiveLoop < 0 && HasClosedTurtle();
    public bool CanPress => ActiveLoop == Frame && IsJoin(CurrentPin) && !pressed[CurrentPin];
    public bool CanConfirm => HasClosedTurtle() &&
        (Kind == CurtainPatternKind.Turtle || IsClosed(Frame));

    public CurtainPatternDraft(CurtainPatternKind kind)
    {
        if (kind != CurtainPatternKind.Turtle && kind != CurtainPatternKind.VermilionJade)
            throw new System.ArgumentOutOfRangeException(nameof(kind));
        Kind = kind;
    }

    public IReadOnlyList<int> Path(int loop) => paths[loop];
    public bool IsDemonstration(int loop) => Kind == CurtainPatternKind.Turtle && (loop == 0 || loop == 2);
    public bool IsPressed(int pin) => IsJoin(pin) && pressed[pin];
    public bool IsClosed(int loop) => paths[loop].Count > 1 && paths[loop][paths[loop].Count - 1] == 0;

    public bool CanStart(int loop) => loop >= 0 && loop <= Frame && ActiveLoop < 0 &&
        !IsDemonstration(loop) && !IsClosed(loop) &&
        (loop == Frame ? UsesVermilion : !UsesVermilion);

    public bool TryStitch(int loop, int pin)
    {
        if (loop < 0 || loop > Frame || pin < 0 || pin >= PinCount(loop))
            return false;
        if (ActiveLoop < 0)
        {
            if (pin != 0 || !CanStart(loop))
                return false;
            paths[loop].Add(pin);
            ActiveLoop = loop;
            history.Push((Action.Start, loop, pin));
            return true;
        }
        if (loop != ActiveLoop || !AreAdjacent(CurrentPin, pin) ||
            (pin == 0 ? paths[loop].Count < MainPins : paths[loop].Contains(pin)))
            return false;
        paths[loop].Add(pin);
        history.Push((Action.Stitch, loop, pin));
        if (pin == 0)
            ActiveLoop = -1;
        return true;
    }

    public bool TrySwitchThread()
    {
        if (!CanSwitchThread)
            return false;
        UsesVermilion = true;
        history.Push((Action.SwitchThread, Frame, -1));
        return true;
    }

    public bool TryPress()
    {
        if (!CanPress)
            return false;
        int pin = CurrentPin;
        pressed[pin] = true;
        history.Push((Action.Press, Frame, pin));
        return true;
    }

    public bool Undo()
    {
        if (history.Count == 0)
            return false;
        var step = history.Pop();
        switch (step.action)
        {
            case Action.Start:
                paths[step.loop].Clear();
                ActiveLoop = -1;
                break;
            case Action.Stitch:
                paths[step.loop].RemoveAt(paths[step.loop].Count - 1);
                ActiveLoop = step.loop;
                break;
            case Action.SwitchThread:
                UsesVermilion = false;
                break;
            case Action.Press:
                pressed[step.pin] = false;
                break;
        }
        return true;
    }

    public int GuidedPin
    {
        get
        {
            const int guidedLoop = 1;
            if (Kind != CurtainPatternKind.Turtle || IsClosed(guidedLoop))
                return -1;
            if (ActiveLoop < 0)
                return 0;
            if (ActiveLoop != guidedLoop)
                return -1;
            int last = CurrentPin;
            for (int step = 1; step <= MainPins; step++)
            {
                int pin = (last + step) % MainPins;
                if (AreAdjacent(last, pin) && (pin == 0 ? paths[guidedLoop].Count >= MainPins :
                        !paths[guidedLoop].Contains(pin)))
                    return pin;
            }
            return -1;
        }
    }

    public CurtainPatternResult Evaluate()
    {
        int correct = 0;
        for (int loop = 0; loop <= Frame; loop++)
        {
            List<int> path = paths[loop];
            for (int i = 1; i < path.Count; i++)
            {
                if (path[i] < MainPins && path[i - 1] < MainPins)
                    correct++;
            }
        }
        if (Kind == CurtainPatternKind.Turtle)
            return new CurtainPatternResult(correct, 12);
        return new CurtainPatternResult(correct + (pressed[RightJoin] ? 1 : 0) +
            (pressed[LeftJoin] ? 1 : 0), 32);
    }

    bool HasClosedTurtle()
    {
        for (int loop = 0; loop < TurtleCount; loop++)
        {
            if (!IsDemonstration(loop) && IsClosed(loop))
                return true;
        }
        return false;
    }

    static bool AreAdjacent(int from, int to)
    {
        if (from == DetourPin || to == DetourPin)
            return from == 1 || from == 2 || to == 1 || to == 2;
        int difference = Mathf.Abs(from - to);
        return difference == 1 || difference == MainPins - 1;
    }

    public static bool IsJoin(int pin) => pin == RightJoin || pin == LeftJoin;
    public static int PinCount(int loop) => loop == Frame ? MainPins : MainPins + 1;

    static readonly Vector2[] FramePins =
    {
        new(.075f, .925f), new(.925f, .925f), new(.925f, .5f),
        new(.925f, .075f), new(.075f, .075f), new(.075f, .5f)
    };
    static readonly Vector2[] TurtlePins =
    {
        new(0f, .135f), new(.14f, .067f), new(.14f, -.067f),
        new(0f, -.135f), new(-.14f, -.067f), new(-.14f, .067f), new(.18f, 0f)
    };

    public static Vector2 PinPosition(int loop, int pin)
    {
        if (loop == Frame)
            return FramePins[pin];
        var center = new Vector2(loop % 2 == 0 ? .29f : .71f, loop < 2 ? .69f : .31f);
        return center + TurtlePins[pin];
    }
}
