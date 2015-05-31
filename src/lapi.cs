using System;

namespace cclua {

    public static partial class lua530 {

        public static lua_CFunction lua_atpanic (lua_State L, lua_CFunction panicf) {
            imp.lua_lock (L);
            lua_CFunction old = imp.G (L).panic;
            imp.G (L).panic = panicf;
            imp.lua_unlock (L);
            return old;
        }

		public static long lua_version (lua_State L) {
			if (L == null) return LUA_VERSION_NUM;
			else return imp.G (L).version;
		}
    }
}
