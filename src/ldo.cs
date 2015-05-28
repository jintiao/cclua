using System;

namespace cclua53
{
    public static partial class imp {

        /* chain list of long jump buffers */
        public class lua_longjmp {
            public lua_longjmp previous;
            public volatile int status;  /* error code */
        }

        private class LuaException : Exception {
            public lua_longjmp lj;
            public LuaException (lua_longjmp jmp) {
                lj = jmp;
            }
        }

        /* type of protected functions, to be ran by 'runprotected' */
        public delegate void Pfunc (cclua.lua_State L, object ud);

        public static int luaD_rawrunprotected (cclua.lua_State L, Pfunc f, object ud) {
            ushort oldCcalls = L.nCcalls;
            lua_longjmp lj = new lua_longjmp ();
            lj.status = cclua.LUA_OK;
            lj.previous = L.errorJmp;  /* chain new error handler */
            L.errorJmp = lj;
            try {
                f (L, ud);
            }
            catch (Exception e) {
                if (lj.status == 0) lj.status = -1;
            }
            L.errorJmp = lj.previous;  /* restore old error handler */
            L.nCcalls = oldCcalls;
            return lj.status;
        }
    }
}
