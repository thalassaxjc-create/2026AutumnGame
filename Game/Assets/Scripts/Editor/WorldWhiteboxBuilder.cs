using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 室外大地图白盒。按草图重排 WorldRoot：工坊北门左竹林右街坊，南门回家。山只作外圈点景，不圈死可走范围。
/// </summary>
public static class WorldWhiteboxBuilder
{
    const string WorldScenePath = "Assets/Scenes/WorldScene.unity";
    const string WorldRootName = "WorldRoot";
    const string LitShaderPath = "Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader";
    const string MeshFolder = "Assets/Meshes/Whitebox";
    const string MaterialFolder = "Assets/Materials";

    const float MapWidth = 640f;
    const float MapDepth = 560f;
    const float GroundTopY = 0f;
    const float GroundThickness = 1f;
    const float TileSize = 10f;
    const float RiverWidth = 8f;
    const float RiverSurfaceY = 0.045f;
    const float RoadWidth = 4.5f;
    const float RoadHeight = 0.12f;
    const int BambooSeed = 12880001;
    const int BambooCount = 90;
    const float WorkshopDoorWidth = 2.8f;
    const float WorkshopDoorHeight = 3f;

    static readonly Vector3 PlayerSpawn = new Vector3(72f, 1f, 76f);
    static readonly Quaternion PlayerSpawnRotation = Quaternion.Euler(0f, 180f, 0f);

    static readonly Vector3 WorkshopCenter = new Vector3(-48f, 0f, 10f);
    static readonly Vector3 WorkshopSize = new Vector3(34f, 5.5f, 22f);
    static readonly Vector3 WorkshopNorthDoor = new Vector3(-58f, 0f, 21f);
    static readonly Vector3 WorkshopSouthDoor = new Vector3(-36f, 0f, -1f);

    static readonly Vector3 StreetHouseACenter = new Vector3(0f, 0f, 64f);
    static readonly Vector3 StreetHouseASize = new Vector3(16f, 5.6f, 14f);
    static readonly Vector3 StreetHouseBCenter = new Vector3(26f, 0f, 48f);
    static readonly Vector3 StreetHouseBSize = new Vector3(11f, 5.2f, 12f);
    static readonly Vector3 StreetHouseCCenter = new Vector3(64f, 0f, 4f);
    static readonly Vector3 StreetHouseCSize = new Vector3(12f, 5.4f, 14f);

    static readonly Vector3 CourtyardCenter = new Vector3(12f, 0f, -30f);
    static readonly Vector3 CourtyardSize = new Vector3(28f, 4.2f, 22f);
    static readonly Vector3 KitchenCenter = new Vector3(12f, 0f, -15f);
    static readonly Vector3 KitchenSize = new Vector3(12f, 3.6f, 8f);
    static readonly Vector3 BedroomCenter = new Vector3(3f, 0f, -47f);
    static readonly Vector3 BedroomSize = new Vector3(10f, 3.8f, 12f);
    static readonly Vector3 BridgeCenter = new Vector3(60f, 0f, 48f);
    static readonly Vector3 BridgeSize = new Vector3(18f, 0.35f, 5.5f);
    static readonly Vector3 BambooCenter = new Vector3(-90f, 0f, 40f);
    static readonly Vector3 BambooArea = new Vector3(40f, 0f, 36f);
    static readonly Vector3 VillageGateCenter = new Vector3(72f, 0f, 72f);

    static readonly Vector3[] RiverPoints =
    {
        new Vector3(16f, 0f, 272f),
        new Vector3(26f, 0f, 228f),
        new Vector3(36f, 0f, 178f),
        new Vector3(46f, 0f, 132f),
        new Vector3(52f, 0f, 96f),
        new Vector3(56f, 0f, 78f),
        new Vector3(58f, 0f, 64f),
        new Vector3(60f, 0f, 48f),
        new Vector3(66f, 0f, 32f),
        new Vector3(74f, 0f, 16f),
        new Vector3(84f, 0f, 2f),
        new Vector3(92f, 0f, -18f),
        new Vector3(88f, 0f, -40f),
        new Vector3(96f, 0f, -70f),
        new Vector3(114f, 0f, -118f),
        new Vector3(146f, 0f, -168f),
        new Vector3(188f, 0f, -214f),
        new Vector3(242f, 0f, -258f)
    };

    static readonly Vector3[] NorthToBamboo =
    {
        new Vector3(-58f, 0f, 24.5f),
        new Vector3(-68f, 0f, 30f),
        new Vector3(-78f, 0f, 36f),
        new Vector3(-90f, 0f, 40f)
    };

    static readonly Vector3[] NorthToStreet =
    {
        new Vector3(-58f, 0f, 24.5f),
        new Vector3(-44f, 0f, 32f),
        new Vector3(-24f, 0f, 40f),
        new Vector3(-8f, 0f, 48f),
        new Vector3(0f, 0f, 52f)
    };

    static readonly Vector3[] SouthToHome =
    {
        new Vector3(-36f, 0f, -4.5f),
        new Vector3(-28f, 0f, -12f),
        new Vector3(-18f, 0f, -20f),
        new Vector3(-8f, 0f, -28f),
        new Vector3(-5.2f, 0f, -30f)
    };

    static readonly Vector3[] HomeToBridge =
    {
        new Vector3(30f, 0f, -30f),
        new Vector3(36f, 0f, -8f),
        new Vector3(40f, 0f, 10f),
        new Vector3(44f, 0f, 28f),
        new Vector3(48f, 0f, 40f),
        new Vector3(51f, 0f, 48f),
        new Vector3(69f, 0f, 48f),
        new Vector3(74f, 0f, 60f),
        new Vector3(72f, 0f, 72f),
        new Vector3(70f, 0f, 84f)
    };

    static readonly Vector3[] StreetLink =
    {
        new Vector3(0f, 0f, 52f),
        new Vector3(16f, 0f, 40f),
        new Vector3(32f, 0f, 36f),
        new Vector3(46f, 0f, 40f)
    };

    static readonly Vector3[] EastHouseRoad =
    {
        new Vector3(40f, 0f, 4f),
        new Vector3(56f, 0f, 4f)
    };

    static Materials Palette;

    struct Materials
    {
        public Material Terrain;
        public Material Road;
        public Material Workshop;
        public Material Shop;
        public Material House;
        public Material Village;
        public Material Roof;
        public Material Water;
        public Material Bank;
        public Material Bamboo;
        public Material Leaf;
        public Material Mountain;
        public Material Courtyard;
        public Material NpcElder;
        public Material NpcUncle;
    }

