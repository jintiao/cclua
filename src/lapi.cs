using System;

using lua_State = cclua.lua530.lua_State;
using CallInfo = cclua.imp.CallInfo;
using TValue = cclua.imp.TValue;
using TString = cclua.imp.TString;
using CClosure = cclua.imp.CClosure;
using Table = cclua.imp.Table;

namespace cclua {

    public static partial class imp {

        private static class lapi {

            /* value at a non-valid index */
            public static TValue NONVALIDVALUE = luaO_nilobject;
        }


        /* corresponding test */
        public static bool isvalid (TValue o) { return (o != luaO_nilobject); }

        /* test for pseudo index */
        public static bool ispseudo (int i) { return (i <= lua530.LUA_REGISTRYINDEX); }

        /* test for upvalue */
        public static bool isupvalue (int i) { return (i < lua530.LUA_REGISTRYINDEX); }

        /* test for valid but not pseudo index */
        public static bool isstackindex (int i, TValue o) { return (isvalid (o) && (ispseudo (i) == false)); }

        public static void api_checkvalidindex (TValue o) { api_check (isvalid (o), "invalid index"); }

        public static void api_checkstackindex (int i, TValue o) { api_check (isstackindex (i, o), "index not in the stack"); }


        public static void api_incr_top (lua_State L) { L.top++; api_check (L.top <= L.ci.top, "stack overflow"); }

        public static void api_checknelems (lua_State L, int n) { api_check (n < (L.top - L.ci.func), "not enough elements in the stack"); }


        public static TValue index2addr (lua_State L, int idx) {
            CallInfo ci = L.ci;
            if (idx > 0) {
                api_check (idx <= ci.top - (ci.func + 1), "unacceptable index");
                if (ci.func + idx >= L.top) return lapi.NONVALIDVALUE;
                else return L.stack[ci.func + idx];
          
            }
            else if (ispseudo (idx) == false) {  /* negative index */
                api_check (idx != 0 && -idx <= L.top - (ci.func + 1), "invalid index");
                return L.stack[L.top + idx];
            }
            else if (idx == lua530.LUA_REGISTRYINDEX)
                return G (L).l_registry;
            else {  /* upvalues */
                idx = lua530.LUA_REGISTRYINDEX - idx;
                api_check (idx <= MAXUPVAL + 1, "upvalue index too large");
                if (ttislcf (L.stack[ci.func]))  /* light C function? */
                    return lapi.NONVALIDVALUE;  /* it has no upvalues */
                else {
                    CClosure func = clCvalue (L.stack[ci.func]);
                    return (idx <= func.nupvalues) ? func.upvalue[idx - 1] : lapi.NONVALIDVALUE;
                }
            }
        }


        /*
        ** to be called by 'lua_checkstack' in protected mode, to grow stack
        ** capturing memory errors
        */
        public static void growstack (lua_State L, object ud) {
            int size = (int)ud;
            luaD_growstack (L, size);
        }


        /*
        ** Reverse the stack segment from 'from' to 'to'
        ** (auxiliary to 'lua_rotate')
        */
        public static void reverse (lua_State L, int from, int to) {
            TValue temp = new TValue ();
            for (; from < to; from++) {
                setobj (L, temp, from);
                setobjs2s (L, from, to);
                setobj2s (L, to, temp);
                to--;
            }
        }
    }

    public static partial class lua530 {

        public static int lua_checkstack (lua_State L, int n) {
            int res;
            CallInfo ci = L.ci;
            imp.lua_lock (L);
            imp.api_check (n >= 0, "negative 'n'");
            if (L.stack_last - L.top > n)  /* stack large enough? */
                res = 1;  /* yes; check is OK */
            else {  /* no; need to grow stack */
                int inuse = L.top + imp.EXTRA_STACK;
                if (inuse > imp.LUAI_MAXSTACK - n)  /* can grow without overflow? */
                    res = 0;  /* no */
                else  /* try to grow stack */
                    res = (imp.luaD_rawrunprotected (L, imp.growstack, n) == LUA_OK ? 1 : 0);
            }
            if (res > 0 && ci.top < L.top + n)
                ci.top = L.top + n;  /* adjust frame top */
            imp.lua_unlock (L);
            return res;
        }


        public static void lua_xmove (lua_State from, lua_State to, int n) {
            if (from == to) return;
            imp.lua_lock (to);
            imp.api_checknelems (from, n);
            imp.api_check (imp.G (from) == imp.G (to), "moving among independent states");
            imp.api_check (to.ci.top - to.top >= n, "not enough elements to move");
            from.top -= n;
            for (int i = 0; i < n; i++)
                imp.setobj2s (to, to.stack[to.top++], from.stack[from.top + i]);
            imp.lua_unlock (to);
        }


