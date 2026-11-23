using System.Collections.Generic;
using UnityEngine;

public sealed class AtlasWalker : MonoBehaviour
{
    static readonly Dictionary<string,Sprite[][]> cache=new();
    Sprite[][] walk, attack;
    SpriteRenderer art;
    public Rigidbody2D Body { get; private set; }
    public Vector2 Facing {get;private set;} = Vector2.down;
    public Vector2 Motion;
    float attackUntil, attackDuration, stride;
    int direction=2;
    public void Setup(string key,bool solid)
    {
        art=gameObject.AddComponent<SpriteRenderer>();
        walk=Frames(key+"Walk");
        if(key=="Enemy"||key=="Hero")attack=Frames(key+"Attack");
        if(solid)
        {
            Body=gameObject.AddComponent<Rigidbody2D>();Body.gravityScale=0;Body.freezeRotation=true;
            Body.interpolation=RigidbodyInterpolation2D.Interpolate;
            var c=gameObject.AddComponent<CircleCollider2D>();c.radius=.31f;
        }
        Draw();
    }
    static Sprite[][] Frames(string key)
    {
        if(cache.TryGetValue(key,out var frames))return frames;
        var t=Resources.Load<Texture2D>("Albazin/Actors/"+key);
        int side=t.height/4,count=t.width/side;
        frames=new Sprite[4][];
        for(int d=0;d<4;d++)
        {
            frames[d]=new Sprite[count];
            for(int f=0;f<count;f++)
                frames[d][f]=Sprite.Create(t,new Rect(side*f,(3-d)*side,side,side),new Vector2(.5f,((side-64)*.5f+5)/side),32,0,SpriteMeshType.FullRect);
        }
        return cache[key]=frames;
    }
    public void Look(Vector2 v)
    {
        if(v.sqrMagnitude<.001f)return;
        Facing=v.normalized;
        direction=Mathf.Abs(v.x)>Mathf.Abs(v.y)?(v.x>0?3:1):(v.y>0?0:2);
    }
    public void Strike(float seconds)
    {
        attackDuration=seconds;attackUntil=Time.time+seconds;
    }
    public void Halt(){Motion=Vector2.zero;if(Body)Body.linearVelocity=Vector2.zero;}
    void FixedUpdate()
    {
        if(Body)Body.linearVelocity=Motion;
        else transform.position+=(Vector3)(Motion*Time.fixedDeltaTime);
    }
    void LateUpdate()=>Draw();
    void Draw()
    {
        if(!art)return;
        var active=attack!=null && Time.time<attackUntil;
        if(!active)Look(Motion);
        var strip=(active?attack:walk)[direction];
        if(Motion.sqrMagnitude>.01f)stride+=Time.deltaTime*9;
        int index=active?Mathf.Clamp((int)((1-(attackUntil-Time.time)/attackDuration)*strip.Length),0,strip.Length-1):
            Motion.sqrMagnitude>.01f?1+(int)stride%(strip.Length-1):0;
        art.sprite=strip[index];art.sortingOrder=Mathf.RoundToInt(-transform.position.y*16)+1;
    }
}
