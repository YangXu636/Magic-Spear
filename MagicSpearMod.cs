using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using System.Security.Permissions;
using UnityEngine;//使用了UNITY之后，所有类里名为Update的函数都会在每帧被调用。
//注意需要添加UnityEngine.InputLegacyModule的引用才能用Unity函数的按键检测
using Unity.Mathematics;
using MoreSlugcats;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Runtime.CompilerServices;
using RWCustom;
using Menu.Remix.MixedUI;//mod菜单初始化
using IL;
using On;
using BepInEx.Logging;
//这是一段神奇代码，加上后就可以访问类的私有成员
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618
//拾起矛时将矛添加到列表，围绕猫旋转，发射列表中的矛
namespace MagicSpear
{
    [BepInPlugin("Owl.MagicSpear", "MagicSpear", "1.0.0")]
    public class Spear_Patch : BaseUnityPlugin
    {
        private bool IsInit;
        public static ModOptionMenu optionsMenuInstance;
        public static readonly string MOD_ID = "Owl.MagicSpear";
        public void OnEnable()
        {
            On.RainWorld.OnModsInit += new On.RainWorld.hook_OnModsInit(this.RainWorld_OnModsInit);
        }
        private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig.Invoke(self);
            try {
                if (IsInit) return;
                IsInit = true;
                HookInit();
                optionsMenuInstance = new ModOptionMenu();
                MachineConnector.SetRegisteredOI(MOD_ID, optionsMenuInstance);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex);
                throw;
            }
        }

        public static void HookInit()
        {
            On.Player.ctor += Player_ctor;
            On.Player.Update += Player_Update;
            On.Player.Die += Player_Die;
            On.Spear.Update += Spear_Update;
            //On.Spear.PickedUp += Spear_PickedUp;
            //On.Weapon.Grabbed += Weapon_Grabbed;
            //On.Spear.ChangeMode += Spear_ChangeMode;
            //On.Spear.HitSomething += Spear_HitSomething;

            MagicSpearManager.HookOn();
        }

        private static void Spear_ChangeMode(On.Spear.orig_ChangeMode orig, Spear self, Weapon.Mode newMode)
        {
            if (newMode == Weapon.Mode.Carried)
            {
                foreach (var player in self.room.game.Players)
                {
                    if (MagicSpearManager.magicSpearRings.TryGetValue(player.realizedCreature as Player, out var module))
                    {
                        if (module.instances.Contains(self))
                        {
                            for (int i = self.grabbedBy.Count - 1; i >= 0; i--)
                                self.grabbedBy[i].Release();
                            self.grabbedBy.Clear();
                            self.gravity = 0;
                            self.firstChunk.collideWithObjects = false;
                            self.firstChunk.collideWithTerrain = false;
                            self.firstChunk.collideWithSlopes = false;
                            self.waterFriction = 0f;
                            self.mode = MagicSpearManager.Spin;
                            return;
                        }
                    }
                }
            }
            orig.Invoke(self,newMode);
        }

        private static void Player_Die(On.Player.orig_Die orig, Player self)
        {
            //玩家死亡解放矛环
            orig.Invoke(self);
            if (MagicSpearManager.magicSpearRings.TryGetValue(self, out var module))
                module.Unequip();
        }

        private static bool Spear_HitSomething(On.Spear.orig_HitSomething orig, Spear self, SharedPhysics.CollisionResult result, bool eu)
        {
            bool q=orig.Invoke(self,result, eu);

            if (self.thrownBy is Player) 
            {
                if (MagicSpearManager.magicSpearRings.TryGetValue(self.thrownBy as Player, out var module)) 
                {
                    Debug.Log("Hit smth");
                    if (module.instances.Contains(self)) 
                    {
                        if (optionsMenuInstance.ifdestroy.Value)
                        self.mode = MagicSpearManager.Attacked;
                    }
                }
            }
            //可选的，在发射后销毁矛
            return q;
        }

        private static void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig.Invoke(self, eu);
            if (MagicSpearManager.magicSpearRings.TryGetValue(self, out var module))
            {
                //获取self（Player类型）对应的MagicSpearRing类型实例，并赋值给module
                if (optionsMenuInstance.unequip.Value != KeyCode.None && Input.GetKey(optionsMenuInstance.unequip.Value))
                    module.Unequip();
                module.RingsUpdate();
            }
        }

        private static void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
        {
            orig.Invoke(self,abstractCreature,world);
            MagicSpearManager.magicSpearRings.Add(self, new MagicSpearManager.MagicSpearRing(self,
                optionsMenuInstance.examrange.Value, optionsMenuInstance.ringradius.Value, optionsMenuInstance.color.Value));
        }
    
        private static void Spear_Update(On.Spear.orig_Update orig, Spear self, bool eu)
        {
            //矛被抓取时添加到列表同时改变属性
            orig.Invoke(self, eu);
            if (self is ExplosiveSpear || self is ElectricSpear) return;
            if (self.grabbedBy.Count > 0)
            {
                foreach (var player in self.room.game.Players)
                {
                    if (MagicSpearManager.magicSpearRings.TryGetValue(player.realizedCreature as Player, out var module))
                    {
                        if (module.instances.Contains(self))
                        {
                            self.grabbedBy[0].Release();
                            self.mode = MagicSpearManager.Spin;
                        }
                    }
                }
            }
           if (IsCraftable()&&self.grabbedBy.Any())
           {
                var grasp = self.grabbedBy[0];
                if (grasp.grabber is Player)
                {
                    if (MagicSpearManager.magicSpearRings.TryGetValue(grasp.grabber as Player, out var module) && self.mode != MagicSpearManager.Spin)
                    {
                        //对矛的调整: 将矛取消被拾取状态，物理阻力设为0
                        self.grabbedBy[0].Release();
                        self.grabbedBy.Clear();
                        self.gravity = 0;
                        self.firstChunk.collideWithObjects = false;
                        self.firstChunk.collideWithTerrain = false;
                        self.firstChunk.collideWithSlopes = false;
                        self.waterFriction = 0f;
                        self.mode = MagicSpearManager.Spin;
                        module.instances.Add(self);
                        module.instance_num++;
                    }
                }
            }
        }

        public static bool IsCraftable() 
        { 
            if (optionsMenuInstance.craftable.Value == KeyCode.None) { return false; }
            if (Input.GetKey(optionsMenuInstance.craftable.Value)) { return true; }
            return false;
        }
    }
}
