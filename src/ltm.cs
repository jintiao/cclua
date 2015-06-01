using System;

namespace cclua {

	public static partial class imp {
		private static class ltm {

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
		public const int TM_INDEX = 0;
		public const int TM_NEWINDEX = 1;
		public const int TM_GC = 2;
		public const int TM_MODE = 3;
		public const int TM_LEN = 4;
		public const int TM_EQ = 5;  /* last tag method with fast access */
		public const int TM_ADD = 6;
		public const int TM_SUB = 7;
		public const int TM_MUL = 8;
		public const int TM_MOD = 9;
		public const int TM_POW = 10;
		public const int TM_DIV = 11;
		public const int TM_IDIV = 12;
		public const int TM_BAND = 13;
		public const int TM_BOR = 14;
		public const int TM_BXOR = 15;
		public const int TM_SHL = 16;
		public const int TM_SHR = 17;
		public const int TM_UNM = 18;
		public const int TM_BNOT = 19;
		public const int TM_LT = 20;
		public const int TM_LE = 21;
		public const int TM_CONCAT = 22;
		public const int TM_CALL = 23;
		public const int TM_N = 24;  /* number of elements in the enum */


		public static void luaT_init (lua530.lua_State L) {
			for (int i = 0; i < TM_N; i++) {
                TString str = luaS_new (L, ltm.luaT_eventname[i]);
				G (L).tmname[i] = str;
				luaC_fix (L, str);
			}
		}
	}
}
