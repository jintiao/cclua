using System;

using cc = cclua.lua530;

using lua_State = cclua.lua530.lua_State;
using luaL_Reg = cclua.lua530.luaL_Reg;

namespace cclua {
	
	public static partial class imp {

        public static int luaB_error (lua_State L) {
            int level = (int)cc.luaL_optinteger (L, 2, 1);
            cc.lua_settop (L, 1);
            if (cc.lua_isstring (L, 1) != 0 && level > 0) {  /* add extra information? */
                cc.luaL_where (L, level);
                cc.lua_pushvalue (L, 1);
                cc.lua_concat (L, 2);
            }
            return cc.lua_error (L);
        }


        public static int luaB_assert (lua_State L) {
            if (cc.lua_toboolean (L, 1) != 0)  /* condition is true? */
                return cc.lua_gettop (L);  /* return all arguments */
            else {  /* error */
                cc.luaL_checkany (L, 1);  /* there must be a condition */
                cc.lua_remove (L, 1);  /* remove it */
                cc.lua_pushliteral (L, "assertion failed!");  /* default message */
                cc.lua_settop (L, 1);  /* leave only message (default if no other one) */
                return luaB_error (L);  /* call 'error' */
            }
        }

		public static luaL_Reg[] base_funcs = {
			new luaL_Reg ("assert", luaB_assert),
			new luaL_Reg (null, null),
		};
	}
	
	public static partial class mod {

		public static int luaopen_base (lua_State L) {
			/* open lib into global table */
			cc.lua_pushglobaltable (L);
			cc.luaL_setfuncs (L, imp.base_funcs, 0);
			return 1;
		}
	}
}
