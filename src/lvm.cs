﻿using System;

using lua_State = cclua.lua530.lua_State;

namespace cclua {

    public static partial class imp {

        private static class lvm {

            /*
            ** You can define LUA_FLOORN2I if you want to convert floats to integers
            ** by flooring them (instead of raising an error if they are not
            ** integral values)
            */
            public const int LUA_FLOORN2I = 0;


            /* limit for table tag-method chains (to avoid loops) */
            public const int MAXTAGLOOP = 2000; 


            /*
            ** Similar to 'tonumber', but does not attempt to convert strings and
            ** ensure correct precision (no extra bits). Used in comparisons.
            */
            public static bool tofloat (TValue obj, ref double n) {
                if (ttisfloat (obj)) n = fltvalue (obj);
                else if (ttisinteger (obj)) {
                    double x = ivalue (obj);  /* avoid extra precision */
                    n = x;
                }
                else {
                    n = 0;  /* to avoid warnings */
                    return false;
                }
                return true;
            }


            /*
            ** try to convert a value to an integer, rounding according to 'mode':
            ** mode == 0: accepts only integral values
            ** mode == 1: takes the floor of the number
            ** mode == 2: takes the ceil of the number
            */
            public static int tointeger_aux (TValue obj, ref long p, int mode) {
                TValue v = luaM_newobject<TValue> (null);
                while (true) {
                    if (ttisfloat (obj)) {
                        double n = fltvalue (obj);
                        double f = l_floor (n);
                        if (n != f) {  /* not an integral value? */
                            if (mode == 0) return 0;  /* fails if mode demands integral value */
                            else if (mode > 1)  /* needs ceil? */
                                f += 1;  /* convert floor to ceil (remember: n != f) */
                        }
                        return lua530.lua_numbertointeger (f, ref p);
                    }
                    else if (ttisinteger (obj)) {
                        p = ivalue (obj);
                        return 1;
                    }
                    else {
                        if (cvt2num (obj) && luaO_str2num (svalue (obj), v) == tsvalue (obj).len + 1)
                            tvcopy (obj, v);  /* convert result from 'luaO_str2num' to an integer */
                        else
                            return 0;  /* conversion failed */
                    }
                }
            }


            /*
            ** Compare two strings 'ls' x 'rs', returning an integer smaller-equal-
            ** -larger than zero if 'ls' is smaller-equal-larger than 'rs'.
            ** The code is a little tricky because it allows '\0' in the strings
            ** and it uses 'strcoll' (to respect locales) for each segments
            ** of the strings.
            */
            public static int l_strcmp (TString ls, TString rs) {
                return 0;
            }



        }



        public static bool cvt2str (TValue o) { return false; }  /* no conversion from numbers to strings */
        public static bool cvt2num (TValue o) { return false; }  /* no conversion from strings to numbers */


        public static int tonumber (TValue o, ref double n) {
            if (ttisfloat (o)) {
                n = fltvalue (o);
                return 1;
            }
            else {
                return luaV_tonumber_ (o, ref n);
            }
        }


        public static int tointeger (TValue o, ref long n) {
            if (ttisinteger (o)) {
                n = ivalue (o);
                return 1;
            }
            else {
                return luaV_tointeger_ (o, ref n);
            }
        }


        public static bool luaV_rawequalobj (TValue t1, TValue t2) { return luaV_equalobj (null, t1, t2); }


        /*
        ** Try to convert a value to a float. The float case is already handled
        ** by the macro 'tonumber'.
        */
        public static int luaV_tonumber_ (TValue obj, ref double n) {
            TValue v = luaM_newobject<TValue> (null);
            if (ttisinteger (obj)) {
                n = ivalue (obj);
                return 1;
            }
            else if (cvt2num (obj) &&
                        luaO_str2num (svalue (obj), v) == tsvalue (obj).len + 1) {
                n = nvalue (v);  /* convert result of 'luaO_str2num' to a float */
                return 1;
            }
            return 0;  /* conversion failed */
        }


        /*
        ** try to convert a value to an integer
        */
        public static int luaV_tointeger_ (TValue obj, ref long p) {
            return lvm.tointeger_aux (obj, ref p, lvm.LUA_FLOORN2I);
        }


        /*
        ** Main function for table access (invoking metamethods if needed).
        ** Compute 'val = t[key]'
        */
        public static void luaV_gettable (lua_State L, TValue t, TValue key, int val) {
            for (int loop = 0; loop < lvm.MAXTAGLOOP; loop++) {  /* counter to avoid infinite loops */
                if (ttistable (t)) {  /* 't' is a table? */
                    Table h = hvalue (t);
                    TValue res = luaH_get (h, key);  /* do a primitive get */
                    if (ttisnil (res) == false) {  /* result is not nil? */
                        TValue tm = fasttm (L, h.metatable, TMS.TM_INDEX);
                        if (tm == null) {  /* or no TM? */
                            setobj2s (L, val, res);  /* result is the raw get */
                            return;
                        }
                    }
                    /* else will try metamethod */
                }
                else {
                    TValue tm = luaT_gettmbyobj (L, t, TMS.TM_INDEX);
                    if (ttisnil (tm))
                        luaG_typeerror (L, t, "index");
                    else if (ttisfunction (tm)) {  /* metamethod is a function */
                        luaT_callTM (L, tm, t, key, val, 1);
                        return;
                    }
                    t = tm;  /* else repeat access over 'tm' */
                }
            }
            luaG_runerror (L, "gettable chain too long; possible loop");
        }


