using System;

using lua_State = cclua.lua530.lua_State;

namespace cclua {
	
	public static partial class imp {

		public static luaL_Reg[] base_funcs = {
			new luaL_Reg ("assert", luaB_assert),
			new luaL_Reg (null, null),
		};
	}
	
	public static partial class mod {

		public static int luaopen_base (lua_State L) {
			/* open lib into global table */
			lua530.lua_pushglobaltable (L);
			lua530.luaL_setfuncs (L, imp.base_funcs, 0);
			return 1;
		}
	}
}
