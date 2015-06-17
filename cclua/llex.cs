using System;

using cc = cclua.lua530;

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
                return luaX_token2str (ls, (int)token);
                }
            }


            public static void lexerror (LexState ls, string msg, int token) {
                string buff = null;
                luaO_chunkid (ref buff, getsstr (ls.source), cc.LUA_IDSIZE);
                msg = luaO_pushfstring (ls.L, "%s:%d: %s", buff, ls.linenumber, msg);
                if (token != 0)
                    luaO_pushfstring (ls.L, "%s near %s", msg, txtToken (ls, (RESERVED)token));
                luaD_throw (ls.L, cc.LUA_ERRSYNTAX);
            }


            /*
            ** increment line number and skips newline sequence (any of
            ** \n, \r, \n\r, or \r\n)
            */
            public static void inclinenumber (LexState ls) {
                int old = ls.current;
                lua_assert (currIsNewline (ls));
                next (ls);
                if (currIsNewline (ls) && ls.current != old)
                    next (ls);
                if (++ls.linenumber >= MAX_INT)
                    lexerror (ls, "chunk has too many lines", 0);
            }


            public static bool check_next1 (LexState ls, int c) {
                if (ls.current == c) {
                    next (ls);
                    return true;
                }
                else return false;
            }


            /*
            ** Check whether current char is in set 'set' (with two chars) and
            ** saves it
            */
            public static bool check_next2 (LexState ls, string set) {
                lua_assert (set.Length == 2);
                if (ls.current == (int)set[0] || ls.current == (int)set[1]) {
                    save_and_next (ls);
                    return true;
                }
                else return false;
            }


            /*
            ** change all characters 'from' in buffer to 'to'
            */
            public static void buffreplace (LexState ls, byte from, byte to) {
                if (from != to) {
                    int n = luaZ_bufflen (ls.buff);
                    byte[] p = luaZ_buffer (ls.buff);
                    for (int i = 0; i < n; i++)
                        if (p[i] == from) p[i] = to;
                }
            }


            public static bool buff2num (MBuffer b, TValue o) { return (luaO_str2num (luaZ_buffer (b), o) != 0); }


            /*
            ** in case of format error, try to change decimal point separator to
            ** the one defined in the current locale and check again
            */
            public static void trydecpoint (LexState ls, TValue o) {
                // TODO : localeconv
                lexerror (ls, "malformed number", (int)RESERVED.TK_FLT);
            }


            /* LUA_NUMBER */
            /*
            ** this function is quite liberal in what it accepts, as 'luaO_str2num'
            ** will reject ill-formed numerals.
            */
            public static int read_numeral (LexState ls, SemInfo seminfo) {
                TValue obj = new TValue ();
                string expo = "Ee";
                int first = ls.current;
                lua_assert (lisdigit (ls.current));
                save_and_next (ls);
                if (first == '0' && check_next2 (ls, "xX"))
                    expo = "Pp";
                while (true) {
                    if (check_next2 (ls, expo))
                        check_next2 (ls, "-+");
                    if (lisxdigit (ls.current))
                        save_and_next (ls);
                    else if (ls.current == '.')
                        save_and_next (ls);
                    else break;
                }
                save (ls, '\0');
                buffreplace (ls, (byte)'.', (byte)ls.decpoint);
                if (buff2num (ls.buff, obj) == false)
                    trydecpoint (ls, obj);
                if (ttisinteger (obj)) {
                    seminfo.o = ivalue (obj);
                    return (int)RESERVED.TK_INT;
                }
                else {
                    lua_assert (ttisfloat (obj));
                    seminfo.o = fltvalue (obj);
                    return (int)RESERVED.TK_FLT;
                }
            }


            /*
            ** skip a sequence '[=*[' or ']=*]' and return its number of '='s or
            ** -1 if sequence is malformed
            */
            public static int skip_sep (LexState ls) {
                int count = 0;
                int s = ls.current;
                lua_assert (s == '[' || s == ']');
                save_and_next (ls);
                while (ls.current == '=') {
                    save_and_next (ls);
                    count++;
                }
                return (ls.current == s ? count : (-count) - 1);
            }


            public static void read_long_string (LexState ls, SemInfo seminfo, int sep) {
                int line = ls.linenumber;
                save_and_next (ls);
                if (currIsNewline (ls))
                    inclinenumber (ls);
                while (true) {
                    switch (ls.current) {
                        case EOZ: {
                            string what = (seminfo == null ? "string" : "comment");
                            string msg = luaO_pushfstring (ls.L, "unfinished long %s (starting at line %d)", what, line);
                            lexerror (ls, msg, (int)RESERVED.TK_EOS);
                            break;
                        }
                        case ']': {
                            if (skip_sep (ls) == sep) {
                                save_and_next (ls);
                                goto endloop;
                            }
                            break;
                        }
                        case '\n': goto case '\r';
                        case '\r': {
                            save (ls, '\n');
                            inclinenumber (ls);
                            if (seminfo == null)
                                luaZ_resetbuffer (ls.buff);
                            break;
                        }
                        default: {
                            if (seminfo != null) save_and_next (ls);
                            else next (ls);
                            break;
                        }
                    }
                }
            endloop:
                if (seminfo != null)
                    seminfo.o = luaX_newstring (ls, luaZ_buffer (ls.buff), 2 + sep, luaZ_bufflen (ls.buff) - 2 * (2 + sep));
            }


            public static void esccheck (LexState ls, bool c, string msg) {
                if (c == false) {
                    if (ls.current != EOZ)
                        save_and_next (ls);  /* add current to buffer for error message */
                    lexerror (ls, msg, (int)RESERVED.TK_STRING);
                }
            }


            public static int gethexa (LexState ls) {
                save_and_next (ls);
                esccheck (ls, lisxdigit (ls.current), "hexadecimal digit expected");
                return luaO_hexavalue (ls.current);
            }


            public static int readhexaesc (LexState ls) {
                int r = gethexa (ls);
                r = (r << 4) + gethexa (ls);
                luaZ_buffremove (ls.buff, 2);
                return r;  /* remove saved chars from buffer */
            }


            public static ulong readutf8esc (LexState ls) {
                int i = 4;  /* chars to be removed: '\', 'u', '{', and first digit */
                save_and_next (ls);  /* skip 'u' */
                esccheck (ls, ls.current == '{', "missing '{'");
                ulong r = (ulong)gethexa (ls);  /* must have at least one digit */
                save_and_next (ls);
                while (lisxdigit (ls.current)) {
                    i++;
                    r = (ulong)(r << 4) + (ulong)luaO_hexavalue (ls.current);
                    esccheck (ls, r <= 0x10FFFF, "UTF-8 value too large");
                    save_and_next (ls);
                }
                esccheck (ls, ls.current == '}', "missing '}'");
                next (ls);  /* skip '}' */
                luaZ_buffremove (ls.buff, i);  /* remove saved chars from buffer */
                return r;
            }


            public static void utf8esc (LexState ls) {
                byte[] buff = new byte[UTF8BUFFSZ];
                int n = luaO_utf8esc (buff, readutf8esc (ls));
                for (; n > 0; n--)
                    save (ls, buff[UTF8BUFFSZ - n]);
            }


            public static int readdecesc (LexState ls) {
                int r = 0;  /* result accumulator */
                int i = 0;
                for (; i < 3 && lisdigit (ls.current); i++) {  /* read up to 3 digits */
                    r = 10 * r + ls.current - '0';
                    save_and_next (ls);
                }
                esccheck (ls, r <= UCHAR_MAX, "decimal escape too large");
                luaZ_buffremove (ls.buff, i);  /* remove read digits from buffer */
                return r;
            }


            public static void read_string (LexState ls, int del, SemInfo seminfo) {
                save_and_next (ls);  /* keep delimiter (for error messages) */
                while (ls.current != del) {
                    switch (ls.current) {
                        case EOZ: {
                            lexerror (ls, "unfinished string", (int)RESERVED.TK_EOS);
                            break;  /* to avoid warnings */
                        }
                        case '\n': goto case '\r';
                        case '\r': {
                            lexerror (ls, "unfinished string", (int)RESERVED.TK_STRING);
                            break;  /* to avoid warnings */
                        }
                        case '\\': {  /* escape sequences */
                            save_and_next (ls);  /* keep '\\' for error messages */
                            int c = 0;  /* final character to be saved */
                            switch (ls.current) {
                                case 'a': c = '\a'; goto read_save;
                                case 'b': c = '\b'; goto read_save;
                                case 'f': c = '\f'; goto read_save;
                                case 'n': c = '\n'; goto read_save;
                                case 'r': c = '\r'; goto read_save;
                                case 't': c = '\t'; goto read_save;
                                case 'v': c = '\v'; goto read_save;
                                case 'x': c = readhexaesc (ls); goto read_save;
                                case 'u': utf8esc (ls); goto no_save;
                                case '\n': goto case '\r';
                                case '\r':
                                    inclinenumber (ls); c = '\n'; goto no_save;
                                case '\\': goto case '\'';
                                case '\"': goto case '\'';
                                case '\'':
                                    c = ls.current; goto read_save;
                                case EOZ: goto no_save;  /* will raise an error next loop */
                                case 'z': {  /* zap following span of spaces */
                                    luaZ_buffremove (ls.buff, 1);  /* remove '\\' */
                                    next (ls);  /* skip the 'z' */
                                    while (lisspace (ls.current)) {
                                        if (currIsNewline (ls)) inclinenumber (ls);
                                        else next (ls);
                                    }
                                    goto no_save;
                                }
                                default: {
                                    esccheck (ls, lisdigit (ls.current), "invalid escape sequence");
                                    c = readdecesc (ls);  /* digital escape '\ddd' */
                                    goto only_save;
                                }
                            }
                        read_save:
                            next (ls);
                            /* go through */
                        only_save:
                            luaZ_buffremove (ls.buff, 1);
                            save (ls, c);
                            /* go through */
                        no_save:
                            break;
                        }
                        default:
                        save_and_next (ls);
                        break;
                    }
                }
                save_and_next (ls);
                seminfo.o = luaX_newstring (ls, luaZ_buffer (ls.buff), 1, luaZ_bufflen (ls.buff) - 2);
            }


            public static int dolex (LexState ls, SemInfo seminfo) {
                luaZ_resetbuffer (ls.buff);
                while (true) {
                    switch (ls.current) {
                        case '\n': goto case '\r';
                        case '\r': {
                            inclinenumber (ls);
                            break;
                        }
                        case ' ': goto case '\v';
                        case '\f': goto case '\v';
                        case '\t': goto case '\v';
                        case '\v': {
                            next (ls);
                            break;
                        }
                        case '-': {  /* '-' or '--' (comment) */
                            next (ls);
                            if (ls.current != '-') return '-';
                            /* else is a comment */
                            next (ls);
                            if (ls.current == '[') {  /* long comment? */
                                int sep = skip_sep (ls);
                                luaZ_resetbuffer (ls.buff);  /* 'skip_sep' may dirty the buffer */
                                if (sep >= 0) {
                                    read_long_string (ls, null, sep);  /* skip long comment */
                                    luaZ_resetbuffer (ls.buff);  /* previous call may dirty the buff. */
                                    break;
                                }
                            }
                            /* else short comment */
                            while (currIsNewline (ls) == false && ls.current != EOZ)
                                next (ls);  /* skip until end of line (or end of file) */
                            break;
                        }
                        case '[': {  /* long string or simply '[' */
                            int sep = skip_sep (ls);
                            if (sep >= 0) {
                                read_long_string (ls, seminfo, sep);
                                return (int)RESERVED.TK_STRING;
                            }
                            else if (sep == -1) return '[';
                            else lexerror (ls, "invalid long string delimiter", (int)RESERVED.TK_STRING);
                            break;
                        }
                        case '=': {
                            next (ls);
                            if (check_next1 (ls, '=')) return (int)RESERVED.TK_EQ;
                            else return '=';
                        }
                        case '<': {
                            next (ls);
                            if (check_next1 (ls, '=')) return (int)RESERVED.TK_LE;
                            else if (check_next1 (ls, '<')) return (int)RESERVED.TK_SHL;
                            else return '<';
                        }
                        case '>': {
                            next (ls);
                            if (check_next1 (ls, '=')) return (int)RESERVED.TK_GE;
                            else if (check_next1 (ls, '>')) return (int)RESERVED.TK_SHR;
                            else return '>';
                        }
                        case '/': {
                            next (ls);
                            if (check_next1 (ls, '/')) return (int)RESERVED.TK_IDIV;
                            else return '/';
                        }
                        case '~': {
                            next (ls);
                            if (check_next1 (ls, '=')) return (int)RESERVED.TK_NE;
                            else return '~';
                        }
                        case ':': {
                            next (ls);
                            if (check_next1 (ls, ':')) return (int)RESERVED.TK_DBCOLON;
                            else return ':';
                        }
                        case '"': goto case '\'';  /* short literal strings */
                        case '\'': {
                            read_string (ls, ls.current, seminfo);
                            return (int)RESERVED.TK_STRING;
                        }
                        case '.': {  /* '.', '..', '...', or number */
                            save_and_next (ls);
                            if (check_next1 (ls, '.')) {
                                if (check_next1 (ls, '.'))
                                    return (int)RESERVED.TK_DOTS;
                                else return (int)RESERVED.TK_CONCAT;
                            }
                            else if (lisdigit (ls.current) == false) return '.';
                            else return read_numeral (ls, seminfo);
                        }
                        case '0': goto case '9';
                        case '1': goto case '9';
                        case '2': goto case '9';
                        case '3': goto case '9';
                        case '4': goto case '9';
                        case '5': goto case '9';
                        case '6': goto case '9';
                        case '7': goto case '9';
                        case '8': goto case '9';
                        case '9': {
                            return read_numeral (ls, seminfo);
                        }
                        case EOZ: {
                            return (int)RESERVED.TK_EOS;
                        }
                        default: {
                            if (lislalpha (ls.current)) {  /* identifier or reserved word? */
                                do {
                                    save_and_next (ls);
                                } while (lislalnum (ls.current));
                                TString ts = luaX_newstring (ls, luaZ_buffer (ls.buff), luaZ_bufflen (ls.buff));
                                seminfo.o = ts;
                                if (isreserved (ts))
                                    return (ts.extra - 1 + FIRST_RESERVED);
                                else {
                                    return (int)RESERVED.TK_NAME;
                                }
                            }
                            else {  /* single-char tokens (+ - / ...) */
                                int c = ls.current;
                                next (ls);
                                return c;
                            }
                        }
                    }
                }
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
            public object o;
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


        public static string luaX_token2str (LexState ls, int token) {
            if (token < FIRST_RESERVED) {  /* single-byte symbols? */
                cc.lua_assert (token = (byte)token);
                return luaO_pushfstring (ls.L, "'%c'", token);
            }
            else {
                string s = llex.luaX_tokens[token - FIRST_RESERVED];
                if (token < (int)RESERVED.TK_EOS)  /* fixed format (symbols and reserved words)? */
                    return luaO_pushfstring (ls.L, "'%s'", s);
                else  /* names, strings, and numerals */
                    return s;
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
        public static TString luaX_newstring (LexState ls, byte[] str, int offset, int l) {
            lua_State L = ls.L;
            TString ts = luaS_newlstr (L, str, offset, l);
            setsvalue2s (L, L.top++, ts);
            TValue o = luaH_set (L, ls.h, L.stack[L.top - 1]);
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
        public static TString luaX_newstring (LexState ls, byte[] str, int l) { return luaX_newstring (ls, str, 0, l); }


        public static void luaX_setinput (lua_State L, LexState ls, Zio z, TString source, int firstchar) {
            ls.t.token = 0;
            ls.decpoint = (byte)'.';
            ls.L = L;
            ls.current = firstchar;
            ls.lookahead.token = (int)RESERVED.TK_EOS;  /* no look-ahead token */
            ls.z = z;
            ls.fs = null;
            ls.linenumber = 1;
            ls.lastline = 1;
            ls.source = source;
            ls.envn = luaS_new (L, LUA_ENV);  /* get env name */
            luaZ_resizebuffer (ls.L, ls.buff, cc.LUA_MINBUFFER);  /* initialize buffer */
        }


        public static void luaX_next (LexState ls) {
            ls.lastline = ls.linenumber;
            if (ls.lookahead.token != (int)RESERVED.TK_EOS) {
                ls.t = ls.lookahead;
                ls.lookahead.token = (int)RESERVED.TK_EOS;
            }
            else
                ls.t.token = llex.dolex (ls, ls.t.seminfo);
        }


        public static int luaX_lookahead (LexState ls) {
            lua_assert (ls.lookahead.token == (int)RESERVED.TK_EOS);
            ls.lookahead.token = llex.dolex (ls, ls.lookahead.seminfo);
            return ls.lookahead.token;
        }
    }
}
