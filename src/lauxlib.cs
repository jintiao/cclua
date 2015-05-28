using System;

namespace cclua53
{

    public static partial class cclua {

        private static class lauxlib {

            public static int panic (lua_State L) {
                imp.lua_writestringerror ("PANIC: unprotected error in call to Lua API (%s)\n", lua_tostring (L, -1));
                return 0; /* return to Lua to abort */
            }
        }

        public static string lua_tolstring (lua_State L, int index, ref ulong len) {
            // TODO
            return "";
        }

        public static lua_State luaL_newstate () {
            lua_State L = lua_newstate ();
            if (L == null) lua_atpanic (L, lauxlib.panic);
            return L;
        }
    }
}
