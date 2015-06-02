using System;

using lua_State = cclua.lua530.lua_State;

namespace cclua {

    public static partial class imp {

        private static class lvm {

            /*
            ** Similar to 'tonumber', but does not attempt to convert strings and
            ** ensure correct precision (no extra bits). Used in comparisons.
            */
            public static int tofloat (TValue obj, ref double n) {
                if (ttisfloat (obj)) n = fltvalue (obj);
                else if (ttisinteger (obj)) {
                    double x = ivalue (obj);  /* avoid extra precision */
                    n = x;
                }
                else {
                    n = 0;  /* to avoid warnings */
                    return 0;
                }
                return 1;
            }
        }


        public static bool cvt2str (TValue o) { return false; }  /* no conversion from numbers to strings */


        public static int tonumber (TValue o, ref double n) {
            if (ttisfloat (o)) {
                n = fltvalue (o);
                return 1;
            }
            else {
                return luaV_tonumber_ (o, ref n);
            }
        }


        /*
        ** Try to convert a value to a float. The float case is already handled
        ** by the macro 'tonumber'.
        */
        public static int luaV_tonumber_ (TValue o, ref double n) {
            return 0;
        }


        /*
        ** Main operation for equality of Lua values; return 't1 == t2'. 
        ** L == NULL means raw equality (no metamethods)
        */
        public static int luaV_equalobj (lua530.lua_State L, TValue t1, TValue t2) {
            if (ttype (t1) != ttype (t2)) {  /* not the same variant? */
                if (ttnov (t1) != ttnov (t2) || ttnov (t1) != lua530.LUA_TNUMBER)
                    return 0;  /* only numbers can be equal with different variants */
                else {  /* two numbers with different variants */
                    lua_assert (ttisnumber (t1) && ttisnumber (t2));
                    double n1 = 0;
                    double n2 = 0;
                    lvm.tofloat (t1, ref n1);
                    lvm.tofloat (t2, ref n2);
                    return luai_numeq (n1, n2);
                }
            }
            /* values have same type and same variant */
            switch (ttype (t1)) {
                case lua530.LUA_TNIL: return 1;
                case LUA_TNUMINT: return ((ivalue (t1) == ivalue (t2)) ? 1 : 0);
                default:
                    return ((gcvalue (t1) == gcvalue (t2)) ? 1 : 0);
            }
        }

        public static int luaV_rawequalobj (TValue t1, TValue t2) {
            return luaV_equalobj (null, t1, t2);
        }


        public static void luaV_execute (lua_State L) {
        }
    }
}
