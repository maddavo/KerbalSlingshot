using System;
using System.Linq;
using System.Threading;
using KerbalSlingshot.Core;
using KSP.UI.Screens;
using UnityEngine;

namespace KerbalSlingshot.KSP
{
    public sealed partial class PlannerPlugin
    {
        private ApplicationLauncherButton? toolbarButton;
        private Texture2D? toolbarIcon;
        private Texture2D? panelTexture;
        private bool clearFocus;
        private bool manualOpen,advancedOpen,detailsOpen,estimateReady;
        private string estimateSource="No estimate imported";
        private PlannerResultCard? resultCard;
        private Vector2 advancedScroll,detailsScroll;
        private GUIStyle? labelStyle,headingStyle,fieldStyle,buttonStyle,noteStyle,windowStyle;
        private static readonly string[] AdvancedKeys={"Terrain ceiling km","Assist min km","Clearance km","Pe tolerance m",
            "Departure +/- s","Journey limit s","Delta-v limit m/s","Scan step s","Scan steps","Evaluations",
            "Time step s","DV step m/s","Wall budget s"};
        private static readonly string[] AdvancedLabels={"Terrain ceiling, all bodies (km)","Minimum assist altitude (km)","Clearance margin (km)","Periapsis tolerance (m)",
            "Departure window +/- (s)","Journey limit (s)","Departure delta-v limit (m/s)","Maximum scan step (s)","Maximum scan steps","Evaluation limit",
            "Refinement time step (s)","Refinement DV step (m/s)","Wall-time budget (s)"};

