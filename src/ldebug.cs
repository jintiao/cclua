using System;

namespace cclua {

	public static partial class imp {
		
		public static void resethookcount (lua_State L) { L.hookcount = L.basehookcount; }

        public static void luaG_runerror (lua530.lua_State L, string format, params object[] args) {
            // TODO
        }
    }
}
