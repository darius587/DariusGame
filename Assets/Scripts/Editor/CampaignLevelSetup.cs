using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using U = Zhuzbai.Platformer.Editor.GameEditorUtility;
using UI = Zhuzbai.Platformer.Editor.GameMenuSetup;
using Object = UnityEngine.Object;

namespace Zhuzbai.Platformer.Editor
{
    public static class CampaignLevelSetup
    {
        private const string Art = "Assets/Sprites/Phase5/";
        private const string Prefabs = "Assets/Prefabs/Phase5/";
        private static readonly string[] Paths = { U.Menu, U.Level, "Assets/Scenes/Level2.unity", "Assets/Scenes/Level3.unity" };
        private static readonly string[] Names = { "", "Green Valley", "Desert Canyon", "Dark Castle" };
        private static readonly string[] Objectives = { "", "Learn the platforms, collect 8 coins, find the exit.", "Collect 10 coins, avoid falling rocks, defeat enemies.", "Collect 12 coins, avoid bombs, use moving platforms." };
        private static bool abyssTestRunning;
        private static Material Material => AssetDatabase.LoadAssetAtPath<Material>("Assets/Sprites/Foundation/PixelUnlit.mat");

        [MenuItem("Tools/Zhuzbai/Setup Game Levels")]
        public static void Generate()
        {
            U.RequireEditMode();
            if (!File.Exists(U.Level) || !File.Exists(U.Menu)) throw new InvalidOperationException("Existing Phase 4 MainMenu and MainLevel are required.");
            U.Folder(Art.TrimEnd('/')); U.Folder(Prefabs.TrimEnd('/'));
            EnsureArt(); EnsurePrefabs();
            DariusManSetup.ApplyPrefabsIfInstalled();
            MultiDeviceControlsSetup.ApplyPrefabIfInstalled();
            var audio = GameAudioSetup.EnsurePrefab();
            U.EditScene(U.Level, scene => Configure(scene, 1, audio));
            for (int number = 2; number <= 3; number++)
            {
                int n = number;
                bool created = !File.Exists(Paths[n]);
                if (created) U.EditScene(U.Level, scene => {
                    if (!EditorSceneManager.SaveScene(scene, Paths[n], true)) throw new IOException("Could not copy MainLevel to " + Paths[n]);
                }, false);
                U.EditScene(Paths[n], scene => {
                    var definitions = U.All<LevelDefinition>(scene);
                    if (definitions.Length != 1 || (!created && definitions[0].GeneratedLayoutScene != scene.name))
                        throw new InvalidOperationException("Refusing to replace an unowned scene: " + scene.path + ". Move it aside or use a different scene name.");
                    if (created) BuildEnvironment(scene, n);
                    Configure(scene, n, audio);
                });
            }
            U.EditScene(U.Menu, scene => { UI.BuildScene(scene, false); GameAudioSetup.ConnectScene(scene, audio, false); });
            RegisterBuildScenes(); AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("Three levels generated. Existing layouts are retained on reruns. Open MainMenu alone and enter Play Mode. See PHASE5_SETUP.md.");
        }

        private static void Configure(Scene scene, int number, GameObject audio)
        {
            var session = U.All<LevelSession>(scene).Single();
            var definition = Get<LevelDefinition>(session.gameObject);
            var player = U.All<PlayerFoundation>(scene).Single();
            Transform spawn = session.transform.Find("Phase5 Spawn");
            if (spawn == null) { spawn = new GameObject("Phase5 Spawn").transform; spawn.SetParent(session.transform); spawn.position = player.transform.position; }
            Set(definition, "levelNumber", number); Set(definition, "levelName", Names[number]);
            Set(definition, "requiredCoins", 6 + number * 2); Set(definition, "objective", Objectives[number]);
            Set(definition, "nextScene", number < 3 || ShadowKingSetup.IsInstalled ? "Level" + (number + 1) : "");
            Set(definition, "generatedLayoutScene", scene.name); U.Reference(definition, "spawnPoint", spawn);
            Set(player.GetComponent<PlayerHealth>(), "maximumHealth", 3);
            UI.BuildScene(scene, true); BuildPhase5Ui(scene);
            GameAudioSetup.ConnectScene(scene, audio, true);
            DariusManSetup.ApplySceneIfInstalled(scene);
            MapBoundarySetup.ApplyScene(scene);
        }

