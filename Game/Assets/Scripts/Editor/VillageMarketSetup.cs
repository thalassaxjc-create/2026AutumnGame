using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Role = VillageMarketDialogue.Role;
using Object = UnityEngine.Object;

public static class VillageMarketSetup
{
    const string Folder = "Assets/Materials/";

    sealed class Palette
    {
        public readonly Material Plaster = Load("WhiteboxHouse");
        public readonly Material Wood = Load("WhiteboxShop");
        public readonly Material Roof = Load("WhiteboxRoof");
        public readonly Material Road = Load("WhiteboxRoad");
        public readonly Material Green = Load("WhiteboxLeaf");
        public readonly Material Water = Load("WhiteboxWater");
        public readonly Material Plum = Tint("MarketPlum", new Color(.47f, .25f, .58f));
        public readonly Material Gold = Tint("MarketGold", new Color(.89f, .61f, .23f));
        public readonly Material Charcoal = Tint("MarketCharcoal", new Color(.085f, .10f, .12f));
        public readonly Material Brick = Tint("MarketBrick", new Color(.55f, .24f, .18f));
    }

    [MenuItem("宣纸帘/配置集市与三条支线")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("请先结束 Play Mode，再放置集市。");

        var palette = new Palette();
        ConfigureScene(SceneTransitionState.WorldScenePath, scene => PlaceInVillage(scene, palette));
        ConfigureScene(SceneTransitionState.WorkshopScenePath, scene => PlaceBucket(scene, palette));
    }

