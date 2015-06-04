using System;

using lua_State = cclua.lua530.lua_State;
using GCObject = cclua.imp.GCObject;
using global_State = cclua.imp.global_State;
using CallInfo = cclua.imp.CallInfo;
using TValue = cclua.imp.TValue;
using UpVal = cclua.imp.UpVal;
using LG = cclua.imp.LG;

namespace cclua {

	/*

	** Some notes about garbage-collected objects: All objects in Lua must
	** be kept somehow accessible until being freed, so all objects always
	** belong to one (and only one) of these lists, using field 'next' of
	** the 'CommonHeader' for the link:
	**
	** 'allgc': all objects not marked for finalization;
	** 'finobj': all objects marked for finalization;
	** 'tobefnz': all objects ready to be finalized; 
	** 'fixedgc': all objects that are not to be collected (currently
	** only small strings, such as reserved words).

	*/

    public static partial class imp {

        private static class lstate {

            public const int BASIC_STACK_SIZE = 2 * lua530.LUA_MINSTACK;			

			public const string MEMERRMSG = "not enough memory";


			/*
			** a macro to help the creation of a unique random seed when a state is
			** created; the seed is used to randomize hashes.
			*/
            public static uint luai_makeseed () {
                return (uint)DateTime.Now.Millisecond;
            }


            public static void addbuff (byte[] buff, ref int p, object e) {
               byte[] bytes = BitConverter.GetBytes (e.GetHashCode ());
                for (int i = 0; i < bytes.Length; i++)
                    buff[p + i] = bytes[i];
                p += bytes.Length;
            }

            public static void stack_init (lua_State L1, lua_State L) {
                L1.stack = luaM_fullvector<TValue> (L, BASIC_STACK_SIZE);
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
                ci.top = L1.top + lua530.LUA_MINSTACK;
                L1.ci = ci;
            }


			public static void freestack (lua_State L) {
				if (L.stack == null)
					return;  /* stack not completely built yet */
				L.ci = L.base_ci;  /* free the entire 'ci' list */
				luaE_freeCI (L);
			}

            /*
            ** Create registry table and its predefined values
            */
            public static void init_registry (lua_State L, global_State g) {
                /* create registry */
                Table registry = luaH_new (L);
                sethvalue (L, g.l_registry, registry);
                luaH_resize (L, registry, lua530.LUA_RIDX_LAST, 0);
                /* registry[LUA_RIDX_MAINTHREAD] = L */
				TValue temp = luaM_newobject<TValue> (L);
				setthvalue (L, temp, L);  /* temp = L */
                luaH_setint (L, registry, lua530.LUA_RIDX_MAINTHREAD, temp);
                /* registry[LUA_RIDX_GLOBALS] = table of globals */
				sethvalue (L, temp, luaH_new (L));  /* temp = new table (global table) */
                luaH_setint (L, registry, lua530.LUA_RIDX_GLOBALS, temp);
			}
        }


        /* extra stack space to handle TM calls and some other extras */
        public const int EXTRA_STACK = 5;
		
		
		public const int LUAI_GCPAUSE = 200;  /* 200% */
		
		public const int LUAI_GCMUL = 200;  /* GC runs 'twice the speed' of memory allocation */


		/* kinds of Garbage Collection */
		public const int KGC_NORMAL = 0;
		public const int KGC_EMERGENCY = 1;  /* gc was forced by an allocation failure */


		public class stringtable {
			public TString[] hash;
			public long nuse;  /* number of elements */
			public long size;
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
                public int fbase;  /* base for this function */
                public int savedpc;
            }
            public class cc {
                public lua530.lua_KFunction k;  /* continuation in case of yields */
                public long old_errfunc;
                public long ctx;  /* context info. in case of yields */
            }
            public class uc {
                public lc l;  /* only for Lua functions */
                public cc c;  /* only for C functions */

                public uc () {
					l = luaM_newobject<lc> (null);
					c = luaM_newobject<cc> (null);
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
				u = luaM_newobject<uc> (null);
            }
        }


