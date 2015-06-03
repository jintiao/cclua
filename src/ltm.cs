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


        public static TValue gfasttm (global_State g, Table et, TMS e) {
            if (et == null) return null;
            if ((et.flags & (1 << (int)e)) != 0) return null;
            return luaT_gettm (et, e, g.tmname[(int)e]);
        }

        public static TValue fasttm (lua_State L, Table et, TMS e) { return gfasttm (G (L), et, e); }



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


        public static void luaT_callTM (lua_State L, TValue f, TValue p1, TValue p2, int p3, int hasres) {
            int result = savestack (L, p3);
            setobj2s (L, L.top++, f);  /* push function (assume EXTRA_STACK) */
            setobj2s (L, L.top++, p1);  /* 1st argument */
            setobj2s (L, L.top++, p2);  /* 2nd argument */
            if (hasres == 0)  /* no result? 'p3' is third argument */
                setobj2s (L, L.top++, p3);  /* 3rd argument */
            /* metamethod may yield only when called from Lua code */
            luaD_call (L, L.top - (4 - hasres), hasres, (isLua (L.ci) ? 1 : 0));
            if (hasres != 0) {  /* if has result, move it to its place */
                p3 = restorestack (L, result);
                setobjs2s (L, p3, --L.top);
            }
        }


        public static bool luaT_callbinTM (lua_State L, TValue p1, TValue p2, int res, TMS ev) {
            TValue tm = luaT_gettmbyobj (L, p1, ev);  /* try first operand */
            if (ttisnil (tm))
                tm = luaT_gettmbyobj (L, p2, ev);  /* try second operand */
            if (ttisnil (tm)) return false;
            luaT_callTM (L, tm, p1, p2, res, 1);
            return true;
        }


        public static void luaT_trybinTM (lua_State L, TValue p1, TValue p2, int res, TMS ev) {
            if (luaT_callbinTM (L, p1, p2, res, ev) == false) {
                if (ev == TMS.TM_CONCAT)
                    luaG_concaterror (L, p1, p2);
                else if (ev == TMS.TM_BAND || ev == TMS.TM_BOR || ev == TMS.TM_BXOR || ev == TMS.TM_SHL || ev == TMS.TM_SHR || ev == TMS.TM_BNOT) {
                    double dummy = 0;
                    if (tonumber (p1, ref dummy) && tonumber (p2, ref dummy))
                        luaG_tointerror (L, p1, p2);
                    else
                        luaG_opinterror (L, p1, p2, "perform bitwise operation on");
                }
                else
                    luaG_opinterror (L, p1, p2, "perform arithmetic on");
            }
        }
        public static void luaT_trybinTM (lua_State L, int p1, int p2, int res, TMS ev) { luaT_trybinTM (L, L.stack[p1], L.stack[p2], res, ev); }


        public static int luaT_callorderTM (lua_State L, TValue p1, TValue p2, TMS ev) {
            if (luaT_callbinTM (L, p1, p2, L.top, ev) == false)
                return -1;  /* no metamethod */
            else
                return (l_isfalse (L.stack[L.top]) ? 0 : 1);
        }






	}
}
