using System;

namespace cclua53
{
    public static partial class imp {

        /* minimum size for the string table (must be power of 2) */
        public const int MINSTRTABSIZE = 64;  /* minimum size for "predefined" strings */

        public static void lua_lock (cclua.lua_State L) {
        }

        public static void lua_unlock (cclua.lua_State L) {
        }

        public static void api_check (bool e, object msg) {
        }

        public static void lua_assert (object x) {
        }

        public static void lua_writestringerror (string fmt, params object[] args) {
        }
    }
}
