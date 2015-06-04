using System;

using lua_State = cclua.lua530.lua_State;

namespace cclua {

    public static partial class imp {


        /* test whether thread is in 'twups' list */
        public static bool isintwups (lua_State L) { return (L.twups != L); }

        /*
        ** Upvalues for Lua closures
        */
        public class UpVal {
            public class openc {
                public UpVal next;  /* linked list */
                public int touched;  /* mark to avoid cycles with dead threads */
            }
            public class uc {
                public openc open;  /* (when open) */
                public TValue value;  /* the value (when closed) */

                public uc () {
                    open = new openc ();
                    value = new TValue ();
                }
            }

            public TValue v;  /* points to stack or to its own value */
            public ulong refcount;  /* reference counter */
            public uc u;
            public int level;

            public UpVal () {
                u = new uc ();
            }
        }


        public static bool upisopen (UpVal up) { return (up.v != up.u.value); }



        public static CClosure luaF_newCclosure (lua_State L, int n) {
            CClosure c = luaC_newobj<CClosure> (L, LUA_TCCL);
            c.nupvalues = (byte)n;
            return c;
        }


        public static LClosure luaF_newLclosure (lua_State L, int n) {
            LClosure c = luaC_newobj<LClosure> (L, LUA_TLCL);
            c.p = null;
            c.nupvalues = (byte)n;
            c.upvalue = luaM_emptyvector<UpVal> (L, n);
            return c;
        }


        /*
        ** fill a closure with new closed upvalues
        */
        public static void luaF_initupvals (lua_State L, LClosure cl) {
            for (int i = 0; i < cl.nupvalues; i++) {
                UpVal uv = luaM_newobject<UpVal> (L);
                uv.refcount = 1;
                uv.v = uv.u.value;  /* make it closed */
                setnilvalue (uv.v);
                cl.upvalue[i] = uv;
            }
        }


        public static UpVal luaF_findupval (lua_State L, int level) {
            TValue sl = L.stack[level];
            UpVal pp = L.openupval;
            UpVal p;
            lua_assert (isintwups (L) || L.openupval == null);
            while (pp != null && pp.level >= level) {
                p = pp;
                lua_assert (upisopen (p));
                if (p.level == level)  /* found a corresponding upvalue? */
                    return p;  /* return it */
                pp = p.u.open.next;
            }

            /* not found: create a new upvalue */
            UpVal uv = luaM_newobject<UpVal> (L);
            uv.refcount = 0;
            uv.u.open.next = pp.u.open.next;  /* link it to list of open upvalues */
            pp.u.open.next = uv;
            uv.u.open.touched = 1;
            uv.v = sl;  /* current value lives in the stack */
            uv.level = level;
            if (isintwups (L) == false) {  /* thread not in list of threads with upvalues? */
                L.twups = G (L).twups;  /* link it to the list */
                G (L).twups = L;
            }
            return uv;
        }


        public static void luaF_close (lua_State L, int level) {
            while (L.openupval != null && L.openupval.level >= level) {
                UpVal uv = L.openupval;
                lua_assert (upisopen (uv));
                L.openupval = uv.u.open.next;  /* remove from 'open' list */
                if (uv.refcount == 0)  /* no references? */
                    luaM_free (L, uv);  /* free upvalue */
                else {
                    setobj (L, uv.u.value, uv.v);  /* move value to upvalue slot */
                    uv.v = uv.u.value;  /* now current value lives here */
                    luaC_upvalbarrier (L, uv);
                }
                
            }
		}


        public static Proto luaF_newproto (lua_State L) {
            Proto f = luaC_newobj<Proto> (L, LUA_TPROTO);
            return f;
        }


        public static void luaF_freeproto (lua_State L, Proto f) {
            luaM_freearray (L, f.code);
            luaM_freearray (L, f.p);
            luaM_freearray (L, f.k);
            luaM_freearray (L, f.lineinfo);
            luaM_freearray (L, f.locvars);
            luaM_freearray (L, f.upvalues);
            luaM_free (L, f);
        }
    }
}
