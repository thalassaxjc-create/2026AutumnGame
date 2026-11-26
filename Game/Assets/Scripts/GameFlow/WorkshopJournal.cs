using System.Collections.Generic;

internal sealed class WorkshopJournal
{
    internal sealed class Entry
    {
        public string Title { get; }
        public string Notes { get; }
        public byte[] Photo { get; set; }

        public Entry(string title, string notes)
        {
            Title = title;
            Notes = notes;
        }
    }

    internal readonly List<Entry> Events = new();
    internal readonly List<Entry> People = new();
    internal readonly Queue<Entry> Unphotographed = new();
    internal readonly Queue<Entry> Unread = new();
    readonly HashSet<string> knownPeople = new();

    internal void AddEvent(string title, string notes, bool announce)
    {
        var entry = new Entry(title, notes);
        Events.Add(entry);
        Unphotographed.Enqueue(entry);
        if (announce)
            Unread.Enqueue(entry);
    }

    internal void Meet(string name, string day, string scene)
    {
        if (string.IsNullOrWhiteSpace(name) || !knownPeople.Add(name))
            return;
        string description = name switch
        {
            "舅舅" => "村里的制帘师傅，也是这五天比试的发起人。",
            "小皮" => "一起学做帘子的对手。会把样稿拿在手里反复对照。",
            "小白" => "喜欢叼竹丝的小狗，也是比试的一员。",
            "村口大爷" => "在村口打招呼，知道舅舅最近的消息。",
            "老周" => "捞纸工人。斜着照光验帘，再用指肚摸平整度。",
            "艾琳" => "来纸坊看试捞的访客，想带一张龟纹帘回去自己试试。",
            _ => "在村镇交谈时认识的人。"
        };
        string location = scene == SceneTransitionState.WorkshopSceneName ? "工坊" :
            scene == SceneTransitionState.WorldSceneName ? "村镇" : "家里";
        People.Add(new Entry(name, $"初次交谈：{day} · {location}\n{description}"));
    }
}
