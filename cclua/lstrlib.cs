using System;

using cc = cclua.lua530;

using lua_State = cclua.lua530.lua_State;
using luaL_Reg = cclua.lua530.luaL_Reg;

namespace cclua {
	
	public static partial class imp {
		
		public static class lstr {

            /* translate a relative string position: negative means back from end */
            public static long posrelat (long pos, int size) {
                if (pos >= 0) return pos;
                else if (0u - pos > size) return 0;
                else return (size + pos + 1);
            }
		}





        public static int str_len (lua_State L) {
            int l = 0;
            cc.luaL_checklstring (L, 1, ref l);
            cc.lua_pushinteger (L, l);
            return l;
        }


        public static int str_sub (lua_State L) {
            int l = 0;
            string s = cc.luaL_checklstring (L, 1, l);
            long start = lstr.posrelat (cc.luaL_checkinteger (L, 2), l);
            long end = lstr.posrelat (cc.luaL_checkinteger (L, 3), l);
            if (start < 1) start = 1;
            if (end > l) end = l;
            if (start <= end)
                cc.lua_pushlstring (L, s, (end - start + 1));
            else cc.lua_pushliteral (L, "");
            return 1;
        }







        public static luaL_Reg[] strlib = {
			new luaL_Reg ("byte", str_byte),
			new luaL_Reg ("char", str_char),
			new luaL_Reg ("dump", str_dump),
			new luaL_Reg ("find", str_find),
			new luaL_Reg ("format", str_format),
			new luaL_Reg ("gmatch", gmatch),
			new luaL_Reg ("gsub", str_gsub),
			new luaL_Reg ("len", str_len),
			new luaL_Reg ("lower", str_lower),
			new luaL_Reg ("match", str_match),
			new luaL_Reg ("rep", str_rep),
			new luaL_Reg ("reverse", str_reverse),
			new luaL_Reg ("sub", str_sub),
			new luaL_Reg ("upper", str_upper),
			new luaL_Reg ("pack", str_pack),
			new luaL_Reg ("packsize", str_packsize),
			new luaL_Reg ("unpack", str_unpack),
			new luaL_Reg (null, null),
		};


        public static void createmetatable (lua_State L) {
            cc.lua_createtable (L, 0, 1);  /* table to be metatable for strings */
            cc.lua_pushliteral (L, "");  /* dummy string */
            cc.lua_pushvalue (L, -2);  /* copy table */
            cc.lua_setmetatable (L, -2);  /* set table as metatable for strings */
            cc.lua_pop (L, 1);  /* pop dummy string */
            cc.lua_pushvalue (L, -2);  /* get string library */
            cc.lua_setfield (L, -2, "__index");  /* metatable.__index = string */
            cc.lua_pop (L, 1);  /* pop metatable */
        }
	}
	
	public static partial class mod {

        /*
        ** Open string library
        */
		public static int luaopen_string (lua_State L) {
            cc.luaL_newlib (L, imp.strlib);
            imp.createmetatable (L);
			return 1;
		}
	}
}