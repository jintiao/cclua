using System;

namespace cclua53
{
    public static partial class imp {
        private static class lstate {

            /* extra stack space to handle TM calls and some other extras */
            public const int EXTRA_STACK = 5;

            public const int BASIC_STACK_SIZE = 2 * cclua.LUA_MINSTACK;

            public static void resethookcount (cclua.lua_State L) {
                L.hookcount = L.basehookcount;
            }

            public static uint luai_makeseed () {
                return (uint)DateTime.Now.Millisecond;
            }

            public static void addbuff (byte[] buff, ref int p, object e) {
               byte[] bytes = BitConverter.GetBytes (e.GetHashCode ());
                for (int i = 0; i < bytes.Length; i++)
                    buff[p + i] = bytes[i];
                p += bytes.Length;
            }

            public static void stack_init (cclua.lua_State L1, cclua.lua_State L) {
                L1.stack = luaM_newvector<TValue> (L, BASIC_STACK_SIZE);
                L1.stacksize = BASIC_STACK_SIZE;
                for (int i = 0; i < BASIC_STACK_SIZE; i++)
                    setnilvalue (L1.stack[i]);  /* erase new stack */
                L1.top = 0;
                L1.stack_last = L1.stacksize - EXTRA_STACK;
                /* initialize first ci */
                CallInfo ci = L1.base_ci;
                ci.next = null;
                ci.previous = null;
                ci.callstatus = 0;
                ci.func = L1.top;
                setnilvalue (L1.stack[L1.top++]);  /* 'function' entry for this 'ci' */
                ci.top = L1.top + cclua.LUA_MINSTACK;
                L1.ci = ci;
            }

            /*
            ** Create registry table and its predefined values
            */
            public static void init_registry (cclua.lua_State L, global_State g) {
                /* create registry */
                Table registry = luaH_new (L);
                sethvalue (L, g.l_registry, registry);
                luaH_resize (L, registry, cclua.LUA_RIDX_LAST, 0);
                /* registry[LUA_RIDX_MAINTHREAD] = L */
                TValue temp = new TValue ();
                def.sethvalue (L, temp, L);
                module.luaH_setint (L, registry, cclua.LUA_RIDX_MAINTHREAD, temp);
                /* registry[LUA_RIDX_GLOBALS] = table of globals */
                def.sethvalue (L, temp, module.luaH_new (L));  /* temp = new table (global table) */
                module.luaH_setint (L, registry, cclua.LUA_RIDX_GLOBALS, temp);
            }
        }



        /*
        ** Information about a call.
        ** When a thread yields, 'func' is adjusted to pretend that the
        ** top function has only the yielded values in its stack; in that
        ** case, the actual 'func' value is saved in field 'extra'. 
        ** When a function calls another with a continuation, 'extra' keeps
        ** the function index so that, in case of errors, the continuation
        ** function can be called with the correct top.
        */
        public class CallInfo {
            public class lc {
                public uint fbase;  /* base for this function */
                public ulong[] savedpc;
            }
            public class cc {
                public cclua.lua_KFunction k;  /* continuation in case of yields */
                public long old_errfunc;
                public long ctx;  /* context info. in case of yields */
            }
            public class uc {
                public lc l;  /* only for Lua functions */
                public cc c;  /* only for C functions */

                public uc () {
                    l = new lc ();
                    c = new cc ();
                }
            }

            public int func;  /* function index in the stack */
            public int top;  /* top for this function */
            public CallInfo previous;
            public CallInfo next;  /* dynamic call link */
            public uc u;
            public long extra;
            public short nresults;  /* expected number of results from this function */
            public byte callstatus;

            public CallInfo () {
                u = new uc ();
            }
        }

        public class stringtable {
            public TString[] hash;
            public long nuse;  /* number of elements */
            public long size;
        }

        /*
        ** 'global state', shared by all threads of this state
        */
        public class global_State {
            public stringtable strt;  /* hash table for strings */
            public TValue l_registry;
            public uint seed;  /* randomized seed for hashes */
            public cclua.lua_State twups;  /* list of threads with open upvalues */
            public MBuffer buff;  /* temporary buffer for string concatenation */
            public cclua.lua_CFunction panic;  /* to be called in unprotected errors */
            public cclua.lua_State mainthread;
            public double version;  /* pointer to version number */
            public TString memerrmsg;  /* memory-error message */
            public TString[] tmname;  /* array with tag-method names */
            public Table[] mt;  /* metatables for basic types */

            public global_State () {
                strt = new stringtable ();
                l_registry = new TValue ();
                buff = new MBuffer ();
                mt = new Table[cclua.LUA_NUMTAGS];
            }
        }

