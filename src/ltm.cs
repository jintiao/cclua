using System;

using lua_State = cclua.lua530.lua_State;

namespace cclua {

	public static partial class imp {
		private static class ltm {

            public static string udatatypename = "userdata";

            public static string[] luaT_typenames_ = {
              "no value",
              "nil", "boolean", udatatypename, "number",
              "string", "table", "function", udatatypename, "thread",
              "proto" /* this last case is used for tests only */
            };


			public static string[] luaT_eventname = {  /* ORDER TM */
				"__index", "__newindex",
				"__gc", "__mode", "__len", "__eq",
				"__add", "__sub", "__mul", "__mod", "__pow",
				"__div", "__idiv",
				"__band", "__bor", "__bxor", "__shl", "__shr",
				"__unm", "__bnot", "__lt", "__le",
				"__concat", "__call"
			};
		}



		/*
		* WARNING: if you change the order of this enumeration,
		* grep "ORDER TM" and "ORDER OP"
		*/
        public enum TMS {
          TM_INDEX,
          TM_NEWINDEX,
          TM_GC,
          TM_MODE,
          TM_LEN,
          TM_EQ,  /* last tag method with fast access */
          TM_ADD,
          TM_SUB,
          TM_MUL,
          TM_MOD,
          TM_POW,
          TM_DIV,
          TM_IDIV,
          TM_BAND,
          TM_BOR,
          TM_BXOR,
          TM_SHL,
          TM_SHR,
          TM_UNM,
          TM_BNOT,
          TM_LT,
          TM_LE,
          TM_CONCAT,
          TM_CALL,
          TM_N		/* number of elements in the enum */
        } ;



        public static string ttypename (int x) { return ltm.luaT_typenames_[x + 1]; }


		public static void luaT_init (lua530.lua_State L) {
			for (int i = 0; i < (int)TMS.TM_N; i++) {
                G (L).tmname[i] = luaS_new (L, ltm.luaT_eventname[i]);
                luaC_fix (L, G (L).tmname[i]);  /* never collect these names */
			}
		}


        /*
        ** function to be used with macro "fasttm": optimized for absence of
        ** tag methods
        */
        public static TValue luaT_gettm (Table events, TMS ev, TString ename) {
            TValue tm = luaH_getstr (events, ename);
            lua_assert (ev <= TMS.TM_EQ);
            if (ttisnil (tm)) {  /* no tag method? */
                events.flags |= (byte)(1 << (byte)ev);  /* cache this fact */
                return null;
            }
            return tm;
        }


        public static TValue luaT_gettmbyobj (lua_State L, TValue o, TMS ev) {
            Table mt;
            switch (ttnov (o)) {
                case lua530.LUA_TTABLE:
                    mt = hvalue (o).metatable;
                    break;
                case lua530.LUA_TUSERDATA:
                    mt = uvalue (o).metatable;
                    break;
                default:
                    mt = G (L).mt[ttnov (o)];
                    break;
            }
            return ((mt == null) ? luaH_getstr (mt, G (L).tmname[(byte)ev]) : luaO_nilobject);
        }









	}
}