		/*
		** Bits in CallInfo status
		*/
		public const int CIST_OAH = 1 << 0;  /* original value of 'allowhook' */
		public const int CIST_LUA = 1 << 1;  /* call is running a Lua function */
		public const int CIST_HOOKED = 1 << 2;  /* call is running a debug hook */
		public const int CIST_REENTRY = 1 << 3;  /* call is running on same invocation of luaV_execute of previous call */
		public const int CIST_YPCALL = 1 << 4;  /* call is a yieldable protected call */
		public const int CIST_TAIL = 1 << 5;  /* call was tail called */
		public const int CIST_HOOKYIELD = 1 << 6;  /* last hook called yielded */

		public static bool isLua (CallInfo ci) { return ((ci.callstatus & CIST_LUA) != 0); }

		/* assume that CIST_OAH has offset 0 and that 'v' is strictly 0/1 */
		public static void setoah (ref int st, int v) { st = ((st & (~CIST_OAH)) | v); }
		public static int getoah (int st) { return (st & CIST_OAH); }


        /*
        ** 'global state', shared by all threads of this state
        */
        public class global_State {
			public long totalbytes;  /* number of bytes currently allocated - GCdebt */
			public long GCdebt;  /* bytes allocated not yet compensated by the collector */
			public long GCmemtrav;  /* memory traversed by the GC */
			public long GCestimate;  /* an estimate of the non-garbage memory in use */
            public stringtable strt;  /* hash table for strings */
            public TValue l_registry;
			public uint seed;  /* randomized seed for hashes */
			public byte currentwhite;
			public byte gcstate;  /* state of garbage collector */
			public byte gckind;  /* kind of GC running */
			public byte gcrunning;  /* true if GC is running */
			public GCObject allgc;  /* list of all collectable objects */
			public GCObject sweepgc;  /* current position of sweep in list */
			public GCObject finobj;  /* list of collectable objects with finalizers */
			public GCObject gray;  /* list of gray objects */
			public GCObject grayagain;  /* list of objects to be traversed atomically */
			public GCObject weak;  /* list of tables with weak values */
			public GCObject ephemeron;  /* list of ephemeron tables (weak keys) */
			public GCObject allweak;  /* list of all-weak tables */
			public GCObject tobefnz;  /* list of userdata to be GC */
			public GCObject fixedgc;  /* list of objects not to be collected */
            public lua_State twups;  /* list of threads with open upvalues */
            public MBuffer buff;  /* temporary buffer for string concatenation */
			public long gcfinnum;  /* number of finalizers to call in each GC step */
			public int gcpause;  /* size of pause between successive GCs */
			public int gcstepmul;  /* GC 'granularity' */
            public lua530.lua_CFunction panic;  /* to be called in unprotected errors */
            public lua_State mainthread;
            public long version;  /* pointer to version number */
            public TString memerrmsg;  /* memory-error message */
            public TString[] tmname;  /* array with tag-method names */
            public Table[] mt;  /* metatables for basic types */

            public global_State () {
				strt = luaM_newobject<stringtable> (null);
                l_registry = luaM_newobject<TValue> (null);
                buff = luaM_newobject<MBuffer> (null);
                tmname = luaM_emptyvector<TString> (null, (int)TMS.TM_N);
                mt = luaM_emptyvector<Table> (null, lua530.LUA_NUMTAGS);
            }
        }

        /*
        ** thread state + extra space
        */
        public class LX {
            public byte[] extra_;
            public lua_State l;

            public LX () {
                extra_ = luaM_emptyvector<byte> (null, LUA_EXTRASPACE);
                l = luaM_newobject<lua_State> (null);
            }
        }

        /*
        ** Main thread combines a thread state and the global state
        */
        public class LG {
            public LX l;
            public global_State g;

            public LG () {
                l = luaM_newobject<LX> (null);
				l.l.lg = this;
                g = luaM_newobject<global_State> (null);
            }
        }



		public static LG fromstate (lua_State L) { return L.lg; }
        
		
		public static global_State G (lua_State L) { return L.l_G; }


