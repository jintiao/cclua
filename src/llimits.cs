using System;

using lua_State = cclua.lua530.lua_State;

namespace cclua {

    public static partial class imp {

        public const int DBL_MAX_EXP = 1024;

        /* maximum value for size_t */
        public const long MAX_SIZET = MAX_INT;

        /* maximum size visible for Lua (must be representable in a lua_Integer */
        public const long MAX_SIZE = MAX_INT;


        public const ulong MAX_LUMEM = (ulong)(~((ulong)0));

        public const long MAX_LMEM = (long)(MAX_LUMEM >> 1);


        public const int MAX_INT = Int32.MaxValue;  /* maximum value of an int */


        /*
        ** conversion of pointer to integer:
        ** this is for hashing only; there is no problem if the integer
        ** cannot hold the whole pointer value
        */
        public static int point2int (object p) { return p.GetHashCode (); }


        /*
        ** maximum depth for nested C calls and syntactical nested non-terminals
        ** in a program. (Value must fit in an unsigned short int.)
        */
        public const int LUAI_MAXCCALLS = 200;


        /*
        ** maximum number of upvalues in a closure (both C and Lua). (Value
        ** must fit in an unsigned char.)
        */
        public const int MAXUPVAL = Byte.MaxValue;


        /* minimum size for the string table (must be power of 2) */
        public const int MINSTRTABSIZE = 64;  /* minimum size for "predefined" strings */


        /* minimum size for string buffer */
        public const int LUA_MINBUFFER = 32;


        public static void lua_lock (lua_State L) { }

        public static void lua_unlock (lua_State L) { }

        public static void api_check (bool e, object msg) { }

        public static void lua_assert (object x) { }

		public static T check_exp<T> (bool c, object e) { lua_assert (c); return (T)e; }

		public static void lua_longassert (bool c) { if (c == false) lua_assert (0); }

        public static void lua_writestringerror (string fmt, params object[] args) { }

		public static void luai_userstateopen (lua_State L) { }

		public static void luai_userstateclose (lua_State L) { }

        public static void luai_userstatefree (lua_State L, lua_State L1) { }

        public static void condmovestack (lua_State L) { }

		public static void condchangemem (lua_State L) { condmovestack (L); }
    }
}
