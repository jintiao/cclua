using System;

using lua_State = cclua.lua530.lua_State;

namespace cclua {

	/*
	** Collectable objects may have one of three colors: white, which
	** means the object is not marked; gray, which means the
	** object is marked, but its references may be not marked; and
	** black, which means that the object and all its references are marked.
	** The main invariant of the garbage collector, while marking objects,
	** is that a black object can never point to a white one. Moreover,
	** any gray object must be in a "gray list" (gray, grayagain, weak,
	** allweak, ephemeron) so that it can be visited again before finishing
	** the collection cycle. These lists have no meaning when the invariant
	** is not being enforced (e.g., sweep phase).
	*/

    public static partial class imp {

		private static class lgc {

            public const int OBJ_SIZE = 32;
            public const int REF_SIZE = 8;

            /*
            ** internal state for collector while inside the atomic phase. The
            ** collector should never be in this state while running regular code.
            */
            public const int GCSinsideatomic = GCSpause + 1;


            /*
            ** cost of sweeping one element (the size of a small object divided
            ** by some adjust for the sweep speed)
            */
            public const int GCSWEEPCOST = (OBJ_SIZE + 4) / 4;

            /* maximum number of elements to sweep in each single step */
            public static int GCSWEEPMAX = ((GCSTEPSIZE / GCSWEEPCOST) / 4);

            /* cost of calling one finalizer */
            public const int GCFINALIZECOST = GCSWEEPCOST;


            /*
            ** macro to adjust 'stepmul': 'stepmul' is actually used like
            ** 'stepmul / STEPMULADJ' (value chosen by tests)
            */
            public const int STEPMULADJ = 200;


            /*
            ** macro to adjust 'pause': 'pause' is actually used like
            ** 'pause / PAUSEADJ' (value chosen by tests)
            */
            public const int PAUSEADJ = 100;



			public static void luaC_barrierback_ (lua_State L, Table t) {
			}


            /*
            ** Set a reasonable "time" to wait before starting a new GC cycle; cycle
            ** will start when memory use hits threshold. (Division by 'estimate'
            ** should be OK: it cannot be zero (because Lua cannot even start with
            ** less than PAUSEADJ bytes).
            */
            public static void setpause (global_State g) {
                long estimate = g.GCestimate / PAUSEADJ;  /* adjust 'estimate' */
                lua_assert (estimate > 0);
                long threshold = (g.gcpause < MAX_LMEM / estimate)  /* overflow? */
                                  ? estimate * g.gcpause  /* no overflow */
                                  : MAX_LMEM;  /* overflow; truncate to maximum */
                long debt = gettotalbytes (g) - threshold;
                luaE_setdebt (g, debt);
            }




            public static long singlestep (lua_State L) {
                global_State g = G (L);
                switch (g.gcstate) {
                    case GCSpause: {
                        g.GCmemtrav = g.strt.size * REF_SIZE;
                        restartcollection (g);
                        g.gcstate = GCSpropagate;
                        return g.GCmemtrav;
                    }
                    case GCSpropagate: {
                        g.GCmemtrav = 0;
                        lua_assert (g.gray != null);
                        propagatemark (g);
                        if (g.gray == null)  /* no more gray objects? */
                            g.gcstate = GCSatomic;  /* finish propagate phase */
                        return g.GCmemtrav;  /* memory traversed in this step */
                    }
                    case GCSatomic: {
                        propagateall (g);  /* make sure gray list is empty */
                        long work = atomic (L);  /* work is what was traversed by 'atomic' */
                        int sw = entersweep (L);
                        g.GCestimate = gettotalbytes (g);  /* first estimate */;
                        return work + sw * GCSWEEPCOST;
                    }
                    case GCSswpallgc: {  /* sweep "regular" objects */
                        return sweepstep (L, g, GCSswpfinobj, g.finobj);
                    }
                    case GCSswpfinobj: {  /* sweep objects with finalizers */
                        return sweepstep (L, g, GCSswptobefnz, g.tobefnz);
                    }
                    case GCSswptobefnz: {  /* sweep objects to be finalized */
                        return sweepstep (L, g, GCSswpend, null);
                    }
                    case GCSswpend: {  /* finish sweeps */
                        makewhite (g, g.mainthread);  /* sweep main thread */
                        checkSizes (L, g);
                        g.gcstate = GCScallfin;
                        return 0;
                    }
                    case GCScallfin: {  /* call remaining finalizers */
                        if (g.tobefnz != null && g.gckind != KGC_EMERGENCY) {
                            int n = runafewfinalizers (L);
                            return (n * GCFINALIZECOST);
                        }
                        else {  /* emergency mode or no more finalizers */
                            g.gcstate = GCSpause;  /* finish collection */
                            return 0;
                        }
                    }
                    default: {
                        lua_assert (false); return 0;
                    }
                }
            }


            /*
            ** get GC debt and convert it from Kb to 'work units' (avoid zero debt
            ** and overflows)
            */
            public static long getdebt (global_State g) {
                long debt = g.GCdebt;
                int stepmul = g.gcstepmul;
                debt = (debt / STEPMULADJ) + 1;
                debt = (debt < (MAX_LMEM / stepmul)) ? debt * stepmul : MAX_LMEM;
                return debt;
            }
		}

		/* how much to allocate before next GC step */
        public static int GCSTEPSIZE = 100 * 32;  /* ~100 small strings */


		/*
		** Possible states of the Garbage Collector
		*/
		public const int GCSpropagate = 0;
		public const int GCSatomic = 1;
		public const int GCSswpallgc = 2;
		public const int GCSswpfinobj = 3;
		public const int GCSswptobefnz = 4;
		public const int GCSswpend = 5;
		public const int GCScallfin = 6;
		public const int GCSpause = 7;


