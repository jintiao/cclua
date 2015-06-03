using System;

namespace cclua {

    public static partial class imp {


        /*
        @@ LUAI_MAXSTACK limits the size of the Lua stack.
        ** CHANGE it if you need a different limit. This limit is arbitrary;
        ** its only purpose is to stop Lua from consuming unlimited stack
        ** space (and to reserve some numbers for pseudo-indices).
        */
        public const int LUAI_MAXSTACK = 15000;


        /* reserve some space for error handling */
        public const int LUAI_FIRSTPSEUDOIDX = -LUAI_MAXSTACK - 1000;


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


        public static string lua_integer2str (long i) { return i.ToString (); }


        public static string lua_number2str (double i) { return i.ToString (); }


        /* these are quite standard operations */
        public static double luai_numadd (lua530.lua_State L, double a, double b) { return (a + b); }
        public static double luai_numsub (lua530.lua_State L, double a, double b) { return (a - b); }
        public static double luai_nummul (lua530.lua_State L, double a, double b) { return (a * b); }
        public static double luai_numdiv (lua530.lua_State L, double a, double b) { return (a / b); }
        public static double luai_numunm (double a) { return (-a); }
        public static bool luai_numeq (double a, double b) { return (a == b); }
        public static bool luai_numlt (double a, double b) { return (a < b); }
        public static bool luai_numle (double a, double b) { return (a <= b); }
        public static bool luai_numisnan (double a) { return luai_numeq (a, a); }

        public static double l_floor (double x) { return Math.Floor (x); }
    }


    public static partial class lua530 {

        /*
        @@ lua_numbertointeger converts a float number to an integer, or
        ** returns 0 if float is not within the range of a lua_Integer.
        ** (The range comparisons are tricky because of rounding. The tests
        ** here assume a two-complement representation, where MININTEGER always
        ** has an exact representation as a float; MAXINTEGER may not have one,
        ** and therefore its conversion to float may have an ill-defined value.)
        */
        public static bool lua_numbertointeger (double n, ref long p) {
            if (n >= (double)imp.LUA_MININTEGER && n < (-((double)imp.LUA_MININTEGER))) {
                p = (long)n;
                return true;
            }
            return false;
        }
    }
}
