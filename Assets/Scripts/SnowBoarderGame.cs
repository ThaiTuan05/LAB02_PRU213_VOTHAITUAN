using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
namespace Lab2 {
public sealed partial class SnowBoarderGame : MonoBehaviour {
    enum Page { Menu, Options, Playing, Pause, Result, Help }
    Page page;
    const float MaxSpeed = 16;
    // Three mountains of rising difficulty. Track 1 keeps the original numbers so
    // existing personal bests and the automated checks stay valid.
    static readonly float[] RouteFinish = {285f,470f,700f};
    static readonly float[] RouteTime = {40f,60f,90f};
    static readonly string[] RouteName = {"RIDGE RUN","GLACIER RIDGE","SUMMIT DESCENT"};
    // One looping theme per mountain, swapped as soon as the route changes.
    static readonly string[] RouteMusic = {"RIDGE THEME","GLACIER THEME","SUMMIT THEME"};
    static readonly string[] RouteTag = {"285 m    EASY","470 m    MEDIUM","700 m    HARD"};
    static readonly string[] RouteBlurb = {
        "Gentle slopes. Learn to carve, jump and land tricks.",
        "Steeper drops, a tighter slalom line and bigger air.",
        "The full descent. Long, fast and unforgiving."};
    static readonly System.Func<float,float>[] Terrains = { Height, IceHeight, SummitHeight };
    int track = 1;
    float finish = 285f, timeLimit = 40f;
    System.Func<float,float> terrain;
    float[] routeRamps;
    int flakesTotal;
    bool timeAttack;
    int tricksLanded;
    readonly bool[] missions = new bool[3];
    float steering, rotationSensitivity = .9f, obstacleNoticeUntil;
    string BestKey { get { string suffix = timeAttack ? ".TimeAttack" : ""; return track==1 ? "Snow.Best"+suffix : "Snow.Best.L"+track+suffix; } }
    readonly List<float> hazards = new List<float>();
    Rigidbody2D body;
    Camera cam;
    AudioSource audioSource, musicSource;
    Transform visual, shield;
    Sprite pixel;
    PhysicsMaterial2D snow;
    readonly List<GameObject> objects = new List<GameObject>();
    readonly List<UnityEngine.Object> generated = new List<UnityEngine.Object>();
    readonly List<Transform> weather = new List<Transform>();
    readonly List<SnowPickup> pickups = new List<SnowPickup>();
    float score, timer, respawnUntil, boostUntil, comboUntil, spin, previousAngle, airTime, noticeUntil, shieldGraceUntil, shake, comboFlash;
    float lastGrounded = -9f;
    int lives = 3, combo, collected, best, shields, airFlips;
    Vector2 checkpoint;
    bool wasGrounded, won, newRecord, snowfall = true, fromPause, musicOn = true;
    float volume = .65f, snowTime;
    string notice = "", reason = "";
    GUIStyle title, heading, text, small, button;
    Texture2D panel, mint;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() {
        string name = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if ((name == "SampleScene" || name == "SnowBoarder") && FindAnyObjectByType<SnowBoarderGame>() == null)
            new GameObject("Snow Boarder - Game").AddComponent<SnowBoarderGame>();
    }
    void Awake() {
        Time.timeScale = 1;
        foreach (Camera c in FindObjectsByType<Camera>()) c.gameObject.SetActive(false);
        foreach (AudioListener a in FindObjectsByType<AudioListener>()) a.enabled = false;
        cam = new GameObject("Follow camera").AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 8;
        cam.backgroundColor = new Color(.55f,.76f,.87f);
        cam.transform.position = new Vector3(7,4,-10);
        cam.gameObject.AddComponent<AudioListener>();
        audioSource = gameObject.AddComponent<AudioSource>();
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true; musicSource.playOnAwake = false; musicSource.spatialBlend = 0; musicSource.priority = 0;
        SnowSfx.Preload();
        volume = PlayerPrefs.GetFloat("Snow.Volume", .65f);
        snowfall = PlayerPrefs.GetInt("Snow.Weather",1) == 1;
        musicOn = PlayerPrefs.GetInt("Snow.Music",1) == 1;
        best = PlayerPrefs.GetInt(BestKey,0);
        rotationSensitivity = PlayerPrefs.GetFloat("Snow.Steering",.9f);
        var texture = Solid(Color.white);
        pixel = Sprite.Create(texture,new Rect(0,0,1,1),Vector2.one*.5f,1);
        snow = new PhysicsMaterial2D("Packed snow") { friction=.025f, bounciness=0 };
        Course(); body.simulated = false; page = Page.Menu;
    }
    // Background music: a dedicated looping source kept on the game object so course rebuilds
    // never interrupt it. Volume follows the audio slider; the music toggle mutes it outright.
    void ApplyMusic() {
        if(!musicSource) return;
        var clip = SnowMusic.Theme(track);
        if(clip && clip!=musicSource.clip) musicSource.clip = clip;
        RefreshMusic();
    }
    void RefreshMusic() {
        if(!musicSource) return;
        musicSource.volume = musicOn ? volume*.5f : 0f;
        if(musicOn && musicSource.clip && !musicSource.isPlaying) musicSource.Play();
        else if(!musicOn && musicSource.isPlaying) musicSource.Pause();
    }
    public static float Height(float x) { return 1-x*.19f+Mathf.Sin(x*.075f)*2.4f+Mathf.Sin(x*.23f)*.75f; }
    static float IceHeight(float x) { return 3-x*.22f+Mathf.Sin(x*.06f)*3+Mathf.Sin(x*.21f)*.9f+Mathf.Sin(x*.62f)*.28f; }
    static float SummitHeight(float x) { return 5-x*.245f+Mathf.Sin(x*.05f)*3.6f+Mathf.Sin(x*.18f)*1f+Mathf.Sin(x*.6f)*.3f; }
    float Ground(float x) { return terrain != null ? terrain(x) : Height(x); }
    static bool NearAny(float x,float[] points,float range) { foreach(float p in points) if(Mathf.Abs(p-x)<range) return true; return false; }
    GameObject Art(string name, string asset, Vector2 pos, float width, int order, Color color) {
        var go = new GameObject(name); objects.Add(go); go.transform.position = pos;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = string.IsNullOrEmpty(asset) ? pixel : Resources.Load<Sprite>("SnowBoarder/Sprites/"+asset);
        if (!renderer.sprite) renderer.sprite = pixel;
        renderer.sortingOrder = order; renderer.color = color;
        renderer.sharedMaterial = Resources.Load<Material>("SnowBoarder/SpriteMaterial");
        go.transform.localScale = Vector3.one * (width/renderer.sprite.bounds.size.x); return go;
    }
    void Strip(string name, List<Vector2> points, Color color, int order) {
        var go = new GameObject(name); objects.Add(go);
        var vertices = new Vector3[points.Count];
        for (int i=0;i<points.Count;i++) vertices[i]=points[i];
        var triangles = new int[(points.Count/2-1)*6];
        for (int i=0;i<points.Count/2-1;i++) {
            int k=i*6,v=i*2;
            triangles[k]=v; triangles[k+1]=v+2; triangles[k+2]=v+1;
            triangles[k+3]=v+1; triangles[k+4]=v+2; triangles[k+5]=v+3;
        }
        var mesh=new Mesh(); mesh.vertices=vertices; mesh.triangles=triangles; mesh.RecalculateNormals();
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=new Material(Resources.Load<Material>("SnowBoarder/WorldMaterial")) {color=color}; renderer.sortingOrder=order; generated.Add(mesh); generated.Add(renderer.sharedMaterial);
    }
    void Course() {
        terrain = Terrains[Mathf.Clamp(track-1,0,Terrains.Length-1)];
        finish = RouteFinish[track-1];
        timeLimit = RouteTime[track-1];
        flakesTotal = 0;
        float[] ramps, rocks, trees, boosts, shields;
        switch (track) {
            case 2:
                ramps  = new float[]{42,96,150,205,262,318,372,420};
                rocks  = new float[]{70,130,186,240,300,356,402};
                trees  = new float[]{55,112,168,224,278,334,388,442};
                boosts = new float[]{34,200,360};
                shields= new float[]{120,300,430};
                break;
            case 3:
                ramps  = new float[]{45,110,170,232,300,365,430,500,565,630,672};
                rocks  = new float[]{78,140,205,268,335,400,470,540,600,662};
                trees  = new float[]{62,124,188,252,318,382,448,515,580,645};
                boosts = new float[]{60,340,610};
                shields= new float[]{180,520};
                break;
            default:
                ramps  = new float[]{38,88,142,198,247};
                rocks  = new float[]{61,117,174,227,266};
                trees  = new float[]{76,158,213};
                boosts = new float[]{27,184};
                shields= new float[]{103,239};
                break;
        }
        routeRamps = ramps;
        var edge=new List<Vector2>(); var earth=new List<Vector2>(); var surface=new List<Vector2>();
        int end=Mathf.CeilToInt(finish)+40;
        for(int x=-35;x<=end;x++) {
            float y=Ground(x); edge.Add(new Vector2(x,y));
            earth.Add(new Vector2(x,y)); earth.Add(new Vector2(x,y-25));
            surface.Add(new Vector2(x,y)); surface.Add(new Vector2(x,y-.45f));
        }
        cam.backgroundColor=Palette(track).sky;
        Strip("Mountain slope",earth,Palette(track).earth,0);
        Strip("Snow surface",surface,Palette(track).snow,2);
        var ground=new GameObject("Slope physics"); objects.Add(ground);
        var collider=ground.AddComponent<EdgeCollider2D>(); collider.SetPoints(edge); collider.sharedMaterial=snow;
        BuildBackdrop(trees);
        foreach(float x in ramps) {
            var ramp=new GameObject("Jump ramp"); objects.Add(ramp);
            var rampEdge=ramp.AddComponent<EdgeCollider2D>(); rampEdge.sharedMaterial=snow;
            rampEdge.points=new[]{new Vector2(x-5,Ground(x-5)+.04f),new Vector2(x,Ground(x)+2.4f)};
            Strip("Ramp snow",new List<Vector2>{new Vector2(x-5,Ground(x-5)),new Vector2(x-5,Ground(x-5)-.4f),new Vector2(x,Ground(x)+2.4f),new Vector2(x,Ground(x))},Palette(track).snow,3);
            for(int n=0;n<4;n++) Pickup(SnowPickup.Kind.Flake,new Vector2(x+2+n*2.2f,Ground(x)+3.7f+Mathf.Sin(n*.8f)*1.6f));
        }
        if(track==1) { for(int i=0;i<20;i++) { float x=17+i*13; Pickup(SnowPickup.Kind.Flake,new Vector2(x,Ground(x)+1.1f)); } }
        else {
            for(float x=17;x<finish-18;x+=13f) {
                if(NearAny(x,rocks,4f)||NearAny(x,trees,4f)||NearAny(x,ramps,6f)) continue;
                Pickup(SnowPickup.Kind.Flake,new Vector2(x,Ground(x)+1.1f));
            }
        }
        foreach(float x in rocks) {
            var rock=Art("Rock obstacle","Snow-Rock",new Vector2(x,0f),1.4f,5,Color.white);
            rock.transform.position+=Vector3.up*(Ground(x)-rock.GetComponent<SpriteRenderer>().bounds.min.y);
            var c=rock.AddComponent<CircleCollider2D>(); c.radius=.43f/rock.transform.localScale.x;
            var rockItem=rock.AddComponent<SnowPickup>(); rockItem.kind=SnowPickup.Kind.Obstacle; rockItem.isRock=true;hazards.Add(x);
            MarkRockHazard(rock,x);
        }
        foreach(float x in trees) {
            var tree=Art("Tree obstacle","Snow-Tree-2",new Vector2(x,Ground(x)+1.5f),2.2f,6,Color.white);
            float align=Ground(x)-tree.GetComponent<SpriteRenderer>().bounds.min.y;
            tree.transform.position+=Vector3.up*align;
            var c=tree.AddComponent<BoxCollider2D>(); c.size=new Vector2(.5f,2.1f)/tree.transform.localScale.x;
            c.offset=new Vector2(0,-.45f-align)/tree.transform.localScale.x;
            tree.AddComponent<SnowPickup>().kind=SnowPickup.Kind.Obstacle;hazards.Add(x);
            MarkTreeHazard(tree,x);
        }
        foreach(float x in boosts) Pickup(SnowPickup.Kind.Boost,new Vector2(x,Ground(x)+1));
        foreach(float x in shields) Pickup(SnowPickup.Kind.Shield,new Vector2(x,Ground(x)+1.1f));
        for(int i=0;i<2;i++) {
            var pole=Art("Finish pole","",new Vector2(finish+i*4,Ground(finish)+2),.14f,8,new Color(.04f,.1f,.17f));
            pole.transform.localScale=new Vector3(.14f,5,1);
        }
        var banner=Art("Finish banner","",new Vector2(finish+2,Ground(finish)+4.3f),4.4f,8,new Color(.28f,.91f,.84f));
        banner.transform.localScale=new Vector3(4.4f,.7f,1);
        var player=new GameObject("Player - Snowboarder"); objects.Add(player);
        player.transform.position=new Vector2(2,Ground(2)+.9f);
        body=player.AddComponent<Rigidbody2D>(); body.mass=1; body.gravityScale=1.25f; body.linearDamping=.12f;
        body.interpolation=RigidbodyInterpolation2D.Interpolate; body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
        var board=player.AddComponent<CapsuleCollider2D>(); board.direction=CapsuleDirection2D.Horizontal;
        board.size=new Vector2(1.5f,.4f); board.sharedMaterial=snow;
        var sensor=player.AddComponent<CircleCollider2D>(); sensor.isTrigger=true; sensor.radius=.85f; sensor.offset=new Vector2(0,.5f);
        player.AddComponent<SnowRider>().game=this;
        visual=new GameObject("Animated rider").transform; visual.SetParent(player.transform,false);
        var bottom=Art("Snowboard","Boarder_Bottom",Vector2.zero,1.8f,14,Color.white);
        bottom.transform.SetParent(visual,false); bottom.transform.localPosition=new Vector3(0,.12f,0);
        // Both halves share the same pixel scale; align the waist and keep the upper body above the lower sprite.
        // Both halves share the same pixel scale; align the waist using the original Level1 sprite offsets (-42px, +122px).
        var upper = Resources.Load<Sprite>("SnowBoarder/Sprites/Boarder_Top");
        var lower = Resources.Load<Sprite>("SnowBoarder/Sprites/Boarder_Bottom");
        float pixelScale = 1.8f / lower.rect.width;
        var top=Art("Rider","Boarder_Top",Vector2.zero,upper.rect.width * pixelScale,15,Color.white);
        top.transform.SetParent(visual,false); top.transform.localPosition=new Vector3(0,.12f + (lower.rect.height * .5f - (upper.rect.height * .5f - 160)) * pixelScale,0);
        top.transform.SetParent(visual,false); top.transform.localPosition=new Vector3(-42f * pixelScale,.12f + 122f * pixelScale,0);
        shield=new GameObject("Shield bubble").transform; objects.Add(shield.gameObject);
        shield.SetParent(player.transform,false); shield.localPosition=new Vector3(0,.45f,0);
        Ring("Shield rim",shield,1.22f,1.16f,new Color(.3f,1,.91f,.8f),18);
        Ring("Shield fill",shield,1.16f,0,new Color(.3f,1,.91f,.055f),10);
        shield.gameObject.SetActive(false);
        for(int i=0;i<85;i++) weather.Add(Art("Falling snow","",Vector2.zero,.025f+i%4*.014f,25,new Color(1,1,1,.7f)).transform);
        checkpoint=body.position;
        ApplyMusic();
    }
    void Ring(string name,Transform parent,float outer,float inner,Color tint,int order) {
        const int segments=96;
        var go=new GameObject(name);objects.Add(go);go.transform.SetParent(parent,false);
        var vertices=new Vector3[segments*2];var indices=new int[segments*6];
        for(int i=0;i<segments;i++){
            float a=i*Mathf.PI*2/segments;var direction=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
            vertices[i*2]=direction*outer;vertices[i*2+1]=direction*inner;
            int next=(i+1)%segments*2,k=i*6,v=i*2;
            indices[k]=v;indices[k+1]=next;indices[k+2]=v+1;
            indices[k+3]=v+1;indices[k+4]=next;indices[k+5]=next+1;
        }
        var mesh=new Mesh();mesh.vertices=vertices;mesh.triangles=indices;mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=new Material(Resources.Load<Material>("SnowBoarder/WorldMaterial")){color=tint};renderer.sortingOrder=order;
        generated.Add(mesh);generated.Add(renderer.sharedMaterial);
    }
    void Pickup(SnowPickup.Kind kind,Vector2 pos) {
        var go=new GameObject(kind.ToString()); objects.Add(go); go.transform.position=pos;
        var pickup=go.AddComponent<SnowPickup>(); pickup.kind=kind; pickups.Add(pickup);
        if(kind==SnowPickup.Kind.Flake) flakesTotal++;
        var c=go.AddComponent<CircleCollider2D>(); c.isTrigger=true; c.radius=.55f;
        var color=kind==SnowPickup.Kind.Flake?new Color(.86f,1,1):kind==SnowPickup.Kind.Boost?new Color(1,.73f,.23f):new Color(.28f,.91f,.84f);
        for(int i=0;i<(kind==SnowPickup.Kind.Flake?3:2);i++) {
            var part=Art("Pickup icon","",pos,.8f,9,color); part.transform.SetParent(go.transform,true);
            part.transform.localScale=new Vector3(.8f,.10f,1);
            part.transform.localRotation=Quaternion.Euler(0,0,kind==SnowPickup.Kind.Flake?i*60:45+i*90);
        }
    }
    bool TrickHeld() {
#if ENABLE_INPUT_SYSTEM
        var k=Keyboard.current;return k!=null && (k.leftShiftKey.isPressed||k.rightShiftKey.isPressed);
#else
        return Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift);
#endif
    }
    float Horizontal() {
#if ENABLE_INPUT_SYSTEM
        var k=Keyboard.current; if(k==null)return 0;
        return (k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0);
#else
        return Input.GetAxisRaw("Horizontal");
#endif
    }
    bool Press(string key) {
#if ENABLE_INPUT_SYSTEM
        var k=Keyboard.current; if(k==null)return false;
        return key=="jump"?k.spaceKey.wasPressedThisFrame:key=="pause"?k.escapeKey.wasPressedThisFrame:k.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(key=="jump"?KeyCode.Space:key=="pause"?KeyCode.Escape:KeyCode.R);
#endif
    }
    bool Held(bool accelerate) {
#if ENABLE_INPUT_SYSTEM
        var k=Keyboard.current; if(k==null)return false;
        return accelerate?k.wKey.isPressed||k.upArrowKey.isPressed:k.sKey.isPressed||k.downArrowKey.isPressed;
#else
        return Input.GetKey(accelerate?KeyCode.W:KeyCode.S)||Input.GetKey(accelerate?KeyCode.UpArrow:KeyCode.DownArrow);
#endif
    }
    RaycastHit2D GroundHit() {
        foreach(var hit in Physics2D.RaycastAll(body.position,Vector2.down,.55f))
            if(hit.collider && hit.rigidbody!=body && !hit.collider.isTrigger &&
               (!hit.collider.GetComponent<SnowPickup>() || hit.collider.GetComponent<SnowPickup>().isRock) && hit.normal.y > .15f) return hit;
        return default;
    }
    bool Grounded() { return GroundHit().collider != null; }
    float SurfaceAngle(RaycastHit2D hit) {
        if(!hit.collider) return 0f;
        var pickup = hit.collider.GetComponent<SnowPickup>();
        if(pickup && pickup.isRock) {
            float x = hit.point.x;
            return Mathf.Atan2(Ground(x + .5f) - Ground(x - .5f), 1f) * Mathf.Rad2Deg;
        }
        return Mathf.Atan2(-hit.normal.x, hit.normal.y)*Mathf.Rad2Deg;
    }
    void SyncEffects() {
        if(shield) {
            shield.gameObject.SetActive(page==Page.Playing && shields>0);
            shield.localRotation=Quaternion.Inverse(body.transform.rotation);
            shield.localScale=Vector3.one*(1+Mathf.Sin(Time.time*3)*.025f);
        }
    }
    bool SafeCheckpoint(float x) {
        foreach(float h in hazards) if(Mathf.Abs(h-x)<5) return false;
        return true;
    }
    void Update() {
        if(page==Page.Playing) {
            if(Press("pause")){ Pause();return; }
            if(Press("restart")){ StartRun();return; }
            timer+=Time.deltaTime;
            if(timeAttack && timer>=timeLimit){reason="Time limit reached";EndRun(false);return;}
            var groundHit=GroundHit(); bool grounded=groundHit.collider != null;
            if(grounded) lastGrounded=Time.time;
            if(Press("jump") && (grounded || Time.time-lastGrounded<.12f)){body.AddForce(Vector2.up*9,ForceMode2D.Impulse);grounded=false;lastGrounded=-9f;}
            if(!grounded){
                airTime+=Time.deltaTime;
                spin+=Mathf.DeltaAngle(previousAngle,body.rotation);
                // Air combo: each full 360 counts the moment it is completed in the air,
                // before the rider ever lands. Chaining jumps keeps the multiplier alive.
                int flips=Mathf.FloorToInt(Mathf.Abs(spin)/360f);
                if(flips>airFlips){
                    int gained=flips-airFlips;airFlips=flips;
                    tricksLanded+=gained;
                    combo=Mathf.Clamp((Time.time<comboUntil?combo:0)+gained,1,8);
                    comboUntil=Time.time+7;
                    int points=250*gained*combo+(gained>1?175*(gained-1)*combo:0);
                    score+=points;
                    ComboBurst();
                    Say(gained+(gained>1?" FLIPS  +":" FLIP  +")+points+"   /   COMBO x"+combo);
                }
            }
            if(grounded&&!wasGrounded&&airTime>.25f) {
                if(Mathf.Abs(Mathf.DeltaAngle(body.rotation,SurfaceAngle(groundHit)))>65) {
                    if(Crash("Bad landing")) return;
                }
            }
            if(grounded && !wasGrounded && airTime>=.8f) {
                float off=Mathf.Abs(Mathf.DeltaAngle(body.rotation,SurfaceAngle(groundHit)));
                if(off<45){
                    int airPoints=Mathf.RoundToInt(airTime*50);score+=airPoints;
                    if(off<12){score+=150;Say("PERFECT LANDING  +"+(airPoints+150));}
                    else if(Mathf.Abs(spin)<360)Say("CLEAN AIR  +"+airPoints);
                }
            }
            CheckMissions();
            if(grounded){spin=0;airTime=0;airFlips=0;}
            previousAngle=body.rotation;wasGrounded=grounded;
            if(Time.time>comboUntil)combo=0;
            score+=Mathf.Max(0,body.linearVelocity.x)*Time.deltaTime*.7f;
            if(grounded && groundHit.collider.name=="Slope physics" && SafeCheckpoint(body.position.x) && body.position.x>checkpoint.x+10)checkpoint=new Vector2(body.position.x,Ground(body.position.x)+1.2f);
            if(body.position.y<Ground(body.position.x)-8) { Crash("Off the slope",true); return; }
            if(body.position.x>=finish){EndRun(true);return;}
            SyncEffects();
            visual.localScale=new Vector3(1,1+Mathf.Sin(Time.time*8)*(grounded?.025f:.06f),1);
        } else if(page==Page.Pause&&Press("pause"))Resume();
        else if(page==Page.Menu) {
            // Idle showcase: the rider sways gently while the live mountain plays behind the menu.
            float t=Time.unscaledTime;
            if(body){ body.rotation=Mathf.Sin(t*1.1f)*5f; previousAngle=body.rotation; }
            if(visual)visual.localScale=new Vector3(1,1+Mathf.Sin(t*2.2f)*.025f,1);
        }
        SyncEffects();
        if(shake>0)shake=Mathf.Max(0,shake-Time.unscaledDeltaTime*2.2f);
        if(comboFlash>0)comboFlash=Mathf.Max(0,comboFlash-Time.unscaledDeltaTime*2.4f);
        snowTime+=Time.unscaledDeltaTime;
        float half=cam.orthographicSize*cam.aspect;
        for(int i=0;i<weather.Count;i++) {
            weather[i].gameObject.SetActive(snowfall);
            if(!snowfall)continue;
            float x=Mathf.Repeat(i*7.317f-snowTime*(.7f+i%3),half*2+2)-half-1;
            float y=Mathf.Repeat(i*3.71f-snowTime*(1.3f+i%5*.3f),18)-9;
            weather[i].position=cam.transform.position+new Vector3(x,y,10);
        }
    }
    void FixedUpdate() {
        if(page==Page.Playing)StepMovement(Held(true),Held(false),Horizontal(),TrickHeld(),Time.fixedDeltaTime);
    }
    void StepMovement(bool accelerate,bool brake,float input,bool fastTrick,float dt) {
        var hit=GroundHit();bool grounded=hit.collider!=null;
        steering=Mathf.MoveTowards(steering,Mathf.Clamp(input,-1,1),dt*5);
        float angle=grounded?SurfaceAngle(hit):0;
        Vector2 tangent=grounded?new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad)):Vector2.right;
        float forward=Vector2.Dot(body.linearVelocity,tangent);
        float drive=accelerate?19:2;
        if(accelerate && grounded && forward<2.5f)drive+=9;
        if(Time.time<boostUntil)drive+=7;
        if(brake){drive=0;body.AddForce(-body.linearVelocity*4.2f);}
        body.AddForce(tangent*drive*(grounded?1:.35f));
        if(!grounded){
            float target=-steering*Mathf.Min(fastTrick?360:260,(fastTrick?360:210)*rotationSensitivity);
            float acceleration=Mathf.Abs(input)<.05f?1100:720;
            body.angularVelocity=Mathf.Clamp(Mathf.MoveTowards(body.angularVelocity,target,acceleration*dt),-360,360);
        } else {
            float correction=Mathf.Clamp(Mathf.DeltaAngle(body.rotation,angle+steering*7)*8,-85,85);
            body.angularVelocity=Mathf.Clamp(Mathf.MoveTowards(body.angularVelocity,correction,600*dt),-85,85);
        }
        var velocity=body.linearVelocity;
        velocity.x=Mathf.Clamp(velocity.x,-2,Time.time<boostUntil?MaxSpeed+4:MaxSpeed);
        body.linearVelocity=velocity;
    }
    void LateUpdate() {
        if(body) {
            Vector3 target = page==Page.Menu
                ? new Vector3(body.position.x-4.7f+Mathf.Sin(Time.unscaledTime*.5f)*.5f,body.position.y+1f,-10)
                : new Vector3(body.position.x+5,body.position.y+2.4f,-10);
            cam.transform.position=Vector3.Lerp(cam.transform.position,target,1-Mathf.Exp(-5*Time.unscaledDeltaTime));
            cam.orthographicSize=page==Page.Menu?6.8f:8f;
            if(shake>0)cam.transform.position+=(Vector3)(UnityEngine.Random.insideUnitCircle*.4f*shake);
        }
        if(atmosphere && cam)atmosphere.position=new Vector3(cam.transform.position.x,cam.transform.position.y,0);
    }
    public void Collect(SnowPickup item) {
        if(page!=Page.Playing || !item || !item.gameObject.activeSelf || item.kind==SnowPickup.Kind.Obstacle)return;
        if(Time.time>=comboUntil) combo=0;
        if(item.kind==SnowPickup.Kind.Flake){collected++;int points=100*Mathf.Max(1,combo);score+=points;Say("SNOWFLAKE  +"+points);PickupSfx(SnowSfx.Kind.Flake,combo);}
        if(item.kind==SnowPickup.Kind.Boost){boostUntil=Time.time+5;Say("SPEED BOOST   /   5 seconds");PickupSfx(SnowSfx.Kind.Boost,1);}
        if(item.kind==SnowPickup.Kind.Shield){shields=Mathf.Min(shields+1,3);Say("SHIELD READY   /   "+shields+" HIT"+(shields==1?"":"S"));PickupSfx(SnowSfx.Kind.Shield,1);}
        item.gameObject.SetActive(false); SyncEffects(); CheckMissions();
    }
    void CheckMissions() {
        if(page!=Page.Playing)return;
        if(!missions[0] && collected>=10){missions[0]=true;score+=300;Say("MISSION COMPLETE  /  10 flakes  +300");}
        if(!missions[1] && tricksLanded>=1){missions[1]=true;score+=400;Say("MISSION COMPLETE  /  First flip  +400");}
    }
    void SelectMode(bool timed){timeAttack=timed;best=PlayerPrefs.GetInt(BestKey,0);}
    void SelectTrack(int value){track=Mathf.Clamp(value,1,RouteName.Length);best=PlayerPrefs.GetInt(BestKey,0);ApplyMusic();}
    void StartRun() {
        Time.timeScale=1;
        foreach(var go in objects)if(go) {go.SetActive(false);Destroy(go);}
        objects.Clear();weather.Clear();pickups.Clear();hazards.Clear();
        foreach(var asset in generated) if(asset) Destroy(asset); generated.Clear();
        foreach(var pulse in FindObjectsByType<SnowComboPulse>(FindObjectsSortMode.None)) if(pulse) Destroy(pulse.gameObject);
        score=0;timer=0;respawnUntil=0;boostUntil=0;comboUntil=0;noticeUntil=0;spin=0;airTime=0;previousAngle=0;shieldGraceUntil=0;airFlips=0;shake=0;comboFlash=0;lastGrounded=-9f;
        lives=3;combo=0;collected=0;tricksLanded=0;steering=0;obstacleNoticeUntil=0;shields=0;System.Array.Clear(missions,0,missions.Length);wasGrounded=false;won=false;newRecord=false;reason="";fromPause=false;
        Course();body.linearVelocity=new Vector2(3,0);cam.transform.position=new Vector3(7,4,-10);
        page=Page.Playing;Say(RouteName[track-1]+"   /   Reach the finish!");
    }
    void Pause(){if(page!=Page.Playing)return;page=Page.Pause;Time.timeScale=0;SyncEffects();}
    void Resume(){if(page!=Page.Pause)return;page=Page.Playing;Time.timeScale=1;SyncEffects();}
    void Menu(){
        Time.timeScale=1;page=Page.Menu;body.simulated=false;shields=0;shieldGraceUntil=0;boostUntil=0;respawnUntil=0;combo=0;comboUntil=0;spin=0;airTime=0;airFlips=0;
        // Reset the rider to the start so the menu always frames a clean showcase.
        body.position=new Vector2(2,Ground(2)+.9f);body.rotation=0;body.linearVelocity=Vector2.zero;body.angularVelocity=0;
        SyncEffects();
    }
    // Deterministic rock rule, purely geometric - contact normal, event type and speed are
    // ignored so the answer never changes with the frame or the physics solver.
    //
    // "Riding" a rock means the board is genuinely resting on the rock's crown: the rider has
    // to be horizontally over the collider AND standing on the rock's surface at that exact x.
    // A flank hit (riding straight into the rock from the left) keeps the rider roughly 1.2
    // units outside the rock's 0.86 span, so it fails the first test by a wide margin no matter
    // how high the shoulder of the circle nudges the board - which is what used to make a
    // side impact read as a safe "rock ride" and silently eat the hit.
    bool RideRockTop(SnowPickup rock,Vector2 normal) {
        var collider=rock.GetComponent<Collider2D>();
        if(!collider) return false;
        var box=collider.bounds;
        float radius=Mathf.Min(box.size.x,box.size.y)*.5f;
        float dx=body.position.x-box.center.x;
        if(Mathf.Abs(dx)>radius) return false;
        // Height of the rock's own surface beneath the rider; on a circle this is the upper
        // half of the chord, so the test follows the crown instead of a single fixed height.
        float surface=box.center.y+Mathf.Sqrt(Mathf.Max(0f,radius*radius-dx*dx));
        return body.position.y-.2f>=surface-.08f;
    }
    public void HandleObstacleContact(SnowPickup obstacle,Vector2 normal,float impact,bool entering) {
        if(page!=Page.Playing || !obstacle || obstacle.kind!=SnowPickup.Kind.Obstacle)return;
        if(Time.time<shieldGraceUntil || Time.time<respawnUntil)return;
        if(!obstacle.isRock){Crash("Obstacle hit",false,obstacle);return;}
        if(RideRockTop(obstacle,normal)) {
            if(entering && Time.time>obstacleNoticeUntil){Say("ROCK RIDE   /   clean top contact");obstacleNoticeUntil=Time.time+1;}
            return;
        }
        Crash("Obstacle hit",false,obstacle);
    }
    public bool Crash(string why, bool forceRecovery=false, SnowPickup obstacle=null) {
        if(page!=Page.Playing)return false;
        // A single-use shield absorbs the hit instead of a life. Forced recoveries
        // (falling off the slope) always resolve, so the rider can never fall forever.
        if(!forceRecovery){
            if(Time.time<shieldGraceUntil || Time.time<respawnUntil)return false;
            if(shields>0){
                shields--;shieldGraceUntil=Time.time+1.1f;SyncEffects();
                if(obstacle){
                    // The shield spends itself by setting the rider down past the obstacle instead of
                    // leaving them pressed against it, so play can continue straight away.
                    float dir=Mathf.Sign(body.linearVelocity.x);if(dir==0)dir=1;
                    float spawnX=obstacle.transform.position.x+dir*2.2f;
                    foreach(float h in hazards)if(Mathf.Abs(h-spawnX)<1.4f)spawnX+=dir*1.2f;
                    spawnX=Mathf.Clamp(spawnX,2f,finish-4f);
                    float spawnY=Ground(spawnX);
                    if(routeRamps!=null)foreach(float ramp in routeRamps)if(spawnX>=ramp-5&&spawnX<=ramp)spawnY=Mathf.Max(spawnY,Mathf.Lerp(Ground(ramp-5)+.04f,Ground(ramp)+2.4f,(spawnX-ramp+5)/5f));
                    body.position=new Vector2(spawnX,spawnY+1.05f);body.rotation=0;body.angularVelocity=0;body.linearVelocity=new Vector2(dir*5f,0);
                } else body.linearVelocity=new Vector2(Mathf.Min(body.linearVelocity.x,4),Mathf.Max(body.linearVelocity.y,3));
                Say("SHIELD ABSORBED   /   "+(shields>0?shields+" LEFT":"GONE"));return true;
            }
        }
        lives--;combo=0;comboUntil=0;spin=0;airTime=0;airFlips=0;boostUntil=0;shieldGraceUntil=0;shake=.4f;
        wasGrounded=false; previousAngle=0; steering=0; SyncEffects(); Sfx("Crash SFX");
        if(lives<=0){reason=why;EndRun(false);return true;}
        float rx=checkpoint.x;
        if(why=="Obstacle hit")rx=Mathf.Max(rx,body.position.x+1);
        foreach(float h in hazards)if(Mathf.Abs(h-rx)<3)rx=h+3.5f;
        rx=Mathf.Clamp(rx,2,finish-4);
        // Spawn above both ground and ramp, never inside their colliders.
        float y=Ground(rx);
        if(routeRamps!=null) foreach(float ramp in routeRamps)
            if(rx>=ramp-5 && rx<=ramp) y=Mathf.Max(y,Mathf.Lerp(Ground(ramp-5)+.04f,Ground(ramp)+2.4f,(rx-ramp+5)/5f));
        body.position=new Vector2(rx,y+1.2f);body.rotation=0;body.linearVelocity=new Vector2(4,0);body.angularVelocity=0;
        respawnUntil=Time.time+2.5f;
        Say(why.ToUpperInvariant()+"   /   "+lives+" lives left");return true;
    }
    // Reward moment for a new combo step: shockwave at the rider, a sting that climbs with the
    // multiplier, a short camera kick and a flash the HUD card plays off.
    void ComboBurst() {
        SnowComboPulse.Burst(body.position,combo);
        PickupSfx(SnowSfx.Kind.Combo,combo);
        shake=Mathf.Max(shake,.2f);
        comboFlash=1f;
    }
    void Sfx(string name){var clip=Resources.Load<AudioClip>("SnowBoarder/Audio/"+name);if(clip)audioSource.PlayOneShot(clip,volume);}
    // Pickups reuse the same one-shot source as Crash/Finish so the audio slider governs them too.
    void PickupSfx(SnowSfx.Kind kind,int comboLevel){var clip=SnowSfx.Clip(kind,comboLevel);if(clip)audioSource.PlayOneShot(clip,volume);}
    void Say(string value){notice=value;noticeUntil=Time.time+2.3f;}
    void EndRun(bool success) {
        if(page!=Page.Playing)return;
        won=success;
        if(success){
            if(lives>=2 && !missions[2]){missions[2]=true;score+=500;}
            score+=lives*300+Mathf.Max(0,(timeAttack?timeLimit:120)-timer)*10;Sfx("Finish SFX");
        }
        newRecord=Mathf.RoundToInt(score)>best;
        best=Mathf.Max(best,Mathf.RoundToInt(score));PlayerPrefs.SetInt(BestKey,best);PlayerPrefs.Save();
        body.simulated=false;page=Page.Result;shields=0;shieldGraceUntil=0;boostUntil=0;respawnUntil=0;SyncEffects();
    }
    void OnDestroy(){
        Time.timeScale=1;ReleasePresentation();SnowComboPulse.Release();
        foreach(var pulse in FindObjectsByType<SnowComboPulse>(FindObjectsSortMode.None)) if(pulse) Destroy(pulse.gameObject);
        foreach(var go in objects) if(go) Destroy(go);
        foreach(var asset in generated) if(asset) Destroy(asset);
        if(pixel){Destroy(pixel.texture);Destroy(pixel);}
        if(snow)Destroy(snow);if(panel)Destroy(panel);if(mint)Destroy(mint);
        if(cam)Destroy(cam.gameObject);
    }
    Texture2D Solid(Color color){var t=new Texture2D(1,1);t.SetPixel(0,0,color);t.Apply();return t;}
    Texture2D RoundedTexture(Color color) {
        var t=new Texture2D(32,32);t.wrapMode=TextureWrapMode.Clamp;
        for(int y=0;y<32;y++)for(int x=0;x<32;x++){
            float dx=Mathf.Max(Mathf.Abs(x-15.5f)-7.5f,0),dy=Mathf.Max(Mathf.Abs(y-15.5f)-7.5f,0);
            var c=color;c.a*=Mathf.Clamp01(8.5f-Mathf.Sqrt(dx*dx+dy*dy));t.SetPixel(x,y,c);
        }t.Apply();return t;
    }
    void Quit() {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying=false;
#else
        Application.Quit();
#endif
    }
}
public sealed class SnowRider : MonoBehaviour {
    public SnowBoarderGame game;
    void OnTriggerEnter2D(Collider2D other){var item=other.GetComponent<SnowPickup>();if(item&&item.kind!=SnowPickup.Kind.Obstacle)game.Collect(item);}
    void Contact(Collision2D collision,bool entering){
        var item=collision.gameObject.GetComponent<SnowPickup>();if(!item || item.kind!=SnowPickup.Kind.Obstacle)return;
        Vector2 normal=collision.contactCount>0?collision.GetContact(0).normal:Vector2.zero;
        game.HandleObstacleContact(item,normal,Mathf.Abs(Vector2.Dot(collision.relativeVelocity,normal)),entering);
    }
    void OnCollisionEnter2D(Collision2D collision){Contact(collision,true);}
    void OnCollisionStay2D(Collision2D collision){Contact(collision,false);}
}
public sealed class SnowPickup : MonoBehaviour {
    public enum Kind {Flake,Boost,Shield,Obstacle}
    public Kind kind;
    public bool isRock;
}
// Expanding shockwave shown the moment the combo multiplier goes up. The ring mesh and its
// material are shared by every pulse and each one tints itself through a MaterialPropertyBlock,
// so chaining tricks in mid-air allocates nothing and cleans itself up when it finishes.
public sealed class SnowComboPulse : MonoBehaviour {
    const int Segments = 72;
    static Mesh ring;
    static Material ringMaterial;
    static readonly MaterialPropertyBlock tint = new MaterialPropertyBlock();
    MeshRenderer meshRenderer;
    float life, reach = 2.4f;
    Color colour = Color.white;

