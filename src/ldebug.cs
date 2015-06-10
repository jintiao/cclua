using System;

using lua_State = cclua.lua530.lua_State;

namespace cclua {

	public static partial class imp {
		
		public static void resethookcount (lua_State L) { L.hookcount = L.basehookcount; }


        public static void luaG_errormsg (lua_State L) {
            if (L.errfunc != 0) {  /* is there an error handling function? */
                int errfunc = restorestack (L, L.errfunc);
                setobjs2s (L, L.top, L.top - 1);  /* move argument */
                setobjs2s (L, L.top - 1, errfunc);  /* push function */
                L.top++;  /* assume EXTRA_STACK */
                luaD_call (L, L.top - 2, 1, 0);  /* call it */
            }
            luaD_throw (L, lua530.LUA_ERRRUN);
        }


        public static void luaG_runerror (lua_State L, string format, params object[] args) {
            // TODO
        }


        public static void luaG_typeerror (lua_State L, TValue o, string op) {
            // TODO
        }


        public static void luaG_concaterror (lua_State L, TValue p1, TValue p2) {
            // TODO
        }


        public static void luaG_opinterror (lua_State L, TValue p1, TValue p2, string msg) {
            // TODO
        }

        public static void luaG_tointerror (lua_State L, TValue p1, TValue p2) {
        }


        public static void luaG_ordererror (lua_State L, TValue p1, TValue p2) {
        }
    }
}
