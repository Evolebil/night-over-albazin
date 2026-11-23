using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class AlbazinGame
{
    readonly Dictionary<string, Texture2D> ui = new();
    static readonly Color Ink = new(.18f,.125f,.085f), Accent = new(.38f,.19f,.095f);
    GUIStyle panelStyle, insetStyle, parchment, normal, small, caption, title, centered, standardButton, primaryButton, creditStyle;
    string creditText, sourceText;
    bool creditSources;
    Vector2 sourceScroll;
    int consumedClick = -1;

    void LoadInterfaceArt()
    {
        foreach (var texture in Resources.LoadAll<Texture2D>("Albazin/UI")) ui[texture.name]=texture;
        ui["panel_brown"]=Resources.Load<Texture2D>("Albazin/panel_brown");
        ui["panel_beige"]=Resources.Load<Texture2D>("Albazin/panel_beige");
        var layers = Resources.LoadAll<TextAsset>("Albazin/Credits");
        var authors = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in layers.Where(t=>t.name!="About"))
        {
            bool listing=false;
            foreach (string line in file.text.Split('\n'))
            {
                if (!line.StartsWith("\t\t- ")) listing=line.Trim()=="- Authors:";
                else if (listing) authors.Add(line.Trim().Substring(2).Trim());
            }
        }
        creditText = "Ночь над Албазином\n\nВлад Бобров\nПроект и макет острога\n\n" +
            "Деревянная архитектура\nNicrom\n\nЛандшафт\nLeonard Pabin · Crush · Zyndikate\n\n" +
            "Жители острога\nSorthen и художники LPC\n\nИнтерфейс\nKenney\n\n" +
            "Музыка\nKevin MacLeod\nAngevin · Five Armies\n\nЗвуки\nartisticdude\n\n" +
            "Герой и воевода\nХудожники Liberated Pixel Cup\n\n"+string.Join("\n",authors)+
            "\n\nДальневосточный джем\n\nСпасибо за игру";
        sourceText=string.Join("\n\n",layers.OrderBy(t=>t.name=="About"?0:1).Select(t=>t.text));
    }
    GUIStyle Label(int size, Color color, bool bold=false, TextAnchor align=TextAnchor.MiddleLeft)
    {
        var style=new GUIStyle(GUI.skin.label) { fontSize=size, wordWrap=true, richText=false,
            alignment=align, fontStyle=bold?FontStyle.Bold:FontStyle.Normal, padding=new RectOffset() };
        style.normal.textColor=color;
        return style;
    }
    GUIStyle Surface(string key,int border)
    {
        var style=new GUIStyle { border=new RectOffset(border,border,border,border) };
        style.normal.background=ui[key];
        return style;
    }
    GUIStyle ButtonStyle(string key, Color color)
    {
        var style=Label(18,color,true,TextAnchor.MiddleCenter);
        style.border=new RectOffset(12,12,12,12);
        style.normal.background=ui[key]; style.hover.background=ui[key];
        style.hover.textColor=color;style.active.background=ui[key+"_pressed"];style.active.textColor=color;
        style.focused.background=ui[key];style.focused.textColor=color;
        return style;
    }
    void Styles()
    {
        if (normal!=null)return;
        panelStyle=Surface("panel_brown",12); parchment=Surface("panel_beige",12); insetStyle=Surface("panelInset_beige",9);
        normal=Label(18,Ink); small=Label(15,Ink); caption=Label(16,Accent,true);
        title=Label(36,Ink,true); centered=Label(15,Ink,true,TextAnchor.MiddleCenter);
        creditStyle=Label(22,Ink,false,TextAnchor.UpperCenter);
        standardButton=ButtonStyle("buttonLong_beige",Ink); primaryButton=ButtonStyle("buttonLong_blue",Color.white);
    }
    static void Tint(Rect rect,Color color)
    {
        Color old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;
    }
    static void Paper(Rect rect,GUIStyle style) => GUI.Box(rect,GUIContent.none,style);
    void Text(float x,float y,float w,float h,string text,GUIStyle style=null) =>
        GUI.Label(new Rect(x,y,w,h),text,style??normal);
    Vector2 Pointer()
    {
        if(Mouse.current==null)return new Vector2(-1,-1);
        var pos=Mouse.current.position.ReadValue();
        return new Vector2(pos.x,Screen.height-pos.y)/InterfaceScale;
    }
    float InterfaceScale => Mathf.Max(.1f,Mathf.Min(Screen.width/960f,Screen.height/650f));

    bool Button(float x,float y,float w,string text,bool primary=false,float h=41)
    {
        var rect=new Rect(x,y,w,h);
        bool hit=GUI.Button(rect,text,primary?primaryButton:standardButton);
        if(Event.current.type==EventType.Repaint && Mouse.current!=null &&
           Mouse.current.leftButton.wasReleasedThisFrame && rect.Contains(Pointer())) hit=true;
        if(!GUI.enabled || !hit || consumedClick==Time.frameCount)return false;
        consumedClick=Time.frameCount;return true;
    }
    void Strip(Rect r,string color)
    {
        if(r.width<=0)return;
        float edge=Mathf.Min(r.width/2,r.height*.5f);
        var key="bar"+color+"_horizontal";
        GUI.DrawTexture(new Rect(r.x,r.y,edge,r.height),ui[key+"Left"]);
        if(r.width>2*edge)GUI.DrawTexture(new Rect(r.x+edge,r.y,r.width-edge*2,r.height),ui[key+"Mid"]);
        GUI.DrawTexture(new Rect(r.xMax-edge,r.y,edge,r.height),ui[key+"Right"]);
    }
    void Health(Rect r,float fraction,string color)
    {
        Strip(r,"Back");r.width*=Mathf.Clamp01(fraction);Strip(r,color);
    }
    void OpenCredits()
    {
        credits=true;creditSources=false;creditOffset=0;sourceScroll=Vector2.zero;Freeze();
    }
    void OnGUI()
    {
        if (!hero || ui.Count==0)return;
        Styles();
        float scale=InterfaceScale,w=Screen.width/scale,h=Screen.height/scale;
        GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
        if(phase==Phase.Night||phase==Phase.End)Tint(new Rect(0,0,w,h),new Color(.015f,.045f,.15f,.54f));
        if(phase==Phase.Menu)Tint(new Rect(0,0,w,h),new Color(.045f,.09f,.19f,.28f));
        if(phase==Phase.Day||phase==Phase.Night)
        {
            if (!Frozen) DrawWorldHealth(scale,h);
            DrawHud(w,h);
        }
        if(phase!=Phase.Menu && (Frozen||phase==Phase.End))Tint(new Rect(0,0,w,h),new Color(.045f,.04f,.035f,.48f));
        if(credits){DrawCredits(w,h);return;}
        if(settings){DrawSettings(w,h);return;}
        if(phase==Phase.Menu){DrawMenu(w,h);return;}
        if(!(Frozen||phase==Phase.End))return;
        float x=(w-540)/2,y=(h-395)/2;
        Paper(new Rect(x,y,540,395),parchment);x+=28;y+=20;
        if(phase==Phase.End)
        {
            Text(x,y,480,50,headline,title);Text(x,y+70,480,149,ending);
            if(headline=="Защитник ранен" && Button(x,y+231,235,"Повторить ночь",true))BeginNight(true);
            if(Button(x,y+304,220,"Главное меню"))Restart();
            if(Button(x+236,y+304,220,"Титры"))OpenCredits();
        }
        else if(workshop!=Workshop.None) DrawWorkshop(x,y);
        else if(journal)
        {
            Text(x,y,475,52,"Журнал дозора",title);
            Text(x,y+62,475,42,"День "+day+"  ·  "+errands[day-1],caption);
            Text(x,y+112,480,96,TaskText()+(taskStage==2?"\nПри себе — "+cargo[day-1]:""));
            Text(x,y+221,475,52,"Выполнено поручений  "+prepared+" / 3");
            if(Button(x,y+304,210,"Закрыть")){ClosePanels();Freeze();}
        }
        else if(dialogue!=null)
        {
            string role=Near()?.role??"Разговор";
            Text(x,y,475,56,role,title);Text(x,y+70,475,130,dialogue);
            if(role=="Воевода"&&Button(x,y+225,320,"Подготовка гарнизона",true)){dialogue=null;workshop=Workshop.Garrison;}
            if(Button(x,y+304,210,"Закончить разговор")){ClosePanels();Freeze();}
        }
        else
        {
            Text(x,y,475,55,"Пауза",title);
            if(Button(x,y+72,285,"Продолжить",true)){ClosePanels();Freeze();}
            if(Button(x,y+128,285,"Настройки"))settings=true;
            if(Button(x,y+184,285,"Титры"))OpenCredits();
            if(Button(x,y+240,285,"Главное меню"))Restart();
        }
    }
    void DrawMenu(float w,float h)
    {
        float x=Mathf.Max(30,w*.048f), y=(h-355)/2;
        Paper(new Rect(x,y,306,355),panelStyle);
        Paper(new Rect(x+11,y+12,284,99),insetStyle);
        Text(x+25,y+15,260,90,"Ночь над\nАлбазином",title);
        if(Button(x+22,y+123,262,"Играть",true))BeginDay();
        if(Button(x+22,y+179,262,"Настройки"))settings=true;
        if(Button(x+22,y+235,262,"Титры"))OpenCredits();
        if(Button(x+22,y+291,262,"Выйти из игры"))Application.Quit();
    }
    void DrawSettings(float w,float h)
    {
        float x=(w-540)/2,y=(h-430)/2;
        Paper(new Rect(x,y,540,430),parchment);x+=29;y+=21;
        Text(x,y,470,49,"Настройки",title);
        Text(x,y+55,240,34,"Общая громкость",caption);
        Text(x+266,y+55,80,34,Mathf.RoundToInt(AudioListener.volume*100)+"%",centered);
        if(Button(x+208,y+55,50,"−",false,34))Volume(-.1f);
        if(Button(x+354,y+55,50,"+",false,34))Volume(.1f);
        Text(x,y+111,470,36,"Управление",caption);
        string[] actions={"Движение","Разговор и взаимодействие","Удар в сторону курсора","Досрочно вызвать волну","Журнал","Пауза или назад"};
        string[] keys={"WASD / стрелки","E","Левая кнопка мыши","Пробел","J","Esc"};
        for(int i=0;i<actions.Length;i++)
        {
            Text(x,y+153+28*i,263,27,actions[i],small);
            Text(x+268,y+153+28*i,209,27,keys[i],caption);
        }
        if(Button(x,y+346,190,"Назад")){settings=false;Freeze();}
    }
    void Volume(float delta)
    {
        AudioListener.volume=Mathf.Clamp01(AudioListener.volume+delta);
        PlayerPrefs.SetFloat("AlbazinVolume",AudioListener.volume);PlayerPrefs.Save();
    }
    void DrawCredits(float w,float h)
    {
        float width=Mathf.Min(700,w-48),height=h-62,x=(w-width)/2,y=31;
        Paper(new Rect(x,y,width,height),parchment);
        Text(x+28,y+15,width-56,50,creditSources?"Авторы и лицензии":"Титры",title);
        var viewport=new Rect(x+25,y+79,width-50,height-160);
        if(creditSources)
        {
            float tw=viewport.width-28;
            float th=small.CalcHeight(new GUIContent(sourceText),tw)+20;
            sourceScroll=GUI.BeginScrollView(viewport,sourceScroll,new Rect(0,0,tw,th));
            GUI.Label(new Rect(0,0,tw,th),sourceText,small);GUI.EndScrollView();
        }
        else
        {
            float textHeight=creditStyle.CalcHeight(new GUIContent(creditText),viewport.width-20);
            float textY=viewport.height-48-creditOffset;
            GUI.BeginGroup(viewport);
            GUI.Label(new Rect(10,textY,viewport.width-20,textHeight),creditText,creditStyle);
            GUI.EndGroup();
            if(textY+textHeight < -20){credits=false;Freeze();}
        }
        if(Button(x+25,y+height-64,172,"Назад")){credits=false;Freeze();}
        if(Button(x+width-250,y+height-64,225,creditSources?"К титрам":"Авторы и лицензии"))creditSources=!creditSources;
    }
    void DrawHud(float w,float h)
    {
        Paper(new Rect(15,14,238,99),parchment);
        Text(28,22,210,22,(phase==Phase.Day?"День ":"Ночь ")+day+" / 3     "+TimeSpan.FromSeconds(Mathf.Max(0,timeLeft)).ToString(@"mm\:ss"),caption);
        Text(28,47,205,20,"Здоровье  "+Mathf.Max(0,hp)+" / "+maxHP,small);
        Health(new Rect(28,70,206,9),hp/(float)maxHP,"Red");
        Text(28,83,205,21,"Золото  "+gold,small);
        if(Frozen)return;
        float rx=w-333;
        Paper(new Rect(rx,14,318,79),parchment);
        Vector2 delta=Goal()-Here;
        string dir=Mathf.Abs(delta.x)>Mathf.Abs(delta.y)?(delta.x>0?"восток":"запад"):(delta.y>0?"север":"юг");
        string goal=phase==Phase.Day?TaskText():"Волна  "+waveNumber+" / "+WaveTotal+"     Отбито  "+kills;
        Text(rx+13,20,290,42,goal,caption);
        Text(rx+13,60,290,22,phase==Phase.Day?Mathf.RoundToInt(delta.magnitude)+" м · "+dir:
            (remaining>0?"До следующей волны  "+Mathf.CeilToInt(Mathf.Max(0,spawnIn))+" с":"Последняя волна в бою"),small);
        if(phase==Phase.Night)
        {
            Paper(new Rect(15,h-72,222,57),parchment);
            Text(28,h-68,196,28,"Острог   "+Mathf.RoundToInt(FortRatio()*100)+"%",caption);
            Health(new Rect(28,h-37,195,10),FortRatio(),"Yellow");
        }
        else
        {
            var npc=Near();
            if(npc!=null || Vector2.Distance(Here,gate)<4)
            {
                if(Button((w-248)/2,h-62,248,npc?.role??"Выйти в ночной дозор",true))Interact();
            }
        }
    }
    void DrawWorldHealth(float scale,float h)
    {
        if(phase!=Phase.Night)return;
        foreach(var e in raiders)
        {
            if(!e.actor.gameObject.activeSelf)continue;
            var p=eye.WorldToViewportPoint(e.actor.transform.position+Vector3.up*1.92f);
            if(p.z<=0||p.x<0||p.x>1||p.y<0||p.y>1)continue;
            float sx=p.x*Screen.width/scale,sy=h*(1-p.y);
            Health(new Rect(sx-23,sy,46,6),e.hp/(float)cfg.infantryHP,"Red");
        }
        var gateTop=eastGate.GetComponent<SpriteRenderer>().bounds;
        var point=eye.WorldToViewportPoint(new Vector3(gateTop.center.x,gateTop.max.y+.1f,0));
        if(point.z>0 && point.x>.05f && point.x<.95f && point.y>.05f && point.y<1)
        {
            float sx=point.x*Screen.width/scale,sy=h*(1-point.y);
            Paper(new Rect(sx-82,sy-32,164,35),parchment);
            Text(sx-80,sy-31,160,19,"Ворота  "+Mathf.CeilToInt(eastGate.health)+" / "+eastGate.capacity,centered);
            Health(new Rect(sx-69,sy-9,138,7),eastGate.health/eastGate.capacity,"Yellow");
        }
    }
    void DrawWorkshop(float x,float y)
    {
        string name=workshop switch { Workshop.Smith=>"Кузнец", Workshop.Armorer=>"Бронник", _=>"Гарнизон" };
        Text(x,y,320,53,name,title);Text(x+335,y+9,145,36,"Золото  "+gold,caption);
        switch(workshop)
        {
            case Workshop.Smith:
                Offer(x,y+68,"Заточить копьё","Урон за удар +5",ref damageLevel,45,()=>damage+=5);
                Offer(x,y+140,"Подогнать сапоги","Скорость ходьбы +10%",ref speedLevel,30,()=>{});
                GUI.enabled=gold>0&&fort.Any(w=>w.health<w.capacity);
                if(Button(x,y+236,462,"Ремонт стен · 4 здоровья за 1 зол.")) Repair();
                GUI.enabled=true;
                break;
            case Workshop.Armorer:
                Offer(x,y+68,"Укрепить доспех","Урон от каждого удара пехоты −2",ref armorLevel,35,()=>{});
                Offer(x,y+140,"Подогнать поддоспешник","Запас здоровья +25",ref hpLevel,40,()=>{maxHP+=25;hp+=25;});
                break;
            case Workshop.Garrison:
                Offer(x,y+68,"Обучить гарнизон","Урон по укреплениям −1 за удар",ref armyLevel,45,()=>{});
                Text(x,y+171,462,78,"Каждый уровень также добавляет восемь подготовленных бойцов к финальной вылазке.",small);
                break;
        }
        if(Button(x,y+304,186,"Готово")){ClosePanels();Freeze();}
    }
    void Offer(float x,float y,string name,string effect,ref int level,int cost,Action apply)
    {
        int price=cost*(level+1);
        Text(x,y,310,30,name+"   "+level+" / 3",caption);
        Text(x,y+29,308,28,effect,small);
        GUI.enabled=level<3&&gold>=price;
        if(Button(x+316,y+5,156,level<3?price+" зол.":"Максимум",true))
        {gold-=price;level++;apply();Sound("Coin");}
        GUI.enabled=true;
    }
    void Repair()
    {
        foreach(var part in fort.OrderBy(w=>w==eastGate?0:1))
        {
            int spend=Mathf.Min(gold,Mathf.CeilToInt((part.capacity-part.health)/4));
            part.SetHealth(part.health+spend*4);gold-=spend;
        }
        Sound("Wall");
    }
}