        private void EnsureToolbar()
        {
            if (!ApplicationLauncher.Ready || ApplicationLauncher.Instance==null || toolbarButton!=null) return;
            if (toolbarIcon==null)
            {
                // Authored procedural planet/flyby icon; no external art or distributed Unity assets.
                toolbarIcon=new Texture2D(38,38,TextureFormat.RGBA32,false);
                var pixels=new Color[38*38];
                for (int y=0;y<38;y++) for (int x=0;x<38;x++)
                {
                    double radius=Math.Sqrt((x-19)*(x-19)+(y-19)*(y-19));
                    bool planet=radius<=6,arc=radius>=12 && radius<=14 && !(x>23 && y>18),tip=x>=26 && x<=32 && Math.Abs(y-27)<=2;
                    pixels[y*38+x]=planet?new Color(.3f,.8f,1,1):arc || tip?Color.white:Color.clear;
                }
                toolbarIcon.SetPixels(pixels); toolbarIcon.Apply();
            }
            toolbarButton=ApplicationLauncher.Instance.AddModApplication(()=>visible=true,()=> { visible=false; clearFocus=true; InputLockManager.RemoveControlLock(InputLock); },
                null,null,null,null,ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW,toolbarIcon);
        }
        private void RemoveToolbar()
        {
            if (toolbarButton!=null && ApplicationLauncher.Instance!=null) ApplicationLauncher.Instance.RemoveModApplication(toolbarButton);
            toolbarButton=null;
            visible=false; clearFocus=true; InputLockManager.RemoveControlLock(InputLock);
        }
        private void Styles()
        {
            if (labelStyle!=null) return;
            panelTexture=new Texture2D(1,1,TextureFormat.RGBA32,false);
            panelTexture.SetPixel(0,0,new Color(.055f,.075f,.095f,.97f)); panelTexture.Apply();
            windowStyle=new GUIStyle(GUI.skin.window) { fontSize=14,fontStyle=FontStyle.Bold };
            foreach (GUIStyleState state in new[]{windowStyle.normal,windowStyle.hover,windowStyle.active,windowStyle.focused,
                windowStyle.onNormal,windowStyle.onHover,windowStyle.onActive,windowStyle.onFocused})
            { state.background=panelTexture; state.textColor=Color.white; }
            labelStyle=new GUIStyle(GUI.skin.label) { fontSize=14,wordWrap=false,alignment=TextAnchor.MiddleLeft };
            labelStyle.normal.textColor=new Color(.94f,.96f,.98f);
            headingStyle=new GUIStyle(labelStyle) { fontStyle=FontStyle.Bold,fontSize=14 };
            headingStyle.normal.textColor=new Color(.5f,.85f,1);
            noteStyle=new GUIStyle(labelStyle) { fontSize=12,wordWrap=true,alignment=TextAnchor.UpperLeft };
            noteStyle.normal.textColor=new Color(.8f,.84f,.88f);
            fieldStyle=new GUIStyle(GUI.skin.textField) { fontSize=14,alignment=TextAnchor.MiddleLeft };
            fieldStyle.normal.textColor=Color.white; fieldStyle.focused.textColor=Color.white;
            fieldStyle.active.textColor=Color.white; fieldStyle.hover.textColor=Color.white;
            buttonStyle=new GUIStyle(GUI.skin.button) { fontSize=13,alignment=TextAnchor.MiddleCenter };
        }
        public void OnGUI()
        {
            if (clearFocus) { GUI.FocusControl(""); clearFocus=false; }
            if (!HighLogic.LoadedSceneIsFlight || !visible) { InputLockManager.RemoveControlLock(InputLock); return; }
            GUISkin previous=GUI.skin;
            try
            {
                GUI.skin=HighLogic.Skin; Styles();
                window.width=PlannerLayout.Width;
                window.height=PlannerLayout.Height(manualOpen,advancedOpen,detailsOpen,Screen.height);
                window.x=Math.Max(0,Math.Min(window.x,Screen.width-window.width));
                window.y=Math.Max(28,Math.Min(window.y,Screen.height-window.height));
                window=GUI.Window(GetInstanceID(),window,DrawWindow,"KerbalSlingshot 0.2.1",windowStyle!);
                if (window.Contains(Event.current.mousePosition) || GUI.GetNameOfFocusedControl().StartsWith("slingshot:",StringComparison.Ordinal))
                    InputLockManager.SetControlLock(ControlTypes.ALL_SHIP_CONTROLS,InputLock);
                else InputLockManager.RemoveControlLock(InputLock);
            }
            finally { GUI.skin=previous; }
        }
        private void Label(float x,float y,float width,string text,bool heading=false)
            => GUI.Label(new Rect(x,y,width,22),text,heading?headingStyle!:labelStyle!);
        private void Note(float y,string text,float height=28)
            => GUI.Label(new Rect(14,y,492,height),text,noteStyle!);
        private bool Button(float x,float y,float width,string text,string tooltip="")
        {
            bool pressed=GUI.Button(new Rect(x,y,width,26),new GUIContent(text,tooltip),buttonStyle!);
            if (pressed) GUI.FocusControl("");
            return pressed;
        }
        private void Field(float y,string key,string label,string unit="")
        {
            Label(14,y,210,label);
            EditField(new Rect(228,y,168,24),key);
            Label(404,y,96,unit);
        }
        private void EditField(Rect rect,string key)
        {
            GUI.SetNextControlName("slingshot:"+key);
            string next=GUI.TextField(rect,fields[key],fieldStyle!);
            if (next==fields[key]) return;
            fields[key]=next;
            if (key=="Departure UT" || key=="Radial m/s" || key=="Normal m/s" || key=="Prograde m/s")
            { estimateReady=true; estimateSource="Manual estimate"; }
            if (key=="Terrain ceiling km") terrainConfirmed=false;
            Invalidate("Input changed; recalculate.");
        }
        private void Target(float y,string name,ref int selected)
        {
            Label(14,y,210,name);
            if (bodyIds.Length==0) { Label(228,y,264,"No supported bodies"); return; }
            int next=selected;
            if (Button(228,y,28,"<")) next=(selected+bodyIds.Length-1)%bodyIds.Length;
            GUI.Label(new Rect(262,y,202,26),new GUIContent(PlannerResultCard.Short(bodyIds[selected],23),bodyIds[selected]),labelStyle!);
            if (Button(474,y,28,">")) next=(selected+1)%bodyIds.Length;
            if (next!=selected) { selected=next; Invalidate("Target changed; recalculate."); }
        }
        private string Readiness(out bool valid)
        {
            valid=false;
            try { PlannerSettings.Parse(fields); valid=true; } catch (ArgumentException ex) { return ex.Message; }
            if (bodyIds.Length<2) return "Read a vessel with two supported sibling targets.";
            if (assistIndex==destinationIndex) return "Choose two different target bodies.";
            if (!estimateReady) return "Import an existing node, or open Manual estimate.";
            if (!terrainConfirmed) return "Open Advanced and review the required safety settings.";
            return "Evaluate checks this burn; Refine searches nearby burns.";
        }