        /*
        ** Main function for table assignment (invoking metamethods if needed).
        ** Compute 't[key] = val'
        */
        public static void luaV_settable (lua_State L, TValue t, TValue key, int val) {
            for (int loop = 0; loop < lvm.MAXTAGLOOP; loop++) {  /* counter to avoid infinite loops */
                TValue tm;
                if (ttistable (t)) {  /* 't' is a table? */
                    Table h = hvalue (t);
                    TValue oldval = luaH_get (h, key);
                    /* if previous value is not nil, there must be a previous entry
                        in the table; a metamethod has no relevance */
                    if (ttisnil (oldval) == false) {
                        /* previous value is nil; must check the metamethod */
                        tm = fasttm (L, h.metatable, TMS.TM_NEWINDEX);
                        if (tm == null) {
                            /* no metamethod; is there a previous entry in the table? */
                            if (oldval == luaO_nilobject) {
                                /* no previous entry; must create one. (The next test is
                                    always true; we only need the assignment.) */
                                oldval = luaH_newkey (L, h, key);
                            }
                        }
                    }
                    /* no metamethod and (now) there is an entry with given key */
                    setobj2t (L, oldval, val);  /* assign new value to that entry */
                    invalidateTMcache (h);
                    luaC_barrierback (L, h, val);
                    return;
                    /* else will try the metamethod */
                }
                else {  /* not a table; check metamethod */
                    tm = luaT_gettmbyobj (L, t, TMS.TM_NEWINDEX);
                    if (ttisnil (tm))
                        luaG_typeerror (L, t, "index");
                }
                /* try the metamethod */
                if (ttisfunction (tm)) {
                    luaT_callTM (L, tm, t, key, val, 0);
                    return;
                }
                t = tm;  /* else repeat assignment over 'tm' */
            }
            luaG_runerror (L, "settable chain too long; possible loop");
        }


        /*
        ** Main operation less than; return 'l < r'.
        */
        public static bool luaV_lessthan (lua_State L, TValue l, TValue r) {
            int res = 0;
            double nl = 0;
            double nr = 0;
            if (ttisinteger (l) && ttisinteger (r))  /* both operands are integers? */
                return (ivalue (l) < ivalue (r));
            else if (lvm.tofloat (l, ref nl) && lvm.tofloat (r, ref nr))  /* both are numbers? */
                return luai_numlt (nl, nr);
            else if (ttisstring (l) && ttisstring (r))  /* both are strings? */
                return lvm.l_strcmp (tsvalue (l), tsvalue (r)) < 0;
            else {
                res = luaT_callorderTM (L, l, r, TMS.TM_LT);
                if (res < 0)  /* no metamethod? */
                    luaG_ordererror (L, l, r);  /* error */
            }
            return (res > 0);
        }


        /*
        ** Main operation less than or equal to; return 'l <= r'.
        */
        public static bool luaV_lessequal (lua_State L, TValue l, TValue r) {
            int res = 0;
            double nl = 0;
            double nr = 0;
            if (ttisinteger (l) && ttisinteger (r))  /* both operands are integers? */
                return (ivalue (l) <= ivalue (r));
            else if (lvm.tofloat (l, ref nl) && lvm.tofloat (r, ref nr))  /* both are numbers? */
                return luai_numle (nl, nr);
            else if (ttisstring (l) && ttisstring (r))  /* both are strings? */
                return lvm.l_strcmp (tsvalue (l), tsvalue (r)) <= 0;
            else {
                res = luaT_callorderTM (L, l, r, TMS.TM_LE);  /* first try 'le' */
                if (res >= 0)
                    return ((res > 0) ? true : false);
                res = luaT_callorderTM (L, r, l, TMS.TM_LT);  /* else try 'lt' */
                if (res < 0) 
                    luaG_ordererror (L, l, r);
            }
            return (res == 0);
        }








        /*
        ** Main operation for equality of Lua values; return 't1 == t2'. 
        ** L == NULL means raw equality (no metamethods)
        */
        public static bool luaV_equalobj (lua530.lua_State L, TValue t1, TValue t2) {
            if (ttype (t1) != ttype (t2)) {  /* not the same variant? */
                if (ttnov (t1) != ttnov (t2) || ttnov (t1) != lua530.LUA_TNUMBER)
                    return false;  /* only numbers can be equal with different variants */
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
                case lua530.LUA_TNIL: return true;
                case LUA_TNUMINT: return (ivalue (t1) == ivalue (t2));
                default:
                    return (gcvalue (t1) == gcvalue (t2));
            }
        }


        public static void luaV_execute (lua_State L) {
        }
    }
}