        public static lua_CFunction lua_atpanic (lua_State L, lua_CFunction panicf) {
            imp.lua_lock (L);
            lua_CFunction old = imp.G (L).panic;
            imp.G (L).panic = panicf;
            imp.lua_unlock (L);
            return old;
        }


		public static long lua_version (lua_State L) {
			if (L == null) return LUA_VERSION_NUM;
            else return imp.G (L).version;
		}



        /*
        ** basic stack manipulation
        */


        /*
        ** convert an acceptable stack index into an absolute index
        */
        public static int lua_absindex (lua_State L, int idx) {
            return (idx > 0 || imp.ispseudo (idx))
                ? idx
                : (L.top - L.ci.func + idx);
        }


        public static int lua_gettop (lua_State L) {
            return (L.top - (L.ci.func + 1));
        }


        public static void lua_settop (lua_State L, int idx) {
            int func = L.ci.func;
            imp.lua_lock (L);
            if (idx > 0) {
                imp.api_check (idx <= L.stack_last - (func + 1), "new top too large");
                while (L.top < (func + 1) + idx)
                    imp.setnilvalue (L.stack[L.top++]);
                L.top = (func + 1) + idx;
            }
            else {
                imp.api_check (-(idx + 1) <= (L.top - (func + 1)), "invalid new top");
                L.top += idx + 1;  /* 'subtract' index (index is negative) */
            }
            imp.lua_unlock (L);
        }


        /*
        ** Let x = AB, where A is a prefix of length 'n'. Then,
        ** rotate x n == BA. But BA == (A^r . B^r)^r.
        */
        public static void lua_rotate (lua_State L, int idx, int n) {
            imp.lua_lock (L);
            int t = L.top - 1;  /* end of stack segment being rotated */
            TValue p = imp.index2addr (L, idx);  /* start of segment */
            imp.api_checkstackindex (idx, p);
            imp.api_check ((n >= 0 ? n : -n) <= (t - idx + 1), "invalid 'n'");
            int m = (n >= 0 ? t - n : idx - n - 1);  /* end of prefix */
            imp.reverse (L, idx, m);  /* reverse the prefix with length 'n' */
            imp.reverse (L, m + 1, t);  /* reverse the suffix */
            imp.reverse (L, idx, t);  /* reverse the entire segment */
            imp.lua_unlock (L);
        }


        public static void lua_copy (lua_State L, int fromidx, int toidx) {
            imp.lua_lock (L);
            TValue fr = imp.index2addr (L, fromidx);
            TValue to = imp.index2addr (L, toidx);
            imp.api_checkvalidindex (to);
            imp.setobj (L, to, fr);
            if (imp.isupvalue (toidx))  /* function upvalue? */
                imp.luaC_barrier (L, imp.clCvalue (L.stack[L.ci.func]), fr);
            /* LUA_REGISTRYINDEX does not need gc barrier
                 (collector revisits it before finishing collection) */
            imp.lua_unlock (L);
        }


        public static void lua_pushvalue (lua_State L, int idx) {
            imp.lua_lock (L);
            imp.setobj2s (L, L.stack[L.top], imp.index2addr (L, idx));
            imp.api_incr_top (L);
            imp.lua_unlock (L);
        }



        /*
        ** access functions (stack -> C)
        */


        public static int lua_type (lua_State L, int idx) {
            TValue o = imp.index2addr (L, idx);
            return (imp.isvalid (o) ? imp.ttnov (o) : LUA_TNONE);
        }


        public static string lua_typename (lua_State L, int t) {
            imp.api_check (LUA_TNONE <= t && t < LUA_NUMTAGS, "invalid tag");
            return imp.ttypename (t);
        }


        public static int lua_iscfunction (lua_State L, int idx) {
            TValue o = imp.index2addr (L, idx);
            return ((imp.ttislcf (o) || imp.ttisCclosure (o)) ? 1 : 0);
        }


        public static int lua_isinteger (lua_State L, int idx) {
            TValue o = imp.index2addr (L, idx);
            return ((imp.ttisinteger (o)) ? 1 : 0);
        }


        public static int lua_isnumber (lua_State L, int idx) {
            TValue o = imp.index2addr (L, idx);
            double n = 0;
            return (imp.tonumber (o, ref n) ? 1 : 0);
        }