        private void DrawWindow(int id)
        {
            bool busy=job!=null;
            string readiness=Readiness(out bool valid);
            var controls=new PlannerButtons(busy,bodyIds.Length>=2,assistIndex!=destinationIndex,estimateReady,terrainConfirmed,valid);
            GUI.enabled=controls.Edit;
            GUI.Label(new Rect(14,28,382,22),new GUIContent(PlannerResultCard.Short(context,48),context),noteStyle!);
            if (Button(406,26,96,"Refresh")) Try(ReadContext);
            Label(14,54,492,"Targets",true);
            Target(78,"Gravity Assist Target",ref assistIndex);
            Target(108,"Intercept Target",ref destinationIndex);
            Field(140,"Intercept Pe km","Destination periapsis","km");
            Label(14,174,492,"Starting estimate",true);
            GUI.enabled=controls.Import;
            if (Button(14,198,280,"Import existing node (recommended)")) Try(ImportNode);
            GUI.enabled=controls.Edit;
            if (Button(306,198,196,manualOpen?"Hide manual estimate":"Manual estimate"))
            {
                manualOpen=!manualOpen;
                if (manualOpen && !estimateReady) { estimateReady=true; estimateSource="Manual estimate"; }
            }
            string estimate=estimateSource;
            if (estimateReady && double.TryParse(fields["Departure UT"],System.Globalization.NumberStyles.Float,Invariant,out double ut))
                estimate+=" | UT "+PlannerResultCard.Time(ut);
            Note(230,estimate,20);
            int offset=manualOpen?PlannerLayout.ManualHeight:0;
            if (manualOpen)
            {
                Field(252,"Departure UT","Burn time","UT seconds");
                Label(14,280,492,"Native burn components (m/s): radial / normal / prograde");
                EditField(new Rect(14,304,154,24),"Radial m/s");
                EditField(new Rect(180,304,154,24),"Normal m/s");
                EditField(new Rect(346,304,156,24),"Prograde m/s");
            }
            GUI.enabled=true;
            Label(14,254+offset,492,"Calculate",true);
            GUI.enabled=controls.Calculate;
            if (Button(14,278+offset,158,"Evaluate estimate","Check the exact starting estimate")) Try(()=>Begin(false));
            if (Button(182,278+offset,158,"Refine estimate","Search bounded nearby burns")) Try(()=>Begin(true));
            GUI.enabled=controls.Cancel;
            if (Button(350,278+offset,152,"Cancel")) Invalidate("Cancelled; no nodes changed.");
            GUI.enabled=true;
            Note(308+offset,busy?"Calculating — inputs locked; Cancel remains available.":readiness,32);
            DrawResult(offset,busy);
            float y=574+offset;
            if (Button(14,y,488,(advancedOpen?"- ":"+ ")+"Advanced"+(terrainConfirmed?" — safety reviewed":" — safety review required")))
            { advancedOpen=!advancedOpen; if (advancedOpen) detailsOpen=false; }
            y+=32;
            if (advancedOpen) { DrawAdvanced(y); y+=PlannerLayout.AdvancedHeight; }
            if (Button(14,y,488,(detailsOpen?"- ":"+ ")+"Details & KSP comparison"))
            { detailsOpen=!detailsOpen; if (detailsOpen) advancedOpen=false; }
            y+=32;
            if (detailsOpen)
            {
                float available=Math.Max(50,Math.Min(PlannerLayout.DetailsHeight,window.height-y-38));
                GUI.enabled=!busy;
                if (Button(14,y,235,"Read existing KSP patches")) Try(ReadPatches);
                GUI.enabled=true;
                if (Button(260,y,242,"Save diagnostics")) Try(WriteDiagnostics);
                Rect area=new Rect(14,y+32,488,available-34);
                string details="Build "+Build+"\n"+context+"\n"+status+"\n"+report+"\n"+patchReport;
                float height=Math.Max(area.height,noteStyle!.CalcHeight(new GUIContent(details),460));
                detailsScroll=GUI.BeginScrollView(area,detailsScroll,new Rect(0,0,460,height));
                GUI.Label(new Rect(0,0,460,height),details,noteStyle); GUI.EndScrollView();
                y+=available;
            }
            Note(window.height-30,"Shared-parent routes only. No node creation or flight changes.",24);
            GUI.DragWindow(new Rect(0,0,window.width-20,24));
        }
        private void DrawResult(int offset,bool busy)
        {
            float top=PlannerLayout.ResultTop+offset;
            string heading=resultCard?.Heading ?? PlannerResultCard.Short(status,55);
            if (busy && job!=null)
            {
                Progress progress=Volatile.Read(ref job.Progress);
                heading="Calculating: "+progress.Count+" / "+job.Settings.Budget;
            }
            Color previous=GUI.color;
            GUI.color=busy?new Color(.7f,.86f,1):resultCard?.Accepted==true?new Color(.7f,1,.86f):new Color(1,.91f,.68f);
            GUI.Box(new Rect(14,top,488,42),GUIContent.none);
            GUI.Label(new Rect(24,top+1,468,22),new GUIContent(heading,status),headingStyle!);
            GUI.color=previous;
            GUI.Label(new Rect(24,top+23,468,18),resultCard?.Validation ?? "Unvalidated — compare predictions with KSP",noteStyle!);
            float y=top+48;
            Label(14,y,270,"Departure: "+(resultCard?.Departure ?? "--"));
            Label(292,y,210,"Delta-v: "+(resultCard?.DeltaV ?? "--"));
            y+=24;
            Label(14,y,204,"Predicted event",true); Label(224,y,154,"Time (UT s)",true); Label(402,y,100,"Alt. (km)",true);
            y+=22;
            string[] labels={"Assist entry","Assist periapsis","Assist exit","Destination entry","Destination periapsis"};
            for (int i=0;i<5;i++)
            {
                PlannerEventRow? row=resultCard?.Events[i];
                Label(14,y,204,row?.Label ?? labels[i]); Label(224,y,174,row?.Time ?? "--"); Label(402,y,100,row?.Altitude ?? "--"); y+=22;
            }
            Label(14,y+2,488,"Pe error: "+(resultCard?.Error ?? "--"));
        }
        private void DrawAdvanced(float y)
        {
            float available=Math.Min(PlannerLayout.AdvancedHeight,Math.Max(120,window.height-y-100));
            Rect area=new Rect(14,y,488,available-6);
            advancedScroll=GUI.BeginScrollView(area,advancedScroll,new Rect(0,0,460,AdvancedKeys.Length*28+70));
            bool previous=GUI.enabled; GUI.enabled=job==null;
            for (int i=0;i<AdvancedKeys.Length;i++)
            {
                float row=i==0?0:88+(i-1)*28;
                GUI.Label(new Rect(0,row,280,24),AdvancedLabels[i],labelStyle!);
                EditField(new Rect(286,row,166,24),AdvancedKeys[i]);
            }
            GUI.Label(new Rect(0,28,460,24),"Conservative ceiling for the parent and every child body.",noteStyle!);
            bool confirmed=GUI.Toggle(new Rect(0,54,460,26),terrainConfirmed,"I checked this conservative terrain ceiling");
            if (confirmed!=terrainConfirmed) { terrainConfirmed=confirmed; Invalidate("Safety assumption changed; recalculate."); }
            GUI.enabled=previous; GUI.EndScrollView();
        }
    }
}
