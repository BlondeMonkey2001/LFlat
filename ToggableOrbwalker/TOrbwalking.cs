using SharpDX;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;

namespace ToggableOrbwalker
{
    public static class TOrbwalking
    {
        public delegate void AfterAttackEvenH(Obj_AI_Base unit, Obj_AI_Base target);
        public delegate void BeforeAttackEvenH(TOrbwalking.BeforeAttackEventArgs args);
        public delegate void OnAttackEvenH(Obj_AI_Base unit, Obj_AI_Base target);
        public delegate void OnNonKillableMinionH(Obj_AI_Base minion);
        public delegate void OnTargetChangeH(Obj_AI_Base oldTarget, Obj_AI_Base newTarget);
        public enum TOrbwalkingMode
        {
            LastHit,
            Mixed,
            LaneClear,
            Combo,
            None
        }
        public class BeforeAttackEventArgs
        {
            public Obj_AI_Base Target;
            public Obj_AI_Base Unit = ObjectManager.Player;
            private bool _process = true;
            public bool Process
            {
                get
                {
                    return this._process;
                }
                set
                {
                    TOrbwalking.DisableNextAttack = !value;
                    this._process = value;
                }
            }
        }
        public class TOrbwalker
        {
            private const float LaneClearWaitTimeMod = 2f;
            private static Menu _config;
            private readonly Obj_AI_Hero Player;
            private Obj_AI_Base _forcedTarget;
            private Vector3 _orbwalkingPoint;
            private Obj_AI_Minion _prevMinion;
            private int FarmDelay
            {
                get
                {
                    return TOrbwalking.TOrbwalker._config.Item("FarmDelay").GetValue<Slider>().Value;
                }
            }
            public TOrbwalking.TOrbwalkingMode ActiveMode
            {
                get
                {
                    if (TOrbwalking.TOrbwalker._config.Item("Orbwalk").GetValue<KeyBind>().Active)
                    {
                        return TOrbwalking.TOrbwalkingMode.Combo;
                    }
                    if (TOrbwalking.TOrbwalker._config.Item("LaneClearHold").GetValue<KeyBind>().Active || TOrbwalking.TOrbwalker._config.Item("LaneClearToggle").GetValue<KeyBind>().Active)
                    {
                        return TOrbwalking.TOrbwalkingMode.LaneClear;
                    }
                    if (TOrbwalking.TOrbwalker._config.Item("FarmHold").GetValue<KeyBind>().Active || TOrbwalking.TOrbwalker._config.Item("FarmToggle").GetValue<KeyBind>().Active)
                    {
                        return TOrbwalking.TOrbwalkingMode.Mixed;
                    }
                    if (TOrbwalking.TOrbwalker._config.Item("LastHitHold").GetValue<KeyBind>().Active || TOrbwalking.TOrbwalker._config.Item("LastHitToggle").GetValue<KeyBind>().Active)
                    {
                        return TOrbwalking.TOrbwalkingMode.LastHit;
                    }
                    return TOrbwalking.TOrbwalkingMode.None;
                }
            }
            public TOrbwalker(Menu attachToMenu)
            {
                TOrbwalking.TOrbwalker._config = attachToMenu;
                Menu menu = new Menu("Drawings", "drawings", false);
                menu.AddItem(new MenuItem("AACircle", "AACircle").SetShared().SetValue<Circle>(new Circle(true, System.Drawing.Color.FromArgb(255, 255, 0, 255), 100f)));
                menu.AddItem(new MenuItem("AACircle2", "Enemy AA circle").SetShared().SetValue<Circle>(new Circle(false, System.Drawing.Color.FromArgb(255, 255, 0, 255), 100f)));
                menu.AddItem(new MenuItem("HoldZone", "HoldZone").SetShared().SetValue<Circle>(new Circle(false, System.Drawing.Color.FromArgb(255, 255, 0, 255), 100f)));
                TOrbwalking.TOrbwalker._config.AddSubMenu(menu);
                Menu menu2 = new Menu("Misc", "Misc", false);
                menu2.AddItem(new MenuItem("HoldPosRadius", "Hold Position Radius").SetShared().SetValue<Slider>(new Slider(0, 150, 0)));
                menu2.AddItem(new MenuItem("PriorizeFarm", "Priorize farm over harass").SetShared().SetValue<bool>(true));
                TOrbwalking.TOrbwalker._config.AddSubMenu(menu2);
                TOrbwalking.TOrbwalker._config.AddItem(new MenuItem("ExtraWindup", "Extra windup time").SetShared().SetValue<Slider>(new Slider(80, 200, 0)));
                TOrbwalking.TOrbwalker._config.AddItem(new MenuItem("FarmDelay", "Farm delay").SetShared().SetValue<Slider>(new Slider(0, 200, 0)));
                TOrbwalking.TOrbwalker._config.AddItem(new MenuItem("LastHitHold", "Last hit").SetShared().SetValue<KeyBind>(new KeyBind((uint)"X".ToCharArray()[0], KeyBindType.Press, false)));
                TOrbwalking.TOrbwalker._config.AddItem(new MenuItem("FarmHold", "Mixed").SetShared().SetValue<KeyBind>(new KeyBind((uint)"C".ToCharArray()[0], KeyBindType.Press, false)));
                TOrbwalking.TOrbwalker._config.AddItem(new MenuItem("LaneClearHold", "LaneClear").SetShared().SetValue<KeyBind>(new KeyBind((uint)"V".ToCharArray()[0], KeyBindType.Press, false)));

                TOrbwalking.TOrbwalker._config.AddItem(new MenuItem("LastHitToggle", "Last hit (Toggle)").SetShared().SetValue<KeyBind>(new KeyBind((uint)"T".ToCharArray()[0], KeyBindType.Toggle, false)));
                TOrbwalking.TOrbwalker._config.AddItem(new MenuItem("FarmToggle", "Mixed (Toggle)").SetShared().SetValue<KeyBind>(new KeyBind((uint)"Y".ToCharArray()[0], KeyBindType.Toggle, false)));
                TOrbwalking.TOrbwalker._config.AddItem(new MenuItem("LaneClearToggle", "LaneClear (Toggle)").SetShared().SetValue<KeyBind>(new KeyBind((uint)"U".ToCharArray()[0], KeyBindType.Toggle, false)));
                this.Player = ObjectManager.Player;
                Game.OnGameUpdate += new GameUpdate(this.GameOnOnGameUpdate);
                Drawing.OnDraw += new Draw(this.DrawingOnOnDraw);
            }
            public void SetAttack(bool b)
            {
                TOrbwalking.Attack = b;
            }
            public void SetMovement(bool b)
            {
                TOrbwalking.Move = b;
            }
            public void ForceTarget(Obj_AI_Base target)
            {
                this._forcedTarget = target;
            }
            public void SetOrbwalkingPoint(Vector3 point)
            {
                this._orbwalkingPoint = point;
            }
            private bool ShouldWait()
            {
                return ObjectManager.Get<Obj_AI_Minion>().Any((Obj_AI_Minion minion) => minion.IsValidTarget(3.40282347E+38f, true, default(Vector3)) && minion.Team != GameObjectTeam.Neutral && TOrbwalking.InAutoAttackRange(minion) && (double)HealthPrediction.LaneClearHealthPrediction(minion, (int)(this.Player.AttackDelay * 1000f * 2f), this.FarmDelay) <= this.Player.GetAutoAttackDamage(minion, false));
            }
            public Obj_AI_Base GetTarget()
            {
                Obj_AI_Base obj_AI_Base = null;
                float r = 3.40282347E+38f;
                if ((this.ActiveMode == TOrbwalking.TOrbwalkingMode.Mixed || this.ActiveMode == TOrbwalking.TOrbwalkingMode.LaneClear) && !TOrbwalking.TOrbwalker._config.Item("PriorizeFarm").GetValue<bool>())
                {
                    Obj_AI_Hero target = SimpleTs.GetTarget(-1f, SimpleTs.DamageType.Physical);
                    if (target != null)
                    {
                        return target;
                    }
                }
                if (this.ActiveMode == TOrbwalking.TOrbwalkingMode.LaneClear || this.ActiveMode == TOrbwalking.TOrbwalkingMode.Mixed || this.ActiveMode == TOrbwalking.TOrbwalkingMode.LastHit)
                {
                    foreach (Obj_AI_Minion current in
                        from minion in ObjectManager.Get<Obj_AI_Minion>()
                        where minion.IsValidTarget(3.40282347E+38f, true, default(Vector3)) && TOrbwalking.InAutoAttackRange(minion)
                        select minion)
                    {
                        int time = (int)(this.Player.AttackCastDelay * 1000f) - 100 + Game.Ping / 2 + 1000 * (int)this.Player.Distance(current, false) / (int)TOrbwalking.GetMyProjectileSpeed();
                        float healthPrediction = HealthPrediction.GetHealthPrediction(current, time, this.FarmDelay);
                        if (current.Team != GameObjectTeam.Neutral)
                        {
                            if (healthPrediction <= 0f)
                            {
                                TOrbwalking.FireOnNonKillableMinion(current);
                            }
                            if (healthPrediction > 0f && (double)healthPrediction <= this.Player.GetAutoAttackDamage(current, true))
                            {
                                Obj_AI_Base result = current;
                                return result;
                            }
                        }
                    }
                }
                if (this._forcedTarget != null && this._forcedTarget.IsValidTarget(3.40282347E+38f, true, default(Vector3)) && TOrbwalking.InAutoAttackRange(this._forcedTarget))
                {
                    return this._forcedTarget;
                }
                if (this.ActiveMode != TOrbwalking.TOrbwalkingMode.LastHit)
                {
                    Obj_AI_Hero target2 = SimpleTs.GetTarget(-1f, SimpleTs.DamageType.Physical);
                    if (target2 != null)
                    {
                        return target2;
                    }
                }
                if (this.ActiveMode == TOrbwalking.TOrbwalkingMode.LaneClear || this.ActiveMode == TOrbwalking.TOrbwalkingMode.Mixed)
                {
                    foreach (Obj_AI_Minion current2 in
                        from mob in ObjectManager.Get<Obj_AI_Minion>()
                        where mob.IsValidTarget(3.40282347E+38f, true, default(Vector3)) && TOrbwalking.InAutoAttackRange(mob) && mob.Team == GameObjectTeam.Neutral
                        where mob.MaxHealth >= r || Math.Abs(r - 3.40282347E+38f) < 1.401298E-45f
                        select mob)
                    {
                        obj_AI_Base = current2;
                        r = current2.MaxHealth;
                    }
                }
                if (obj_AI_Base != null)
                {
                    return obj_AI_Base;
                }
                r = 3.40282347E+38f;
                if (this.ActiveMode == TOrbwalking.TOrbwalkingMode.LaneClear && !this.ShouldWait())
                {
                    if (this._prevMinion != null && this._prevMinion.IsValidTarget(3.40282347E+38f, true, default(Vector3)) && TOrbwalking.InAutoAttackRange(this._prevMinion))
                    {
                        float num = HealthPrediction.LaneClearHealthPrediction(this._prevMinion, (int)(this.Player.AttackDelay * 1000f * 2f), this.FarmDelay);
                        if ((double)num >= 2.0 * this.Player.GetAutoAttackDamage(this._prevMinion, false) || Math.Abs(num - this._prevMinion.Health) < 1.401298E-45f)
                        {
                            return this._prevMinion;
                        }
                    }
                    foreach (Obj_AI_Minion current3 in
                        from minion in ObjectManager.Get<Obj_AI_Minion>()
                        where minion.IsValidTarget(3.40282347E+38f, true, default(Vector3)) && TOrbwalking.InAutoAttackRange(minion)
                        select minion)
                    {
                        float num2 = HealthPrediction.LaneClearHealthPrediction(current3, (int)(this.Player.AttackDelay * 1000f * 2f), this.FarmDelay);
                        if (((double)num2 >= 2.0 * this.Player.GetAutoAttackDamage(current3, false) || Math.Abs(num2 - current3.Health) < 1.401298E-45f) && (current3.Health >= r || Math.Abs(r - 3.40282347E+38f) < 1.401298E-45f))
                        {
                            obj_AI_Base = current3;
                            r = current3.Health;
                            this._prevMinion = current3;
                        }
                    }
                }
                if (this.ActiveMode == TOrbwalking.TOrbwalkingMode.LaneClear)
                {
                    using (IEnumerator<Obj_AI_Turret> enumerator4 = (
                        from t in ObjectManager.Get<Obj_AI_Turret>()
                        where t.IsValidTarget(3.40282347E+38f, true, default(Vector3)) && TOrbwalking.InAutoAttackRange(t)
                        select t).GetEnumerator())
                    {
                        if (enumerator4.MoveNext())
                        {
                            Obj_AI_Turret current4 = enumerator4.Current;
                            Obj_AI_Base result = current4;
                            return result;
                        }
                    }
                }
                return obj_AI_Base;
            }
            private void GameOnOnGameUpdate(EventArgs args)
            {
                if (this.ActiveMode == TOrbwalking.TOrbwalkingMode.None)
                {
                    return;
                }
                if (this.Player.IsChannelingImportantSpell())
                {
                    return;
                }
                Obj_AI_Base target = this.GetTarget();
                TOrbwalking.Orbwalk(target, this._orbwalkingPoint.To2D().IsValid() ? this._orbwalkingPoint : Game.CursorPos, (float)TOrbwalking.TOrbwalker._config.Item("ExtraWindup").GetValue<Slider>().Value, (float)TOrbwalking.TOrbwalker._config.Item("HoldPosRadius").GetValue<Slider>().Value);
            }
            private void DrawingOnOnDraw(EventArgs args)
            {
                if (TOrbwalking.TOrbwalker._config.Item("AACircle").GetValue<Circle>().Active)
                {
                    Utility.DrawCircle(this.Player.Position, TOrbwalking.GetRealAutoAttackRange(null) + 65f, TOrbwalking.TOrbwalker._config.Item("AACircle").GetValue<Circle>().Color, 5, 30, false);
                }
                if (TOrbwalking.TOrbwalker._config.Item("AACircle2").GetValue<Circle>().Active)
                {
                    foreach (Obj_AI_Hero current in
                        from target in ObjectManager.Get<Obj_AI_Hero>()
                        where target.IsValidTarget(1175f, true, default(Vector3))
                        select target)
                    {
                        Utility.DrawCircle(current.Position, TOrbwalking.GetRealAutoAttackRange(current) + 65f, TOrbwalking.TOrbwalker._config.Item("AACircle2").GetValue<Circle>().Color, 5, 30, false);
                    }
                }
                if (TOrbwalking.TOrbwalker._config.Item("HoldZone").GetValue<Circle>().Active)
                {
                    Utility.DrawCircle(this.Player.Position, (float)TOrbwalking.TOrbwalker._config.Item("HoldPosRadius").GetValue<Slider>().Value, TOrbwalking.TOrbwalker._config.Item("HoldZone").GetValue<Circle>().Color, 5, 30, false);
                }
            }
        }
        private static readonly string[] AttackResets;
        private static readonly string[] NoAttacks;
        private static readonly string[] Attacks;
        public static int LastAATick;
        public static bool Attack;
        public static bool DisableNextAttack;
        public static bool Move;
        public static int LastMoveCommandT;
        private static Obj_AI_Base _lastTarget;
        private static readonly Obj_AI_Hero Player;
        public static event TOrbwalking.BeforeAttackEvenH BeforeAttack;
        public static event TOrbwalking.OnAttackEvenH OnAttack;
        public static event TOrbwalking.AfterAttackEvenH AfterAttack;
        public static event TOrbwalking.OnTargetChangeH OnTargetChange;
        public static event TOrbwalking.OnNonKillableMinionH OnNonKillableMinion;
        static TOrbwalking()
        {
            TOrbwalking.AttackResets = new string[]
			{
				"dariusnoxiantacticsonh",
				"fioraflurry",
				"garenq",
				"hecarimrapidslash",
				"jaxempowertwo",
				"jaycehypercharge",
				"leonashieldofdaybreak",
				"luciane",
				"lucianq",
				"monkeykingdoubleattack",
				"mordekaisermaceofspades",
				"nasusq",
				"nautiluspiercinggaze",
				"netherblade",
				"parley",
				"poppydevastatingblow",
				"powerfist",
				"renektonpreexecute",
				"rengarq",
				"shyvanadoubleattack",
				"sivirw",
				"takedown",
				"talonnoxiandiplomacy",
				"trundletrollsmash",
				"vaynetumble",
				"vie",
				"volibearq",
				"xenzhaocombotarget",
				"yorickspectral"
			};
            TOrbwalking.NoAttacks = new string[]
			{
				"jarvanivcataclysmattack",
				"monkeykingdoubleattack",
				"shyvanadoubleattack",
				"shyvanadoubleattackdragon",
				"zyragraspingplantattack",
				"zyragraspingplantattack2",
				"zyragraspingplantattackfire",
				"zyragraspingplantattack2fire"
			};
            TOrbwalking.Attacks = new string[]
			{
				"caitlynheadshotmissile",
				"frostarrow",
				"garenslash2",
				"kennenmegaproc",
				"lucianpassiveattack",
				"masteryidoublestrike",
				"quinnwenhanced",
				"renektonexecute",
				"renektonsuperexecute",
				"rengarnewpassivebuffdash",
				"trundleq",
				"xenzhaothrust",
				"xenzhaothrust2",
				"xenzhaothrust3"
			};
            TOrbwalking.Attack = true;
            TOrbwalking.DisableNextAttack = false;
            TOrbwalking.Move = true;
            TOrbwalking.LastMoveCommandT = 0;
            TOrbwalking.Player = ObjectManager.Player;
            Obj_AI_Base.OnProcessSpellCast += new GameObjectProcessSpellCast(TOrbwalking.OnProcessSpell);
            GameObject.OnCreate += new GameObjectCreate(TOrbwalking.Obj_SpellMissile_OnCreate);
            Game.OnGameProcessPacket += new GameProcessPacket(TOrbwalking.OnProcessPacket);
        }
        private static void Obj_SpellMissile_OnCreate(GameObject sender, EventArgs args)
        {
            if (sender is Obj_SpellMissile && sender.IsValid)
            {
                Obj_SpellMissile obj_SpellMissile = (Obj_SpellMissile)sender;
                if (obj_SpellMissile.SpellCaster is Obj_AI_Hero && obj_SpellMissile.SpellCaster.IsValid && TOrbwalking.IsAutoAttack(obj_SpellMissile.SData.Name))
                {
                    TOrbwalking.FireAfterAttack(obj_SpellMissile.SpellCaster, TOrbwalking._lastTarget);
                }
            }
        }
        private static void FireBeforeAttack(Obj_AI_Base target)
        {
            if (TOrbwalking.BeforeAttack != null)
            {
                TOrbwalking.BeforeAttack(new TOrbwalking.BeforeAttackEventArgs
                {
                    Target = target
                });
                return;
            }
            TOrbwalking.DisableNextAttack = false;
        }
        private static void FireOnAttack(Obj_AI_Base unit, Obj_AI_Base target)
        {
            if (TOrbwalking.OnAttack != null)
            {
                TOrbwalking.OnAttack(unit, target);
            }
        }
        private static void FireAfterAttack(Obj_AI_Base unit, Obj_AI_Base target)
        {
            if (TOrbwalking.AfterAttack != null)
            {
                TOrbwalking.AfterAttack(unit, target);
            }
        }
        private static void FireOnTargetSwitch(Obj_AI_Base newTarget)
        {
            if (TOrbwalking.OnTargetChange != null && (TOrbwalking._lastTarget == null || TOrbwalking._lastTarget.NetworkId != newTarget.NetworkId))
            {
                TOrbwalking.OnTargetChange(TOrbwalking._lastTarget, newTarget);
            }
        }
        private static void FireOnNonKillableMinion(Obj_AI_Base minion)
        {
            if (TOrbwalking.OnNonKillableMinion != null)
            {
                TOrbwalking.OnNonKillableMinion(minion);
            }
        }
        public static bool IsAutoAttackReset(string name)
        {
            return TOrbwalking.AttackResets.Contains(name.ToLower());
        }
        public static bool IsMelee(this Obj_AI_Base unit)
        {
            return unit.CombatType == GameObjectCombatType.Melee;
        }
        public static bool IsAutoAttack(string name)
        {
            return (name.ToLower().Contains("attack") && !TOrbwalking.NoAttacks.Contains(name.ToLower())) || TOrbwalking.Attacks.Contains(name.ToLower());
        }
        public static float GetRealAutoAttackRange(Obj_AI_Base target)
        {
            float num = TOrbwalking.Player.AttackRange + TOrbwalking.Player.BoundingRadius;
            if (target != null)
            {
                return num + target.BoundingRadius - (float)((target is Obj_AI_Hero) ? 50 : 0);
            }
            return num;
        }
        public static bool InAutoAttackRange(Obj_AI_Base target)
        {
            if (target != null)
            {
                float realAutoAttackRange = TOrbwalking.GetRealAutoAttackRange(target);
                return Vector2.DistanceSquared(target.ServerPosition.To2D(), TOrbwalking.Player.ServerPosition.To2D()) <= realAutoAttackRange * realAutoAttackRange;
            }
            return false;
        }
        public static float GetMyProjectileSpeed()
        {
            if (!TOrbwalking.Player.IsMelee())
            {
                return TOrbwalking.Player.BasicAttack.MissileSpeed;
            }
            return 3.40282347E+38f;
        }
        public static bool CanAttack()
        {
            return TOrbwalking.LastAATick <= Environment.TickCount && (float)(Environment.TickCount + Game.Ping / 2 + 25) >= (float)TOrbwalking.LastAATick + TOrbwalking.Player.AttackDelay * 1000f && TOrbwalking.Attack;
        }
        public static bool CanMove(float extraWindup)
        {
            return TOrbwalking.LastAATick <= Environment.TickCount && (float)(Environment.TickCount + Game.Ping / 2) >= (float)TOrbwalking.LastAATick + TOrbwalking.Player.AttackCastDelay * 1000f + extraWindup && TOrbwalking.Move;
        }
        private static void MoveTo(Vector3 position, float holdAreaRadius = 0f, bool overrideTimer = false)
        {
            if (Environment.TickCount - TOrbwalking.LastMoveCommandT < 80 && !overrideTimer)
            {
                return;
            }
            TOrbwalking.LastMoveCommandT = Environment.TickCount;
            if (TOrbwalking.Player.ServerPosition.Distance(position, false) < holdAreaRadius)
            {
                if (TOrbwalking.Player.Path.Count<Vector3>() > 1)
                {
                    TOrbwalking.Player.IssueOrder(GameObjectOrder.HoldPosition, TOrbwalking.Player.ServerPosition);
                }
                return;
            }
            Vector3 targetPos = TOrbwalking.Player.ServerPosition + 400f * (position.To2D() - TOrbwalking.Player.ServerPosition.To2D()).Normalized().To3D();
            TOrbwalking.Player.IssueOrder(GameObjectOrder.MoveTo, targetPos);
        }
        public static void Orbwalk(Obj_AI_Base target, Vector3 position, float extraWindup = 90f, float holdAreaRadius = 0f)
        {
            if (target != null && TOrbwalking.CanAttack())
            {
                TOrbwalking.DisableNextAttack = false;
                TOrbwalking.FireBeforeAttack(target);
                if (!TOrbwalking.DisableNextAttack)
                {
                    TOrbwalking.Player.IssueOrder(GameObjectOrder.AttackUnit, target);
                    if (!(target is Obj_AI_Hero))
                    {
                        TOrbwalking.LastAATick = Environment.TickCount + Game.Ping / 2;
                    }
                    return;
                }
            }
            if (TOrbwalking.CanMove(extraWindup))
            {
                TOrbwalking.MoveTo(position, holdAreaRadius, false);
            }
        }
        public static void ResetAutoAttackTimer()
        {
            TOrbwalking.LastAATick = 0;
        }
        private static void OnProcessPacket(GamePacketEventArgs args)
        {
            if (args.PacketData[0] != 52 || new GamePacket(args).ReadInteger(1L) != ObjectManager.Player.NetworkId || (args.PacketData[5] != 17 && args.PacketData[5] != 145))
            {
                return;
            }
            TOrbwalking.ResetAutoAttackTimer();
        }
        private static void OnProcessSpell(Obj_AI_Base unit, GameObjectProcessSpellCastEventArgs Spell)
        {
            if (TOrbwalking.IsAutoAttackReset(Spell.SData.Name) && unit.IsMe)
            {
                Utility.DelayAction.Add(250, new Utility.DelayAction.Callback(TOrbwalking.ResetAutoAttackTimer));
            }
            if (TOrbwalking.IsAutoAttack(Spell.SData.Name))
            {
                if (unit.IsMe)
                {
                    TOrbwalking.LastAATick = Environment.TickCount - Game.Ping / 2;
                    if (Spell.Target is Obj_AI_Base)
                    {
                        TOrbwalking.FireOnTargetSwitch((Obj_AI_Base)Spell.Target);
                        TOrbwalking._lastTarget = (Obj_AI_Base)Spell.Target;
                    }
                    if (unit.IsMelee())
                    {
                        Utility.DelayAction.Add((int)(unit.AttackCastDelay * 1000f + 40f), delegate
                        {
                            TOrbwalking.FireAfterAttack(unit, TOrbwalking._lastTarget);
                        });
                    }
                }
                TOrbwalking.FireOnAttack(unit, TOrbwalking._lastTarget);
            }
        }
    }
}