        public static int lua_isstring (lua_State L, int idx) {
            TValue o = imp.index2addr (L, idx);
            return ((imp.ttisstring (o) || imp.cvt2str(o)) ? 1 : 0);
        }


        public static int lua_isuserdata (lua_State L, int idx) {
            TValue o = imp.index2addr (L, idx);
            return ((imp.ttisfulluserdata (o) || imp.ttislightuserdata (o)) ? 1 : 0);
        }


        public static int lua_rawequal (lua_State L, int index1, int index2) {
            TValue o1 = imp.index2addr (L, index1);
            TValue o2 = imp.index2addr (L, index2);
            return (imp.isvalid (o1) && imp.isvalid (o2)) ? (imp.luaV_rawequalobj (o1, o2) ? 1 : 0) : 0;
        }


        public static void lua_arith (lua_State L, int op) {
            imp.lua_lock (L);
            if (op != LUA_OPUNM && op != LUA_OPBNOT)
                imp.api_checknelems (L, 2);  /* all other operations expect two operands */
            else {  /* for unary operations, add fake 2nd operand */
                imp.api_checknelems (L, 1);
                imp.setobjs2s (L, L.top, L.top - 1);
                L.top++;
            }
            /* first operand at top - 2, second at top - 1; result go to top - 2 */
            imp.luaO_arith (L, op, L.top - 2, L.top - 1, L.top - 2);
            L.top--;  /* remove second operand */
            imp.lua_unlock (L);
        }


        public static int lua_compare (lua_State L, int index1, int index2, int op) {
            imp.lua_lock (L);
            bool i = false;
            TValue o1 = imp.index2addr (L, index1);
            TValue o2 = imp.index2addr (L, index2);
            if (imp.isvalid (o1) && imp.isvalid (o2)) {
                switch (op) {
                    case LUA_OPEQ: i = imp.luaV_equalobj (L, o1, o2); break;
                    case LUA_OPLT: i = imp.luaV_lessthan (L, o1, o2); break;
                    case LUA_OPLE: i = imp.luaV_lessequal (L, o1, o2); break;
                    default: imp.api_check (false, "invalid option"); break;
                }
            }
            imp.lua_unlock (L);
            return (i ? 1 : 0);
        }


        public static int lua_stringtonumber (lua_State L, string s) {
            int sz = imp.luaO_str2num (imp.str2byte (s), L.stack[L.top]);
            if (sz != 0)
                imp.api_incr_top (L);
            return sz;
        }


        public static double lua_tonumberx (lua_State L, int idx, ref int pisnum) {
            double n = 0;
            TValue o = imp.index2addr (L, idx);
            bool isnum = imp.tonumber (o, ref n);
            if (isnum == false)
                n = 0;  /* call to 'tonumber' may change 'n' even if it fails */
            pisnum = (isnum ? 1 : 0);
            return n;
        }


        public static double lua_tointegerx (lua_State L, int idx, ref int pisnum) {
            long n = 0;
            TValue o = imp.index2addr (L, idx);
            bool isnum = imp.tointeger (o, ref n);
            if (isnum == false)
                n = 0;  /* call to 'tointeger' may change 'n' even if it fails */
            pisnum = (isnum ? 1 : 0);
            return n;
        }


        public static int lua_toboolean (lua_State L, int idx) {
            TValue o = imp.index2addr (L, idx);
            return (imp.l_isfalse (o) ? 1 : 0);
        }


        public static string lua_tolstring (lua_State L, int idx, ref int len) {
            TValue o = imp.index2addr (L, idx);
            if (imp.ttisstring (o) == false) {
                if (imp.cvt2str (o) == false) {
                    len = 0;
                    return null;
                }
                imp.lua_lock (L);
                imp.luaC_checkGC (L);
                o = imp.index2addr (L, idx);
                imp.luaO_tostring (L, o);
                imp.lua_unlock (L);
            }
            len = imp.tsvalue (o).len;
            return imp.byte2str (imp.svalue (o));
        }


        public static int lua_rawlen (lua_State L, int idx) {
            TValue o = imp.index2addr (L, idx);
            switch (imp.ttnov (o)) {
                case LUA_TSTRING: return imp.tsvalue (o).len;
                case LUA_TUSERDATA: return imp.uvalue (o).len;
                case LUA_TTABLE: return imp.luaH_getn (imp.hvalue (o));
                default: return 0;
            }
        }


        public static lua_CFunction lua_tocfunction (lua_State L, int idx) {
            TValue o = imp.index2addr (L, idx);
            if (imp.ttislcf (o)) return imp.fvalue (o);
            else if (imp.ttisCclosure (o))
                return imp.clCvalue (o).f;
            else return null;  /* not a C function */
        }