        /*
        ** thread state + extra space
        */
        public class LX {
            public byte[] extra_;
            public cclua.lua_State l;

            public LX () {
                extra_ = new byte[LUA_EXTRASPACE];
                l = new cclua.lua_State ();
            }
        }

        /*
        ** Main thread combines a thread state and the global state
        */
        public class LG {
            public LX l;
            public global_State g;

            public LG () {
                l = new LX ();
                g = new global_State ();
            }
        }
        
        public static global_State G (cclua.lua_State L) {
            return L.l_G;
        }

        /*
        ** preinitialize a thread with consistent values without allocating
        ** any memory (to avoid errors)
        */
        public static void preinit_thread (cclua.lua_State L, global_State g) {
            L.l_G = g;
            L.stack = null;
            L.ci = null;
            L.stacksize = 0;
            L.twups = L;
            L.errorJmp = null;
            L.nCcalls = 0;
            L.basehookcount = 0;
            L.allowhook = 1;
            lstate.resethookcount (L);
            L.openupval = null;
            L.nny = 1;
            L.status = cclua.LUA_OK;
            L.errfunc = 0;
        }

        /*
        ** Compute an initial seed as random as possible. Rely on Address Space
        ** Layout Randomization (if present) to increase randomness..
        */
        public static uint makeseed (cclua.lua_State L) {
            uint h = lstate.luai_makeseed ();
            int p = 0;
            byte[] buff = new byte[4 * sizeof (int)];
            lstate.addbuff (buff, ref p, L);
            lstate.addbuff (buff, ref p, h);
            lstate.addbuff (buff, ref p, imp.luaO_nilobject);
            lstate.addbuff (buff, ref p, p);
            lua_assert (p == buff.Length);
            return luaS_hash (buff, p, h);
        }

        /*
        ** open parts of the state that may cause memory-allocation errors.
        ** ('g->version' != NULL flags that the state was completely build)
        */
        public static void f_luaopen (cclua.lua_State L, object ud) {
            global_State g = G (L);
            lstate.stack_init (L, L);
            lstate.init_registry (L, g);
            luaS_resize (L, MINSTRTABSIZE);
        }

        public static void close_state (cclua.lua_State L) {
        }

		/* macro to convert a Lua object into a GCObject */
		public static GCObject obj2gco (GCObject x) {
			return x;
		}
    }


    public static partial class cclua {

        /*
        ** 'per thread' state
        */
        public class lua_State : imp.GCObject {
            public byte status;
            public int top;  /* first free slot in the stack */
            public imp.global_State l_G;
            public imp.CallInfo ci;  /* call info for current function */
            public ulong oldpc;  /* last pc traced */
            public int stack_last;  /* last free slot in the stack */
            public imp.TValue[] stack;  /* stack base */
            public imp.UpVal openupval;  /* list of open upvalues in this stack */
            public lua_State twups;  /* list of threads with open upvalues */
            public imp.lua_longjmp errorJmp;  /* current error recover point */
            public imp.CallInfo base_ci;  /* CallInfo for first level (C calling Lua) */
            public cclua.lua_Hook hook;
            public long errfunc;  /* current error handling function (stack index) */
            public int stacksize;
            public int basehookcount;
            public int hookcount;
            public ushort nny;  /* number of non-yieldable calls in stack */
            public ushort nCcalls;  /* number of nested C calls */
            public byte hookmask;
            public byte allowhook;

            public lua_State () {
                base_ci = new imp.CallInfo ();
            }
        }

        public static lua_State lua_newstate () {
            lua_State L;
            imp.global_State g;
            imp.LG l = imp.luaM_newobject<imp.LG> ();
            if (l == null) return null;
            L = l.l.l;
            g = l.g;
            L.tt = LUA_TTHREAD;
            imp.preinit_thread (L, g);
            g.mainthread = L;
            g.seed = imp.makeseed (L);
            g.strt.size = 0;
            g.strt.nuse = 0;
            g.strt.hash = null;
            imp.setnilvalue (g.l_registry);
            imp.luaZ_initbuffer (L, g.buff);
            g.panic = null;
            g.version = 0;
            g.twups = null;
            for (int i = 0; i < LUA_NUMTAGS; i++) g.mt[i] = null;
            if (imp.luaD_rawrunprotected (L, imp.f_luaopen, null) != cclua.LUA_OK) {
                /* memory allocation error: free partial state */
                imp.close_state (L);
                L = null;
            }
            return L;
        }
    }
}
