using System;

namespace cclua53
{
    public static partial class imp {

		/* maximum value for size_t */
		public const ulong MAX_SIZET = (ulong)(~(ulong)0);

		/* maximum size visible for Lua (must be representable in a lua_Integer */
		public const ulong MAX_SIZE = MAX_SIZET;

		public const int MAX_INT = Int32.MaxValue;

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

		public static T check_exp<T> (bool c, T e) {
			return e;
		}

        public static void lua_writestringerror (string fmt, params object[] args) {
        }
    }
}
