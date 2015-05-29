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

		/*
		@@ LUAI_MAXSHORTLEN is the maximum length for short strings, that is,
		** strings that are internalized. (Cannot be smaller than reserved words
		** or tags for metamethods, as these strings must be internalized;
		** #("function") = 8, #("__newindex") = 10.)
		*/
		public const int LUAI_MAXSHORTLEN = 40;

        public const long LUA_MAXINTEGER = Int64.MaxValue;
        public const long LUA_MININTEGER = Int64.MinValue;


        /* these are quite standard operations */
        public static double luai_numadd (cclua.lua_State L, double a, double b) { return (a + b); }
        public static double luai_numsub (cclua.lua_State L, double a, double b) { return (a - b); }
        public static double luai_nummul (cclua.lua_State L, double a, double b) { return (a * b); }
        public static double luai_numdiv (cclua.lua_State L, double a, double b) { return (a / b); }
        public static double luai_numunm (double a) { return (-a); }
        public static int luai_numeq (double a, double b) { return ((a == b) ? 1 : 0); }
        public static int luai_numlt (double a, double b) { return ((a < b) ? 1 : 0); }
        public static int luai_numle (double a, double b) { return ((a <= b) ? 1 : 0); }
        public static int luai_numisnan (double a) { return luai_numeq (a, a); }

        public static double l_floor (double x) {
            return Math.Floor (x);
        }
    }


    public static partial class cclua {

        /*
        @@ lua_numbertointeger converts a float number to an integer, or
        ** returns 0 if float is not within the range of a lua_Integer.
        ** (The range comparisons are tricky because of rounding. The tests
        ** here assume a two-complement representation, where MININTEGER always
        ** has an exact representation as a float; MAXINTEGER may not have one,
        ** and therefore its conversion to float may have an ill-defined value.)
        */
        public static int lua_numbertointeger (double n, ref long p) {
            if (n >= (double)imp.LUA_MININTEGER && n < (-((double)imp.LUA_MININTEGER))) {
                p = (long)n;
                return 1;
            }
            return 0;
        }
    }
}
