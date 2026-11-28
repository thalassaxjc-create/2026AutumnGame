using UnityEngine;

public sealed class PaperInspectionRound
{
    public enum Condition { Clean, Crease, ThinSpot, Pinholes }
    public const int SheetCount = 6;

    public readonly struct Sample
    {
        public Condition Defect { get; }
        internal Vector2 Position { get; }
        internal float Slant { get; }

        internal Sample(Condition defect, Vector2 position, float slant)
        {
            Defect = defect;
            Position = position;
            Slant = slant;
        }
    }

    readonly Sample[] sheets = new Sample[SheetCount];
    public int Inspected { get; private set; }
    public int Correct { get; private set; }
    public bool Complete => Inspected == SheetCount;
    public Sample Current => Complete ? default : sheets[Inspected];
    public Condition LastCondition { get; private set; }
    public bool LastCorrect { get; private set; }

    internal PaperInspectionRound()
    {
        var random = new System.Random();
        Condition[] conditions =
        {
            Condition.Clean, Condition.Clean, Condition.Crease, Condition.ThinSpot,
            Condition.Pinholes, (Condition)random.Next(1, 4)
        };
        for (int index = conditions.Length - 1; index > 0; index--)
        {
            int other = random.Next(index + 1);
            (conditions[index], conditions[other]) = (conditions[other], conditions[index]);
        }
        for (int index = 0; index < sheets.Length; index++)
        {
            Vector2 position = new(.21f + .58f * (float)random.NextDouble(), .26f + .48f * (float)random.NextDouble());
            sheets[index] = new Sample(conditions[index], position, random.Next(2) == 0 ? -.85f : .85f);
        }
    }

    internal bool TryClassify(Condition choice)
    {
        if (Complete || (uint)choice > (uint)Condition.Pinholes)
            return false;
        LastCondition = Current.Defect;
        LastCorrect = choice == LastCondition;
        if (LastCorrect)
            Correct++;
        Inspected++;
        return true;
    }

    internal static string Label(Condition condition) => condition switch
    {
        Condition.Crease => "折痕",
        Condition.ThinSpot => "薄斑",
        Condition.Pinholes => "针孔",
        _ => "合格"
    };
}
