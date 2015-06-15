using System;

using cc = cclua.lua530;

using lua_State = cclua.lua530.lua_State;
using luaL_Reg = cclua.lua530.luaL_Reg;

namespace cclua {
	
	public static partial class imp {
		
		public static class lstr {
			
			
		}


        public static luaL_Reg[] str_funcs = {
			
			new luaL_Reg (null, null),
		};
	}
	
	public static partial class mod {
		
		public static int luaopen_string (lua_State L) {
			
			return 1;
		}
	}
}