		public static bool issweepphase (global_State g) { return (GCSswpallgc <= g.gcstate && g.gcstate <= GCSswpend); }


		/*
		** macro to tell when main invariant (white objects cannot point to black
		** ones) must be kept. During a collection, the sweep
		** phase may break the invariant, as objects turned white may point to
		** still-black objects. The invariant is restored when sweep ends and
		** all objects are white again.
		*/
		public static bool keepinvariant (global_State g) { return (g.gcstate <= GCSatomic); }


		/*
		** some useful bit tricks
		*/
        public static void resetbits (ref byte x, byte m) { x &= (byte)(~m); }
        public static void setbits (ref byte x, byte m) { x |= m; }
        public static bool testbits (byte x, byte m) { return ((x & m) != 0); }
        public static byte bitmask (byte b) { return (byte)(1 << b); }
        public static byte bit2mask (byte b1, byte b2) { return (byte)(bitmask (b1) | bitmask (b2)); }
        public static void l_setbit (ref byte x, byte b) { setbits (ref x, bitmask (b)); }
        public static void resetbit (ref byte x, byte b) { resetbits (ref x, bitmask (b)); }
        public static bool testbit (byte x, byte b) { return testbits (x, bitmask (b)); }


		/* Layout for bit use in 'marked' field: */
        public const byte WHITE0BIT = 0;  /* object is white (type 0) */
        public const byte WHITE1BIT = 1;  /* object is white (type 1) */
        public const byte BLACKBIT = 2;  /* object is black */
        public const byte FINALIZEDBIT = 3;  /* object has been marked for finalization */
		/* bit 7 is currently used by tests (luaL_checkmemory) */

        public static byte WHITEBITS = bit2mask (WHITE0BIT, WHITE1BIT);


		public static bool iswhite (GCObject x) { return testbits (x.marked, WHITEBITS); }
		public static bool isblack (GCObject x) { return testbit (x.marked, BLACKBIT); }
		/* neither white nor black */
		public static bool isgray (GCObject x) { return (testbits (x.marked, (byte)(WHITEBITS | (bitmask (BLACKBIT)))) == false); }

		public static bool tofinalize (GCObject x) { return testbit (x.marked, FINALIZEDBIT); }

		public static int otherwhite (global_State g) { return (g.currentwhite ^ WHITEBITS); }
		public static bool isdeadm (int ow, int m) { return (((m ^ WHITEBITS) & ow) == 0); }
		public static bool isdead (global_State g, GCObject x) { return isdeadm (otherwhite (g), x.marked); }

		public static void changewhite (GCObject x) { x.marked ^= WHITEBITS; }
		public static void gray2black (GCObject x) { l_setbit (ref x.marked, BLACKBIT); }
		
		public static byte luaC_white (global_State g) { return (byte)(g.currentwhite & WHITEBITS); }


        public static void luaC_checkGC (lua_State L) { 
			if (G (L).GCdebt > 0) {
				luaC_step (L);
			}
			condchangemem (L);
		}


        public static void luaC_barrier (lua_State L, GCObject p, TValue v) {
			if (iscollectable (v) && isblack (p) && iswhite (gcvalue (v)))
				lgc.luaC_barrier_ (L, p, gcvalue (v));
        }

        public static void luaC_barrierback (lua_State L, GCObject p, TValue v) {
			if (iscollectable (v) && isblack (p) && iswhite (gcvalue (v)))
				luaC_barrierback_ (L, p, gcvalue (v));
		}
        public static void luaC_barrierback (lua_State L, GCObject p, int v) { luaC_barrierback (L, p, L.stack[v]); }

        public static void luaC_objbarrier (lua_State L, GCObject p, GCObject o) {
			if (isblack (p) && iswhite (o))
                luaC_barrier_ (L, obj2gco (p), obj2gco (o));
		}

        public static void luaC_upvalbarrier (lua_State L, UpVal uv) {
			if (iscollectable (uv.v) && upisopen (uv) == false)
				luaC_upvalbarrier_ (L, uv);
        }



        public static T luaC_newobj<T> (lua_State L, int tt) where T : GCObject, new () {
            T o = luaM_newobject<T> (L);
            o.tt = (byte)tt;
            return o;
        }

		public static void luaC_fix (lua_State L, GCObject o) {
			global_State g = G (L);
			o.next = g.fixedgc;
			g.fixedgc = o;
		}


		public static void luaC_freeallobjects (lua_State L) {
		}


        /*
        ** performs a basic GC step when collector is running
        */
        public static void luaC_step (lua_State L) {
            global_State g = G (L);
            long debt = lgc.getdebt (g);  /* GC deficit (be paid now) */
            if (g.gcrunning == 0) {  /* not running? */
                luaE_setdebt (g, -GCSTEPSIZE * 10);  /* avoid being called too often */
                return;
            }
            do {  /* repeat until pause or enough "credit" (negative debt) */
                long work = lgc.singlestep (L);  /* perform one single step */
                debt -= work;
            } while (debt > -GCSTEPSIZE && g.gcstate != GCSpause);
            if (g.gcstate == GCSpause)
                lgc.setpause (g);  /* pause until next cycle */
            else {
                debt = (debt / g.gcstepmul) * lgc.STEPMULADJ;  /* convert 'work units' to Kb */
                luaE_setdebt (g, debt);
                lgc.runafewfinalizers (L);
            }
        }








    }
}