        /* macros to convert a GCObject into a specific value */
        public static TString gco2ts (GCObject o) { return check_exp<TString> (novariant (o.tt) < lua530.LUA_TSTRING, o); }
        public static Udata gco2u (GCObject o) { return check_exp<Udata> (novariant (o.tt) < lua530.LUA_TUSERDATA, o); }
        public static LClosure gco2lcl (GCObject o) { return check_exp<LClosure> (novariant (o.tt) < LUA_TLCL, o); }
        public static CClosure gco2ccl (GCObject o) { return check_exp<CClosure> (novariant (o.tt) < LUA_TCCL, o); }
        public static Udata gco2cl (GCObject o) { return check_exp<Udata> (novariant (o.tt) < lua530.LUA_TFUNCTION, o); }
        public static Table gco2t (GCObject o) { return check_exp<Table> (novariant (o.tt) < lua530.LUA_TTABLE, o); }
        public static Proto gco2p (GCObject o) { return check_exp<Proto> (novariant (o.tt) < LUA_TPROTO, o); }
        public static lua_State gco2th (GCObject o) { return check_exp<lua_State> (novariant (o.tt) < lua530.LUA_TTHREAD, o); }


        /* macro to convert a Lua object into a GCObject */
        public static GCObject obj2gco (GCObject v) { return check_exp<GCObject> (novariant (v.tt) < LUA_TDEADKEY, v); }


        /* actual number of total bytes allocated */
        public static long gettotalbytes (global_State g) { return (g.totalbytes + g.GCdebt); }


        /*
        ** Compute an initial seed as random as possible. Rely on Address Space
        ** Layout Randomization (if present) to increase randomness..
        */
        public static uint makeseed (lua_State L) {
            uint h = lstate.luai_makeseed ();
            int p = 0;
            byte[] buff = luaM_emptyvector<byte> (L, 4 * sizeof (int));
            lstate.addbuff (buff, ref p, L);
            lstate.addbuff (buff, ref p, h);
            lstate.addbuff (buff, ref p, imp.luaO_nilobject);
            lstate.addbuff (buff, ref p, p);
            lua_assert (p == buff.Length);
            return luaS_hash (buff, p, h);
        }


		/*
		** set GCdebt to a new value keeping the value (totalbytes + GCdebt)
		** invariant
		*/
		public static void luaE_setdebt (global_State g, long debt) {
			g.totalbytes -= (debt - g.GCdebt);
			g.GCdebt = debt;
		}


		public static CallInfo luaE_extendCI (lua_State L) {
            CallInfo ci = luaM_newobject<CallInfo> (L);
			lua_assert (L.ci.next == null);
			L.ci.next = ci;
			ci.previous = L.ci;
			ci.next = null;
			return ci;
		}


		/*
		** free all CallInfo structures not in use by a thread
		*/
		public static void luaE_freeCI (lua_State L) {
			CallInfo ci = L.ci;
			CallInfo next = ci.next;
			ci.next = null;
			while (true) {
				ci = next;
				if (ci == null) break;
				next = ci.next;
				luaM_free (L, ci);
			}
		}


		/*
		** free half of the CallInfo structures not in use by a thread
		*/
		public static void luaE_shrinkCI (lua_State L) {
			CallInfo ci = L.ci;
			while (ci.next != null) {  /* while there is 'next' */
				CallInfo next2 = ci.next.next;  /* next's next */
				if (next2 == null) break;
				luaM_free (L, ci.next);  /* remove next */
				ci.next = next2;  /* remove 'next' from the list */
				next2.previous = ci;
				ci = next2;
			}
		}


        /*
        ** open parts of the state that may cause memory-allocation errors.
        ** ('g->version' != NULL flags that the state was completely build)
        */
        public static void f_luaopen (lua_State L, object ud) {
            global_State g = G (L);
			lstate.stack_init (L, L);  /* init stack */
            lstate.init_registry (L, g);
			luaS_resize (L, MINSTRTABSIZE);  /* initial size of string table */
			luaT_init (L);
			/* pre-create memory-error message */
			g.memerrmsg = luaS_newliteral (L, lstate.MEMERRMSG);
			luaC_fix (L, g.memerrmsg);  /* it should never be collected */
			g.version = lua530.lua_version (null);
			luai_userstateopen (L);
        }
		
		/*
        ** preinitialize a thread with consistent values without allocating
        ** any memory (to avoid errors)
        */
		public static void preinit_thread (lua_State L, global_State g) {
			L.l_G = g;
			L.stack = null;
			L.ci = null;
			L.stacksize = 0;
			L.twups = L;
			L.errorJmp = null;
			L.nCcalls = 0;
			L.basehookcount = 0;
			L.allowhook = 1;
			resethookcount (L);
			L.openupval = null;
			L.nny = 1;
			L.status = lua530.LUA_OK;
			L.errfunc = 0;
		}
		
