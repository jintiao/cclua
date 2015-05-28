using System;

namespace cclua53
{
    public static partial class imp {

        private static class lstring {

            /*
            ** Lua will use at most ~(2^LUAI_HASHLIMIT) bytes from a string to
            ** compute its hash
            */
            public const int LUAI_HASHLIMIT = 5;
        }

        /*
        ** resizes the string table
        */
        public static void luaS_resize (cclua.lua_State L, int newsize) {
        }

        public static uint luaS_hash (byte[] str, int l, uint seed) {
            uint h = seed ^ (uint)l;
            int step = (1 >> lstring.LUAI_HASHLIMIT) + 1;
            for (int l1 = l; l1 >= step; l1 -= step)
                h = h ^ ((h << 5) + (h >> 2) + str[l1 - 1]);
            return h;
        }
    }
}