    static void ConfigureScene(string path, Func<Scene, bool> place)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        bool openedForSetup = !scene.isLoaded;
        if (openedForSetup)
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            if (scene.isDirty)
                throw new InvalidOperationException($"请先保存 {scene.name} 的现有编辑，再放置集市。");
            if (!place(scene))
                return;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"无法保存 {scene.name}。");
        }
        finally
        {
            if (openedForSetup && !scene.isDirty)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    static bool PlaceInVillage(Scene scene, Palette palette)
    {
        Transform houses = Find(scene, "VillageBuildings");
        Transform forest = Find(scene, "BambooForest");
        if (houses.Find("VillageMarket") != null)
        {
            Transform groveInsideForest = forest.Find("LiSpottedBambooGrove");
            if (groveInsideForest == null)
                return false;
            groveInsideForest.SetParent(forest.parent, true);
            return true;
        }
        Transform smithDoor = Find(scene, "StreetHouse_BEntranceMarker");
        Transform teaDoor = Find(scene, "StreetHouse_AEntranceMarker");
        Transform liDoor = Find(scene, "StreetHouse_CEntranceMarker");
        Transform gate = Find(scene, "GateLintel");

        Transform market = Group(houses, "VillageMarket", Vector3.zero);
        House(market, "染坊民居", new Vector3(36.8f, 0, 58.4f), 9.2f, 8.6f, palette.Plaster, palette);
        House(market, "晒谷人家", new Vector3(13.6f, 0, 65f), 8.4f, 9.8f, palette.Wood, palette);
        House(market, "西街民居", new Vector3(-14f, 0, 57.2f), 8.2f, 7.8f, palette.Plaster, palette);
        Box(market, "赶集空地", new Vector3(12f, .018f, 47f), new Vector3(11.5f, .036f, 10f), palette.Road);
        Strip(market, "青石支路", new Vector3(14.1f, .028f, 42f), new Vector3(9.5f, .028f, 47.8f),
            2.5f, palette.Wood);

        Transform smith = Group(market, "铁匠铺", smithDoor.position + Vector3.back * 1.6f);
        smith.position = new Vector3(smith.position.x, 0, smith.position.z);
        Awning(smith, palette.Brick, palette);
        Sign(smith, "铁匠铺", new Vector3(0, 2.82f, -1.35f), Vector3.back, palette.Brick);
        Forge(smith, palette);
        Merchant(smith, "铁匠", new Vector3(.7f, 0, -1.7f), Role.Blacksmith, palette.Charcoal, palette);

        Transform tea = Group(market, "茶摊", new Vector3(teaDoor.position.x, 0, teaDoor.position.z - 1f));
        Awning(tea, palette.Green, palette);
        Sign(tea, "茶摊", new Vector3(0, 2.82f, -1.35f), Vector3.back, palette.Green);
        TeaTable(tea, palette);
        Merchant(tea, "王老板", new Vector3(.75f, 0, -1.8f), Role.TeaOwner, palette.Green, palette);

        Transform goods = Group(market, "集市杂货摊", new Vector3(9.25f, 0, 50f));
        Awning(goods, palette.Roof, palette);
        Sign(goods, "杂货摊", new Vector3(0, 2.82f, -1.35f), Vector3.back, palette.Roof);
        Box(goods, "竹筐", new Vector3(-1.15f, .28f, -.75f), new Vector3(.87f, .56f, .8f), palette.Wood, true);
        for (int i = 0; i < 4; i++)
            Piece(goods, "斑竹样货", PrimitiveType.Cylinder, new Vector3(-1.44f + i * .19f, .74f, -.74f),
                new Vector3(.095f, .49f, .095f), palette.Plum);
        Merchant(goods, "杂货商", new Vector3(.61f, 0, -1.66f), Role.GoodsMerchant, palette.Wood, palette);

        Merchant(market, "集市东头富商", new Vector3(42.5f, 0, 44.2f), Role.Collector, palette.Plum, palette);
        Transform neighbor = Group(liDoor, "李婶家", new Vector3(-.26f, -.15f, 0));
        Sign(neighbor, "李婶家", new Vector3(1.63f, 2.76f, 0), Vector3.left, palette.Roof);
        Merchant(neighbor, "李婶", new Vector3(-1.6f, 0, 1.17f), Role.LiAunt, palette.Brick, palette);
        Transform villageEntrance = gate.parent;
        Merchant(villageEntrance, "黑衣小贩", new Vector3(gate.position.x + 6.3f, 0, gate.position.z - 4.7f),
            Role.NightTrader, palette.Charcoal, palette);

        PlaceGrove(forest.parent, palette);
        return true;
    }

    static bool PlaceBucket(Scene scene, Palette palette)
    {
        Transform weaving = Find(scene, "WeavingArea");
        if (weaving.Find("小皮的编帘水桶") != null)
            return false;
        Transform bucket = Group(weaving, "小皮的编帘水桶", new Vector3(-6.52f, 0, -5.71f));
        Piece(bucket, "桶身", PrimitiveType.Cylinder, new Vector3(0, .32f, 0),
            new Vector3(.76f, .32f, .76f), palette.Wood, true);
        Piece(bucket, "箍圈", PrimitiveType.Cylinder, new Vector3(0, .56f, 0),
            new Vector3(.80f, .035f, .80f), palette.Roof);
        Piece(bucket, "水面", PrimitiveType.Cylinder, new Vector3(0, .645f, 0),
            new Vector3(.67f, .014f, .67f), palette.Water);
        Box(bucket, "桶提手", new Vector3(0, .77f, 0), new Vector3(.70f, .075f, .065f), palette.Wood);
        Interaction(bucket, "小皮的编帘水桶", Role.XiaoPiBucket, null, 1.55f);
        return true;
    }

    static void PlaceGrove(Transform world, Palette palette)
    {
        Transform grove = Group(world, "LiSpottedBambooGrove", new Vector3(-69.5f, 0, 46.5f));
        Transform entrance = Group(grove, "斑竹地入口", new Vector3(6.2f, .08f, -8.5f));
        Transform bamboo = Group(grove, "紫斑竹", Vector3.zero);
        Transform stalks = Group(bamboo, "两根可砍斑竹", Vector3.zero);
        Strip(grove, "斑竹地土路", new Vector3(4.8f, .027f, -13.8f), entrance.localPosition,
            1.65f, palette.Road);
        Box(grove, "斑竹小径", new Vector3(5.75f, .13f, -8.7f), new Vector3(.18f, .25f, .18f), palette.Roof);
        Sign(grove, "李婶的斑竹地", new Vector3(6.2f, 1.78f, -8.1f), Vector3.back, palette.Plum);
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * .57f;
            float height = side < 0 ? 4.4f : 4.9f;
            Piece(stalks, "紫斑竹杆", PrimitiveType.Cylinder, new Vector3(x, height / 2f, 0),
                new Vector3(.28f, height / 2f, .28f), palette.Plum);
            for (int joint = 1; joint <= 3; joint++)
                Piece(stalks, "斑竹节", PrimitiveType.Sphere, new Vector3(x, height * joint / 4f, 0),
                    new Vector3(.33f, .11f, .33f), palette.Roof);
            Piece(stalks, "紫竹叶", PrimitiveType.Sphere, new Vector3(x + side * .48f, height - .25f, .14f),
                new Vector3(1.37f, .19f, .64f), palette.Plum).localRotation = Quaternion.Euler(0, 10 * side, 18 * side);
        }
        Box(bamboo, "竹根的土", new Vector3(0, .025f, 0), new Vector3(3f, .05f, 2.8f), palette.Road);

        Transform[] route =
        {
            Group(grove, "巡逻北西", new Vector3(-3.2f, 0, 3.1f)),
            Group(grove, "巡逻北东", new Vector3(2.8f, 0, 3.1f)),
            Group(grove, "巡逻南东", new Vector3(2.8f, 0, -3.75f)),
            Group(grove, "巡逻南西", new Vector3(-3.2f, 0, -3.75f))
        };
        Transform dog = Group(grove, "黄狗", route[0].localPosition);
        dog.localRotation = Quaternion.LookRotation(Vector3.right);
        Piece(dog, "身体", PrimitiveType.Capsule, new Vector3(0, .55f, -.08f),
            new Vector3(.55f, .64f, .53f), palette.Gold).localRotation = Quaternion.Euler(90, 0, 0);
        Piece(dog, "头", PrimitiveType.Sphere, new Vector3(0, .73f, .61f),
            new Vector3(.47f, .47f, .47f), palette.Gold);
        Piece(dog, "鼻", PrimitiveType.Sphere, new Vector3(0, .67f, .87f),
            new Vector3(.23f, .17f, .22f), palette.Charcoal);
        for (int side = -1; side <= 1; side += 2)
        {
            Box(dog, "耳朵", new Vector3(side * .18f, .96f, .60f), new Vector3(.105f, .27f, .12f), palette.Brick);
            Box(dog, "前爪", new Vector3(side * .18f, .23f, .32f), new Vector3(.13f, .44f, .16f), palette.Gold);
            Box(dog, "后爪", new Vector3(side * .18f, .23f, -.42f), new Vector3(.13f, .44f, .16f), palette.Gold);
        }
        Box(dog, "尾巴", new Vector3(0, .73f, -.72f), new Vector3(.11f, .11f, .62f), palette.Gold)
            .localRotation = Quaternion.Euler(43f, 0, 0);

        Transform sight = Group(dog, "巡逻视线边界", new Vector3(0, .16f, 0));
        Vector3 last = SightPoint(-VillageSpottedGrove.SightHalfAngle);
        Strip(sight, "左界", Vector3.zero, last, .08f, palette.Gold);
        for (int i = 1; i <= 10; i++)
        {
            Vector3 next = SightPoint(Mathf.Lerp(-VillageSpottedGrove.SightHalfAngle, VillageSpottedGrove.SightHalfAngle, i / 10f));
            Strip(sight, "弧界", last, next, .08f, palette.Gold);
            last = next;
        }
        Strip(sight, "右界", Vector3.zero, last, .08f, palette.Gold);
        sight.gameObject.SetActive(false);
        grove.gameObject.AddComponent<VillageSpottedGrove>().Configure(entrance, bamboo, dog, route, stalks.gameObject, sight.gameObject);
    }

    static Vector3 SightPoint(float angle) =>
        Quaternion.Euler(0, angle, 0) * Vector3.forward * VillageSpottedGrove.SightRange;

    static void House(Transform parent, string name, Vector3 position, float width, float depth, Material plaster, Palette p)
    {
        Transform house = Group(parent, name, position);
        const float wall = 3.85f;
        Box(house, "青石基座", new Vector3(0, .17f, 0), new Vector3(width + .45f, .34f, depth + .4f), p.Roof, true);
        Box(house, "粉墙", new Vector3(0, wall / 2f, 0), new Vector3(width, wall, depth), plaster, true);
        for (int side = -1; side <= 1; side += 2)
        {
            var roof = Box(house, "灰瓦屋面", new Vector3(side * width / 4f, wall + .38f, 0),
                new Vector3(width / 2f + .52f, .28f, depth + 1.0f), p.Roof);
            roof.localRotation = Quaternion.Euler(0, 0, side * -13f);
            Box(house, "木窗", new Vector3(side * width * .31f, 2.03f, -depth / 2f - .025f),
                new Vector3(1.0f, 1.13f, .075f), p.Wood);
        }
        Box(house, "木门", new Vector3(0, 1.13f, -depth / 2f - .029f), new Vector3(1.35f, 2.25f, .095f), p.Road);
    }

    static void Awning(Transform stall, Material top, Palette p)
    {
        Box(stall, "遮阳棚", new Vector3(0, 2.64f, -.1f), new Vector3(4.3f, .14f, 2.23f), top);
        for (int side = -1; side <= 1; side += 2)
            Box(stall, "棚柱", new Vector3(side * 2f, 1.27f, -1f), new Vector3(.15f, 2.54f, .15f), p.Wood, true);
    }

    static void Forge(Transform stall, Palette p)
    {
        Box(stall, "炉床", new Vector3(-1.2f, .39f, -.55f), new Vector3(.96f, .78f, .86f), p.Brick, true);
        Box(stall, "炉口", new Vector3(-1.2f, .55f, -1.0f), new Vector3(.59f, .32f, .09f), p.Charcoal);
        Box(stall, "砧木", new Vector3(1.38f, .30f, -.4f), new Vector3(.56f, .60f, .56f), p.Wood, true);
        Box(stall, "铁砧", new Vector3(1.38f, .70f, -.4f), new Vector3(.69f, .18f, .32f), p.Charcoal);
    }

    static void TeaTable(Transform stall, Palette p)
    {
        Box(stall, "茶板", new Vector3(-.86f, .76f, -1.02f), new Vector3(1.54f, .12f, .76f), p.Wood, true);
        Box(stall, "桌脚", new Vector3(-.86f, .34f, -1.02f), new Vector3(.13f, .70f, .39f), p.Wood);
        Piece(stall, "壶", PrimitiveType.Sphere, new Vector3(-1.1f, .94f, -1.02f), new Vector3(.33f, .29f, .31f), p.Brick);
        for (int cup = 0; cup < 3; cup++)
            Piece(stall, "茶盏", PrimitiveType.Cylinder, new Vector3(-.81f + .22f * cup, .85f, -1.11f),
                new Vector3(.15f, .065f, .15f), p.Plaster);
    }

    static void Sign(Transform parent, string name, Vector3 position, Vector3 outward, Material paint)
    {
        Transform board = Group(parent, name + "招牌", position);
        board.localRotation = Quaternion.LookRotation(-outward);
        Box(board, "木牌", Vector3.zero, new Vector3(2.35f, .59f, .11f), paint);
        Transform face = Group(board, "店名", new Vector3(0, -1.5f, -.08f));
        Nameplate(face, name);
    }

    static void Merchant(Transform parent, string name, Vector3 position, Role role, Material clothes, Palette p)
    {
        Transform npc = Group(parent, name, position);
        Transform model = Group(npc, "人物模型", Vector3.zero);
        Piece(model, "衣袍", PrimitiveType.Capsule, new Vector3(0, .76f, 0),
            new Vector3(.71f, .76f, .53f), clothes);
        Piece(model, "脸", PrimitiveType.Sphere, new Vector3(0, 1.48f, 0),
            new Vector3(.43f, .47f, .43f), p.Plaster);
        Piece(model, "布帽", PrimitiveType.Cylinder, new Vector3(0, 1.69f, 0),
            new Vector3(.51f, .079f, .50f), clothes);
        Interaction(npc, name, role, model.gameObject, 2.55f);
    }

    static void Interaction(Transform root, string name, Role role, GameObject visual, float radius)
    {
        Nameplate(root, name);
        var entry = root.gameObject.AddComponent<VillageMarketDialogue>();
        entry.Configure(role, visual);
        var interaction = root.gameObject.AddComponent<NpcDialogueInteraction>();
        interaction.Configure(entry, "按 E  ·  " + name, radius);
        interaction.ConfigureMarkerHeight(role == Role.XiaoPiBucket ? 1.97f : 2.22f);
    }

    static void Nameplate(Transform parent, string displayName)
    {
        var serialized = new SerializedObject(parent.gameObject.AddComponent<NpcCharacter>());
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static Transform Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == name)
                    return item;
        throw new InvalidOperationException($"场景 {scene.name} 缺少 {name}。");
    }

    static Transform Group(Transform parent, string name, Vector3 at)
    {
        var item = new GameObject(name);
        SceneManager.MoveGameObjectToScene(item, parent.gameObject.scene);
        item.transform.SetParent(parent, false);
        item.transform.localPosition = at;
        return item.transform;
    }

    static Transform Box(Transform parent, string name, Vector3 at, Vector3 size, Material material, bool solid = false) =>
        Piece(parent, name, PrimitiveType.Cube, at, size, material, solid);

    static Transform Piece(Transform parent, string name, PrimitiveType type, Vector3 at, Vector3 size,
        Material material, bool solid = false)
    {
        var item = GameObject.CreatePrimitive(type);
        item.name = name;
        SceneManager.MoveGameObjectToScene(item, parent.gameObject.scene);
        item.transform.SetParent(parent, false);
        item.transform.localPosition = at;
        item.transform.localScale = size;
        item.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid)
            Object.DestroyImmediate(item.GetComponent<Collider>());
        return item.transform;
    }

    static void Strip(Transform parent, string name, Vector3 a, Vector3 b, float width, Material material) =>
        Box(parent, name, (a + b) * .5f, new Vector3(width, .038f, Vector3.Distance(a, b)), material)
            .localRotation = Quaternion.LookRotation(b - a);

    static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>(Folder + name + ".mat")
        ?? throw new InvalidOperationException("缺少材质 " + name);

    static Material Tint(string name, Color color)
    {
        string path = Folder + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
            return existing;
        var material = new Material(Load("WhiteboxHouse")) { name = name, color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