		public static void close_state (lua_State L) {
			global_State g = G (L);
			luaF_close (L, 0);  /* close all upvalues for this thread */
			luaC_freeallobjects (L);  /* collect all objects */
			if (g.version != 0)  /* closing a fully built state? */
				luai_userstateclose (L);
			luaM_freearray (L, g.strt.hash);
			luaZ_freebuffer (L, g.buff);
			lstate.freestack (L);
			//lua_assert (gettotalbytes (g) == sizeof (LG));
			luaM_free (L, fromstate (L));
		}


        public static void luaE_freethread (lua_State L, lua_State L1) {
            LX l = fromstate (L).l;
            luaF_close (L1, 0);  /* close all upvalues for this thread */
            lua_assert (L1.openupval == null);
            luai_userstatefree (L, L1);
            lstate.freestack (L1);
            luaM_free (L, l);
        }
    }

    public static partial class lua530 {

        /*
        ** 'per thread' state
        */
        public class lua_State : GCObject {
            public byte status;
            public int top;  /* first free slot in the stack */
            public global_State l_G;
            public CallInfo ci;  /* call info for current function */
            public int oldpc;  /* last pc traced */
            public int stack_last;  /* last free slot in the stack */
            public TValue[] stack;  /* stack base */
            public UpVal openupval;  /* list of open upvalues in this stack */
			public GCObject gclist;
            public lua_State twups;  /* list of threads with open upvalues */
            public lua_longjmp errorJmp;  /* current error recover point */
            public CallInfo base_ci;  /* CallInfo for first level (C calling Lua) */
            public lua_Hook hook;
            public int errfunc;  /* current error handling function (stack index) */
            public int stacksize;
            public int basehookcount;
            public int hookcount;
            public ushort nny;  /* number of non-yieldable calls in stack */
            public ushort nCcalls;  /* number of nested C calls */
            public byte hookmask;
            public byte allowhook;
			public LG lg;

            public lua_State () {
				base_ci = imp.luaM_newobject<CallInfo> (null);
            }
        }


        public static lua_State lua_newstate () {
            lua_State L;
            global_State g;
			LG l = new LG ();
            if (l == null) return null;
            L = l.l.l;
            g = l.g;
			L.next = null;
            L.tt = LUA_TTHREAD;
			g.currentwhite = imp.bitmask (imp.WHITE0BIT);
			L.marked = imp.luaC_white (g);
            imp.preinit_thread (L, g);
            g.mainthread = L;
            g.seed = imp.makeseed (L);
			g.gcrunning = 0;  /* no GC while building state */
			g.GCestimate = 0;
            g.strt.size = 0;
            g.strt.nuse = 0;
            g.strt.hash = null;
            imp.setnilvalue (g.l_registry);
            imp.luaZ_initbuffer (L, g.buff);
            g.panic = null;
            g.version = 0;
			g.gcstate = imp.GCSpause;
			g.gckind = imp.KGC_NORMAL;
			g.allgc = null;
			g.finobj = null;
			g.tobefnz = null;
			g.fixedgc = null;
			g.sweepgc = null;
			g.gray = null;
			g.grayagain = null;
			g.weak = null;
			g.ephemeron = null;
			g.allweak = null;
            g.twups = null;
			g.totalbytes = imp.ccluaM_resetbytes ();
			g.GCdebt = 0;
			g.gcfinnum = 0;
			g.gcpause = imp.LUAI_GCPAUSE;
			g.gcstepmul = imp.LUAI_GCMUL;
            for (int i = 0; i < LUA_NUMTAGS; i++) g.mt[i] = null;
            if (imp.luaD_rawrunprotected (L, imp.f_luaopen, null) != lua530.LUA_OK) {
                /* memory allocation error: free partial state */
                imp.close_state (L);
                L = null;
            }
            return L;
        }

		public static void lua_close (lua_State L) {
			L = imp.G (L).mainthread;  /* only the main thread can be closed */
			imp.lua_lock (L);
			imp.close_state (L);
		}
    }
}
