using System;

namespace cclua53
{
    public static partial class imp {

        /*
        @@ LUA_EXTRASPACE defines the size of a raw memory area associated with
        ** a Lua state with very fast access.
        ** CHANGE it if you need a different size.
        */
        public const int LUA_EXTRASPACE = sizeof (long);
    }
}
