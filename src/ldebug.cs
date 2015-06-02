using System;

using lua_State = cclua.lua530.lua_State;

namespace cclua {

	public static partial class imp {
		
		public static void resethookcount (lua_State L) { L.hookcount = L.basehookcount; }

        public static void luaG_runerror (lua_State L, string format, params object[] args) {
            // TODO
        }


        public static void luaG_typeerror (lua_State L, TValue o, string op) {
            // TODO
        }
    }
}
