using System;

using lua_State = cclua.lua530.lua_State;

namespace cclua {

	public static partial class imp {

		private static class llex {

            public static void next (LexState ls) { ls.current = zgetc (ls.z); }


            public static bool currIsNewline (LexState ls) { return (ls.current == '\n' || ls.current == '\r'); }


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


            public static void save_and_next (LexState ls) { save (ls, ls.current); next (ls); }


            public static void save (LexState ls, int c) {
                MBuffer b = ls.buff;
                if (luaZ_bufflen (b) + 1 > luaZ_sizebuffer (b)) {
                    if (luaZ_sizebuffer (b) > MAX_SIZE / 2)
                        lexerror (ls, "lexical element too long", 0);
                    int newsize = luaZ_sizebuffer (b) * 2;
                    luaZ_resizebuffer (ls.L, b, newsize);
                }
                b.buffer[b.n++] = (byte)c;
            }


            public static string txtToken (LexState ls, RESERVED token) {
                switch (token) {
                    case RESERVED.TK_NAME: goto case RESERVED.TK_INT;
                    case RESERVED.TK_STRING: goto case RESERVED.TK_INT;
                    case RESERVED.TK_FLT: goto case RESERVED.TK_INT;
                    case RESERVED.TK_INT:
                        save (ls, '\0');
                        return luaO_pushfstring (ls.L, "'%s'", luaZ_buffer (ls.buff));
                    default:
                        return luaX_token2str (ls, token);
                }
            }


            public static void lexerror (LexState ls, string msg, int token) {
                byte[] buff = new byte[LUA_IDSIZE];
                luaO_chunkid (buff, getstr (ls.source), LUA_IDSIZE);
                msg = luaO_pushfstring (ls.L, "%s:%d: %s", buff, ls.linenumber, msg);
                if (token != 0)
                    luaO_pushfstring (ls.L, "%s near %s", msg, txtToken (ls, (RESERVED)token));
                luaD_throw (ls.L, lua530.LUA_ERRSYNTAX);
            }
		}


		public const int FIRST_RESERVED = 257;


		public const string LUA_ENV = "_ENV";

		
		/*
		* WARNING: if you change the order of this enumeration,
		* grep "ORDER RESERVED"
		*/
        public enum RESERVED {
            /* terminal symbols denoted by reserved words */
            TK_AND = FIRST_RESERVED, TK_BREAK,
            TK_DO, TK_ELSE, TK_ELSEIF, TK_END, TK_FALSE, TK_FOR, TK_FUNCTION,
            TK_GOTO, TK_IF, TK_IN, TK_LOCAL, TK_NIL, TK_NOT, TK_OR, TK_REPEAT,
            TK_RETURN, TK_THEN, TK_TRUE, TK_UNTIL, TK_WHILE,
            /* other terminal symbols */
            TK_IDIV, TK_CONCAT, TK_DOTS, TK_EQ, TK_GE, TK_LE, TK_NE,
            TK_SHL, TK_SHR,
            TK_DBCOLON, TK_EOS,
            TK_FLT, TK_INT, TK_NAME, TK_STRING
        };


		/* number of reserved words */
        public const int NUM_RESERVED = (int)RESERVED.TK_WHILE - FIRST_RESERVED + 1;


        public class SemInfo {  /* semantics information */
            object o;
        }


        public class Token {
            public int token;
            public SemInfo seminfo;

            public Token () {
                seminfo = new SemInfo ();
            }
        }


        /* state of the lexer plus state of the parser when shared by all
            functions */
        public class LexState {
            public int current;  /* current character (charint) */
            public int linenumber;  /* input line counter */
            public int lastline;  /* line of last token 'consumed' */
            public Token t;  /* current token */
            public Token lookahead;  /* look ahead token */
            public FuncState fs;  /* current function (parser) */
            public lua_State L;
            public Zio z;  /* input stream */
            public MBuffer buff;  /* buffer for tokens */
            public Table h;  /* to avoid collection/reuse strings */
            public Dyndata dyd;  /* dynamic structures used by the parser */
            public TString source;  /* current source name */
            public TString envn;  /* environment variable name */
            public byte decpoint;  /* locale decimal point */

            public LexState () {
                t = new Token ();
                lookahead = new Token ();
            }
        }



		public static void luaX_init (lua_State L) {
			TString e = luaS_new (L, LUA_ENV);  /* create env name */
            luaC_fix (L, obj2gco (e));  /* never collect this name */
			for (int i = 0; i < NUM_RESERVED; i++) {
                TString ts = luaS_new (L, llex.luaX_tokens[i]);
				luaC_fix (L, ts);  /* reserved words are never collected */
				ts.extra = (byte)(i + 1);  /* reserved word */
			}
		}


        public static void luaX_syntaxerror (LexState ls, string msg) {
            llex.lexerror (ls, msg, ls.t.token);
        }


        /*
        ** creates a new string and anchors it in scanner's table so that
        ** it will not be collected until the end of the compilation
        ** (by that time it should be anchored somewhere)
        */
        public static TString luaX_newstring (LexState ls, byte[] str, int l) {
            lua_State L = ls.L;
            TString ts = luaS_newlstr (L, str, l);
            setsvalue2s (L, L.top++, ts);
            TValue o = luaH_set (L, ls.h, L.top - 1);
            if (ttisnil (o)) {
                setbvalue (o, 1);
                luaC_checkGC (L);
            }
            else {
                ts = tsvalue (o);
            }
            L.top--;
            return ts;
        }





	}
}
