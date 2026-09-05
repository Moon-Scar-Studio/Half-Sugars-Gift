//using NebulaN.Roles.Crewmate;
//using Virial;
//using Virial.Assignable;
//using Virial.Game;
//using static UnityEngine.ParticleSystem.PlaybackState;

//namespace NebulaN.Roles.Modifier;

//public class MiracleTouched : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
//{
//    private MiracleTouched() : base(
//        "miracletouched",
//        "mt",
//        Cor.Golden,
//        null, false, false, false
//    )
//    { }
//    bool ISpawnable.IsSpawnable => false;
//    public static MiracleTouched MyRole = new();
//    public RuntimeModifier CreateInstance(GamePlayer player, int[] arguments)
//        => new Instance(player);

//    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
//    {
//        public Instance(GamePlayer player) : base(player) { }
//        DefinedModifier RuntimeModifier.Modifier => MyRole;
//        bool RuntimeAssignable.CanBeAwareAssignment => false;

//        void RuntimeAssignable.OnActivated()
//        {
//        }
//        [Local]
//        void CheckCanBeMiracle(PlayerTaskCompleteLocalEvent ev)
//        {
//            if (PlayerControl.LocalPlayer.AllTasksCompleted() && MyPlayer.Role.Role.Category == RoleCategory.CrewmateRole)
//            {
//                AmongUsUtil.PlayQuickFlash(MyPlayer.Role.Role.Color);
//                Miraclist.Instance.RpcMiracleSet.Invoke(MyPlayer.PlayerId);
//                MyPlayer.AddModifier(Miracle.MyRole);
//                MyPlayer.RemoveModifier(MiracleTouched.MyRole);
//            }
//        }
        
//    }
//}
//public class Miracle : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
//{
//    private Miracle() : base(
//        "miracle",
//        "mcle",
//        Cor.lightYellow,
//        null, false, false, false
//    )
//    { }
//    bool ISpawnable.IsSpawnable => false;
//    public static Miracle MyRole = new();

//    public RuntimeModifier CreateInstance(GamePlayer player, int[] arguments)
//        => new Instance(player);

//    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
//    {
//        public Instance(GamePlayer player) : base(player) { }
//        DefinedModifier RuntimeModifier.Modifier => MyRole;

//        void RuntimeAssignable.OnActivated()
//        {
//        }
//        void DecorateDivineRelation(PlayerDecorateNameEvent ev)
//        {
//            if (!AmOwner) return;
//            if (!MyPlayer.TryGetModifier<Miracle.Instance>(out _)) return;

//            if (ev.Player.TryGetModifier<DivineSeed.Instance>(out _))
//            {
//                ev.Name += " DS".Color(Cor.Golden.ToUnityColor());
//            }
//        }
//    }
//}
//public class GiveDivineSeed : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
//{
//    private GiveDivineSeed() : base(
//        "givedivineseed",
//        "GDS",
//        Cor.Golden,
//        null, false, false, false)
//    {
//    }
//    bool DefinedAssignable.ShowOnHelpScreen => false;
//    bool ISpawnable.IsSpawnable => false;
//    public static readonly GiveDivineSeed MyRole = new();
//    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
//    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
//    {
//        public Instance(GamePlayer player) : base(player)
//        {
//        }

//        bool RuntimeAssignable.CanBeAwareAssignment => false;
//        DefinedModifier RuntimeModifier.Modifier => MyRole;


//        ModAbilityButton? DivineSeed;


//        void RuntimeAssignable.OnActivated()
//        {
//            if (!AmOwner) return;
//            AmongUsUtil.PlayQuickFlash(Cor.Golden);
//            DivineSeed = NebulaAPI.Modules.AbilityButton(
//                this, 
//                MyPlayer,
//                false,true,
//                VirtualKeyInput.Ability,null,0f,"miraclist.seed"
//                ,NebulaAPI.AddonAsset.GetResource("DivineSeed.png")?.AsImage(100f)
//                ,_=>true,_=>!MyPlayer.IsDead,false
//                );
//            var Tracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer);
//            Tracker.SetColor(Cor.Golden);
//            DivineSeed.OnClick = button =>
//            {
//                var target = Tracker.CurrentTarget;
//                if (target == null) return;
//                if (target.Role.Role.Category != RoleCategory.CrewmateRole) { MyPlayer.Suicide(PlayerState.Dead, null, KillParameter.NormalKill);  return; }
//                target.AddModifier(Modifier.DivineSeed.MyRole);
//            };
//        }
//    }
//}