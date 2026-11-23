using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public sealed partial class AlbazinGame : MonoBehaviour
{
    enum Phase { Menu, Day, Night, End }
    Phase phase;
    AlbazinBalance cfg;
    [SerializeField] Transform charactersRoot;
    [SerializeField] Transform residentsRoot;
    [SerializeField] Transform raidersRoot;
    AtlasWalker hero;
    Camera eye;
    FortPart[] fort;
    readonly List<Raider> raiders=new();
    readonly List<Resident> residents=new();
    readonly Dictionary<string,AudioClip> clips=new();
    AudioSource music,fx;
    float timeLeft,spawnIn,attackAt,invulnerable,stepAt,noticeUntil;
    int day=1,hp,maxHP,damage,gold,remaining,kills,prepared,taskStage;
    int speedLevel,damageLevel,hpLevel;
    enum Workshop { None, Smith, Armorer, Garrison }
    Workshop workshop;
    int armorLevel, armyLevel, waveNumber;
    bool paused, journal, credits, settings;
    float creditOffset;
    FortPart eastGate;
    Vector2 commanderSpot;
    Vector2 dayStart => commanderSpot + new Vector2(0, -2);
    int WaveTotal => Mathf.CeilToInt(cfg.waves[day-1] / (float)(day+1));
    float[] wallCheckpoint;
    int goldCheckpoint;
    string message="",ending="",headline="";

    readonly Vector2 gate=new(36.2f,-2.5f);
    readonly Vector2 smithSpot=new(15,6.4f), armorerSpot=new(21,6.3f), watchSpot=new(31,-3);
    static readonly string[] errands={"Донесение с восточных ворот","Скобы для ремонта","Приказ о вылазке"};
    static readonly string[] cargo={"донесение","связка скоб","запечатанный приказ"};
    bool Frozen=>paused||journal||credits||settings||workshop!=Workshop.None||dialogue!=null;
    string dialogue;
    class Resident { public string role; public AtlasWalker actor;public Vector2 start,end;public bool going; }
    class Raider { public AtlasWalker actor; public FortPart target; public bool chasing; public int hp; public float hitAt; }
    public void EnsureActorGroups()
    {
        if (!charactersRoot) charactersRoot = new GameObject("Персонажи").transform;
        if (!residentsRoot) residentsRoot = CreateActorGroup("Жители");
        if (!raidersRoot) raidersRoot = CreateActorGroup("Штурмующие");
    }

    Transform CreateActorGroup(string groupName)
    {
        var group = new GameObject(groupName).transform;
        group.SetParent(charactersRoot, false);
        return group;
    }

    void Start()
    {
        cfg=Resources.Load<AlbazinBalance>("Albazin/Balance");
        eye=Camera.main;eye.orthographic=true;eye.orthographicSize=9;
        fort=FindObjectsByType<FortPart>(FindObjectsSortMode.None);
        eastGate = fort.Single(w => w.name == "Постройка 17");
        var residence = GameObject.Find("Постройка 2");
        commanderSpot = (Vector2)residence.transform.position + new Vector2(0, -1.15f);
        EnsureActorGroups();
        hero=Actor(charactersRoot,"Защитник","Hero",dayStart,true);
        ResidentAt("Воевода","Commander",commanderSpot);
        ResidentAt("Кузнец","Smith",smithSpot);
        ResidentAt("Бронник","Armorer",armorerSpot);
        ResidentAt("Караульный","Watch",watchSpot);
        ResidentAt("Житель","Citizen",new Vector2(-6,-2),new Vector2(-11,-4));
        ResidentAt("Посыльный","Courier",new Vector2(9,1),new Vector2(12,-2));
        foreach(var id in new[]{"Day","Night","Swing","Strike","Wall","Coin","Talk","March","Step"})
            clips[id]=Resources.Load<AudioClip>("Albazin/Audio/"+id);
        music=gameObject.AddComponent<AudioSource>();fx=gameObject.AddComponent<AudioSource>();
        music.loop=true;music.volume=.32f;Music("Day");
        AudioListener.volume=PlayerPrefs.GetFloat("AlbazinVolume",.65f);
        LoadInterfaceArt();
        maxHP=hp=cfg.playerHP;damage=cfg.stab;gold=cfg.startingGold;
        PositionMenuCamera();
    }
    AtlasWalker Actor(Transform parent,string label,string key,Vector2 pos,bool solid)
    {
        var go=new GameObject(label);
        go.transform.SetParent(parent,false);
        go.transform.position=pos;var a=go.AddComponent<AtlasWalker>();
        a.Setup(key,solid);return a;
    }
    void ResidentAt(string role,string key,Vector2 pos,Vector2? end=null)
    {
        residents.Add(new Resident{role=role,start=pos,end=end??pos,going=true,actor=Actor(residentsRoot,role,key,pos,false)});
    }
    void Sound(string id,float volume=1){if(clips.TryGetValue(id,out var clip)&&clip)fx.PlayOneShot(clip,volume);}
    void Music(string id){music.clip=clips[id];music.Play();}
    void Hint(string text){message=text;noticeUntil=Time.time+4;}
    Vector2 Here=>hero.transform.position;
    Resident Near()=>residents.Where(r=>Vector2.Distance(Here,r.actor.transform.position)<1.85f)
        .OrderBy(r=>Vector2.Distance(Here,r.actor.transform.position)).FirstOrDefault();
    void Update()
    {
        if (credits && !creditSources) creditOffset += Time.unscaledDeltaTime * 35;
        var k=Keyboard.current;
        if(k==null)return;
        if (k.escapeKey.wasPressedThisFrame)
        {
            if (settings || credits) { settings=credits=false; Freeze(); }
            else if (phase==Phase.Day || phase==Phase.Night)
            {
                if (Frozen) ClosePanels(); else paused=true;
                Freeze();
            }
            return;
        }
        if(phase==Phase.Menu)
        {
            if(!Frozen && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame))BeginDay();
            return;
        }
        if(phase==Phase.End)return;
        if((!Frozen||journal) && k.jKey.wasPressedThisFrame){journal=!journal;Freeze();return;}
        if(Frozen){if(dialogue!=null&&k.eKey.wasPressedThisFrame){ClosePanels();Freeze();}return;}
        Vector2 input=new((k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0),
            (k.wKey.isPressed||k.upArrowKey.isPressed?1:0)-(k.sKey.isPressed||k.downArrowKey.isPressed?1:0));
        hero.Motion=input.normalized*(cfg.speed*(1+.1f*speedLevel));
        if(input.sqrMagnitude>0 && Time.time>=stepAt){Sound("Step",.13f);stepAt=Time.time+.38f;}
        timeLeft-=Time.deltaTime;
        if(phase==Phase.Day)
        {
            foreach(var r in residents)
            {
                if(r.start==r.end)continue;
                Vector2 dest=r.going?r.end:r.start;
                Vector2 delta=dest-(Vector2)r.actor.transform.position;
                if(delta.magnitude<.1f)r.going=!r.going;
                r.actor.Motion=delta.normalized*1.15f;
            }
            if(timeLeft<=0){BeginNight();return;}
            if(k.eKey.wasPressedThisFrame)Interact();
        }
        else
        {
            if (k.spaceKey.wasPressedThisFrame) CallWave();
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                Attack((Vector2)eye.ScreenToWorldPoint(Mouse.current.position.ReadValue())-Here);
            TickNight();
        }
    }
    void ClosePanels(){dialogue=null;workshop=Workshop.None;paused=journal=credits=settings=false;}
    void Freeze(){Time.timeScale=(Frozen||phase==Phase.End)?0:1;if(Frozen)hero.Halt();}
    void PositionMenuCamera()
    {
        eye.orthographicSize = Mathf.Max(49, 62 / eye.aspect);
        eye.transform.position = new Vector3(-eye.orthographicSize*eye.aspect*.25f, 0, -10);
    }
    void LateUpdate()
    {
        if(!hero)return;
        if (phase==Phase.Menu) { PositionMenuCamera(); return; }
        Vector2 p=Here;var dest=new Vector3(Mathf.Clamp(p.x,-49,67),Mathf.Clamp(p.y,-59,59),-10);
        eye.transform.position=Vector3.Lerp(eye.transform.position,dest,1-Mathf.Exp(-9*Time.unscaledDeltaTime));
    }
    void OnDestroy(){Time.timeScale=1;}
    void Interact()
    {
        var npc=Near();
        if(npc==null)
        {
            if(Vector2.Distance(Here,gate)<4){BeginNight();return;}
            return;
        }
        Sound("Talk",.5f);
        if(npc.role=="Воевода"&&taskStage==0)
        {
            taskStage=1;
            dialogue=new[]{"У восточных ворот сменили караул. Забери у караульного донесение и принеси сюда. Нужно знать, сколько людей останется для вылазки.",
                "Кузнец закончил скобы. Забери их и отнеси караульному у восточных ворот. До темноты успеем укрепить створки.",
                "Всё готово. Отнеси мой приказ караульному. После этой ночи выступаем к северной батарее."}[day-1];
            if(day==3)taskStage=2;
        }
        else if(taskStage==1 && npc.role==(day==1?"Караульный":"Кузнец"))
        {
            taskStage=2;dialogue=day==1?"Вот донесение. Видели движение за рекой. Воевода ждёт тебя во дворе.":"Забирай скобы. Караульный у восточных ворот покажет, куда их ставить.";
        }
        else if(taskStage==2 && npc.role==(day==1?"Воевода":"Караульный"))
        {
            taskStage=3;prepared++;gold+=30;Sound("Coin");
            dialogue="Успел вовремя. Гарнизон готов к вылазке на шаг больше. Получено 30 золотых.";
        }
        else if(npc.role=="Кузнец") workshop=Workshop.Smith;
        else if(npc.role=="Бронник") workshop=Workshop.Armorer;
        else dialogue=npc.role switch {"Караульный"=>"Пока держатся восточные ворота, люди успеют собраться. Ночью отвлекай штурмующих на себя.",
            "Воевода"=>"Люди собираются на плацу. Можем провести учения перед выходом к батарее.",
            _=>"У ворот нынче неспокойно. До темноты нужно вернуться во двор."};
        Freeze();
    }
    string TaskText()=>taskStage switch {0=>"Поговорить с воеводой",1=>day==1?"Забрать донесение у караульного":"Забрать скобы у кузнеца",
        2=>day==1?"Доставить донесение воеводе":"Доставить груз караульному",_=>"Поручение выполнено. Подготовиться к ночи"};
    Vector2 Goal()=>phase==Phase.Night?gate:taskStage switch {0=>commanderSpot,1=>day==1?watchSpot:smithSpot,2=>day==1?commanderSpot:watchSpot,_=>gate};
    void BeginDay()
    {
        ClosePanels();Time.timeScale=1;phase=Phase.Day;timeLeft=cfg.phaseSeconds;taskStage=0;hp=maxHP;
        eye.orthographicSize=9;
        MoveHero(dayStart);
        eye.transform.position=new Vector3(dayStart.x,dayStart.y,-10);
        Music("Day");
        foreach(var r in residents)r.actor.gameObject.SetActive(true);
        Hint("Воевода ждёт у крыльца своих хором.");
    }
    void MoveHero(Vector2 spot)
    {
        hero.Halt();
        hero.Body.position=spot;
        hero.transform.position=spot;
    }
    void BeginNight(bool retry=false)
    {
        ClosePanels();Time.timeScale=1;phase=Phase.Night;timeLeft=cfg.phaseSeconds;
        if(!retry){wallCheckpoint=fort.Select(w=>w.health).ToArray();goldCheckpoint=gold;}
        else {for(int i=0;i<fort.Length;i++)fort[i].SetHealth(wallCheckpoint[i]);gold=goldCheckpoint;}
        foreach(var e in raiders)e.actor.gameObject.SetActive(false);
        remaining=cfg.waves[day-1];waveNumber=0;kills=0;spawnIn=1;hp=maxHP;invulnerable=Time.time+1;
        MoveHero(gate+Vector2.right*4);Music("Night");Sound("March",.5f);
        foreach(var r in residents){r.actor.Halt();r.actor.gameObject.SetActive(false);}
        Hint("Отведи штурмующих от восточных ворот.");
    }
    void Attack(Vector2 aim)
    {
        if(Time.time<attackAt)return;
        attackAt=Time.time+.5f;hero.Look(aim);hero.Strike(.3f);Sound("Swing",.7f);
        foreach(var e in raiders)
        {
            if(!e.actor.gameObject.activeSelf)continue;
            Vector2 d=(Vector2)e.actor.transform.position-Here;
            if(d.magnitude>2.35f || (d.magnitude>.55f && Vector2.Dot(hero.Facing,d.normalized)<.45f))continue;
            e.hp-=damage;Sound("Strike",.45f);
            if(e.hp<=0){e.actor.Halt();e.actor.gameObject.SetActive(false);kills++;gold+=Random.Range(5,11);}
        }
    }
    void Spawn()
    {
        var e=raiders.FirstOrDefault(r=>!r.actor.gameObject.activeSelf);
        bool east=(remaining%3!=0);
        Vector2 pos=east?new Vector2(52+Random.value*4,Random.Range(-8,6)):new Vector2(Random.Range(11,28),-54);
        if(e==null){e=new Raider{actor=Actor(raidersRoot,"Штурмовой пехотинец","Enemy",pos,true)};raiders.Add(e);}
        e.actor.gameObject.SetActive(true);e.actor.transform.position=pos;e.actor.Body.position=pos;
        e.actor.GetComponent<SpriteRenderer>().color=new Color(.98f,.76f,.70f);
        e.actor.Halt();e.chasing=false;e.target=null;e.hp=cfg.infantryHP;e.hitAt=Time.time;remaining--;
    }
    FortPart Target(Vector2 p)=>fort.Where(w=>w.assaultTarget&&w.health>0)
        .OrderBy(w=>((Vector2)w.transform.position-p).sqrMagnitude).FirstOrDefault();
    void CallWave()
    {
        if (remaining <= 0) return;
        int count = Mathf.Min(remaining, day + 1);
        for (int i=0; i<count; i++) Spawn();
        waveNumber++;
        spawnIn=cfg.waveInterval;
    }
    void TickNight()
    {
        spawnIn-=Time.deltaTime;
        if(spawnIn<=0) CallWave();
        bool active=false;
        foreach(var e in raiders)
        {
            if(!e.actor.gameObject.activeSelf)continue;
            active=true;Vector2 pos=e.actor.transform.position;
            float dist=Vector2.Distance(pos,Here);
            e.chasing=dist<(e.chasing?cfg.release:cfg.aggro);
            if(!e.target||e.target.health<=0)e.target=Target(pos);
            if(!e.target){End("Острог пал","Оборона прорвана. Гарнизону пришлось оставить острог.");return;}
            Vector2 aim=e.chasing?Here:(Vector2)e.target.transform.position;
            if (!e.chasing && e.target!=eastGate)
            {
                var barrier=e.target.GetComponent<Collider2D>();
                if (barrier) aim=barrier.ClosestPoint(pos);
            }
            float reach=e.chasing?1.15f:1.4f;
            if(Vector2.Distance(pos,aim)>reach)e.actor.Motion=(aim-pos).normalized*cfg.infantrySpeed;
            else
            {
                e.actor.Motion=Vector2.zero;
                if(Time.time<e.hitAt)continue;
                e.actor.Look(aim-pos);e.actor.Strike(.33f);e.hitAt=Time.time+(e.chasing?1.2f:1.5f);
                if(e.chasing&&Time.time>=invulnerable)
                {
                    hp-=Mathf.Max(1,10-2*armorLevel);invulnerable=Time.time+.6f;Sound("Strike",.5f);
                    if(hp<=0){End("Защитник ранен","Ночную оборону можно повторить с состоянием стен на закате.");return;}
                }
                else if(!e.chasing){e.target.SetHealth(e.target.health-(5-armyLevel));if(Vector2.Distance(pos,Here)<14)Sound("Wall",.3f);}
            }
        }
        if(eastGate.health<=0){End("Острог пал","Восточные ворота разрушены. Штурмующие вошли в острог.");return;}
        if(timeLeft>0 && (remaining>0||active))return;
        foreach(var e in raiders)e.actor.gameObject.SetActive(false);
        if(day<3){day++;BeginDay();return;}
        float intact=FortRatio();
        int survivors=Mathf.RoundToInt(intact*(20+15*prepared+8*armyLevel));
        bool win=survivors>=25;
        End(win?"Вылазка удалась":"Тяжёлая цена",(win?"Защитники вывели из строя северную батарею. У острога появился шанс снять осаду.":"На северной батарее умолкли несколько орудий. Острог выиграл время, но осада продолжается.")+"\n\nВышли на вылазку: "+survivors+". Поручений выполнено: "+prepared+" из 3.");
    }
    float FortRatio()=>fort.Sum(w=>w.health)/Mathf.Max(1,fort.Sum(w=>w.capacity));
    void End(string title,string text){headline=title;ending=text;phase=Phase.End;hero.Halt();foreach(var e in raiders)e.actor.Halt();Time.timeScale=0;}
    void Restart(){Time.timeScale=1;SceneManager.LoadScene(SceneManager.GetActiveScene().name);}
}
