﻿using System;

using cc = cclua.lua530;

using lua_State = cclua.lua530.lua_State;
using luaL_Reg = cclua.lua530.luaL_Reg;

namespace cclua {
	
	public static partial class imp {
		
		public static class lutf {
			
			
		}


        public static luaL_Reg[] utf_funcs = {
			
			new luaL_Reg (null, null),
		};
	}
	
	public static partial class mod {
		
		public static int luaopen_utf8 (lua_State L) {
			
			return 1;
		}
	}
}