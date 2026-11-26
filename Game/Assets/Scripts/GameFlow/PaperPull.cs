using System;

internal sealed class PaperPull
{
    internal const float LiftWindowStart = 3.6f;
    internal const float LiftWindowEnd = 5.3f;
    internal const float ReminderStart = 1.8f;
    internal const float ReminderEnd = 3.4f;

    readonly float tolerance;
    readonly float faultDirection;
    internal float Time { get; private set; }
    internal float Balance { get; private set; }
    internal float Coverage { get; private set; }
    internal bool Reminded { get; private set; }
    internal bool TimelyReminder { get; private set; }
    internal bool CueVisible => Time >= ReminderStart && Time <= ReminderEnd;

    internal PaperPull(WorkshopOrder.LessonTopic topic)
    {
        tolerance = topic == WorkshopOrder.LessonTopic.TurtleKnot ? .20f : .27f;
        faultDirection = topic == WorkshopOrder.LessonTopic.ThinCorner ? -1f : 1f;
    }

    internal void Practice(float delta, float steering, float? pointer)
    {
        Time += delta;
        Balance = pointer ?? Math.Max(-1f, Math.Min(1f, Balance + (steering * 1.8f + .45f * faultDirection) * delta));
        if (Math.Abs(Balance) <= tolerance)
            Coverage = Math.Min(1f, Coverage + delta / 3.8f);
    }

    internal bool Lift() => Time >= LiftWindowStart && Time <= LiftWindowEnd &&
        Coverage >= .78f && Math.Abs(Balance) <= tolerance;

    internal void Observe(float delta)
    {
        Time += delta;
        Balance = TimelyReminder ? 0f : faultDirection * Math.Max(0f, Math.Min(.85f, (Time - ReminderStart) * .55f));
        Coverage = Math.Min(1f, Time / 4.5f);
    }

    internal void Remind()
    {
        if (Reminded)
            return;
        Reminded = true;
        TimelyReminder = CueVisible;
    }
}
