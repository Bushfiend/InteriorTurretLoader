using HarmonyLib;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Entities.Inventory;
using Sandbox.Game.Gui;
using Sandbox.Game.Screens.Terminal.Controls;
using Sandbox.Game.Weapons;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.Entities.Weapons;
using SpaceEngineers.Game.EntityComponents.GameLogic;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI.Ingame;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace InteriorTurretLoader
{
    internal static class UserControllableGunPatch
    {
        public static void Patch()
        {
            var harmony = new Harmony("InteriorTurretLoader");
            harmony.Patch(AccessTools.Method("Sandbox.Game.Weapons.MyUserControllableGun:CreateTerminalControls"),
                transpiler: new HarmonyMethod(typeof(UserControllableGunPatch), nameof(CreateTerminalControlsTranspiler)));
        }


        public static void Update()
        {
            if (MySession.Static == null)
                _itemTransferQueue.Clear();


            if(_itemTransferQueue.Count > 0)
            {
                _isLoadingAmmo = true;
                var command = _itemTransferQueue.Dequeue();
                command.Invoke();
            }                
            else   
                _isLoadingAmmo = false;      
        }






        private static void AddAmmoControls()
        {
            MyTerminalControlFactory.AddControl<MyUserControllableGun>(new MyTerminalControlLabel<MyUserControllableGun>(MyStringId.GetOrCompute("Ammo Loader"))
            {
                Enabled = new Func<MyUserControllableGun, bool>(IsVisible),
                Visible = new Func<MyUserControllableGun, bool>(IsVisible),
                SupportsMultipleBlocks = true
            });

            var seperator1 = new MyTerminalControlSeparator<MyUserControllableGun>();
            MyTerminalControlFactory.AddControl<MyUserControllableGun>(seperator1);

            var ammoAmountSlider = new MyTerminalControlSlider<MyUserControllableGun>("AmmoSlider", MyStringId.GetOrCompute($"Ammo Amount"), MyStringId.GetOrCompute("Amount of ammo to place in each turret."));
            ammoAmountSlider.DefaultValue = _ammoAmount;
            ammoAmountSlider.SetMinStep(1);
            ammoAmountSlider.SetLimits(1, 200);
            ammoAmountSlider.Setter = delegate (MyUserControllableGun x, float v)
            {
                _ammoAmount = v;
            };
            
            ammoAmountSlider.Getter = delegate (MyUserControllableGun x)
            {
                return _ammoAmount;
            };
            ammoAmountSlider.Writer = delegate (MyUserControllableGun x, StringBuilder result)
            {
                result.Append(new StringBuilder().AppendDecimal(_ammoAmount, 0));
            };


            MyTerminalControlFactory.AddControl<MyUserControllableGun>(ammoAmountSlider);

            MyTerminalControlFactory.AddControl<MyUserControllableGun>(new MyTerminalControlButton<MyUserControllableGun>("LoadButton", MyStringId.GetOrCompute("Load Ammo"), MyStringId.GetOrCompute("Loads Selected Amount of Ammo."), new Action<MyUserControllableGun>(LoadAmmo), false, false)
            {
                Enabled = new Func<MyUserControllableGun, bool>(IsVisible),
                Visible = new Func<MyUserControllableGun, bool>(IsVisible),
                SupportsMultipleBlocks = true
            });
            
        }


        private static void LoadAmmo(MyUserControllableGun block)
        {
            if (_isLoadingAmmo)
                return;        
            var turretBlock = block as MyLargeInteriorTurret;
            if (turretBlock == null)
                return;
            var inventory = block.GetInventory();
            if (inventory == null)
                return;
           
            var ammoTypes = turretBlock.GunBase.WeaponDefinition.AmmoMagazinesId;
            var ammo = ammoTypes.First();
            if (ammo == null)
                return; 
          
            var inventories = GetInventories(ammo, block);
            if (inventories.Count == 0)
                return;


            Action itemTransfer = () =>
            {
                if (inventory == null)
                    return;

                foreach (var sourceInventory in inventories)
                {
                    if (sourceInventory == null)
                        continue;

                    var current = (float)inventory.GetItemAmount(ammo);
                    var need = _ammoAmount - current;

                    if (Math.Abs(need) < 0.0001f)
                        return;

                    if (need > 0)
                    {
                        var available = (float)sourceInventory.GetItemAmount(ammo);
                        if (available <= 0.0001f)
                            continue;

                        var move = Math.Min(need, available);
                        MyInventory.TransferByPlanner(sourceInventory, inventory,
                            (SerializableDefinitionId)ammo, MyItemFlags.None, (VRage.MyFixedPoint)move);
                    }
                    else
                    {
                        var extra = -need;
                        MyInventory.TransferByPlanner(inventory, sourceInventory,
                            (SerializableDefinitionId)ammo, MyItemFlags.None, (VRage.MyFixedPoint)extra);
                    }

                    break;
                }
            };
            _itemTransferQueue.Enqueue(itemTransfer);
        }
      

        private static bool IsVisible(MyUserControllableGun block)
        {
            return (block is MyLargeInteriorTurret);
        }

        private static List<MyInventory> GetInventories(MyDefinitionId item, MyFunctionalBlock block = null)
        {

            List<MyInventory> inventories = new List<MyInventory>();
            if (MySession.Static.LocalCharacter == null) 
                return inventories;
            if (MySession.Static.LocalCharacter.GetInventory().ContainItems(null, item))
            {
                inventories.Add(MySession.Static.LocalCharacter.GetInventory());
            }

            if(block == null)
                return inventories;

            foreach(var b in block.CubeGrid.GetFatBlocks().OfType<MyCargoContainer>())
            {
                if (!b.HasInventory)
                    continue;
                var inventory = b.GetInventory();
                if (!inventory.ContainItems(1, item))
                    continue;
                inventories.Add(inventory);
            }

            return inventories;
        }

      

        public static IEnumerable<CodeInstruction> CreateTerminalControlsTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(instructions);
            MethodInfo addAmmo = AccessTools.Method(typeof(UserControllableGunPatch), "AddAmmoControls", null, null);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                bool flag = list[i].opcode == OpCodes.Call && list[i + 1].opcode == OpCodes.Call;
                if (flag)
                {
                    list.Insert(i + 2, new CodeInstruction(OpCodes.Call, addAmmo));
                    break;
                }
            }
            return list;
        }


        
     
        private static bool _isLoadingAmmo = false;
        private static float _ammoAmount = 20f;
        private static Queue<Action> _itemTransferQueue = new Queue<Action>();
    }
}
