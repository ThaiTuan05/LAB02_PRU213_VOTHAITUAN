using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class Lab2Validation {
    static Lab2Validation() { if(SessionState.GetBool("Lab2.Validate",false)) { began=EditorApplication.timeSinceStartup; EditorApplication.update+=Tick; } }
    static double began;
    static int stage;
    static Lab2.SnowBoarderGame game;
    static int checks;
    static int previewRoute;
    static BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run() {
        SessionState.SetBool("Lab2.Validate",true); SessionState.SetInt("Lab2.PreviousBest",PlayerPrefs.GetInt("Snow.Best",0)); SessionState.SetInt("Lab2.PreviousTimeBest",PlayerPrefs.GetInt("Snow.Best.TimeAttack",0));
        if(!AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/SnowBoarder/WorldMaterial.mat")) { AssetDatabase.CreateAsset(new Material(Shader.Find("Sprites/Default")),"Assets/Resources/SnowBoarder/WorldMaterial.mat"); AssetDatabase.SaveAssets(); }
        if(!AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/SnowBoarder/SpriteMaterial.mat")) { AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")),"Assets/Resources/SnowBoarder/SpriteMaterial.mat"); AssetDatabase.SaveAssets(); }
        began=EditorApplication.timeSinceStartup; EditorApplication.update-=Tick; EditorApplication.update+=Tick;
        EditorSceneManager.OpenScene("Assets/Scenes/SnowBoarder.unity");
        EditorSettings.enterPlayModeOptionsEnabled=false;
                var viewType=Type.GetType("UnityEditor.GameView,UnityEditor");
        var view=EditorWindow.GetWindow(viewType);
        var sizesType=Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
        var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes=singleton.GetProperty("instance").GetValue(null);
        var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{0});
        var sizeType=Type.GetType("UnityEditor.GameViewSize,UnityEditor");
        var modeType=Type.GetType("UnityEditor.GameViewSizeType,UnityEditor");
        var size=Activator.CreateInstance(sizeType, new object[]{Enum.ToObject(modeType,1),1280,720,"Lab2 Preview"});
        group.GetType().GetMethod("AddCustomSize").Invoke(group,new object[]{size});
        int builtIn=(int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group,null);
        int custom=(int)group.GetType().GetMethod("GetCustomCount").Invoke(group,null);
        viewType.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(view,builtIn+custom-1);
        view.Show();
        EditorApplication.isPlaying = true;
    }
    static void OnPlay(PlayModeStateChange state) {
        if (state == PlayModeStateChange.EnteredPlayMode) {began=EditorApplication.timeSinceStartup; EditorApplication.update+=Tick;}
    }
    static object Get(string name) {return game.GetType().GetField(name,flags).GetValue(game);}
    static void Set(string name,object value) {game.GetType().GetField(name,flags).SetValue(game,value);}
    static object Call(string name,params object[] args) {return game.GetType().GetMethod(name,flags).Invoke(game,args);}
    static void Check(bool valid,string name) {if(!valid)throw new Exception("FAILED: "+name); checks++; Debug.Log("LAB2 PASS: "+name);}
    static void CaptureRoute(){
        Call("SelectTrack",previewRoute);Call("SelectMode",false);Call("StartRun");
        var camera=(Camera)Get("cam");
        Color expected=previewRoute==1?new Color(234/255f,219/255f,200/255f):previewRoute==2?new Color(173/255f,221/255f,235/255f):new Color(32/255f,45/255f,74/255f);
        Check(Vector4.Distance(camera.backgroundColor,expected)<.01f,"route "+previewRoute+" uses its distinctive sky palette");
        if(previewRoute==2)Check(GameObject.Find("Glacier spire")!=null && GameObject.Find("Decorative pine - no collision")==null,"glacier landscape uses ice spires instead of decorative trees");
        if(previewRoute==3)Check(GameObject.Find("Aurora band 0")!=null && GameObject.Find("Moon")!=null,"summit landscape includes aurora and moon");
        Lab2.SnowPickup tree=null;
        foreach(var item in UnityEngine.Object.FindObjectsByType<Lab2.SnowPickup>())if(item.kind==Lab2.SnowPickup.Kind.Obstacle&&!item.isRock && (tree==null || item.transform.position.x<tree.transform.position.x))tree=item;
        Check(tree!=null && tree.transform.Find("Tree warning markers")!=null,"route "+previewRoute+" foreground trees have visible warning markers");
        Check(Mathf.Abs(tree.GetComponent<SpriteRenderer>().bounds.min.y-(float)Call("Ground",tree.transform.position.x))<.01f,"route "+previewRoute+" tree sprite is grounded on the snow surface");
        foreach(var renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>())if(renderer.name=="Decorative pine - no collision") {
            Check(renderer.color.a<=.28f && renderer.sortingOrder<0 && renderer.GetComponent<Collider2D>()==null,"decorative tree is faint, behind terrain, and non-colliding");
            foreach(var item in UnityEngine.Object.FindObjectsByType<Lab2.SnowPickup>())if(item.kind==Lab2.SnowPickup.Kind.Obstacle&&!item.isRock)Check(Mathf.Abs(item.transform.position.x-renderer.transform.position.x)>=11,"decorative pine does not overlap a foreground tree warning lane");
            break;
        }
        var body=(Rigidbody2D)Get("body");float x=tree.transform.position.x-6;float y=(float)Call("Ground",x);
        body.simulated=false;float angle=Mathf.Atan2((float)Call("Ground",x+.5f)-(float)Call("Ground",x-.5f),1)*Mathf.Rad2Deg;
        body.position=new Vector2(x,y+.3f);body.rotation=angle;body.transform.SetPositionAndRotation(new Vector3(x,y+.3f,0),Quaternion.Euler(0,0,angle));body.linearVelocity=Vector2.zero;body.angularVelocity=0;Set("noticeUntil",0f);
        camera.transform.position=new Vector3(x+5,y+2.7f,-10);Physics2D.SyncTransforms();
    }
    static void Tick() {
        try {
            if(!EditorApplication.isPlaying) return;
            double t=EditorApplication.timeSinceStartup-began;
            if(stage==0 && t>1) {
                game=UnityEngine.Object.FindAnyObjectByType<Lab2.SnowBoarderGame>();
                Check(game!=null,"runtime bootstrap");
                Check(Resources.Load<Sprite>("SnowBoarder/Sprites/Boarder_Top")!=null,"rider sprite imported");
                Check(Resources.Load<AudioClip>("SnowBoarder/Audio/Crash SFX")!=null,"audio imported");
                Check(UnityEngine.Object.FindObjectsByType<Lab2.SnowPickup>().Length==52,"40 flakes, 4 powers, 8 obstacles");
                ScreenCapture.CaptureScreenshot("Documentation/Menu.png");stage=1; began=EditorApplication.timeSinceStartup; return;
            }
            if(stage==1 && t>1) {Call("StartRun");stage=2; began=EditorApplication.timeSinceStartup; return;}
            if(stage==2 && t>2 && (int)Get("collected")>0) {
                var body=(Rigidbody2D)Get("body");
                Check(body.position.x>3,"physics moves player downhill");
                Check((float)Get("score")>0,"speed score accrues");
                Check((int)Get("collected")>0,"physics trigger collects reachable course snowflakes");
                ScreenCapture.CaptureScreenshot("Documentation/Gameplay.png");stage=3; began=EditorApplication.timeSinceStartup; return;
            }
            if(stage==3 && t>1) {
                Call("Pause");Check(Time.timeScale==0,"pause freezes physics");Call("Resume");Check(Time.timeScale==1,"resume restores physics"); Call("StartRun");
                var items=UnityEngine.Object.FindObjectsByType<Lab2.SnowPickup>();
                foreach(var item in items) if(item.kind==Lab2.SnowPickup.Kind.Flake){int before=(int)Get("collected");game.Collect(item);Check((int)Get("collected")==before+1&&!item.gameObject.activeSelf,"flake collection and deactivation");break;}
                foreach(var item in items) if(item.kind==Lab2.SnowPickup.Kind.Boost){game.Collect(item);Check((float)Get("boostUntil")>Time.time,"speed boost timer");break;}
                foreach(var item in items) if(item.kind==Lab2.SnowPickup.Kind.Shield){game.Collect(item);int lives=(int)Get("lives");game.Crash("test");Check((int)Get("lives")==lives && (int)Get("shields")==0,"single-use shield absorbs a crash without losing a life");break;}
                Set("shieldGraceUntil",0f);Set("respawnUntil",0f);game.Crash("test");Check((int)Get("lives")==2,"obstacle loses one life");
                Check(!((Transform)Get("shield")).gameObject.activeSelf && (int)Get("shields")==0,"respawn has no green power-up effect");
                Check((float)Get("boostUntil")==0 && (float)Get("respawnUntil")>Time.time,"death clears boost and uses separate recovery timer");
                game.Crash("test");Check((int)Get("lives")==2,"repeated collision during recovery cannot consume lives");
                Call("StartRun");Check((int)Get("lives")==3&&(int)Get("collected")==0&&(float)Get("score")==0,"restart resets state");
                // A full rotation scores the instant it completes in the air, before landing.
                var body=(Rigidbody2D)Get("body");body.position=new Vector2(12,Lab2.SnowBoarderGame.Height(12)+30);body.rotation=0;body.angularVelocity=0;body.linearVelocity=Vector2.zero;
                Physics2D.SyncTransforms();Set("previousAngle",0f);Set("spin",0f);Set("airFlips",0);Set("airTime",0f);Set("wasGrounded",true);Set("combo",0);Set("comboUntil",0f);
                Set("spin",360f);Call("Update");
                Check((int)Get("combo")==1&&(float)Get("score")>=250,"full in-air rotation counts a combo before landing");
                Set("spin",720f);Call("Update");Check((int)Get("combo")==2&&(int)Get("airFlips")==2,"a second in-air rotation chains the combo mid-flight");
                Call("EndRun",true);Check((bool)Get("won"),"finish result and bonus");
                float resultScore=(float)Get("score");Call("EndRun",true);Call("EndRun",false);
                Check((float)Get("score")==resultScore && (bool)Get("won"),"finish is idempotent and cannot become a loss");
                Call("StartRun");
                var top=GameObject.Find("Rider").GetComponent<SpriteRenderer>();
                var bottom=GameObject.Find("Snowboard").GetComponent<SpriteRenderer>();
                Check(top.sprite!=null && top.enabled && top.sortingOrder>bottom.sortingOrder && top.sharedMaterial!=null && top.sharedMaterial.shader.name=="Universal Render Pipeline/2D/Sprite-Unlit-Default","upper body is enabled with explicit unlit material");
                float upperScale=top.transform.localScale.x/top.sprite.pixelsPerUnit;
                float lowerScale=bottom.transform.localScale.x/bottom.sprite.pixelsPerUnit;
                Check(Mathf.Abs(upperScale-lowerScale)<.00001f && top.transform.localPosition.y<.65f,"sprite halves share pixel scale and waist alignment");
                items=UnityEngine.Object.FindObjectsByType<Lab2.SnowPickup>();
                foreach(var item in items)if(item.kind==Lab2.SnowPickup.Kind.Shield){game.Collect(item);break;}
                Check(((Transform)Get("shield")).gameObject.activeSelf&&(int)Get("shields")>0,"shield power-up activates its effect immediately");
                int shieldLives=(int)Get("lives");Set("shieldGraceUntil",0f);Set("respawnUntil",0f);game.Crash("test");
                Check((int)Get("lives")==shieldLives&&(int)Get("shields")==0&&!((Transform)Get("shield")).gameObject.activeSelf,"single-use shield absorbs one hit then disappears");
                foreach(var item in items)if(item.kind==Lab2.SnowPickup.Kind.Flake){Set("combo",8);Set("comboUntil",Time.time-1);float before=(float)Get("score");game.Collect(item);Check(Mathf.Abs((float)Get("score")-before-100)<.01f,"expired combo cannot multiply pickup score");break;}
                game.Collect(null);Check(true,"null pickup is safely ignored");
                Set("shields",1);Set("shieldGraceUntil",0f);body=(Rigidbody2D)Get("body");body.position=new Vector2(20,Lab2.SnowBoarderGame.Height(20)-12);
                Call("Update");Check((int)Get("lives")==2 && body.position.y>Lab2.SnowBoarderGame.Height(body.position.x),"falling out of bounds recovers even while shielded");
                Set("shields",0);Set("shieldGraceUntil",0f);
                Set("respawnUntil",0f);Set("checkpoint",new Vector2(37,0));game.Crash("test");
                float rampY=Mathf.Lerp(Lab2.SnowBoarderGame.Height(33)+.04f,Lab2.SnowBoarderGame.Height(38)+2.4f,.8f);
                Check(body.position.y>rampY+1,"respawn above a ramp never overlaps terrain");
                Call("StartRun");body=(Rigidbody2D)Get("body");body.position=new Vector2(37,rampY+.2f);body.rotation=0;Physics2D.SyncTransforms();
                float actual=(float)Call("SurfaceAngle",Call("GroundHit"));
                float expected=Mathf.Atan2(Lab2.SnowBoarderGame.Height(38)+2.4f-(Lab2.SnowBoarderGame.Height(33)+.04f),5)*Mathf.Rad2Deg;
                Check(Mathf.Abs(actual-expected)<1,"ground orientation follows actual ramp collision normal");
                Call("StartRun");body=(Rigidbody2D)Get("body");body.position=new Vector2(12,Lab2.SnowBoarderGame.Height(12)+.2f);body.rotation=0;Physics2D.SyncTransforms();
                Set("wasGrounded",false);Set("airTime",.7f);Set("spin",345f);Call("Update");Check((int)Get("combo")==0,"partial flips do not receive full-flip points");
                foreach(var item in UnityEngine.Object.FindObjectsByType<Lab2.SnowPickup>())if(item.kind==Lab2.SnowPickup.Kind.Shield){game.Collect(item);break;}
                Call("Menu");Check(!((Transform)Get("shield")).gameObject.activeSelf,"main menu clears lingering shield");
                Call("StartRun");Set("shieldGraceUntil",0f);Set("respawnUntil",0f);game.Crash("test");Set("shieldGraceUntil",0f);Set("respawnUntil",0f);game.Crash("test");Set("shieldGraceUntil",0f);Set("respawnUntil",0f);game.Crash("test");Check((int)Get("lives")==0&&Get("page").ToString()=="Result","three crashes end the run");
                Call("SelectMode",false);Call("StartRun");body=(Rigidbody2D)Get("body");Set("rotationSensitivity",.9f);
                var simulation=Physics2D.simulationMode;
                try {
                    Physics2D.simulationMode=SimulationMode2D.Script;
                    body.position=new Vector2(1,Lab2.SnowBoarderGame.Height(1)+.23f);body.rotation=0;body.linearVelocity=Vector2.zero;body.angularVelocity=0;Physics2D.SyncTransforms();
                    for(int i=0;i<50;i++){Call("StepMovement",true,false,0f,false,.02f);Physics2D.Simulate(.02f);}
                    Check(body.linearVelocity.x>4 && body.position.x>2,"W motor restarts a stationary rider on uphill terrain");
                    body.position=new Vector2(4,Lab2.SnowBoarderGame.Height(4)+25);body.linearVelocity=Vector2.zero;body.angularVelocity=0;Set("steering",0f);Physics2D.SyncTransforms();
                    for(int i=0;i<30;i++){Call("StepMovement",false,false,1f,false,.02f);Physics2D.Simulate(.02f);}
                    float ordinary=Mathf.Abs(body.angularVelocity);Check(ordinary>100 && ordinary<=210,"ordinary A/D rotates at a controlled speed");
                    for(int i=0;i<12;i++){Call("StepMovement",false,false,0f,false,.02f);Physics2D.Simulate(.02f);}
                    Check(Mathf.Abs(body.angularVelocity)<10,"releasing A/D quickly stops angular momentum");
                    for(int i=0;i<30;i++){Call("StepMovement",false,false,1f,true,.02f);Physics2D.Simulate(.02f);}
                    Check(Mathf.Abs(body.angularVelocity)>ordinary+80 && Mathf.Abs(body.angularVelocity)<=360,"Shift enables intentional faster trick rotation");
                } finally {Physics2D.simulationMode=simulation;}
                Call("StartRun");body=(Rigidbody2D)Get("body");
                Lab2.SnowPickup rock=null,tree=null;
                foreach(var item in UnityEngine.Object.FindObjectsByType<Lab2.SnowPickup>())if(item.kind==Lab2.SnowPickup.Kind.Obstacle){if(item.isRock)rock=item;else tree=item;}
                var rockCollider=rock.GetComponent<Collider2D>();
                Check(Mathf.Abs(rock.GetComponent<SpriteRenderer>().bounds.min.y-Lab2.SnowBoarderGame.Height(rock.transform.position.x))<.25f,"rock obstacle is grounded on the snow surface");
                float rockX=rock.transform.position.x,rockTop=rockCollider.bounds.max.y,rockMid=rockCollider.bounds.center.y;
                body.rotation=0;body.position=new Vector2(rockX,rockTop+.25f);Physics2D.SyncTransforms();
                game.HandleObstacleContact(rock,Vector2.up,8,true);Check((int)Get("lives")==3,"landing on a rock top does not damage the rider");
                Set("shieldGraceUntil",0f);body.position=new Vector2(rockX-.9f,rockMid-.35f);body.linearVelocity=new Vector2(3,0);Physics2D.SyncTransforms();
                game.HandleObstacleContact(rock,Vector2.left,2,true);Check((int)Get("lives")==2,"grounded rock contact always costs exactly one life");
                Set("respawnUntil",0f);Set("shieldGraceUntil",0f);body.position=new Vector2(rockX,rockTop+.25f);body.rotation=0;Physics2D.SyncTransforms();
                game.HandleObstacleContact(rock,Vector2.up,9,true);Check((int)Get("lives")==2,"rock top contact stays safe at high speed too");
                Set("respawnUntil",0f);Set("shieldGraceUntil",0f);Set("shields",1);body.position=new Vector2(rockX-.9f,rockMid-.35f);body.linearVelocity=new Vector2(5,0);body.rotation=0;Physics2D.SyncTransforms();
                game.HandleObstacleContact(rock,Vector2.left,6,true);
                Check((int)Get("lives")==2 && (int)Get("shields")==0 && body.position.x>rockX,"shield spends itself and sets the rider down past the rock");
                Set("respawnUntil",0f);Set("shieldGraceUntil",0f);game.HandleObstacleContact(tree,Vector2.left,1,true);Check((int)Get("lives")==1,"tree trunk remains an explicit crash hazard");
                // Regression for the reported rock bug: riding straight into the first rock of Ridge Run
                // from the left must cost a life even though the shoulder of the circle nudges the board
                // upwards, while sliding across the crown stays safe.
                Call("StartRun");body=(Rigidbody2D)Get("body");
                rock=null;tree=null;
                foreach(var item in UnityEngine.Object.FindObjectsByType<Lab2.SnowPickup>())if(item.kind==Lab2.SnowPickup.Kind.Obstacle){if(item.isRock&&(rock==null||item.transform.position.x<rock.transform.position.x))rock=item;else if(!item.isRock)tree=item;}
                rockCollider=rock.GetComponent<Collider2D>();
                rockX=rock.transform.position.x;rockTop=rockCollider.bounds.max.y;rockMid=rockCollider.bounds.center.y;
                float rockRadius=rockCollider.bounds.extents.x;
                Check(Mathf.Abs(rockX-61f)<.01f,"route 1 first rock is the reported obstacle at x=61");
                body.rotation=0;body.position=new Vector2(rockX-1.18f,Lab2.SnowBoarderGame.Height(rockX-1.18f)+.2f);body.linearVelocity=new Vector2(8,0);Physics2D.SyncTransforms();
                game.HandleObstacleContact(rock,Vector2.left,8,true);
                Check((int)Get("lives")==2,"slamming into the rock from the left always costs exactly one life");
                Set("respawnUntil",0f);Set("shieldGraceUntil",0f);
                float crownDx=rockRadius*.6f;
                body.rotation=0;body.position=new Vector2(rockX+crownDx,rockMid+Mathf.Sqrt(Mathf.Max(0f,rockRadius*rockRadius-crownDx*crownDx))+.22f);body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
                game.HandleObstacleContact(rock,Vector2.up,4,true);Check((int)Get("lives")==2,"sliding across the rock crown stays safe");
                body.position=new Vector2(rockX,rockTop+.25f);Physics2D.SyncTransforms();
                game.HandleObstacleContact(rock,Vector2.up,4,true);Check((int)Get("lives")==2,"landing on the crown from a jump is not punished as a bad landing");
                Check(Mathf.Abs((float)Call("SurfaceAngle",Call("GroundHit")))<20,"rock surface angle follows the slope, not the circle normal");
                Set("respawnUntil",0f);Set("shieldGraceUntil",0f);
                var ridgeClip=Lab2.SnowMusic.Theme(1);var glacierClip=Lab2.SnowMusic.Theme(2);var summitClip=Lab2.SnowMusic.Theme(3);
                Check(ridgeClip!=null&&glacierClip!=null&&summitClip!=null&&ridgeClip.length>15f&&glacierClip.length>20f&&summitClip.length>25f&&ridgeClip.frequency==Lab2.SnowSynth.SampleRate,"each route gets its own synthesised music loop");
                // Pickup sounds: every level of the flake ladder, boost and shield must exist and
                // actually carry audio, and a higher combo must pick a different clip.
                var flakeLadder=new AudioClip[Lab2.SnowSfx.FlakeLevels];
                bool ladderOk=true,ladderAscends=true;
                for(int level=1;level<=Lab2.SnowSfx.FlakeLevels;level++){
                    flakeLadder[level-1]=Lab2.SnowSfx.Clip(Lab2.SnowSfx.Kind.Flake,level);
                    if(flakeLadder[level-1]==null||flakeLadder[level-1].length<.15f||flakeLadder[level-1].length>1f)ladderOk=false;
                    if(level>1&&flakeLadder[level-1]==flakeLadder[level-2])ladderAscends=false;
                }
                Check(ladderOk,"snowflakes have a sound at every combo level");
                Check(ladderAscends,"the combo multiplier climbs a distinct flake pitch");
                var boostClip=Lab2.SnowSfx.Clip(Lab2.SnowSfx.Kind.Boost,1);var shieldClip=Lab2.SnowSfx.Clip(Lab2.SnowSfx.Kind.Shield,1);
                Check(boostClip!=null&&shieldClip!=null&&boostClip!=shieldClip&&boostClip.length>.3f&&shieldClip.length>.4f,"boost and shield each have their own pickup sound");
                float probeLevel=0f;var probe=new float[2048];boostClip.GetData(probe,0);
                foreach(var value in probe)probeLevel+=System.Math.Abs(value);
                Check(probeLevel>1f,"the boost pickup sound carries real audio data");
                Check(Lab2.SnowSfx.Clip(Lab2.SnowSfx.Kind.Flake,99)==flakeLadder[Lab2.SnowSfx.FlakeLevels-1],"an out-of-range combo level is clamped instead of regenerating");
                float edge=0f;var edgeSamples=new float[64];flakeLadder[0].GetData(edgeSamples,flakeLadder[0].samples-64);
                foreach(var value in edgeSamples){float a=System.Math.Abs(value);if(a>edge)edge=a;}
                Check(edge<.02f,"pickup sounds end on silence so they cannot click");
                // Combo reward: one sting per multiplier level, and a new shockwave spawns each time
                // the multiplier goes up (then cleans itself up instead of piling up forever).
                var comboLadder=new AudioClip[Lab2.SnowSfx.FlakeLevels];
                bool comboOk=true,comboDistinct=true;
                for(int level=1;level<=Lab2.SnowSfx.FlakeLevels;level++){
                    comboLadder[level-1]=Lab2.SnowSfx.Clip(Lab2.SnowSfx.Kind.Combo,level);
                    if(comboLadder[level-1]==null||comboLadder[level-1].length<.2f)comboOk=false;
                    if(level>1&&comboLadder[level-1]==comboLadder[level-2])comboDistinct=false;
                }
                Check(comboOk,"every combo step has its own reward sting");
                Check(comboDistinct&&comboLadder[7]!=flakeLadder[7],"combo stings climb independently of the flake ladder");
                Set("comboFlash",0f);
                Call("ComboBurst");
                Check((float)Get("comboFlash")==1f&&(float)Get("shake")>.15f,"a new combo step flashes the HUD and kicks the camera");
                var pulses=UnityEngine.Object.FindObjectsByType<Lab2.SnowComboPulse>(FindObjectsSortMode.None);
                Check(pulses.Length>0,"a combo step spawns a shockwave in the world");
                Call("ComboBurst");Call("ComboBurst");
                Check(UnityEngine.Object.FindObjectsByType<Lab2.SnowComboPulse>(FindObjectsSortMode.None).Length==pulses.Length+2,"each combo step spawns its own shockwave");
                // Destroy is deferred to the end of the frame, so the leftover shockwave cleanup is
                // re-checked in stage 4 rather than on this same tick.
                Call("StartRun");
                Set("musicOn",false);Call("ApplyMusic");
                Check(!((AudioSource)Get("musicSource")).isPlaying&&((AudioSource)Get("musicSource")).volume==0f,"the music toggle silences the background theme");
                Set("musicOn",true);Call("SelectTrack",3);Call("StartRun");
                var musicSource=(AudioSource)Get("musicSource");
                Check(musicSource.loop&&musicSource.isPlaying&&musicSource.clip==summitClip,"changing route swaps and loops the background theme");
                float ridgeLevel=0f;var ridgeProbe=new float[4096];ridgeClip.GetData(ridgeProbe,0);
                foreach(var value in ridgeProbe)ridgeLevel+=System.Math.Abs(value);
                Check(ridgeLevel>1f&&ridgeClip.samples==(int)(ridgeClip.length*ridgeClip.frequency),"the music loop carries real audio data");
                Call("SelectTrack",1);Call("StartRun");body=(Rigidbody2D)Get("body");body.position=new Vector2(61,Lab2.SnowBoarderGame.Height(61)+1.15f);Physics2D.SyncTransforms();
                Call("StartRun");body=(Rigidbody2D)Get("body");body.position=new Vector2(61,Lab2.SnowBoarderGame.Height(61)+1.15f);Physics2D.SyncTransforms();
                Check((bool)Call("Grounded"),"rock top counts as ground so player can push or jump off it");
                var rim=((Transform)Get("shield")).Find("Shield rim").gameObject;
                var vertices=rim.GetComponent<MeshFilter>().sharedMesh.vertices;
                Check(vertices.Length==192 && Mathf.Abs(vertices[0].magnitude-vertices[48].magnitude)<.001f,"shield has a round annulus mesh instead of a square sprite");
                Set("collected",10);float missionScore=(float)Get("score");Call("CheckMissions");Check((float)Get("score")==missionScore+300,"collection mission gives its one-time reward");
                Call("CheckMissions");Check((float)Get("score")==missionScore+300,"mission reward cannot be farmed repeatedly");
                Call("SelectTrack",2);Call("StartRun");Check(Mathf.RoundToInt((float)Get("finish"))==470 && UnityEngine.Object.FindObjectsByType<Lab2.SnowPickup>().Length>60,"Glacier route is longer and denser than Ridge Run");
                Call("SelectTrack",3);Call("StartRun");Check(Mathf.RoundToInt((float)Get("finish"))==700,"Summit route is the longest mountain");
                Call("SelectTrack",1);Call("SelectMode",false);Call("StartRun");
                body=(Rigidbody2D)Get("body");body.position=new Vector2(12,Lab2.SnowBoarderGame.Height(12)+30);body.rotation=0;body.angularVelocity=0;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
                Set("previousAngle",0f);Set("spin",0f);Set("airFlips",0);Set("airTime",0f);Set("wasGrounded",true);Set("combo",0);Set("comboUntil",0f);
                Set("spin",1080f);Call("Update");
                Check((int)Get("combo")==3 && (float)Get("score")>=2250,"three in-air rotations build the combo mid-flight with a chain bonus");
                Call("StartRun");
                Call("SelectMode",true);Call("StartRun");Set("timer",40f);Call("Update");Check(Get("page").ToString()=="Result" && !(bool)Get("won"),"Time Attack ends when its 40-second budget expires");
                Call("StartRun");Call("EndRun",true);Check(PlayerPrefs.GetInt("Snow.Best.TimeAttack",0)>0,"Time Attack saves a separate personal best");
                Call("StartRun");
                foreach(var item in UnityEngine.Object.FindObjectsByType<Lab2.SnowPickup>())if(item.kind==Lab2.SnowPickup.Kind.Shield){game.Collect(item);break;}
                ((Rigidbody2D)Get("body")).simulated=false;
                stage=4;began=EditorApplication.timeSinceStartup;return;
            }
            if(stage==4 && t>1){
                Check(UnityEngine.Object.FindObjectsByType<Lab2.SnowComboPulse>(FindObjectsSortMode.None).Length==0,"restarting a run clears leftover combo shockwaves");
                ScreenCapture.CaptureScreenshot("Documentation/ShieldAndHUD.png");stage=5;began=EditorApplication.timeSinceStartup;return;
            }
            if(stage==5 && t>1){
                previewRoute=1;CaptureRoute();stage=6;began=EditorApplication.timeSinceStartup;return;
            }
            if(stage==6 && t>1){
                ScreenCapture.CaptureScreenshot("Documentation/Route"+previewRoute+".png");stage=7;began=EditorApplication.timeSinceStartup;return;
            }
            if(stage==7 && t>1){
                if(previewRoute<3){previewRoute++;CaptureRoute();stage=6;began=EditorApplication.timeSinceStartup;return;}
                Call("Menu");stage=8;began=EditorApplication.timeSinceStartup;return;
            }
            if(stage==8 && t>1){
                File.WriteAllText("Documentation/Validation.txt",checks+" checks passed in Unity "+Application.unityVersion+"\nMenu, gameplay, shield/HUD and all three landscape screenshots captured.\n");
                Debug.Log("LAB2 VALIDATION SUCCESS: "+checks+" checks"); PlayerPrefs.SetInt("Snow.Best",SessionState.GetInt("Lab2.PreviousBest",0)); PlayerPrefs.SetInt("Snow.Best.TimeAttack",SessionState.GetInt("Lab2.PreviousTimeBest",0)); PlayerPrefs.Save(); SessionState.SetBool("Lab2.Validate",false); EditorApplication.Exit(0);stage=9;
            }
            if(t>60)throw new Exception("Validation timed out");
        } catch(Exception e) {Debug.LogException(e);PlayerPrefs.SetInt("Snow.Best",SessionState.GetInt("Lab2.PreviousBest",0)); PlayerPrefs.SetInt("Snow.Best.TimeAttack",SessionState.GetInt("Lab2.PreviousTimeBest",0)); PlayerPrefs.Save(); SessionState.SetBool("Lab2.Validate",false); EditorApplication.Exit(1);}
    }
}











