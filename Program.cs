using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cc = cclua.lua530;

namespace lua530 {
    class Program {
        static void Main (string[] args) {
            var L = cc.luaL_newstate ();
			cc.luaL_openlibs (L);
			//cc.lua_newtable(L);
            cc.lua_pushinteger (L, 2);
			//cc.lua_pushnumber(L, 7);
			//cc.lua_rawset(L, -3);
			//cc.lua_setglobal(L, "foo");
			cc.lua_close(L);
        }
    }
}
