using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class PinballExpansionValidation
{
    private const string Running = "BonkSquad.ExpansionValidation";
    private static EnemyWaveController run;
    private static CombatPresentation ui;
    private static int stage, frames;
    private static double deadline, waitUntil;
    private static float frozenTime, snareTime;
    private static NetProjectile projectile;
    private static GameObject blocker;
    private static EnemyWaveController.WaveDefinition[] waves;
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static PinballExpansionValidation() { if (SessionState.GetBool(Running,false)) EditorApplication.update += Tick; }
    [MenuItem("Tools/BonkSquad/Validate Pinball Expansion (Play Mode)")]
    public static void Run()
    {
        EditorSceneManager.OpenScene(Map01CombatSetup.ScenePath);
        SessionState.SetBool(Running,true); EditorApplication.EnterPlaymode();
    }
    private static void Check(bool pass, string message) { if (!pass) throw new Exception("EXPANSION_TEST_FAILED: " + message); }
    private static void Field(object target,string name,object value) => target.GetType().GetField(name,Private).SetValue(target,value);
    private static T Read<T>(object target,string name) => (T)target.GetType().GetField(name,Private).GetValue(target);
    private static void Invoke(object target,string name) => target.GetType().GetMethod(name,Private).Invoke(target,null);
    private static void Wait(int next,double seconds=.12) { stage=next; waitUntil=EditorApplication.timeSinceStartup+seconds; }
    private static void KillAll()
    {
        run.DropRandom=()=>.99d;
        foreach (var enemy in run.LivingEnemyObjects.ToArray())
            while (!enemy.IsDefeated) enemy.TryHitFromBall(run.Player.SpeedSettings.SmallEnemyKillSpeed,run.Player.SpeedSettings);
    }
    private static void Advance()
    {
        ui.DismissForValidation(); run.NextWave(); ui.ShowWavePreview(); ui.StartPresentedWave();
        foreach (var mortar in UnityEngine.Object.FindObjectsByType<MortarEnemy>(FindObjectsSortMode.None)) Field(mortar,"nextFire",float.PositiveInfinity);
    }
    private static void ValidateScore()
    {
        var score = new PinballScore(); int notifications=0; score.TokensEarned += n=>notifications+=n;
        score.RecordKill(false,0,false); score.RecordKill(false,1,false);
        Check(score.Points==100 && score.SkillTokens==1 && score.ImpactCharges==0 && notifications==1,"100 points grants one token, not automatic Impact");
        Check(score.BuyImpact() && score.SkillTokens==0 && score.ImpactCharges==1 && !score.BuyImpact(),"Impact choice spends exactly one token");
        var saved=score.Save(); score.RecordKill(false,2,false); score.RecordKill(false,3,false); score.Restore(saved);
        Check(score.Points==100 && score.SkillTokens==0 && score.ImpactCharges==1,"token/charge checkpoint restore");
        score.RegisterBounce(10); score.RecordKill(false,11,false);
        Check(score.Points==150 && score.ChainCount==0,"mortar defeat gives base points without ball combos");
        Check(score.ConsumeImpactCharge() && !score.ConsumeImpactCharge(),"Impact single use");
    }
    private static void ValidateSpawner(SpawnerEnemy spawner)
    {
        var enemy=spawner.GetComponent<EnemyBumper>();
        Field(spawner,"nextSpawn",Time.time); Invoke(spawner,"Update");
        Check(Read<bool>(spawner,"warningPending"),"spawner begins warning");
        enemy.TryHitFromBall(run.Player.SpeedSettings.SmallEnemyKillSpeed,run.Player.SpeedSettings);
        Check(enemy.HitPoints==1 && Read<bool>(spawner,"warningPending"),"surviving damage keeps warning and schedule");
        for (int i=0;i<2;i++)
        {
            Check(run.TryFindMinionSpawn(out Vector3 point),"safe child point");
            Field(spawner,"spawnPoint",point); Field(spawner,"warningPending",true); Field(spawner,"warningEnd",Time.time-.01f);
            Invoke(spawner,"Update");
        }
        Check(spawner.TotalSpawned==2,"damaged spawner produces children up to living cap");
        Field(spawner,"nextSpawn",Time.time); Invoke(spawner,"Update");
        Check(!Read<bool>(spawner,"warningPending") && spawner.TotalSpawned==2,"living cap prevents extra warnings");
        var children=Read<System.Collections.Generic.List<EnemyBumper>>(spawner,"children");
        children[0].TryHitFromBall(run.Player.SpeedSettings.SmallEnemyKillSpeed,run.Player.SpeedSettings);
        Check(run.TryFindMinionSpawn(out Vector3 lastPoint),"third child point");
        Field(spawner,"spawnPoint",lastPoint); Field(spawner,"warningPending",true); Field(spawner,"warningEnd",Time.time-.01f); Invoke(spawner,"Update");
        Check(spawner.TotalSpawned==3,"finite lifetime budget preserved after damage");
        Field(spawner,"nextSpawn",Time.time); Invoke(spawner,"Update"); Check(spawner.TotalSpawned==3,"exhausted budget cannot farm children");
        Field(spawner,"totalSpawnBudget",4); Field(spawner,"warningPending",true);
        var marker=run.CreateMarker("Minion incoming",lastPoint,1,Color.green); Field(spawner,"marker",marker);
        enemy.TryHitFromBall(run.Player.SpeedSettings.SmallEnemyKillSpeed,run.Player.SpeedSettings);
        Check(enemy.IsDefeated && !Read<bool>(spawner,"warningPending") && !marker.activeSelf,"defeat cancels pending warning immediately");
        Invoke(spawner,"Update"); Check(spawner.TotalSpawned==3,"no post-death spawn");
    }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (deadline==0) deadline=EditorApplication.timeSinceStartup+120;
            Check(EditorApplication.timeSinceStartup<deadline,"timeout at stage "+stage);
            if (ui==null) ui=UnityEngine.Object.FindFirstObjectByType<CombatPresentation>();
            if (ui==null || frames++<8) return;
            run=ui.GetComponent<EnemyWaveController>();
            if (EditorApplication.timeSinceStartup<waitUntil) return;
            switch(stage)
            {
                case 0:
                    Check(ui.Page==CombatPage.MainMenu,"launch main menu"); ValidateScore();
                    waves=Read<EnemyWaveController.WaveDefinition[]>(run,"waves");
                    Check(waves.Select(w=>w.SpawnList().Count).SequenceEqual(new[]{1,2,2,3,3}),"agreed specialist progression");
                    Check(waves.Select(w=>w.basicMinions).SequenceEqual(new[]{3,3,4,4,4}),"few basic minions per wave");
                    Check(waves[0].prefab.GetComponent<MortarEnemy>()!=null,"first specialist is mortar");
                    Check(run.GetComponentsInChildren<RotatingPinballBat>().Length==2,"two bats installed");
                    ui.PlayRun(); Check(ui.Page==CombatPage.WavePreview && run.LivingEnemies==0 && Time.timeScale==0,"wave preview pauses before spawning");
                    Check(UnityEngine.Object.FindObjectsByType<EnemyBumper>(FindObjectsSortMode.None).Length==0,"preview meshes never create enemies");
                    frozenTime=Time.time; Wait(1); break;
                case 1:
                    Check(Time.time==frozenTime,"preview freezes time");
                    var previewRoot=ui.GetComponentInChildren<UIDocument>().rootVisualElement;
                    Check(!previewRoot.Q("wave-preview").ClassListContains("hidden") && previewRoot.Q<Button>("start-wave").enabledSelf,"visible preview and Start Wave button");
                    ui.StartPresentedWave();
                    Check(run.IsCombatActive && run.LivingEnemies==4 && run.Player.DashUsesRemaining==1,"wave one and one starting dash");
                    var mortar=run.LivingEnemyObjects.First(e=>e.GetComponent<MortarEnemy>()!=null);
                    Field(mortar.GetComponent<MortarEnemy>(),"nextFire",float.PositiveInfinity);
                    run.DropRandom=()=>.99d;
                    var mine=run.SpawnMine(run.Player.ActivePosition); run.Health.ResetHealth(); Invoke(mine,"Explode");
                    Check(run.Health.Current==80 && run.Health.LastDamageLabel.Contains("Mine blast"),"mine labels accepted damage");
                    string cause=run.Health.LastDamageLabel;
                    var immuneMine=run.SpawnMine(run.Player.ActivePosition); Invoke(immuneMine,"Explode");
                    Check(run.Health.Current==80 && run.Health.LastDamageLabel==cause,"invulnerable blasts do not claim extra damage");
                    run.Health.ResetHealth();
                    run.SpawnShell(mortar.transform.position+Vector3.up*2,mortar.transform.position,mortar);
                    var shell=UnityEngine.Object.FindObjectsByType<CombatHazard>(FindObjectsSortMode.None).Last(h=>!Read<bool>(h,"isMine"));
                    Check(Mathf.Abs(shell.Radius-2.75f*run.Placement.Diameter)<.001f,"larger mortar warning and blast radius");
                    int points=run.Scoring.Points; Invoke(shell,"Explode"); Check(mortar.HitPoints==1,"own shell removes one segment");
                    Invoke(shell,"Explode"); Check(mortar.HitPoints==1,"shell damage applies once");
                    run.SpawnShell(mortar.transform.position+Vector3.up*2,mortar.transform.position,mortar);
                    var second=UnityEngine.Object.FindObjectsByType<CombatHazard>(FindObjectsSortMode.None).First(h=>h!=shell && !Read<bool>(h,"isMine"));
                    Invoke(second,"Explode"); Check(mortar.IsDefeated && run.Scoring.Points==points+50 && run.Scoring.ChainCount==0,"self defeat normal score without fake combo");
                    run.Scoring.RecordKill(false,Time.time,false); Check(run.Scoring.SkillTokens==1,"token earned from score");
                    ui.SendMessage("Update");
                    var skillRoot=ui.GetComponentInChildren<UIDocument>().rootVisualElement;
                    Check(!skillRoot.Q("token-popup").ClassListContains("hidden") && skillRoot.Q<Label>("token-popup").text.Contains("Choose Impact or Dash"),"token earned popup is visible");
                    ui.OpenSkills(); Check(ui.IsSkillChoiceOpen && !run.IsCombatActive && Time.timeScale==0,"skill choice pauses combat");
                    Check(ui.BuySkill(false) && run.Scoring.ImpactCharges==1 && run.Scoring.SkillTokens==0,"UI Impact purchase");
                    run.Scoring.RecordKill(false,Time.time,false);run.Scoring.RecordKill(false,Time.time,false);
                    int tokens=run.Scoring.SkillTokens;
                    while(run.Player.TryRestoreDashUse()) {}
                    Check(!ui.BuySkill(true) && run.Scoring.SkillTokens==tokens,"full dash rejects without token loss");
                    run.Player.RestoreDashCheckpoint(new BonkRobotController.DashCheckpoint(1,0));
                    Check(ui.BuySkill(true) && run.Player.DashUsesRemaining==2,"UI dash refill");
                    frozenTime=Time.time; Wait(2); break;
                case 2:
                    Check(Time.time==frozenTime,"skill screen freezes timers"); ui.CloseSkills();
                    run.Health.TryDamage(1000); Wait(3); break;
                case 3:
                    Check(run.State==EnemyWaveState.Failed,"death result"); ui.DismissForValidation(); run.RetryWave();
                    Check(run.Scoring.Points==0 && run.Scoring.SkillTokens==0 && run.Scoring.ImpactCharges==0 && run.Player.DashUsesRemaining==1,"retry resets skill and dash checkpoints");
                    KillAll(); Wait(4); break;
                case 4:
                    Check(run.State==EnemyWaveState.Cleared,"first clear"); Advance(); Check(run.WaveIndex==1 && run.LivingEnemies==5,"wave two mixed roster");
                    KillAll(); Wait(5); break;
                case 5:
                    Advance(); Check(run.WaveIndex==2 && run.LivingEnemies==6,"wave three mixed roster");
                    ValidateSpawner(UnityEngine.Object.FindFirstObjectByType<SpawnerEnemy>());
                    var net=UnityEngine.Object.FindFirstObjectByType<NetShooterEnemy>(); Field(net,"nextFire",float.PositiveInfinity);
                    run.Health.ResetHealth();
                    Check(run.Player.TrySnare(.6f,1.5f) && run.Player.IsSnared && !run.Player.CanAcceptPlayerInput,"net stops controls");
                    Check(!run.Player.TrySnare(.6f,1.5f),"net cannot chain during snare");
                    snareTime=run.Player.SnareRemaining; frozenTime=Time.time; ui.OpenHelp(); Wait(6); break;
                case 6:
                    Check(Time.time==frozenTime && run.Player.SnareRemaining==snareTime,"help preserves snare timer"); ui.SkipTutorial(); Wait(7,.7); break;
                case 7:
                    Check(!run.Player.IsSnared && !run.Player.TrySnare(.6f,1.5f),"snare releases with immunity");
                    run.Player.ResetForEncounter(run.Player.ActivePosition,Quaternion.identity);
                    var targetCollider=run.Player.GetComponentsInChildren<Collider>().First(c=>c.enabled && !c.isTrigger);
                    Vector3 target=targetCollider.bounds.center;
                    blocker=GameObject.CreatePrimitive(PrimitiveType.Cube); blocker.transform.position=target+Vector3.right*1.6f;
                    blocker.transform.localScale=new Vector3(.2f,3,3);
                    projectile=NetProjectile.Create(run,null,target+Vector3.right*3,target); Physics.SyncTransforms(); Wait(8,.6); break;
                case 8:
                    Check(projectile==null && !run.Player.IsSnared,"solid obstacle blocks net projectile"); UnityEngine.Object.DestroyImmediate(blocker);
                    var targetShape=run.Player.GetComponentsInChildren<Collider>().First(c=>c.enabled && !c.isTrigger);
                    projectile=NetProjectile.Create(run,null,targetShape.bounds.center+Vector3.right*2,targetShape.bounds.center); Wait(9,.4); break;
                case 9:
                    Check(run.Player.IsSnared && run.Health.Current==100,"net projectile snares without direct damage");
                    KillAll(); Wait(10); break;
                case 10:
                    Advance(); Check(run.WaveIndex==3 && run.LivingEnemies==7,"wave four three specialists");
                    var runner=UnityEngine.Object.FindFirstObjectByType<RunnerEnemy>(); var runnerBody=runner.GetComponent<EnemyBumper>();
                    float d=run.Placement.Diameter;
                    Check(runnerBody.UsesStraightPatrol && Mathf.Abs(runner.Speed-.8f*d)<.01f,"runner readable initial speed");
                    runnerBody.TryHitFromBall(run.Player.SpeedSettings.SmallEnemyKillSpeed,run.Player.SpeedSettings);
                    Check(Mathf.Abs(runner.Speed-1.2f*d)<.01f,"runner speeds up after first hit");
                    runnerBody.TryHitFromBall(run.Player.SpeedSettings.SmallEnemyKillSpeed,run.Player.SpeedSettings);
                    Check(Mathf.Abs(runner.Speed-1.6f*d)<.01f,"runner speeds up again");
                    run.Player.ResetForEncounter(run.Player.ActivePosition,Quaternion.identity); run.Player.ToggleForm();
                    var ball=run.Player.GetComponentInChildren<BouncingBallController>(); ball.EnterBallMode(Vector3.right*8);
                    var bat=run.GetComponentsInChildren<RotatingPinballBat>().First();
                    Check(ball.LaunchFromBat(bat.LaunchDirection,12,.15f),"bat launches ball");
                    Check(Vector3.Dot(ball.PlanarVelocity.normalized,bat.LaunchDirection)>.99f && ball.PlanarVelocity.magnitude<=run.Player.SpeedSettings.MaximumSpeed,"bat direction and speed cap");
                    var strike=typeof(RotatingPinballBat).GetMethod("TryStrike",Private);
                    var ballCollider=ball.GetComponent<Collider>();
                    Check((bool)strike.Invoke(bat,new object[]{ballCollider}),"fresh bat contact launches");
                    Field(bat,"readyAt",Time.time-1f);
                    Check(!(bool)strike.Invoke(bat,new object[]{ballCollider}),"same unseparated contact cannot launch again");
                    Read<System.Collections.Generic.HashSet<Collider>>(bat,"contacts").Clear(); Field(bat,"readyAt",Time.time+1f);
                    Check(!(bool)strike.Invoke(bat,new object[]{ballCollider}),"bat cooldown rejects rapid recontact");
                    run.Player.ToggleForm(); Check(!ball.LaunchFromBat(Vector3.forward,12,.15f),"bat cannot launch robot");
                    KillAll(); Wait(11); break;
                case 11:
                    Advance(); Check(run.WaveIndex==4 && run.LivingEnemies==7,"final wave three specialists"); KillAll(); Wait(12); break;
                case 12:
                    Check(run.State==EnemyWaveState.Victory,"all five waves finish");
                    Debug.Log("PINBALL_EXPANSION_VALIDATION_OK: agreed rosters, model-only previews, mine damage labels/protection, larger mortar self-damage/single defeat, tokens and purchases, checkpoints, surviving spawner/caps/death cancellation, nets/obstacles/snare/immunity/pause, runner speed stages, bat direction/cap/forms, five-wave flow.");
                    Finish(0); break;
            }
        }
        catch(Exception ex) { Debug.LogException(ex); Finish(1); }
    }
    private static void Finish(int code)
    {
        SessionState.SetBool(Running,false); EditorApplication.update-=Tick;
        if(Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.ExitPlaymode();
    }
}
