using System;

using lua_State = cclua.lua530.lua_State;

namespace cclua {

    public static partial class imp {

        /*
        ** Upvalues for Lua closures
        */
        public class UpVal {
            public class openc {
                UpVal next;  /* linked list */
                int touched;  /* mark to avoid cycles with dead threads */
            }
            public class uc {
                openc open;  /* (when open) */
                TValue value;  /* the value (when closed) */
            }

            public TValue v;  /* points to stack or to its own value */
            public ulong refcount;  /* reference counter */
            public uc u;
        }


		public static void luaF_close (lua_State L, int level) {
		}
    }
}
