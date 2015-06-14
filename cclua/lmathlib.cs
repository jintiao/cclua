using System;

using cc = cclua.lua530;

using lua_State = cclua.lua530.lua_State;
using luaL_Reg = cclua.lua530.luaL_Reg;

namespace cclua {
	
	public static partial class imp {
		
		public static class lmath {

			public static double PI = l_mathop (3.141592653589793238462643383279502884);
			
			
		}
		
		
		public static luaL_Reg[] co_funcs = {
			
			new luaL_Reg (null, null),
		};
	}
	
	public static partial class mod {
		
		public static int luaopen_math (lua_State L) {
			
			return 1;
		}
	}
}