using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using RWCustom;
using MoreSlugcats;
using System.Security.Cryptography;
using System.Collections;

namespace MagicSpear
{
    //注意这里，MagicSpearManager和magicSpearRings都是静态的，在程序加载时就被实例化了，并且不可以在运行中被创建实例，是唯一的
    //这意味着 可以直接通过 PlayerModuleManager.playerModules 访问它，而不需要 new PlayerModuleManager()
    internal static class MagicSpearManager
    {
        public static ConditionalWeakTable<Player, MagicSpearRing> magicSpearRings = new ConditionalWeakTable<Player, MagicSpearRing> ();
        public static readonly Weapon.Mode Spin = new Weapon.Mode("Spin");
        public static readonly Weapon.Mode Attack = new Weapon.Mode("Attack");
        public static readonly Weapon.Mode Attacked = new Weapon.Mode("Attacked");
        internal class MagicSpearRing: UpdatableAndDeletable
        {
            WeakReference<Player> playerRef;
            int life;
            float r;//矛环半径
            public static float sum = 1;//飞行的矛数量
            public float angle;//矛环的角度，相当于第一根矛的角度
            float angleSpeed;//矛调整角度的旋转速度
            public int instance_num;//列表中存储的矛数量
            public float ExamRange;//危险的检测范围，主要用于ExamDanger函数
            public List<Spear> instances;

            bool color;
            public MagicSpearRing(Player player, float range, float radius, bool color)
            {
                playerRef = new WeakReference<Player>(player);
                //this.GetPlayer() = player这是强引用
                this.ExamRange = range;
                this.angleSpeed = 1f;
                this.angle = 0f;
                this.r = radius;
                this.instances = new List<Spear>();
                this.instance_num = 0;
                this.color = color;
                this.life = 100;
            }

            public Player GetPlayer()
            {
                if (playerRef.TryGetTarget(out var player))
                {
                    return player;
                }
                return null; // Player 已被 GC 回收
            }
            public void RingsUpdate()// 与player的update合并，因为但写Update并不会每帧调用
            {
                if (this.GetPlayer() == null) 
                    Destroy();
                //生命周期计时
/*                this.life--;
                if (this.life <= 0) Delete();*/
                if (this.GetPlayer().room != null)//猫在房间内
                {
                    this.Refresh();
                    this.Spears_Spin(instances, this.GetPlayer(), ExamDanger(this.GetPlayer()));
                    this.Spears_Attack(this.ExamDanger(this.GetPlayer()));
                    this.Spears_Attack_Update(this.ExamDanger(this.GetPlayer()));
                }
                else
                {
                    //猫在管道中，销毁所有列表里的矛并清空列表（不为Destroy的矛被保留）
                    for (int i = instances.Count - 1; i >= 0; i--)
                    {
                        if (instances[i].mode == Attacked)
                            instance_num--;
                        instances[i].Destroy();
                    }
                    this.instances.Clear();
                }
            }
            private void Delete()
            {
                this.life = 100;
                for (int i = instances.Count - 1; i >= 0; i--)
                {
                    if (instances[i].mode == Attacked)
                    {
                        instances[i].Destroy(); // 销毁关联的 GameObject
                        instances.RemoveAt(i); // 移除该元素
                        instance_num--;
                    }
                }
            }
            private void Refresh()//判断是否需要新增矛
            {
                if (this.instances.Count != this.instance_num)//列表中实际矛数量与记录不同，说明进入新房间
                {
                    for (int i = this.instance_num - this.instances.Count; i > 0; i--)
                    {
                        AbstractSpear abstractSpear = new AbstractSpear(this.GetPlayer().abstractCreature.world, null, this.GetPlayer().abstractCreature.pos,
                        this.GetPlayer().room.game.GetNewID(), false);
                        this.GetPlayer().room.abstractRoom.AddEntity(abstractSpear);
                        abstractSpear.RealizeInRoom();
                        (abstractSpear.realizedObject as Spear).gravity = 0;
                        (abstractSpear.realizedObject as Spear).mode = Spin;
                        // (abstractSpear.realizedObject as Spear).collisionLayer = SpinCollisionLayer;
                        if(color)
                        (abstractSpear.realizedObject as Spear).color = Color.green;
                        (abstractSpear.realizedObject as Spear).firstChunk.collideWithObjects = false;
                        (abstractSpear.realizedObject as Spear).firstChunk.collideWithTerrain = false;
                        (abstractSpear.realizedObject as Spear).firstChunk.collideWithSlopes = false;
                        (abstractSpear.realizedObject as Spear).waterFriction = 0f;
                        this.instances.Add(abstractSpear.realizedObject as Spear);
                    }
                }

            }