    public static void Burst(Vector2 position,int level) {
        var source = Resources.Load<Material>("SnowBoarder/WorldMaterial");
        if(!source) return;                                   // no world material: skip, never crash
        if(ring==null) ring = BuildRing();
        if(ringMaterial==null) ringMaterial = new Material(source){name="Combo pulse"};
        float heat=(Mathf.Clamp(level,1,8)-1)/7f;
        var go = new GameObject("Combo pulse");
        go.transform.position = position;
        go.transform.localScale = Vector3.one*.2f;
        go.AddComponent<MeshFilter>().sharedMesh = ring;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = ringMaterial;
        renderer.sortingOrder = 17;
        var pulse = go.AddComponent<SnowComboPulse>();
        pulse.meshRenderer = renderer;
        pulse.reach = Mathf.Lerp(2.2f,3.8f,heat);
        pulse.colour = Color.Lerp(new Color(1,1,1,.9f),new Color(1,.78f,.26f,.95f),heat);
        pulse.Tint(1f);
    }
    public static void Release() {
        if(ring) Destroy(ring);
        if(ringMaterial) Destroy(ringMaterial);
        ring=null; ringMaterial=null;
    }
    static Mesh BuildRing() {
        var vertices = new Vector3[Segments*2];
        var indices = new int[Segments*6];
        for(int i=0;i<Segments;i++) {
            float a=i*Mathf.PI*2/Segments; var direction=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
            vertices[i*2]=direction; vertices[i*2+1]=direction*.62f;
            int next=(i+1)%Segments*2, k=i*6, v=i*2;
            indices[k]=v; indices[k+1]=next; indices[k+2]=v+1;
            indices[k+3]=v+1; indices[k+4]=next; indices[k+5]=next+1;
        }
        var mesh = new Mesh{name="Combo pulse ring"};
        mesh.vertices=vertices; mesh.triangles=indices; mesh.RecalculateBounds();
        return mesh;
    }
    void Tint(float alpha) {
        tint.Clear();
        var c = new Color(colour.r,colour.g,colour.b,colour.a*alpha);
        tint.SetColor("_Color",c); tint.SetColor("_BaseColor",c);
        meshRenderer.SetPropertyBlock(tint);
    }
    void Update() {
        life += Time.deltaTime;
        float t = Mathf.Clamp01(life/.45f);
        transform.localScale = Vector3.one*Mathf.Lerp(.2f,reach,t*t);
        Tint(1f - t*t);
        if(life>=.45f) Destroy(gameObject);
    }
}
}