        private static void BuildPhase5Ui(Scene scene)
        {
            var controller = U.All<GameMenuController>(scene).Single();
            var safe = (RectTransform)controller.transform.Find("Safe Area");
            var help = UI.Panel(safe, "How To Play", new Vector2(900, 940));
            UI.Label(help, "Title", "HOW TO PLAY", new Vector2(0, -40), new Vector2(760, 70), 44, Color.yellow);
            var objective = UI.Label(help, "Objective", "", new Vector2(0, -130), new Vector2(790, 185), 27, new Color32(255, 217, 126, 255));
            UI.Label(help, "Goals", "1. Collect all coins to unlock the exit gate.\n\n2. Reach the unlocked gate to finish the level.\n\n3. You have 3 HP. Avoid abyss falls, enemies, falling rocks, and bombs.\n\n4. Complete all four levels to win.", new Vector2(0, -330), new Vector2(790, 400), 28, Color.white);
            UI.MakeButton(help, "Close", "X", -100, MenuCommand.Back, controller);
            var close = (RectTransform)help.Find("Close"); UI.Place(close, Vector2.one, Vector2.one, new Vector2(-20, -20), new Vector2(64, 64));
            UI.MakeButton(help, "Got It", "GOT IT", -830, MenuCommand.Back, controller);
            U.Reference(Get<HowToPlayPanel>(help.gameObject), "objective", objective);
            U.Reference(controller, "howToPlayPanel", help.gameObject); help.gameObject.SetActive(false);
            foreach (var spec in new[] { ("Game Over", "GAME OVER", "RETRY", MenuCommand.Retry, "gameOverPanel"),
                ("Level Complete", "LEVEL COMPLETE", "NEXT LEVEL", MenuCommand.NextLevel, "levelCompletePanel"),
                ("Victory", "YOU WIN!", "REPLAY", MenuCommand.Replay, "victoryPanel") })
            {
                var panel = UI.Panel(safe, spec.Item1, new Vector2(800, 650));
                UI.Label(panel, "Title", spec.Item2, new Vector2(0, -65), new Vector2(740, 90), 48, Color.yellow);
                if (spec.Item4 == MenuCommand.NextLevel)
                    U.Reference(controller, "completedCoins", UI.Label(panel, "Coins", "COINS", new Vector2(0, -185), new Vector2(700, 60), 32, Color.white));
                UI.MakeButton(panel, "Continue", spec.Item3, -330, spec.Item4, controller);
                UI.MakeButton(panel, "Main Menu", "MAIN MENU", -445, MenuCommand.MainMenu, controller);
                U.Reference(controller, spec.Item5, panel.gameObject); panel.gameObject.SetActive(false);
            }
            safe.Find("Corner Buttons").SetAsLastSibling();
            var hud = U.All<FoundationHud>(scene).Single();
            var layout = hud.GetComponentInChildren<PixelHudSafeArea>(true);
            var label = UI.Label(layout.transform, "Level Indicator", "LEVEL " + U.All<LevelDefinition>(scene).Single().LevelNumber,
                new Vector2(24, -118), new Vector2(150, 30), 22, new Color32(255, 231, 164, 255));
            UI.Place(label.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -118), new Vector2(150, 30));
            label.alignment = TextAnchor.MiddleLeft; U.Reference(hud, "levelIndicator", label);
            label.enabled = false;
            var badgeRect = UI.Rect(label.transform, "Pixel Badge");
            UI.Place(badgeRect, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(140, 35));
            var badge = Get<Image>(badgeRect.gameObject); badge.preserveAspect = true; badge.raycastTarget = false;
            badge.sprite = Sprite("Level" + U.All<LevelDefinition>(scene).Single().LevelNumber);
            U.Reference(hud, "levelBadge", badge); U.References(hud, "levelBadges", new[] { Sprite("Level1"), Sprite("Level2"), Sprite("Level3") });
        }