        public static object lua_touserdata (lua_State L, int idx) {
            TValue o = imp.index2addr (L, idx);
            switch (imp.ttnov (o)) {
                case LUA_TUSERDATA: return imp.getudatamem (imp.uvalue (o));
                case LUA_TLIGHTUSERDATA: return imp.pvalue (o);
                default: return null;
            }
        }


        public static lua_State lua_tothread (lua_State L, int idx) {
            TValue o = imp.index2addr (L, idx);
            return (imp.ttisthread (o) ? imp.thvalue (o) : null);
        }


        public static object lua_topointer (lua_State L, int idx) {
            TValue o = imp.index2addr (L, idx);
            switch (imp.ttype (o)) {
                case LUA_TTABLE: return imp.hvalue (o);
                case imp.LUA_TLCL: return imp.clLvalue (o);
                case imp.LUA_TCCL: return imp.clCvalue (o);
                case imp.LUA_TLCF: return imp.fvalue (o);
                case LUA_TTHREAD: return imp.thvalue (o);
                case LUA_TUSERDATA: goto case LUA_TLIGHTUSERDATA;
                case LUA_TLIGHTUSERDATA: return lua_touserdata (L, idx);
                default: return null;
            }
        }





        /*
        ** push functions (C -> stack)
        */
        public static void lua_pushnil (lua_State L) {
            imp.lua_lock (L);
            imp.setnilvalue (L, L.top);
            imp.api_incr_top (L);
            imp.lua_unlock (L);
        }


        public static void lua_pushnumber (lua_State L, double n) {
            imp.lua_lock (L);
            imp.setfltvalue (L, L.top, n);
            imp.api_incr_top (L);
            imp.lua_unlock (L);
        }


        public static void lua_pushinteger (lua_State L, long n) {
            imp.lua_lock (L);
            imp.setivalue (L, L.top, n);
            imp.api_incr_top (L);
            imp.lua_unlock (L);
        }


        public static byte[] lua_pushlstring (lua_State L, byte[] s, int len) {
            imp.lua_lock (L);
            imp.luaC_checkGC (L);
            TString ts = imp.luaS_newlstr (L, s, len);
            imp.setsvalue2s (L, L.top, ts);
            imp.api_incr_top (L);
            imp.lua_unlock (L);
            return imp.getstr (ts);
        }


        public static string lua_pushlstring (lua_State L, string s) {
            if (s == null) {
                lua_pushnil (L);
                return null;
            }
            else {
                imp.lua_lock (L);
                imp.luaC_checkGC (L);
                TString ts = imp.luaS_new (L, s);
                imp.setsvalue2s (L, L.top, ts);
                imp.api_incr_top (L);
                imp.lua_unlock (L);
                return imp.byte2str (imp.getstr (ts));
            }
        }


        public static void lua_pushcclosure (lua_State L, lua_CFunction fn, int n) {
            imp.lua_lock (L);
            if (n == 0) {
                imp.setfvalue (L, L.top, fn);
            }
            else {
                imp.api_checknelems (L, n);
                imp.api_check (n <= imp.MAXUPVAL, "upvalue index too large");
                imp.luaC_checkGC (L);
                CClosure cl = imp.luaF_newCclosure (L, n);
                cl.f = fn;
                L.top -= n;
                while (n-- != 0) {
                    imp.setobj2n (L, cl.upvalue[n], L.top + n);
                    /* does not need barrier because closure is white */
                }
                imp.setclCvalue (L, L.top, cl);
            }
            imp.api_incr_top (L);
            imp.lua_unlock (L);
        }


        public static void lua_pushboolean (lua_State L, int b) {
            imp.lua_lock (L);
            imp.setbvalue (L, L.top, b);  /* ensure that true is 1 */
            imp.api_incr_top (L);
            imp.lua_unlock (L);
        }


        public static void lua_pushlightuserdata (lua_State L, object p) {
            imp.lua_lock (L);
            imp.setpvalue (L, L.top, p);
            imp.api_incr_top (L);
            imp.lua_unlock (L);
        }


        public static int lua_pushthread (lua_State L) {
            imp.lua_lock (L);
            imp.setthvalue (L, L.top, L);
            imp.api_incr_top (L);
            imp.lua_unlock (L);
            return (imp.G (L).mainthread == L ? 1 : 0);
        }




        /*
        ** get functions (Lua -> stack)
        */


