using System;

namespace cclua53
{
    public static partial class imp {

        public const int DBL_MAX_EXP = 1024;

        public const int MAX_INT = Int32.MaxValue;

        /* maximum value for size_t */
        public const long MAX_SIZET = MAX_INT;

        /* maximum size visible for Lua (must be representable in a lua_Integer */
        public const long MAX_SIZE = MAX_INT;


        /*
        ** conversion of pointer to integer:
        ** this is for hashing only; there is no problem if the integer
        ** cannot hold the whole pointer value
        */
        public static int point2int (object p) { return p.GetHashCode (); }

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

		public static T check_exp<T> (bool c, object e) {
			return (T)e;
		}

        public static void lua_writestringerror (string fmt, params object[] args) {
        }
    }
}
