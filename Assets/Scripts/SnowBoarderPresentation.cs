using System.Collections.Generic;
using UnityEngine;
namespace Lab2 {
public sealed partial class SnowBoarderGame {
    struct WorldPalette {
        public Color sky, far, near, earth, snow, accent;
        public string setting;
        public WorldPalette(string sky,string far,string near,string earth,string snow,string accent,string setting) {
            this.sky=Hex(sky);this.far=Hex(far);this.near=Hex(near);this.earth=Hex(earth);this.snow=Hex(snow);this.accent=Hex(accent);this.setting=setting;
        }
    }
    static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out Color color);return color;}
    static WorldPalette Palette(int route) {
        switch(route){
            case 2:return new WorldPalette("ADDDEB","D6EFF3","75A8C0","386F92","EDFDFE","70DDEA","BLUE ICE VALLEY");
            case 3:return new WorldPalette("202D4A","52607F","344765","273851","D2DFEE","B9ACF5","AURORA NIGHT");
            default:return new WorldPalette("EADBC8","D1CCBF","AAB9AF","718C89","FFF7EC","F0BE7B","ALPINE SUNRISE");
        }
    }
    Transform atmosphere;
    readonly List<Texture2D> presentationTextures=new List<Texture2D>();
    Texture2D[] previews;
    GUIStyle hero,cardTitle,caption,quietButton,centered,numberStyle,comboStyle;
    bool missionExpanded;
    Color UIAccent {get{return Palette(track).accent;}}
    static readonly Color Canvas=Hex("0D1828"),Card=Hex("18283A"),Muted=Hex("9AAFBF"),Light=Hex("F2F5F7"),Warning=Hex("FFB151");

    void BuildBackdrop(float[] routeTrees) {
        var colors=Palette(track);
        atmosphere=new GameObject("Atmosphere - "+colors.setting).transform;objects.Add(atmosphere.gameObject);
        if(track==3){
            Ring("Moon",atmosphere,.82f,0,Hex("E2ECF5"),-44);
            atmosphere.Find("Moon").localPosition=new Vector3(9,4.3f,0);
            for(int i=0;i<42;i++){
                var star=Art("Night star","",Vector2.zero,.025f+(i%3)*.012f,-46,new Color(.8f,.9f,1,.75f));
                star.transform.SetParent(atmosphere,false);star.transform.localPosition=new Vector3(Mathf.Repeat(i*6.731f,36)-18,Mathf.Repeat(i*2.193f,9)-1,0);
            }
            for(int band=0;band<3;band++){
                var points=new List<Vector2>();
                for(int i=-22;i<=22;i++){
                    float y=3+band*.5f+Mathf.Sin(i*.18f+band)*1.1f;
                    points.Add(new Vector2(i,y));points.Add(new Vector2(i,y+.45f+Mathf.Sin(i*.2f)*.15f));
                }
                Strip("Aurora band "+band,points,band==1?new Color(.57f,.46f,.84f,.14f):new Color(.2f,.84f,.68f,.14f),-43);
                objects[objects.Count-1].transform.SetParent(atmosphere,false);
            }
        } else {
            Ring("Sun",atmosphere,track==1?1.1f:.8f,0,track==1?Hex("FFF0C9"):Hex("E9FBFE"),-44);
            atmosphere.Find("Sun").localPosition=new Vector3(9,4.8f,0);
        }
        int end=Mathf.CeilToInt(finish)+50;
        for(int layer=0;layer<2;layer++){
            float spacing=layer==0?29:21;
            var points=new List<Vector2>();
            for(float x=-55;x<=end+55;x+=spacing){
                float peak=Ground(x)+(layer==0?10:5)+Mathf.Sin(x*.093f+layer)*2;
                points.Add(new Vector2(x-spacing*.5f,Ground(x)-4));points.Add(new Vector2(x-spacing*.5f,Ground(x)-35));
                points.Add(new Vector2(x,peak));points.Add(new Vector2(x,Ground(x)-35));
            }
            Strip(layer==0?"Far mountain silhouette":"Near mountain silhouette",points,layer==0?colors.far:colors.near,layer==0?-30:-24);
        }
        for(int i=0;i<Mathf.CeilToInt(end/24f);i++){
            float x=i*24+6;
            if(track!=3)Art("Atmospheric cloud","Sprite_Cloud_"+(i%2==0?"5":"6"),new Vector2(x,Ground(x)+8),track==1?8:12,-32,new Color(1,1,1,track==1?.36f:.21f));
            if(track==2){
                Strip("Glacier spire",new List<Vector2>{new Vector2(x-2,Ground(x)),new Vector2(x-2,Ground(x)-4),new Vector2(x,Ground(x)+4+i%3),new Vector2(x,Ground(x)-4),new Vector2(x+1.5f,Ground(x)),new Vector2(x+1.5f,Ground(x)-4)},new Color(.64f,.84f,.9f,.6f),-18);
            } else if(!NearAny(x,routeTrees,11) && (track==1 || i%2==0)){
                var color=colors.near;color.a=track==1?.28f:.16f;
                var pine=Art("Decorative pine - no collision","Snow-Tree-1",new Vector2(x,Ground(x)+2),track==1?2.7f:2.2f,-18,color);
                pine.transform.position=new Vector3(x,Ground(x)+pine.GetComponent<SpriteRenderer>().bounds.extents.y-.25f,0);
            }
        }
        var fog=Art("Background haze","",Vector2.zero,1,-12,new Color(colors.sky.r,colors.sky.g,colors.sky.b,track==3?.12f:.2f));
        fog.transform.SetParent(atmosphere,false);fog.transform.localPosition=new Vector3(0,-4,0);fog.transform.localScale=new Vector3(50,6,1);
    }
    void MarkTreeHazard(GameObject tree,float x) {
        float y=Ground(x);
        // Grounded hazard markings only: a trunk band and a footprint on the snow.
        // All markers are decorative; the existing trunk collider remains authoritative.
        var root=new GameObject("Tree warning markers");objects.Add(root);root.transform.SetParent(tree.transform,true);
        for(int row=0;row<2;row++){
            var tape=Art("Orange trunk band","",new Vector2(x,y+.26f+row*.24f),1,8,Warning);
            tape.transform.localScale=new Vector3(.7f,.14f,1);tape.transform.SetParent(root.transform,true);
            for(int stripe=0;stripe<3;stripe++){
                var dark=Art("Hazard stripe","",new Vector2(x-.23f+stripe*.23f,y+.26f+row*.24f),1,9,Canvas);
                dark.transform.localScale=new Vector3(.07f,.14f,1);dark.transform.rotation=Quaternion.Euler(0,0,-20);dark.transform.SetParent(root.transform,true);
            }
        }
        var footprint=Art("Tree danger footprint","",new Vector2(x,y+.05f),1,8,Warning);footprint.transform.localScale=new Vector3(1.45f,.065f,1);footprint.transform.SetParent(root.transform,true);
    }
    void MarkRockHazard(GameObject rock,float x) {
        // Rocks share the same grounded footprint language so both hazards read the same way.
        float y=Ground(x);
        var root=new GameObject("Rock hazard footprint");objects.Add(root);root.transform.SetParent(rock.transform,true);
        var footprint=Art("Rock danger footprint","",new Vector2(x,y+.05f),1,7,Warning);footprint.transform.localScale=new Vector3(1.35f,.06f,1);footprint.transform.SetParent(root.transform,true);
    }
    void ReleasePresentation(){foreach(var texture in presentationTextures)if(texture)Destroy(texture);}
    Texture2D Preview(int route) {
        const int width=384,height=174;
        var texture=new Texture2D(width,height);texture.wrapMode=TextureWrapMode.Clamp;
        var colors=Palette(route);var pixels=new Color[width*height];
        for(int x=0;x<width;x++)for(int y=0;y<height;y++){
            float u=x/(float)width,v=y/(float)height;
            Color c=Color.Lerp(colors.near,colors.sky,v);
            float far=.34f+Mathf.Abs(Mathf.Sin(u*8+route))* .36f;
            float near=.10f+Mathf.Abs(Mathf.Sin(u*11+2))* .33f;
            if(v<far)c=colors.far;if(v<near)c=colors.near;
            float ground=.12f-u*.075f+.027f*Mathf.Sin(u*8);
            if(v<ground)c=colors.earth;else if(v<ground+.025f)c=colors.snow;
            float dx=(u-.82f)*width,dy=(v-.78f)*height;
            if(dx*dx+dy*dy<144)c=route==1?Hex("FFF0C9"):Hex("E8F6FF");
            if(route==3 && v>.58f){
                float aurora=.75f+Mathf.Sin(u*9)*.075f;
                if(v>aurora && v<aurora+.07f)c=Color.Lerp(c,Hex("65C2A7"),.37f);
                if((x*19+y*31)%887==0)c=Light;
            }
            pixels[y*width+x]=c;
        }
        texture.SetPixels(pixels);texture.Apply();presentationTextures.Add(texture);return texture;
    }
    void Styles() {
        if(title!=null)return;
        mint=RoundedTexture(Color.white);panel=Solid(Canvas);
        title=new GUIStyle(GUI.skin.label){fontSize=50,fontStyle=FontStyle.Bold,normal={textColor=Light}};
        hero=new GUIStyle(title){fontSize=42};heading=new GUIStyle(title){fontSize=27};cardTitle=new GUIStyle(title){fontSize=23};
        text=new GUIStyle(GUI.skin.label){fontSize=18,wordWrap=true,normal={textColor=Light}};
        small=new GUIStyle(text){fontSize=14,normal={textColor=Muted}};caption=new GUIStyle(small){fontSize=12};
        centered=new GUIStyle(text){alignment=TextAnchor.MiddleCenter};numberStyle=new GUIStyle(heading){fontSize=28};
        comboStyle=new GUIStyle(cardTitle){fontSize=24,alignment=TextAnchor.MiddleCenter};
        button=new GUIStyle(GUI.skin.button){fontSize=17,fontStyle=FontStyle.Bold,border=new RectOffset(9,9,9,9),normal={background=mint,textColor=Canvas},hover={background=mint,textColor=Canvas},active={background=mint,textColor=Canvas}};
        quietButton=new GUIStyle(button){fontSize=14,normal={background=mint,textColor=Light},hover={background=mint,textColor=Light},active={background=mint,textColor=Light}};
        previews=new[]{Preview(1),Preview(2),Preview(3)};
    }
    void Fill(Rect rect,Color color,float radius=0){GUI.DrawTexture(rect,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,radius);}
    void Box(float x,float y,float w,float h){Fill(new Rect(x,y,w,h),new Color(Card.r,Card.g,Card.b,.96f),14);}
    void Label(float x,float y,float w,float h,string value,GUIStyle style){GUI.Label(new Rect(x,y,w,h),value,style);}
    void TintLabel(float x,float y,float w,float h,string value,GUIStyle style,Color color){var old=GUI.color;var previous=style.normal.textColor;GUI.color=Color.white;style.normal.textColor=color;GUI.Label(new Rect(x,y,w,h),value,style);style.normal.textColor=previous;GUI.color=old;}
    bool Action(Rect rect,string value,bool primary=false){var old=GUI.backgroundColor;GUI.backgroundColor=primary?UIAccent:Hex("25384C");bool clicked=GUI.Button(rect,value,primary?button:quietButton);GUI.backgroundColor=old;return clicked;}
    bool Button(float x,float y,float w,string value){return Action(new Rect(x,y,w,48),value,true);}
    bool Button(float x,float y,float w,float h,string value){return Action(new Rect(x,y,w,h),value,true);}
    void Shade(){Fill(new Rect(0,0,1280,720),new Color(.025f,.05f,.09f,.78f));}
    void MenuUI() {
        // A single translucent left panel, so the live mountain and the rider stay visible on the right.
        const float panel=456f;
        Fill(new Rect(0,0,panel,720),new Color(Canvas.r,Canvas.g,Canvas.b,.9f));
        Fill(new Rect(panel,0,5,720),UIAccent);
        Fill(new Rect(0,0,panel,6),UIAccent);
        Label(36,34,380,22,"ALPINE ARCADE   /   LAB 02",small);
        Label(33,60,404,54,"SNOW BOARDER",hero);
        Label(36,122,400,26,"Find your next descent.",small);
        Label(36,168,400,22,"CHOOSE A ROUTE",caption);
        for(int i=0;i<3;i++){
            float y=196+i*62;bool selected=track==i+1;var colors=Palette(i+1);
            Fill(new Rect(28,y,392,56),selected?colors.accent:Hex("23364A"),13);
            Fill(new Rect(30,y+2,388,52),Card,11);
            GUI.DrawTexture(new Rect(40,y+9,66,38),previews[i],ScaleMode.StretchToFill);
            TintLabel(116,y+8,206,28,RouteName[i],selected?cardTitle:small,selected?Light:Muted);
            TintLabel(116,y+31,206,20,RouteTag[i],selected?small:caption,selected?colors.accent:Muted);
            if(selected)TintLabel(330,y+8,76,22,"SELECTED",caption,colors.accent);
            if(GUI.Button(new Rect(28,y,392,56),"",GUIStyle.none))SelectTrack(i+1);
        }
        Label(36,392,400,22,"YOUR RUN STYLE",caption);
        if(Action(new Rect(28,420,186,46),"FREE RIDE",!timeAttack))SelectMode(false);
        if(Action(new Rect(234,420,186,46),"TIME ATTACK",timeAttack))SelectMode(true);
        Label(36,478,384,44,timeAttack?"Beat the clock: reach the finish in "+RouteTime[track-1].ToString("0")+" seconds.":"Ride at your own pace. Collect flakes and land tricks.",small);
        if(Action(new Rect(28,530,392,58),"RIDE THIS MOUNTAIN  >",true))StartRun();
        string key=track==1?"Snow.Best":"Snow.Best.L"+track;if(timeAttack)key+=".TimeAttack";
        Label(36,600,384,24,"BEST SCORE ON THIS ROUTE   "+PlayerPrefs.GetInt(key,0).ToString("N0"),small);
        if(Action(new Rect(28,640,128,36),"HOW TO PLAY"))page=Page.Help;
        if(Action(new Rect(164,640,110,36),"SETTINGS")){fromPause=false;page=Page.Options;}
        if(Action(new Rect(282,640,138,36),"QUIT"))Quit();
        // Small caption over the live scene so the backdrop reads as part of the menu.
        Fill(new Rect(1004,660,260,44),new Color(Canvas.r,Canvas.g,Canvas.b,.72f),12);
        TintLabel(1020,668,150,28,RouteName[track-1],small,Light);
        Label(1020,688,236,20,Palette(track).setting,caption);
    }
    void Controls(float x,float y,float width){Label(x,y,width,140,"W / UP    Push and accelerate\nS / DOWN    Brake\nA / D    Controlled rotation\nSHIFT + A / D    Fast air trick\nSPACE    Jump    ESC    Pause    R    Restart",text);}
    void HelpUI(){
        Shade();Box(220,94,840,540);Label(260,128,760,50,"Make every landing count.",heading);
        Label(260,198,340,26,"ON YOUR BOARD",small);Controls(260,240,340);
        Label(660,198,350,26,"READ THE MOUNTAIN",small);
        Label(660,241,350,170,"Orange trunk bands and a footprint mark tree and rock obstacles.\nFaint trees behind the slope are scenery.\nLand on top of rocks to ride them. Side contact causes a crash.\nA shield absorbs one hit, then it is gone.",text);
        Label(260,437,750,64,"Collect flakes, land full flips and finish with two lives to complete your run missions.",small);
        if(Action(new Rect(260,550,750,48),"BACK TO ROUTES",true))page=Page.Menu;
    }
    void OptionsUI(){
        Shade();Box(355,66,570,614);Label(395,104,490,47,"Ride your way.",heading);
        Label(395,180,490,31,"AUDIO   "+Mathf.RoundToInt(volume*100)+"%",small);
        volume=GUI.HorizontalSlider(new Rect(397,229,484,24),volume,0,1);
        if(Action(new Rect(395,278,236,48),"MUSIC   "+(musicOn?"ON":"OFF")))musicOn=!musicOn;
        if(Action(new Rect(647,278,236,48),"SNOW   "+(snowfall?"ON":"OFF")))snowfall=!snowfall;
        RefreshMusic();
        Label(395,345,490,31,"BACKGROUND MUSIC   "+RouteMusic[track-1].ToUpperInvariant(),small);
        Label(395,399,490,31,"ROTATION SENSITIVITY   "+rotationSensitivity.ToString("0.00"),small);
        rotationSensitivity=GUI.HorizontalSlider(new Rect(397,449,484,24),rotationSensitivity,.65f,1.2f);
        Label(395,493,490,59,"Lower values make landings easier. Hold Shift for faster tricks; release A / D to stop spinning.",small);
        if(Action(new Rect(395,591,490,48),"SAVE AND BACK",true)){PlayerPrefs.SetFloat("Snow.Steering",rotationSensitivity);PlayerPrefs.SetFloat("Snow.Volume",volume);PlayerPrefs.SetInt("Snow.Weather",snowfall?1:0);PlayerPrefs.SetInt("Snow.Music",musicOn?1:0);PlayerPrefs.Save();ApplyMusic();page=fromPause?Page.Pause:Page.Menu;}
    }
    void PauseUI(){Shade();Box(397,104,486,512);Label(437,143,406,43,"A moment on the mountain.",cardTitle);Label(437,199,406,31,RouteName[track-1],small);if(Action(new Rect(437,256,406,56),"KEEP RIDING",true))Resume();if(Action(new Rect(437,328,406,48),"RESTART RUN"))StartRun();if(Action(new Rect(437,392,406,48),"SETTINGS")){fromPause=true;page=Page.Options;}if(Action(new Rect(437,456,406,48),"BACK TO ROUTES"))Menu();Label(437,544,406,30,"ESC also resumes your ride.",small);}
    void ResultUI(){
        Shade();Box(354,76,572,570);TintLabel(394,110,492,28,won?"DESCENT COMPLETE":"END OF RUN",small,UIAccent);
        Label(394,157,492,50,RouteName[track-1],heading);Label(394,215,492,32,won?"The mountain is yours.":reason,small);
        Label(394,278,492,60,Mathf.RoundToInt(score).ToString("N0"),title);Label(396,343,492,27,newRecord?"POINTS   /   NEW PERSONAL BEST":"POINTS",small);
        Label(394,401,492,55,"FLAKES   "+collected+" / "+flakesTotal+"      LIVES   "+lives+" / 3\nTIME   "+timer.ToString("0.0")+" s      BEST   "+best.ToString("N0"),small);
        if(Action(new Rect(394,496,492,52),"RIDE AGAIN",true))StartRun();if(Action(new Rect(394,563,492,44),"CHOOSE ANOTHER MOUNTAIN"))Menu();
    }
    void OnGUI(){
        Styles();float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
        GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-720*scale)/2),Quaternion.identity,Vector3.one*scale);
        if(page==Page.Menu){MenuUI();return;}
        if(page==Page.Help){HelpUI();return;}
        if(page==Page.Playing || page==Page.Pause || (page==Page.Options && fromPause))HUD();
        if(page==Page.Options)OptionsUI();else if(page==Page.Pause)PauseUI();else if(page==Page.Result)ResultUI();
    }
    void Bar(float x,float y,float width,float progress,Color color){Fill(new Rect(x,y,width,5),Hex("344759"),2);if(progress>0)Fill(new Rect(x,y,width*Mathf.Clamp01(progress),5),color,2);}
    void HUD(){
        Color accent=UIAccent;Box(24,20,1232,82);
        Label(46,34,180,21,"SCORE",caption);Label(46,58,182,36,Mathf.RoundToInt(score).ToString("N0"),numberStyle);
        Label(270,34,170,21,"SPEED",caption);Label(270,58,170,36,(body.linearVelocity.magnitude*3.6f).ToString("0")+" km/h",cardTitle);
        Label(475,34,230,21,"LIVES  /  FLAKES",caption);Label(475,63,230,30,lives+" / 3     "+collected+" / "+flakesTotal,text);
        float clock=timeAttack?Mathf.Max(0,timeLimit-timer):timer;
        Label(735,34,155,21,timeAttack?"TIME REMAINING":"RUN TIME",caption);Label(735,59,155,33,clock.ToString("0.0")+" s",cardTitle);
        TintLabel(930,35,162,28,RouteName[track-1],small,accent);Label(930,65,162,23,timeAttack?"TIME ATTACK":"FREE RIDE",caption);
        if(Action(new Rect(1113,38,120,46),"II  PAUSE") && page==Page.Playing)Pause();
        int completed=(missions[0]?1:0)+(missions[1]?1:0)+(missions[2]?1:0);
        if(Action(new Rect(24,116,236,37),"OBJECTIVES   "+completed+" / 3   "+(missionExpanded?"-":"+")) && page==Page.Playing)missionExpanded=!missionExpanded;
        if(missionExpanded){Box(24,163,236,107);Label(40,178,204,86,(missions[0]?"DONE":"OPEN")+"  Collect 10 flakes\n"+(missions[1]?"DONE":"OPEN")+"  Land a full flip\n"+(missions[2]?"DONE":"OPEN")+"  Finish with 2+ lives",small);}
        float effectY=574;
        if(shields>0){Box(24,effectY,236,61);TintLabel(40,effectY+10,204,28,"SHIELD   "+shields+" HIT"+(shields==1?"":"S")+" LEFT",text,accent);effectY-=72;}
        if(Time.time<boostUntil){EffectCard(effectY,"BOOST",boostUntil-Time.time,5,Warning);}
        if(combo>0 && Time.time<comboUntil){
            // The card pops the moment a new combo step lands, then settles back down.
            // Font stays inside the card: centred, and never large enough to clip "COMBO  x8".
            float flash=comboFlash;
            comboStyle.fontSize=24+(int)(flash*6);
            Box(1020,116,236,74);
            if(flash>0) Fill(new Rect(1020,116,236,74),new Color(1,1,1,flash*.45f),14);
            TintLabel(1032,124,212,36,"COMBO  x"+combo,comboStyle,flash>0?Light:UIAccent);
            Bar(1040,171,196,(comboUntil-Time.time)/(7f+(combo-1)*1.5f),Warning);
        }
        if(page==Page.Playing && !wasGrounded && airTime>.2f){Box(1020,202,236,76);Label(1040,216,196,20,"IN THE AIR",caption);Label(1040,242,196,30,Mathf.RoundToInt(Mathf.Abs(spin))+" deg  /  "+Mathf.FloorToInt(Mathf.Abs(spin)/360f)+" flips",small);}
        if(timeAttack && clock<10){Box(491,116,300,42);TintLabel(511,125,260,26,"FINAL "+Mathf.CeilToInt(clock)+" SECONDS",text,Warning);}
        if(Time.time<noticeUntil){float noticeY=timeAttack && clock<10?168:116;Box(332,noticeY,616,50);Label(350,noticeY+14,580,31,notice,small);}
        Box(24,652,1232,46);Label(44,665,194,24,Palette(track).setting,caption);Bar(248,675,766,body.position.x/finish,accent);Label(1050,665,183,25,Mathf.Max(0,Mathf.CeilToInt(finish-body.position.x))+" m to finish",small);
    }
    void EffectCard(float y,string name,float remaining,float duration,Color color){Box(24,y,236,61);TintLabel(40,y+10,204,28,name+"   "+remaining.ToString("0.0")+" s",text,color);Bar(40,y+45,204,remaining/duration,color);}
}
}