        // A new scene is a Unity API copy, retaining the player, HUD, camera, input and audio references.
        // Only the initial copied environment is replaced. Once owned, no layout is rebuilt on rerun.
        private static void BuildEnvironment(Scene scene, int number)
        {
            var foundation = scene.GetRootGameObjects().First(root => root.GetComponentInChildren<PlayerFoundation>(true) != null);
            foreach (string name in new[] { "Terrain", "Brick Platforms", "Background" })
            {
                var child = foundation.transform.Find(name); if (child != null) Object.DestroyImmediate(child.gameObject);
            }
            foreach (var coin in U.All<CoinCollectible>(scene)) Object.DestroyImmediate(coin.gameObject);
            foreach (var gate in U.All<ExitGate>(scene)) Object.DestroyImmediate(gate.gameObject);
            foreach (var zone in U.All<FallHazard>(scene)) Object.DestroyImmediate(zone.gameObject);
            var root = new GameObject("Phase5 Environment").transform;
            var player = U.All<PlayerFoundation>(scene).Single(); player.transform.position = new Vector3(2, 1.2f, 0);
            var oldSpawn = U.All<LevelDefinition>(scene).Single().SpawnPoint; if (oldSpawn != null) oldSpawn.position = player.transform.position;
            var camera = U.All<CameraFollow2D>(scene).Single();
            Set(camera, "maximum", new Vector2(53, 5.5f)); Set(camera, "horizontalWorldBounds", new Vector2(-1, 61));
            camera.transform.position = new Vector3(8, 2.5f, -10);
            camera.GetComponent<Camera>().backgroundColor = number == 2 ? new Color32(244, 184, 115, 255) : new Color32(37, 27, 64, 255);
            int[][] ranges = number == 2 ? new[] { new[] {-1,10}, new[] {14,27}, new[] {32,44}, new[] {49,60} } :
                new[] { new[] {-1,9}, new[] {14,23}, new[] {29,39}, new[] {45,60} };
            foreach (var range in ranges) for (int x = range[0]; x <= range[1]; x++) for (int y = 0; y < 3; y++)
                Solid(root, "Ground", new Vector2(x, -0.5f-y), Vector2.one, number == 2 ? "Sand" : "Stone");
            if (number == 2)
            {
                Platform(root, 12, 1.1f, 2); Platform(root, 28.5f, 1.8f, 3); Platform(root, 36.5f, 2.3f, 4); Platform(root, 46.5f, 1.5f, 2);
                Coins(root, new[] {new Vector2(5,1),new Vector2(9,1),new Vector2(12,2.2f),new Vector2(17,1),new Vector2(23,1),new Vector2(29,2.8f),new Vector2(37,3.4f),new Vector2(42,1),new Vector2(47,2.5f),new Vector2(56,1)});
                Enemy(root, 18,24,2,1.1f); Enemy(root,36,42,2,1.1f);
                Spawner(root,19,false); Spawner(root,40,false);
            }
            else
            {
                Platform(root,11.5f,1.4f,2.5f); Platform(root,20,2,3); Platform(root,34,2.3f,3); Platform(root,51,2,3);
                Moving(root,new Vector2(24.5f,2),new Vector2(27.5f,2)); Moving(root,new Vector2(41,1.2f),new Vector2(43.5f,1.2f));
                Coins(root,new[] {new Vector2(5,1),new Vector2(8,1),new Vector2(12,2.4f),new Vector2(16,1),new Vector2(21,3),new Vector2(26,3),new Vector2(31,1),new Vector2(35,3.4f),new Vector2(42,2.2f),new Vector2(47,1),new Vector2(52,3.1f),new Vector2(57,1)});
                Enemy(root,17,21,3,1.4f); Enemy(root,33,38,3,1.4f); Enemy(root,51,56,3,1.4f);
                Spawner(root,19,true); Spawner(root,36,true); Spawner(root,53,true);
            }
            var gateObject = Instance("Assets/Prefabs/Foundation/ExitGate.prefab", root, new Vector2(59,1)); gateObject.name = "Exit Gate";
            var fall = new GameObject("Fall Zone", typeof(BoxCollider2D), typeof(FallHazard)); fall.transform.SetParent(root); fall.transform.position = new Vector3(30,-6,0);
            fall.GetComponent<BoxCollider2D>().size = new Vector2(76,3); fall.GetComponent<BoxCollider2D>().isTrigger = true;
            for (int x = 2; x < 61; x += 9)
            {
                var background = Visual(root, number == 2 ? "Canyon Mountain" : "Castle Tower", new Vector2(x, number == 2 ? 3 : 3.4f), number == 2 ? FoundationSprite("Mountain") : Sprite("Tower"));
                background.transform.localScale = number == 2 ? new Vector3(7,6,1) : new Vector3(3,7,1);
                background.GetComponent<SpriteRenderer>().sortingOrder = -15;
                background.GetComponent<SpriteRenderer>().color = number == 2 ? new Color32(210,125,66,255) : new Color32(94,76,121,255);
                var decoration = Visual(root, number == 2 ? "Cactus" : "Castle Glow", new Vector2(x+2,number == 2 ? 0.7f : 4), Sprite(number == 2 ? "Cactus" : "Warning"));
                decoration.GetComponent<SpriteRenderer>().sortingOrder = -5;
                if (number == 3) decoration.GetComponent<SpriteRenderer>().color = new Color(0.5f,0.8f,1,0.6f);
            }
        }
        private static void Coins(Transform root, Vector2[] positions) { foreach (var p in positions) Instance("Assets/Prefabs/Foundation/Coin.prefab", root, p); }
        private static void Platform(Transform root, float x, float y, float width) => Solid(root,"Canyon / Castle Platform",new Vector2(x,y),new Vector2(width,0.5f),"Stone");
        private static GameObject Solid(Transform root, string name, Vector2 position, Vector2 size, string art)
        {
            var go = Visual(root,name,position,Sprite(art)); go.transform.localScale = size; go.AddComponent<BoxCollider2D>().size = Vector2.one;
            return go;
        }
        private static GameObject Visual(Transform root,string name,Vector2 position,Sprite sprite)
        {
            var go = new GameObject(name,typeof(SpriteRenderer)); go.transform.SetParent(root); go.transform.position = position;
            var renderer = go.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = Material;
            return go;
        }
        private static GameObject Instance(string path, Transform root, Vector2 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (prefab == null) throw new InvalidOperationException("Missing prefab " + path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root); go.transform.position = position; return go;
        }
        private static void Enemy(Transform root,float left,float right,int hp,float speed)
        {
            var go = Instance(Prefabs+"PatrolEnemy.prefab",root,new Vector2(left,0.45f));
            var enemy = go.GetComponent<PatrolEnemy>(); Set(enemy,"left",left); Set(enemy,"right",right); Set(enemy,"hitPoints",hp); Set(enemy,"speed",speed);
        }
        private static void Moving(Transform root,Vector2 start,Vector2 end)
        {
            var go = Instance(Prefabs+"MovingPlatform.prefab",root,start); Set(go.GetComponent<MovingPlatform>(),"start",start); Set(go.GetComponent<MovingPlatform>(),"end",end);
        }
        private static void Spawner(Transform root,float x,bool bomb)
        {
            var go = new GameObject(bomb ? "Bomb Spawner" : "Rock Spawner",typeof(HazardSpawner)); go.transform.SetParent(root); go.transform.position = new Vector3(x,9,0);
            var spawner = go.GetComponent<HazardSpawner>(); U.Reference(spawner,"prefab",AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+(bomb?"Bomb":"Rock")+".prefab").GetComponent<FallingProjectile>());
            U.Reference(spawner,"warningSprite",Sprite("Warning")); Set(spawner,"interval",bomb?4.5f:3.5f); Set(spawner,"maximumAlive",2);
        }

        private static void EnsurePrefabs()
        {
            foreach (string name in new[] { "PatrolEnemy", "Rock", "Bomb", "MovingPlatform" })
            {
                string path = Prefabs+name+".prefab"; if (File.Exists(path)) continue;
                var go = Visual(null,name,Vector2.zero,Sprite(name == "PatrolEnemy" ? "Enemy" : name == "MovingPlatform" ? "Stone" : name));
                try
                {
                    var body = go.AddComponent<Rigidbody2D>(); body.freezeRotation = true;
                    body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                    if (name == "PatrolEnemy") { go.AddComponent<BoxCollider2D>().size = new Vector2(0.85f,0.8f); body.bodyType = RigidbodyType2D.Kinematic; body.useFullKinematicContacts = true; go.AddComponent<PatrolEnemy>(); }
                    else if (name == "MovingPlatform") { go.transform.localScale = new Vector3(3,0.5f,1); go.AddComponent<BoxCollider2D>().size=Vector2.one; body.bodyType=RigidbodyType2D.Kinematic; body.interpolation=RigidbodyInterpolation2D.Interpolate; go.AddComponent<MovingPlatform>(); }
                    else { go.transform.localScale=Vector3.one*0.7f; go.AddComponent<CircleCollider2D>().radius=0.42f; body.gravityScale=1.2f; var projectile=go.AddComponent<FallingProjectile>(); Set(projectile,"bomb",name=="Bomb"); Set(projectile,"damage",name=="Bomb"?2:1); }
                    PrefabUtility.SaveAsPrefabAsset(go,path);
                }
                finally { Object.DestroyImmediate(go); }
            }
        }
        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art+name+".png");
        private static Sprite FoundationSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Foundation/"+name+".png");
        private static void EnsureArt()
        {
            EnsureLevelBadges();
            foreach (string name in new[] { "Sand", "Stone", "Enemy", "Rock", "Bomb", "Tower", "Cactus", "Warning" })
            {
                string path=Art+name+".png"; if (File.Exists(path)) continue;
                var texture=new Texture2D(16,16,TextureFormat.RGBA32,false);
                try
                {
                    var pixels=new Color32[256];
                    for(int y=0;y<16;y++) for(int x=0;x<16;x++)
                    {
                        bool edge=x==0||x==15||y==0||y==15; Color32 color=new Color32(0,0,0,0);
                        if(name=="Sand") color=(x*7+y*3)%13<2?new Color32(188,133,67,255):new Color32(235,191,108,255);
                        if(name=="Stone") color=y%8==0||(x+(y/8)*8)%16==0?new Color32(59,56,77,255):new Color32(124,121,145,255);
                        if(name=="Enemy" && x>=1&&x<=14&&y>=2&&y<=12) color=y>8&&((x>=3&&x<=5)||(x>=10&&x<=12))?new Color32(255,238,154,255):new Color32(179,67,83,255);
                        if((name=="Rock"||name=="Bomb")&&Vector2.Distance(new Vector2(x,y),new Vector2(7.5f,7.5f))<6.5f) color=name=="Bomb"?new Color32(53,47,70,255):new Color32((byte)(100+x*4),(byte)(90+x*3),83,255);
                        if(name=="Bomb"&&y>=13&&x>=7&&x<=9)color=new Color32(255,179,66,255);
                        if(name=="Tower"&&(y<13||(x/3)%2==0))color=edge||y%5==0?new Color32(41,35,61,255):new Color32(111,94,133,255);
                        if(name=="Tower"&&y>=6&&y<=9&&x>=6&&x<=9)color=new Color32(255,194,110,255);
                        if(name=="Cactus"&&((x>=6&&x<=9&&y<15)||(y>=5&&y<=8&&x>=2&&x<=13)||(x>=2&&x<=4&&y>=7&&y<=11)||(x>=11&&x<=13&&y>=7&&y<=13)))color=new Color32(68,137,89,255);
                        float d=Vector2.Distance(new Vector2(x,y),new Vector2(7.5f,7.5f));
                        if(name=="Warning"&&d>=5.4f&&d<=7.4f)color=new Color32(255,183,66,210);
                        pixels[y*16+x]=color;
                    }
                    texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(texture); }
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.textureType=TextureImporterType.Sprite; importer.spritePixelsPerUnit=16;
                importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.alphaIsTransparency=true; importer.SaveAndReimport();
            }
        }
        private static void EnsureLevelBadges()
        {
            string[] letters={"100/100/100/100/111","111/100/110/100/111","101/101/101/101/010","111/100/110/100/111","100/100/100/100/111"};
            string[] digits={"010/110/010/010/111","111/001/111/100/111","111/001/111/001/111"};
            for(int number=1;number<=3;number++)
            {
                string path=Art+"Level"+number+".png"; if(File.Exists(path))continue;
                var texture=new Texture2D(28,7,TextureFormat.RGBA32,false);
                try
                {
                    var pixels=new Color32[28*7];
                    for(int glyph=0;glyph<6;glyph++)
                    {
                        string[] rows=(glyph<5?letters[glyph]:digits[number-1]).Split('/');
                        int origin=glyph<5?glyph*4:23;
                        for(int y=0;y<5;y++)for(int x=0;x<3;x++)if(rows[4-y][x]=='1')pixels[(y+1)*28+origin+x]=new Color32(255,231,164,255);
                    }
                    texture.SetPixels32(pixels);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(texture); }
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spritePixelsPerUnit=16;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.SaveAndReimport();
            }
        }
        private static T Get<T>(GameObject go) where T:Component { var value=go.GetComponent<T>(); return value!=null?value:go.AddComponent<T>(); }
        private static void Set(Object target,string field,object value)
        {
            var data=new SerializedObject(target); var property=data.FindProperty(field);
            if(value is int i) property.intValue=i; else if(value is float f) property.floatValue=f; else if(value is bool b) property.boolValue=b; else if(value is string s) property.stringValue=s; else if(value is Vector2 v) property.vector2Value=v; else throw new ArgumentException(field);
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
            if(PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
        private static string[] BuildPaths => Paths.Concat(ShadowKingSetup.IsInstalled ? new[] { ShadowKingSetup.ScenePath } : Array.Empty<string>()).ToArray();
        private static EditorBuildSettingsScene[] Ordered(EditorBuildSettingsScene[] current) => BuildPaths.Select(p=>new EditorBuildSettingsScene(p,true)).Concat(current.Where(s=>!BuildPaths.Contains(s.path))).ToArray();
        private static void RegisterBuildScenes()
        {
            EditorBuildSettings.scenes=Ordered(EditorBuildSettings.scenes);
            foreach(string guid in AssetDatabase.FindAssets("t:BuildProfile")) { var profile=AssetDatabase.LoadAssetAtPath<BuildProfile>(AssetDatabase.GUIDToAssetPath(guid)); if(profile!=null&&profile.overrideGlobalScenes) { profile.scenes=Ordered(profile.scenes); EditorUtility.SetDirty(profile); } }
        }

        [MenuItem("Tools/Zhuzbai/Validate Game Levels")]
        public static void Validate()
        {
            U.RequireEditMode();
            foreach(string path in Paths) if(!File.Exists(path)) throw new InvalidOperationException("Missing scene: "+path+". Run Generate Three Levels.");
            U.EditScene(U.Menu, scene => { UI.ValidateScene(scene); ValidateAudio(scene); }, false);
            for(int i=1;i<=3;i++) { int number=i; U.EditScene(Paths[i],scene=>ValidateLevel(scene,number),false); }
            CheckBuild(EditorBuildSettings.scenes,"global scene list");
            foreach(string guid in AssetDatabase.FindAssets("t:BuildProfile")) { var p=AssetDatabase.LoadAssetAtPath<BuildProfile>(AssetDatabase.GUIDToAssetPath(guid)); if(p!=null&&p.overrideGlobalScenes)CheckBuild(p.scenes,p.name); }
            Debug.Log("Phase 5 structural validation passed: 8/10/12 coins, metadata, players, gates, fall zones, hazards, UI, audio and Build Profiles. Play Mode checks remain required.");
        }
        private static void CheckBuild(EditorBuildSettingsScene[] list,string owner) { if(list.Length<BuildPaths.Length||BuildPaths.Where((p,i)=>list[i].path!=p||!list[i].enabled).Any())throw new InvalidOperationException("Incorrect level scene order in "+owner); }
        private static void Require(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }
        private static void ValidateLevel(Scene scene,int number)
        {
            UI.ValidateScene(scene);
            var definition=U.All<LevelDefinition>(scene).Single();
            Require(definition.LevelNumber==number&&definition.RequiredCoins==6+number*2&&definition.SpawnPoint!=null,"Invalid level metadata/spawn in "+scene.name);
            Require(definition.NextScene==(number<3||ShadowKingSetup.IsInstalled?"Level"+(number+1):""),"Invalid next scene in "+scene.name);
            Require(U.All<CoinCollectible>(scene).Length==definition.RequiredCoins,"Incorrect coin count in "+scene.name);
            var player=U.All<PlayerFoundation>(scene).Single(); var body=player.GetComponent<Rigidbody2D>();
            Require(body!=null&&body.bodyType==RigidbodyType2D.Dynamic&&player.GetComponent<CapsuleCollider2D>()!=null&&player.GetComponent<PlayerHealth>().MaximumHealth==3,"Invalid player physics/health in "+scene.name);
            Require(U.All<ExitGate>(scene).Length==1&&U.All<ExitGate>(scene)[0].GetComponent<Collider2D>().isTrigger,"Missing gate trigger in "+scene.name);
            var zones=U.All<FallHazard>(scene); Require(zones.Length>0,"Missing fall zone in "+scene.name);
            foreach(var zone in zones) { var box=zone.GetComponent<BoxCollider2D>(); Require(box!=null&&box.isTrigger&&box.size.x>=40&&box.bounds.max.y<definition.SpawnPoint.position.y,"Invalid fall trigger in "+scene.name); }
            Require(U.All<AudioManager>(scene).Length==1&&U.All<GameplayAudioBridge>(scene).Length==1,"Audio manager/event bridge duplicates or missing in "+scene.name);
            ValidateAudio(scene);
            UiInputActionSetup.Validate(U.All<EventSystem>(scene).Single().GetComponent<InputSystemUIInputModule>(),scene.name);
            var controller=new SerializedObject(U.All<GameMenuController>(scene).Single());
            foreach(string field in new[]{"howToPlayPanel","gameOverPanel","levelCompletePanel","victoryPanel","completedCoins"})Require(controller.FindProperty(field).objectReferenceValue!=null,"Missing Phase 5 UI: "+field+" in "+scene.name);
            Require(U.All<HowToPlayPanel>(scene).Length==1,"Expected one contextual help panel in "+scene.name);
            var hud = new SerializedObject(U.All<FoundationHud>(scene).Single());
            Require(hud.FindProperty("levelIndicator").objectReferenceValue != null,"Missing HUD level indicator in " + scene.name);
            var bridge = new SerializedObject(U.All<GameplayAudioBridge>(scene).Single());
            Require(bridge.FindProperty("player").objectReferenceValue == player && bridge.FindProperty("health").objectReferenceValue == player.GetComponent<PlayerHealth>(),"Disconnected audio player/health in " + scene.name);
            foreach (var group in new[] { ("coins", U.All<CoinCollectible>(scene).Cast<Object>().ToArray()), ("fallZones", zones.Cast<Object>().ToArray()) })
            {
                var array = bridge.FindProperty(group.Item1);
                var assigned = Enumerable.Range(0, array.arraySize).Select(index => array.GetArrayElementAtIndex(index).objectReferenceValue).ToArray();
                Require(assigned.Length == group.Item2.Length && assigned.All(item => item != null) && assigned.Distinct().Count() == assigned.Length && assigned.All(group.Item2.Contains),"Disconnected audio " + group.Item1 + " in " + scene.name);
            }
            if(number>1)
            {
                Require(U.All<PatrolEnemy>(scene).Length>0&&U.All<HazardSpawner>(scene).Length>0,"Missing enemies/hazards in "+scene.name);
                foreach(var enemy in U.All<PatrolEnemy>(scene))
                {
                    var data=new SerializedObject(enemy);
                    Require(enemy.GetComponent<BoxCollider2D>()!=null&&enemy.GetComponent<Rigidbody2D>().bodyType==RigidbodyType2D.Kinematic&&data.FindProperty("left").floatValue<data.FindProperty("right").floatValue&&data.FindProperty("hitPoints").intValue==(number==3?3:2),"Incorrect enemy patrol/health/physics in "+scene.name);
                }
                foreach(var spawner in U.All<HazardSpawner>(scene)) { var data=new SerializedObject(spawner); var prefab=(FallingProjectile)data.FindProperty("prefab").objectReferenceValue; Require(prefab!=null&&EditorUtility.IsPersistent(prefab),"Missing persistent hazard prefab"); var p=new SerializedObject(prefab); Require(p.FindProperty("damage").intValue==(number==3?2:1)&&p.FindProperty("bomb").boolValue==(number==3)&&prefab.GetComponent<CircleCollider2D>()!=null&&prefab.GetComponent<Rigidbody2D>()!=null,"Incorrect hazard damage/physics"); Require(data.FindProperty("maximumAlive").intValue<=2&&data.FindProperty("interval").floatValue>=2,"Unbounded hazard spawner"); }
            }
            if(number==3) { Require(U.All<MovingPlatform>(scene).Length>=2,"Missing moving platforms"); foreach(var platform in U.All<MovingPlatform>(scene))Require(platform.GetComponent<Rigidbody2D>().bodyType==RigidbodyType2D.Kinematic&&platform.GetComponent<BoxCollider2D>()!=null,"Invalid moving platform physics"); }
            var tuning = new SerializedObject(player);
            float gravity = Mathf.Abs(Physics2D.gravity.y) * body.gravityScale;
            float jump = tuning.FindProperty("jumpSpeed").floatValue;
            float speed = tuning.FindProperty("moveSpeed").floatValue;
            Require(gravity > 0 && jump * jump / (2 * gravity) >= 2.6f && speed * 2 * jump / gravity >= 5,
                "Player tuning no longer supports the generated jumps. Restore inspected speed 6 / jump 13.5 / gravity scale 3, or redesign platforms.");
        }
        private static void ValidateAudio(Scene scene)
        {
            var managers=U.All<AudioManager>(scene); Require(managers.Length==1,"Expected one AudioManager in "+scene.name);
            var manager=managers[0]; var data=new SerializedObject(manager);
            var sources=new[]{"musicSource","effectsSource","uiSource","footstepsSource"}.Select(field=>data.FindProperty(field).objectReferenceValue as AudioSource).ToArray();
            Require(sources.All(s=>s!=null&&s.enabled&&!s.playOnAwake&&s.spatialBlend==0)&&sources.Distinct().Count()==4&&manager.GetComponentsInChildren<AudioSource>(true).Length==4,"Missing, duplicate or incorrectly configured audio sources in "+scene.name);
            foreach(string field in new[]{"music","footsteps","coin","jump","fall","shoot","damage","click"})Require(data.FindProperty(field).objectReferenceValue!=null,"Missing audio clip "+field+" in "+scene.name);
        }

        [MenuItem("Tools/Zhuzbai/Test Fall Damage")]
        public static void TestAbyss()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode in a fresh gameplay scene at 3 HP first. This test deliberately ends the run at GAME OVER.");
            var health=Object.FindAnyObjectByType<PlayerHealth>();
            if(health==null||health.CurrentHealth!=3||GameMenuController.IsGameplayBlocked)throw new InvalidOperationException("Start a fresh, unpaused level at 3 HP.");
            if(abyssTestRunning)throw new InvalidOperationException("An abyss test is already running.");
            var zone=Object.FindAnyObjectByType<FallHazard>();
            Require(zone!=null,"No fall zone in the active level.");
            int falls=0,damageSounds=0; Action<PlayerHealth> onFall=_=>falls++; Action onDamage=()=>damageSounds++;
            zone.PlayerFell+=onFall; health.Damaged+=onDamage;
            int expected=2,lastFrame=Time.frameCount; abyssTestRunning=true;
            EditorApplication.CallbackFunction tick=null;
            Action cleanup=()=>{ EditorApplication.update-=tick; if(zone!=null)zone.PlayerFell-=onFall; if(health!=null)health.Damaged-=onDamage; abyssTestRunning=false; };
            tick=()=>
            {
                if(!EditorApplication.isPlaying||health==null||zone==null) { cleanup(); return; }
                if(Time.frameCount==lastFrame)return;
                lastFrame=Time.frameCount;
                try
                {
                    health.GetComponent<Rigidbody2D>().linearVelocity=new Vector2(3,-15);
                    bool accepted=zone.TryHandleFall(health); bool duplicate=zone.TryHandleFall(health);
                    Require(accepted&&!duplicate&&health.CurrentHealth==expected,"Abyss HP / duplicate protection failed.");
                    Require(health.GetComponent<Rigidbody2D>().linearVelocity==Vector2.zero&&health.GetComponent<Rigidbody2D>().angularVelocity==0,"Respawn/death must zero velocity.");
                    if(expected-->0)return; // Next frame, still within normal invulnerability.
                    Require(falls==3&&damageSounds==0,"Expected one fall event and no ordinary damage sound per fall.");
                    Require(Time.timeScale==0&&GameMenuController.IsGameplayBlocked&&Object.FindAnyObjectByType<GameMenuController>().HasOutcome,"GAME OVER failed to pause gameplay.");
                    Debug.Log("Abyss test passed: 3 → 2 → 1 → 0, invulnerability bypass, duplicate protection, fall events, zero velocities, and paused outcome. RETRY must restore 3/3.");
                    cleanup();
                }
                catch(Exception exception) { cleanup(); Debug.LogException(exception); }
            };
            EditorApplication.update+=tick;
        }
    }
}