            private void Spears_Spin(List<Spear> spears, Player master, AbstractCreature enemy)
            //令列表中的所有矛旋转，如果有目标，旋转指向目标
            {
                if (angle < 360f) angle += angleSpeed;
                else angle = angle + angleSpeed - 360f;
                float interval = 360f / spears.Count(item => item.mode == Spin);
                float angleRad = 0f;
                int index = 0;
                foreach (var item in spears.Where(item => item.mode == Spin))
                {
                    //对于每个处于旋转状态的矛，设置旋转速度和调整
                    angleRad = (angle + index * interval) * Mathf.Deg2Rad;
                    Vector2 vector = new Vector2(Mathf.Cos(angleRad) * r, Mathf.Sin(angleRad) * r);
                    item.firstChunk.pos = master.firstChunk.pos + vector;
                    if (enemy == null)
                    {
                        //未找到目标，正常旋转
                        AdjustRotation(ref item.rotation, vector, 3f);
                        item.setRotation = item.rotation;
                        //setRotation必须也被赋值，否则在穿过管道后矛会旋转错误
                        //并且setRotation不能作为参数传入AdjustRotation，不知道为啥
                        if(color)
                        item.color = Color.green;
                    }
                    else
                    {
                        //找到目标，朝着目标方向旋转
                        AdjustRotation(ref item.rotation, enemy.realizedCreature.mainBodyChunk.pos - item.firstChunk.pos, 3f);
                        item.setRotation = item.rotation;
                        if(color)
                        item.color = Color.yellow;
                    }
                    index++;//令矛在猫身边均匀分布的参数
                }
            }

            private void AdjustRotation(ref Vector2 start, Vector2 target, float rotateSpeed)
            //将一个Vector2类型的向量缓慢调整到目标值
            {
                Vector2 currentDirection = start.normalized;
                Vector2 targetDirection = target.normalized;
                float maxRotationAngle = rotateSpeed;
                float angle = Vector2.SignedAngle(currentDirection, targetDirection);
                float rotateAngle = Mathf.Clamp(angle, -maxRotationAngle, maxRotationAngle);
                Quaternion rotation = Quaternion.Euler(0, 0, rotateAngle);
                Vector2 newDirection = rotation * currentDirection;
                start = newDirection * start.magnitude;
            }

            private AbstractCreature ExamDanger(Player player)
            //检测猫身边是否有危险，如果有，返回最近的危险源
            {
                if (player == null || player.room == null) return null;
                EntityID id = player.abstractCreature.ID;
                foreach (var creature in player.room.abstractRoom.creatures)
                {
                    if (creature == null || creature.abstractAI == null || creature.state.dead||creature.abstractAI is SlugNPCAbstractAI) continue;
                    if (Custom.DistLess(player.firstChunk.pos, creature.realizedCreature.mainBodyChunk.pos, this.ExamRange))
                    {
                        if (creature.realizedCreature is GarbageWorm) continue;// 忽略垃圾虫
                        if (creature.realizedCreature is Lizard ||creature.realizedCreature is Scavenger)
                        {
                            if (creature.state.socialMemory.GetOrInitiateRelationship(id).like > 0f)//如果是蜥蜴或者拾荒，好感超过0则不攻击
                                continue;
                            else return creature;
                        }
                        else return creature;
                    }
                }
                return null;
            }

