using System;

using lua_State = cclua.lua530.lua_State;

namespace cclua {

	public static partial class imp {

		private static class llex {

			/* ORDER RESERVED */
			public static string[] luaX_tokens = {
				"and", "break", "do", "else", "elseif",
				"end", "false", "for", "function", "goto", "if",
				"in", "local", "nil", "not", "or", "repeat",
				"return", "then", "true", "until", "while",
				"//", "..", "...", "==", ">=", "<=", "~=",
				"<<", ">>", "::", "<eof>",
				"<number>", "<integer>", "<name>", "<string>"
			};
		}

		public const int FIRST_RESERVED = 257;

		public const string LUA_ENV = "_ENV";

		
		/*
		* WARNING: if you change the order of this enumeration,
		* grep "ORDER RESERVED"
		*/

		/* terminal symbols denoted by reserved words */
		public const int TK_AND = FIRST_RESERVED;
		public const int TK_BREAK = FIRST_RESERVED + 1;
		public const int TK_DO = FIRST_RESERVED + 2;
		public const int TK_ELSE = FIRST_RESERVED + 3;
		public const int TK_ELSEIF = FIRST_RESERVED + 4;
		public const int TK_END = FIRST_RESERVED + 5;
		public const int TK_FALSE = FIRST_RESERVED + 6;
		public const int TK_FOR = FIRST_RESERVED + 7;
		public const int TK_FUNCTION = FIRST_RESERVED + 8;
		public const int TK_GOTO = FIRST_RESERVED + 9;
		public const int TK_IF = FIRST_RESERVED + 10;
		public const int TK_IN = FIRST_RESERVED + 11;
		public const int TK_LOCAL = FIRST_RESERVED + 12;
		public const int TK_NIL = FIRST_RESERVED + 13;
		public const int TK_NOT = FIRST_RESERVED + 14;
		public const int TK_OR = FIRST_RESERVED + 15;
		public const int TK_REPEAT = FIRST_RESERVED + 16;
		public const int TK_RETURN = FIRST_RESERVED + 17;
		public const int TK_THEN = FIRST_RESERVED + 18;
		public const int TK_TRUE = FIRST_RESERVED + 19;
		public const int TK_UNTIL = FIRST_RESERVED + 20;
		public const int TK_WHILE = FIRST_RESERVED + 21;
		/* other terminal symbols */
		public const int TK_IDIV = FIRST_RESERVED + 22;
		public const int TK_CONCAT = FIRST_RESERVED + 23;
		public const int TK_DOTS = FIRST_RESERVED + 24;
		public const int TK_EQ = FIRST_RESERVED + 25;
		public const int TK_GE = FIRST_RESERVED + 26;
		public const int TK_LE = FIRST_RESERVED + 27;
		public const int TK_NE = FIRST_RESERVED + 28;
		public const int TK_SHL = FIRST_RESERVED + 29;
		public const int TK_SHR = FIRST_RESERVED + 30;
		public const int TK_DBCOLON = FIRST_RESERVED + 31;
		public const int TK_EOS = FIRST_RESERVED + 32;
		public const int TK_FLT = FIRST_RESERVED + 33;
		public const int TK_INT = FIRST_RESERVED + 34;
		public const int TK_NAME = FIRST_RESERVED + 35;
		public const int TK_STRING = FIRST_RESERVED + 36;

		/* number of reserved words */
		public const int NUM_RESERVED = TK_WHILE - FIRST_RESERVED + 1;


		public static void luaX_init (lua_State L) {
			TString e = luaS_new (L, LUA_ENV);  /* create env name */
			luaC_fix (L, e);
			for (int i = 0; i < NUM_RESERVED; i++) {
                TString ts = luaS_new (L, llex.luaX_tokens[i]);
				luaC_fix (L, ts);  /* reserved words are never collected */
				ts.extra = (byte)(i + 1);  /* reserved word */
			}
		}
	}
}
