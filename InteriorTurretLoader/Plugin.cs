using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Plugins;

namespace InteriorTurretLoader
{
    public class Plugin : IPlugin
    {
        public void Init(object gameObject)
        {
            UserControllableGunPatch.Patch();
        }

        public void Update()
        {
            UserControllableGunPatch.Update();
        }


        public void Dispose()
        {

        }








    }
}
