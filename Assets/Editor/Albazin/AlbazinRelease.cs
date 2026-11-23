using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;

public static class AlbazinRelease
{
    const string ScenePath = "Assets/Scenes/Ostrog.unity";
    const string Art = "Assets/AlbazinArt";
    static Transform landmarks;
    static Dictionary<string,Sprite> sprites;
    static Dictionary<string,Sprite> atlas;
    static Transform MapGroup(string path)
    {
        var parent = landmarks;
        foreach (string name in path.Split('/'))
        {
            var group = parent.Find(name);
            if (!group)
            {
                group = new GameObject(name).transform;
                group.SetParent(parent, false);
            }
            parent = group;
        }
        return parent;
    }

    static Tile GroundTile(string name)
    {
        string path=Art+"/Tiles/"+name;
        var tile=AssetDatabase.LoadAssetAtPath<Tile>(path+".asset");
        if (!tile) {tile=ScriptableObject.CreateInstance<Tile>();AssetDatabase.CreateAsset(tile,path+".asset");}
        tile.sprite=atlas[name];
        tile.colliderType=Tile.ColliderType.None;
        EditorUtility.SetDirty(tile);return tile;
    }
    static void Terrain()
    {
        var grid=new GameObject("Сетка • 1 метр",typeof(Grid));
        grid.transform.SetParent(MapGroup("Ландшафт"), false);
        var obj=new GameObject("Земля и тропы",typeof(Tilemap),typeof(TilemapRenderer));
        obj.transform.SetParent(grid.transform,false);
        var map=obj.GetComponent<Tilemap>();obj.GetComponent<TilemapRenderer>().sortingOrder=-10000;
        var grass=new[]{GroundTile("Grass"),GroundTile("Grass0"),GroundTile("Grass1"),GroundTile("Grass2")}; var water=GroundTile("Water");
        var road=Enumerable.Range(0,16).Select(i=>GroundTile("Road"+i)).ToArray();
        var paths=new HashSet<Vector2Int>();
        foreach(string line in File.ReadAllLines(Art+"/Roads.csv"))
        {
            var v=line.Split(',').Select(x=>float.Parse(x,CultureInfo.InvariantCulture)).ToArray();
            var a=new Vector2(v[0],v[1]);var b=new Vector2(v[2],v[3]);
            int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)*3));
            for(int i=0;i<=n;i++)
            {
                var cell=Vector2Int.FloorToInt(Vector2.Lerp(a,b,(float)i/n));
                paths.Add(cell);paths.Add(cell+Vector2Int.right);
            }
        }
        var bounds=new BoundsInt(-72,-74,0,158,152,1);
        TileBase[] data=new TileBase[bounds.size.x*bounds.size.y];
        int j=0;
        foreach(var v in bounds.allPositionsWithin)
        {
            Vector2Int c=new Vector2Int(v.x,v.y);
            int mask=0;
            if(!paths.Contains(c+Vector2Int.up)) mask|=1;
            if(!paths.Contains(c+Vector2Int.right)) mask|=2;
            if(!paths.Contains(c+Vector2Int.down)) mask|=4;
            if(!paths.Contains(c+Vector2Int.left)) mask|=8;
            data[j++]=v.x < -46 ? water : paths.Contains(c)?road[mask]:grass[(int)((uint)(v.x*73856093 ^ v.y*19349663) % 4)];
        }
        map.SetTilesBlock(bounds,data);
        Box("Берег Амура",-47,0,2,152);Box("Восточная граница",77,0,2,152);
        Box("Северная граница",14,67,128,2);Box("Южная граница",14,-66,128,2);
    }
    static void Box(string name,float x,float y,float w,float h)
    {
        var go=new GameObject(name);
        go.transform.SetParent(MapGroup("Ландшафт/Границы карты"), false);
        go.transform.position=new Vector2(x,y);
        go.AddComponent<BoxCollider2D>().size=new Vector2(w,h);
    }
    static GameObject Place(Transform parent,string prefix,string name,float x,float y,float scale=1)
    {
        var pair=sprites.First(s=>s.Key.StartsWith(prefix,StringComparison.Ordinal));
        var obj=new GameObject(name,typeof(SpriteRenderer));
        obj.transform.SetParent(parent,false);obj.transform.position=new Vector2(x,y);
        obj.transform.localScale=Vector3.one*scale;
        var sr=obj.GetComponent<SpriteRenderer>();sr.sprite=pair.Value;
        sr.sortingOrder=Depth(y);return obj;
    }
    static int Depth(float y)=>Mathf.RoundToInt(-y*16);
    static void Solid(GameObject obj,float w,float h,float x=0,float y=0)
    {
        var b=obj.AddComponent<BoxCollider2D>();b.size=new Vector2(w,h);b.offset=new Vector2(x,y);
    }
    static void Structure(int id,string prefix,float x,float y,float scale)
    {
        string category = prefix switch
        {
            "Башня" => "Башни",
            "Ворота" => "Ворота",
            "LongHouse" => "Длинные дома",
            _ => "Дома"
        };
        var obj=Place(MapGroup("Постройки/"+category),prefix,"Постройка "+id,x,y,scale);
        var size=obj.GetComponent<SpriteRenderer>().sprite.bounds.size;
        if(prefix=="Ворота")
        {
            // Leave an east-west passage at the east gate, north-south elsewhere.
            if(id==17){Solid(obj,1,.7f,0,-2.8f);Solid(obj,1,.7f,0,2.8f);}
            else {Solid(obj,.65f,1,-size.x*.39f,.5f);Solid(obj,.65f,1,size.x*.39f,.5f);}
            var part=obj.AddComponent<FortPart>();part.capacity=500;part.assaultTarget=id!=13;
        }
        else
        {
            float h=Mathf.Min(3.6f,size.y*.39f);
            Solid(obj,size.x*.88f,h,0,h*.5f);
            if(prefix=="Башня")obj.AddComponent<FortPart>().capacity=400;
        }
    }
    static void Fence(string section,Vector2 a,Vector2 b,string prefix,float step,bool vertical,bool flip,bool exterior, float gap=float.NaN)
    {
        var parent = MapGroup("Стены/"+section);
        int count=Mathf.CeilToInt(Vector2.Distance(a,b)/step);
        for(int i=0;i<count;i++)
        {
            var p=Vector2.Lerp(a,b,(i+.5f)/count);
            if(Mathf.Abs((vertical?p.y:p.x)-gap)<3.5f)continue;
            var obj=Place(parent,prefix,"Секция стены",p.x,p.y);
            obj.GetComponent<SpriteRenderer>().flipX=flip;
            float length=Vector2.Distance(a,b)/count;
            if (section.StartsWith("Внешние/"))
            {
                var renderer = obj.GetComponent<SpriteRenderer>();
                bool north = section.EndsWith("Северная");
                renderer.sprite = atlas[vertical ? "SideSpan" : north ? "NorthSpan" : "SouthSpan"];
                renderer.drawMode = SpriteDrawMode.Sliced;
                renderer.size = vertical ? new Vector2(2.65f, length + .12f)
                    : new Vector2(length + .12f, north ? 4.5f : 5);
            }
            Solid(obj,vertical?.85f:length+.12f,vertical?length+.12f:.8f);
            var part=obj.AddComponent<FortPart>();part.assaultTarget=exterior;
        }
    }
    [MenuItem("Албазин/Открыть игровую карту")]
    public static void OpenOstrog()
    {
        if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ScenePath);
    }
    static void Prop(string key,float x,float y,float scale,bool solid)
    {
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"/Tiles/"+key+".png");
        string category = key switch { "Trees" => "Деревья", "Barrel" => "Бочки", "Sacks" => "Мешки", _ => key };
        var go=new GameObject(key,typeof(SpriteRenderer));
        go.transform.SetParent(MapGroup("Окружение/"+category),false);
        go.transform.localScale=Vector3.one*scale;
        go.transform.position=new Vector2(x,y+sprite.bounds.size.y*scale/2);
        var sr=go.GetComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=Depth(y);
        if(solid)Solid(go,sprite.bounds.size.x*.48f,.52f,0,-sprite.bounds.size.y/2+.26f);
    }
    static void Yard()
    {
        // The stores share a smaller fenced yard northeast of the central paths.
        Fence("Складской двор",new Vector2(8,31),new Vector2(27,31),"Частокол_H",1,false,false,false);
        Fence("Складской двор",new Vector2(8,17),new Vector2(8,31),"Частокол_V",1.55f,true,false,false,22);
        Fence("Складской двор",new Vector2(8,17),new Vector2(25,17),"Частокол_H",1,false,false,false,16);
        Prop("Barrel",17.8f,10.6f,1,true);Prop("Sacks",18.8f,10.8f,1,true);
        Prop("Barrel",-1.8f,4,1,true);Prop("Sacks",29.4f,-4.5f,1,true);
        var rng=new System.Random(1684);
        for(int i=0;i<96;i++)
        {
            float x=i%2==0?58+(float)rng.NextDouble()*16:-43+(float)rng.NextDouble()*108;
            float y=i%2==0?-60+(float)rng.NextDouble()*119:46+(float)rng.NextDouble()*16;
            Prop("Trees",x,y,1+(float)rng.NextDouble()*.4f,true);
        }
    }
    public static void Bake()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Камера",typeof(Camera),typeof(AudioListener));camera.tag="MainCamera";
        var systems = new GameObject("Системы").transform;
        camera.transform.SetParent(systems, false);
        camera.transform.position=new Vector3(0,0,-10);camera.GetComponent<Camera>().orthographic=true;
        camera.GetComponent<Camera>().orthographicSize=9;
        var light=new GameObject("Дневной свет").AddComponent<UnityEngine.Rendering.Universal.Light2D>();
        light.transform.SetParent(systems, false);
        light.lightType=UnityEngine.Rendering.Universal.Light2D.LightType.Global;
        light.intensity=1;
        landmarks=new GameObject("Албазинский острог").transform;
        sprites=Directory.GetFiles("Assets/Sprites/AlbazinLandmarks","*.png").ToDictionary(s=>Path.GetFileNameWithoutExtension(s),s=>AssetDatabase.LoadAssetAtPath<Sprite>(s));
        atlas = AssetDatabase.LoadAllAssetsAtPath(Art+"/OstrogAtlas.png").OfType<Sprite>().ToDictionary(sprite => sprite.name);
        Terrain();
        Populate();
        Yard();
        Fence("Внешние/Северная",new Vector2(-37.7f,39),new Vector2(33.6f,39),"Стена_свер",2.5f,false,false,false);
        Fence("Внешние/Южная",new Vector2(-34.8f,-39.5f),new Vector2(39,-38.4f),"Стена_юж",2.8f,false,false,true,5.9f);
        Fence("Внешние/Западная",new Vector2(-37.7f,39),new Vector2(-34.8f,-39.5f),"Стена_вост",3.8f,true,true,false);
        Fence("Внешние/Восточная",new Vector2(33.6f,39),new Vector2(39,-38.4f),"Стена_вост",3.8f,true,false,true,-2.5f);
        Fence("Малый острог",new Vector2(-37,13),new Vector2(-18,13),"Частокол_H",1,false,false,false);
        Fence("Малый острог",new Vector2(-18,13),new Vector2(-18,-15),"Частокол_V",1.6f,true,false,false,-4);
        Fence("Малый острог",new Vector2(-18,-15),new Vector2(-35,-15),"Частокол_H",1,false,false,false);
        var balance=AssetDatabase.LoadAssetAtPath<AlbazinBalance>("Assets/Resources/Albazin/Balance.asset");
        if(!balance)AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<AlbazinBalance>(),"Assets/Resources/Albazin/Balance.asset");
        var game = new GameObject("Ночь над Албазином").AddComponent<AlbazinGame>();
        game.transform.SetParent(systems, false);
        game.EnsureActorGroups();
        EditorSceneManager.SaveScene(scene,ScenePath);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        AssetDatabase.SaveAssets();
    }
    public static void BakeWindows(){Bake();Windows();}
    public static void Linux()
    {
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Albazin-Linux", "Albazin.x86_64"));
        Build(BuildTarget.StandaloneLinux64, output);
    }
    public static void Windows()=>Build(BuildTarget.StandaloneWindows64,"Builds/Windows/Albazin.exe");
    static void Build(BuildTarget target,string output)
    {
        var report=BuildPipeline.BuildPlayer(new[]{ScenePath},output,target,BuildOptions.None);
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
    }
    static void Populate()
    {
        Structure(1,"Башня",-37.7f,35.8f,0.8f);
        Structure(3,"Башня",33.6f,35.6f,0.8f);
        Structure(25,"Башня",-34.8f,-43.0f,0.8f);
        Structure(29,"Башня",39.0f,-42.0f,0.8f);
        Structure(9,"Башня",-38.4f,11.1f,0.6f);
        Structure(16,"Башня",-37.4f,-17.6f,0.6f);
        Structure(2,"LongHouse",-13.0f,29.7f,0.9f);
        Structure(21,"LongHouse",-25.6f,-25.6f,0.8f);
        Structure(24,"LongHouse",-17.9f,-35.8f,0.93f);
        Structure(12,"Башня",17.2f,7.8f,1.0f);
        Structure(5,"Дом_смотрит_на_север",19.9f,28.9f,0.8f);
        Structure(10,"Дом_смотрит_на_север",19.8f,20.6f,0.7f);
        Structure(8,"Дом_смотрит_на_север",11.3f,23.9f,0.6f);
        Structure(6,"Дом_смотрит_на_восток(запад)",-22.4f,17.6f,0.65f);
        Structure(7,"Дом_смотрит_на_восток(запад)",-28.7f,17.6f,0.55f);
        Structure(11,"Дом_смотрит_на_север",-28.6f,6.5f,0.7f);
        Structure(14,"Дом_смотрит_на_север",-26.1f,-11.5f,0.8f);
        Structure(22,"Дом_смотрит_на_восток(запад)",30.5f,-18.5f,0.85f);
        Structure(26,"Дом_смотрит_на_восток(запад)",30.9f,-32.2f,0.85f);
        Structure(23,"Дом_смотрит_на_восток(запад)",17.3f,-20.2f,0.9f);
        Structure(4,"Дом_смотрит_на_восток(запад)",-35.2f,19.2f,0.45f);
        Structure(15,"Дом_смотрит_на_восток(запад)",29.8f,1.5f,0.5f);
        Structure(20,"Дом_смотрит_на_восток(запад)",10.1f,-12.5f,0.5f);
        Structure(27,"Дом_смотрит_на_восток(запад)",-3.7f,-37.2f,0.5f);
        Structure(30,"Дом_смотрит_на_юг",24.2f,-40.6f,0.35f);
        Structure(19,"Дом_смотрит_на_юг",4.5f,-17.6f,1.0f);
        Structure(13,"Ворота",-19.5f,-4.0f,1.0f);
        Structure(17,"Ворота",36.3f,-2.5f,1.0f);
        Structure(28,"Ворота",5.9f,-45.6f,1.3f);
    }
}
