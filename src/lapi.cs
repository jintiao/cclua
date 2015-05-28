using System;

namespace cclua53 {

    public static partial class cclua {

        public static lua_CFunction lua_atpanic (lua_State L, lua_CFunction panicf) {
            imp.lua_lock (L);
            lua_CFunction old = imp.G (L).panic;
            imp.G (L).panic = panicf;
            imp.lua_unlock (L);
            return old;
        }


    }
}
