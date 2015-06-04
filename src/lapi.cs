using System;

using lua_State = cclua.lua530.lua_State;
using CallInfo = cclua.imp.CallInfo;
using TValue = cclua.imp.TValue;

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


        public static bool lua_rawequal (lua_State L, int index1, int index2) {
            TValue o1 = imp.index2addr (L, index1);
            TValue o2 = imp.index2addr (L, index2);
            return (imp.isvalid (o1) && imp.isvalid (o2)) ? imp.luaV_rawequalobj (o1, o2) : false;
        }




        /*
        ** push functions (C -> stack)
        */
        public static void lua_pushinteger (lua_State L, long n) {
            imp.lua_lock (L);
            imp.setivalue (L, L.top, n);
            imp.api_incr_top (L);
            imp.lua_unlock (L);
        }










    }
}