        public static int lua_getglobal (lua_State L, string name) {
            Table reg = imp.hvalue (imp.G (L).l_registry);
            imp.lua_lock (L);
            TValue gt = imp.luaH_getint (reg, LUA_RIDX_GLOBALS);
            imp.setsvalue2s (L, L.top++, imp.luaS_new (L, name));
            imp.luaV_gettable (L, gt, L.top - 1, L.top - 1);
            imp.lua_unlock (L);
            return imp.ttnov (L, L.top - 1);
        }


        public static int lua_gettable (lua_State L, int idx) {
            imp.lua_lock (L);
            TValue t = imp.index2addr (L, idx);
            imp.luaV_gettable (L, t, L.top - 1, L.top - 1);
            imp.lua_unlock (L);
            return imp.ttnov (L, L.top - 1);
        }


        public static int lua_getfield (lua_State L, int idx, string k) {
            imp.lua_lock (L);
            TValue t = imp.index2addr (L, idx);
            imp.setsvalue2s (L, L.top, imp.luaS_new (L, k));
            imp.api_incr_top (L);
            imp.luaV_gettable (L, t, L.top - 1, L.top - 1);
            imp.lua_unlock (L);
            return imp.ttnov (L, L.top - 1);
        }


        public static int lua_geti (lua_State L, int idx, long n) {
            imp.lua_lock (L);
            TValue t = imp.index2addr (L, idx);
            imp.setivalue (L, L.top, n);
            imp.api_incr_top (L);
            imp.luaV_gettable (L, t, L.top - 1, L.top - 1);
            imp.lua_unlock (L);
            return imp.ttnov (L, L.top - 1);
        }


        public static int lua_rawget (lua_State L, int idx) {
            imp.lua_lock (L);
            TValue t = imp.index2addr (L, idx);
            imp.api_check (imp.ttistable (t), "table expected");
            imp.setobj2s (L, L.top - 1, imp.luaH_get (imp.hvalue (t), L.stack[L.top - 1]));
            imp.lua_unlock (L);
            return imp.ttnov (L, L.top - 1);
        }


        public static int lua_rawgeti (lua_State L, int idx, long n) {
            imp.lua_lock (L);
            TValue t = imp.index2addr (L, idx);
            imp.api_check (imp.ttistable (t), "table expected");
            imp.setobj2s (L, L.top, imp.luaH_getint (imp.hvalue (t), n));
            imp.api_incr_top (L);
            imp.lua_unlock (L);
            return imp.ttnov (L, L.top - 1);
        }


        public static int lua_rawgeti (lua_State L, int idx, object p) {
            imp.lua_lock (L);
            TValue t = imp.index2addr (L, idx);
            imp.api_check (imp.ttistable (t), "table expected");
            TValue k = new TValue ();
            imp.setpvalue (k, p);
            imp.setobj2s (L, L.top, imp.luaH_get (imp.hvalue (t), k));
            imp.api_incr_top (L);
            imp.lua_unlock (L);
            return imp.ttnov (L, L.top - 1);
        }


        public static void lua_createtable (lua_State L, int narray, int nrec) {
            imp.lua_lock (L);
            imp.luaC_checkGC (L);
            Table t = imp.luaH_new (L);
            imp.sethvalue (L, L.top, t);
            imp.api_incr_top (L);
            if (narray > 0 || nrec > 0)
                imp.luaH_resize (L, t, narray, nrec);
            imp.lua_unlock (L);
        }


        public static int lua_getmetatable (lua_State L, int objindex) {
            imp.lua_lock (L);
            int res = 0;
            TValue obj = imp.index2addr (L, objindex);
            Table mt = null;
            switch (imp.ttnov (obj)) {
                case LUA_TTABLE:
                    mt = imp.hvalue (obj).metatable;
                    break;
                case LUA_TUSERDATA:
                    mt = imp.uvalue (obj).metatable;
                    break;
                default:
                    mt = imp.G (L).mt[imp.ttnov (obj)];
                    break;
            }
            if (mt != null) {
                imp.sethvalue (L, L.top, mt);
                imp.api_incr_top (L);
                res = 1;
            }
            imp.lua_unlock (L);
            return res;
        }


        public static int lua_getuservalue (lua_State L, int idx) {
            imp.lua_lock (L);
            TValue o = imp.index2addr (L, idx);
            imp.getuservalue (L, imp.uvalue (o), L.top);
            imp.api_incr_top (L);
            imp.lua_unlock (L);
            return imp.ttnov (L, L.top - 1);
        }












    }
}
