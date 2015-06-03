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


			/*
			** 'makewhite' erases all color bits then sets only the current white
			** bit
			*/
			public const int maskcolors = (~(bitmask(BLACKBIT) | WHITEBITS));

			public static void makewhite (global_State g, GCObject x) {
				x.marked = (byte)((x.marked & maskcolors) | luaC_white (g));
			}

			public static void white2gray (GCObject x) { resetbits (x.marked, WHITEBITS); }
			public static void black2gray (GCObject x) { resetbit (x.marked, BLACKBIT); }


			public static bool valiswhite (TValue x) { return (iscollectable (x) && iswhite (gcvalue (x))); }

			public static void checkdeadkey (Node n) { lua_assert (ttisdeadkey (n.i_key.tvk) == false || ttisnil (n.i_val)); }


			public static void checkconsistency (TValue obj) { lua_longassert (iscollectable (obj) == false || righttt (obj)); }


			public static void markvalue (global_State g, TValue o) {
				checkconsistency (o);
				if (valiswhite (o)) reallymarkobject (g, gcvalue (o));
			}

			public static void markobject (global_State g, GCObject t) {
				if (t != null && iswhite (t)) reallymarkobject (g, obj2gco (t));
			}



			/*
			** {======================================================
			** Generic functions
			** =======================================================
			*/




			/*
			** if key is not marked, mark its entry as dead (therefore removing it
			** from the table)
			*/
			public static void removeentry (Node n) {
				lua_assert (ttisnil (n.i_val));
				if (valiswhite (n.i_key.tvk))
					setdeadvalue (n.i_key.tvk);  /* unused and unmarked key; remove it */
			}


			/*
			** tells whether a key or value can be cleared from a weak
			** table. Non-collectable objects are never removed from weak
			** tables. Strings behave as 'values', so are never removed too. for
			** other objects: if really collected, cannot keep them; for objects
			** being finalized, keep them in keys, but not in values
			*/
			public static bool iscleared (global_State g, TValue o) {
				if (iscollectable (o) == false) return false;
				else if (ttisstring (o)) {
					markobject (g, tsvalue (o));  /* strings are 'values', so are never weak */
					return false;
				}
				else return iswhite (gcvalue (o));
			}


			/*
			** barrier that moves collector forward, that is, mark the white object
			** being pointed by a black object. (If in sweep phase, clear the black
			** object to white [sweep it] to avoid other barrier calls for this
			** same object.)
			*/
			public static void luaC_barrier_ (lua_State L, GCObject o, GCObject v) {
				global_State g = G (L);
				lua_assert (isblack (o) && iswhite (v) && isdead (g, v) == false && isdead (g, o) == false);
				if (keepinvariant (g))  /* must keep invariant? */
					reallymarkobject (g, v);  /* restore invariant */
				else {  /* sweep phase */
					lua_assert (issweepphase (g));
					makewhite (g, o);  /* mark main obj. as white to avoid other barriers */
				}
			}


			/*
			** barrier that moves collector backward, that is, mark the black object
			** pointing to a white object as gray again.
			*/
			public static void luaC_barrierback_ (lua_State L, Table t) {
				global_State g = G (L);
				lua_assert (isblack (t) && isdead (g, t) == false);
				black2gray (t);  /* make table gray (again) */
				t.gclist = g.grayagain;
				g.grayagain = obj2gco (t);
			}


			/*
			** barrier for assignments to closed upvalues. Because upvalues are
			** shared among closures, it is impossible to know the color of all
			** closures pointing to it. So, we assume that the object being assigned
			** must be marked.
			*/
			public static void luaC_upvalbarrier_ (lua_State L, UpVal uv) {
				global_State g = G (L);
				GCObject o = gcvalue (uv.v);
				lua_assert (upisopen (uv) == false);  /* ensured by macro luaC_upvalbarrier */
				if (keepinvariant (g))
					markobject (g, o);
			}



			/*
			** {======================================================
			** Mark functions
			** =======================================================
			*/


			/*
			** mark an object. Userdata, strings, and closed upvalues are visited
			** and turned black here. Other objects are marked gray and added
			** to appropriate list to be visited (and turned black) later. (Open
			** upvalues are already linked in 'headuv' list.)
			*/


			public static void reallymarkobject (global_State g, GCObject o) {
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

			
			/*
			** Enter first sweep phase.
			** The call to 'sweeptolive' makes pointer point to an object inside
			** the list (instead of to the header), so that the real sweep do not
			** need to skip objects created between "now" and the start of the real
			** sweep.
			** Returns how many objects it swept.
			*/
			public static int entersweep (lua_State L) {
				global_State g = G (L);
				g.gcstate = GCSswpallgc;
				lua_assert (g.sweepgc == null);
				int n = 0;
				g.sweepgc = sweeptolive (L, g.allgc, ref n);
				return n;
			}


			public static void atomic (lua_State L) {
			}


			public static void sweepstep (lua_State L, global_State g, int nextstate, GCObject nextlist) {
				if (g.sweepgc != 0) {
					long olddebt = g.GCdebt;
					g.sweepgc = sweeplist (L, g.sweepgc, GCSWEEPMAX);
					g.GCestimate += g.GCdebt - olddebt;  /* update estimate */
					if (g.sweepgc != null)  /* is there still something to sweep? */
						return (GCSWEEPMAX * GCSWEEPCOST);
				}
				/* else enter next state */
				g.gcstate = nextstate;
				g.sweepgc = nextlist;
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



		public static void luaC_fix (lua_State L, GCObject o) {
			global_State g = G (L);
			lua_assert (g.allgc == o);  /* object must be 1st in 'allgc' list! */
			lgc.white2gray (o);  /* they will be gray forever */
			g.allgc = o.next;  /* remove object from 'allgc' list */
			o.next = g.fixedgc;  /* link it to 'fixedgc' list */
			g.fixedgc = o;
		}


		/*
		** create a new collectable object (with given type and size) and link
		** it to 'allgc' list.
		*/
		public static T luaC_newobj<T> (lua_State L, int tt) where T : GCObject, new () {
			global_State g = G (L);
			T o = luaM_newobject<T> (L);
			o.marked = luaC_white (g);
			o.tt = (byte)tt;
			o.next = g.allgc;
			g.allgc = o;
			return o;
		}





		/*
		** if object 'o' has a finalizer, remove it from 'allgc' list (must
		** search the list to find it) and link it in 'finobj' list.
		*/
		public static void luaC_checkfinalizer (lua_State L, GCObject o, Table mt) {
			global_State g = G (L);
			if (tofinalize (o) ||  /* obj. is already marked... */
			    gfasttm (g, mt, TMS.TM_GC) == null)  /* or has no finalizer? */
				return;  /* nothing to be done */
			else {  /* move 'o' to 'finobj' list */
				if (issweepphase (g)) {
					makewhite (g, o);  /* "sweep" object 'o' */
					if (g.sweepgc == o.next)  /* should not remove 'sweepgc' object */
						g.sweepgc = sweeptolive (L, g.sweepgc, null);  /* change 'sweepgc' */
				}
				/* search for pointer pointing to 'o' */
				for (GCObject p = g.allgc; p != o; p = p.next) { /* empty */ }
				p = o.next;  /* remove 'o' from 'allgc' list */
				o.next = g.finobj;  /* link it in 'finobj' list */
				g.finobj = o;
				l_setbit (o.marked, FINALIZEDBIT);  /* mark it as such */
			}
		}



		/*
		** {======================================================
		** GC control
		** =======================================================
		*/



		public static void luaC_freeallobjects (lua_State L) {
			global_State g = G (L);
			separatetobefnz (g, 1);  /* separate all objects with finalizers */
			lua_assert (g.finobj == null);
			callallpendingfinalizers (L, 0);
			lua_assert (g.tobefnz == null);
			g.currentwhite = WHITEBITS;  /* this "white" makes all objects look dead */
			g.gckind = KGC_NORMAL;
			sweepwholelist (L, g.finobj);
			sweepwholelist (L, g.allgc);
			sweepwholelist (L, g.fixedgc);  /* collect fixed objects */
			lua_assert (g.strt.size == 0);
		}


		/*
		** advances the garbage collector until it reaches a state allowed
		** by 'statemask'
		*/
		public static void luaC_runtilstate (lua_State L, int statesmask) {
			global_State g = G (L);
			while (testbit (statesmask, g.gcstate) == false)
				lgc.singlestep (L);
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


		/*
		** Performs a full GC cycle; if 'isemergency', set a flag to avoid
		** some operations which could change the interpreter state in some
		** unexpected ways (running finalizers and shrinking some structures).
		** Before running the collection, check 'keepinvariant'; if it is true,
		** there may be some objects marked as black, so the collector has
		** to sweep all objects to turn them back to white (as white has not
		** changed, nothing will be collected).
		*/
		public static void luaC_fullgc (lua_State L, bool isemergency) {
			global_State g = G (L);
			lua_assert (g.gckind == KGC_NORMAL);
			if (isemergency) g.gckind = KGC_EMERGENCY;  /* set flag */
			if (keepinvariant (g)) {  /* black objects? */
				entersweep (L);  /* sweep everything to turn them back to white */
			}
			/* finish any pending sweep phase to start a new cycle */
			luaC_runtilstate (L, bitmask(GCSpause));
			luaC_runtilstate (L, ~bitmask(GCSpause));  /* start new collection */
			luaC_runtilstate (L, bitmask(GCScallfin));  /* run up to finalizers */
			/* estimate must be correct after a full GC cycle */
			lua_assert (g.GCestimate == gettotalbytes (g));
			luaC_runtilstate(L, bitmask(GCSpause));  /* finish collection */
			g.gckind = KGC_NORMAL;
			setpause (g);
		}








    }
}
