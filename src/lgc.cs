﻿using System;

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

			public static void luaC_barrierback_ (lua_State L, Table t) {
			}
		}

		/* how much to allocate before next GC step */
		public const int GCSTEPSIZE = 100 * sizeof (TString);  /* ~100 small strings */


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
		public static void resetbits (int x, int m) { x &= (byte)(~m); }
		public static void setbits (int x, int m) { x |= m; }
		public static bool testbits (int x, int m) { return ((x & m) != 0); }
		public static int bitmask (int b) { return (1 << b); }
		public static int bit2mask (int b1, int b2) { return (bitmask(b1) | bitmask(b2)); }
		public static void l_setbit (int x, int b) { setbits (x, bitmask (b)); }
		public static void resetbit (int x, int b) { resetbits (x, bitmask (b)); }
		public static void testbit (int x, int b) { testbits (x, bitmask (b)); }


		/* Layout for bit use in 'marked' field: */
		public const int WHITE0BIT = 0;  /* object is white (type 0) */
		public const int WHITE1BIT = 1;  /* object is white (type 1) */
		public const int BLACKBIT = 2;  /* object is black */
		public const int FINALIZEDBIT = 3;  /* object has been marked for finalization */
		/* bit 7 is currently used by tests (luaL_checkmemory) */

		public const int WHITEBITS = bit2mask (WHITE0BIT, WHITE1BIT);


		public static bool iswhite (GCObject x) { return testbits (x.marked, WHITEBITS); }
		public static bool isblack (GCObject x) { return testbit (x.marked, BLACKBIT); }
		/* neither white nor black */
		public static bool isgray (GCObject x) { return (testbits (x.marked, WHITEBITS | (bitmask (BLACKBIT))) == false); }

		public static bool tofinalize (GCObject x) { return testbit (x.marked, FINALIZEDBIT); }

		public static int otherwhite (global_State g) { return (g.currentwhite ^ WHITEBITS); }
		public static bool isdeadm (int ow, int m) { return (((m ^ WHITEBITS) & ow) == 0); }
		public static bool isdead (global_State g, GCObject x) { return isdeadm (otherwhite (g), x.marked); }

		public static void changewhite (GCObject x) { x.marked ^= WHITEBITS; }
		public static void gray2black (GCObject x) { l_setbit (x.marked, BLACKBIT); }
		
		public static byte luaC_white (global_State g) { return (g.currentwhite & WHITEBITS); }




		public static void luaC_barrierback (lua_State L, Table p, TValue v) {
			if (iscollectable (p) && isblack (p) && iswhite (gcvalue (v)))
				luaC_barrierback_ (L, p);
		}



        public static T luaC_newobj<T> (lua_State L, int tt) where T : GCObject, new () {
            T o = luaM_newobject<T> ();
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
    }
}