            private bool CanAttack(Spear spear, AbstractCreature enemy)
            {
                if (enemy == null) return false;
                if (enemy.realizedCreature.room != spear.room) return false;
                Vector2 target = Vector2.zero;
                //下面一个条件判断是针对不同生物瞄准部位的区别，后续优化可以定位不同生物打击部位
                if (enemy.realizedCreature is Lizard)//避开蜥蜴的头甲
                    target = enemy.realizedCreature.bodyChunks[1].pos;
                else
                    target = enemy.realizedCreature.mainBodyChunk.pos;
                Vector2 distance = (target - spear.firstChunk.pos);
                Vector2 dir = (target - spear.firstChunk.pos).normalized;
                if (this.GetPlayer().abstractCreature.realizedCreature.mainBodyChunk.vel.x * dir.x <= 0 ||
                   (this.GetPlayer().abstractCreature.realizedCreature.mainBodyChunk.vel.x < 0.2 &&
                   this.GetPlayer().abstractCreature.realizedCreature.mainBodyChunk.vel.x > -0.2)) return false;
                //玩家运动方向必须与矛发射方向同相，单纯用hook修改Weapon或Spear的Thrown函数没用
                //是因为玩家原地不动的时候始终有一个很小的正向速度值，令它不为0，为此增加条件设置令它的绝对值大于0.2时才发射
                //虽然勉强可以了，但是这个条件判断没有更简单的写法吗？
                if (Vector2.Angle(spear.rotation.normalized, dir.normalized) > 5f) return false;
                if (enemy.realizedCreature.room.GetTile(spear.firstChunk.pos + spear.rotation.normalized).IsSolid()
                    || enemy.realizedCreature.room.GetTile(spear.firstChunk.pos - spear.rotation.normalized).IsSolid())
                    return false;
                Room.Tile destination = enemy.realizedCreature.room.GetTile(target);
                int i;
                for (i = 0; i * dir.magnitude <= distance.magnitude; i++)
                {
                    if (enemy.realizedCreature.room.GetTile(spear.firstChunk.pos + i * dir).IsSolid()) return false;
                    if (enemy.realizedCreature.room.GetTile(spear.firstChunk.pos + i * dir) == destination) break;
                }
                if (i * dir.magnitude > distance.magnitude)
                    return false;
                return true;
            }

            private void Spears_Attack(AbstractCreature enemy)
            //判断是否发射，同时进行发射初始设置
            {
                if(enemy==null) return;
                if (instances.Count == 0 || enemy == null || enemy.realizedCreature.dead || !(enemy.state is HealthState)) return;
                Vector2 target = Vector2.one;
                if (enemy.realizedCreature is Lizard)
                    target = enemy.realizedCreature.bodyChunks[1].pos;
                else
                    target = enemy.realizedCreature.mainBodyChunk.pos;

                for (int i = instances.Count - 1; i >= 0; i--)//使用for循环从后往前遍历，可以在循环内从后往前删除元素
                {
                    Vector2 dir = (target - instances[i].firstChunk.pos).normalized;
                    if (CanAttack(instances[i], enemy))
                    {
                        instances[i].Thrown(this.GetPlayer(), instances[i].firstChunk.pos, instances[i].firstChunk.pos,//发射
                        new IntVector2(dir.x > 0 ? 1 : -1, 0), 1f, true);
                        if(color)
                        instances[i].color = Color.red;
                        instances[i].gravity = 0.9f;//把重力还给矛
                        instances[i].waterFriction = 0.98f;
                        instances[i].setRotation = instances[i].rotation = dir.normalized;
                        instances[i].firstChunk.vel = instances[i].firstChunk.vel.magnitude * dir;//设定初始运动方向为目标方向
                        //将矛从矛环中移除
                        instances.Remove(instances[i]);
                        instance_num--;
                    }
                    else continue;
                }
            }

            private void Spears_Attack_Update(AbstractCreature enemy)
            {
                if (instances.Count == 0 || enemy == null || enemy.realizedCreature.dead || !(enemy.state is HealthState)) return;
                Vector2 target = Vector2.one;
                if (enemy.realizedCreature is Lizard)
                    target = enemy.realizedCreature.bodyChunks[1].pos;
                else
                    target = enemy.realizedCreature.mainBodyChunk.pos;
                foreach (var item in instances.Where(item => item.mode == Weapon.Mode.Thrown))
                {
                    Vector2 dir = (target - item.firstChunk.pos).normalized;
                    //这里必须改变rotation，不然矛只会横向或者竖向飞，vel改变的是体块的运动方向，不是矛自身旋转的方向
                    this.AdjustRotation(ref item.rotation, dir, 20f);
                    item.setRotation = item.rotation;
                    this.AdjustRotation(ref item.firstChunk.vel, dir, 90f);
                }
            }

            public void Unequip() {
                for (int i = instances.Count - 1; i >= 0; i--) 
                {
                    instances[i].gravity = 0.9f;//把重力还给矛
                    instances[i].waterFriction = 0.98f;
                    instances[i].ChangeMode(Weapon.Mode.Free);
                    instances[i].firstChunk.vel = Vector2.zero;//设定初始运动方向为0
                    instances.Remove(instances[i]);//将矛从矛环中移除
                    instance_num--;
                }
            }
        }
    }

}