    [MenuItem("宣纸帘/生成第二阶段大地图白盒")]
    public static void BuildMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("请先退出 Play Mode，再生成第二阶段大地图。");
            return;
        }

        Debug.Log(BuildWorldMap());
    }

    [MenuItem("宣纸帘/检查第二阶段大地图")]
    public static void ValidateMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("请先退出 Play Mode，再检查大地图。");
            return;
        }

        if (SceneManager.GetActiveScene().path != WorldScenePath)
            EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);

        Debug.Log(ValidateWorldMap());
    }

    public static string BuildWorldMap()
    {
        if (SceneManager.GetActiveScene().path != WorldScenePath)
        {
            EditorSceneManager.SaveOpenScenes();
            EditorSceneManager.OpenScene(WorldScenePath, OpenSceneMode.Single);
        }
        else
            EditorSceneManager.SaveOpenScenes();

        EnsureFolder(MaterialFolder);
        EnsureFolder(MeshFolder);
        Palette = CreateMaterials();

        DisableLegacyGround();
        GameObject worldRoot = RebuildWorldRoot();

        Transform terrain = CreateGroup(worldRoot.transform, "Terrain");
        Transform roads = CreateGroup(worldRoot.transform, "Roads");
        Transform workshop = CreateGroup(worldRoot.transform, "WorkshopExterior");
        Transform bamboo = CreateGroup(worldRoot.transform, "BambooForest");
        Transform courtyard = CreateGroup(worldRoot.transform, "Courtyard");
        Transform kitchen = CreateGroup(worldRoot.transform, "Kitchen");
        Transform bedroom = CreateGroup(worldRoot.transform, "Bedroom");
        Transform river = CreateGroup(worldRoot.transform, "River");
        Transform bridge = CreateGroup(worldRoot.transform, "Bridge");
        Transform village = CreateGroup(worldRoot.transform, "VillageBuildings");
        Transform villageGate = CreateGroup(worldRoot.transform, "VillageEntrance");
        Transform npcs = CreateGroup(worldRoot.transform, "Npcs");
        Transform mountains = CreateGroup(worldRoot.transform, "Mountains");

        int groundTiles = BuildTerrain(terrain);
        BuildRiver(river);
        BuildBridge(bridge);
        BuildRoads(roads);
        BuildWorkshop(workshop);
        int bambooCount = BuildBamboo(bamboo);
        BuildCourtyard(courtyard);
        BuildKitchen(kitchen);
        BuildBedroom(bedroom);
        BuildVillage(village);
        BuildVillageEntrance(villageGate);
        BuildNpcs(npcs);
        BuildMountains(mountains);
        ApplyWorldFog();
        PlacePlayerSafely();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), WorldScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var report = new StringBuilder();
        report.AppendLine("第二阶段大地图已生成并保存 WorldScene。");
        report.AppendLine("地面块数量: " + groundTiles + "，竹子数量: " + bambooCount);
        report.AppendLine(DescribeLayout());
        report.AppendLine(ValidateWorldMap());
        return report.ToString();
    }

    static GameObject RebuildWorldRoot()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == WorldRootName)
                Object.DestroyImmediate(root);
        }

        var worldRoot = new GameObject(WorldRootName);
        worldRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        return worldRoot;
    }

    static void DisableLegacyGround()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == "Ground")
            {
                root.SetActive(false);
                return;
            }
        }
    }

    static int BuildTerrain(Transform parent)
    {
        int cols = Mathf.RoundToInt(MapWidth / TileSize);
        int rows = Mathf.RoundToInt(MapDepth / TileSize);
        float originX = -MapWidth * 0.5f;
        float originZ = -MapDepth * 0.5f;
        bool[,] land = new bool[cols, rows];
        bool[,] used = new bool[cols, rows];
        int landCount = 0;

        for (int x = 0; x < cols; x++)
        {
            for (int z = 0; z < rows; z++)
            {
                land[x, z] = true;
                landCount++;
            }
        }

        int tiles = 0;
        for (int z = 0; z < rows; z++)
        {
            for (int x = 0; x < cols; x++)
            {
                if (!land[x, z] || used[x, z])
                    continue;

                int width = 1;
                while (x + width < cols && land[x + width, z] && !used[x + width, z])
                    width++;

                int depth = 1;
                bool canGrow = true;
                while (canGrow && z + depth < rows)
                {
                    for (int i = 0; i < width; i++)
                    {
                        if (!land[x + i, z + depth] || used[x + i, z + depth])
                        {
                            canGrow = false;
                            break;
                        }
                    }

                    if (canGrow)
                        depth++;
                }

                for (int dz = 0; dz < depth; dz++)
                {
                    for (int dx = 0; dx < width; dx++)
                        used[x + dx, z + dz] = true;
                }

                float sizeX = width * TileSize + 0.08f;
                float sizeZ = depth * TileSize + 0.08f;
                Vector3 center = new Vector3(
                    originX + x * TileSize + width * TileSize * 0.5f,
                    GroundTopY - GroundThickness * 0.5f,
                    originZ + z * TileSize + depth * TileSize * 0.5f);
                CreateBox(parent, "Ground_" + tiles, center, new Vector3(sizeX, GroundThickness, sizeZ), Palette.Terrain, true);
                tiles++;
            }
        }

        if (landCount == 0)
            throw new InvalidOperationException("地面生成失败：河床裁切后没有可走地面。");

        return tiles;
    }

    static void BuildRiver(Transform parent)
    {
        Vector3[] samples = SampleCatmullRom(RiverPoints, 1.25f);
        Mesh waterMesh = CreateRibbonMesh(samples, RiverWidth, RiverSurfaceY);
        SaveMesh(waterMesh, MeshFolder + "/RiverSurface.mesh");

        var water = new GameObject("RiverSurface");
        water.transform.SetParent(parent, false);
        MeshFilter filter = water.AddComponent<MeshFilter>();
        filter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshFolder + "/RiverSurface.mesh");
        MeshRenderer renderer = water.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = Palette.Water;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Vector3[] bankSamples = SampleCatmullRom(RiverPoints, 4.8f);
        int wallIndex = 0;
        for (int i = 0; i < bankSamples.Length - 1; i++)
        {
            Vector3 a = bankSamples[i];
            Vector3 b = bankSamples[i + 1];
            Vector3 mid = (a + b) * 0.5f;
            Vector2 bridgeFlat = new Vector2(BridgeCenter.x, BridgeCenter.z);
            if ((new Vector2(mid.x, mid.z) - bridgeFlat).sqrMagnitude < 7.5f * 7.5f)
                continue;

            Vector3 tangent = b - a;
            tangent.y = 0f;
            float length = tangent.magnitude + 0.2f;
            if (length < 0.2f)
                continue;
            tangent.Normalize();
            Vector3 normal = new Vector3(-tangent.z, 0f, tangent.x);
            Quaternion rotation = Quaternion.LookRotation(tangent, Vector3.up);

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 wallPos = mid + normal * (RiverWidth * 0.5f * side);
                wallPos.y = 0.85f;
                GameObject wall = CreateBox(parent, "RiverWall_" + wallIndex, wallPos, new Vector3(0.45f, 1.7f, length), Palette.Bank, true);
                wall.transform.rotation = rotation;
                wallIndex++;

                Vector3 bankPos = mid + normal * ((RiverWidth * 0.5f + 0.7f) * side);
                bankPos.y = 0.18f;
                GameObject bank = CreateBox(parent, "RiverBank_" + wallIndex, bankPos, new Vector3(1.4f, 0.45f, length * 0.95f), Palette.Bank, true);
                bank.transform.rotation = rotation * Quaternion.Euler(0f, 0f, side * -18f);
                wallIndex++;
            }
        }
    }

    static void BuildBridge(Transform parent)
    {
        Vector3 deckCenter = new Vector3(BridgeCenter.x, BridgeSize.y * 0.5f, BridgeCenter.z);
        CreateBox(parent, "BridgeDeck", deckCenter, BridgeSize, Palette.Road, true);

        float railHeight = 0.7f;
        Vector3 railSize = new Vector3(BridgeSize.x, railHeight, 0.18f);
        CreateBox(parent, "BridgeRailSouth", new Vector3(BridgeCenter.x, railHeight * 0.5f, BridgeCenter.z - BridgeSize.z * 0.5f + 0.12f), railSize, Palette.House, true);
        CreateBox(parent, "BridgeRailNorth", new Vector3(BridgeCenter.x, railHeight * 0.5f, BridgeCenter.z + BridgeSize.z * 0.5f - 0.12f), railSize, Palette.House, true);

        CreateBox(parent, "BridgeApproachWest", new Vector3(BridgeCenter.x - BridgeSize.x * 0.5f - 1.2f, 0.08f, BridgeCenter.z), new Vector3(2.6f, 0.16f, RoadWidth), Palette.Road, true);
        CreateBox(parent, "BridgeApproachEast", new Vector3(BridgeCenter.x + BridgeSize.x * 0.5f + 1.2f, 0.08f, BridgeCenter.z), new Vector3(2.6f, 0.16f, RoadWidth), Palette.Road, true);

        float flankHeight = 1.35f;
        Vector3 flankSize = new Vector3(RiverWidth + 1.2f, flankHeight, 3.2f);
        CreateBox(parent, "BridgeFlankNorth", new Vector3(BridgeCenter.x, flankHeight * 0.5f, BridgeCenter.z + BridgeSize.z * 0.5f + 1.7f), flankSize, Palette.Bank, true);
        CreateBox(parent, "BridgeFlankSouth", new Vector3(BridgeCenter.x, flankHeight * 0.5f, BridgeCenter.z - BridgeSize.z * 0.5f - 1.7f), flankSize, Palette.Bank, true);
    }

    static void BuildRoads(Transform parent)
    {
        BuildRoadStrip(parent, "NorthToBamboo", NorthToBamboo, Palette.Road);
        BuildRoadStrip(parent, "NorthToStreet", NorthToStreet, Palette.Road);
        BuildRoadStrip(parent, "SouthToHome", SouthToHome, Palette.Road);
        BuildRoadStrip(parent, "HomeToBridge", HomeToBridge, Palette.Road);
        BuildRoadStrip(parent, "StreetLink", StreetLink, Palette.Road);
        BuildRoadStrip(parent, "EastHouseRoad", EastHouseRoad, Palette.Road);
        CreateBox(parent, "NorthForkPlaza", new Vector3(-58f, RoadHeight * 0.5f, 26f), new Vector3(9f, RoadHeight, 8f), Palette.Road, true);
    }

    static void BuildRoadStrip(Transform parent, string name, Vector3[] waypoints, Material material)
    {
        Transform group = CreateGroup(parent, name);
        Vector3[] samples = SampleCatmullRom(waypoints, 2.1f);
        for (int i = 0; i < samples.Length - 1; i++)
        {
            Vector3 a = samples[i];
            Vector3 b = samples[i + 1];
            Vector3 mid = (a + b) * 0.5f;
            Vector3 offset = b - a;
            offset.y = 0f;
            float length = offset.magnitude + 0.25f;
            if (length < 0.2f)
                continue;

            mid.y = RoadHeight * 0.5f;
            GameObject segment = CreateBox(group, name + "_" + i, mid, new Vector3(RoadWidth, RoadHeight, length), material, true);
            segment.transform.rotation = Quaternion.LookRotation(offset.normalized, Vector3.up);
        }
    }

    static void BuildWorkshop(Transform parent)
    {
        Transform building = CreateGroup(parent, "WorkshopBuilding");
        Vector3 center = WorkshopCenter;
        float width = WorkshopSize.x;
        float depth = WorkshopSize.z;
        float height = WorkshopSize.y;
        float wall = 0.45f;
        float wallY = height * 0.5f;

        CreateBox(building, "Foundation", center + Vector3.up * 0.1f, new Vector3(width + 0.4f, 0.2f, depth + 0.4f), Palette.Workshop, true);
        CreateBox(building, "InteriorFill", center + Vector3.up * wallY, new Vector3(width - 1.2f, height, depth - 3.4f), Palette.Workshop, true);
        CreateBox(building, "WallWest", center + Vector3.left * (width * 0.5f - wall * 0.5f) + Vector3.up * wallY, new Vector3(wall, height, depth), Palette.Workshop, true);
        CreateBox(building, "WallEast", center + Vector3.right * (width * 0.5f - wall * 0.5f) + Vector3.up * wallY, new Vector3(wall, height, depth), Palette.Workshop, true);

        float northZ = center.z + depth * 0.5f - wall * 0.5f;
        float southZ = center.z - depth * 0.5f + wall * 0.5f;
        CreateWallRun(building, "North", new Vector3(center.x, wallY, northZ), width, height, wall, WorkshopNorthDoor.x - center.x, WorkshopDoorWidth, WorkshopDoorHeight, Palette.Workshop);
        CreateWallRun(building, "South", new Vector3(center.x, wallY, southZ), width, height, wall, WorkshopSouthDoor.x - center.x, WorkshopDoorWidth, WorkshopDoorHeight, Palette.Workshop);
        CreateBox(building, "Roof", center + Vector3.up * (height + 0.2f), new Vector3(width + 1f, 0.4f, depth + 1f), Palette.Roof, true);

        CreateDoorMarker(building, "WorkshopNorthMarker", WorkshopNorthDoor, Vector3.forward);
        CreateDoorMarker(building, "WorkshopSouthMarker", WorkshopSouthDoor, Vector3.back);
    }

    static void CreateWallRun(Transform parent, string prefix, Vector3 center, float length, float height, float thickness, float doorOffset, float doorWidth, float doorHeight, Material material)
    {
        float leftWidth = (length * 0.5f + doorOffset) - doorWidth * 0.5f;
        float rightWidth = (length * 0.5f - doorOffset) - doorWidth * 0.5f;
        float leftCenterX = center.x - length * 0.5f + leftWidth * 0.5f;
        float rightCenterX = center.x + length * 0.5f - rightWidth * 0.5f;
        if (leftWidth > 0.2f)
            CreateBox(parent, prefix + "Left", new Vector3(leftCenterX, center.y, center.z), new Vector3(leftWidth, height, thickness), material, true);
        if (rightWidth > 0.2f)
            CreateBox(parent, prefix + "Right", new Vector3(rightCenterX, center.y, center.z), new Vector3(rightWidth, height, thickness), material, true);

        float lintelHeight = Mathf.Max(0.25f, height - doorHeight);
        CreateBox(parent, prefix + "Lintel", new Vector3(center.x + doorOffset, doorHeight + lintelHeight * 0.5f, center.z), new Vector3(doorWidth, lintelHeight, thickness), material, true);
    }

    static void CreateDoorMarker(Transform parent, string name, Vector3 doorPosition, Vector3 outward)
    {
        var marker = new GameObject(name);
        marker.transform.SetParent(parent, false);
        doorPosition.y = 0.15f;
        marker.transform.SetPositionAndRotation(doorPosition, Quaternion.LookRotation(Flatten(outward), Vector3.up));
    }

    static void BuildKitchen(Transform parent)
    {
        CreateBuilding(
            parent,
            "KitchenBuilding",
            KitchenCenter,
            KitchenSize.x,
            KitchenSize.z,
            KitchenSize.y,
            Vector3.back,
            2.4f,
            2.4f,
            Palette.House,
            "KitchenEntranceMarker");
    }

    static void BuildBedroom(Transform parent)
    {
        CreateBuilding(
            parent,
            "BedroomBuilding",
            BedroomCenter,
            BedroomSize.x,
            BedroomSize.z,
            BedroomSize.y,
            Vector3.forward,
            2.4f,
            2.4f,
            Palette.House,
            "BedroomEntranceMarker");
    }

    static void BuildCourtyard(Transform parent)
    {
        float wallHeight = CourtyardSize.y;
        float wallThickness = 0.4f;
        float halfX = CourtyardSize.x * 0.5f;
        float halfZ = CourtyardSize.z * 0.5f;
        Vector3 c = CourtyardCenter;
        float northZ = c.z + halfZ - wallThickness * 0.5f;
        float southZ = c.z - halfZ + wallThickness * 0.5f;
        float westX = c.x - halfX + wallThickness * 0.5f;
        float eastX = c.x + halfX - wallThickness * 0.5f;

        CreateBox(parent, "CourtyardPad", new Vector3(c.x, 0.04f, c.z), new Vector3(CourtyardSize.x - 1.2f, 0.08f, CourtyardSize.z - 1.2f), Palette.Courtyard, true);

        CreateBox(parent, "CourtyardWallNorthWest", new Vector3(-2f + 4f, wallHeight * 0.5f, northZ), new Vector3(8f, wallHeight, wallThickness), Palette.Courtyard, true);
        CreateBox(parent, "CourtyardWallNorthEast", new Vector3(18f + 4f, wallHeight * 0.5f, northZ), new Vector3(8f, wallHeight, wallThickness), Palette.Courtyard, true);
        CreateBox(parent, "CourtyardWallSouthLeft", new Vector3((-2f + 1.2f) * 0.5f, wallHeight * 0.5f, southZ), new Vector3(3.2f, wallHeight, wallThickness), Palette.Courtyard, true);
        CreateBox(parent, "CourtyardWallSouthRight", new Vector3((4.8f + 26f) * 0.5f, wallHeight * 0.5f, southZ), new Vector3(21.2f, wallHeight, wallThickness), Palette.Courtyard, true);

        CreateBox(parent, "CourtyardWallWestSouth", new Vector3(westX, wallHeight * 0.5f, (-41f + -32f) * 0.5f), new Vector3(wallThickness, wallHeight, 9f), Palette.Courtyard, true);
        CreateBox(parent, "CourtyardWallWestNorth", new Vector3(westX, wallHeight * 0.5f, (-28f + -19f) * 0.5f), new Vector3(wallThickness, wallHeight, 9f), Palette.Courtyard, true);
        CreateBox(parent, "CourtyardWallEastSouth", new Vector3(eastX, wallHeight * 0.5f, (-41f + -32f) * 0.5f), new Vector3(wallThickness, wallHeight, 9f), Palette.Courtyard, true);
        CreateBox(parent, "CourtyardWallEastNorth", new Vector3(eastX, wallHeight * 0.5f, (-28f + -19f) * 0.5f), new Vector3(wallThickness, wallHeight, 9f), Palette.Courtyard, true);

        CreateBox(parent, "WestGatePostSouth", new Vector3(westX, 1.5f, -32f), new Vector3(0.45f, 3f, 0.45f), Palette.House, true);
        CreateBox(parent, "WestGatePostNorth", new Vector3(westX, 1.5f, -28f), new Vector3(0.45f, 3f, 0.45f), Palette.House, true);
        CreateBox(parent, "EastGatePostSouth", new Vector3(eastX, 1.5f, -32f), new Vector3(0.45f, 3f, 0.45f), Palette.House, true);
        CreateBox(parent, "EastGatePostNorth", new Vector3(eastX, 1.5f, -28f), new Vector3(0.45f, 3f, 0.45f), Palette.House, true);
        CreateBox(parent, "WestDoorLintel", new Vector3(westX, 3.35f, -30f), new Vector3(wallThickness + 0.2f, 0.45f, 4.4f), Palette.House, true);
        CreateBox(parent, "EastDoorLintel", new Vector3(eastX, 3.35f, -30f), new Vector3(wallThickness + 0.2f, 0.45f, 4.4f), Palette.House, true);
        CreateBox(parent, "CourtyardRoof", new Vector3(c.x, wallHeight + 0.22f, c.z), new Vector3(CourtyardSize.x + 1.1f, 0.44f, CourtyardSize.z + 1.1f), Palette.Roof, true);
        CreateBox(parent, "CourtyardInteriorFill", new Vector3(c.x, wallHeight * 0.5f, c.z), new Vector3(CourtyardSize.x - 8.5f, wallHeight, CourtyardSize.z - 1.8f), Palette.Courtyard, true);

        var westMarker = new GameObject("HomeEntranceMarker");
        westMarker.transform.SetParent(parent, false);
        westMarker.transform.SetPositionAndRotation(new Vector3(westX, 0.15f, -30f), Quaternion.LookRotation(Vector3.left, Vector3.up));

        var eastMarker = new GameObject("HomeEastEntranceMarker");
        eastMarker.transform.SetParent(parent, false);
        eastMarker.transform.SetPositionAndRotation(new Vector3(eastX, 0.15f, -30f), Quaternion.LookRotation(Vector3.right, Vector3.up));

        var courtyardMarker = new GameObject("CourtyardCenterMarker");
        courtyardMarker.transform.SetParent(parent, false);
        courtyardMarker.transform.position = new Vector3(c.x, 0.05f, c.z);
    }

    static void BuildVillage(Transform parent)
    {
        CreateVillageHouse(parent, "StreetHouse_A", StreetHouseACenter, StreetHouseASize.x, StreetHouseASize.z, StreetHouseASize.y, Vector3.back, Palette.Village);
        CreateVillageHouse(parent, "StreetHouse_B", StreetHouseBCenter, StreetHouseBSize.x, StreetHouseBSize.z, StreetHouseBSize.y, Vector3.back, Palette.House);
        CreateVillageHouse(parent, "StreetHouse_C", StreetHouseCCenter, StreetHouseCSize.x, StreetHouseCSize.z, StreetHouseCSize.y, Vector3.left, Palette.Village);
    }

    static void BuildVillageEntrance(Transform parent)
    {
        float gap = 6f;
        CreateBox(parent, "GatePostWest", VillageGateCenter + new Vector3(-gap * 0.5f, 1.6f, 0f), new Vector3(0.7f, 3.2f, 0.7f), Palette.House, true);
        CreateBox(parent, "GatePostEast", VillageGateCenter + new Vector3(gap * 0.5f, 1.6f, 0f), new Vector3(0.7f, 3.2f, 0.7f), Palette.House, true);
        CreateBox(parent, "GateLintel", VillageGateCenter + new Vector3(0f, 3.35f, 0f), new Vector3(gap + 1.2f, 0.45f, 0.7f), Palette.Roof, true);
    }

    static void CreateVillageHouse(Transform parent, string name, Vector3 center, float width, float depth, float height, Vector3 doorDir, Material wall)
    {
        CreateBuilding(parent, name, center, width, depth, height, doorDir, 2f, 2.4f, wall, name + "EntranceMarker");
    }

    static int BuildBamboo(Transform parent)
    {
        var rng = new System.Random(BambooSeed);
        var planted = new List<Vector2>();
        int created = 0;
        int attempts = 0;
        Bounds workshopBounds = new Bounds(WorkshopCenter, new Vector3(WorkshopSize.x + 8f, 12f, WorkshopSize.z + 10f));
        Rect forest = new Rect(
            BambooCenter.x - BambooArea.x * 0.5f,
            BambooCenter.z - BambooArea.z * 0.5f,
            BambooArea.x,
            BambooArea.z);

        while (created < BambooCount && attempts < 900)
        {
            attempts++;
            float x = forest.xMin + (float)rng.NextDouble() * forest.width;
            float z = forest.yMin + (float)rng.NextDouble() * forest.height;
            var pos = new Vector2(x, z);
            var world = new Vector3(x, 0f, z);

            if (workshopBounds.Contains(world))
                continue;
            if (NearPath(world, NorthToBamboo, 4.2f) || NearPath(world, NorthToStreet, 4.2f))
                continue;

            bool tooClose = false;
            for (int i = 0; i < planted.Count; i++)
            {
                if ((planted[i] - pos).sqrMagnitude < 2.1f * 2.1f)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                continue;

            planted.Add(pos);
            CreateBamboo(parent, "Bamboo_" + created.ToString("00"), world, rng);
            created++;
        }

        return created;
    }

    static void CreateBamboo(Transform parent, string name, Vector3 position, System.Random rng)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        float height = 3.4f + (float)rng.NextDouble() * 2.4f;
        float radius = 0.11f + (float)rng.NextDouble() * 0.05f;
        root.transform.position = position;

        GameObject stalk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stalk.name = "Stalk";
        stalk.transform.SetParent(root.transform, false);
        stalk.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
        stalk.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        AssignMaterial(stalk, Palette.Bamboo);

        int nodes = 2 + rng.Next(2);
        for (int i = 1; i <= nodes; i++)
        {
            float y = height * (i / (float)(nodes + 1));
            GameObject node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            node.name = "Node_" + i;
            node.transform.SetParent(root.transform, false);
            node.transform.localPosition = new Vector3(0f, y, 0f);
            node.transform.localScale = new Vector3(radius * 2.4f, 0.12f, radius * 2.4f);
            AssignMaterial(node, Palette.Bamboo);
            CapsuleCollider extra = node.GetComponent<Collider>() as CapsuleCollider;
            Object.DestroyImmediate(node.GetComponent<Collider>());
            if (extra != null)
                Object.DestroyImmediate(extra);
        }

        int leaves = 2 + rng.Next(2);
        for (int i = 0; i < leaves; i++)
        {
            GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = "Leaf_" + i;
            leaf.transform.SetParent(root.transform, false);
            float yaw = (float)rng.NextDouble() * 360f;
            float y = height * (0.62f + 0.28f * (float)rng.NextDouble());
            leaf.transform.localPosition = new Vector3(0f, y, 0f);
            leaf.transform.localRotation = Quaternion.Euler(-25f - (float)rng.NextDouble() * 20f, yaw, 12f);
            leaf.transform.localScale = new Vector3(1.1f + (float)rng.NextDouble() * 0.5f, 0.06f, 0.28f);
            AssignMaterial(leaf, Palette.Leaf);
            Object.DestroyImmediate(leaf.GetComponent<Collider>());
        }
    }

    static void BuildNpcs(Transform parent)
    {
        CreateNpc(
            parent,
            "村口大爷",
            new Vector3(77.4f, 1f, 71.2f),
            Quaternion.LookRotation(new Vector3(-5.4f, 0f, 4.8f), Vector3.up),
            Palette.NpcElder,
            1.05f);
        CreateNpc(
            parent,
            "舅舅",
            new Vector3(28.6f, 1f, -33.6f),
            Quaternion.LookRotation(Vector3.right, Vector3.up),
            Palette.NpcUncle,
            1.12f);
    }

    static void CreateNpc(Transform parent, string npcName, Vector3 position, Quaternion rotation, Material material, float heightScale)
    {
        GameObject npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        npc.name = npcName;
        npc.transform.SetParent(parent, false);
        npc.transform.SetPositionAndRotation(position, rotation);
        npc.transform.localScale = new Vector3(0.95f, heightScale, 0.95f);
        AssignMaterial(npc, material);

        CapsuleCollider collider = npc.GetComponent<CapsuleCollider>();
        if (collider != null)
            collider.isTrigger = false;

        NpcCharacter character = npc.GetComponent<NpcCharacter>();
        if (character == null)
            character = npc.AddComponent<NpcCharacter>();
        character.Configure(npcName);
    }

    static void ApplyWorldFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.70f, 0.74f, 0.76f);
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = 150f;
    }

    static void BuildMountains(Transform parent)
    {
        // 山放在很外围，地面继续铺到山外。游玩范围内只看到雾和远山，看不到地图切边。
        CreatePeak(parent, "Mountain_NorthWestA", new Vector3(-250f, 0f, 210f), new Vector3(36f, 28f, 32f));
        CreatePeak(parent, "Mountain_NorthWestB", new Vector3(-210f, 0f, 240f), new Vector3(28f, 22f, 24f));
        CreatePeak(parent, "Mountain_North", new Vector3(-20f, 0f, 250f), new Vector3(42f, 24f, 26f));
        CreatePeak(parent, "Mountain_NorthEast", new Vector3(210f, 0f, 245f), new Vector3(34f, 26f, 28f));
        CreatePeak(parent, "Mountain_EastNorth", new Vector3(270f, 0f, 140f), new Vector3(28f, 30f, 36f));
        CreatePeak(parent, "Mountain_East", new Vector3(280f, 0f, -10f), new Vector3(26f, 22f, 40f));
        CreatePeak(parent, "Mountain_SouthEast", new Vector3(250f, 0f, -230f), new Vector3(36f, 24f, 32f));
        CreatePeak(parent, "Mountain_South", new Vector3(20f, 0f, -250f), new Vector3(44f, 20f, 26f));
        CreatePeak(parent, "Mountain_SouthWest", new Vector3(-160f, 0f, -245f), new Vector3(36f, 22f, 28f));
        CreatePeak(parent, "Mountain_WestSouth", new Vector3(-275f, 0f, -140f), new Vector3(28f, 24f, 36f));
        CreatePeak(parent, "Mountain_West", new Vector3(-285f, 0f, 10f), new Vector3(26f, 26f, 44f));
        CreatePeak(parent, "Mountain_WestNorth", new Vector3(-260f, 0f, 90f), new Vector3(24f, 20f, 28f));
    }

    static void CreatePeak(Transform parent, string name, Vector3 baseCenter, Vector3 size)
    {
        Vector3 center = baseCenter + Vector3.up * (size.y * 0.5f);
        CreateBox(parent, name, center, size, Palette.Mountain, true);
    }

    static void CreateBuilding(
        Transform parent,
        string name,
        Vector3 center,
        float width,
        float depth,
        float height,
        Vector3 doorDir,
        float doorWidth,
        float doorHeight,
        Material wallMaterial,
        string markerName)
    {
        Transform building = CreateGroup(parent, name);
        doorDir = Flatten(doorDir);
        Vector3 right = Vector3.Cross(Vector3.up, doorDir).normalized;
        Vector3 back = -doorDir;
        float wall = 0.4f;
        float wallY = height * 0.5f;

        Vector3 foundationCenter = center + Vector3.up * 0.1f;
        CreateOrientedBox(building, "Foundation", foundationCenter, new Vector3(width + 0.3f, 0.2f, depth + 0.3f), wallMaterial, right, doorDir);

        CreateOrientedBox(building, "WallBack", center + back * (depth * 0.5f - wall * 0.5f) + Vector3.up * wallY, new Vector3(width, height, wall), wallMaterial, right, doorDir);
        CreateOrientedBox(building, "WallLeft", center - right * (width * 0.5f - wall * 0.5f) + Vector3.up * wallY, new Vector3(wall, height, depth), wallMaterial, right, doorDir);
        CreateOrientedBox(building, "WallRight", center + right * (width * 0.5f - wall * 0.5f) + Vector3.up * wallY, new Vector3(wall, height, depth), wallMaterial, right, doorDir);

        float sideWidth = (width - doorWidth) * 0.5f;
        Vector3 frontCenter = center + doorDir * (depth * 0.5f - wall * 0.5f) + Vector3.up * wallY;
        CreateOrientedBox(building, "WallFrontLeft", frontCenter - right * ((doorWidth + sideWidth) * 0.5f), new Vector3(sideWidth, height, wall), wallMaterial, right, doorDir);
        CreateOrientedBox(building, "WallFrontRight", frontCenter + right * ((doorWidth + sideWidth) * 0.5f), new Vector3(sideWidth, height, wall), wallMaterial, right, doorDir);

        float lintelHeight = Mathf.Max(0.3f, height - doorHeight);
        Vector3 lintelPos = center + doorDir * (depth * 0.5f - wall * 0.5f) + Vector3.up * (doorHeight + lintelHeight * 0.5f);
        CreateOrientedBox(building, "DoorLintel", lintelPos, new Vector3(doorWidth, lintelHeight, wall), wallMaterial, right, doorDir);

        Vector3 roofCenter = center + Vector3.up * (height + 0.18f);
        CreateOrientedBox(building, "Roof", roofCenter, new Vector3(width + 0.9f, 0.36f, depth + 0.9f), Palette.Roof, right, doorDir);

        Vector3 markerPos = DoorWorldPosition(center, width, depth, doorDir) + doorDir * 0.6f;
        markerPos.y = 0.15f;
        var marker = new GameObject(markerName);
        marker.transform.SetParent(building, false);
        marker.transform.position = markerPos;
        marker.transform.rotation = Quaternion.LookRotation(doorDir, Vector3.up);
    }

    static void PlacePlayerSafely()
    {
        GameObject player = FindRoot("Player");
        if (player == null)
            throw new InvalidOperationException("找不到第一阶段 Player，第二阶段不会重建玩家。");

        PlayerController controller = player.GetComponent<PlayerController>();
        CharacterController character = player.GetComponent<CharacterController>();
        if (controller == null || character == null)
            throw new InvalidOperationException("Player 缺少 PlayerController 或 CharacterController。");

        player.transform.SetPositionAndRotation(PlayerSpawn, PlayerSpawnRotation);
        SerializedObject serializedPlayer = new SerializedObject(controller);
        serializedPlayer.FindProperty("moveSpeed").floatValue = 7f;
        serializedPlayer.ApplyModifiedPropertiesWithoutUndo();

        GameObject cameraObject = FindRoot("Main Camera");
        if (cameraObject == null)
            throw new InvalidOperationException("找不到 Main Camera。");

        CameraFollow follow = cameraObject.GetComponent<CameraFollow>();
        if (follow == null)
            throw new InvalidOperationException("Main Camera 缺少 CameraFollow。");

        follow.ApplyGameplayView(player.transform);
    }

    public static string ValidateWorldMap()
    {
        var report = new StringBuilder();
        GameObject worldRoot = FindRoot(WorldRootName);
        GameObject player = FindRoot("Player");
        GameObject cameraObject = FindRoot("Main Camera");
        report.AppendLine("校验:");
        report.AppendLine("- Player 数量: " + CountRoots("Player"));
        report.AppendLine("- Main Camera 数量: " + CountRoots("Main Camera"));
        report.AppendLine("- WorldRoot: " + (worldRoot != null));
        report.AppendLine("- 旧 Ground 激活: " + LegacyGroundActive());

        if (worldRoot != null)
        {
            string[] groups = { "Terrain", "Roads", "WorkshopExterior", "BambooForest", "Courtyard", "Kitchen", "Bedroom", "River", "Bridge", "VillageBuildings", "VillageEntrance", "Npcs", "Mountains" };
            foreach (string group in groups)
            {
                Transform child = worldRoot.transform.Find(group);
                report.AppendLine("- " + group + ": " + (child != null ? child.childCount + " 个子对象" : "缺失"));
            }
        }

        if (player != null)
        {
            report.AppendLine("- 玩家位置: " + player.transform.position);
            report.AppendLine("- PlayerController: " + (player.GetComponent<PlayerController>() != null));
        }

        if (cameraObject != null)
        {
            CameraFollow follow = cameraObject.GetComponent<CameraFollow>();
            report.AppendLine("- CameraFollow: " + (follow != null));
            report.AppendLine("- 摄像机位置: " + cameraObject.transform.position);
        }

        Physics.SyncTransforms();
        CharacterController character = player != null ? player.GetComponent<CharacterController>() : null;
        if (character != null)
            character.enabled = false;
        try
        {
            Physics.SyncTransforms();
            report.AppendLine(CheckSpawnClearance());
            report.AppendLine(CheckApproach("工坊北门", WorkshopNorthDoor, Vector3.forward));
            report.AppendLine(CheckApproach("工坊南门", WorkshopSouthDoor, Vector3.back));
            report.AppendLine(CheckPathAlong("北门到竹林", NorthToBamboo));
            report.AppendLine(CheckPathAlong("北门到街坊", NorthToStreet));
            report.AppendLine(CheckPathAlong("南门到家", ConcatPaths(SouthToHome, new[] { new Vector3(-6.4f, 0f, -30f) })));
            report.AppendLine(CheckPathAlong("家到村口", ConcatPaths(new[] { new Vector3(30.4f, 0f, -30f) }, HomeToBridge)));
            report.AppendLine(CheckPathAlong("桥面通行", new[] { new Vector3(51f, 0f, 48f), new Vector3(69f, 0f, 48f) }));
            report.AppendLine(CheckRiverBlocked(new Vector3(48f, 1.2f, 16f), new Vector3(96f, 1.2f, 16f)));
            report.AppendLine("- WorkshopNorthMarker: " + (GameObject.Find("WorkshopNorthMarker") != null));
            report.AppendLine("- WorkshopSouthMarker: " + (GameObject.Find("WorkshopSouthMarker") != null));
            report.AppendLine("- HomeEntranceMarker: " + (GameObject.Find("HomeEntranceMarker") != null));
            report.AppendLine("- HomeEastEntranceMarker: " + (GameObject.Find("HomeEastEntranceMarker") != null));
            report.AppendLine("- 村口大爷: " + DescribeNpc("村口大爷"));
            report.AppendLine("- 舅舅: " + DescribeNpc("舅舅"));
        }
        finally
        {
            if (character != null)
                character.enabled = true;
        }
        return report.ToString().TrimEnd();
    }

    static string DescribeLayout()
    {
        var report = new StringBuilder();
        report.AppendLine("布局（对照草图，+Z 为北）:");
        report.AppendLine("- 地图: X=" + MapWidth + ", Z=" + MapDepth + "，地面铺满，山在外圈");
        report.AppendLine("- 工坊: 中心 " + WorkshopCenter + " 尺寸 " + WorkshopSize);
        report.AppendLine("- 北门 " + WorkshopNorthDoor + "：向左进竹林，向右去街坊");
        report.AppendLine("- 南门 " + WorkshopSouthDoor + "：出门去家里");
        report.AppendLine("- 竹林: 中心 " + BambooCenter + " 范围 " + BambooArea);
        report.AppendLine("- 街坊 A/B/C: " + StreetHouseACenter + " / " + StreetHouseBCenter + " / " + StreetHouseCCenter);
        report.AppendLine("- 庭院 " + CourtyardCenter + "，厨房 " + KitchenCenter + "，卧室 " + BedroomCenter);
        report.AppendLine("- 桥 " + BridgeCenter + "，村口 " + VillageGateCenter);
        report.AppendLine("- NPC: 村口大爷、舅舅");
        report.AppendLine("- 玩家出生点: " + PlayerSpawn);
        return report.ToString().TrimEnd();
    }

    static string CheckSpawnClearance()
    {
        Vector3 origin = PlayerSpawn + Vector3.up * 0.2f;
        bool hit = Physics.CapsuleCast(origin, origin + Vector3.up * 1.4f, 0.4f, Vector3.down, out RaycastHit info, 2.5f);
        if (!hit)
            return "- 出生点下方未命中地面";

        bool blocked = Physics.CheckCapsule(PlayerSpawn + Vector3.up * 0.45f, PlayerSpawn + Vector3.up * 1.5f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
        return "- 出生点地面: " + info.collider.name + " Y=" + info.point.y.ToString("0.###") + "，胶囊重叠: " + blocked;
    }

    static string DescribeNpc(string name)
    {
        GameObject npc = GameObject.Find(name);
        if (npc == null)
            return "缺失";
        NpcCharacter character = npc.GetComponent<NpcCharacter>();
        return "pos " + npc.transform.position.ToString("F1")
            + " name=" + (character != null ? character.DisplayName : npc.name);
    }

    static string CheckApproach(string label, Vector3 door, Vector3 outward)
    {
        Vector3 start = door + Flatten(outward) * 4.8f + Vector3.up * 1.2f;
        Vector3 end = door + Flatten(outward) * 0.55f + Vector3.up * 1.2f;
        Vector3 delta = end - start;
        bool blocked = Physics.SphereCast(start, 0.35f, delta.normalized, out RaycastHit hit, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
        return "- " + label + " 门外可到达: " + (!blocked) + (blocked ? " 阻挡=" + hit.collider.name : string.Empty);
    }

    static bool NearPath(Vector3 world, Vector3[] path, float radius)
    {
        float limit = radius * radius;
        Vector2 point = new Vector2(world.x, world.z);
        for (int i = 0; i < path.Length - 1; i++)
        {
            Vector2 a = new Vector2(path[i].x, path[i].z);
            Vector2 b = new Vector2(path[i + 1].x, path[i + 1].z);
            if (DistancePointSegmentSqr(point, a, b) <= limit)
                return true;
        }

        return false;
    }

    static float DistancePointSegmentSqr(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 offset = b - a;
        float denom = offset.sqrMagnitude;
        if (denom < 0.0001f)
            return (point - a).sqrMagnitude;
        float t = Mathf.Clamp01(Vector2.Dot(point - a, offset) / denom);
        return (point - (a + offset * t)).sqrMagnitude;
    }

    static string CheckPathAlong(string label, Vector3[] waypoints)
    {
        int blocked = 0;
        int grounded = 0;
        int samples = 0;
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            Vector3 a = WithProbeHeight(waypoints[i]);
            Vector3 b = WithProbeHeight(waypoints[i + 1]);
            int steps = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(a, b) / 2.5f));
            for (int s = 0; s <= steps; s++)
            {
                samples++;
                Vector3 p = Vector3.Lerp(a, b, s / (float)steps);
                if (Physics.CheckSphere(p, 0.28f, ~0, QueryTriggerInteraction.Ignore))
                    blocked++;
                if (Physics.Raycast(p + Vector3.up * 1.6f, Vector3.down, out RaycastHit hit, 3.5f))
                {
                    grounded++;
                    if (hit.point.y < -0.2f)
                        blocked++;
                }
            }
        }

        return "- " + label + " 采样着地 " + grounded + "/" + samples + "，穿模采样 " + blocked;
    }

    static Vector3[] ConcatPaths(Vector3 first, Vector3[] rest)
    {
        var points = new Vector3[rest.Length + 1];
        points[0] = first;
        Array.Copy(rest, 0, points, 1, rest.Length);
        return points;
    }

    static Vector3[] ConcatPaths(Vector3[] first, Vector3[] rest)
    {
        var points = new Vector3[first.Length + rest.Length];
        Array.Copy(first, points, first.Length);
        Array.Copy(rest, 0, points, first.Length, rest.Length);
        return points;
    }

    static Vector3[] ReversePath(Vector3[] path)
    {
        var reversed = new Vector3[path.Length];
        for (int i = 0; i < path.Length; i++)
            reversed[i] = path[path.Length - 1 - i];
        return reversed;
    }

    static Vector3 WithProbeHeight(Vector3 point)
    {
        point.y = 1.2f;
        return point;
    }

    static string CheckRiverBlocked(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        bool hit = Physics.SphereCast(from, 0.4f, delta.normalized, out RaycastHit info, delta.magnitude);
        return "- 非桥梁过河阻挡: " + hit + (hit ? " (" + info.collider.name + ")" : string.Empty);
    }

    static bool LegacyGroundActive()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == "Ground")
                return root.activeSelf;
        }

        return false;
    }

    static Vector3 DoorWorldPosition(Vector3 center, float width, float depth, Vector3 doorDir)
    {
        doorDir = Flatten(doorDir);
        Vector3 pos = center + doorDir * (depth * 0.5f);
        pos.y = 0f;
        pos += Vector3.Cross(Vector3.up, doorDir) * (width * 0f);
        return pos;
    }

    static GameObject CreateBox(Transform parent, string name, Vector3 center, Vector3 size, Material material, bool collider)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = center;
        box.transform.localScale = size;
        AssignMaterial(box, material);
        if (!collider)
            Object.DestroyImmediate(box.GetComponent<Collider>());
        return box;
    }

    static GameObject CreateOrientedBox(Transform parent, string name, Vector3 center, Vector3 size, Material material, Vector3 _, Vector3 doorDir)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = center;
        box.transform.rotation = Quaternion.LookRotation(Flatten(doorDir), Vector3.up);
        box.transform.localScale = new Vector3(size.x, size.y, size.z);
        AssignMaterial(box, material);
        return box;
    }

    static void AssignMaterial(GameObject target, Material material)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    static Transform CreateGroup(Transform parent, string name)
    {
        var group = new GameObject(name);
        group.transform.SetParent(parent, false);
        group.transform.localPosition = Vector3.zero;
        group.transform.localRotation = Quaternion.identity;
        return group.transform;
    }

    static Materials CreateMaterials()
    {
        return new Materials
        {
            Terrain = GetOrCreateLitMaterial(MaterialFolder + "/WhiteboxTerrain.mat", new Color(0.62f, 0.64f, 0.58f)),
            Road = GetOrCreateLitMaterial(MaterialFolder + "/WhiteboxRoad.mat", new Color(0.62f, 0.48f, 0.32f)),
            Workshop = GetOrCreateLitMaterial(MaterialFolder + "/WhiteboxWorkshop.mat", new Color(0.82f, 0.78f, 0.72f)),
            Shop = GetOrCreateLitMaterial(MaterialFolder + "/WhiteboxShop.mat", new Color(0.76f, 0.68f, 0.58f)),
            House = GetOrCreateLitMaterial(MaterialFolder + "/WhiteboxHouse.mat", new Color(0.80f, 0.76f, 0.71f)),
            Village = GetOrCreateLitMaterial(MaterialFolder + "/WhiteboxVillage.mat", new Color(0.70f, 0.66f, 0.62f)),
            Roof = GetOrCreateLitMaterial(MaterialFolder + "/WhiteboxRoof.mat", new Color(0.46f, 0.41f, 0.38f)),
            Water = GetOrCreateLitMaterial(MaterialFolder + "/WhiteboxWater.mat", new Color(0.34f, 0.48f, 0.58f)),
            Bank = GetOrCreateLitMaterial(MaterialFolder + "/WhiteboxBank.mat", new Color(0.58f, 0.54f, 0.46f)),
            Bamboo = GetOrCreateLitMaterial(MaterialFolder + "/WhiteboxBamboo.mat", new Color(0.46f, 0.56f, 0.38f)),
            Leaf = GetOrCreateLitMaterial(MaterialFolder + "/WhiteboxLeaf.mat", new Color(0.40f, 0.52f, 0.34f)),
            Mountain = GetOrCreateLitMaterial(MaterialFolder + "/WhiteboxMountain.mat", new Color(0.55f, 0.56f, 0.58f)),
            Courtyard = GetOrCreateLitMaterial(MaterialFolder + "/WhiteboxCourtyard.mat", new Color(0.68f, 0.66f, 0.60f)),
            NpcElder = GetOrCreateLitMaterial(MaterialFolder + "/WhiteboxNpcElder.mat", new Color(0.62f, 0.50f, 0.40f)),
            NpcUncle = GetOrCreateLitMaterial(MaterialFolder + "/WhiteboxNpcUncle.mat", new Color(0.48f, 0.54f, 0.46f))
        };
    }

    static Material GetOrCreateLitMaterial(string assetPath, Color color)
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(LitShaderPath);
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("找不到 URP Lit shader。");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material == null)
        {
            material = new Material(shader);
            ApplyFlatColor(material, color);
            AssetDatabase.CreateAsset(material, assetPath);
        }
        else
        {
            if (material.shader != shader)
                material.shader = shader;
            ApplyFlatColor(material, color);
            EditorUtility.SetDirty(material);
        }

        return material;
    }

    static void ApplyFlatColor(Material material, Color color)
    {
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.08f);
    }

    static Mesh CreateRibbonMesh(Vector3[] samples, float width, float y)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uvs = new List<Vector2>();
        float distance = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            Vector3 tangent;
            if (i == samples.Length - 1)
                tangent = samples[i] - samples[i - 1];
            else if (i == 0)
                tangent = samples[i + 1] - samples[i];
            else
                tangent = samples[i + 1] - samples[i - 1];
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 0.0001f)
                tangent = Vector3.forward;
            tangent.Normalize();
            Vector3 normal = new Vector3(-tangent.z, 0f, tangent.x);
            Vector3 p = samples[i];
            p.y = y;
            vertices.Add(p - normal * (width * 0.5f));
            vertices.Add(p + normal * (width * 0.5f));
            if (i > 0)
                distance += Vector3.Distance(samples[i], samples[i - 1]);
            uvs.Add(new Vector2(0f, distance * 0.1f));
            uvs.Add(new Vector2(1f, distance * 0.1f));
            if (i == 0)
                continue;
            int v = i * 2;
            triangles.Add(v - 2);
            triangles.Add(v - 1);
            triangles.Add(v);
            triangles.Add(v - 1);
            triangles.Add(v + 1);
            triangles.Add(v);
        }

        var mesh = new Mesh { name = "RiverSurface" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void SaveMesh(Mesh mesh, string path)
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return;
        }

        existing.Clear();
        existing.vertices = mesh.vertices;
        existing.triangles = mesh.triangles;
        existing.uv = mesh.uv;
        existing.normals = mesh.normals;
        existing.RecalculateBounds();
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(mesh);
    }

    static Vector3[] SampleCatmullRom(Vector3[] path, float spacing)
    {
        if (path == null || path.Length < 2)
            return path ?? Array.Empty<Vector3>();

        var points = new List<Vector3> { path[0] };
        for (int i = 0; i < path.Length - 1; i++)
        {
            Vector3 p0 = path[Mathf.Max(i - 1, 0)];
            Vector3 p1 = path[i];
            Vector3 p2 = path[i + 1];
            Vector3 p3 = path[Mathf.Min(i + 2, path.Length - 1)];
            float dist = Vector3.Distance(p1, p2);
            int steps = Mathf.Max(1, Mathf.RoundToInt(dist / Mathf.Max(0.2f, spacing)));
            for (int s = 1; s <= steps; s++)
                points.Add(CatmullRom(p0, p1, p2, p3, s / (float)steps));
        }

        return points.ToArray();
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    static Vector3 Flatten(Vector3 value)
    {
        value.y = 0f;
        if (value.sqrMagnitude < 0.0001f)
            return Vector3.forward;
        return value.normalized;
    }

    static GameObject FindRoot(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name)
                return root;
        }

        return null;
    }

    static int CountRoots(string name)
    {
        int count = 0;
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name)
                count++;
        }

        return count;
    }

    static void EnsureFolder(string assetPath)
    {
        assetPath = assetPath.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(assetPath))
            return;

        string parent = Path.GetDirectoryName(assetPath);
        if (string.IsNullOrEmpty(parent))
            return;

        parent = parent.Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
    }
}
